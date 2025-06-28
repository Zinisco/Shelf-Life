using UnityEngine;

[System.Serializable]
public class BookSaveData
{
    public string bookID;             // Unique ID (used to retrieve prefab/definition)

    public string title;
    public string genre;
    public string summary;
    public float[] color;             // RGB format

    public string shelfID;
    public int spotIndex;
    public Vector3 position;
    public Quaternion rotation;
}

