using UnityEngine;

public class ShelfSpot : MonoBehaviour
{
    private bool occupied = false;
    private GameObject occupyingBook = null;

    [SerializeField] private Transform bookAnchor;
    [SerializeField] private MeshRenderer spotRenderer;

    private Color occupiedColor = Color.red;
    private Color emptyColor = Color.green;

    public int SpotIndex { get; private set; }

    public void SetIndex(int index)
    {
        SpotIndex = index;
    }

    public void InitializeFromVisual(GameObject visual)
    {
        // Look for the Anchor under visual
        Transform foundAnchor = visual.transform.Find("Anchor");
        if (foundAnchor != null)
        {
            bookAnchor = foundAnchor;
        }
        else
        {
            Debug.LogError($"ShelfSpot '{name}' could not find 'Anchor' under visual!");
        }

        // Assign MeshRenderer
        spotRenderer = visual.GetComponentInChildren<MeshRenderer>();
        if (spotRenderer == null)
        {
            Debug.LogWarning($"ShelfSpot '{name}' could not find MeshRenderer in visual prefab!");
        }

        UpdateSpotColor();
    }

    public void SetOccupied(bool value, GameObject book = null)
    {
        occupied = value;
        occupyingBook = value ? book : null;
        UpdateSpotColor();
    }

    public bool IsOccupied() => occupied;

    public bool IsOccupiedBy(GameObject book) => occupied && occupyingBook == book;

    public GameObject GetOccupyingBook() => occupyingBook;

    private void UpdateSpotColor()
    {
        if (spotRenderer != null)
        {
            spotRenderer.material.color = occupied ? occupiedColor : emptyColor;
        }
    }

    public void Occupy(BookInfo book)
    {
        occupied = true;
        occupyingBook = book.gameObject;

        string shelfID = transform.parent.GetComponent<Bookshelf>()?.GetID() ?? "UnknownShelf";
        book.SetShelfSpot(this, shelfID, SpotIndex);

        UpdateSpotColor();
    }



    public Transform GetBookAnchor() => bookAnchor;
}
