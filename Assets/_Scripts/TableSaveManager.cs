using System.Collections.Generic;
using UnityEngine;

public class TableSaveManager : MonoBehaviour
{
    [SerializeField] private string tableID;
    [SerializeField] private TableSpot tableSpot;

    private static TableSaveManager instance;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    public TableSaveData CaptureData()
    {
        TableSaveData data = new TableSaveData();
        data.tableID = tableID;
        data.stackedBooks = new List<BookSaveData>();

        foreach (GameObject book in tableSpot.GetStackedBooks())
        {
            BookInfo info = book.GetComponent<BookInfo>();
            if (info == null) continue;

            BookSaveData bookData = new BookSaveData
            {
                bookID = info.bookID,
                position = book.transform.position,
                rotation = book.transform.rotation
            };

            data.stackedBooks.Add(bookData);
        }

        return data;
    }

    public void LoadData(TableSaveData data, BookDatabase bookDatabase)
    {
        if (data == null || data.stackedBooks == null) return;

        tableSpot.ClearStack();

        foreach (BookSaveData bookData in data.stackedBooks)
        {
            GameObject prefab = bookDatabase.GetBookPrefabByID(bookData.bookID);
            if (prefab != null)
            {
                GameObject spawnedBook = Instantiate(prefab, bookData.position, bookData.rotation);
                tableSpot.ForceAddBookToStack(spawnedBook);
            }

        }
    }
}
