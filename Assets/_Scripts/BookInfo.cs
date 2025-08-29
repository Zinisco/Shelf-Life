using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BookInfo : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text spineText;

    [SerializeField] private Material secondaryMaterial; // Set in prefab

    [Header("Definition")]
    public BookDefinition definition;

    [Header("Book Metadata")]
    public System.Collections.Generic.List<string> tags = new();

    [HideInInspector] public Vector3 Position;
    [HideInInspector] public Quaternion Rotation;
    [HideInInspector] public string ObjectID;
    [HideInInspector] public string title;

    public BookStackRoot currentStackRoot;

    public string bookID { get; private set; }

    [SerializeField] private Material masterMaterial; // Drag in the master material via Inspector

    private void Awake()
    {
        EnsureBookID(logIfMissing: false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (definition != null)
        {
            bookID = definition.bookID;
            title = definition.title;
            UpdateVisuals();
        }
    }
#endif

    public void ApplyDefinition(BookDefinition def)
    {
        definition = def;

        if (definition == null || string.IsNullOrEmpty(definition.bookID))
        {
            Debug.LogError($"[BookInfo] Missing/invalid BookDefinition on {name}. " +
                           $"This will break saving/loading because bookID won't map to a prefab.");
            return;
        }

        bookID = definition.bookID;
        title = definition.title;
        UpdateVisuals();
    }

    public void EnsureBookID(bool logIfMissing)
    {
        if (!string.IsNullOrEmpty(bookID)) return;

        if (definition != null && !string.IsNullOrEmpty(definition.bookID))
        {
            bookID = definition.bookID;
            title = definition.title;
        }
        else if (logIfMissing)
        {
            Debug.LogError($"[BookInfo] No definition on {name}. Assign a BookDefinition so bookID matches the database.");
        }
    }

    public void UpdateVisuals()
    {
        if (definition == null) return;

        if (titleText) titleText.text = definition.title;
        if (spineText) spineText.text = definition.title;

        var rends = GetComponentsInChildren<MeshRenderer>(true);
        var mpb = new MaterialPropertyBlock();

        foreach (var r in rends)
        {
            if (!r) continue;

            var mats = r.sharedMaterials;

            if (masterMaterial != null && mats.Length > 0)
            {
                mats[0] = masterMaterial;
                if (mats.Length > 1 && secondaryMaterial != null)
                    mats[1] = secondaryMaterial;

                r.sharedMaterials = mats;
            }


            // Apply color to _Color and _BaseColor using MPB
            r.GetPropertyBlock(mpb, 0);
            mpb.SetColor("_Color", definition.color);      // For Standard Shader
            mpb.SetColor("_BaseColor", definition.color);  // For URP/HDRP
            r.SetPropertyBlock(mpb, 0);
        }
    }
}
