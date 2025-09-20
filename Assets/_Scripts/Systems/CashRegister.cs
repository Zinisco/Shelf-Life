using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class CashRegister : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bagPoint;          // where scanned books go
    [SerializeField] private TMP_Text registerText;       // UI to show scanned items
    [SerializeField] private AudioSource scanSound;
    [SerializeField] private BookDatabase bookDatabase;   // Reference for lookups

    // Tracks by bookID instead of title
    private Dictionary<string, (int quantity, int price)> saleItems = new();
    private int totalPrice = 0;

    // Called when the player scans a book (clicks/interacts with top book)
    public void ScanBook(BookInfo book)
    {
        if (book == null || string.IsNullOrEmpty(book.bookID)) return;

        string bookID = book.bookID;
        int price = book.definition?.price ?? 1;

        // Add or increment quantity
        if (saleItems.ContainsKey(bookID))
        {
            var entry = saleItems[bookID];
            entry.quantity++;
            saleItems[bookID] = entry;
        }
        else
        {
            saleItems[bookID] = (1, price);
        }

        totalPrice += price;

        // Play scan feedback
        if (scanSound != null) scanSound.Play();

        // Update UI
        UpdateRegisterUI();

        // Disable interaction (so you can’t scan twice)
        book.enabled = false;

        // Animate into the bag
        StartCoroutine(MoveBookToBag(book.gameObject));
    }

    private IEnumerator MoveBookToBag(GameObject book)
    {
        Transform t = book.transform;
        Vector3 startPos = t.position;
        Quaternion startRot = t.rotation;

        Vector3 endPos = bagPoint.position;
        Quaternion endRot = bagPoint.rotation;

        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float tLerp = elapsed / duration;

            t.position = Vector3.Lerp(startPos, endPos, tLerp);
            t.rotation = Quaternion.Slerp(startRot, endRot, tLerp);

            yield return null;
        }

        // Snap to final just in case
        t.position = endPos;
        t.rotation = endRot;

        // Parent to bag (optional, keeps hierarchy clean)
        t.SetParent(bagPoint);

        // Destroy after tiny delay (simulate disappearing inside bag)
        Destroy(book, 0.2f);
    }

    public void FinalizeSale()
    {
        if (saleItems.Count == 0) return;

        // Add total to wallet
        CurrencyManager.Instance.Add(totalPrice, isSale: true);

        // Remove stock from inventory
        foreach (var kv in saleItems)
        {
            string bookID = kv.Key;
            int qty = kv.Value.quantity;

            var def = bookDatabase.GetDefinitionByID(bookID);
            if (def != null)
                InventoryManager.Instance.RemoveStock(def, qty);
        }

        // Clear transaction
        saleItems.Clear();
        totalPrice = 0;
        UpdateRegisterUI();
    }

    private void UpdateRegisterUI()
    {
        if (registerText == null) return;

        if (saleItems.Count == 0)
        {
            registerText.text = "Register Ready";
            return;
        }

        string ui = "Scanned Items:\n";
        foreach (var kv in saleItems)
        {
            string bookID = kv.Key;
            int qty = kv.Value.quantity;
            int price = kv.Value.price;

            // Look up title for display
            var def = bookDatabase.GetDefinitionByID(bookID);
            string title = def != null ? def.title : bookID;

            ui += $"{title} x{qty} - ${price * qty}\n";
        }

        ui += $"\nTOTAL: ${totalPrice}";
        registerText.text = ui;
    }
}
