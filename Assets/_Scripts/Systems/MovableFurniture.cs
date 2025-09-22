using UnityEngine;

public class MovableFurniture : MonoBehaviour
{
    [Header("Visual Setup")]
    [SerializeField] private GameObject ghostVisual;    // The translucent placement preview
    [SerializeField] private Transform visualRoot;      // The real visual mesh (e.g. BookTable1, ShelfVisual, etc.)
    [SerializeField] private Transform contentsRoot;


    public bool CanMove()
    {
        return ghostVisual != null;
    }

    public GameObject GetGhostVisual()
    {
        return ghostVisual;
    }

    public Transform GetContentsRoot()
    {
        return contentsRoot != null ? contentsRoot : transform;
    }


    public Transform GetVisualRoot()
    {
        return visualRoot;
    }
}
