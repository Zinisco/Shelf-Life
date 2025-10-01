using System.Collections.Generic;
using UnityEngine;

public enum StackContext { Table, Shelf }

[RequireComponent(typeof(BoxCollider))]
public class BookStackRoot : MonoBehaviour
{
    public StackContext context;

    [Header("Stack Settings")]
    [Tooltip("Title shared by all books in this stack")]
    public string stackTitle;
    [Tooltip("Vertical thickness of one book (used for stacking offset)")]
    public float bookThickness = 0.12f;
    [Tooltip("Maximum number of books allowed in this stack")]
    public int maxStackHeight = 4;
    public bool wasJustNudged = false;

    [HideInInspector] public List<GameObject> books = new List<GameObject>();

    /// <summary>
    /// Returns true if the incoming title matches and there's room.
    /// </summary>
    public bool CanStack(string title)
    {
        return string.Equals(title?.Trim(), stackTitle?.Trim(), System.StringComparison.OrdinalIgnoreCase)
               && books.Count < maxStackHeight;
    }

    /// <summary>
    /// World-space position where the next book should be placed.
    /// </summary>
    public Vector3 TopPosition
    {
        get
        {
            if (context == StackContext.Table)
                return transform.position + Vector3.up * (bookThickness * books.Count);
            else
                return transform.position + transform.up * (bookThickness * books.Count);
        }
    }

    /// <summary>
    /// Un-parents and reflows remaining books; destroys root if empty.
    /// </summary>
    public void RemoveBook(GameObject book, bool rebuild = true)
    {
        if (!books.Remove(book))
            return;

        book.transform.SetParent(null, worldPositionStays: true);
        var info = book.GetComponent<BookInfo>();
        if (info != null) info.currentStackRoot = null;

        if (rebuild)
            RebuildStackLayout();

        if (books.Count == 0)
            Destroy(gameObject);
    }


    public void InsertBookAt(GameObject book, int index)
    {
        if (index < 0 || index > books.Count)
            index = books.Count; // fallback to top

        books.Insert(index, book);
        book.transform.SetParent(transform, false);

        // Reassign reference
        var info = book.GetComponent<BookInfo>();
        if (info != null)
            info.currentStackRoot = this;

        RebuildStackLayout();
    }

    public void AddBook(GameObject book)
    {
        var info = book.GetComponent<BookInfo>();
        if (info == null || !CanStack(info.title) || books.Contains(book))
            return;

        books.Add(book);

        // Ensure root is aligned to the first book’s position
        if (books.Count == 1)
        {
            // Root goes exactly where the first book is
            transform.position = book.transform.position;
            if (books.Count == 1)
            {
                transform.position = book.transform.position;
                transform.rotation = book.transform.rotation; // match the book instead of identity
            }

        }

        book.transform.SetParent(transform, false);
        info.currentStackRoot = this;

        RebuildStackLayout();
    }

    public void RebuildStackLayout()
    {
        // Root stays where it was first created

        for (int i = 0; i < books.Count; i++)
        {
            if (context == StackContext.Table)
                books[i].transform.localPosition = new Vector3(0f, bookThickness * i, 0f);
            else // Shelf
                books[i].transform.localPosition = new Vector3(0f, 0f, bookThickness * i);
        }
    }


    void Reset()
    {
        // Auto-fit the BoxCollider to stack dimensions
        var bc = GetComponent<BoxCollider>();
        bc.center = new Vector3(0, (maxStackHeight * bookThickness) * 0.5f, 0);
        bc.size = new Vector3(0.3f, maxStackHeight * bookThickness, 0.12f);
        bc.isTrigger = true;
    }

    public int GetCount() => books.Count;
    public int GetBookIndex(GameObject book) => books.IndexOf(book);
}
