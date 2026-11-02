using System;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Expands grayscale mask values inside rasterized UV-island coverage.
    /// Only the requested RGBA channel is changed; pixels outside UV islands are barriers.
    /// </summary>
    internal static class SurfaceUvMaskDilationProcessor
    {
        private const ushort AmbiguousOwner = ushort.MaxValue;
        private const long MaxTriangleCandidatePixels = 100000000L;

        internal struct Result
        {
            public int IslandCount;
            public int CoveredPixels;
            public int SeedPixels;
            public int ChangedPixels;
            public int AmbiguousPixels;
            public long TriangleCandidatePixels;
        }

        public static bool TryApplyDilation(
            Mesh mesh,
            int uvChannel,
            Color32[] maskPixels,
            int width,
            int height,
            int channelIndex,
            int radiusPixels,
            out Result result,
            out string error)
        {
            result = default(Result);
            error = string.Empty;

            if (mesh == null)
            {
                error = "Select a readable mesh before applying mask dilation.";
                return false;
            }
            if (maskPixels == null || width <= 0 || height <= 0 || maskPixels.Length != (long)width * height)
            {
                error = "The active mask pixel buffer does not match the mask page dimensions.";
                return false;
            }
            if (channelIndex < 0 || channelIndex > 3)
            {
                error = "Mask dilation requires a valid RGBA channel index (0–3).";
                return false;
            }
            if (radiusPixels < 1 || radiusPixels > 32)
            {
                error = "Mask dilation radius must be between 1 and 32 pixels.";
                return false;
            }
            if (uvChannel != 0)
            {
                error = "The current SurfaceWeaver mask painter and layered shader use UV0. Set the target UV Channel to 0 before applying mask dilation.";
                return false;
            }

            MeshUvValidationReport report = MeshUvValidator.Validate(mesh, uvChannel);
            if (report.Errors.Count > 0)
            {
                error = "Mask dilation could not run because UV validation contains blocking errors:\n" +
                        string.Join("\n", report.Errors);
                return false;
            }
            if (!report.IslandOverlapAnalysisCompleted || !report.OverlapAnalysisCompleted)
            {
                error = "UV overlap analysis was incomplete. Simplify or split the mesh and validate again before mask dilation.";
                return false;
            }
            if (report.OutOfRangeUvCount > 0 || report.NonFiniteUvCount > 0)
            {
                error = "Mask dilation requires finite UV coordinates inside the 0–1 range. Correct the UV layout first.";
                return false;
            }
            if (report.DegenerateUvTriangleCount > 0)
            {
                error = "Mask dilation requires non-degenerate UV triangles. The validator found " +
                        report.DegenerateUvTriangleCount + " degenerate triangle(s).";
                return false;
            }
            if (report.OverlappingUvIslandPairCount > 0)
            {
                error = "Mask dilation was not applied because " + report.OverlappingUvIslandPairCount +
                        " separate UV island pair(s) overlap. Resolve or intentionally repack those overlaps first. " +
                        "Mirrored triangle overlaps inside one connected island are allowed because they share the same UV owner.";
                return false;
            }
            if (report.RasterTriangles.Count == 0 || report.UvIslandCount == 0)
            {
                error = "No valid UV islands are available for mask dilation.";
                return false;
            }
            if (report.UvIslandCount >= AmbiguousOwner)
            {
                error = "The mesh contains too many UV islands for the safe dilation owner map.";
                return false;
            }

            int pixelCount = width * height;
            ushort[] owners = new ushort[pixelCount];
            long candidatePixels = 0;
            int coveredPixels = 0;

            for (int triangleIndex = 0; triangleIndex < report.RasterTriangles.Count; triangleIndex++)
            {
                MeshUvValidator.UvTriangle triangle = report.RasterTriangles[triangleIndex];
                if (!IsFinite01(triangle.UvA) || !IsFinite01(triangle.UvB) || !IsFinite01(triangle.UvC))
                {
                    error = "A UV triangle contains a non-finite or out-of-range coordinate. No dilation was applied.";
                    return false;
                }
                if (triangle.IslandId <= 0 || triangle.IslandId >= AmbiguousOwner)
                {
                    error = "A valid UV island owner could not be assigned to one or more triangles.";
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
                    error = "UV dilation rasterization exceeded its safety limit of " +
                            MaxTriangleCandidatePixels.ToString("N0") +
                            " candidate pixels. Split the mesh or use a smaller mask resolution.";
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
                        else if (existingOwner != islandOwner && existingOwner != AmbiguousOwner)
                        {
                            // Do not let a texel claimed by multiple islands become a dilation bridge.
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

            byte[] channelValues = new byte[pixelCount];
            int seedPixels = 0;
            int ambiguousPixels = 0;
            for (int i = 0; i < pixelCount; i++)
            {
                channelValues[i] = GetChannel(maskPixels[i], channelIndex);
                if (owners[i] == AmbiguousOwner)
                    ambiguousPixels++;
                else if (owners[i] > 0 && channelValues[i] > 0)
                    seedPixels++;
            }

            if (seedPixels == 0)
            {
                error = "No painted values were found in the selected channel inside the UV islands. Paint this channel first or select a layer that already contains mask data.";
                return false;
            }

            // Apply an efficient separable grayscale maximum filter. Every scanline is split
            // into contiguous runs of one UV-island owner, so the filter cannot cross an island
            // boundary or jump through empty atlas space. Horizontal + vertical passes produce
            // a square/Chebyshev pixel radius while remaining constrained to rasterized coverage.
            int lineLength = Mathf.Max(width, height);
            byte[] lineInput = new byte[lineLength];
            byte[] lineOutput = new byte[lineLength];
            int[] deque = new int[lineLength];

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width;
                Array.Copy(channelValues, rowStart, lineInput, 0, width);
                ApplyLineMaximum(owners, rowStart, width, 1, lineInput, lineOutput, deque, radiusPixels);
                Array.Copy(lineOutput, 0, channelValues, rowStart, width);
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    lineInput[y] = channelValues[y * width + x];

                ApplyLineMaximum(owners, x, height, width, lineInput, lineOutput, deque, radiusPixels);

                for (int y = 0; y < height; y++)
                    channelValues[y * width + x] = lineOutput[y];
            }

            int changedPixels = 0;
            for (int i = 0; i < pixelCount; i++)
            {
                if (owners[i] == 0 || owners[i] == AmbiguousOwner)
                    continue;

                byte originalValue = GetChannel(maskPixels[i], channelIndex);
                byte dilatedValue = channelValues[i];
                if (dilatedValue <= originalValue)
                    continue;

                Color32 pixel = maskPixels[i];
                SetChannel(ref pixel, channelIndex, dilatedValue);
                maskPixels[i] = pixel;
                changedPixels++;
            }

            result = new Result
            {
                IslandCount = report.UvIslandCount,
                CoveredPixels = coveredPixels,
                SeedPixels = seedPixels,
                ChangedPixels = changedPixels,
                AmbiguousPixels = ambiguousPixels,
                TriangleCandidatePixels = candidatePixels
            };
            return true;
        }

        private static void ApplyLineMaximum(
            ushort[] owners,
            int startIndex,
            int length,
            int stride,
            byte[] source,
            byte[] destination,
            int[] deque,
            int radius)
        {
            Array.Copy(source, destination, length);

            int segmentStart = 0;
            while (segmentStart < length)
            {
                ushort owner = owners[startIndex + segmentStart * stride];
                if (owner == 0 || owner == AmbiguousOwner)
                {
                    segmentStart++;
                    continue;
                }

                int segmentEnd = segmentStart;
                while (segmentEnd + 1 < length &&
                       owners[startIndex + (segmentEnd + 1) * stride] == owner)
                {
                    segmentEnd++;
                }

                int head = 0;
                int tail = 0;
                int nextToAdd = segmentStart;
                for (int position = segmentStart; position <= segmentEnd; position++)
                {
                    int right = Mathf.Min(segmentEnd, position + radius);
                    while (nextToAdd <= right)
                    {
                        byte nextValue = source[nextToAdd];
                        while (tail > head && source[deque[tail - 1]] <= nextValue)
                            tail--;
                        deque[tail++] = nextToAdd++;
                    }

                    int left = Mathf.Max(segmentStart, position - radius);
                    while (head < tail && deque[head] < left)
                        head++;

                    if (head < tail)
                        destination[position] = source[deque[head]];
                }

                segmentStart = segmentEnd + 1;
            }
        }

        private static byte GetChannel(Color32 color, int channelIndex)
        {
            switch (channelIndex)
            {
                case 0: return color.r;
                case 1: return color.g;
                case 2: return color.b;
                default: return color.a;
            }
        }

        private static void SetChannel(ref Color32 color, int channelIndex, byte value)
        {
            switch (channelIndex)
            {
                case 0: color.r = value; break;
                case 1: color.g = value; break;
                case 2: color.b = value; break;
                default: color.a = value; break;
            }
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
