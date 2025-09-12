#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using TMPro;

public class BookPreviewGenerator : EditorWindow
{
    private const string OUTPUT_FOLDER = "Assets/BookPreviews";
    private static readonly Color BACKGROUND = new Color(0, 0, 0, 0);

    private int resolution = 512;
    private bool orthographic = true;
    private float padding = 1.15f;

    [MenuItem("Tools/Books/Generate Book Thumbnails")]
    public static void ShowWindow()
    {
        GetWindow<BookPreviewGenerator>("Book Thumbnails");
    }

    private void OnGUI()
    {
        GUILayout.Label("Thumbnail Settings", EditorStyles.boldLabel);
        resolution = EditorGUILayout.IntSlider("Resolution", resolution, 128, 2048);
        orthographic = EditorGUILayout.Toggle("Orthographic Camera", orthographic);
        padding = EditorGUILayout.Slider("Framing Padding", padding, 1f, 1.6f);

        if (GUILayout.Button("Generate Thumbnails For All BookDefinitions"))
            GenerateAll();

        if (GUILayout.Button("Generate All (Perspective View)"))
            GenerateAllPerspective();

    }

    private void GenerateAll()
    {
        if (!Directory.Exists(OUTPUT_FOLDER))
            Directory.CreateDirectory(OUTPUT_FOLDER);

        string[] guids = AssetDatabase.FindAssets("t:BookDefinition");
        int done = 0;

        foreach (string guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<BookDefinition>(path);
            if (def == null || def.prefab == null)
            {
                Debug.LogWarning($"[ThumbGen] Skipping: {path} (missing definition or prefab).");
                continue;
            }

            string fileSafeId = string.IsNullOrEmpty(def.bookID) ? def.title : def.bookID;
            if (string.IsNullOrEmpty(fileSafeId)) fileSafeId = def.name;
            foreach (char c in Path.GetInvalidFileNameChars())
                fileSafeId = fileSafeId.Replace(c, '-');

            string pngPath = $"{OUTPUT_FOLDER}/{fileSafeId}.png";

            Texture2D tex = RenderPrefab(def, BACKGROUND, resolution, orthographic, padding);
            if (tex == null)
            {
                Debug.LogWarning($"[ThumbGen] Failed render for {def.name}");
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
            def.thumbnail = sprite;
            EditorUtility.SetDirty(def);

            done++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ThumbGen] Done. Generated {done} thumbnails into {OUTPUT_FOLDER}");
    }

    /// <summary>
    /// Instantiates the prefab, applies the BookDefinition (visuals + text),
    /// frames and renders it to a Texture2D.
    /// </summary>
    // inside BookPreviewGenerator

    // ...

    public static Texture2D RenderPrefab(
     BookDefinition def,
     Color bg,
     int size,
     bool useOrtho,
     float framePad,
     Vector3? customCamOffset = null,
     Vector3? customCamEuler = null)
    {
        var root = new GameObject("~ThumbRoot") { hideFlags = HideFlags.HideAndDontSave };

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(def.prefab);
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

        // 🔹 now static
        OrientUsingAnchors(instance.transform);

        // Apply BookDefinition to instance if possible
        var bookInfo = instance.GetComponentInChildren<BookInfo>();
        if (bookInfo != null)
        {
            bookInfo.ApplyDefinition(def);
        }
        else
        {
            // Fallback: directly tint renderers & set TMP text
            var fallbackRends = instance.GetComponentsInChildren<Renderer>(true);
            if (fallbackRends.Length > 0)
            {
                var mpb = new MaterialPropertyBlock();
                foreach (var r in fallbackRends)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_Color", def.color);
                    mpb.SetColor("_BaseColor", def.color);
                    r.SetPropertyBlock(mpb);
                }
            }

            foreach (var t in instance.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                t.text = string.IsNullOrEmpty(def.title) ? "Untitled" : def.title;
            }
        }


        // LIGHT
        var lightGO = new GameObject("~ThumbLight") { hideFlags = HideFlags.HideAndDontSave, layer = THUMB_LAYER };
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50f, 30f, 0f);

        // BOUNDS
        var rends = instance.GetComponentsInChildren<Renderer>(true);
        Bounds b = new Bounds(instance.transform.position, Vector3.one * 0.1f);
        foreach (var r in rends) if (r != null) b.Encapsulate(r.bounds);

        // CAMERA
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
        if (!useOrtho) cam.fieldOfView = 35f;

