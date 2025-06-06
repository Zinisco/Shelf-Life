using UnityEngine;

public class ShelfDetector : MonoBehaviour
{
    [SerializeField] private float shelfDetectionRange = 4f;
    [SerializeField] private LayerMask shelfSpotLayerMask;

    public ShelfSpot CurrentLookedAtShelfSpot { get; private set; }

    public void UpdateLookedAtShelf()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, shelfDetectionRange, shelfSpotLayerMask))
        {
            ShelfSpot spot = hit.collider.GetComponent<ShelfSpot>();
            if (spot != null)
            {
                if (CurrentLookedAtShelfSpot != spot)
                {
                    CurrentLookedAtShelfSpot = spot;
                    Debug.Log($"Looking at ShelfSpot: {spot.gameObject.name}");
                }
                return;
            }
        }

        if (CurrentLookedAtShelfSpot != null)
        {
            Debug.Log("No longer looking at a shelf spot");
            CurrentLookedAtShelfSpot = null;
        }
    }
}
