using UnityEngine;

[System.Serializable]
public class BookSaveData
{
    public string title;
    public string genre;
    public float[] color; // RGB values
    public string shelfID;
    public int spotIndex;

    public Vector3 Position;     // Used if not shelved
    public Quaternion Rotation;
}
