using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[ExecuteAlways]
public class Runtime3DText : MonoBehaviour
{
    public enum LayoutDirection { Auto, LeftToRight, RightToLeft }
    [SerializeField] private LayoutDirection layoutDirection = LayoutDirection.RightToLeft;

    [Header("Font Metrics (for spacing only)")]
    [SerializeField] private TMP_FontAsset spacingFont;   // Used to read advances/kerning
    [SerializeField] private float baseFontSize = 1f;     // Scales metric advances to world units

    // Size control independent of auto-fit
    [SerializeField, Tooltip("Uniform scale applied to each letter before auto-fit.")]
    private float letterScale = 1f;

    // Extra spacing when using prefab-bounds spacing (unscaled; multiplied by 'scale' at layout)
    [SerializeField, Tooltip("Extra gap between letters when using prefab widths.")]
    private float boundsPadding = 0.02f;

    [SerializeField, Range(0.7f, 1.3f)]
    private float prefabWidthMul = 1.00f;  // <— 0.90 ~ tighter, 1.05 ~ looser

    // If true, force text to render Left->Right even if parent has negative X scale
    [SerializeField, Tooltip("Keep Left-to-Right even if parent has negative X scale.")]
    private bool forceLTR = true;

    // If true, nudge each spawned letter so its left (or right) renderer edge sits exactly on X
    [SerializeField, Tooltip("Align by renderer edge to neutralize inconsistent pivots.")]
    private bool alignByRendererEdge = true;
    
    [SerializeField, Tooltip("If OFF, spacing/kerning ignore letterScale (good for tuning).")]
    private bool scaleSpacingWithLetterScale = true;


    [Header("Character Prefabs")]
    [SerializeField] private List<LetterPrefab> characters = new(); // Map char -> 3D prefab

    [Header("Layout")]
    [SerializeField] private float letterSpacingMul = 1.0f; // 1 = use font advance; >1 = looser spacing
    [SerializeField] private float lineHeight = 1.2f;       // world units between lines (roughly font-size * 1.2)
    [SerializeField] private Alignment alignment = Alignment.Center;

    [Header("Fitting")]
    [SerializeField] private bool autoScaleToFit = true;
    [SerializeField] private float maxWidth = 4f;           // total width allowed (world units)

    [Header("Depth / Thickness")]
    [SerializeField] private float depthScale = 1.0f;       // scales Z on each letter to make it �thicker�

    [Header("Options")]
    [SerializeField] private bool combineMeshes = false;    // optional: combine into one mesh after build
    [SerializeField] private bool staticAfterBuild = true;  // mark letters static for lightmapping, etc.

    // Fine-tuning dials
    [SerializeField, Tooltip("Multiplies font-derived advances to match your prefab scale.")]
    private float metricMul = 1f;

    [SerializeField, Tooltip("Extra tracking (world units) per character, can be negative.")]
    private float tracking = 0f;

    [SerializeField, Tooltip("Multiplies kerning amount.")]
    private float kerningMul = 1f;

    // Optional: use prefab bounds instead of font metrics for spacing
    [SerializeField, Tooltip("If ON, use each prefab's renderer width for spacing (ignores font metrics).")]
    private bool usePrefabBoundsSpacing = false;

    // Cache of prefab widths (computed once)
    private Dictionary<char, float> _prefabWidthCache;


    [Serializable]
    public struct LetterPrefab
    {
        public string character;
        public GameObject prefab;

        [Tooltip("Override spacing width for this letter (world units, at scale=1). Leave 0 for auto.")]
        public float spacingOverride;
    }


    public enum Alignment { Left, Center, Right }

    private Dictionary<uint, TMP_Character> _charTable;           // unicode -> TMP_Character
    private Dictionary<char, GameObject> _prefabTable;            // char -> prefab
    private Dictionary<(uint, uint), float> _kerningPairCache;     // pair -> kerning offset (world units)
    private Dictionary<uint, uint> _codepointToGlyphIndex; // unicode -> glyphIndex


