using UnityEngine;
using static PlantMover;

public class MovablePlant : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject ghostVisual;

    public PlantType Type;

    private void Awake()
    {
        // Auto-find the ghost if not manually assigned
        if (ghostVisual == null)
        {
            Transform ghost = transform.Find("Ghost");
            if (ghost != null)
                ghostVisual = ghost.gameObject;
        }
    }

    public bool CanMove()
    {
        return ghostVisual != null;
    }

    public GameObject GetGhostVisual()
    {
        return ghostVisual;
    }
}
