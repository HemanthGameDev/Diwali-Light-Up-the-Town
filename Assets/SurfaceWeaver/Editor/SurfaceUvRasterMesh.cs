using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Builds a temporary mesh whose triangles are laid out in UV0 space while carrying
    /// their original local positions and per-triangle local-space face normal as vertex attributes.
    /// Face-normal orientation is reconciled with authored mesh normals when available so
    /// projection-facing tests follow the visible side of imported meshes.
    /// Rendering this mesh into a mask lets a world-space brush affect every UV island that
    /// represents nearby surface geometry, including duplicated vertices at UV seams.
    /// </summary>
    internal sealed class SurfaceUvRasterMesh : IDisposable
    {
        public Mesh Mesh { get; private set; }
        public int SourceTriangleCount { get; private set; }
        public int SkippedDegenerateTriangleCount { get; private set; }
        public int SkippedNonTriangleSubmeshCount { get; private set; }

        // Geometry degeneracy must be scale-independent. The previous implementation
        // compared local-space cross.sqrMagnitude against an absolute threshold, which
        // rejected valid meshes authored at small local dimensions and enlarged by the
        // GameObject transform (for example, a 0.02-unit mesh scaled by 100).
        // This threshold is sin(angle)^2 between the two triangle edges, so it is
        // independent of object units and uniform/non-uniform non-zero scale.
        private const double RelativeGeometrySinSquaredEpsilon = 1e-12d;

        private SurfaceUvRasterMesh() { }

        public static bool TryBuild(Mesh source, out SurfaceUvRasterMesh result, out string error)
        {
            result = null;
            error = string.Empty;

            if (source == null)
            {
                error = "No source mesh was provided for UV rasterization.";
                return false;
            }

            if (!source.isReadable)
            {
                error = "The source mesh is not readable. Enable Read/Write in its import settings.";
                return false;
            }

            Vector3[] sourceVertices;
            Vector3[] sourceNormals;
            Vector2[] sourceUvs;
            try
            {
                sourceVertices = source.vertices;
                sourceNormals = source.normals;
                sourceUvs = source.uv;
            }
            catch (Exception exception)
            {
                error = "Could not read source mesh data: " + exception.Message;
                return false;
            }

            if (sourceVertices == null || sourceUvs == null || sourceUvs.Length != sourceVertices.Length)
            {
                error = "UV0 must contain one coordinate per source mesh vertex.";
                return false;
            }

            List<int> sourceTriangles = new List<int>();
            int skippedNonTriangleSubmeshes = 0;
            try
            {
                for (int submesh = 0; submesh < source.subMeshCount; submesh++)
                {
                    if (source.GetTopology(submesh) != MeshTopology.Triangles)
                    {
                        skippedNonTriangleSubmeshes++;
                        continue;
                    }

                    int[] submeshIndices = source.GetIndices(submesh);
                    if (submeshIndices != null)
                        sourceTriangles.AddRange(submeshIndices);
                }
            }
            catch (Exception exception)
            {
                error = "Could not read source mesh triangle submeshes: " + exception.Message;
                return false;
            }

            if (sourceTriangles.Count < 3 || sourceTriangles.Count % 3 != 0)
            {
                error = "The source mesh has no valid triangle topology. Non-triangle submeshes are not rasterized.";
                return false;
            }

            List<Vector3> rasterPositions = new List<Vector3>(sourceTriangles.Count);
            List<Vector2> rasterUvs = new List<Vector2>(sourceTriangles.Count);
            List<Vector3> localPositions = new List<Vector3>(sourceTriangles.Count);
            List<Vector3> localNormals = new List<Vector3>(sourceTriangles.Count);
            List<int> indices = new List<int>(sourceTriangles.Count);
            int skipped = 0;

            for (int triangle = 0; triangle < sourceTriangles.Count / 3; triangle++)
            {
                int i0 = sourceTriangles[triangle * 3];
                int i1 = sourceTriangles[triangle * 3 + 1];
                int i2 = sourceTriangles[triangle * 3 + 2];
                if (!ValidIndex(i0, sourceVertices.Length) ||
                    !ValidIndex(i1, sourceVertices.Length) ||
                    !ValidIndex(i2, sourceVertices.Length))
                {
                    skipped++;
                    continue;
                }

                Vector3 p0 = sourceVertices[i0];
                Vector3 p1 = sourceVertices[i1];
                Vector3 p2 = sourceVertices[i2];
                Vector2 uv0 = sourceUvs[i0];
                Vector2 uv1 = sourceUvs[i1];
                Vector2 uv2 = sourceUvs[i2];
                if (!IsFinite(uv0) || !IsFinite(uv1) || !IsFinite(uv2) ||
                    !IsFinite(p0) || !IsFinite(p1) || !IsFinite(p2))
                {
                    skipped++;
                    continue;
                }

                const float uvRangeTolerance = 0.0001f;
                if (!IsInsideUnitUv(uv0, uvRangeTolerance) ||
                    !IsInsideUnitUv(uv1, uvRangeTolerance) ||
                    !IsInsideUnitUv(uv2, uvRangeTolerance))
                {
                    error = "The current UV raster painter requires UV0 coordinates inside the 0–1 range. " +
                        "Run UV diagnostics and repack or normalize out-of-range UVs before painting.";
                    return false;
                }

                float du1 = uv1.x - uv0.x;
                float dv1 = uv1.y - uv0.y;
                float du2 = uv2.x - uv0.x;
                float dv2 = uv2.y - uv0.y;
                float determinant = du1 * dv2 - du2 * dv1;
                if (Mathf.Abs(determinant) < 0.0000001f)
                {
                    skipped++;
                    continue;
                }

                Vector3 edge1 = p1 - p0;
                Vector3 edge2 = p2 - p0;
                if (!TryGetNormalizedFaceNormal(edge1, edge2, out Vector3 localFaceNormal))
                {
                    skipped++;
                    continue;
                }

                // Keep the projection-facing normal consistent with the source mesh's
                // authored normals when those normals are available. Some imported
                // meshes have triangle winding that differs from the visible/shaded side.
                // We still use a flat geometric face normal for each rasterized triangle;
                // the source normals only choose its orientation.
                if (sourceNormals != null && sourceNormals.Length == sourceVertices.Length)
                {
                    Vector3 referenceNormal = sourceNormals[i0] + sourceNormals[i1] + sourceNormals[i2];
                    if (IsFinite(referenceNormal) && referenceNormal.sqrMagnitude > 0.0000001f &&
                        Vector3.Dot(localFaceNormal, referenceNormal) < 0f)
                    {
                        localFaceNormal = -localFaceNormal;
                    }
                }

                int baseIndex = rasterPositions.Count;
                AddVertex(rasterPositions, rasterUvs, localPositions, localNormals,
                    uv0, p0, localFaceNormal);
                AddVertex(rasterPositions, rasterUvs, localPositions, localNormals,
                    uv1, p1, localFaceNormal);
                AddVertex(rasterPositions, rasterUvs, localPositions, localNormals,
                    uv2, p2, localFaceNormal);
                indices.Add(baseIndex);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 2);
            }

            if (indices.Count == 0)
            {
                error = "No non-degenerate UV0 triangles could be rasterized.";
                return false;
            }

            Mesh rasterMesh = new Mesh
            {
                name = source.name + " (SurfaceWeaver UV Raster)",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = rasterPositions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            try
            {
                rasterMesh.SetVertices(rasterPositions);
                rasterMesh.SetUVs(0, rasterUvs);
                rasterMesh.SetUVs(1, localPositions);
                rasterMesh.SetUVs(2, localNormals);
                rasterMesh.SetTriangles(indices, 0, false);
                rasterMesh.RecalculateBounds();
            }
            catch (Exception exception)
            {
                UnityEngine.Object.DestroyImmediate(rasterMesh);
                error = "Could not construct the temporary UV raster mesh: " + exception.Message;
                return false;
            }

            result = new SurfaceUvRasterMesh
            {
                Mesh = rasterMesh,
                SourceTriangleCount = sourceTriangles.Count / 3,
                SkippedDegenerateTriangleCount = skipped,
                SkippedNonTriangleSubmeshCount = skippedNonTriangleSubmeshes
            };
            return true;
        }

        public void Dispose()
        {
            if (Mesh != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(Mesh);
                else
                    UnityEngine.Object.DestroyImmediate(Mesh);
                Mesh = null;
            }
        }


        /// <summary>
        /// Computes a unit face normal without an absolute local-space area cutoff.
        /// A triangle is considered geometrically degenerate when its edge directions
        /// are effectively collinear: |cross(e1,e2)|^2 / (|e1|^2 |e2|^2) is near zero.
        /// The calculation uses double precision so small, otherwise valid imported
        /// meshes are not rejected merely because their local dimensions are small.
        /// </summary>
        private static bool TryGetNormalizedFaceNormal(
            Vector3 edge1,
            Vector3 edge2,
            out Vector3 normalizedNormal)
        {
            normalizedNormal = Vector3.zero;

            if (!IsFinite(edge1) || !IsFinite(edge2))
                return false;

            double e1x = edge1.x;
            double e1y = edge1.y;
            double e1z = edge1.z;
            double e2x = edge2.x;
            double e2y = edge2.y;
            double e2z = edge2.z;

            double edge1LengthSquared = e1x * e1x + e1y * e1y + e1z * e1z;
            double edge2LengthSquared = e2x * e2x + e2y * e2y + e2z * e2z;
            if (!(edge1LengthSquared > 0d) || !(edge2LengthSquared > 0d) ||
                !IsFinite(edge1LengthSquared) || !IsFinite(edge2LengthSquared))
                return false;

            double crossX = e1y * e2z - e1z * e2y;
            double crossY = e1z * e2x - e1x * e2z;
            double crossZ = e1x * e2y - e1y * e2x;
            double crossLengthSquared = crossX * crossX + crossY * crossY + crossZ * crossZ;
            double edgeProduct = edge1LengthSquared * edge2LengthSquared;

            if (!(crossLengthSquared > 0d) || !(edgeProduct > 0d) ||
                !IsFinite(crossLengthSquared) || !IsFinite(edgeProduct))
                return false;

            double relativeSinSquared = crossLengthSquared / edgeProduct;
            if (!IsFinite(relativeSinSquared) ||
                relativeSinSquared <= RelativeGeometrySinSquaredEpsilon)
                return false;

            double inverseCrossLength = 1d / Math.Sqrt(crossLengthSquared);
            normalizedNormal = new Vector3(
                (float)(crossX * inverseCrossLength),
                (float)(crossY * inverseCrossLength),
                (float)(crossZ * inverseCrossLength));

            return IsFinite(normalizedNormal) && normalizedNormal.sqrMagnitude > 0.5f;
        }

        private static void AddVertex(
            List<Vector3> positions,
            List<Vector2> uvs,
            List<Vector3> localPositions,
            List<Vector3> localNormals,
            Vector2 uv,
            Vector3 localPosition,
            Vector3 localFaceNormal)
        {
            positions.Add(new Vector3(uv.x, uv.y, 0f));
            uvs.Add(uv);
            localPositions.Add(localPosition);
            localNormals.Add(localFaceNormal);
        }

        private static bool ValidIndex(int index, int count)
        {
            return index >= 0 && index < count;
        }

        private static bool IsInsideUnitUv(Vector2 value, float tolerance)
        {
            return value.x >= -tolerance && value.x <= 1f + tolerance &&
                   value.y >= -tolerance && value.y <= 1f + tolerance;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