    private string _currentText = "";
    private bool _dirty = false;

    // Public API
    public void SetText(string text)
    {
        ClearText();
        _currentText = text ?? "";
        _dirty = true;
        Rebuild();
    }

    private void ClearText()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }


    // Auto rebuild in Editor when properties change
    private void OnValidate()
    {
        _dirty = true;
    }

    private void Update()
    {
        if (_dirty) Rebuild();
    }

    private void EnsureTables()
    {
        // Characters
        _charTable ??= new Dictionary<uint, TMP_Character>();
        _charTable.Clear();
        _codepointToGlyphIndex ??= new Dictionary<uint, uint>();
        _codepointToGlyphIndex.Clear();

        if (spacingFont != null)
        {
            foreach (var kv in spacingFont.characterLookupTable)
            {
                _charTable[kv.Key] = kv.Value;
                _codepointToGlyphIndex[kv.Key] = kv.Value.glyphIndex;
            }
        }

        // Prefabs
        _prefabTable ??= new Dictionary<char, GameObject>();
        _prefabTable.Clear();
        foreach (var lp in characters)
        {
            if (!string.IsNullOrEmpty(lp.character))
            {
                char c = char.ToUpperInvariant(lp.character[0]); // Normalize key
                if (!_prefabTable.ContainsKey(c))                // Avoid duplicates
                    _prefabTable[c] = lp.prefab;
            }
        }


        // Kerning (glyph pair adjustment)
        _kerningPairCache = new Dictionary<(uint, uint), float>();
        if (spacingFont != null && spacingFont.fontFeatureTable != null)
        {
            var records = spacingFont.fontFeatureTable.glyphPairAdjustmentRecords;
            if (records != null)
            {
                float metricScale =
     (baseFontSize / spacingFont.faceInfo.pointSize) *
     spacingFont.faceInfo.scale *
     metricMul * kerningMul;

                foreach (var rec in records)
                {
                    // rec is UnityEngine.TextCore.GlyphPairAdjustmentRecord
                    uint first = rec.firstAdjustmentRecord.glyphIndex;
                    uint second = rec.secondAdjustmentRecord.glyphIndex;

                    // Use GlyphValueRecord.xAdvance (not xAdvanceAdjustment)
                    float xAdj = 0f;
                    xAdj += rec.firstAdjustmentRecord.glyphValueRecord.xAdvance * metricScale;
                    xAdj += rec.secondAdjustmentRecord.glyphValueRecord.xAdvance * metricScale;

                    var key = (first, second);
                    if (!_kerningPairCache.ContainsKey(key))
                        _kerningPairCache[key] = xAdj;
                }
            }
        }
    }


    private void ClearChildren()
    {
        var toDestroy = new List<Transform>();
        foreach (Transform t in transform) toDestroy.Add(t);
        foreach (var t in toDestroy)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(t.gameObject);
            else Destroy(t.gameObject);
#else
            Destroy(t.gameObject);
#endif
        }
    }

    private float GetAdvance(char c)
    {
        if (usePrefabBoundsSpacing)
        {
            float width = GetPrefabWidth(c);
            return (width * prefabWidthMul * letterSpacingMul) + boundsPadding + tracking;
        }


        if (spacingFont == null) return baseFontSize + tracking;

        uint code = c;
        if (!_charTable.TryGetValue(code, out var ch))
            return baseFontSize * 0.6f + tracking;

        float metricScale =
            (baseFontSize / spacingFont.faceInfo.pointSize) *
            spacingFont.faceInfo.scale *
            metricMul;

        var gm = ch.glyph.metrics;
        return gm.horizontalAdvance * metricScale * letterSpacingMul + tracking;
    }

    private float GetKerning(char prev, char cur)
    {
        prev = char.ToUpperInvariant(prev);
        cur = char.ToUpperInvariant(cur);

        // Kerning only makes sense when using font metrics.
        if (usePrefabBoundsSpacing || spacingFont == null || kerningMul <= 0f)
            return 0f;

        uint aCode = prev, bCode = cur;
        if (!_codepointToGlyphIndex.TryGetValue(aCode, out var aGlyph)) return 0f;
        if (!_codepointToGlyphIndex.TryGetValue(bCode, out var bGlyph)) return 0f;

        return _kerningPairCache.TryGetValue((aGlyph, bGlyph), out var v) ? v : 0f;
    }



    private GameObject GetPrefab(char c)
    {
        c = char.ToUpperInvariant(c);
        if (_prefabTable.TryGetValue(c, out var pf)) return pf;
        // Optional: treat space as null prefab
        if (c == ' ') return null;
        return null; // unknown char � you may want a fallback cube or �?� prefab
    }

    private (List<Vector3> lineWidths, float maxLineWidth) Measure(string[] lines)
    {
        float maxWidthFound = 0f;
        var widths = new List<Vector3>(lines.Length);
        for (int li = 0; li < lines.Length; li++)
        {
            string line = lines[li];
            float x = 0f;
            char prev = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (prev != '\0') x += GetKerning(prev, c);
                x += GetAdvance(c);
                prev = c;
            }
            maxWidthFound = Mathf.Max(maxWidthFound, x);
            widths.Add(new Vector3(x, 0, 0));
        }
        return (widths, maxWidthFound);
    }

    private void Rebuild()
    {
        _dirty = false;
        EnsureTables();
        ClearChildren();

        string[] lines = _currentText.Replace("\r", "").Split('\n');

        // Measure
        var (lineWidths, maxMeasuredWidth) = Measure(lines);

        // Fit scale (auto-fit) and spacing scale (includes letterScale so spacing matches visual size)
        float scale = 1f;
        if (autoScaleToFit && maxWidth > 0f && maxMeasuredWidth > 0f)
        {
            // Include letterScale in the fit so the final visual width <= maxWidth
            float visualWidth = maxMeasuredWidth * letterScale;
            if (visualWidth > maxWidth)
                scale = maxWidth / visualWidth;
        }

        scale = Mathf.Clamp(scale, 0.001f, 100f);

        float spacingScale = scaleSpacingWithLetterScale ? (scale * letterScale) : scale;
        spacingScale = Mathf.Clamp(spacingScale, 0.001f, 100f);


        float dir = 1f;
        switch (layoutDirection)
        {
            case LayoutDirection.LeftToRight: dir = 1f; break;
            case LayoutDirection.RightToLeft: dir = -1f; break;
            case LayoutDirection.Auto:
                dir = IsMirroredWorld() && !forceLTR ? -1f : 1f;
                break;
        }

        // Alignment start depends on direction
        float y = 0f;
        for (int li = 0; li < lines.Length; li++)
        {
            string line = lines[li];
            float lineWidth = lineWidths[li].x * spacingScale;

            float x = alignment switch
            {
                Alignment.Left => (dir > 0f) ? 0f : lineWidth,
                Alignment.Center => (dir > 0f) ? -lineWidth * 0.5f : lineWidth * 0.5f,
                Alignment.Right => (dir > 0f) ? -lineWidth : 0f,
                _ => 0f
            };

            char prev = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                // kerning (scaled & directional)
                if (prev != '\0') x += GetKerning(prev, c) * spacingScale * dir;

                // spawn
                var pf = GetPrefab(c);
                if (pf != null)
                {
                    var go = Instantiate(pf, transform);
                    go.name = $"char_{c}_{i}";
                    go.transform.localPosition = new Vector3(x, y, 0);
                    go.transform.localRotation = Quaternion.identity;

                    // Size control (letterScale) + auto-fit scale
                    go.transform.localScale = new Vector3(
      letterScale * Mathf.Abs(scale),
      letterScale * Mathf.Abs(scale),
      letterScale * Mathf.Abs(scale) * depthScale
  );
                    /*
                    if (alignByRendererEdge && usePrefabBoundsSpacing)
                    {
                        var mr = go.GetComponentInChildren<Renderer>();
                        if (mr != null)
                        {
                            float edge = (dir > 0f) ? mr.bounds.min.x : mr.bounds.max.x;
                            float offset = edge - go.transform.position.x;
                            go.transform.localPosition -= new Vector3(offset, 0f, 0f);
                        }
                    }
                    */

#if UNITY_EDITOR
                    if (staticAfterBuild)
                        UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, UnityEditor.StaticEditorFlags.BatchingStatic | UnityEditor.StaticEditorFlags.ContributeGI);
#endif
                }

                // advance (scaled & directional)
                x += GetAdvance(c) * spacingScale * dir;
                prev = c;
            }

            y -= lineHeight * spacingScale;
        }

        if (combineMeshes) TryCombineMeshes();
    }

    private float GetPrefabWidth(char c)
    {
        c = char.ToUpperInvariant(c);

        if (_prefabWidthCache == null) _prefabWidthCache = new Dictionary<char, float>();
        if (_prefabWidthCache.TryGetValue(c, out var w)) return w;

        if (!_prefabTable.TryGetValue(c, out var pf) || pf == null)
        {
            // Reasonable fallback; space ~ half an average glyph
            _prefabWidthCache[c] = (c == ' ') ? 0.5f : 0.6f;
            return _prefabWidthCache[c];
        }

        var overrideWidth = characters.Find(cp => cp.character == c.ToString()).spacingOverride;
        if (overrideWidth > 0f)
        {
            _prefabWidthCache[c] = overrideWidth;
            return overrideWidth;
        }

        GameObject temp;
#if UNITY_EDITOR
        temp = (GameObject)PrefabUtility.InstantiatePrefab(pf);
#else
    temp = Instantiate(pf);
#endif
        temp.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;

        // IMPORTANT: do NOT parent to the sign; measure at world root so scale = 1
        temp.transform.SetParent(null, false);
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;
        temp.transform.localScale = Vector3.one;

        Bounds b = default;
        bool has = false;
        var rs = temp.GetComponentsInChildren<Renderer>();
        foreach (var r in rs)
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }

