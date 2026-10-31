using UnityEditor;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    [CustomEditor(typeof(SurfacePaintTarget))]
    public sealed class SurfacePaintTargetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);

            SurfacePaintTarget paintTarget = (SurfacePaintTarget)target;

            if (GUILayout.Button("Open SurfaceWeaver Painter"))
                SurfaceWeaverWindow.OpenFor(paintTarget);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);

            SurfacePaintMaterialBinder binder = paintTarget.GetComponent<SurfacePaintMaterialBinder>();
            if (GUILayout.Button("Create / Assign SurfaceWeaver URP Material"))
                CreateOrAssignMaterial(paintTarget, binder);

            if (binder != null && GUILayout.Button("Apply Profile to Renderer Now"))
            {
                bool applied = binder.ApplyProfile(out string report);
                if (paintTarget.TryGetRenderer(out MeshRenderer renderer) && renderer != null)
                {
                    Material[] slots = renderer.sharedMaterials;
                    int index = Mathf.Clamp(binder.MaterialIndex, 0, Mathf.Max(0, slots.Length - 1));
                    if (slots.Length > 0 && slots[index] != null)
                        EditorUtility.SetDirty(slots[index]);
                }
                if (applied)
                    AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("SurfaceWeaver", report, "OK");
            }

            if (paintTarget.TryGetMesh(out Mesh mesh))
            {
                EditorGUILayout.HelpBox(
                    "Mesh assigned: " + mesh.name + " (" + mesh.vertexCount + " vertices).",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This target needs a MeshFilter with a valid shared mesh.",
                    MessageType.Warning);
            }

            if (paintTarget.UvChannel != 0)
            {
                EditorGUILayout.HelpBox(
                    "The v0.7 rendering shader samples paint masks from UV0. Set this target's UV Channel to 0 for matching paint/render coordinates.",
                    MessageType.Warning);
            }

            if (paintTarget.TryGetRenderer(out MeshRenderer meshRenderer) && meshRenderer != null && meshRenderer.sharedMaterials.Length > 1)
            {
                EditorGUILayout.HelpBox(
                    "This initial v0.7 binder targets one selected material slot. Multi-profile submesh binding is not supported yet. The binder's Material Index chooses the slot.",
                    MessageType.Info);
            }

            EditorGUILayout.HelpBox(
                "v0.7 supports one packed RGBA mask page and up to four overlay layers, plus the base surface. Use Mesh UV0 for curved props; World Planar XZ is intended for mostly horizontal environments. The generated material is dedicated to this profile/target; do not share it between targets with different profiles.",
                MessageType.Info);
        }

        private static void CreateOrAssignMaterial(SurfacePaintTarget paintTarget, SurfacePaintMaterialBinder binder)
        {
            if (!paintTarget.TryGetRenderer(out MeshRenderer renderer) || renderer == null)
            {
                EditorUtility.DisplayDialog("SurfaceWeaver", "A MeshRenderer is required.", "OK");
                return;
            }

            if (paintTarget.Profile == null)
            {
                EditorUtility.DisplayDialog("SurfaceWeaver", "Assign a SurfacePaintProfile to the target first.", "OK");
                return;
            }

            Shader shader = Shader.Find(SurfacePaintMaterialBinder.ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("SurfaceWeaver", "Could not find shader: " + SurfacePaintMaterialBinder.ShaderName, "OK");
                return;
            }

            int materialIndex = binder != null ? binder.MaterialIndex : 0;
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0)
                materials = new Material[1];
            materialIndex = Mathf.Clamp(materialIndex, 0, materials.Length - 1);

            string path = EditorUtility.SaveFilePanelInProject(
                "Create SurfaceWeaver URP Material",
                paintTarget.name + "_SurfaceWeaver",
                "mat",
                "Choose where to save the generated material.");
            if (string.IsNullOrEmpty(path))
                return;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                EditorUtility.DisplayDialog("SurfaceWeaver", "The selected path is occupied by a non-material asset. Choose another path.", "OK");
                return;
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                Undo.RecordObject(material, "Update SurfaceWeaver Material");
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            Undo.RecordObject(renderer, "Assign SurfaceWeaver Material");
            materials[materialIndex] = material;
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);

            if (binder == null)
                binder = Undo.AddComponent<SurfacePaintMaterialBinder>(paintTarget.gameObject);
            Undo.RecordObject(binder, "Configure SurfaceWeaver Material Binder");
            binder.Configure(paintTarget, materialIndex);
            if (!binder.ApplyProfile(out string report))
            {
                EditorUtility.DisplayDialog("SurfaceWeaver", report, "OK");
                return;
            }

            EditorUtility.SetDirty(binder);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("SurfaceWeaver", report, "OK");
        }
    }
}
