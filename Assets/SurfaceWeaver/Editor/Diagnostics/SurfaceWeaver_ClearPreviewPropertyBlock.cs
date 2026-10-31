#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor.Diagnostics
{
    /// <summary>
    /// Temporary diagnostic: clears renderer-wide and per-material MaterialPropertyBlocks
    /// on the selected GameObject only. It does not modify material assets or textures.
    /// Re-load the mask in SurfaceWeaver to restore its live preview binding.
    /// </summary>
    public static class SurfaceWeaverClearPreviewPropertyBlock
    {
        [MenuItem("Tools/SurfaceWeaver/Diagnostics/Clear Selected Renderer Property Blocks")]
        private static void ClearSelectedRendererPropertyBlocks()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("SurfaceWeaver Diagnostics", "Select the duplicate/test GameObject first.", "OK");
                return;
            }

            Renderer renderer = selected.GetComponent<Renderer>();
            if (renderer == null)
            {
                EditorUtility.DisplayDialog("SurfaceWeaver Diagnostics", "The selected GameObject has no Renderer component.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Clear preview property blocks?",
                    "This clears renderer-wide and per-material MaterialPropertyBlocks on '" + selected.name + "'.\n\n" +
                    "It does NOT modify material assets or mask textures. Other systems using property blocks on this renderer will also be cleared. Use this only on your duplicate/test object.",
                    "Clear Blocks", "Cancel"))
                return;

            renderer.SetPropertyBlock(null);
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                renderer.SetPropertyBlock(null, i);

            string materialSummary = "";
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                materialSummary += "\nSlot " + i + ": " +
                    (material != null ? material.name + " | " + (material.shader != null ? material.shader.name : "<no shader>") : "<null>");
            }

            Debug.Log("[SurfaceWeaver] Cleared renderer-wide and per-material property blocks on '" +
                      selected.name + "'. Material slots:" + materialSummary, selected);
            EditorUtility.DisplayDialog(
                "SurfaceWeaver Diagnostics",
                "Property blocks cleared on '" + selected.name + "'.\n\n" +
                "Check whether the base grass returns. The live mask preview is now detached; click Load / Reload Mask Page again to restore it.",
                "OK");
        }
    }
}
#endif
