using UnityEngine;
using UnityEngine.InputSystem;

public class NudgableStackMover : MonoBehaviour
{
    [SerializeField] private float holdTime = 1.5f;
    [SerializeField] private LayerMask bookLayer;
    [SerializeField] private GhostBookManager ghostBookManager;
    [SerializeField] private Camera playerCamera;

    private float holdTimer = 0f;
    private bool isNudging = false;
    private BookStackRoot selectedStackRoot;
    private GameObject heldGhost;
    private float currentYRotation = 0f;

    void Update()
    {
        if (Keyboard.current.nKey.isPressed && !isNudging)
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 3f, bookLayer))
            {
                BookInfo hitInfo = hit.collider.GetComponent<BookInfo>();
                if (hitInfo != null && hitInfo.currentStackRoot != null)
                {
                    if (selectedStackRoot == null || selectedStackRoot != hitInfo.currentStackRoot)
                    {
                        selectedStackRoot = hitInfo.currentStackRoot;
                        holdTimer = 0f; // reset if new target
                    }

                    holdTimer += Time.deltaTime;

                    if (holdTimer >= holdTime)
                    {
                        BeginNudging();
                    }
                }
            }
        }
        else if (!Keyboard.current.nKey.isPressed && !isNudging)
        {
            holdTimer = 0f;
            selectedStackRoot = null;
        }


        if (isNudging)
        {
            UpdateGhostFollow();
            HandleRotation();
            if (Mouse.current.leftButton.wasPressedThisFrame)
                ConfirmPlacement();
        }
    }

    void BeginNudging()
    {
        if (selectedStackRoot == null)
        {
            Debug.LogWarning("BeginNudging called but selectedStackRoot is null!");
            return;
        }

        isNudging = true;
        ghostBookManager.Init();

        if (selectedStackRoot.books == null || selectedStackRoot.books.Count == 0)
        {
            Debug.LogWarning("Selected stack root has no books.");
            return;
        }

        heldGhost = selectedStackRoot.books[0]; // used for ghost visuals only
        Debug.Log("Started nudging stack: " + selectedStackRoot.name);
    }


    void UpdateGhostFollow()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f))
        {
            Vector3 pos = hit.point + Vector3.up * 0.12f;
            Quaternion rot = Quaternion.Euler(0f, currentYRotation, 0f) * Quaternion.Euler(0f, 90f, 90f);
            ghostBookManager.UpdateGhost(heldGhost, null, playerCamera, ~0, ref currentYRotation);
            ghostBookManager.GhostBookInstance.transform.SetPositionAndRotation(pos, rot);
        }
    }

    void HandleRotation()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentYRotation += scroll > 0 ? 15f : -15f;
            currentYRotation = Mathf.Repeat(currentYRotation, 360f);
        }
    }

    public void ConfirmPlacement()
    {
        if (selectedStackRoot == null)
        {
            Debug.LogWarning("No stack selected to place.");
            return;
        }

        if (ghostBookManager == null || ghostBookManager.GhostBookInstance == null)
        {
            Debug.LogWarning("GhostBookManager or GhostBookInstance is null.");
            return;
        }

        Transform ghost = ghostBookManager.GhostBookInstance.transform;

        // Move the whole stack root
        selectedStackRoot.transform.SetPositionAndRotation(ghost.position, ghost.rotation);

        Debug.Log("Placed stack: " + selectedStackRoot.name);

        selectedStackRoot = null;
        heldGhost = null;
        ghostBookManager.HideGhost();
        isNudging = false;
    }
}
