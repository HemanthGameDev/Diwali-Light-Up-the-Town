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

            if (paintTarget.TryGetMesh(out Mesh mesh))
            {
                EditorGUILayout.HelpBox(
                    "Mesh assigned: " + mesh.name + " (" + mesh.vertexCount + " vertices).",
                    MessageType.Info
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This target needs a MeshFilter with a valid shared mesh.",
                    MessageType.Warning
                );
            }
        }
    }
}
