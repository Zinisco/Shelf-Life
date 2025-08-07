using System.Collections.Generic;
using UnityEngine;

public class FreeformBookshelf : MonoBehaviour
{
    [System.Serializable]
    public class ShelfArea
    {
        public string name = "Shelf";
        public Vector3 localPosition = Vector3.zero;
        public Vector3 size = new Vector3(2.1f, 3.6f, -0.13f);
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
        GenerateShelfRegions();
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

            GameObject regionObj = new GameObject(shelf.name); // Exact name for saving/loading
            regionObj.transform.SetParent(transform);
            regionObj.transform.localPosition = shelf.localPosition;
            regionObj.transform.localRotation = Quaternion.identity;
            regionObj.layer = LayerMask.NameToLayer("ShelfRegion");

            BoxCollider collider = regionObj.AddComponent<BoxCollider>();
            collider.size = shelf.size;
            collider.isTrigger = true;

            shelf.regionCollider = collider;
            shelfCollidersByName[shelf.name] = collider;
        }
    }

    public List<BoxCollider> GetAllShelfRegions()
    {
        List<BoxCollider> regions = new List<BoxCollider>();
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

    public IEnumerable<string> GetShelfNames()
    {
        return shelfCollidersByName.Keys;
    }
}
