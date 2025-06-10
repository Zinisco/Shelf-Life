using UnityEngine;

public class MovableFurniture : MonoBehaviour
{
    [SerializeField] private GameObject ghostVisual;

    public bool CanMove()
    {
        return ghostVisual != null;
    }

    public GameObject GetGhostVisual()
    {
        return ghostVisual;
    }
}
