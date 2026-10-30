using System.Collections.Generic;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Performs a first-pass UV validation for texture-backed mesh painting.
    /// This validator does not yet perform UV-island overlap or seam-padding analysis.
    /// </summary>
    public static class MeshUvValidator
    {
        private const float UvAreaEpsilon = 0.0000001f;
        private const float RangeEpsilon = 0.0001f;

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
                    "then reimport the model before running UV diagnostics."
                );
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
                    "UV count does not match the mesh vertex count. " +
                    "Check the imported mesh and selected UV channel."
                );
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
                {
                    report.OutOfRangeUvCount++;
                }
            }

            if (report.NonFiniteUvCount > 0)
            {
                report.Errors.Add(
                    report.NonFiniteUvCount + " UV coordinate(s) contain NaN or Infinity."
                );
            }

            if (report.OutOfRangeUvCount > 0)
            {
                report.Warnings.Add(
                    report.OutOfRangeUvCount + " UV coordinate(s) fall outside the 0–1 range. " +
                    "The initial mask-painting workflow expects normalized UVs."
                );
            }

            if (IsFinite(minU) && IsFinite(minV) &&
                IsFinite(maxU) && IsFinite(maxV) &&
                ((maxU - minU) < RangeEpsilon || (maxV - minV) < RangeEpsilon))
            {
                report.Errors.Add("The selected UV channel has effectively zero area.");
            }

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

                    if (ia < 0 || ib < 0 || ic < 0 ||
                        ia >= uvs.Count || ib >= uvs.Count || ic >= uvs.Count)
                    {
                        invalidIndexCount++;
                        continue;
                    }

                    Vector2 a = uvs[ia];
                    Vector2 b = uvs[ib];
                    Vector2 c = uvs[ic];

                    float twiceArea = Mathf.Abs(
                        (b.x - a.x) * (c.y - a.y) -
                        (b.y - a.y) * (c.x - a.x)
                    );

                    if (twiceArea <= UvAreaEpsilon)
                        report.DegenerateUvTriangleCount++;

                    report.TriangleCount++;
                }
            }

            if (unsupportedSubmeshes > 0)
            {
                report.Warnings.Add(
                    unsupportedSubmeshes + " submesh(es) do not use triangle topology. " +
                    "The first painting backend will support triangle meshes only."
                );
            }

            if (report.TriangleCount == 0)
            {
                report.Errors.Add("No triangle faces were found in the selected mesh topology.");
            }

            if (invalidIndexCount > 0)
            {
                report.Errors.Add(
                    invalidIndexCount + " triangle index reference(s) are outside the UV array."
                );
            }

            if (report.DegenerateUvTriangleCount > 0)
            {
                report.Warnings.Add(
                    report.DegenerateUvTriangleCount + " triangle(s) have zero or near-zero UV area. " +
                    "These faces may not paint correctly."
                );
            }

            if (report.OutOfRangeUvCount == 0 && report.NonFiniteUvCount == 0)
            {
                report.Infos.Add("All UV coordinates are finite and inside the 0–1 range.");
            }

            report.Warnings.Add(
                "UV island overlap detection and seam-padding analysis are not included in this first foundation build."
            );

            return report;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
