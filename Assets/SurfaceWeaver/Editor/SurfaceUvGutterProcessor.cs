using System;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Extends packed RGBA mask texels from the edge of each UV island into unused atlas pixels.
    /// Pixels covered by mesh triangles are never changed. Separate-island overlaps and padding
    /// front collisions are rejected so one island cannot contaminate another.
    /// </summary>
    internal static class SurfaceUvGutterProcessor
    {
        private const ushort AmbiguousOwner = ushort.MaxValue;
        private const byte Unvisited = byte.MaxValue;
        private const long MaxTriangleCandidatePixels = 100000000L;
        private const int MaxQueuedPixels = 2000000;

        private static readonly int[] NeighborX = { -1, 0, 1, -1, 1, -1, 0, 1 };
        private static readonly int[] NeighborY = { -1, -1, -1, 0, 0, 1, 1, 1 };

        internal struct Result
        {
            public int IslandCount;
            public int CoveredPixels;
            public int BoundarySeedPixels;
            public int PaddedPixels;
            public int PaddingCollisionPixels;
            public long TriangleCandidatePixels;
        }

        public static bool TryApplyPadding(
            Mesh mesh,
            int uvChannel,
            Color32[] maskPixels,
            int width,
            int height,
            int gutterPixels,
            out Result result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (mesh == null)
            {
                error = "Select a readable mesh before generating UV gutter padding.";
                return false;
            }
            if (maskPixels == null || width <= 0 || height <= 0 || maskPixels.Length != width * height)
            {
                error = "The active mask pixel buffer does not match the mask page dimensions.";
                return false;
            }
            if (gutterPixels < 1 || gutterPixels > 32)
            {
                error = "Gutter padding must be between 1 and 32 pixels.";
                return false;
            }
            if (uvChannel != 0)
            {
                error = "The current SurfaceWeaver mask painter and layered shader use UV0. Set the target UV Channel to 0 before generating mask gutters.";
                return false;
            }

            MeshUvValidationReport report = MeshUvValidator.Validate(mesh, uvChannel);
            if (report.Errors.Count > 0)
            {
                error = "UV gutter padding could not run because the UV validation contains blocking errors:\n" +
                        string.Join("\n", report.Errors);
                return false;
            }
            if (!report.IslandOverlapAnalysisCompleted || !report.OverlapAnalysisCompleted)
            {
                error = "UV overlap analysis was incomplete. Simplify or split the mesh and validate again before padding.";
                return false;
            }
            if (report.OutOfRangeUvCount > 0 || report.NonFiniteUvCount > 0)
            {
                error = "UV gutter padding requires finite UV coordinates inside the 0–1 range. Correct the UV layout first.";
                return false;
            }
            if (report.DegenerateUvTriangleCount > 0)
            {
                error = "UV gutter padding requires non-degenerate UV triangles. The validator found " +
                        report.DegenerateUvTriangleCount + " degenerate triangle(s).";
                return false;
            }
            if (report.OverlappingUvIslandPairCount > 0)
            {
                error = "UV gutter padding was not applied because " + report.OverlappingUvIslandPairCount +
                        " separate UV island pair(s) overlap. Resolve or intentionally repack those overlaps first. " +
                        "Mirrored overlaps inside one connected island are supported.";
                return false;
            }
            if (report.RasterTriangles.Count == 0 || report.UvIslandCount == 0)
            {
                error = "No valid UV islands are available for gutter padding.";
                return false;
            }
            if (report.UvIslandCount >= AmbiguousOwner)
            {
                error = "The mesh contains too many UV islands for the safe padding owner map.";
                return false;
            }

            int pixelCount = width * height;
            ushort[] owners = new ushort[pixelCount];
            byte[] distances = new byte[pixelCount];
            for (int i = 0; i < distances.Length; i++)
                distances[i] = Unvisited;

            long candidatePixels = 0;
            int coveredPixels = 0;

            for (int triangleIndex = 0; triangleIndex < report.RasterTriangles.Count; triangleIndex++)
            {
                MeshUvValidator.UvTriangle triangle = report.RasterTriangles[triangleIndex];
                if (!IsFinite01(triangle.UvA) || !IsFinite01(triangle.UvB) || !IsFinite01(triangle.UvC))
                {
                    error = "A UV triangle contains a non-finite or out-of-range coordinate. No padding was applied.";
                    return false;
                }

                float minU = Mathf.Min(triangle.UvA.x, Mathf.Min(triangle.UvB.x, triangle.UvC.x));
                float minV = Mathf.Min(triangle.UvA.y, Mathf.Min(triangle.UvB.y, triangle.UvC.y));
                float maxU = Mathf.Max(triangle.UvA.x, Mathf.Max(triangle.UvB.x, triangle.UvC.x));
                float maxV = Mathf.Max(triangle.UvA.y, Mathf.Max(triangle.UvB.y, triangle.UvC.y));

                int minX = Mathf.Clamp(Mathf.FloorToInt(minU * width), 0, width - 1);
                int minY = Mathf.Clamp(Mathf.FloorToInt(minV * height), 0, height - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt(maxU * width) - 1, 0, width - 1);
                int maxY = Mathf.Clamp(Mathf.CeilToInt(maxV * height) - 1, 0, height - 1);
                if (maxX < minX || maxY < minY)
                    continue;

                long triangleCandidates = (long)(maxX - minX + 1) * (maxY - minY + 1);
                candidatePixels += triangleCandidates;
                if (candidatePixels > MaxTriangleCandidatePixels)
                {
                    error = "UV gutter rasterization exceeded its safety limit of " +
                            MaxTriangleCandidatePixels.ToString("N0") + " candidate pixels. Split the mesh or use a smaller mask resolution.";
                    return false;
                }

                ushort islandOwner = (ushort)triangle.IslandId;
                for (int y = minY; y <= maxY; y++)
                {
                    float v = (y + 0.5f) / height;
                    int row = y * width;
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2 point = new Vector2((x + 0.5f) / width, v);
                        if (!ContainsPoint(triangle.UvA, triangle.UvB, triangle.UvC, point))
                            continue;

                        int pixelIndex = row + x;
                        ushort existingOwner = owners[pixelIndex];
                        if (existingOwner == 0)
                        {
                            owners[pixelIndex] = islandOwner;
                            coveredPixels++;
                        }
                        else if (existingOwner != islandOwner)
                        {
                            // Different islands claim the same raster texel. Leave it ambiguous.
                            owners[pixelIndex] = AmbiguousOwner;
                        }
                    }
                }
            }

            if (coveredPixels == 0)
            {
                error = "No mesh UV triangles covered any texel centers at this mask resolution. Increase the mask resolution or inspect the UV layout.";
                return false;
            }

            // Surface pixels are distance-zero seeds. Ambiguous texels remain barriers.
            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i] > 0 && owners[i] != AmbiguousOwner)
                    distances[i] = 0;
            }

            int queueCapacity = Mathf.Min(pixelCount, MaxQueuedPixels);
            int[] queue = new int[queueCapacity];
            int queueHead = 0;
            int queueTail = 0;
            int boundarySeeds = 0;

            // Seed only occupied pixels at an island boundary; interior pixels do not need to expand.
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int index = row + x;
                    ushort owner = owners[index];
                    if (owner == 0 || owner == AmbiguousOwner)
                        continue;

                    bool isBoundary = false;
                    for (int neighbor = 0; neighbor < NeighborX.Length; neighbor++)
                    {
                        int nx = x + NeighborX[neighbor];
                        int ny = y + NeighborY[neighbor];
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                            continue;
                        if (owners[ny * width + nx] == 0)
                        {
                            isBoundary = true;
                            break;
                        }
                    }

                    if (!isBoundary)
                        continue;
                    if (queueTail >= queue.Length)
                    {
                        error = "The UV boundary seed count exceeded the safe processing limit of " +
                                MaxQueuedPixels.ToString("N0") + " pixels. Use a lower mask resolution or split the mesh.";
                        return false;
                    }

                    queue[queueTail++] = index;
                    boundarySeeds++;
                }
            }

            int paddedPixels = 0;
            int collisions = 0;
            while (queueHead < queueTail)
            {
                int currentIndex = queue[queueHead++];
                ushort currentOwner = owners[currentIndex];
                byte currentDistance = distances[currentIndex];
                if (currentOwner == 0 || currentOwner == AmbiguousOwner || currentDistance >= gutterPixels)
                    continue;

                int x = currentIndex % width;
                int y = currentIndex / width;
                byte nextDistance = (byte)(currentDistance + 1);

                for (int neighbor = 0; neighbor < NeighborX.Length; neighbor++)
                {
                    int nx = x + NeighborX[neighbor];
                    int ny = y + NeighborY[neighbor];
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    int nextIndex = ny * width + nx;
                    ushort nextOwner = owners[nextIndex];
                    if (nextOwner == 0)
                    {
                        if (queueTail >= queue.Length)
                        {
                            error = "Gutter expansion exceeded the safe processing limit of " +
                                    MaxQueuedPixels.ToString("N0") + " pixels. Reduce the gutter width or mask resolution.";
                            return false;
                        }

                        owners[nextIndex] = currentOwner;
                        distances[nextIndex] = nextDistance;
                        maskPixels[nextIndex] = maskPixels[currentIndex];
                        queue[queueTail++] = nextIndex;
                        paddedPixels++;
                    }
                    else if (nextOwner != currentOwner && nextOwner != AmbiguousOwner &&
                             distances[nextIndex] == nextDistance)
                    {
                        // Two separate island fronts reached this empty texel at the same distance.
                        // Refuse the whole operation rather than choosing one island arbitrarily.
                        owners[nextIndex] = AmbiguousOwner;
                        distances[nextIndex] = Unvisited;
                        collisions++;
                    }
                }
            }

            result = new Result
            {
                IslandCount = report.UvIslandCount,
                CoveredPixels = coveredPixels,
                BoundarySeedPixels = boundarySeeds,
                PaddedPixels = paddedPixels,
                PaddingCollisionPixels = collisions,
                TriangleCandidatePixels = candidatePixels
            };

            if (collisions > 0)
            {
                error = collisions + " gutter texel(s) were claimed by multiple UV islands. No changes were applied. " +
                        "Reduce the gutter width or repack the UV islands with more spacing.";
                return false;
            }
            if (paddedPixels == 0)
            {
                error = "No empty atlas texels were available for padding. The UV islands may fill the mask page or the gutter width may be too small.";
                return false;
            }

            return true;
        }

        private static bool ContainsPoint(Vector2 a, Vector2 b, Vector2 c, Vector2 point)
        {
            float ab = Cross(b - a, point - a);
            float bc = Cross(c - b, point - b);
            float ca = Cross(a - c, point - c);
            const float epsilon = 0.00000001f;
            bool hasNegative = ab < -epsilon || bc < -epsilon || ca < -epsilon;
            bool hasPositive = ab > epsilon || bc > epsilon || ca > epsilon;
            return !(hasNegative && hasPositive);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static bool IsFinite01(Vector2 uv)
        {
            return !float.IsNaN(uv.x) && !float.IsInfinity(uv.x) &&
                   !float.IsNaN(uv.y) && !float.IsInfinity(uv.y) &&
                   uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }
    }
}
