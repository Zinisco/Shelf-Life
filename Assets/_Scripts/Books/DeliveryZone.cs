using System.Collections.Generic;
using UnityEngine;

public class DeliveryZone : MonoBehaviour
{
    [SerializeField] private Vector3 boxSize = new Vector3(2, 1, 2);
    [SerializeField] private float spacing = 0.15f;       // horizontal margin
    [SerializeField] private float physicsMargin = 0.05f; // hidden extra gap
    [SerializeField] private LayerMask crateLayer;

    private List<Vector3> basePositions = new();
    private Dictionary<int, HashSet<int>> occupiedIndicesPerLayer = new();

    private void Awake()
    {
        ResetOccupiedMap();
        GenerateBasePositions(Vector3.one); // init with dummy
    }

    private void ResetOccupiedMap()
    {
        occupiedIndicesPerLayer.Clear();
        for (int i = 0; i < 5; i++)
            occupiedIndicesPerLayer[i] = new HashSet<int>();
    }

    private void GenerateBasePositions(Vector3 crateSize)
    {
        basePositions.Clear();

        float stepX = crateSize.x + spacing + physicsMargin;
        float stepZ = crateSize.z + spacing + physicsMargin;

        int perRowX = Mathf.Max(1, Mathf.FloorToInt(boxSize.x / stepX));
        int perRowZ = Mathf.Max(1, Mathf.FloorToInt(boxSize.z / stepZ));

        float startX = -boxSize.x / 2 + crateSize.x / 2;
        float startZ = -boxSize.z / 2 + crateSize.z / 2;
        float baseY = -boxSize.y / 2;

        for (int z = 0; z < perRowZ; z++)
        {
            for (int x = 0; x < perRowX; x++)
            {
                Vector3 localOffset = new Vector3(startX + x * stepX, baseY, startZ + z * stepZ);
                Vector3 worldPos = transform.TransformPoint(localOffset);
                basePositions.Add(worldPos);
            }
        }

        Debug.Log($"[DeliveryZone] Generated {basePositions.Count} slots");
    }

    public bool TrySpawnCrate(GameObject cratePrefab)
    {
        GameObject crate = Instantiate(cratePrefab);
        CrateDimensions dim = crate.GetComponent<CrateDimensions>();
        if (dim == null) { Destroy(crate); return false; }

        GenerateBasePositions(dim.Size);
        PrepareCrateForPlacement(crate);

        if (TryPlaceCrateAtAnyValidPosition(crate, dim.Size))
            return true;

        Destroy(crate);
        return false;
    }

    public bool TryPlacePreInstantiatedCrate(GameObject crate)
    {
        CrateDimensions dim = crate.GetComponent<CrateDimensions>();
        if (dim == null) { Destroy(crate); return false; }

        GenerateBasePositions(dim.Size);
        PrepareCrateForPlacement(crate);

        return TryPlaceCrateAtAnyValidPosition(crate, dim.Size);
    }

    private void PrepareCrateForPlacement(GameObject crate)
    {
        if (crate.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    private bool TryPlaceCrateAtAnyValidPosition(GameObject crate, Vector3 size)
    {
        const int maxVerticalLayers = 5;

        for (int layer = 0; layer < maxVerticalLayers; layer++)
        {
            if (layer > 0 && !IsLayerFull(layer - 1))
                return false;

            for (int i = 0; i < basePositions.Count; i++)
            {
                if (occupiedIndicesPerLayer[layer].Contains(i)) continue;

                float layerHeight = Mathf.Max(size.y, 0.8f);
                Vector3 basePos = basePositions[i] + Vector3.up * (layer * layerHeight);

                if (layer > 0 && !IsDirectlyAboveCrate(basePos, size))
                    continue;

                // overlap check
                Vector3 halfExtents = size * 0.5f;
                Vector3 checkCenter = basePos + Vector3.up * halfExtents.y;

                if (Physics.CheckBox(checkCenter, halfExtents, Quaternion.identity, crateLayer))
                    continue;

                PlaceCrateAtPosition(crate, basePos);
                occupiedIndicesPerLayer[layer].Add(i);
                return true;
            }
        }

        return false;
    }

    private void PlaceCrateAtPosition(GameObject crate, Vector3 targetPos)
    {
        if (crate.TryGetComponent(out Collider col))
        {
            float halfHeight = col.bounds.size.y * 0.5f;
            crate.transform.position = targetPos + Vector3.up * halfHeight;
        }
        else
        {
            crate.transform.position = targetPos;
        }
    }

    private bool IsLayerFull(int layer)
    {
        return occupiedIndicesPerLayer[layer].Count >= basePositions.Count;
    }

    private bool IsDirectlyAboveCrate(Vector3 pos, Vector3 size)
    {
        Ray ray = new Ray(pos + Vector3.up * 0.3f, Vector3.down);
        return Physics.Raycast(ray, 2f, crateLayer);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, boxSize);
    }
#endif
}
