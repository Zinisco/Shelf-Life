using System.Collections.Generic;
using UnityEngine;

public class DeliveryZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField] private Vector3 boxSize = new Vector3(4, 2, 4);   // total delivery area
    [SerializeField] private Vector2 slotSize = new Vector2(0.5f, 0.5f); // base cell size (X,Z)
    [SerializeField] private float layerHeight = 1f;                   // vertical spacing per layer
    [SerializeField] private int maxLayers = 5;
    [SerializeField] private LayerMask crateLayer;

    private int gridX, gridZ;
    private Dictionary<int, bool[,]> occupiedGridPerLayer = new(); // layer ? grid occupancy

    private void Awake()
    {
        BuildGrid();
    }

    /// <summary>
    /// Build the base grid once. Each grid cell is a slotSize footprint.
    /// </summary>
    private void BuildGrid()
    {
        gridX = Mathf.Max(1, Mathf.FloorToInt(boxSize.x / slotSize.x));
        gridZ = Mathf.Max(1, Mathf.FloorToInt(boxSize.z / slotSize.y));

        occupiedGridPerLayer.Clear();
        for (int l = 0; l < maxLayers; l++)
            occupiedGridPerLayer[l] = new bool[gridX, gridZ];

        Debug.Log($"[DeliveryZone] Built grid {gridX}x{gridZ} per layer, {maxLayers} layers");
    }

    // --------------------------
    // PUBLIC API
    // --------------------------

    public bool TrySpawnCrate(GameObject cratePrefab)
    {
        GameObject crate = Instantiate(cratePrefab);
        if (!TryPlacePreInstantiatedCrate(crate))
        {
            Destroy(crate);
            return false;
        }
        return true;
    }

    public bool TryPlacePreInstantiatedCrate(GameObject crate)
    {
        if (!crate.TryGetComponent(out CrateDimensions dim))
        {
            Debug.LogWarning("Crate missing CrateDimensions component");
            Destroy(crate);
            return false;
        }

        PrepareCrateForPlacement(crate);

        // convert crate size into grid cells
        int cellsX = Mathf.CeilToInt(dim.Size.x / slotSize.x);
        int cellsZ = Mathf.CeilToInt(dim.Size.z / slotSize.y);

        // try to find a valid placement
        for (int layer = 0; layer < maxLayers; layer++)
        {
            if (layer > 0 && !IsLayerFullBelow(layer - 1))
                return false; // must fill below first

            for (int gx = 0; gx <= gridX - cellsX; gx++)
            {
                for (int gz = 0; gz <= gridZ - cellsZ; gz++)
                {
                    if (CanOccupy(gx, gz, cellsX, cellsZ, layer))
                    {
                        Vector3 worldPos = GridToWorld(gx, gz, cellsX, cellsZ, layer, dim.Size.y);
                        PlaceCrateAtPosition(crate, worldPos);

                        MarkOccupied(gx, gz, cellsX, cellsZ, layer);
                        return true;
                    }
                }
            }
        }

        Debug.LogWarning("DeliveryZone full. No space for new crate.");
        return false;
    }

    // --------------------------
    // GRID LOGIC
    // --------------------------

    private bool CanOccupy(int startX, int startZ, int sizeX, int sizeZ, int layer)
    {
        var grid = occupiedGridPerLayer[layer];
        for (int x = startX; x < startX + sizeX; x++)
            for (int z = startZ; z < startZ + sizeZ; z++)
                if (grid[x, z]) return false;

        return true;
    }

    private void MarkOccupied(int startX, int startZ, int sizeX, int sizeZ, int layer)
    {
        var grid = occupiedGridPerLayer[layer];
        for (int x = startX; x < startX + sizeX; x++)
            for (int z = startZ; z < startZ + sizeZ; z++)
                grid[x, z] = true;
    }

    private Vector3 GridToWorld(int gx, int gz, int sizeX, int sizeZ, int layer, float crateHeight)
    {
        // center of the slot rectangle
        float cellOriginX = -boxSize.x / 2 + (gx + sizeX / 2f) * slotSize.x;
        float cellOriginZ = -boxSize.z / 2 + (gz + sizeZ / 2f) * slotSize.y;

        Vector3 local = new Vector3(cellOriginX, -boxSize.y / 2 + layer * layerHeight, cellOriginZ);
        Vector3 world = transform.TransformPoint(local);

        // align bottom of crate to grid Y
        world.y += crateHeight / 2f;
        return world;
    }

    private bool IsLayerFullBelow(int layer)
    {
        var grid = occupiedGridPerLayer[layer];
        for (int x = 0; x < gridX; x++)
            for (int z = 0; z < gridZ; z++)
                if (!grid[x, z]) return false;
        return true;
    }

    // --------------------------
    // CRATE HELPERS
    // --------------------------

    private void PrepareCrateForPlacement(GameObject crate)
    {
        if (crate.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void PlaceCrateAtPosition(GameObject crate, Vector3 targetPos)
    {
        crate.transform.position = targetPos;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, boxSize);

        // Bail out if grid isn't built yet
        if (gridX == 0 || gridZ == 0) return;

        // Always draw for all layers, offset upward per layer
        for (int layer = 0; layer < maxLayers; layer++)
        {
            var grid = occupiedGridPerLayer.ContainsKey(layer) ? occupiedGridPerLayer[layer] : null;
            float layerY = -boxSize.y / 2 + layer * layerHeight;

            for (int x = 0; x < gridX; x++)
            {
                for (int z = 0; z < gridZ; z++)
                {
                    Vector3 localCenter = new Vector3(
                        -boxSize.x / 2 + (x + 0.5f) * slotSize.x,
                        layerY,
                        -boxSize.z / 2 + (z + 0.5f) * slotSize.y
                    );
                    Vector3 worldCenter = transform.TransformPoint(localCenter);
                    Vector3 cellSize = new Vector3(slotSize.x, 0.05f, slotSize.y);

                    bool occupied = grid != null && grid[x, z];
                    Gizmos.color = occupied
                        ? new Color(1f, 0f, 0f, 0.5f)   // red semi-transparent
                        : new Color(0f, 1f, 0f, 0.2f); // green transparent

                    Gizmos.DrawCube(worldCenter, cellSize);
                    Gizmos.color = Color.black;
                    Gizmos.DrawWireCube(worldCenter, cellSize);
                }
            }
        }
    }
#endif

}
