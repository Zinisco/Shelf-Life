using UnityEngine;

public class SurfaceAnchor : MonoBehaviour
{
    [SerializeField] private string surfaceID;

    private void Awake()
    {
        if (string.IsNullOrEmpty(surfaceID))
            surfaceID = System.Guid.NewGuid().ToString();
    }

    public string GetID()
    {
        if (string.IsNullOrEmpty(surfaceID))
        {
            Debug.LogWarning($"SurfaceAnchor on {gameObject.name} had null ID! Generating one.");
            surfaceID = System.Guid.NewGuid().ToString();
        }
        return surfaceID;
    }

}
