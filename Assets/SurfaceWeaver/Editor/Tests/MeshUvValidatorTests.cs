using NUnit.Framework;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Editor;

namespace HemanthGameDev.SurfaceWeaver.Editor.Tests
{
    public sealed class MeshUvValidatorTests
    {
        [Test]
        public void Validate_ReportsReadableNormalizedTriangle()
        {
            var mesh = new Mesh { name = "UV validator valid triangle" };
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
                mesh.triangles = new[] { 0, 1, 2 };

                MeshUvValidationReport report = MeshUvValidator.Validate(mesh, 0);

                Assert.IsTrue(report.MeshReadable);
                Assert.AreEqual(3, report.VertexCount);
                Assert.AreEqual(3, report.UvCount);
                Assert.AreEqual(1, report.TriangleCount);
                Assert.AreEqual(0, report.OutOfRangeUvCount);
                Assert.IsTrue(report.IslandOverlapAnalysisCompleted);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Validate_RejectsMissingUvChannel()
        {
            var mesh = new Mesh { name = "UV validator missing UV" };
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2 };

                MeshUvValidationReport report = MeshUvValidator.Validate(mesh, 0);

                Assert.IsFalse(report.IsValid);
                Assert.IsTrue(report.Errors.Count > 0);
                StringAssert.Contains("UV channel is empty", report.Errors[0]);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Validate_RejectsUnsupportedChannelIndex()
        {
            var mesh = new Mesh { name = "UV validator channel index" };
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
                mesh.triangles = new[] { 0, 1, 2 };

                MeshUvValidationReport report = MeshUvValidator.Validate(mesh, 8);

                Assert.IsFalse(report.IsValid);
                StringAssert.Contains("between 0 and 7", report.Errors[0]);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
