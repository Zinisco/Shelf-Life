using UnityEngine;

public class BookInfo : MonoBehaviour
{
    public ShelfSpot currentSpot;

    public void SetShelfSpot(ShelfSpot spot)
    {
        currentSpot = spot;
    }

    public void ClearShelfSpot()
    {
        currentSpot = null;
    }
}
