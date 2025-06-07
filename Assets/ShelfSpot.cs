using UnityEngine;

public class ShelfSpot : MonoBehaviour
{
    private MeshRenderer spotRenderer;
    private Color occupiedColor = Color.red;
    private Color emptyColor = Color.green;


    private bool occupied = false;
    private GameObject occupyingBook = null;

    private void Awake()
    {
        spotRenderer = GetComponent<MeshRenderer>();
    }

    private void Start()
    {
        UpdateSpotColor();
    }

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

        UpdateSpotColor();
        Debug.Log($"ShelfSpot '{gameObject.name}' occupied={occupied}, occupyingBook={occupyingBook?.name}");
    }

    public GameObject GetOccupyingBook()
    {
        return occupyingBook;
    }

    private void UpdateSpotColor()
    {
        if (spotRenderer != null)
        {
            spotRenderer.material.color = occupied ? occupiedColor : emptyColor;
        }
    }

}
