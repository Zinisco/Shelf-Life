using System.Collections.Generic;
using UnityEngine;

public class CrateDeliveryManager : MonoBehaviour
{
    [SerializeField] private GameObject bookCratePrefab;
    [SerializeField] private DeliveryZone deliveryZone;

    private void Awake()
    {
        // Fallback in case the reference isn’t set
        if (deliveryZone == null)
        {
            deliveryZone = FindObjectOfType<DeliveryZone>();
            if (deliveryZone == null)
            {
                Debug.LogError("No DeliveryZone found in the scene.");
            }
        }
    }

    // -------------------------
    // SINGLE DELIVERIES
    // -------------------------

    public void DeliverCrate(List<BookDefinition> customBooks)
    {
        if (deliveryZone == null)
        {
            Debug.LogWarning("Delivery failed: No DeliveryZone available.");
            return;
        }

        GameObject tempCrate = Instantiate(bookCratePrefab);

        if (tempCrate.TryGetComponent<BookCrate>(out var bookCrate))
            bookCrate.SetCustomBooks(customBooks);

        if (!deliveryZone.TryPlacePreInstantiatedCrate(tempCrate))
        {
            Destroy(tempCrate);
            Debug.LogWarning("DeliveryZone full. Crate not delivered.");
        }
        else
        {
            Debug.Log($"Delivered single crate with {customBooks.Count} custom books.");
        }
    }

    public void DeliverDesignItemCrate(DesignItem itemDef)
    {
        if (deliveryZone == null)
        {
            Debug.LogWarning("Delivery failed: No DeliveryZone available.");
            return;
        }

        GameObject tempCrate = Instantiate(itemDef.cratePrefab);

        if (tempCrate.TryGetComponent<DesignItemCrate>(out var crate))
            crate.SetDesignItem(itemDef);

        if (!deliveryZone.TryPlacePreInstantiatedCrate(tempCrate))
        {
            Destroy(tempCrate);
            Debug.LogWarning("DeliveryZone full. Crate not delivered.");
        }
        else
        {
            Debug.Log($"Delivered single crate with design item: {itemDef.itemName}");
        }
    }

    // -------------------------
    // BATCH DELIVERIES
    // -------------------------

    public void DeliverCrateBatch(List<List<BookDefinition>> crateBatches)
    {
        if (deliveryZone == null)
        {
            Debug.LogWarning("Delivery failed: No DeliveryZone available.");
            return;
        }

        foreach (var books in crateBatches)
        {
            GameObject tempCrate = Instantiate(bookCratePrefab);

            if (tempCrate.TryGetComponent<BookCrate>(out var bookCrate))
                bookCrate.SetCustomBooks(books);

            if (!deliveryZone.TryPlacePreInstantiatedCrate(tempCrate))
            {
                Destroy(tempCrate);
                Debug.LogWarning("DeliveryZone full. Crate not delivered.");
            }
            else
            {
                Debug.Log($"Delivered crate with {books.Count} custom books.");
            }
        }
    }

    public void DeliverDesignItemBatch(List<DesignItem> items)
    {
        if (deliveryZone == null)
        {
            Debug.LogWarning("Delivery failed: No DeliveryZone available.");
            return;
        }

        foreach (var itemDef in items)
        {
            GameObject tempCrate = Instantiate(itemDef.cratePrefab);

            if (tempCrate.TryGetComponent<DesignItemCrate>(out var crate))
                crate.SetDesignItem(itemDef);

            if (!deliveryZone.TryPlacePreInstantiatedCrate(tempCrate))
            {
                Destroy(tempCrate);
                Debug.LogWarning("DeliveryZone full. Crate not delivered.");
            }
            else
            {
                Debug.Log($"Delivered crate with design item: {itemDef.itemName}");
            }
        }
    }
}
