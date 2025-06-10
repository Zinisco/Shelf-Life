using UnityEngine;

public class GhostBookManager : MonoBehaviour
{
    [SerializeField] private GameObject ghostBookPrefab;
    private GameObject ghostBookInstance;

    public void Init()
    {
        ghostBookInstance = Instantiate(ghostBookPrefab);
        ghostBookInstance.SetActive(false);
    }

    public void UpdateGhostBook(GameObject heldObject, ShelfSpot spot)
    {
        if (heldObject != null && spot != null && spot.GetBookAnchor() != null)
        {
            Transform anchor = spot.GetBookAnchor();

            if (!ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(true);

            // Position and rotation relative to anchor
            ghostBookInstance.transform.SetPositionAndRotation(
                anchor.position,
                anchor.rotation * Quaternion.Euler(0, 0, 0)
            );

            // Scale to match held book (adjust if book visual is child)
            ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;
        }
        else
        {
            if (ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(false);
        }
    }
}
