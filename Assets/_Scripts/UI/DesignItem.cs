using UnityEngine;

[CreateAssetMenu(menuName = "Design Item")]
public class DesignItem : ScriptableObject
{
    public enum Category { Furniture, Lighting, Decor, Paint }

    public Category category;
    public string itemName;
    public Sprite itemImage;
    public GameObject itemPrefab; // 3D model to instantiate
    public int price;
    [TextArea] public string description;
}