#if UNITY_EDITOR
        DestroyImmediate(temp);
#else
    Destroy(temp);
#endif

        float width = has ? Mathf.Max(0.001f, b.size.x) : 0.6f;
        _prefabWidthCache[c] = width;
        return width;
    }


    private bool IsMirroredWorld()
    {
        // Negative determinant = mirrored (any combo of rotations/scales that mirror X)
        var m = transform.localToWorldMatrix;
        var right = new Vector3(m.m00, m.m10, m.m20);
        var up = new Vector3(m.m01, m.m11, m.m21);
        var fwd = new Vector3(m.m02, m.m12, m.m22);
        return Vector3.Dot(Vector3.Cross(right, up), fwd) < 0f;
    }

    private void TryCombineMeshes()
    {
        // Combine child meshes into one for performance (optional).
        // Note: Loses per-letter materials unless they are the same.
        var filters = GetComponentsInChildren<MeshFilter>();
        var renderers = GetComponentsInChildren<MeshRenderer>();

        if (filters.Length == 0) return;
        var combine = new List<CombineInstance>();
        Material mat = null;

        foreach (var mr in renderers)
        {
            if (mat == null) mat = mr.sharedMaterial;
            // If you have multiple materials/colors per letter, skip combining or split by material.
        }

        foreach (var mf in filters)
        {
            if (mf.transform == transform) continue;
            if (mf.sharedMesh == null) continue;
            CombineInstance ci = new CombineInstance
            {
                mesh = mf.sharedMesh,
                transform = mf.transform.localToWorldMatrix
            };
            combine.Add(ci);
        }

        // Create parent mesh
        var parentFilter = GetComponent<MeshFilter>();
        var parentRenderer = GetComponent<MeshRenderer>();
        if (parentFilter == null) parentFilter = gameObject.AddComponent<MeshFilter>();
        if (parentRenderer == null) parentRenderer = gameObject.AddComponent<MeshRenderer>();

        Mesh combined = new Mesh();
        combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // support large meshes
        combined.CombineMeshes(combine.ToArray());
        parentFilter.sharedMesh = combined;
        parentRenderer.sharedMaterial = mat;

        // Remove children now that we have one mesh
        ClearChildren();
    }


