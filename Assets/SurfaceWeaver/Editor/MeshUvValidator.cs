using System;
using System.Collections.Generic;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Validates mesh UV data and performs bounded UV triangle-overlap and geometric UV-seam analysis.
    /// This is a diagnostic tool: it does not automatically unwrap or modify the source mesh.
    /// </summary>
    public static class MeshUvValidator
    {
        private const float UvAreaEpsilon = 0.0000001f;
        private const float RangeEpsilon = 0.0001f;
        private const float PositionQuantization = 100000f;
        private const float UvEqualityEpsilon = 0.00001f;
        private const long MaxOverlapPairTests = 5000000L;

        private sealed class UvTriangle
        {
            public int A;
            public int B;
            public int C;
            public Vector2 UvA;
            public Vector2 UvB;
            public Vector2 UvC;
            public float MinX;
            public float MinY;
            public float MaxX;
            public float MaxY;
            public float SignedArea;
        }

        private struct QuantizedPoint3 : IEquatable<QuantizedPoint3>, IComparable<QuantizedPoint3>
        {
            public long X;
            public long Y;
            public long Z;

            public QuantizedPoint3(Vector3 value)
            {
                X = (long)Math.Round(value.x * PositionQuantization);
                Y = (long)Math.Round(value.y * PositionQuantization);
                Z = (long)Math.Round(value.z * PositionQuantization);
            }

            public bool Equals(QuantizedPoint3 other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is QuantizedPoint3 other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = X.GetHashCode();
                    hash = (hash * 397) ^ Y.GetHashCode();
                    hash = (hash * 397) ^ Z.GetHashCode();
                    return hash;
                }
            }

            public int CompareTo(QuantizedPoint3 other)
            {
                int x = X.CompareTo(other.X);
                if (x != 0) return x;
                int y = Y.CompareTo(other.Y);
                return y != 0 ? y : Z.CompareTo(other.Z);
            }
        }

        private struct GeometricEdgeKey : IEquatable<GeometricEdgeKey>
        {
            public QuantizedPoint3 A;
            public QuantizedPoint3 B;

            public GeometricEdgeKey(QuantizedPoint3 first, QuantizedPoint3 second)
            {
                if (first.CompareTo(second) <= 0)
                {
                    A = first;
                    B = second;
                }
                else
                {
                    A = second;
                    B = first;
                }
            }

            public bool Equals(GeometricEdgeKey other)
            {
                return A.Equals(other.A) && B.Equals(other.B);
            }

            public override bool Equals(object obj)
            {
                return obj is GeometricEdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked { return (A.GetHashCode() * 397) ^ B.GetHashCode(); }
            }
        }

        private struct UvEdgeMapping
        {
            public Vector2 UvAtA;
            public Vector2 UvAtB;
        }

        public static MeshUvValidationReport Validate(Mesh mesh, int uvChannel)
        {
            MeshUvValidationReport report = new MeshUvValidationReport();

            if (mesh == null)
            {
                report.Errors.Add("No mesh was found on the selected target.");
                return report;
            }

            report.VertexCount = mesh.vertexCount;
            report.MeshReadable = mesh.isReadable;
            if (!mesh.isReadable)
            {
                report.Errors.Add(
                    "The mesh is not readable. Enable Read/Write in the model import settings, " +
                    "then reimport the model before running UV diagnostics.");
                return report;
            }

            if (uvChannel < 0 || uvChannel > 7)
            {
                report.Errors.Add("UV channel must be between 0 and 7.");
                return report;
            }

            List<Vector2> uvs = new List<Vector2>(mesh.vertexCount);
            mesh.GetUVs(uvChannel, uvs);
            report.UvCount = uvs.Count;
            if (uvs.Count == 0)
            {
                report.Errors.Add("The selected UV channel is empty.");
                return report;
            }

            if (uvs.Count != mesh.vertexCount)
            {
                report.Errors.Add(
                    "UV count does not match the mesh vertex count. Check the imported mesh and selected UV channel.");
                return report;
            }

            float minU = float.PositiveInfinity;
            float minV = float.PositiveInfinity;
            float maxU = float.NegativeInfinity;
            float maxV = float.NegativeInfinity;

            for (int i = 0; i < uvs.Count; i++)
            {
                Vector2 uv = uvs[i];
                if (!IsFinite(uv.x) || !IsFinite(uv.y))
                {
                    report.NonFiniteUvCount++;
                    continue;
                }

                minU = Mathf.Min(minU, uv.x);
                minV = Mathf.Min(minV, uv.y);
                maxU = Mathf.Max(maxU, uv.x);
                maxV = Mathf.Max(maxV, uv.y);

                if (uv.x < -RangeEpsilon || uv.x > 1f + RangeEpsilon ||
                    uv.y < -RangeEpsilon || uv.y > 1f + RangeEpsilon)
                    report.OutOfRangeUvCount++;
            }

            if (report.NonFiniteUvCount > 0)
                report.Errors.Add(report.NonFiniteUvCount + " UV coordinate(s) contain NaN or Infinity.");

            if (report.OutOfRangeUvCount > 0)
                report.Warnings.Add(report.OutOfRangeUvCount +
                    " UV coordinate(s) fall outside the 0–1 range. The current mask workflow expects normalized UVs.");

            if (IsFinite(minU) && IsFinite(minV) && IsFinite(maxU) && IsFinite(maxV) &&
                ((maxU - minU) < RangeEpsilon || (maxV - minV) < RangeEpsilon))
                report.Errors.Add("The selected UV channel has effectively zero area.");

            List<UvTriangle> triangles = new List<UvTriangle>();
            List<Vector3> vertices = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(vertices);
            Dictionary<GeometricEdgeKey, List<UvEdgeMapping>> edgeMappings =
                new Dictionary<GeometricEdgeKey, List<UvEdgeMapping>>();

            int unsupportedSubmeshes = 0;
            int invalidIndexCount = 0;

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                {
                    unsupportedSubmeshes++;
                    continue;
                }

                int[] indices = mesh.GetIndices(submesh);
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    int ia = indices[i];
                    int ib = indices[i + 1];
                    int ic = indices[i + 2];
                    if (!IsValidIndex(ia, uvs.Count) || !IsValidIndex(ib, uvs.Count) ||
                        !IsValidIndex(ic, uvs.Count) || !IsValidIndex(ia, vertices.Count) ||
                        !IsValidIndex(ib, vertices.Count) || !IsValidIndex(ic, vertices.Count))
                    {
                        invalidIndexCount++;
                        continue;
                    }

                    Vector2 a = uvs[ia];
                    Vector2 b = uvs[ib];
                    Vector2 c = uvs[ic];
                    if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
                        continue;

                    float signedArea = Cross(b - a, c - a) * 0.5f;
                    if (Mathf.Abs(signedArea) <= UvAreaEpsilon)
                        report.DegenerateUvTriangleCount++;
                    else
                        triangles.Add(CreateTriangle(ia, ib, ic, a, b, c, signedArea));

                    report.TriangleCount++;
                    AddEdgeMapping(edgeMappings, vertices, uvs, ia, ib);
                    AddEdgeMapping(edgeMappings, vertices, uvs, ib, ic);
                    AddEdgeMapping(edgeMappings, vertices, uvs, ic, ia);
                }
            }

            if (unsupportedSubmeshes > 0)
                report.Warnings.Add(unsupportedSubmeshes +
                    " submesh(es) do not use triangle topology. Non-triangle submeshes were skipped.");
            if (report.TriangleCount == 0)
                report.Errors.Add("No triangle faces were found in the selected mesh topology.");
            if (invalidIndexCount > 0)
                report.Errors.Add(invalidIndexCount + " triangle index reference(s) are outside the mesh arrays.");
            if (report.DegenerateUvTriangleCount > 0)
                report.Warnings.Add(report.DegenerateUvTriangleCount +
                    " triangle(s) have zero or near-zero UV area. These faces may not paint correctly.");

            report.UvSeamEdgeCount = CountUvSeamEdges(edgeMappings);
            if (report.UvSeamEdgeCount > 0)
                report.Warnings.Add(report.UvSeamEdgeCount +
                    " geometric edge(s) have discontinuous UV coordinates. Brush stamps may need seam-aware dilation or cross-island handling.");
            else
                report.Infos.Add("No UV seams were detected between shared geometric edges (within the configured positional tolerance).");

            AnalyzeOverlaps(triangles, report);

            if (report.OutOfRangeUvCount == 0 && report.NonFiniteUvCount == 0)
                report.Infos.Add("All UV coordinates are finite and inside the 0–1 range.");

            return report;
        }

        private static UvTriangle CreateTriangle(
            int aIndex, int bIndex, int cIndex,
            Vector2 a, Vector2 b, Vector2 c, float signedArea)
        {
            return new UvTriangle
            {
                A = aIndex, B = bIndex, C = cIndex,
                UvA = a, UvB = b, UvC = c,
                MinX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)),
                MinY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)),
                MaxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)),
                MaxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y)),
                SignedArea = signedArea
            };
        }

        private static void AnalyzeOverlaps(List<UvTriangle> triangles, MeshUvValidationReport report)
        {
            triangles.Sort((a, b) => a.MinX.CompareTo(b.MinX));
            report.OverlapAnalysisCompleted = true;

            for (int i = 0; i < triangles.Count; i++)
            {
                UvTriangle a = triangles[i];
                for (int j = i + 1; j < triangles.Count; j++)
                {
                    UvTriangle b = triangles[j];
                    if (b.MinX >= a.MaxX - 0.0000001f)
                        break;
                    if (b.MinY >= a.MaxY - 0.0000001f || b.MaxY <= a.MinY + 0.0000001f)
                        continue;

                    report.OverlapCandidatePairTests++;
                    if (report.OverlapCandidatePairTests > MaxOverlapPairTests)
                    {
                        report.OverlapAnalysisCompleted = false;
                        report.Warnings.Add(
                            "UV overlap analysis reached its safety limit of " + MaxOverlapPairTests +
                            " candidate pairs. Results are partial; simplify or split the mesh for a complete scan.");
                        return;
                    }

                    if (TrianglesOverlapWithPositiveArea(a, b))
                        report.OverlappingUvTrianglePairCount++;
                }
            }

            if (report.OverlappingUvTrianglePairCount > 0)
                report.Warnings.Add(report.OverlappingUvTrianglePairCount +
                    " pair(s) of UV triangles overlap with positive area. Painting one region may affect multiple mesh regions.");
            else
                report.Infos.Add("No positive-area UV triangle overlaps were detected.");
        }

        // Separating-axis test for two 2D triangles. We require strictly positive
        // projection overlap, so triangles that only share an edge or vertex are not
        // reported as UV overlaps. This avoids per-pair polygon allocations.
        private static bool TrianglesOverlapWithPositiveArea(UvTriangle a, UvTriangle b)
        {
            return ProjectionHasPositiveOverlap(a, b, a.UvB - a.UvA) &&
                   ProjectionHasPositiveOverlap(a, b, a.UvC - a.UvB) &&
                   ProjectionHasPositiveOverlap(a, b, a.UvA - a.UvC) &&
                   ProjectionHasPositiveOverlap(a, b, b.UvB - b.UvA) &&
                   ProjectionHasPositiveOverlap(a, b, b.UvC - b.UvB) &&
                   ProjectionHasPositiveOverlap(a, b, b.UvA - b.UvC);
        }

        private static bool ProjectionHasPositiveOverlap(UvTriangle a, UvTriangle b, Vector2 edge)
        {
            Vector2 axis = new Vector2(-edge.y, edge.x);
            float lengthSquared = axis.sqrMagnitude;
            if (lengthSquared <= 0.0000000001f)
                return false;
            axis /= Mathf.Sqrt(lengthSquared);

            float a0 = Vector2.Dot(a.UvA, axis);
            float a1 = Vector2.Dot(a.UvB, axis);
            float a2 = Vector2.Dot(a.UvC, axis);
            float b0 = Vector2.Dot(b.UvA, axis);
            float b1 = Vector2.Dot(b.UvB, axis);
            float b2 = Vector2.Dot(b.UvC, axis);

            float minA = Mathf.Min(a0, Mathf.Min(a1, a2));
            float maxA = Mathf.Max(a0, Mathf.Max(a1, a2));
            float minB = Mathf.Min(b0, Mathf.Min(b1, b2));
            float maxB = Mathf.Max(b0, Mathf.Max(b1, b2));
            return Mathf.Min(maxA, maxB) - Mathf.Max(minA, minB) > 0.0000001f;
        }

        private static void AddEdgeMapping(
            Dictionary<GeometricEdgeKey, List<UvEdgeMapping>> map,
            List<Vector3> vertices,
            List<Vector2> uvs,
            int indexA,
            int indexB)
        {
            QuantizedPoint3 positionA = new QuantizedPoint3(vertices[indexA]);
            QuantizedPoint3 positionB = new QuantizedPoint3(vertices[indexB]);
            GeometricEdgeKey key = new GeometricEdgeKey(positionA, positionB);
            UvEdgeMapping mapping;

            if (positionA.CompareTo(positionB) <= 0)
            {
                mapping.UvAtA = uvs[indexA];
                mapping.UvAtB = uvs[indexB];
            }
            else
            {
                mapping.UvAtA = uvs[indexB];
                mapping.UvAtB = uvs[indexA];
            }

            if (!map.TryGetValue(key, out List<UvEdgeMapping> mappings))
            {
                mappings = new List<UvEdgeMapping>(2);
                map.Add(key, mappings);
            }

            for (int i = 0; i < mappings.Count; i++)
            {
                if (Approximately(mappings[i].UvAtA, mapping.UvAtA) &&
                    Approximately(mappings[i].UvAtB, mapping.UvAtB))
                    return;
            }
            mappings.Add(mapping);
        }

        private static int CountUvSeamEdges(Dictionary<GeometricEdgeKey, List<UvEdgeMapping>> map)
        {
            int count = 0;
            foreach (KeyValuePair<GeometricEdgeKey, List<UvEdgeMapping>> pair in map)
            {
                if (pair.Value.Count > 1)
                    count++;
            }
            return count;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return (a - b).sqrMagnitude <= UvEqualityEpsilon * UvEqualityEpsilon;
        }

        private static bool IsValidIndex(int index, int count)
        {
            return index >= 0 && index < count;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
