using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BookInfo : MonoBehaviour
{
    public enum BookOriginType { Freeform, Stack, Display }

    public class BookOrigin
    {
        public BookOriginType type;
        public Transform parent;
        public Vector3 localPos;
        public Quaternion localRot;
        public BookStackRoot stackRoot;
        public int stackIndex;
    }


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
    [HideInInspector] public BookOrigin lastOrigin;

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

            // Replace materials
            if (masterMaterial != null && mats.Length > 0)
            {
                mats[0] = masterMaterial;
                if (mats.Length > 1 && secondaryMaterial != null)
                    mats[1] = secondaryMaterial;

                r.sharedMaterials = mats;
            }

            // Apply color ONLY to element 0
            r.GetPropertyBlock(mpb, 0);
            mpb.SetColor("_Color", definition.color);      // For Standard Shader
            mpb.SetColor("_BaseColor", definition.color);  // For URP/HDRP
            r.SetPropertyBlock(mpb, 0);

            // Only clear if a second submesh actually exists
            if (mats.Length > 1)
            {
                r.SetPropertyBlock(null, 1);
            }
        }
    }

    public void RememberOrigin()
    {
        lastOrigin = new BookOrigin
        {
            parent = transform.parent,
            localPos = transform.localPosition,
            localRot = transform.localRotation
        };

        if (currentStackRoot != null)
        {
            lastOrigin.type = BookOriginType.Stack;
            lastOrigin.stackRoot = currentStackRoot;
            lastOrigin.stackIndex = currentStackRoot.GetBookIndex(gameObject);
        }
        else if (transform.parent != null && transform.parent.CompareTag("BookDisplay"))
        {
            lastOrigin.type = BookOriginType.Display;
        }
        else
        {
            lastOrigin.type = BookOriginType.Freeform;
        }
    }


    public void RestoreOrigin()
    {
        if (lastOrigin == null) return;

        switch (lastOrigin.type)
        {
            case BookOriginType.Stack:
                if (lastOrigin.stackRoot != null)
                {
                    int count = lastOrigin.stackRoot.GetCount();

                    // If index still valid, insert back there
                    if (lastOrigin.stackIndex >= 0 && lastOrigin.stackIndex <= count)
                    {
                        lastOrigin.stackRoot.InsertBookAt(gameObject, lastOrigin.stackIndex);
                        Debug.Log($"[BookInfo] Restored {name} to stack index {lastOrigin.stackIndex}");
                    }
                    else
                    {
                        // Always re-add safely to top
                        lastOrigin.stackRoot.AddBook(gameObject);
                        Debug.LogWarning($"[BookInfo] Index invalid, restored {name} to top of stack.");
                    }
                }
                else
                {
                    // fallback: put on ground, but not float
                    if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        transform.SetParent(null);
                        transform.position = hit.position;
                        transform.rotation = Quaternion.identity;
                    }
                    else
                    {
                        transform.SetParent(null);
                    }
                }
                break;


            case BookOriginType.Display:
                transform.SetParent(lastOrigin.parent, true);
                transform.localPosition = lastOrigin.localPos;
                transform.localRotation = lastOrigin.localRot;
                break;

            case BookOriginType.Freeform:
                transform.SetParent(lastOrigin.parent, true);
                transform.localPosition = lastOrigin.localPos;
                transform.localRotation = lastOrigin.localRot;
                break;
        }
    }
}
