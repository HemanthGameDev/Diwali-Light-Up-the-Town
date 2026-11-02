using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor.Tests
{
    public sealed class SurfacePaintPathAssetTests
    {
        [Test]
        public void Configure_RetainsPointsWidthPressureAndPathSettings()
        {
            var asset = ScriptableObject.CreateInstance<SurfacePaintPathAsset>();
            try
            {
                var points = new List<SurfacePaintPathPoint>
                {
                    new SurfacePaintPathPoint(Vector3.zero, Vector3.up, Vector3.left, Vector3.right, true, 0.5f, 0.25f, true),
                    new SurfacePaintPathPoint(Vector3.right, Vector3.up, Vector3.zero, Vector3.forward, true, 1.75f, 0.8f, true)
                };

                asset.Configure(
                    "target-id", "Test Target", points, "layer-id", 0,
                    SurfaceMaskChannel.Blue, 1.5f, 0.65f, 0.3f, 0.2f,
                    false, false, null, null, 25f, 0.1f, true, true, 9876,
                    true, false);

                Assert.AreEqual("target-id", asset.TargetGlobalObjectId);
                Assert.AreEqual(2, asset.Points.Count);
                Assert.AreEqual(0.5f, asset.Points[0].WidthScale, 0.0001f);
                Assert.AreEqual(0.25f, asset.Points[0].Pressure, 0.0001f);
                Assert.AreEqual(1.75f, asset.Points[1].WidthScale, 0.0001f);
                Assert.AreEqual(0.8f, asset.Points[1].Pressure, 0.0001f);
                Assert.AreEqual(SurfaceMaskChannel.Blue, asset.MaskChannel);
                Assert.IsTrue(asset.ClosedLoop);
                Assert.IsTrue(asset.PressureAffectsWidth);
                Assert.IsFalse(asset.PressureAffectsOpacity);
                Assert.AreEqual(9876, asset.RandomSeed);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void PathPoint_ClampsWidthAndPressure()
        {
            var point = new SurfacePaintPathPoint(
                Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero,
                false, 20f, -2f, true);

            Assert.AreEqual(4f, point.WidthScale, 0.0001f);
            Assert.AreEqual(0f, point.Pressure, 0.0001f);
            Assert.AreEqual(Vector3.up, point.LocalNormal);
        }
    }
}
