using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Design Item Database")]
public class DesignItemDatabase : ScriptableObject
{
    [SerializeField] private List<DesignItem> items = new List<DesignItem>();

    public List<DesignItem> Items => items;

    /// <summary>
    /// Get all items in the database for a specific category.
    /// </summary>
    public List<DesignItem> GetItemsByCategory(DesignItem.Category category)
    {
        return items.FindAll(item => item.category == category);
    }

    /// <summary>
    /// Get a specific item by name (optional helper).
    /// </summary>
    public DesignItem GetItemByName(string name)
    {
        return items.Find(item => item.itemName == name);
    }
}
