using UnityEngine;
using UnityEngine.InputSystem;

public class ComputerTerminal : MonoBehaviour
{
    [SerializeField] private ComputerUI computerUI;
    private bool playerInRange = false;
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
        if (playerInRange && computerUI != null)
        {
            Debug.Log("Interacted!");
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
}
