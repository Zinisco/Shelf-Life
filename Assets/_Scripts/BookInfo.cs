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

    [HideInInspector] public Vector3 Position;
    [HideInInspector] public Quaternion Rotation;
    [HideInInspector] public string ObjectID;
    [HideInInspector] public string title;

    public BookStackRoot currentStackRoot;

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
    title = def.title;
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
}