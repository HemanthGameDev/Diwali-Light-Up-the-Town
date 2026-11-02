using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using HemanthGameDev.SurfaceWeaver.Editor;

namespace HemanthGameDev.SurfaceWeaver.Editor.Tests
{
    /// <summary>
    /// GPU-backed EditMode regression tests for the same stroke-history engine used by
    /// the single-target Painter and the per-target sessions in Multi-Mesh Painter.
    /// Tests are explicitly ignored when the Editor has no usable graphics device.
    /// </summary>
    public sealed class SurfaceGpuBrushEngineUndoTests
    {
        private SurfaceGpuBrushEngine engine;

        [SetUp]
        public void SetUp()
        {
            EnsureGpuTestEnvironment();
            engine = new SurfaceGpuBrushEngine();
            if (!engine.Initialize(null, 16, 16, out string error))
            {
                engine.Dispose();
                engine = null;
                Assert.Ignore("SurfaceWeaver GPU history tests require the mask stamp shader and ARGB32 RenderTextures: " + error);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (engine != null)
            {
                engine.Dispose();
                engine = null;
            }
        }

        [Test]
        public void CompletedStroke_IsOneUndoStep_AndCanRedo()
        {
            Assert.IsTrue(engine.BeginStroke(out string beginError), beginError);
            Assert.IsTrue(engine.SetChannelValue(0, 1f, out string fillError), fillError);
            engine.EndStroke();

            Assert.AreEqual(1, engine.UndoCount, "A completed stroke should create exactly one Undo entry.");
            AssertColorChannel(0, expected: 255);

            Assert.IsTrue(engine.UndoLastStroke(out string undoError), undoError);
            Assert.AreEqual(0, engine.UndoCount);
            Assert.AreEqual(1, engine.RedoCount);
            AssertColorChannel(0, expected: 0);

            Assert.IsTrue(engine.RedoLastStroke(out string redoError), redoError);
            Assert.AreEqual(1, engine.UndoCount);
            Assert.AreEqual(0, engine.RedoCount);
            AssertColorChannel(0, expected: 255);
        }

        [Test]
        public void MultipleOperationsInsideStroke_CommitAsOneUndoEntry()
        {
            Assert.IsTrue(engine.BeginStroke(out string beginError), beginError);
            Assert.IsTrue(engine.SetChannelValue(0, 1f, out string redError), redError);
            Assert.IsTrue(engine.SetChannelValue(1, 0.5f, out string greenError), greenError);
            engine.EndStroke();

            Assert.AreEqual(1, engine.UndoCount);
            Color32 painted = ReadPixel();
            Assert.AreEqual(255, painted.r);
            Assert.That(painted.g, Is.InRange(126, 129));

            Assert.IsTrue(engine.UndoLastStroke(out string undoError), undoError);
            Color32 restored = ReadPixel();
            Assert.AreEqual(0, restored.r);
            Assert.AreEqual(0, restored.g);

            Assert.IsTrue(engine.RedoLastStroke(out string redoError), redoError);
            Color32 reapplied = ReadPixel();
            Assert.AreEqual(255, reapplied.r);
            Assert.That(reapplied.g, Is.InRange(126, 129));
        }

        [Test]
        public void CancelStroke_RestoresBaseline_AndDoesNotCreateUndoEntry()
        {
            Color32 before = ReadPixel();
            Assert.IsTrue(engine.BeginStroke(out string beginError), beginError);
            Assert.IsTrue(engine.SetChannelValue(2, 1f, out string fillError), fillError);

            Assert.IsTrue(engine.CancelStroke(out string cancelError), cancelError);

            Assert.AreEqual(0, engine.UndoCount);
            Assert.AreEqual(0, engine.RedoCount);
            Color32 after = ReadPixel();
            Assert.AreEqual(before.r, after.r);
            Assert.AreEqual(before.g, after.g);
            Assert.AreEqual(before.b, after.b);
            Assert.AreEqual(before.a, after.a);
        }

        [Test]
        public void UndoAndRedo_AreRejectedWhileStrokeIsPending()
        {
            Assert.IsTrue(engine.BeginStroke(out string beginError), beginError);
            Assert.IsTrue(engine.SetChannelValue(0, 1f, out string fillError), fillError);

            Assert.IsFalse(engine.UndoLastStroke(out string undoError));
            StringAssert.Contains("Finish the active stroke", undoError);
            Assert.IsFalse(engine.RedoLastStroke(out string redoError));
            StringAssert.Contains("Finish the active stroke", redoError);

            Assert.IsTrue(engine.CancelStroke(out string cancelError), cancelError);
        }

        [Test]
        public void NewCommittedStroke_AfterUndo_ClearsRedoHistory()
        {
            Assert.IsTrue(engine.BeginStroke(out string firstBeginError), firstBeginError);
            Assert.IsTrue(engine.SetChannelValue(0, 1f, out string firstFillError), firstFillError);
            engine.EndStroke();
            Assert.IsTrue(engine.UndoLastStroke(out string undoError), undoError);
            Assert.IsTrue(engine.CanRedo);

            Assert.IsTrue(engine.BeginStroke(out string secondBeginError), secondBeginError);
            Assert.IsTrue(engine.SetChannelValue(1, 1f, out string secondFillError), secondFillError);
            engine.EndStroke();

            Assert.IsFalse(engine.CanRedo, "A new committed branch must discard stale Redo history.");
            Assert.AreEqual(1, engine.UndoCount);
        }

        [Test]
        public void PixelProcessingOperation_IsUndoableAndRedoable()
        {
            var changedPixels = new Color32[engine.Width * engine.Height];
            for (int i = 0; i < changedPixels.Length; i++)
                changedPixels[i] = new Color32(180, 25, 35, 45);

            Assert.IsTrue(engine.ApplyPixelChanges(changedPixels, out string applyError), applyError);
            Assert.AreEqual(1, engine.UndoCount);
            Color32 changed = ReadPixel();
            Assert.AreEqual(180, changed.r);
            Assert.AreEqual(25, changed.g);
            Assert.AreEqual(35, changed.b);
            Assert.AreEqual(45, changed.a);

            Assert.IsTrue(engine.UndoLastStroke(out string undoError), undoError);
            Color32 restored = ReadPixel();
            Assert.AreEqual(0, restored.r);
            Assert.AreEqual(0, restored.g);
            Assert.AreEqual(0, restored.b);

            Assert.IsTrue(engine.RedoLastStroke(out string redoError), redoError);
            Color32 reapplied = ReadPixel();
            Assert.AreEqual(180, reapplied.r);
            Assert.AreEqual(25, reapplied.g);
            Assert.AreEqual(35, reapplied.b);
            Assert.AreEqual(45, reapplied.a);
        }

        [Test]
        public void TwoTargetSessions_CanUndoAndRedoOneCrossTargetStroke()
        {
            var secondEngine = new SurfaceGpuBrushEngine();
            try
            {
                if (!secondEngine.Initialize(null, 16, 16, out string initializeError))
                    Assert.Ignore("Could not initialize second GPU session: " + initializeError);

                Assert.IsTrue(engine.BeginStroke(out string firstBeginError), firstBeginError);
                Assert.IsTrue(secondEngine.BeginStroke(out string secondBeginError), secondBeginError);
                Assert.IsTrue(engine.SetChannelValue(0, 1f, out string firstFillError), firstFillError);
                Assert.IsTrue(secondEngine.SetChannelValue(2, 1f, out string secondFillError), secondFillError);
                engine.EndStroke();
                secondEngine.EndStroke();

                Assert.IsTrue(engine.UndoLastStroke(out string firstUndoError), firstUndoError);
                Assert.IsTrue(secondEngine.UndoLastStroke(out string secondUndoError), secondUndoError);
                AssertColorChannel(0, 0);
                Assert.AreEqual(0, ReadPixel(secondEngine).b);

                Assert.IsTrue(engine.RedoLastStroke(out string firstRedoError), firstRedoError);
                Assert.IsTrue(secondEngine.RedoLastStroke(out string secondRedoError), secondRedoError);
                AssertColorChannel(0, 255);
                Assert.AreEqual(255, ReadPixel(secondEngine).b);
            }
            finally
            {
                secondEngine.Dispose();
            }
        }

        private static void EnsureGpuTestEnvironment()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
                Assert.Ignore("The current Unity Test Runner has no ARGB32-capable graphics device.");

            Shader shader = Shader.Find("Hidden/SurfaceWeaver/MaskStamp");
            if (shader == null || !shader.isSupported)
                Assert.Ignore("Hidden/SurfaceWeaver/MaskStamp is unavailable or unsupported in this Editor graphics configuration.");
        }

        private void AssertColorChannel(int channel, byte expected)
        {
            Color32 pixel = ReadPixel();
            byte actual = channel == 0 ? pixel.r : channel == 1 ? pixel.g : channel == 2 ? pixel.b : pixel.a;
            Assert.AreEqual(expected, actual);
        }

        private Color32 ReadPixel()
        {
            return ReadPixel(engine);
        }

        private static Color32 ReadPixel(SurfaceGpuBrushEngine targetEngine)
        {
            Texture2D readback = targetEngine.CreateReadbackTexture(out string error);
            Assert.IsNotNull(readback, error);
            try
            {
                return readback.GetPixel(0, 0);
            }
            finally
            {
                Object.DestroyImmediate(readback);
            }
        }
    }
}
