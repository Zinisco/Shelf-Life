using System.Collections.Generic;
using UnityEngine;
using System;
using static UnityEngine.Rendering.DebugUI.Table;

public class Bookshelf : MonoBehaviour
{
    [Header("Shelf Metadata")]
    public string ShelfID = Guid.NewGuid().ToString(); // Assigned automatically unless overridden

    [Header("Shelf Spot Generation")]
    [SerializeField] private int rows = 5;
    [SerializeField] private int columns = 21;
    [SerializeField] private Vector2 spacing = new Vector2(0.1f, 0.8f); // X = horizontal spacing, Y = vertical spacing
    [SerializeField] private Vector3 offset = Vector3.zero; // Offset from center
    [SerializeField] private GameObject spotVisualPrefab; // Optional visual helper

    private List<ShelfSpot> shelfSpots = new List<ShelfSpot>();

    public List<Transform> GetAvailableSpots()
    {
        List<Transform> available = new List<Transform>();
        foreach (ShelfSpot spot in shelfSpots)
        {
            if (!spot.IsOccupied())
                available.Add(spot.transform);
        }
        return available;
    }

    private void Awake()
    {
        GenerateShelfSpots();
    }

    private void GenerateShelfSpots()
    {
        shelfSpots.Clear();

        Bounds bounds = GetComponentInChildren<MeshRenderer>()?.bounds ?? new Bounds(transform.position, Vector3.one);

        Vector3 origin = bounds.center + offset;
        float startX = origin.x - ((columns - 1) * spacing.x) / 2f;
        float startY = origin.y + ((rows - 1) * spacing.y) / 2f;
        float z = origin.z;

        int spotIndex = 0;
        


        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector3 spotPos = new Vector3(startX + col * spacing.x, startY - row * spacing.y, z);
                GameObject spot = new GameObject($"Spot_{row}_{col}");
                spot.transform.SetPositionAndRotation(spotPos, Quaternion.LookRotation(transform.forward));
                spot.transform.parent = transform;

                // Add ShelfSpot component
                ShelfSpot shelfSpot = spot.AddComponent<ShelfSpot>();
                shelfSpots.Add(shelfSpot);
                shelfSpot.SetIndex(spotIndex++); // New line to track spot index

                // Optional visual
                if (spotVisualPrefab)
                {
                    GameObject visual = Instantiate(spotVisualPrefab, spot.transform.position, spot.transform.rotation, spot.transform);
                    shelfSpot.InitializeFromVisual(visual);
                }

            }
        }
    }


    public List<ShelfSpot> GetShelfSpots()
    {
        return shelfSpots;
    }

    public void SetID(string id)
    {
        ShelfID = id;
    }

    public string GetID()
    {
        return ShelfID;
    }

}
