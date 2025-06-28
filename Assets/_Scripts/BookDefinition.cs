using UnityEngine;

[CreateAssetMenu(fileName = "BookDefinition", menuName = "Books/BookDefinition")]
public class BookDefinition : ScriptableObject
{
    public string bookID;
    public string title;
    public string genre;
    public Color color;
    public GameObject prefab;
    [TextArea] public string summary;
    
}
