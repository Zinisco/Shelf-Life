using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Books/Book Database")]
public class BookDatabase : ScriptableObject
{
    public List<BookDefinition> allBooks;

    private Dictionary<string, BookDefinition> bookLookup;

    public void Initialize()
    {
        bookLookup = new Dictionary<string, BookDefinition>();
        foreach (var def in allBooks)
        {
            bookLookup[def.bookID] = def;
        }
    }

    public BookDefinition GetBookByID(string id)
    {
        if (bookLookup == null) Initialize();
        return bookLookup.TryGetValue(id, out var def) ? def : null;
    }

    public GameObject GetBookPrefabByID(string id)
    {
        var def = GetBookByID(id);
        return def != null ? def.prefab : null;
    }

    public BookDefinition GetDefinitionByID(string id) => GetBookByID(id);

}

