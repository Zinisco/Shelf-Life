#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

public class DesignItemPreviewGenerator : EditorWindow
{
    private const string OUTPUT_FOLDER = "Assets/DesignPreviews";
    private static readonly Color BACKGROUND = new Color(0, 0, 0, 0);

    private int resolution = 512;
    private bool orthographic = true;
    private float padding = 1.15f;

    [MenuItem("Tools/Design/Generate Design Item Thumbnails")]
    public static void ShowWindow()
    {
        GetWindow<DesignItemPreviewGenerator>("Design Thumbnails");
    }

    private void OnGUI()
    {
        GUILayout.Label("Thumbnail Settings", EditorStyles.boldLabel);
        resolution = EditorGUILayout.IntSlider("Resolution", resolution, 128, 2048);
        orthographic = EditorGUILayout.Toggle("Orthographic Camera", orthographic);
        padding = EditorGUILayout.Slider("Framing Padding", padding, 1f, 1.6f);

        if (GUILayout.Button("Generate Thumbnails For All DesignItems"))
            GenerateAll();
    }

    private void GenerateAll()
    {
        if (!Directory.Exists(OUTPUT_FOLDER))
            Directory.CreateDirectory(OUTPUT_FOLDER);

        string[] guids = AssetDatabase.FindAssets("t:DesignItem");
        int done = 0;

        foreach (string guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<DesignItem>(path);
            if (def == null || def.itemPrefab == null)
            {
                Debug.LogWarning($"[DesignThumbGen] Skipping: {path} (missing prefab).");
                continue;
            }

            string fileSafeId = string.IsNullOrEmpty(def.itemName) ? def.name : def.itemName;
            foreach (char c in Path.GetInvalidFileNameChars())
                fileSafeId = fileSafeId.Replace(c, '-');

            string pngPath = $"{OUTPUT_FOLDER}/{fileSafeId}.png";

            Texture2D tex = RenderPrefab(def, BACKGROUND, resolution, orthographic, padding);
            if (tex == null)
            {
                Debug.LogWarning($"[DesignThumbGen] Failed render for {def.name}");
                continue;
            }

            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            def.itemImage = sprite;
            EditorUtility.SetDirty(def);

            done++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DesignThumbGen] Done. Generated {done} thumbnails into {OUTPUT_FOLDER}");
    }

    private static Texture2D RenderPrefab(DesignItem def, Color bg, int size, bool useOrtho, float framePad,
    Vector3? customCamOffset = null, Vector3? customCamEuler = null)
    {
        var root = new GameObject("~ThumbRoot") { hideFlags = HideFlags.HideAndDontSave };
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(def.itemPrefab);
        if (instance == null)
        {
            Object.DestroyImmediate(root);
            return null;
        }
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.transform.SetParent(root.transform, false);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;

        const int THUMB_LAYER = 31;
        SetLayerRecursively(instance, THUMB_LAYER);
        foreach (var an in instance.GetComponentsInChildren<Animator>(true))
            an.enabled = false;

        var lightGO = new GameObject("~ThumbLight") { hideFlags = HideFlags.HideAndDontSave, layer = THUMB_LAYER };
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50f, 30f, 0f);

        var rends = instance.GetComponentsInChildren<Renderer>(true);
        Bounds b = new Bounds(instance.transform.position, Vector3.one * 0.1f);
        foreach (var r in rends) if (r != null) b.Encapsulate(r.bounds);

        var camGO = new GameObject("~ThumbCam") { hideFlags = HideFlags.HideAndDontSave, layer = THUMB_LAYER };
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = bg;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1000f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.cullingMask = 1 << THUMB_LAYER;
        cam.orthographic = useOrtho;
        if (!useOrtho)
        {
            // Perspective settings
            cam.fieldOfView = 35f; // tweak for how dramatic you want perspective
        }


