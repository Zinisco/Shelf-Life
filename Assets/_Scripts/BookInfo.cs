using UnityEngine;

public class BookInfo : MonoBehaviour
{
    public ShelfSpot currentSpot;
    public string Genre;
    public string Title;
    public string SpineTitle;
    public Color CoverColor;
    public Vector3 Position;
    public Quaternion Rotation;
    public string tempShelfSpotName;  // Only used during load

    public string ShelfID;
    public int SpotIndex;


    public void SetShelfSpot(ShelfSpot spot, string shelfID, int index)
    {
        currentSpot = spot;
        ShelfID = shelfID;
        SpotIndex = index;
    }

    public void ClearShelfSpot()
    {
        currentSpot = null;
        ShelfID = "";
        SpotIndex = -1;
    }
}
