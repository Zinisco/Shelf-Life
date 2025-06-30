using System.Collections.Generic;

[System.Serializable]
public class TableSpotSaveData
{
    public string spotID;
    public List<BookSaveData> stackedBooks = new();
}

