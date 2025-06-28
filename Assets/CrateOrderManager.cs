using UnityEngine;
using UnityEngine.InputSystem;

public class CrateOrderManager : MonoBehaviour
{
    [Tooltip("The crate prefab to spawn")]
    [SerializeField] private GameObject cratePrefab;
    [Tooltip("Where new crates should appear")]
    [SerializeField] private Transform crateSpawnPoint;

    private void Update()
    {
        // e.g. press C to order a new crate
        if (Keyboard.current.cKey.wasPressedThisFrame)
            OrderCrate();
    }

    public void OrderCrate()
    {
        Instantiate(
            cratePrefab,
            crateSpawnPoint.position,
            crateSpawnPoint.rotation
        );
    }
}
