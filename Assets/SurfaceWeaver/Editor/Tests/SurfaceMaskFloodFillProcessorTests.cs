using NUnit.Framework;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Editor;

namespace HemanthGameDev.SurfaceWeaver.Editor.Tests
{
    public sealed class SurfaceMaskFloodFillProcessorTests
    {
        [Test]
        public void TryFill_ChangesSelectedChannel_AndPreservesOtherChannels()
        {
            var pixels = new Color32[9];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(64, 20, 30, 40);

            bool success = SurfaceMaskFloodFillProcessor.TryFill(
                pixels, 3, 3, 1, 1, 0, 0f, 1f, false,
                out int changed, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(9, changed);
            for (int i = 0; i < pixels.Length; i++)
            {
                Assert.AreEqual(255, pixels[i].r);
                Assert.AreEqual(20, pixels[i].g);
                Assert.AreEqual(30, pixels[i].b);
                Assert.AreEqual(40, pixels[i].a);
            }
        }

        [Test]
        public void TryFill_FourNeighbour_DoesNotCrossDiagonalConnection()
        {
            var pixels = new Color32[4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 10, 20, 30);
            pixels[0] = new Color32(50, 10, 20, 30);
            pixels[3] = new Color32(50, 10, 20, 30);

            bool success = SurfaceMaskFloodFillProcessor.TryFill(
                pixels, 2, 2, 0, 0, 0, 0f, 1f, false,
                out int changed, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(255, pixels[0].r);
            Assert.AreEqual(50, pixels[3].r);
        }

        [Test]
        public void TryFill_EightNeighbour_IncludesDiagonalConnection()
        {
            var pixels = new Color32[4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 10, 20, 30);
            pixels[0] = new Color32(50, 10, 20, 30);
            pixels[3] = new Color32(50, 10, 20, 30);

            bool success = SurfaceMaskFloodFillProcessor.TryFill(
                pixels, 2, 2, 0, 0, 0, 0f, 1f, true,
                out int changed, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(2, changed);
            Assert.AreEqual(255, pixels[0].r);
            Assert.AreEqual(255, pixels[3].r);
        }

        [Test]
        public void TryFill_UsesToleranceAgainstSeedValue()
        {
            var pixels = new[]
            {
                new Color32(100, 1, 2, 3),
                new Color32(110, 1, 2, 3),
                new Color32(140, 1, 2, 3),
            };

            bool success = SurfaceMaskFloodFillProcessor.TryFill(
                pixels, 3, 1, 0, 0, 0, 0.05f, 0.5f, false,
                out int changed, out string error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(2, changed); // 10/255 is within 0.05; 40/255 is not.
            Assert.AreEqual(128, pixels[0].r);
            Assert.AreEqual(128, pixels[1].r);
            Assert.AreEqual(140, pixels[2].r);
        }

        [Test]
        public void TryFill_RespectsAllowedUvRegion()
        {
            var pixels = new Color32[4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(20, 30, 40, 50);
            var allowed = new byte[] { 1, 1, 0, 0 };

            bool success = SurfaceMaskFloodFillProcessor.TryFill(
                pixels, 2, 2, 0, 0, 0, 0f, 1f, true,
                out int changed, out string error, allowed);

            Assert.IsTrue(success, error);
            Assert.AreEqual(2, changed);
            Assert.AreEqual((byte)255, pixels[0].r);
            Assert.AreEqual((byte)255, pixels[1].r);
            Assert.AreEqual((byte)20, pixels[2].r);
            Assert.AreEqual((byte)20, pixels[3].r);
        }

        [Test]
        public void TryFill_RejectsMismatchedAllowedRegion()
        {
            var pixels = new Color32[4];
            bool success = SurfaceMaskFloodFillProcessor.TryFill(
                pixels, 2, 2, 0, 0, 0, 0f, 1f, false,
                out _, out string error, new byte[3]);

            Assert.IsFalse(success);
            StringAssert.Contains("dimensions", error);
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(2, 0)]
        [TestCase(0, 2)]
        public void TryFill_RejectsSeedOutsideMask(int seedX, int seedY)
        {
            bool success = SurfaceMaskFloodFillProcessor.TryFill(
                new Color32[4], 2, 2, seedX, seedY, 0, 0f, 1f, false,
                out _, out string error);

            Assert.IsFalse(success);
            StringAssert.Contains("outside", error);
        }

        [Test]
        public void TryFill_RejectsInvalidChannel()
        {
            bool success = SurfaceMaskFloodFillProcessor.TryFill(
                new Color32[4], 2, 2, 0, 0, 4, 0f, 1f, false,
                out _, out string error);

            Assert.IsFalse(success);
            StringAssert.Contains("channel", error);
        }

        [Test]
        public void TryFill_RejectsInvalidBufferDimensions()
        {
            bool success = SurfaceMaskFloodFillProcessor.TryFill(
                new Color32[3], 2, 2, 0, 0, 0, 0f, 1f, false,
                out _, out string error);

            Assert.IsFalse(success);
            StringAssert.Contains("invalid mask buffer", error);
        }
    }
}
