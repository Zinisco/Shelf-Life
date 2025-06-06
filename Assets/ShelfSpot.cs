using UnityEngine;

public class ShelfSpot : MonoBehaviour
{
    private bool occupied = false;
    private GameObject occupyingBook = null;

    public bool IsOccupied()
    {
        return occupied;
    }

    public bool IsOccupiedBy(GameObject book)
    {
        return occupied && occupyingBook == book;
    }


    public void SetOccupied(bool value, GameObject book = null)
    {
        occupied = value;
        occupyingBook = value ? book : null;
        Debug.Log($"ShelfSpot '{gameObject.name}' occupied={occupied}, occupyingBook={occupyingBook?.name}");
    }

}
