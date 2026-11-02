using System;
using System.Collections.Generic;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// Builds a texture-space allow-mask for the UV0 chart containing a picked mesh triangle.
    /// Triangles are considered connected only when they share a geometric edge and the UV0
    /// coordinates at both edge endpoints match. A UV seam therefore separates charts even
    /// when importers split the seam vertices in the mesh vertex buffer.
    /// </summary>
    internal static class SurfaceUvIslandRegionBuilder
    {
        private const double PositionQuantization = 100000.0;
        private const double UvQuantization = 1000000.0;

        private struct VertexKey : IEquatable<VertexKey>, IComparable<VertexKey>
        {
            private readonly long px, py, pz, u, v;

            public VertexKey(Vector3 position, Vector2 uv)
            {
                px = Quantize(position.x, PositionQuantization);
                py = Quantize(position.y, PositionQuantization);
                pz = Quantize(position.z, PositionQuantization);
                u = Quantize(uv.x, UvQuantization);
                v = Quantize(uv.y, UvQuantization);
            }

            public bool Equals(VertexKey other)
            {
                return px == other.px && py == other.py && pz == other.pz && u == other.u && v == other.v;
            }

            public override bool Equals(object obj) { return obj is VertexKey && Equals((VertexKey)obj); }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = px.GetHashCode();
                    hash = (hash * 397) ^ py.GetHashCode();
                    hash = (hash * 397) ^ pz.GetHashCode();
                    hash = (hash * 397) ^ u.GetHashCode();
                    hash = (hash * 397) ^ v.GetHashCode();
                    return hash;
                }
            }

            public int CompareTo(VertexKey other)
            {
                int c = px.CompareTo(other.px); if (c != 0) return c;
                c = py.CompareTo(other.py); if (c != 0) return c;
                c = pz.CompareTo(other.pz); if (c != 0) return c;
                c = u.CompareTo(other.u); if (c != 0) return c;
                return v.CompareTo(other.v);
            }
        }

        private struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly VertexKey a;
            private readonly VertexKey b;

            public EdgeKey(VertexKey first, VertexKey second)
            {
                if (first.CompareTo(second) <= 0) { a = first; b = second; }
                else { a = second; b = first; }
            }

            public bool Equals(EdgeKey other) { return a.Equals(other.a) && b.Equals(other.b); }
            public override bool Equals(object obj) { return obj is EdgeKey && Equals((EdgeKey)obj); }
            public override int GetHashCode()
            {
                unchecked { return (a.GetHashCode() * 397) ^ b.GetHashCode(); }
            }
        }

        public static bool TryBuild(Mesh mesh, int seedTriangle, int width, int height,
            Vector2 seedUv, out byte[] allowedRegion, out string error)
        {
            allowedRegion = null;
            error = string.Empty;
            if (mesh == null || width <= 0 || height <= 0)
            {
                error = "Cannot build a UV-island mask because the target mesh or mask dimensions are invalid.";
                return false;
            }

            Vector3[] vertices;
            Vector2[] uvs;
            int[] triangles;
            try
            {
                vertices = mesh.vertices;
                uvs = mesh.uv;
                triangles = mesh.triangles;
            }
            catch (Exception ex)
            {
                error = "The target mesh is not readable. Enable Read/Write on the mesh import settings to use UV-island-restricted Flood Fill. Details: " + ex.Message;
                return false;
            }

            if (vertices == null || uvs == null || uvs.Length != vertices.Length || triangles == null || triangles.Length < 3)
            {
                error = "The target needs readable vertices, UV0 and triangle data to restrict Flood Fill to a UV island.";
                return false;
            }

            int triangleCount = triangles.Length / 3;
            if (seedTriangle < 0 || seedTriangle >= triangleCount)
            {
                error = "The picked triangle index is outside the target mesh triangle array.";
                return false;
            }

            try
            {
                var edgeTriangles = new Dictionary<EdgeKey, List<int>>(triangles.Length);
                for (int t = 0; t < triangleCount; t++)
                {
                    int offset = t * 3;
                    int i0 = triangles[offset];
                    int i1 = triangles[offset + 1];
                    int i2 = triangles[offset + 2];
                    if (!ValidIndex(i0, vertices, uvs) || !ValidIndex(i1, vertices, uvs) || !ValidIndex(i2, vertices, uvs))
                        continue;

                    VertexKey k0 = new VertexKey(vertices[i0], uvs[i0]);
                    VertexKey k1 = new VertexKey(vertices[i1], uvs[i1]);
                    VertexKey k2 = new VertexKey(vertices[i2], uvs[i2]);
                    AddEdge(edgeTriangles, new EdgeKey(k0, k1), t);
                    AddEdge(edgeTriangles, new EdgeKey(k1, k2), t);
                    AddEdge(edgeTriangles, new EdgeKey(k2, k0), t);
                }

                var islandTriangles = new HashSet<int>();
                var pending = new Queue<int>();
                islandTriangles.Add(seedTriangle);
                pending.Enqueue(seedTriangle);
                while (pending.Count > 0)
                {
                    int t = pending.Dequeue();
                    int offset = t * 3;
                    int i0 = triangles[offset];
                    int i1 = triangles[offset + 1];
                    int i2 = triangles[offset + 2];
                    if (!ValidIndex(i0, vertices, uvs) || !ValidIndex(i1, vertices, uvs) || !ValidIndex(i2, vertices, uvs))
                        continue;

                    VisitEdge(edgeTriangles, new EdgeKey(new VertexKey(vertices[i0], uvs[i0]), new VertexKey(vertices[i1], uvs[i1])), islandTriangles, pending);
                    VisitEdge(edgeTriangles, new EdgeKey(new VertexKey(vertices[i1], uvs[i1]), new VertexKey(vertices[i2], uvs[i2])), islandTriangles, pending);
                    VisitEdge(edgeTriangles, new EdgeKey(new VertexKey(vertices[i2], uvs[i2]), new VertexKey(vertices[i0], uvs[i0])), islandTriangles, pending);
                }

                int pixelCount = checked(width * height);
                allowedRegion = new byte[pixelCount];
                foreach (int t in islandTriangles)
                {
                    int offset = t * 3;
                    int i0 = triangles[offset];
                    int i1 = triangles[offset + 1];
                    int i2 = triangles[offset + 2];
                    if (!ValidIndex(i0, vertices, uvs) || !ValidIndex(i1, vertices, uvs) || !ValidIndex(i2, vertices, uvs))
                        continue;
                    RasterizeTriangle(uvs[i0], uvs[i1], uvs[i2], width, height, allowedRegion);
                }

                int seedX = Mathf.Clamp(Mathf.FloorToInt(seedUv.x * width), 0, width - 1);
                int seedY = Mathf.Clamp(Mathf.FloorToInt(seedUv.y * height), 0, height - 1);
                allowedRegion[seedY * width + seedX] = 1;

                return true;
            }
            catch (Exception ex)
            {
                allowedRegion = null;
                error = "Could not construct the UV-island region mask: " + ex.Message;
                return false;
            }
        }

        private static void AddEdge(Dictionary<EdgeKey, List<int>> map, EdgeKey edge, int triangle)
        {
            if (!map.TryGetValue(edge, out List<int> list))
            {
                list = new List<int>(2);
                map.Add(edge, list);
            }
            list.Add(triangle);
        }

        private static void VisitEdge(Dictionary<EdgeKey, List<int>> map, EdgeKey edge,
            HashSet<int> visited, Queue<int> pending)
        {
            if (!map.TryGetValue(edge, out List<int> neighbours)) return;
            for (int i = 0; i < neighbours.Count; i++)
            {
                int candidate = neighbours[i];
                if (visited.Add(candidate)) pending.Enqueue(candidate);
            }
        }

        private static bool ValidIndex(int index, Vector3[] vertices, Vector2[] uvs)
        {
            return index >= 0 && index < vertices.Length && index < uvs.Length;
        }

        private static void RasterizeTriangle(Vector2 aUv, Vector2 bUv, Vector2 cUv,
            int width, int height, byte[] mask)
        {
            float minU = Mathf.Min(aUv.x, Mathf.Min(bUv.x, cUv.x));
            float maxU = Mathf.Max(aUv.x, Mathf.Max(bUv.x, cUv.x));
            float minV = Mathf.Min(aUv.y, Mathf.Min(bUv.y, cUv.y));
            float maxV = Mathf.Max(aUv.y, Mathf.Max(bUv.y, cUv.y));
            if (maxU < 0f || minU > 1f || maxV < 0f || minV > 1f) return;

            int xMin = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(minU) * width), 0, width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(maxU) * width) - 1, 0, width - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(minV) * height), 0, height - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(maxV) * height) - 1, 0, height - 1);

            Vector2 a = new Vector2(aUv.x * width, aUv.y * height);
            Vector2 b = new Vector2(bUv.x * width, bUv.y * height);
            Vector2 c = new Vector2(cUv.x * width, cUv.y * height);
            float area = Cross(b - a, c - a);
            if (Mathf.Abs(area) < 0.000001f) return;

            const float epsilon = 0.0001f;
            for (int y = yMin; y <= yMax; y++)
            {
                float py = y + 0.5f;
                for (int x = xMin; x <= xMax; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, py);
                    float w0 = Cross(b - p, c - p) / area;
                    float w1 = Cross(c - p, a - p) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 >= -epsilon && w1 >= -epsilon && w2 >= -epsilon)
                        mask[y * width + x] = 1;
                }
            }
        }

        private static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
        private static long Quantize(float value, double scale) { return (long)Math.Round(value * scale); }
    }
}
