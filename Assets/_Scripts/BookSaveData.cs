using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BookSaveData
{
    public string bookID;             // Unique ID (used to retrieve prefab/definition)

    public string title;
    public string genre;
    public string summary;
    public float[] color;             // RGB format
    public List<string> tags = new();

    public string shelfID;
    public int spotIndex;
    public int stackIndex = -1;
    public Vector3 position;
    public Quaternion rotation;
}

