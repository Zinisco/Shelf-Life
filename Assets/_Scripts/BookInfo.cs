using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BookInfo : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text spineText;

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

    private void Awake()
    {
        // Be lenient here: crates will set the definition right after instantiation.
        EnsureBookID(logIfMissing: false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (definition != null)
        {
            bookID = definition.bookID;
            title = definition.title;
            UpdateVisuals(); // reflect changes immediately in editor
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

    /// <summary>
    /// If a definition is already present, copy its ID/title. If not, optionally log.
    /// </summary>
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

        // Update text
        if (titleText) titleText.text = definition.title;
        if (spineText) spineText.text = definition.title;

        // Tint all renderers with MPB (safe in Edit Mode and Play Mode)
        var rends = GetComponentsInChildren<MeshRenderer>(true);
        var mpb = new MaterialPropertyBlock();

        foreach (var r in rends)
        {
            if (!r) continue;

            r.GetPropertyBlock(mpb);
            // Attempt both common color properties (Built?in / URP / HDRP)
            mpb.SetColor("_Color", definition.color);
            mpb.SetColor("_BaseColor", definition.color);
            r.SetPropertyBlock(mpb);
        }
    }
}
