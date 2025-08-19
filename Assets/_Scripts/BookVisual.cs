using TMPro;
using UnityEngine;

public class BookVisual : MonoBehaviour
{
    [SerializeField] TMP_Text coverTitleText;
    [SerializeField] TMP_Text spineTitleText;
    [SerializeField] MeshRenderer coverRenderer;

    static readonly int ColorProp = Shader.PropertyToID("_Color");
    static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock _mpb;

    /// Call this right after Instantiate(def.prefab)
    public void ApplyDefinition(BookDefinition def)
    {
        if (coverTitleText) coverTitleText.text = string.IsNullOrEmpty(def.title) ? "Untitled" : def.title;
        if (spineTitleText) spineTitleText.text = string.IsNullOrEmpty(def.title) ? "Untitled" : def.title;

        if (coverRenderer)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            coverRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorProp, def.color);      // Built-in / many shaders
            _mpb.SetColor(BaseColorProp, def.color);  // URP/HDRP Lit
            coverRenderer.SetPropertyBlock(_mpb);
        }
    }
}
