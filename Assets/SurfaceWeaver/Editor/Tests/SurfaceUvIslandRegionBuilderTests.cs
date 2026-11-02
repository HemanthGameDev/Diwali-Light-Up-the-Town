using NUnit.Framework;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Editor;

namespace HemanthGameDev.SurfaceWeaver.Editor.Tests
{
    public sealed class SurfaceUvIslandRegionBuilderTests
    {
        [Test]
        public void TryBuild_SeparatesTrianglesAcrossUvSeam()
        {
            var mesh = new Mesh { name = "FloodFill UV seam test" };
            try
            {
                // Two triangles share a geometric edge, but their UV coordinates do not match
                // at that edge. They must therefore be treated as separate UV charts.
                mesh.vertices = new[]
                {
                    new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0),
                    new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0)
                };
                mesh.uv = new[]
                {
                    new Vector2(0.05f, 0.05f), new Vector2(0.45f, 0.05f), new Vector2(0.05f, 0.45f),
                    new Vector2(0.55f, 0.55f), new Vector2(0.95f, 0.95f), new Vector2(0.95f, 0.55f)
                };
                mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };

                bool success = SurfaceUvIslandRegionBuilder.TryBuild(
                    mesh, 0, 64, 64, new Vector2(0.15f, 0.15f),
                    out byte[] mask, out string error);

                Assert.IsTrue(success, error);
                Assert.IsNotNull(mask);
                int firstIslandPixel = 10 * 64 + 10;
                int secondIslandPixel = 52 * 64 + 52;
                Assert.AreEqual(1, mask[firstIslandPixel]);
                Assert.AreEqual(0, mask[secondIslandPixel]);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void TryBuild_RejectsInvalidSeedTriangle()
        {
            var mesh = new Mesh { name = "Invalid seed test" };
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
                mesh.triangles = new[] { 0, 1, 2 };

                bool success = SurfaceUvIslandRegionBuilder.TryBuild(
                    mesh, 1, 16, 16, new Vector2(0.2f, 0.2f),
                    out byte[] mask, out string error);

                Assert.IsFalse(success);
                Assert.IsNull(mask);
                StringAssert.Contains("triangle index", error);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
