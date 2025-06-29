using UnityEngine;

public class GhostBookManager : MonoBehaviour
{
    [SerializeField] private GameObject ghostBookPrefab;
    private GameObject ghostBookInstance;

    public void Init()
    {
        ghostBookInstance = Instantiate(ghostBookPrefab);
        ghostBookInstance.SetActive(false);
        //Debug.Log("Ghost book initialized: " + ghostBookInstance.name);
    }

    public void UpdateGhost(GameObject heldObject, ShelfSpot shelfSpot, TableSpot tableSpot, float stackOffset)
    {
        if (heldObject == null)
        {
            if (ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(false);
            return;
        }

        // First priority: Shelf
        if (shelfSpot != null && shelfSpot.GetBookAnchor() != null)
        {
            Transform anchor = shelfSpot.GetBookAnchor();

            if (!ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(true);

            ghostBookInstance.transform.SetPositionAndRotation(
                anchor.position,
                anchor.rotation // original orientation for shelf
            );
            ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;
            return;
        }

        // Second priority: Table
        if (tableSpot != null && tableSpot.GetStackAnchor() != null)
        {
            if (!ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(true);

            Vector3 stackPosition = tableSpot.GetStackedPosition();
            Vector3 playerDir = Camera.main.transform.forward;
            playerDir.y = 0f;
            playerDir.Normalize();

            float angle = Mathf.Atan2(playerDir.x, playerDir.z) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 90f) * 90f;

            Quaternion baseRotation = Quaternion.Euler(-90f, 0f, 0f); // cover up
            Quaternion facingRotation = Quaternion.Euler(0f, snappedAngle, 0f);
            Quaternion finalRotation = facingRotation * baseRotation * Quaternion.Euler(0f, 90f, 180f);

            ghostBookInstance.transform.SetPositionAndRotation(stackPosition, finalRotation);
            ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;
            return;
        }

        // No valid placement
        if (ghostBookInstance.activeSelf)
            ghostBookInstance.SetActive(false);
    }



}
