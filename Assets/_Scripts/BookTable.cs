using System.Collections.Generic;
using UnityEngine;
using System;

public class BookTable : MonoBehaviour
{
    [Header("Table Metadata")]
    [SerializeField] private string tableID = "NewGUID";

    [Header("Grid Generation")]
    [SerializeField] private int rows = 3;
    [SerializeField] private int columns = 5;
    [SerializeField] private Vector2 spacing = new Vector2(0.25f, 0.25f); // X = left/right, Y = forward/back
    [SerializeField] private Vector3 offset = Vector3.zero;
    [SerializeField] private GameObject spotVisualPrefab;

    private List<TableSpot> tableSpots = new List<TableSpot>();

    private void Awake()
    {
        if (string.IsNullOrEmpty(tableID) || tableID == "NewGUID")
        {
            tableID = Guid.NewGuid().ToString();
            //Debug.Log($"Generated new TableID: {tableID} for {gameObject.name}");
        }

        GenerateTableSpots();
    }

    private void GenerateTableSpots()
    {
        tableSpots.Clear();

        Bounds bounds = GetComponentInChildren<MeshRenderer>()?.bounds ?? new Bounds(transform.position, Vector3.one);

        Vector3 origin = bounds.center + offset;
        float startX = origin.x - ((columns - 1) * spacing.x) / 2f;
        float startZ = origin.z - ((rows - 1) * spacing.y) / 2f;
        float y = bounds.max.y + offset.y; // Slightly above the surface

        int spotIndex = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector3 spotPos = new Vector3(startX + col * spacing.x, y, startZ + row * spacing.y);
                GameObject spot = new GameObject($"TableSpot_{row}_{col}");
                spot.transform.SetPositionAndRotation(spotPos, Quaternion.identity);
                spot.transform.parent = transform;

                TableSpot tableSpot = spot.AddComponent<TableSpot>();
                tableSpots.Add(tableSpot);
                tableSpot.SetIndex(spotIndex++);

                if (spotVisualPrefab)
                {
                    GameObject visual = Instantiate(spotVisualPrefab, spot.transform.position, Quaternion.identity, spot.transform);
                    tableSpot.InitializeFromVisual(visual);
                }
            }
        }
    }

    public List<TableSpot> GetTableSpots()
    {
        return tableSpots;
    }

    public void SetID(string id)
    {
        tableID = id;
    }

    public string GetID()
    {
        return tableID;
    }
}
