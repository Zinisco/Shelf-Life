using System.Collections.Generic;
using UnityEngine;

public class FreeformBookshelf : MonoBehaviour
{
    [System.Serializable]
    public class ShelfArea
    {
        public string name = "Shelf";
        public Vector3 localPosition = Vector3.zero;
        public Vector3 size = new Vector3(2.3f, 0.7f, 0.6f);
        public float bookSpacing = 0.05f;
        [HideInInspector] public BoxCollider regionCollider;
    }

    [Header("Shelf Metadata")]
    [SerializeField] private string ObjectID = "NewGUID";

    [Header("Shelf Configuration")]
    public List<ShelfArea> shelfAreas = new List<ShelfArea>();

    [Header("Book Parent Root")]
    public Transform bookParentRoot;

    private Dictionary<string, BoxCollider> shelfCollidersByName = new Dictionary<string, BoxCollider>();

    private void Awake()
    {
        // Ensure we have a unique, non-empty ID at runtime.
        if (string.IsNullOrWhiteSpace(ObjectID) || ObjectID == "NewGUID")
            ObjectID = System.Guid.NewGuid().ToString("N");

        GenerateShelfRegions();
    }

    // Optional: in-editor safety so duplicates get fixed when duplicating prefabs in-scene.
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(ObjectID) || ObjectID == "NewGUID")
            ObjectID = System.Guid.NewGuid().ToString("N");
    }
#endif

    public string GetID() => ObjectID;

    /// <summary>
    /// Set a specific ID (used by loader). Pass a non-empty unique value.
    /// </summary>
    public void SetID(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
            ObjectID = id;
        else
            Debug.LogWarning("[FreeformBookshelf] Attempted to SetID with null/empty value.");
    }

    private void GenerateShelfRegions()
    {
        shelfCollidersByName.Clear();

        foreach (var shelf in shelfAreas)
        {
            if (string.IsNullOrEmpty(shelf.name))
            {
                Debug.LogWarning("ShelfRegion has an empty name. Skipping.");
                continue;
            }

            var regionObj = new GameObject(shelf.name);
            regionObj.transform.SetParent(transform);
            regionObj.transform.localPosition = shelf.localPosition;
            regionObj.transform.localRotation = Quaternion.identity;
            regionObj.layer = LayerMask.NameToLayer("ShelfRegion");

            var collider = regionObj.AddComponent<BoxCollider>();
            collider.size = shelf.size;
            collider.isTrigger = true;

            shelf.regionCollider = collider;
            shelfCollidersByName[shelf.name] = collider;
        }
    }

    public List<BoxCollider> GetAllShelfRegions()
    {
        var regions = new List<BoxCollider>();
        foreach (var shelf in shelfAreas)
        {
            if (shelf.regionCollider != null)
                regions.Add(shelf.regionCollider);
        }
        return regions;
    }

    public BoxCollider GetRegionByName(string name)
    {
        shelfCollidersByName.TryGetValue(name, out var result);
        return result;
    }

    public IEnumerable<string> GetShelfNames() => shelfCollidersByName.Keys;
}
