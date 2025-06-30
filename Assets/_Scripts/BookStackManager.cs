using System.Collections.Generic;
using UnityEngine;

public class BookStackManager : MonoBehaviour
{
    [SerializeField] private float stackRange = 2f; // Range to detect nearby table spots
    private TableSpot closestSpot;

    private List<TableSpot> tableSpots = new List<TableSpot>();

    private void Awake()
    {
        TableSpot[] foundSpots = Object.FindObjectsOfType<TableSpot>();
        tableSpots.AddRange(foundSpots);
    }

    private void Update()
    {
        FindClosestSpot();
    }

    private void FindClosestSpot()
    {
        TableSpot[] allSpots = Object.FindObjectsOfType<TableSpot>();
        float closestDistance = Mathf.Infinity;
        closestSpot = null;

        foreach (var spot in allSpots)
        {
            float distance = Vector3.Distance(transform.position, spot.transform.position);
            if (distance < stackRange && distance < closestDistance)
            {
                closestDistance = distance;
                closestSpot = spot;
            }
        }
    }

    public TableSpot GetNearestValidTableSpot(Vector3 position, BookInfo bookInfo, float maxDistance = 2f)
    {
        TableSpot bestSpot = null;
        float closestDistance = maxDistance;

        foreach (var spot in tableSpots)
        {
            if (spot == null) continue;
            if (!spot.CanStack(bookInfo)) continue;

            float dist = Vector3.Distance(position, spot.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                bestSpot = spot;
            }
        }

        return bestSpot;
    }

    public bool TryStackBook(GameObject book, TableSpot spot)
    {
        if (book == null || spot == null) return false;

        BookInfo bookInfo = book.GetComponent<BookInfo>();
        if (bookInfo == null) return false;

        if (spot.CanStack(bookInfo))
        {
            spot.StackBook(book);
            return true;
        }

        return false;
    }

    public bool TryUnstackBook(out GameObject book)
    {
        book = null;

        if (closestSpot == null) return false;
        if (closestSpot.GetStackCount() == 0) return false;

        book = closestSpot.PopTopBook();
        return book != null;
    }

    public bool IsNearSpot()
    {
        return closestSpot != null;
    }

    public void StackBookOnTable(GameObject book, TableSpot spot)
    {
        book.transform.position = spot.GetNextStackPosition();
        book.transform.rotation = Quaternion.Euler(0, 90, 0); // face forward
        book.transform.SetParent(spot.transform, true);

        var rb = book.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
    }

}
