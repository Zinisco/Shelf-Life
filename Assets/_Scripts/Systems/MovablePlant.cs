using UnityEngine;
using static PlantMover;

public class MovablePlant : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject ghostVisual;

    [Header("Plant Info")]
    [SerializeField] private string plantID; // e.g. "Monstera", "Zanzibar", "Orchid", etc.
    public PlantType Type;

    private void Awake()
    {
        if (ghostVisual == null)
        {
            Transform ghost = transform.Find("Ghost");
            if (ghost != null)
                ghostVisual = ghost.gameObject;
        }
    }

    public bool CanMove() => ghostVisual != null;

    public GameObject GetGhostVisual() => ghostVisual;

    public string GetPlantID() => plantID;
    public void SetPlantID(string id) => plantID = id;
}
