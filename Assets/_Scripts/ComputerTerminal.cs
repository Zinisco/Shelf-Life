using UnityEngine;
using UnityEngine.InputSystem;

public class ComputerTerminal : MonoBehaviour
{
    [SerializeField] private ComputerUI computerUI;
    [SerializeField] private float lookThreshold = 0.95f;
    private bool playerInRange = false;
    private Transform playerCamera;
    private GameInput gameInput;

    private void Start()
    {
        gameInput = GameInput.Instance;
        if (gameInput != null)
            gameInput.OnInteractAction += OnInteract;

        if (computerUI != null)
            computerUI.ToggleUI(false);
    }

    private void OnDestroy()
    {
        if (gameInput != null)
            gameInput.OnInteractAction -= OnInteract;
    }

    private void OnInteract(object sender, System.EventArgs e)
    {
        if (playerInRange && playerCamera != null && IsPlayerLookingAtTerminal())
        {
            bool showUI = !computerUI.uiRoot.activeSelf;
            computerUI.ToggleUI(showUI);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && IsPlayerInFront(other.transform))
            playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = IsPlayerInFront(other.transform);
    }

    private bool IsPlayerInFront(Transform player)
    {
        Vector3 localForward = transform.TransformDirection(Vector3.forward);
        Vector3 toPlayer = (player.position - transform.position).normalized;
        float dot = Vector3.Dot(localForward, toPlayer);
        return dot > 0.5f;
    }

    private bool IsPlayerLookingAtTerminal()
    {
        if (playerCamera == null)
            return false;

        Vector3 toTerminal = (transform.position - playerCamera.position).normalized;
        float dot = Vector3.Dot(playerCamera.forward, toTerminal);

        return dot >= lookThreshold;
    }
}
