using System.Collections.Generic;
using UnityEngine;

public class BookStackRoot : MonoBehaviour
{
    public List<GameObject> books = new List<GameObject>();
    public string stackTitle;
    public bool wasJustNudged = false;

    public void AddBook(GameObject book)
    {
        if (!books.Contains(book))
            books.Add(book);
    }

    public void RemoveBook(GameObject book)
    {
        books.Remove(book);
        if (books.Count == 0)
            Destroy(gameObject); // Remove stack root if empty
    }

    public int GetCount()
    {
        return books.Count;
    }

    public int GetBookIndex(GameObject book)
    {
        return books.IndexOf(book);
    }

}
