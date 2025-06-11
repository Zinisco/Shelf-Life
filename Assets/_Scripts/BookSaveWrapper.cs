using static BookSaveManager;
using System.Collections.Generic;

[System.Serializable]
public class BookSaveWrapper
{
    public List<BookSaveData> books = new List<BookSaveData>();
}
