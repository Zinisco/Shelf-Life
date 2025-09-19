using UnityEngine;

[CreateAssetMenu(menuName = "Design Item")]
public class DesignItem : ScriptableObject
{
    public enum Category { Furniture, Signage, Decor, Paint }
    public enum PlacementType { Floor, Table, Ceiling }


    public Category category;
    public string itemName;
    public Sprite itemImage;
    public GameObject itemPrefab; // 3D model to instantiate
    public GameObject cratePrefab;    // Unique crate for this design item
    public int price;

    [Header("Placement")]
    public PlacementType placement = PlacementType.Floor;
    public float verticalOffset = 0f; // fine-tuning per item

}