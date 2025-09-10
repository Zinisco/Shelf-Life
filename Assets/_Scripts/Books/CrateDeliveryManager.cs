using System.Collections.Generic;
using UnityEngine;

public class CrateDeliveryManager : MonoBehaviour
{
    public GameObject bookCratePrefab;
    public Transform spawnPoint;

    public void DeliverCrate(List<BookDefinition> customBooks)
    {
        GameObject crate = Instantiate(bookCratePrefab, spawnPoint.position, Quaternion.identity);

        if (crate.TryGetComponent<BookCrate>(out var bookCrate))
        {
            bookCrate.SetCustomBooks(customBooks);
        }

        Debug.Log($"Delivered crate with {customBooks.Count} custom books.");
    }

}
