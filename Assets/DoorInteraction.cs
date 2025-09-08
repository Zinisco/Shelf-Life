using UnityEngine;
using UnityEngine.InputSystem;

public class DoorInteraction : MonoBehaviour
{
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string doorName; // Optional for debug

    private bool isOpen = false;

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
        Debug.Log($"[Door] {doorName} toggled to {(isOpen ? "Open" : "Closed")}");
    }
}
