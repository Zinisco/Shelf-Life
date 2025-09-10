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
    /// Parents & positions the book under this root.
    /// </summary>
    public void AddBook(GameObject book)
    {
        var info = book.GetComponent<BookInfo>();
        if (info == null || !CanStack(info.title) || books.Contains(book))
            return;

        books.Add(book);
        book.transform.SetParent(transform, worldPositionStays: true);
        if (context == StackContext.Table)
            book.transform.localPosition = new Vector3(0f, bookThickness * (books.Count - 1), 0f);
        else // Shelf
            book.transform.localPosition = new Vector3(0f, 0f, bookThickness * (books.Count - 1));

        book.transform.localRotation = Quaternion.identity;
        info.currentStackRoot = this;
    }

    /// <summary>
    /// Un-parents and reflows remaining books; destroys root if empty.
    /// </summary>
    public void RemoveBook(GameObject book)
    {
        if (!books.Remove(book))
            return;

        book.transform.SetParent(null, worldPositionStays: true);
        book.GetComponent<BookInfo>().currentStackRoot = null;

        // Reposition remaining books
        for (int i = 0; i < books.Count; i++)
        {
            books[i].transform.localPosition = new Vector3(0f, bookThickness * i, 0f);
        }

        if (books.Count == 0)
            Destroy(gameObject);
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