#if UNITY_EDITOR
    [ContextMenu("Calibrate/Fill MaxWidth (BOOKSTORE)")]
    private void Calibrate_FillMaxWidth()
    {
        const string sample = "BOOKSTORE";

        // Make sure we have prefabs and a font table set up
        EnsureTables();

        // Measure sample width using your current spacing mode (metrics or prefab-bounds)
        var (widths, maxW) = Measure(new[] { sample });
        float geomWidth = maxW; // world units at letterScale = 1 (we apply scale ourselves)

        if (geomWidth <= 0f)
        {
            Debug.LogWarning("[Runtime3DText] Calibration failed: measured width <= 0");
            return;
        }

        // If maxWidth is unset/tiny, make it at least the measured width so the result is sane
        if (maxWidth <= 0f) maxWidth = geomWidth;

        // Compute letterScale so BOOKSTORE fills maxWidth WITHOUT auto-fit
        letterScale = maxWidth / geomWidth;
        autoScaleToFit = false;

        // Force LTR so order never flips during calibration
        layoutDirection = LayoutDirection.RightToLeft;

        _dirty = true;
        Rebuild();
        EditorUtility.SetDirty(this);
    }

    [ContextMenu("Calibrate/Set MaxWidth From Geometry (BOOKSTORE)")]
    private void Calibrate_SetMaxWidthFromGeometry()
    {
        const string sample = "BOOKSTORE";

        EnsureTables();

        var (widths, maxW) = Measure(new[] { sample });
        if (maxW <= 0f)
        {
            Debug.LogWarning("[Runtime3DText] Calibration failed: measured width <= 0");
            return;
        }

        // Set maxWidth to the current visual width of BOOKSTORE (includes your letterScale)
        maxWidth = maxW * Mathf.Max(0.0001f, letterScale);

        // Keep names scaling nicely to this width
        autoScaleToFit = true;

        // Ensure LTR
        layoutDirection = LayoutDirection.LeftToRight;

        _dirty = true;
        Rebuild();
        EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// Optional: compute a metricMul so font-metrics spacing matches your prefab geometry width.
    /// Run this if you want to use font metrics (usePrefabBoundsSpacing = false) later.
    /// </summary>
    [ContextMenu("Calibrate/Compute metricMul From Prefabs (BOOKSTORE)")]
    private void Calibrate_ComputeMetricMul()
    {
        const string sample = "BOOKSTORE";

        EnsureTables();

        // Save current mode
        bool prevUseBounds = usePrefabBoundsSpacing;
        float prevMetricMul = metricMul;

        // 1) Measure geometry width (prefab-bounds mode)
        usePrefabBoundsSpacing = true;
        var (geomWidths, geomMax) = Measure(new[] { sample });

        // 2) Measure font-metrics width with metricMul = 1
        usePrefabBoundsSpacing = false;
        metricMul = 1f;
        var (metricWidths, metricMax) = Measure(new[] { sample });

        // Restore mode
        usePrefabBoundsSpacing = prevUseBounds;

        if (metricMax <= 0f)
        {
            Debug.LogWarning("[Runtime3DText] metricMul calibration failed: metric width <= 0");
            metricMul = prevMetricMul;
        }
        else
        {
            metricMul = geomMax / metricMax;
            Debug.Log($"[Runtime3DText] metricMul set to {metricMul:0.###} to match prefab geometry.");
        }

        _dirty = true;
        Rebuild();
        EditorUtility.SetDirty(this);
    }
#endif

}
