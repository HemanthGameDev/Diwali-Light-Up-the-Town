using UnityEditor;
using UnityEngine;
using HemanthGameDev.SurfaceWeaver.Runtime;

/// <summary>
/// Temporary diagnostic for comparing MeshCollider hit normals with the geometric
/// and authored normals used by SurfaceWeaver's UV-raster projection shader.
/// Place this file under Assets/SurfaceWeaver/Editor/.
/// </summary>
public sealed class SurfaceWeaverPickNormalDiagnostic : EditorWindow
{
    [SerializeField] private SurfacePaintTarget target;
    [SerializeField] private float expectedFacingCutoff = 0.2f;
    [SerializeField] private bool captureSceneClick = true;
    private string status = "Assign a SurfacePaintTarget, then click the visible cylinder side in Scene View.";

    [MenuItem("Tools/SurfaceWeaver/Diagnostics/Pick Normal Diagnostic")]
    private static void Open()
    {
        var window = GetWindow<SurfaceWeaverPickNormalDiagnostic>("SW Normal Diagnostic");
        window.minSize = new Vector2(370, 190);
        window.Show();
    }

    private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
    private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    private void OnGUI()
    {
        EditorGUILayout.LabelField("SurfaceWeaver Pick-Normal Diagnostic", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This is a temporary diagnostic only. It does not paint or modify the mesh/mask. " +
            "It compares the collider hit normal with the geometric normal and the authored vertex normals " +
            "for the hit triangle.", MessageType.Info);

        target = (SurfacePaintTarget)EditorGUILayout.ObjectField(
            "Paint Target", target, typeof(SurfacePaintTarget), true);
        expectedFacingCutoff = EditorGUILayout.Slider(
            "Expected Shader Cutoff", expectedFacingCutoff, 0.05f, 0.95f);
        captureSceneClick = EditorGUILayout.ToggleLeft("Capture left-click in Scene View", captureSceneClick);

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(status, MessageType.None);
        if (GUILayout.Button("Select Target GameObject") && target != null)
            Selection.activeGameObject = target.gameObject;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!captureSceneClick || target == null || Event.current == null)
            return;

        MeshCollider collider = target.GetComponent<MeshCollider>();
        if (collider == null || collider.convex || !target.TryGetMesh(out Mesh mesh))
            return;

