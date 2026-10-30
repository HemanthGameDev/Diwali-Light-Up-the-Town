
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class GroundPaintEditor : EditorWindow
{
    private enum BrushMode
    {
        PaintDirt,
        EraseDirt
    }

    private const string MaskProperty = "_PaintMask";

    [SerializeField] private GameObject groundObject;
    [SerializeField] private Material groundMaterial;

    [SerializeField]
    private string maskPath =
        "Assets/Environment/Textures/T_GroundPaintMask.png";

    [SerializeField] private int resolution = 1024;
    [SerializeField] private float brushRadius = 2f;
    [SerializeField] private float opacity = 0.25f;
    [SerializeField] private float hardness = 0.2f;

    [SerializeField]
    private BrushMode brushMode =
        BrushMode.PaintDirt;

    private Texture2D maskTexture;
    private Color32[] pixels;
    private MeshCollider groundCollider;

    private Vector2 lastUV;
    private bool hasLastUV;
    private bool strokeActive;
    private bool hasUnsavedChanges;

    [MenuItem("Tools/Environment/Ground Painter")]
    private static void OpenWindow()
    {
        GetWindow<GroundPaintEditor>("Ground Painter");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Ground Painting Tool",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space(5);

        groundObject = (GameObject)EditorGUILayout.ObjectField(
            "Ground Mesh",
            groundObject,
            typeof(GameObject),
            true
        );

        groundMaterial = (Material)EditorGUILayout.ObjectField(
            "Ground Material",
            groundMaterial,
            typeof(Material),
            false
        );

        maskPath = EditorGUILayout.TextField(
            "Mask Asset Path",
            maskPath
        );

        EditorGUILayout.Space(5);

        resolution = EditorGUILayout.IntPopup(
            "Mask Resolution",
            resolution,
            new[] { "512", "1024", "2048" },
            new[] { 512, 1024, 2048 }
        );

        if (GUILayout.Button("Add / Refresh MeshCollider"))
        {
            AddOrRefreshCollider();
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Create New Black Mask"))
        {
            CreateNewMask();
        }

        if (GUILayout.Button("Load Existing Mask"))
        {
            LoadMask();
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Brush Settings",
            EditorStyles.boldLabel
        );

        brushMode = (BrushMode)EditorGUILayout.EnumPopup(
            "Mode",
            brushMode
        );

        brushRadius = EditorGUILayout.Slider(
            "Brush Radius (World Units)",
            brushRadius,
            0.1f,
            10f
        );

        opacity = EditorGUILayout.Slider(
            "Opacity",
            opacity,
            0.01f,
            1f
        );

        hardness = EditorGUILayout.Slider(
            "Edge Hardness",
            hardness,
            0f,
            0.95f
        );

        EditorGUILayout.Space(10);

        if (maskTexture != null)
        {
            EditorGUILayout.LabelField(
                $"Loaded Mask: {maskTexture.width} x {maskTexture.height}"
            );

            EditorGUILayout.HelpBox(
                "Hold the LEFT mouse button and paint directly " +
                "on the ground in the Scene View.",
                MessageType.Info
            );
        }

        GUI.enabled = maskTexture != null;

        if (GUILayout.Button("Save Mask PNG"))
        {
            SaveMask();
        }

        if (GUILayout.Button("Clear Mask"))
        {
            ClearMask();
        }

        GUI.enabled = true;

        if (hasUnsavedChanges)
        {
            EditorGUILayout.HelpBox(
                "You have unsaved painting changes. Save the mask.",
                MessageType.Warning
            );
        }

        if (groundMaterial != null &&
            !groundMaterial.HasProperty(MaskProperty))
        {
            EditorGUILayout.HelpBox(
                "The material must expose a Texture2D property " +
                "with reference _PaintMask.",
                MessageType.Error
            );
        }

        EditorGUILayout.HelpBox(
            "Painting is Editor-only. The saved PNG is used " +
            "by your final ground material.",
            MessageType.None
        );
    }

    private void AddOrRefreshCollider()
    {
        if (groundObject == null)
        {
            Debug.LogError("Assign your ground object first.");
            return;
        }

        MeshFilter filter = groundObject.GetComponent<MeshFilter>();

        if (filter == null || filter.sharedMesh == null)
        {
            Debug.LogError(
                "Ground object needs a valid MeshFilter."
            );
            return;
        }

        groundCollider = groundObject.GetComponent<MeshCollider>();

        if (groundCollider == null)
        {
            groundCollider =
                Undo.AddComponent<MeshCollider>(groundObject);
        }

        groundCollider.sharedMesh = null;
        groundCollider.sharedMesh = filter.sharedMesh;
        groundCollider.convex = false;

        Debug.Log("Ground MeshCollider configured.");
    }

    private bool ValidatePath()
    {
        if (string.IsNullOrWhiteSpace(maskPath) ||
            !maskPath.StartsWith("Assets/", StringComparison.Ordinal) ||
            Path.GetExtension(maskPath).ToLowerInvariant() != ".png")
        {
            Debug.LogError(
                "Mask path must be a PNG inside the Assets folder."
            );
            return false;
        }

        return true;
    }

    private string AbsoluteMaskPath()
    {
        return Path.GetFullPath(maskPath);
    }

    private void CreateNewMask()
    {
        if (!ValidatePath())
            return;

        int count = resolution * resolution;

        Texture2D newMask = new Texture2D(
            resolution,
            resolution,
            TextureFormat.RGBA32,
            true,
            true
        );

        Color32[] clearPixels = new Color32[count];

        for (int i = 0; i < count; i++)
            clearPixels[i] = new Color32(0, 0, 0, 255);

        newMask.SetPixels32(clearPixels);
        newMask.Apply(true, false);

        Directory.CreateDirectory(
            Path.GetDirectoryName(AbsoluteMaskPath())
        );

        File.WriteAllBytes(
            AbsoluteMaskPath(),
            newMask.EncodeToPNG()
        );

        DestroyImmediate(newMask);

        AssetDatabase.Refresh();

        ConfigureImporter();
        LoadMask();

        Debug.Log("New black ground mask created.");
    }

    private void ConfigureImporter()
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(maskPath) as TextureImporter;

        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.isReadable = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;

        importer.textureCompression =
            TextureImporterCompression.Uncompressed;

        importer.SaveAndReimport();
    }

    private void LoadMask()
    {
        if (!ValidatePath())
            return;

        if (!File.Exists(AbsoluteMaskPath()))
        {
            Debug.LogError("Mask PNG does not exist.");
            return;
        }

        AssetDatabase.ImportAsset(
            maskPath,
            ImportAssetOptions.ForceUpdate
        );

        ConfigureImporter();

        maskTexture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);

        if (maskTexture == null || !maskTexture.isReadable)
        {
            Debug.LogError(
                "Could not load a readable mask texture."
            );
            return;
        }

        pixels = maskTexture.GetPixels32();

        hasUnsavedChanges = false;

        AssignMaskToMaterial();

        Repaint();
        SceneView.RepaintAll();

        Debug.Log("Ground mask loaded.");
    }

    private void AssignMaskToMaterial()
    {
        if (groundMaterial == null || maskTexture == null)
            return;

        if (!groundMaterial.HasProperty(MaskProperty))
        {
            Debug.LogError(
                "Shader Graph property reference must be _PaintMask."
            );
            return;
        }

        groundMaterial.SetTexture(
            MaskProperty,
            maskTexture
        );

        EditorUtility.SetDirty(groundMaterial);
    }

    private void SaveMask()
    {
        if (maskTexture == null || pixels == null)
            return;

        maskTexture.SetPixels32(pixels);
        maskTexture.Apply(true, false);

        Directory.CreateDirectory(
            Path.GetDirectoryName(AbsoluteMaskPath())
        );

        File.WriteAllBytes(
            AbsoluteMaskPath(),
            maskTexture.EncodeToPNG()
        );

        AssetDatabase.ImportAsset(
            maskPath,
            ImportAssetOptions.ForceUpdate
        );

        ConfigureImporter();

        maskTexture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);

        pixels = maskTexture.GetPixels32();

        AssignMaskToMaterial();

        hasUnsavedChanges = false;

        SceneView.RepaintAll();

        Debug.Log("Ground paint mask saved successfully.");
    }

    private void ClearMask()
    {
        if (maskTexture == null)
            return;

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 255);

        maskTexture.SetPixels32(pixels);
        maskTexture.Apply(true, false);

        hasUnsavedChanges = true;

        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (groundObject == null || maskTexture == null ||
            pixels == null)
            return;

        if (groundCollider == null)
            groundCollider = groundObject.GetComponent<MeshCollider>();

        if (groundCollider == null)
            return;

        Event e = Event.current;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        bool hitGround = groundCollider.Raycast(
            ray,
            out RaycastHit hit,
            100000f
        );

        if (hitGround && !e.alt)
        {
            Handles.color = brushMode == BrushMode.PaintDirt
                ? new Color(1f, 0.65f, 0.15f, 0.9f)
                : new Color(1f, 0.2f, 0.2f, 0.9f);

            Handles.DrawWireDisc(
                hit.point,
                hit.normal,
                brushRadius
            );

            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(
                    GUIUtility.GetControlID(FocusType.Passive)
                );
            }
        }

        if (e.alt)
            return;

        if (e.type == EventType.MouseDown &&
            e.button == 0 && hitGround)
        {
            strokeActive = true;
            hasLastUV = false;

            PaintAt(hit.textureCoord);

            e.Use();
        }
        else if (e.type == EventType.MouseDrag &&
                 strokeActive && e.button == 0)
        {
            if (hitGround)
                PaintAt(hit.textureCoord);
            else
                hasLastUV = false;

            e.Use();
        }
        else if (e.type == EventType.MouseUp &&
                 e.button == 0 && strokeActive)
        {
            strokeActive = false;
            hasLastUV = false;

            e.Use();
        }
    }

    private void PaintAt(Vector2 currentUV)
    {
        if (!TryGetBrushPixelRadii(out float radiusX,
                                   out float radiusY))
            return;

        if (!hasLastUV)
        {
            Stamp(currentUV, radiusX, radiusY);
        }
        else
        {
            Vector2 previousPixels = new Vector2(
                lastUV.x * maskTexture.width,
                lastUV.y * maskTexture.height
            );

            Vector2 currentPixels = new Vector2(
                currentUV.x * maskTexture.width,
                currentUV.y * maskTexture.height
            );

            float distance =
                Vector2.Distance(previousPixels, currentPixels);

            float spacing = Mathf.Max(
                1f,
                Mathf.Min(radiusX, radiusY) * 0.25f
            );

            int steps = Mathf.Max(
                1,
                Mathf.CeilToInt(distance / spacing)
            );

            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;

                Stamp(
                    Vector2.Lerp(lastUV, currentUV, t),
                    radiusX,
                    radiusY
                );
            }
        }

        lastUV = currentUV;
        hasLastUV = true;

        maskTexture.SetPixels32(pixels);
        maskTexture.Apply(true, false);

        hasUnsavedChanges = true;

        Repaint();
        SceneView.RepaintAll();
    }

    private bool TryGetBrushPixelRadii(
        out float radiusX,
        out float radiusY)
    {
        radiusX = radiusY = 0f;

        MeshFilter filter = groundObject.GetComponent<MeshFilter>();

        if (filter == null || filter.sharedMesh == null)
            return false;

        Mesh mesh = filter.sharedMesh;

        Vector2[] uvs = mesh.uv;

        if (uvs == null || uvs.Length == 0)
            return false;

        float minU = float.MaxValue;
        float maxU = float.MinValue;
        float minV = float.MaxValue;
        float maxV = float.MinValue;

        foreach (Vector2 uv in uvs)
        {
            minU = Mathf.Min(minU, uv.x);
            maxU = Mathf.Max(maxU, uv.x);
            minV = Mathf.Min(minV, uv.y);
            maxV = Mathf.Max(maxV, uv.y);
        }

        float rangeU = maxU - minU;
        float rangeV = maxV - minV;

        Bounds bounds = mesh.bounds;

        Vector3 depthAxis = bounds.size.z > 0.001f
            ? Vector3.forward
            : Vector3.up;

        float worldWidth = groundObject.transform.TransformVector(
            Vector3.right * bounds.size.x
        ).magnitude;

        float depthSize = bounds.size.z > 0.001f
            ? bounds.size.z
            : bounds.size.y;

        float worldDepth = groundObject.transform.TransformVector(
            depthAxis * depthSize
        ).magnitude;

        if (worldWidth < 0.001f ||
            worldDepth < 0.001f ||
            rangeU < 0.001f ||
            rangeV < 0.001f)
            return false;

        radiusX = brushRadius / worldWidth *
                  rangeU * maskTexture.width;

        radiusY = brushRadius / worldDepth *
                  rangeV * maskTexture.height;

        return radiusX > 0f && radiusY > 0f;
    }

    private void Stamp(
        Vector2 uv,
        float radiusX,
        float radiusY)
    {
        int width = maskTexture.width;
        int height = maskTexture.height;

        int centerX = Mathf.RoundToInt(
            Mathf.Clamp01(uv.x) * (width - 1)
        );

        int centerY = Mathf.RoundToInt(
            Mathf.Clamp01(uv.y) * (height - 1)
        );

        int minX = Mathf.Max(
            0,
            Mathf.FloorToInt(centerX - radiusX)
        );

        int maxX = Mathf.Min(
            width - 1,
            Mathf.CeilToInt(centerX + radiusX)
        );

        int minY = Mathf.Max(
            0,
            Mathf.FloorToInt(centerY - radiusY)
        );

        int maxY = Mathf.Min(
            height - 1,
            Mathf.CeilToInt(centerY + radiusY)
        );

        float targetValue =
            brushMode == BrushMode.PaintDirt ? 1f : 0f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x - centerX) / radiusX;
                float dy = (y - centerY) / radiusY;

                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                if (distance > 1f)
                    continue;

                float falloff = 1f;

                if (distance > hardness)
                {
                    float t = Mathf.InverseLerp(
                        hardness,
                        1f,
                        distance
                    );

                    t = t * t * (3f - 2f * t);

                    falloff = 1f - t;
                }

                float amount = opacity * falloff;

                int index = y * width + x;

                float current =
                    pixels[index].r / 255f;

                float result = Mathf.Lerp(
                    current,
                    targetValue,
                    amount
                );

                byte value = (byte)Mathf.RoundToInt(
                    result * 255f
                );

                pixels[index] = new Color32(
                    value, value, value, 255
                );
            }
        }
    }
}
