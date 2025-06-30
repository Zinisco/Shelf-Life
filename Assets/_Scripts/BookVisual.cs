using TMPro;
using UnityEngine;

public class BookVisual : MonoBehaviour
{
    [SerializeField] TMP_Text coverTitleText;
    [SerializeField] TMP_Text spineTitleText;
    [SerializeField] MeshRenderer coverRenderer;

    /// call this right after Instantiate(def.prefab)
    public void ApplyDefinition(BookDefinition def)
    {
        coverTitleText.text = def.title;
        spineTitleText.text = def.title;
        coverRenderer.material.color = def.color;
    }
}
