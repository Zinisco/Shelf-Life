using UnityEngine;

public class SidewalkPoint : MonoBehaviour
{
    [Tooltip("If true, customer can enter the store from here.")]
    public bool canEnterStore = false;

    [Tooltip("If true, reaching this point despawns the customer.")]
    public bool isDespawnPoint = false;
}