        Event e = Event.current;
        if (e.alt)
            return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!collider.Raycast(ray, out RaycastHit hit, 100000f))
            return;

        Handles.color = Color.cyan;
        Handles.DrawWireDisc(hit.point, hit.normal, 0.08f);

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            DiagnoseHit(mesh, collider, hit);
            e.Use();
        }
    }

    private void DiagnoseHit(Mesh mesh, MeshCollider collider, RaycastHit hit)
    {
        if (mesh.subMeshCount != 1)
        {
            status = "For an unambiguous first test, use a cylinder with exactly ONE triangle submesh. " +
                     $"This mesh has {mesh.subMeshCount} submeshes.";
            Repaint();
            Debug.LogWarning("[SurfaceWeaver Normal Diagnostic] " + status);
            return;
        }

        int[] triangles = mesh.GetTriangles(0);
        int tri = hit.triangleIndex;
        int baseIndex = tri * 3;
        if (tri < 0 || baseIndex + 2 >= triangles.Length)
        {
            status = $"Invalid triangle index {tri}. MeshCollider may be using a cooked/different mesh.";
            Repaint();
            Debug.LogError("[SurfaceWeaver Normal Diagnostic] " + status);
            return;
        }

        int i0 = triangles[baseIndex];
        int i1 = triangles[baseIndex + 1];
        int i2 = triangles[baseIndex + 2];
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        Vector3 p0 = vertices[i0];
        Vector3 p1 = vertices[i1];
        Vector3 p2 = vertices[i2];
        Vector3 geometricLocal = Vector3.Cross(p1 - p0, p2 - p0).normalized;

        Vector3 authoredLocal = Vector3.zero;
        bool hasAuthoredNormals = normals != null && normals.Length == vertices.Length;
        if (hasAuthoredNormals)
        {
            authoredLocal = (normals[i0] + normals[i1] + normals[i2]).normalized;
        }

        // Match the exact orientation rule used by SurfaceUvRasterMesh.TryBuild.
        Vector3 rasterLocal = geometricLocal;
        if (hasAuthoredNormals && authoredLocal.sqrMagnitude > 1e-8f &&
            Vector3.Dot(rasterLocal, authoredLocal) < 0f)
            rasterLocal = -rasterLocal;

        Matrix4x4 localToWorld = target.transform.localToWorldMatrix;
        if (Mathf.Abs(localToWorld.determinant) < 1e-7f)
        {
            status = "Target transform is singular/zero-scale.";
            Repaint();
            Debug.LogError("[SurfaceWeaver Normal Diagnostic] " + status);
            return;
        }

        Matrix4x4 normalMatrix = localToWorld.inverse.transpose;
        Vector3 geometricWorld = normalMatrix.MultiplyVector(geometricLocal).normalized;
        Vector3 rasterWorld = normalMatrix.MultiplyVector(rasterLocal).normalized;
        Vector3 authoredWorld = hasAuthoredNormals
            ? normalMatrix.MultiplyVector(authoredLocal).normalized
            : Vector3.zero;
        Vector3 hitNormal = hit.normal.normalized;

        float hitVsGeometric = Vector3.Dot(hitNormal, geometricWorld);
        float hitVsRaster = Vector3.Dot(hitNormal, rasterWorld);
        float hitVsAuthored = hasAuthoredNormals
            ? Vector3.Dot(hitNormal, authoredWorld)
            : float.NaN;

        bool colliderUsesSameMesh = collider.sharedMesh == mesh;
        Vector3 meshFilterPosition = target.TargetMeshFilter != null
            ? target.TargetMeshFilter.transform.position
            : target.transform.position;
        bool sameTransform = target.TargetMeshFilter == null ||
                             target.TargetMeshFilter.transform == target.transform;

        status =
            $"Triangle {tri}; UV0 {hit.textureCoord:F4};\n" +
            $"Hit·Geometric = {hitVsGeometric:F3}; Hit·Raster = {hitVsRaster:F3}; " +
            $"Hit·Authored = {(hasAuthoredNormals ? hitVsAuthored.ToString("F3") : "N/A")};\n" +
            $"Raster gate ({expectedFacingCutoff:F2}) = {(hitVsRaster >= expectedFacingCutoff ? "ACCEPT" : "REJECT")};\n" +
            $"Collider mesh same = {colliderUsesSameMesh}; Target/MeshFilter transform same = {sameTransform}.";

        string log =
            "[SurfaceWeaver Normal Diagnostic]\n" +
            $"Target: {target.name}\nMesh: {mesh.name}; submeshes: {mesh.subMeshCount}; triangle: {tri}\n" +
            $"UV0: {hit.textureCoord}\nHit point: {hit.point}\nHit normal: {hitNormal}\n" +
            $"Geometric local: {geometricLocal}; world: {geometricWorld}\n" +
            $"Raster local (oriented to authored normals): {rasterLocal}; world: {rasterWorld}\n" +
            (hasAuthoredNormals ? $"Authored average local: {authoredLocal}; world: {authoredWorld}\n" : "Authored normals: unavailable\n") +
            $"Dot(hit, geometric): {hitVsGeometric:F4}\n" +
            $"Dot(hit, raster): {hitVsRaster:F4}\n" +
            (hasAuthoredNormals ? $"Dot(hit, authored): {hitVsAuthored:F4}\n" : string.Empty) +
            $"Shader facing cutoff: {expectedFacingCutoff:F3}; would {(hitVsRaster >= expectedFacingCutoff ? "ACCEPT" : "REJECT")} this face.\n" +
            $"Collider.sharedMesh == MeshFilter.sharedMesh: {colliderUsesSameMesh}\n" +
            $"Target Transform == MeshFilter Transform: {sameTransform}\n" +
            $"MeshFilter position: {meshFilterPosition}";

        Debug.Log(log, target);
        Repaint();
    }
}
