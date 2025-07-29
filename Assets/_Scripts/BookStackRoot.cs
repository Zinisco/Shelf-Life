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
        Debug.Log($"Removed {book.name} from stack. Remaining: {books.Count}");

        if (books.Count == 0)
        {
            Debug.LogWarning("StackRoot destroyed because it was empty.");
            Destroy(gameObject);
        }
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
