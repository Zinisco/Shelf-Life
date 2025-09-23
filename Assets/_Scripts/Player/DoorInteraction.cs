using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

public class DoorInteraction : MonoBehaviour
{
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string doorName; // Optional for debug
    private NavMeshObstacle obstacle;

    private bool isOpen = false;

    private void Awake()
    {
        // Grab the NavMeshObstacle on this door (make sure one is added in the Inspector)
        obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle != null)
        {
            obstacle.carving = true; // make sure carving is on
        }
    }

    private void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                if (hit.collider.gameObject == this.gameObject)
                {
                    ToggleDoor();
                }
            }
        }
    }

    private void ToggleDoor()
    {
        isOpen = !isOpen;
        doorAnimator.SetBool("Open", isOpen);

        if (obstacle != null)
        {
            obstacle.enabled = !isOpen; // Block path if closed, free path if open
        }

        Debug.Log($"[Door] {doorName} toggled to {(isOpen ? "Open" : "Closed")}");
    }
}
