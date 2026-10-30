using UnityEditor;
using UnityEngine;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>Draws a world-space brush footprint aligned to the picked surface normal.</summary>
    internal static class SurfaceBrushPreview
    {
        public static void Draw(
            SurfaceSceneHit hit,
            float radiusWorldUnits,
            Color previewColor)
        {
            float radius = Mathf.Max(0.001f, radiusWorldUnits);
            Vector3 normal = hit.Normal.sqrMagnitude > 0.000001f
                ? hit.Normal.normalized
                : Vector3.up;

            Vector3 center = hit.Point + normal * 0.008f;
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();

            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            float crossRadius = Mathf.Min(radius * 0.12f, 0.25f);

            Color previousColor = Handles.color;
            Handles.color = previewColor;

            Handles.DrawWireDisc(center, normal, radius);
            Handles.DrawWireDisc(center, normal, crossRadius * 0.25f);
            Handles.DrawLine(center - tangent * crossRadius, center + tangent * crossRadius);
            Handles.DrawLine(center - bitangent * crossRadius, center + bitangent * crossRadius);

            Handles.Label(
                center + normal * Mathf.Max(0.04f, radius * 0.04f),
                $"UV0  ({hit.Uv0.x:0.0000}, {hit.Uv0.y:0.0000})"
            );

            Handles.color = previousColor;
        }
    }
}
