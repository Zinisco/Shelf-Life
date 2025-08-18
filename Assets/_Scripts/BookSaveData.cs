using System.Collections.Generic;
using UnityEngine;
using static FreeformBookshelf;

[System.Serializable]
public class BookSaveData
{
    public string bookID;             // Unique ID (used to retrieve prefab/definition)

    public string title;
    public string genre;
    public int cost;
    public int price;
    public string summary;
    public float[] color;             // RGB format
    public List<string> tags = new();

    public string shelfID;
    public int stackIndex = -1;
    public string stackGroupID; // a shared ID among books in the same stack
    public string tableID; // ID of the table (SurfaceAnchor) this book/stack was placed on
    public Vector3 position;
    public Quaternion rotation;
    // NEW: for freeform shelves (stacks or single)
    public ShelfRef shelfRef;
}

[System.Serializable]
public class FreeformShelfSaveData
{
    public string shelfObjectID;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 localScale;
}

[System.Serializable]
public class ShelfRef
{
    public string shelfObjectID;   // FreeformBookshelf.ObjectID
    public string regionName;      // ShelfArea name (child GameObject name)
}