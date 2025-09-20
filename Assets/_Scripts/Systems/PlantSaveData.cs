using UnityEngine;

[System.Serializable]
public class PlantSaveData
{
    public string plantID;               // "Monstera", "Zanzibar", etc.
    public PlantMover.PlantType type;    // Table or Floor
    public Vector3 position;
    public Quaternion rotation;

    public string tableID;               // <-- NEW: surface anchor ID if on a table
}
