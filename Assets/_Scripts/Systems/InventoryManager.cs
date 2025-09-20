using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    // Tracks stock by bookID
    private Dictionary<string, int> stock = new Dictionary<string, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Add books to inventory (e.g. after order arrives).
    /// </summary>
    public void AddStock(BookDefinition def, int amount = 1)
    {
        if (def == null || string.IsNullOrEmpty(def.bookID)) return;

        if (!stock.ContainsKey(def.bookID))
            stock[def.bookID] = 0;

        stock[def.bookID] += amount;
        Debug.Log($"[Inventory] Added {amount}x {def.title}. New count = {stock[def.bookID]}");
    }

    /// <summary>
    /// Remove books from inventory (e.g. when sold).
    /// </summary>
    public bool RemoveStock(BookDefinition def, int amount = 1)
    {
        if (def == null || string.IsNullOrEmpty(def.bookID)) return false;
        if (!stock.ContainsKey(def.bookID)) return false;

        if (stock[def.bookID] < amount)
        {
            Debug.LogWarning($"[Inventory] Tried to remove {amount} of {def.title}, but only {stock[def.bookID]} in stock.");
            return false;
        }

        stock[def.bookID] -= amount;
        Debug.Log($"[Inventory] Removed {amount}x {def.title}. New count = {stock[def.bookID]}");
        return true;
    }

    /// <summary>
    /// Get how many of this book are on hand.
    /// </summary>
    public int GetQuantity(string bookID)
    {
        if (string.IsNullOrEmpty(bookID)) return 0;
        return stock.TryGetValue(bookID, out int qty) ? qty : 0;
    }

    /// <summary>
    /// Debug helper to print current stock levels.
    /// </summary>
    public void PrintInventory()
    {
        Debug.Log("=== Current Inventory ===");
        foreach (var pair in stock)
            Debug.Log($"{pair.Key}: {pair.Value}");
    }
}
