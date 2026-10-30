using System;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>
    /// GPU-backed mask painter. Each stamp reads one RenderTexture and writes to another,
    /// then swaps them. The source and destination are never the same texture.
    /// </summary>
    internal sealed class SurfaceGpuBrushEngine : IDisposable
    {
        private const string ShaderName = "Hidden/SurfaceWeaver/MaskStamp";

        private static readonly int UvCenterId = Shader.PropertyToID("_UvCenter");
        private static readonly int WorldPerUvUId = Shader.PropertyToID("_WorldPerUvU");
        private static readonly int WorldPerUvVId = Shader.PropertyToID("_WorldPerUvV");
        private static readonly int RadiusWorldId = Shader.PropertyToID("_RadiusWorld");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int HardnessId = Shader.PropertyToID("_Hardness");
        private static readonly int ChannelIndexId = Shader.PropertyToID("_ChannelIndex");
        private static readonly int TargetValueId = Shader.PropertyToID("_TargetValue");
        private static readonly int BrushTextureId = Shader.PropertyToID("_BrushTex");
        private static readonly int UseBrushAlphaId = Shader.PropertyToID("_UseBrushAlpha");
        private static readonly int BrushRotationId = Shader.PropertyToID("_BrushRotation");
        private static readonly int FillModeId = Shader.PropertyToID("_FillMode");

        private RenderTexture readTexture;
        private RenderTexture writeTexture;
        private Material stampMaterial;
        private RenderTexture pendingStrokeSnapshot;
        private int pendingStrokeStampCount;
        private readonly System.Collections.Generic.List<RenderTexture> undoHistory =
            new System.Collections.Generic.List<RenderTexture>();
        private readonly System.Collections.Generic.List<RenderTexture> redoHistory =
            new System.Collections.Generic.List<RenderTexture>();
        private long historyBudgetBytes = 64L * 1024L * 1024L;
        private bool initialized;

        public RenderTexture Current => readTexture;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool IsInitialized => initialized && readTexture != null;
        public bool CanUndo => undoHistory.Count > 0;
        public bool CanRedo => redoHistory.Count > 0;
        public int UndoCount => undoHistory.Count;
        public int RedoCount => redoHistory.Count;
        public long HistoryMemoryBytes =>
            (long)(undoHistory.Count + redoHistory.Count + (pendingStrokeSnapshot != null ? 1 : 0)) *
            Width * Height * 4L;

        public void SetHistoryBudgetMegabytes(int megabytes)
        {
            historyBudgetBytes = Math.Max(8L * 1024L * 1024L, megabytes * 1024L * 1024L);
            TrimHistoryToBudget();
        }

        /// <summary>Captures the pre-stroke page once. A completed stroke becomes one Undo step.</summary>
        public bool BeginStroke(out string error)
        {
            error = string.Empty;
            if (!IsInitialized)
            {
                error = "The mask painting session is not initialized.";
                return false;
            }

            if (pendingStrokeSnapshot != null)
                EndStroke();

            pendingStrokeSnapshot = CreateSnapshot(readTexture, "SurfaceWeaver Pending Undo");
            pendingStrokeStampCount = 0;
            if (pendingStrokeSnapshot == null)
            {
                error = "Could not allocate an Undo snapshot. Reduce mask resolution or increase available GPU memory.";
                return false;
            }

            return true;
        }

        /// <summary>Commits the pending pre-stroke snapshot to the Undo history.</summary>
        public void EndStroke()
        {
            if (pendingStrokeSnapshot == null)
                return;

            if (pendingStrokeStampCount <= 0)
            {
                ReleaseRenderTexture(ref pendingStrokeSnapshot);
                return;
            }

            ClearHistory(redoHistory);
            undoHistory.Add(pendingStrokeSnapshot);
            pendingStrokeSnapshot = null;
            pendingStrokeStampCount = 0;
            TrimHistoryToBudget();
        }

        /// <summary>Restores the page to its state before the active stroke.</summary>
        public bool CancelStroke(out string error)
        {
            error = string.Empty;
            if (pendingStrokeSnapshot == null)
                return true;

            if (!RestoreSnapshot(pendingStrokeSnapshot, out error))
                return false;

            ReleaseRenderTexture(ref pendingStrokeSnapshot);
            pendingStrokeStampCount = 0;
            return true;
        }

        public bool UndoLastStroke(out string error)
        {
            error = string.Empty;
            if (!IsInitialized || pendingStrokeSnapshot != null)
            {
                error = "Finish the active stroke before using Undo.";
                return false;
            }
            if (undoHistory.Count == 0)
            {
                error = "There are no strokes to undo.";
                return false;
            }

            RenderTexture redoSnapshot = CreateSnapshot(readTexture, "SurfaceWeaver Redo");
            if (redoSnapshot == null)
            {
                error = "Could not allocate a Redo snapshot.";
                return false;
            }

            int last = undoHistory.Count - 1;
            RenderTexture previousState = undoHistory[last];
            undoHistory.RemoveAt(last);

            if (!RestoreSnapshot(previousState, out error))
            {
                undoHistory.Add(previousState);
                ReleaseRenderTexture(ref redoSnapshot);
                return false;
            }

            ReleaseRenderTexture(ref previousState);
            redoHistory.Add(redoSnapshot);
            TrimHistoryToBudget();
            return true;
        }

        public bool RedoLastStroke(out string error)
        {
            error = string.Empty;
            if (!IsInitialized || pendingStrokeSnapshot != null)
            {
                error = "Finish the active stroke before using Redo.";
                return false;
            }
            if (redoHistory.Count == 0)
            {
                error = "There are no strokes to redo.";
                return false;
            }

            RenderTexture undoSnapshot = CreateSnapshot(readTexture, "SurfaceWeaver Undo");
            if (undoSnapshot == null)
            {
                error = "Could not allocate an Undo snapshot.";
                return false;
            }

            int last = redoHistory.Count - 1;
            RenderTexture nextState = redoHistory[last];
            redoHistory.RemoveAt(last);

            if (!RestoreSnapshot(nextState, out error))
            {
                redoHistory.Add(nextState);
                ReleaseRenderTexture(ref undoSnapshot);
                return false;
            }

            ReleaseRenderTexture(ref nextState);
            undoHistory.Add(undoSnapshot);
            TrimHistoryToBudget();
            return true;
        }

        public bool Initialize(Texture2D source, int width, int height, out string error)
        {
            error = string.Empty;
            Dispose();

            if (width <= 0 || height <= 0)
            {
                error = "Mask dimensions must be greater than zero.";
                return false;
            }

            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
            {
                error = "This graphics device does not support ARGB32 RenderTextures required for packed RGBA mask pages.";
                return false;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                error = "Could not find " + ShaderName + ". Ensure SurfaceMaskStamp.shader is imported and has no compile errors.";
                return false;
            }

            if (!shader.isSupported)
            {
                error = "The SurfaceWeaver mask stamp shader is not supported by the current graphics device/API.";
                return false;
            }

            stampMaterial = new Material(shader)
            {
                name = "SurfaceWeaver Mask Stamp (Editor)",
                hideFlags = HideFlags.HideAndDontSave
            };

            readTexture = CreateRenderTexture(width, height, "SurfaceWeaver Mask Read");
            writeTexture = CreateRenderTexture(width, height, "SurfaceWeaver Mask Write");

            if (readTexture == null || writeTexture == null)
            {
                error = "Failed to create the mask RenderTextures. Check graphics format support and available GPU memory.";
                Dispose();
                return false;
            }

            Texture initial = source != null ? (Texture)source : Texture2D.blackTexture;
            RenderTexture previousActive = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = false;
                Graphics.Blit(initial, readTexture);
                Graphics.Blit(readTexture, writeTexture);
            }
            catch (Exception exception)
            {
                error = "Could not initialize GPU mask buffers: " + exception.Message;
                Dispose();
                return false;
            }
            finally
            {
                GL.sRGBWrite = previousSrgbWrite;
                RenderTexture.active = previousActive;
            }

            Width = width;
            Height = height;
            initialized = true;
            return true;
        }

        public bool Stamp(
            Vector2 uvCenter,
            Vector3 worldPerUvU,
            Vector3 worldPerUvV,
            float radiusWorldUnits,
            float opacity,
            float hardness,
            int channelIndex,
            bool erase,
            Texture2D brushAlpha,
            float brushRotationDegrees,
            out string error)
        {
            error = string.Empty;
            if (!IsInitialized || stampMaterial == null)
            {
                error = "The mask painting session is not initialized.";
                return false;
            }

            if (channelIndex < 0 || channelIndex > 3)
            {
                error = "Packed mask channel must be between 0 (R) and 3 (A).";
                return false;
            }

            stampMaterial.SetVector(UvCenterId, new Vector4(uvCenter.x, uvCenter.y, 0f, 0f));
            stampMaterial.SetVector(WorldPerUvUId, worldPerUvU);
            stampMaterial.SetVector(WorldPerUvVId, worldPerUvV);
            stampMaterial.SetFloat(RadiusWorldId, Mathf.Max(0.001f, radiusWorldUnits));
            stampMaterial.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            stampMaterial.SetFloat(HardnessId, Mathf.Clamp01(hardness));
            stampMaterial.SetFloat(ChannelIndexId, channelIndex);
            stampMaterial.SetFloat(TargetValueId, erase ? 0f : 1f);
            stampMaterial.SetTexture(BrushTextureId,
                brushAlpha != null ? (Texture)brushAlpha : Texture2D.whiteTexture);
            stampMaterial.SetFloat(UseBrushAlphaId, brushAlpha != null ? 1f : 0f);
            stampMaterial.SetFloat(BrushRotationId, brushRotationDegrees * Mathf.Deg2Rad);
            stampMaterial.SetFloat(FillModeId, 0f);

            try
            {
                ExecuteBlit();
                if (pendingStrokeSnapshot != null)
                    pendingStrokeStampCount++;
                return true;
            }
            catch (Exception exception)
            {
                error = "GPU brush stamp failed: " + exception.Message;
                return false;
            }
        }

        public bool ClearChannel(int channelIndex, out string error)
        {
            error = string.Empty;
            if (!IsInitialized || stampMaterial == null)
            {
                error = "The mask painting session is not initialized.";
                return false;
            }

            if (channelIndex < 0 || channelIndex > 3)
            {
                error = "Packed mask channel must be between 0 (R) and 3 (A).";
                return false;
            }

            stampMaterial.SetFloat(ChannelIndexId, channelIndex);
            stampMaterial.SetFloat(TargetValueId, 0f);
            stampMaterial.SetFloat(OpacityId, 1f);
            stampMaterial.SetFloat(FillModeId, 1f);
            try
            {
                ExecuteBlit();
                if (pendingStrokeSnapshot != null)
                    pendingStrokeStampCount++;
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not clear the selected mask channel: " + exception.Message;
                return false;
            }
            finally
            {
                stampMaterial.SetFloat(FillModeId, 0f);
            }
        }

        public Texture2D CreateReadbackTexture(out string error)
        {
            error = string.Empty;
            if (!IsInitialized)
            {
                error = "The mask painting session is not initialized.";
                return null;
            }

            Texture2D result = new Texture2D(
                Width,
                Height,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "SurfaceWeaver Mask Readback"
            };

            RenderTexture previousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = readTexture;
                result.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                result.Apply(false, false);
                return result;
            }
            catch (Exception exception)
            {
                error = "GPU mask readback failed: " + exception.Message;
                UnityEngine.Object.DestroyImmediate(result);
                return null;
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        public void Dispose()
        {
            initialized = false;
            ReleaseRenderTexture(ref pendingStrokeSnapshot);
            pendingStrokeStampCount = 0;
            ClearHistory(undoHistory);
            ClearHistory(redoHistory);
            ReleaseRenderTexture(ref readTexture);
            ReleaseRenderTexture(ref writeTexture);

            if (stampMaterial != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(stampMaterial);
                else
                    UnityEngine.Object.DestroyImmediate(stampMaterial);
                stampMaterial = null;
            }

            Width = 0;
            Height = 0;
        }

        private RenderTexture CreateSnapshot(RenderTexture source, string textureName)
        {
            if (source == null)
                return null;

            RenderTexture snapshot = CreateRenderTexture(
                source.width,
                source.height,
                textureName);
            if (snapshot == null)
                return null;

            try
            {
                Blit(source, snapshot);
                return snapshot;
            }
            catch (Exception exception)
            {
                Debug.LogError("SurfaceWeaver snapshot failed: " + exception.Message);
                ReleaseRenderTexture(ref snapshot);
                return null;
            }
        }

        private bool RestoreSnapshot(RenderTexture snapshot, out string error)
        {
            error = string.Empty;
            if (snapshot == null || readTexture == null || writeTexture == null)
            {
                error = "Cannot restore an invalid mask history snapshot.";
                return false;
            }

            try
            {
                Blit(snapshot, readTexture);
                Blit(snapshot, writeTexture);
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not restore mask history: " + exception.Message;
                return false;
            }
        }

        private static void Blit(Texture source, RenderTexture destination)
        {
            RenderTexture previousActive = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = false;
                Graphics.Blit(source, destination);
            }
            finally
            {
                GL.sRGBWrite = previousSrgbWrite;
                RenderTexture.active = previousActive;
            }
        }

        private void TrimHistoryToBudget()
        {
            long bytesPerSnapshot = (long)Width * Height * 4L;
            if (bytesPerSnapshot <= 0)
                return;

            while ((long)(undoHistory.Count + redoHistory.Count) * bytesPerSnapshot > historyBudgetBytes)
            {
                if (undoHistory.Count > 0)
                {
                    RenderTexture oldest = undoHistory[0];
                    undoHistory.RemoveAt(0);
                    ReleaseRenderTexture(ref oldest);
                }
                else if (redoHistory.Count > 0)
                {
                    RenderTexture oldest = redoHistory[0];
                    redoHistory.RemoveAt(0);
                    ReleaseRenderTexture(ref oldest);
                }
                else
                {
                    break;
                }
            }
        }

        private static void ClearHistory(System.Collections.Generic.List<RenderTexture> history)
        {
            for (int i = 0; i < history.Count; i++)
            {
                RenderTexture snapshot = history[i];
                ReleaseRenderTexture(ref snapshot);
            }
            history.Clear();
        }

        private void ExecuteBlit()
        {
            // Unity documents that source and destination must not be the same RenderTexture.
            RenderTexture previousActive = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = false;
                Graphics.Blit(readTexture, writeTexture, stampMaterial, 0);
            }
            finally
            {
                GL.sRGBWrite = previousSrgbWrite;
                RenderTexture.active = previousActive;
            }

            RenderTexture swap = readTexture;
            readTexture = writeTexture;
            writeTexture = swap;
        }

        private static RenderTexture CreateRenderTexture(int width, int height, string textureName)
        {
            RenderTexture texture = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };

            if (!texture.Create())
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            return texture;
        }

        private static void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            if (texture.IsCreated())
                texture.Release();

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);

            texture = null;
        }
    }
}
