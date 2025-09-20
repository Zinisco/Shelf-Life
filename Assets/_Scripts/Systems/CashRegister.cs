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

    private Dictionary<string, (int quantity, int price)> saleItems = new();
    private int totalPrice = 0;

    // Called when the player scans a book (clicks/interacts with top book)
    public void ScanBook(BookInfo book)
    {
        if (book == null) return;

        string title = book.definition?.title ?? "Unknown Book";
        int price = book.definition?.price ?? 1;

        // Add or increment quantity
        if (saleItems.ContainsKey(title))
        {
            var entry = saleItems[title];
            entry.quantity++;
            saleItems[title] = entry;
        }
        else
        {
            saleItems[title] = (1, price);
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
            string title = kv.Key;
            int qty = kv.Value.quantity;
            int price = kv.Value.price;
            ui += $"{title} x{qty} - ${price * qty}\n";
        }

        ui += $"\nTOTAL: ${totalPrice}";
        registerText.text = ui;
    }
}
