using System;
using System.Collections.Generic;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Validates mesh UV data and performs bounded triangle-overlap, geometric seam,
    /// UV-island grouping and overlapping-island classification.
    /// This is a diagnostic tool: it does not automatically unwrap or modify the source mesh.
    /// </summary>
    public static class MeshUvValidator
    {
        private const float UvAreaEpsilon = 0.0000001f;
        private const float RangeEpsilon = 0.0001f;
        private const float PositionQuantization = 100000f;
        private const float UvEqualityEpsilon = 0.00001f;
        private const float OverlapEpsilon = 0.0000001f;
        private const float IntersectionAreaEpsilon = 0.000000001f;
        private const float FullIslandCoverageThreshold = 0.985f;
        private const long MaxOverlapPairTests = 5000000L;
        private const long MaxIslandPairTests = 5000000L;
        private const long MaxIslandOverlapPairTests = 5000000L;

        internal sealed class UvTriangle
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
            public int TriangleId;
            public int IslandId;

            public float AbsoluteArea => Mathf.Abs(SignedArea);
        }

        private sealed class UvIsland
        {
            public int Id;
            public readonly List<UvTriangle> Triangles = new List<UvTriangle>();
            public float MinX = float.PositiveInfinity;
            public float MinY = float.PositiveInfinity;
            public float MaxX = float.NegativeInfinity;
            public float MaxY = float.NegativeInfinity;
            public float TotalArea;

            public void Add(UvTriangle triangle)
            {
                Triangles.Add(triangle);
                MinX = Mathf.Min(MinX, triangle.MinX);
                MinY = Mathf.Min(MinY, triangle.MinY);
                MaxX = Mathf.Max(MaxX, triangle.MaxX);
                MaxY = Mathf.Max(MaxY, triangle.MaxY);
                TotalArea += triangle.AbsoluteArea;
            }
        }

        private sealed class DisjointSet
        {
            private readonly int[] parent;
            private readonly byte[] rank;

            public DisjointSet(int count)
            {
                parent = new int[count];
                rank = new byte[count];
                for (int i = 0; i < count; i++)
                    parent[i] = i;
            }

            public int Find(int value)
            {
                int root = value;
                while (parent[root] != root)
                    root = parent[root];

                while (parent[value] != value)
                {
                    int next = parent[value];
                    parent[value] = root;
                    value = next;
                }

                return root;
            }

            public void Union(int a, int b)
            {
                int rootA = Find(a);
                int rootB = Find(b);
                if (rootA == rootB)
                    return;

                if (rank[rootA] < rank[rootB])
                {
                    parent[rootA] = rootB;
                }
                else if (rank[rootA] > rank[rootB])
                {
                    parent[rootB] = rootA;
                }
                else
                {
                    parent[rootB] = rootA;
                    rank[rootA]++;
                }
            }
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
            public int TriangleIndex;
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
                    int islandTriangleIndex = -1;
                    if (Mathf.Abs(signedArea) <= UvAreaEpsilon)
                    {
                        report.DegenerateUvTriangleCount++;
                    }
                    else
                    {
                        islandTriangleIndex = triangles.Count;
                        UvTriangle triangle = CreateTriangle(ia, ib, ic, a, b, c, signedArea);
                        triangle.TriangleId = islandTriangleIndex + 1;
                        triangles.Add(triangle);
                    }

                    report.TriangleCount++;
                    AddEdgeMapping(edgeMappings, vertices, uvs, ia, ib, islandTriangleIndex);
                    AddEdgeMapping(edgeMappings, vertices, uvs, ib, ic, islandTriangleIndex);
                    AddEdgeMapping(edgeMappings, vertices, uvs, ic, ia, islandTriangleIndex);
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
                    " triangle(s) have zero or near-zero UV area. These faces are excluded from island grouping.");

            report.UvSeamEdgeCount = CountUvSeamEdges(edgeMappings);
            if (report.UvSeamEdgeCount > 0)
                report.Warnings.Add(report.UvSeamEdgeCount +
                    " geometric edge(s) have discontinuous UV coordinates. These edges separate UV islands.");
            else
                report.Infos.Add("No UV seams were detected between shared geometric edges (within the configured positional tolerance).");

            List<UvIsland> islands = BuildUvIslands(triangles, edgeMappings, report);
            for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
            {
                UvIsland island = islands[islandIndex];
                for (int triangleIndex = 0; triangleIndex < island.Triangles.Count; triangleIndex++)
                {
                    UvTriangle triangle = island.Triangles[triangleIndex];
                    report.RasterTriangles.Add(triangle);
                }
            }

            AnalyzeIslandOverlaps(islands, report);
            AnalyzeOverlaps(triangles, report);

            if (report.OutOfRangeUvCount == 0 && report.NonFiniteUvCount == 0)
                report.Infos.Add("All UV coordinates are finite and inside the 0–1 range.");

            if (report.OverlappingUvTrianglePairCount > 0 && report.OverlappingUvIslandPairCount == 0)
                report.Infos.Add(
                    "Triangle overlaps were detected, but they occur within a connected UV island or could not be grouped as separate island pairs. Review the triangle-level count.");

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
                TriangleId = -1,
                IslandId = 0,
                MinX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)),
                MinY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)),
                MaxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)),
                MaxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y)),
                SignedArea = signedArea
            };
        }

        private static List<UvIsland> BuildUvIslands(
            List<UvTriangle> triangles,
            Dictionary<GeometricEdgeKey, List<UvEdgeMapping>> edgeMappings,
            MeshUvValidationReport report)
        {
            List<UvIsland> islands = new List<UvIsland>();
            if (triangles.Count == 0)
            {
                report.IslandOverlapAnalysisCompleted = true;
                report.Infos.Add("No non-degenerate UV triangles were available for island grouping.");
                return islands;
            }

            DisjointSet sets = new DisjointSet(triangles.Count);
            foreach (KeyValuePair<GeometricEdgeKey, List<UvEdgeMapping>> pair in edgeMappings)
            {
                List<UvEdgeMapping> mappings = pair.Value;
                for (int i = 0; i < mappings.Count; i++)
                {
                    UvEdgeMapping first = mappings[i];
                    if (first.TriangleIndex < 0)
                        continue;

                    for (int j = i + 1; j < mappings.Count; j++)
                    {
                        UvEdgeMapping second = mappings[j];
                        if (second.TriangleIndex < 0 || first.TriangleIndex == second.TriangleIndex)
                            continue;

                        // The geometric edge is shared and its canonical UV endpoints match,
                        // so both faces belong to the same connected UV island.
                        if (Approximately(first.UvAtA, second.UvAtA) &&
                            Approximately(first.UvAtB, second.UvAtB))
                            sets.Union(first.TriangleIndex, second.TriangleIndex);
                    }
                }
            }

            Dictionary<int, UvIsland> byRoot = new Dictionary<int, UvIsland>();
            for (int i = 0; i < triangles.Count; i++)
            {
                int root = sets.Find(i);
                if (!byRoot.TryGetValue(root, out UvIsland island))
                {
                    island = new UvIsland();
                    byRoot.Add(root, island);
                    islands.Add(island);
                }
                island.Add(triangles[i]);
            }

            islands.Sort((a, b) =>
            {
                int y = a.MinY.CompareTo(b.MinY);
                if (y != 0) return y;
                int x = a.MinX.CompareTo(b.MinX);
                if (x != 0) return x;
                return a.Triangles.Count.CompareTo(b.Triangles.Count);
            });

            report.UvIslandCount = islands.Count;
            for (int i = 0; i < islands.Count; i++)
            {
                UvIsland island = islands[i];
                island.Id = i + 1;
                for (int triangleIndex = 0; triangleIndex < island.Triangles.Count; triangleIndex++)
                    island.Triangles[triangleIndex].IslandId = island.Id;
                report.UvIslandSummaries.Add(
                    "Island " + island.Id + ": " + island.Triangles.Count + " triangle(s), " +
                    "U [" + island.MinX.ToString("0.###") + ", " + island.MaxX.ToString("0.###") + "], " +
                    "V [" + island.MinY.ToString("0.###") + ", " + island.MaxY.ToString("0.###") + "]");
            }

            report.Infos.Add("Grouped " + islands.Count + " connected UV island(s) from " + triangles.Count + " non-degenerate triangle(s).");
            return islands;
        }

        private static void AnalyzeIslandOverlaps(List<UvIsland> islands, MeshUvValidationReport report)
        {
            report.IslandOverlapAnalysisCompleted = true;
            if (islands.Count < 2)
            {
                report.Infos.Add("Fewer than two UV islands are present; island-to-island overlap analysis is not needed.");
                return;
            }

            // Reused clipping buffers avoid per-candidate polygon allocations on dense meshes.
            Vector2[] bufferA = new Vector2[8];
            Vector2[] bufferB = new Vector2[8];
            bool reachedSafetyLimit = false;

            for (int i = 0; i < islands.Count && !reachedSafetyLimit; i++)
            {
                UvIsland a = islands[i];
                for (int j = i + 1; j < islands.Count && !reachedSafetyLimit; j++)
                {
                    report.IslandPairCandidateTests++;
                    if (report.IslandPairCandidateTests > MaxIslandPairTests)
                    {
                        report.IslandOverlapAnalysisCompleted = false;
                        report.Warnings.Add(
                            "UV island-pair analysis reached its safety limit of " + MaxIslandPairTests +
                            " island pairs. Island grouping is complete, but island overlap classification is partial.");
                        reachedSafetyLimit = true;
                        break;
                    }

                    UvIsland b = islands[j];
                    if (!BoundsOverlap(a.MinX, a.MinY, a.MaxX, a.MaxY,
                            b.MinX, b.MinY, b.MaxX, b.MaxY))
                        continue;

                    double sameOrientationArea = 0.0;
                    double oppositeOrientationArea = 0.0;

                    for (int ta = 0; ta < a.Triangles.Count && !reachedSafetyLimit; ta++)
                    {
                        UvTriangle triangleA = a.Triangles[ta];
                        for (int tb = 0; tb < b.Triangles.Count; tb++)
                        {
                            UvTriangle triangleB = b.Triangles[tb];
                            if (!BoundsOverlap(
                                    triangleA.MinX, triangleA.MinY, triangleA.MaxX, triangleA.MaxY,
                                    triangleB.MinX, triangleB.MinY, triangleB.MaxX, triangleB.MaxY))
                                continue;

                            report.IslandOverlapCandidateTrianglePairTests++;
                            if (report.IslandOverlapCandidateTrianglePairTests > MaxIslandOverlapPairTests)
                            {
                                report.IslandOverlapAnalysisCompleted = false;
                                report.Warnings.Add(
                                    "UV island overlap analysis reached its safety limit of " + MaxIslandOverlapPairTests +
                                    " candidate triangle pairs. Island grouping is complete, but overlap classification is partial.");
                                reachedSafetyLimit = true;
                                break;
                            }

                            float intersectionArea = TriangleIntersectionArea(triangleA, triangleB, bufferA, bufferB);
                            if (intersectionArea <= IntersectionAreaEpsilon)
                                continue;

                            if (triangleA.SignedArea * triangleB.SignedArea < 0f)
                                oppositeOrientationArea += intersectionArea;
                            else
                                sameOrientationArea += intersectionArea;
                        }
                    }

                    if (reachedSafetyLimit)
                        break;

                    double totalOverlapArea = sameOrientationArea + oppositeOrientationArea;
                    if (totalOverlapArea <= IntersectionAreaEpsilon)
                        continue;

                    report.OverlappingUvIslandPairCount++;
                    // Clamp coverage for readable diagnostics. Triangle intersections are
                    // accumulated pairwise, so malformed/self-overlapping source islands can
                    // otherwise report values slightly above 100%.
                    double coverageA = a.TotalArea > IntersectionAreaEpsilon
                        ? Math.Min(1.0, totalOverlapArea / a.TotalArea) : 0.0;
                    double coverageB = b.TotalArea > IntersectionAreaEpsilon
                        ? Math.Min(1.0, totalOverlapArea / b.TotalArea) : 0.0;
                    bool aFullyCovered = coverageA >= FullIslandCoverageThreshold;
                    bool bFullyCovered = coverageB >= FullIslandCoverageThreshold;
                    bool bothFullyCovered = aFullyCovered && bFullyCovered;

                    string overlapKind;
                    if (bothFullyCovered && oppositeOrientationArea >= totalOverlapArea * 0.75)
                    {
                        report.MirroredUvIslandPairCount++;
                        overlapKind = "Fully mirrored";
                    }
                    else if (bothFullyCovered && sameOrientationArea >= totalOverlapArea * 0.75)
                    {
                        report.StackedUvIslandPairCount++;
                        overlapKind = "Fully stacked";
                    }
                    else if (aFullyCovered || bFullyCovered)
                    {
                        report.ContainedUvIslandOverlapPairCount++;
                        overlapKind = "Contained overlap (one island fully covered)";
                    }
                    else
                    {
                        report.PartialUvIslandOverlapPairCount++;
                        overlapKind = "Partial overlap";
                    }

                    if (report.UvIslandOverlapSummaries.Count < 100)
                    {
                        report.UvIslandOverlapSummaries.Add(
                            "Island " + a.Id + " + Island " + b.Id + ": " + overlapKind +
                            " (coverage " + (coverageA * 100.0).ToString("0.0") + "% / " +
                            (coverageB * 100.0).ToString("0.0") + "%)");
                    }
                }
            }

            if (report.MirroredUvIslandPairCount > 0)
                report.Warnings.Add(report.MirroredUvIslandPairCount +
                    " fully overlapping mirrored UV island pair(s) detected. The islands have near-complete shared coverage and predominantly opposite UV orientation.");
            if (report.StackedUvIslandPairCount > 0)
                report.Warnings.Add(report.StackedUvIslandPairCount +
                    " fully stacked UV island pair(s) detected. The islands have near-complete shared coverage and predominantly matching UV orientation.");
            if (report.ContainedUvIslandOverlapPairCount > 0)
                report.Warnings.Add(report.ContainedUvIslandOverlapPairCount +
                    " contained UV island overlap pair(s) detected. One island is fully covered by part of another island; this is not a full island-to-island stack.");
            if (report.PartialUvIslandOverlapPairCount > 0)
                report.Warnings.Add(report.PartialUvIslandOverlapPairCount +
                    " partially overlapping UV island pair(s) detected. These may cause shared mask texels in the overlap region.");
            if (report.OverlappingUvIslandPairCount > report.UvIslandOverlapSummaries.Count)
                report.Infos.Add("The report stores the first 100 overlapping island-pair details; the total overlap-pair count is shown above.");
            if (report.OverlappingUvIslandPairCount == 0 && report.IslandOverlapAnalysisCompleted)
                report.Infos.Add("No overlaps between separate UV islands were detected.");
        }

        private static float TriangleIntersectionArea(
            UvTriangle subject, UvTriangle clip, Vector2[] bufferA, Vector2[] bufferB)
        {
            if (!TrianglesOverlapWithPositiveArea(subject, clip))
                return 0f;

            bufferA[0] = subject.UvA;
            bufferA[1] = subject.UvB;
            bufferA[2] = subject.UvC;
            int count = 3;
            float clipOrientation = clip.SignedArea >= 0f ? 1f : -1f;
            Vector2[] input = bufferA;
            Vector2[] output = bufferB;

            count = ClipPolygonAgainstEdge(input, count, output, clip.UvA, clip.UvB, clipOrientation);
            SwapBuffers(ref input, ref output);
            if (count < 3) return 0f;

            count = ClipPolygonAgainstEdge(input, count, output, clip.UvB, clip.UvC, clipOrientation);
            SwapBuffers(ref input, ref output);
            if (count < 3) return 0f;

            count = ClipPolygonAgainstEdge(input, count, output, clip.UvC, clip.UvA, clipOrientation);
            SwapBuffers(ref input, ref output);
            if (count < 3) return 0f;

            float twiceArea = 0f;
            for (int i = 0; i < count; i++)
                twiceArea += Cross(input[i], input[(i + 1) % count]);
            return Mathf.Abs(twiceArea) * 0.5f;
        }

        private static int ClipPolygonAgainstEdge(
            Vector2[] input,
            int inputCount,
            Vector2[] output,
            Vector2 clipStart,
            Vector2 clipEnd,
            float orientation)
        {
            if (inputCount == 0)
                return 0;

            Vector2 edge = clipEnd - clipStart;
            int outputCount = 0;
            Vector2 previous = input[inputCount - 1];
            float previousDistance = orientation * Cross(edge, previous - clipStart);
            bool previousInside = previousDistance >= -OverlapEpsilon;

            for (int i = 0; i < inputCount; i++)
            {
                Vector2 current = input[i];
                float currentDistance = orientation * Cross(edge, current - clipStart);
                bool currentInside = currentDistance >= -OverlapEpsilon;

                if (currentInside)
                {
                    if (!previousInside)
                        AddIntersection(output, ref outputCount, previous, current, previousDistance, currentDistance);
                    AddPolygonPoint(output, ref outputCount, current);
                }
                else if (previousInside)
                {
                    AddIntersection(output, ref outputCount, previous, current, previousDistance, currentDistance);
                }

                previous = current;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }

            return outputCount;
        }

        private static void AddIntersection(
            Vector2[] output,
            ref int count,
            Vector2 from,
            Vector2 to,
            float fromDistance,
            float toDistance)
        {
            float denominator = fromDistance - toDistance;
            if (Mathf.Abs(denominator) <= 0.000000001f)
                return;

            float t = Mathf.Clamp01(fromDistance / denominator);
            AddPolygonPoint(output, ref count, from + (to - from) * t);
        }

        private static void AddPolygonPoint(Vector2[] output, ref int count, Vector2 point)
        {
            if (count >= output.Length)
                return;
            if (count > 0 && (output[count - 1] - point).sqrMagnitude <= 0.000000000001f)
                return;
            output[count++] = point;
        }

        private static void SwapBuffers(ref Vector2[] a, ref Vector2[] b)
        {
            Vector2[] temporary = a;
            a = b;
            b = temporary;
        }

        private static bool BoundsOverlap(
            float minAX, float minAY, float maxAX, float maxAY,
            float minBX, float minBY, float maxBX, float maxBY)
        {
            return minAX < maxBX - OverlapEpsilon && maxAX > minBX + OverlapEpsilon &&
                   minAY < maxBY - OverlapEpsilon && maxAY > minBY + OverlapEpsilon;
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
                    if (b.MinX >= a.MaxX - OverlapEpsilon)
                        break;
                    if (b.MinY >= a.MaxY - OverlapEpsilon || b.MaxY <= a.MinY + OverlapEpsilon)
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

                    if (!TrianglesOverlapWithPositiveArea(a, b))
                        continue;

                    report.OverlappingUvTrianglePairCount++;
                    if (a.IslandId > 0 && a.IslandId == b.IslandId)
                    {
                        report.SameIslandOverlappingTrianglePairCount++;
                        bool oppositeOrientation = a.SignedArea * b.SignedArea < 0f;
                        if (oppositeOrientation)
                            report.MirroredOverlapWithinIslandTrianglePairCount++;
                        else
                            report.SameOrientationOverlapWithinIslandTrianglePairCount++;

                        if (report.SameIslandOverlapSummaries.Count < 100)
                        {
                            string orientation = oppositeOrientation
                                ? "Opposite orientation (mirrored-overlap candidate)"
                                : "Matching orientation (same-orientation overlap)";
                            report.SameIslandOverlapSummaries.Add(
                                "Island " + a.IslandId + ": Triangle " + a.TriangleId + " + Triangle " +
                                b.TriangleId + " — " + orientation);
                        }
                    }
                }
            }

            if (report.OverlappingUvTrianglePairCount > 0)
                report.Warnings.Add(report.OverlappingUvTrianglePairCount +
                    " pair(s) of UV triangles overlap with positive area. Painting one region may affect multiple mesh regions.");
            else
                report.Infos.Add("No positive-area UV triangle overlaps were detected.");

            if (report.MirroredOverlapWithinIslandTrianglePairCount > 0)
                report.Warnings.Add(report.MirroredOverlapWithinIslandTrianglePairCount +
                    " opposite-orientation triangle overlap pair(s) were found inside connected UV island(s). This can indicate mirrored faces sharing one connected island.");
            if (report.SameOrientationOverlapWithinIslandTrianglePairCount > 0)
                report.Warnings.Add(report.SameOrientationOverlapWithinIslandTrianglePairCount +
                    " matching-orientation triangle overlap pair(s) were found inside connected UV island(s). Review for duplicate or internally stacked faces.");
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
            return Mathf.Min(maxA, maxB) - Mathf.Max(minA, minB) > OverlapEpsilon;
        }

        private static void AddEdgeMapping(
            Dictionary<GeometricEdgeKey, List<UvEdgeMapping>> map,
            List<Vector3> vertices,
            List<Vector2> uvs,
            int indexA,
            int indexB,
            int triangleIndex)
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
            mapping.TriangleIndex = triangleIndex;

            if (!map.TryGetValue(key, out List<UvEdgeMapping> mappings))
            {
                mappings = new List<UvEdgeMapping>(2);
                map.Add(key, mappings);
            }
            mappings.Add(mapping);
        }

        private static int CountUvSeamEdges(Dictionary<GeometricEdgeKey, List<UvEdgeMapping>> map)
        {
            int count = 0;
            foreach (KeyValuePair<GeometricEdgeKey, List<UvEdgeMapping>> pair in map)
            {
                List<UvEdgeMapping> mappings = pair.Value;
                bool hasDiscontinuity = false;
                for (int i = 0; i < mappings.Count && !hasDiscontinuity; i++)
                {
                    for (int j = i + 1; j < mappings.Count; j++)
                    {
                        if (!Approximately(mappings[i].UvAtA, mappings[j].UvAtA) ||
                            !Approximately(mappings[i].UvAtB, mappings[j].UvAtB))
                        {
                            hasDiscontinuity = true;
                            break;
                        }
                    }
                }
                if (hasDiscontinuity)
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
