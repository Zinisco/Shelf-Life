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
        if (heldObject != null && spot != null && !spot.IsOccupied())
        {
            ghostBookInstance.SetActive(true);
            ghostBookInstance.transform.position = spot.transform.position;

            Quaternion baseRotation = Quaternion.LookRotation(-spot.transform.right, Vector3.up);
            ghostBookInstance.transform.rotation = baseRotation * Quaternion.Euler(0, 180, 90);
        }
        else
        {
            ghostBookInstance.SetActive(false);
        }
    }
}
