using System;
using System.Collections.Generic;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// CPU connected-region fill for one channel of a packed RGBA mask. The region is defined
    /// in UV texture space by 4- or 8-neighbour pixel connectivity and a seed-value tolerance.
    /// Other color channels are preserved byte-for-byte.
    /// </summary>
    internal static class SurfaceMaskFloodFillProcessor
    {
        public static bool TryFill(
            Color32[] pixels, int width, int height, int seedX, int seedY,
            int channel, float tolerance, float replacement, bool eightConnected,
            out int changedPixels, out string error)
        {
            changedPixels = 0;
            error = string.Empty;
            if (pixels == null || width <= 0 || height <= 0 || pixels.Length != (long)width * height)
            {
                error = "Flood Fill received an invalid mask buffer or dimensions.";
                return false;
            }
            if (channel < 0 || channel > 3)
            {
                error = "Flood Fill channel must be R, G, B or A.";
                return false;
            }
            if (seedX < 0 || seedX >= width || seedY < 0 || seedY >= height)
            {
                error = "Flood Fill seed is outside the mask page.";
                return false;
            }

            int count = pixels.Length;
            // Prevent accidental allocation overflow in malformed or extreme assets.
            if (count > 16777216)
            {
                error = "Flood Fill is limited to 16,777,216 pixels per operation. Reduce the mask dimensions before trying again.";
                return false;
            }

            byte[] sourceChannel = new byte[count];
            byte[] visited = new byte[(count + 7) >> 3];
            for (int i = 0; i < count; i++)
                sourceChannel[i] = GetChannel(pixels[i], channel);

            int seedIndex = seedY * width + seedX;
            byte seedValue = sourceChannel[seedIndex];
            byte fillValue = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(replacement) * 255f), 0, 255);
            int toleranceByte = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(tolerance) * 255f), 0, 255);
            Queue<int> queue = new Queue<int>(Math.Min(count, 65536));
            SetVisited(visited, seedIndex);
            queue.Enqueue(seedIndex);

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                if (Math.Abs(sourceChannel[index] - seedValue) > toleranceByte)
                    continue;

                Color32 pixel = pixels[index];
                SetChannel(ref pixel, channel, fillValue);
                pixels[index] = pixel;
                changedPixels++;

                int x = index % width;
                int y = index / width;
                EnqueueNeighbor(x - 1, y, width, height, seedValue, toleranceByte, sourceChannel, visited, queue);
                EnqueueNeighbor(x + 1, y, width, height, seedValue, toleranceByte, sourceChannel, visited, queue);
                EnqueueNeighbor(x, y - 1, width, height, seedValue, toleranceByte, sourceChannel, visited, queue);
                EnqueueNeighbor(x, y + 1, width, height, seedValue, toleranceByte, sourceChannel, visited, queue);
                if (eightConnected)
                {
                    EnqueueNeighbor(x - 1, y - 1, width, height, seedValue, toleranceByte, sourceChannel, visited, queue);
                    EnqueueNeighbor(x + 1, y - 1, width, height, seedValue, toleranceByte, sourceChannel, visited, queue);
                    EnqueueNeighbor(x - 1, y + 1, width, height, seedValue, toleranceByte, sourceChannel, visited, queue);
                    EnqueueNeighbor(x + 1, y + 1, width, height, seedValue, toleranceByte, sourceChannel, visited, queue);
                }
            }

            return true;
        }

        private static void EnqueueNeighbor(
            int x, int y, int width, int height, byte seedValue, int tolerance,
            byte[] source, byte[] visited, Queue<int> queue)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            int index = y * width + x;
            if (IsVisited(visited, index)) return;
            SetVisited(visited, index);
            if (Math.Abs(source[index] - seedValue) <= tolerance) queue.Enqueue(index);
        }

        private static byte GetChannel(Color32 color, int channel)
        {
            switch (channel)
            {
                case 0: return color.r;
                case 1: return color.g;
                case 2: return color.b;
                default: return color.a;
            }
        }

        private static void SetChannel(ref Color32 color, int channel, byte value)
        {
            switch (channel)
            {
                case 0: color.r = value; break;
                case 1: color.g = value; break;
                case 2: color.b = value; break;
                default: color.a = value; break;
            }
        }

        private static bool IsVisited(byte[] visited, int index)
        {
            int byteIndex = index >> 3;
            int bit = 1 << (index & 7);
            return (visited[byteIndex] & bit) != 0;
        }

        private static void SetVisited(byte[] visited, int index)
        {
            int byteIndex = index >> 3;
            int bit = 1 << (index & 7);
            visited[byteIndex] = (byte)(visited[byteIndex] | bit);
        }
    }
}
