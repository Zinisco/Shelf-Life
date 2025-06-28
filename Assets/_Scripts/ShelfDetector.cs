using UnityEngine;

public class ShelfDetector : MonoBehaviour
{
    [SerializeField] private float shelfDetectionRange = 4f;
    [SerializeField] private LayerMask shelfSpotLayerMask;

    public ShelfSpot CurrentLookedAtShelfSpot { get; private set; }

    private float timeSinceLostLook = 0f;
    private float loseLookDelay = 0.2f;  // Delay before clearing looked at spot

    public void UpdateLookedAtShelf()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, shelfDetectionRange, shelfSpotLayerMask))
        {
            ShelfSpot spot = hit.collider.GetComponentInParent<ShelfSpot>();
            if (spot != null)
            {
                if (CurrentLookedAtShelfSpot != spot)
                {
                    CurrentLookedAtShelfSpot = spot;
                    //Debug.Log($"Looking at ShelfSpot: {spot.gameObject.name}");
                }
                timeSinceLostLook = 0f; // Reset timer when looking at a spot
                return;
            }
        }

        // If no hit, start counting time since last valid shelf spot
        if (CurrentLookedAtShelfSpot != null)
        {
            timeSinceLostLook += Time.deltaTime;
            if (timeSinceLostLook > loseLookDelay)
            {
                //Debug.Log("No longer looking at a shelf spot");
                CurrentLookedAtShelfSpot = null;
                timeSinceLostLook = 0f;
            }
        }
    }
}
