using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BookInfo : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text spineText;

    [Header("Definition")]
    [Tooltip("Reference to the ScriptableObject definition for this book")]
    public BookDefinition definition;

    [Header("Book Metadata")]
    public List<string> tags = new(); // Default empty


    [Header("Shelf/Stack Tracking")]
    [Tooltip("Current shelf spot this book occupies (if any)")]
    public ShelfSpot currentSpot;

    [Header("Table Tracking")]
    [Tooltip("Current table spot this book occupies (if any)")]
    public TableSpot currentTableSpot;

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
    this.definition = def;
    bookID = def.bookID;
    UpdateVisuals();

    }


    public void UpdateVisuals()
    {
        // Update the mesh color
        GetComponentInChildren<MeshRenderer>().material.color = definition.color;

        // Update title UI if assigned
        if (titleText != null)
        {
            titleText.text = definition.title;
            spineText.text = definition.title; 
            //Debug.Log($"[UpdateVisuals] Set title: {definition.title}");
        }
        else
        {
            Debug.LogWarning($"[BookInfo] Missing titleText reference on: {gameObject.name}");
        }

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

    public void SetTableSpot(TableSpot spot, string objectID, int index)
    {
        currentTableSpot = spot;
        ObjectID = objectID;
        SpotIndex = index; 
    }

    public void ClearTableSpot()
    {
        currentTableSpot = null;
        ObjectID = string.Empty;
        SpotIndex = -1;
    }

    public void AddTag(string tag)
    {
        if (!tags.Contains(tag))
            tags.Add(tag);
    }

    public void RemoveTag(string tag)
    {
        tags.Remove(tag);
    }

    public bool HasTag(string tag)
    {
        return tags.Contains(tag);
    }

}