using UnityEngine;
using UnityEngine.InputSystem;

public class TableSpotDetector : MonoBehaviour
{
    [SerializeField] private float rayDistance = 3f;
    [SerializeField] private LayerMask tableLayerMask;

    public TableSpot CurrentLookedAtTableSpot { get; private set; }

    public void UpdateLookedAtTable()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayDistance, tableLayerMask))
        {
            CurrentLookedAtTableSpot = hit.collider.GetComponentInParent<TableSpot>();
            return;
        }

        CurrentLookedAtTableSpot = null;
    }
}

