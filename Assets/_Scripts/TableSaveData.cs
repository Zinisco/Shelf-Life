using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TableSaveData
{
    public string tableID;
    public Vector3 position;
    public Quaternion rotation;
    public List<BookSaveData> stackedBooks = new();
}