        // Default placement
        Vector3 center = b.center;
        float dist = b.extents.magnitude * 4f;
        cam.transform.position = center - Vector3.forward * dist;
        cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        // Apply overrides if provided
        if (customCamOffset.HasValue)
        {
            // Scale offset by object size so perspective shots don’t clip
            float scale = Mathf.Max(b.extents.magnitude, 1f);
            cam.transform.position = center + customCamOffset.Value * scale;
        }
        // Apply overrides if provided
        // Apply overrides if provided
        if (customCamOffset.HasValue)
        {
            if (useOrtho)
            {
                // Orthographic: scale offset by object size
                float scale = Mathf.Max(b.extents.magnitude, 1f);
                cam.transform.position = center + customCamOffset.Value * scale;
            }
            else
            {
                // Perspective: auto distance based on FOV
                float halfSize = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
                float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
                float requiredDist = halfSize / Mathf.Tan(fovRad / 2f);

                // Use normalized direction of custom offset, place camera at required distance
                Vector3 dir = customCamOffset.Value.normalized;
                cam.transform.position = center + dir * requiredDist * 1.2f;
            }
        }

        // Apply custom rotation if provided
        if (customCamEuler.HasValue)
        {
            cam.transform.rotation = Quaternion.Euler(customCamEuler.Value);
        }



        GetCameraSpaceSize(cam, b, out float visW, out float visH);
        cam.orthographicSize = 0.5f * Mathf.Max(visH, visW) * framePad;

        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 8;
        var prevRT = RenderTexture.active;

        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply(false, false);

        cam.targetTexture = null;
        RenderTexture.active = prevRT;

        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(lightGO);
        Object.DestroyImmediate(root);

        return tex;
    }

    private static void GetCameraSpaceSize(Camera cam, Bounds b, out float width, out float height)
    {
        var corners = new Vector3[8];
        var c = b.center; var e = b.extents;
        int i = 0;
        for (int xi = -1; xi <= 1; xi += 2)
            for (int yi = -1; yi <= 1; yi += 2)
                for (int zi = -1; zi <= 1; zi += 2)
                    corners[i++] = c + Vector3.Scale(e, new Vector3(xi, yi, zi));

        Matrix4x4 M = cam.worldToCameraMatrix;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;

        for (int k = 0; k < 8; k++)
        {
            Vector3 v = M.MultiplyPoint3x4(corners[k]);
            minX = Mathf.Min(minX, v.x);
            maxX = Mathf.Max(maxX, v.x);
            minY = Mathf.Min(minY, v.y);
            maxY = Mathf.Max(maxY, v.y);
        }

        width = maxX - minX;
        height = maxY - minY;
    }


    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform)
            SetLayerRecursively(c.gameObject, layer);
    }

    public static Sprite GenerateForDefinition(DesignItem def, int resolution = 512, bool orthographic = true, float padding = 1.15f, Vector3? customCamOffset = null, Vector3? customCamEuler = null)
    {
        if (def == null || def.itemPrefab == null)
        {
            Debug.LogWarning("[DesignThumbGen] Missing DesignItem or prefab.");
            return null;
        }

        if (!Directory.Exists(OUTPUT_FOLDER))
            Directory.CreateDirectory(OUTPUT_FOLDER);

        string fileSafeId = string.IsNullOrEmpty(def.itemName) ? def.name : def.itemName;
        foreach (char c in Path.GetInvalidFileNameChars())
            fileSafeId = fileSafeId.Replace(c, '-');

        string pngPath = $"{OUTPUT_FOLDER}/{fileSafeId}.png";

        // Render with optional overrides
        Texture2D tex = RenderPrefab(def, BACKGROUND, resolution, orthographic, padding, customCamOffset, customCamEuler);

        if (tex == null)
        {
            Debug.LogWarning($"[DesignThumbGen] Failed render for {def.name}");
            return null;
        }

        File.WriteAllBytes(pngPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        def.itemImage = sprite;
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();

        return sprite;
    }

}
#endif