        Vector3 center = b.center;
        float dist = b.extents.magnitude * 4f;
        cam.transform.position = center - Vector3.forward * dist;
        cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        if (customCamOffset.HasValue)
        {
            if (useOrtho)
            {
                float scale = Mathf.Max(b.extents.magnitude, 1f);
                cam.transform.position = center + customCamOffset.Value * scale;
            }
            else
            {
                float halfSize = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
                float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
                float requiredDist = halfSize / Mathf.Tan(fovRad / 2f);
                Vector3 dir = customCamOffset.Value.normalized;
                cam.transform.position = center + dir * requiredDist * 1.2f;
            }
        }
        if (customCamEuler.HasValue)
            cam.transform.rotation = Quaternion.Euler(customCamEuler.Value);

        // 🔹 use framePad instead of padding
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

    private static void OrientUsingAnchors(Transform inst)
    {
        var coverA = inst.GetComponentsInChildren<Transform>(true)
                         .FirstOrDefault(t => t.name == "CoverAnchor");
        var spineA = inst.GetComponentsInChildren<Transform>(true)
                         .FirstOrDefault(t => t.name == "SpineAnchor");

        Vector3 localCover = coverA ? coverA.up : inst.up;
        Vector3 localTop = inst.forward;
        Vector3 localSpine = spineA ? spineA.forward : -inst.right;

        Quaternion q = Quaternion.FromToRotation(localCover, -Vector3.forward);
        inst.rotation = q * inst.rotation;

        Vector3 topWorld = Vector3.ProjectOnPlane(inst.TransformDirection(localTop), -Vector3.forward);
        float rollToUp = Vector3.SignedAngle(topWorld, Vector3.up, -Vector3.forward);
        inst.Rotate(-Vector3.forward, rollToUp, Space.World);

        Vector3 spineWorld = Vector3.ProjectOnPlane(inst.TransformDirection(localSpine), -Vector3.forward);
        if (Vector3.Dot(spineWorld.normalized, -Vector3.right) < 0f)
            inst.Rotate(-Vector3.forward, 180f, Space.World);
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
        foreach (Transform c in go.transform) SetLayerRecursively(c.gameObject, layer);
    }

    public static Sprite GenerateForDefinition(BookDefinition def, int resolution = 512, bool orthographic = true, float padding = 1.15f, Vector3? customCamOffset = null, Vector3? customCamEuler = null)
    {
        const string OUTPUT_FOLDER = "Assets/BookPreviews";
        if (!Directory.Exists(OUTPUT_FOLDER)) Directory.CreateDirectory(OUTPUT_FOLDER);

        string fileSafeId = string.IsNullOrEmpty(def.bookID) ? def.title : def.bookID;
        if (string.IsNullOrEmpty(fileSafeId)) fileSafeId = def.name;
        foreach (char c in Path.GetInvalidFileNameChars()) fileSafeId = fileSafeId.Replace(c, '-');

        string pngPath = $"{OUTPUT_FOLDER}/{fileSafeId}.png";

        // Render
        Texture2D tex = BookPreviewGenerator.RenderPrefab(
    def, new Color(0, 0, 0, 0),
    resolution, orthographic, padding,
    customCamOffset, customCamEuler);
        if (tex == null)
        {
            Debug.LogWarning($"[ThumbGen] Failed render for {def.name}");
            return null;
        }

        // Save & import as Sprite
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
        def.thumbnail = sprite;
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();

        return sprite;
    }

    public static void GenerateAllPerspective()
    {
        if (!Directory.Exists(OUTPUT_FOLDER))
            Directory.CreateDirectory(OUTPUT_FOLDER);

        string[] guids = AssetDatabase.FindAssets("t:BookDefinition");
        int done = 0;

        foreach (string guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<BookDefinition>(path);
            if (def == null || def.prefab == null)
            {
                Debug.LogWarning($"[ThumbGen] Skipping: {path} (missing definition or prefab).");
                continue;
            }

            string fileSafeId = string.IsNullOrEmpty(def.bookID) ? def.title : def.bookID;
            if (string.IsNullOrEmpty(fileSafeId)) fileSafeId = def.name;
            foreach (char c in Path.GetInvalidFileNameChars())
                fileSafeId = fileSafeId.Replace(c, '-');

            string pngPath = $"{OUTPUT_FOLDER}/{fileSafeId}.png";

            // Force perspective camera
            Texture2D tex = RenderPrefab(
                def,
                BACKGROUND,
                512,             // resolution
                false,           // orthographic = false
                1.15f,           // padding
                new Vector3(3, 2.5f, -5),   // offset
                new Vector3(25, -30, 0)     // euler
            );

            if (tex == null)
            {
                Debug.LogWarning($"[ThumbGen] Failed render for {def.name}");
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
            def.thumbnail = sprite;
            EditorUtility.SetDirty(def);

            done++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ThumbGen] Done. Generated {done} perspective thumbnails into {OUTPUT_FOLDER}");
    }

}
#endif
