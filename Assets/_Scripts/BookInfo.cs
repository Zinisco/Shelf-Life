using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BookInfo : MonoBehaviour
{
    [Header("Definition")]
    [Tooltip("Reference to the ScriptableObject definition for this book")]
    public BookDefinition definition;

    [Header("Shelf/Stack Tracking")]
    [Tooltip("Current shelf spot this book occupies (if any)")]
    public ShelfSpot currentSpot;

    [HideInInspector] public Vector3 Position;
    [HideInInspector] public Quaternion Rotation;
    [HideInInspector] public string ObjectID;
    [HideInInspector] public int SpotIndex = -1;

    /// <summary>
    /// Computed ID from the definition (if assigned)
    /// </summary>
    public string bookID { get; set; }

    /// <summary>
    /// Applies a BookDefinition to this instance, populating all display properties.
    /// </summary>
    public void ApplyDefinition(BookDefinition def)
{
    definition = def;
    bookID = def.bookID;
    UpdateVisuals();

    }


    public void UpdateVisuals()
    {
        // Update the mesh color
        GetComponentInChildren<MeshRenderer>().material.color = definition.color;

        // Update title/genre UI text if present
        var titleText = GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (titleText != null)
            titleText.text = definition.title;

        // You can also update genre, summary, etc. here
    }


    /// <summary>
    /// Call when you place on a shelf/stack to track its spot.
    /// </summary>
    public void SetShelfSpot(ShelfSpot spot, string objectID, int index)
    {
        currentSpot = spot;
        ObjectID = objectID;
        SpotIndex = index;
    }

    /// <summary>
    /// Clears any shelf/stack tracking so this book is considered free.
    /// </summary>
    public void ClearShelfSpot()
    {
        currentSpot = null;
        ObjectID = string.Empty;
        SpotIndex = -1;
    }
}