using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class NudgableStackMover : MonoBehaviour
{
    [SerializeField] private float holdTime = 1.5f;
    [SerializeField] private LayerMask bookLayer;
    [SerializeField] private GhostBookManager ghostBookManager;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask tableSurfaceMask;

    public bool wasJustNudged = false;
    public static bool IsNudging = false;

    private float holdTimer = 0f;
    private bool isNudging = false;
    private BookStackRoot selectedStackRoot;
    private GameObject heldGhost;
    private float currentYRotation = 0f;
   
    private float rotationAmount = 0f;

    private Renderer[] originalRenderers;

    private Coroutine ghostRotationCoroutine;

    private Vector3 debugBoxCenter;
    private Vector3 debugBoxSize;
    private bool showDebugBox = false;
    private Quaternion debugBoxRotation = Quaternion.identity;

    private bool isPlacementValid = false;

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
           // Debug.Log("Nudging is true — calling UpdateGhostFollow()");
            UpdateGhostFollow();
            //Debug.Log("UpdateGhostFollow() called");
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

        if (selectedStackRoot == null) return;

        IsNudging = true;
        isNudging = true;
        // Temporarily move the stack high above the table to avoid blocking placement
        selectedStackRoot.transform.position += Vector3.up * 5f;

        if (selectedStackRoot.books == null || selectedStackRoot.books.Count == 0)
        {
            Debug.LogWarning("Selected stack root has no books.");
            return;
        }

        heldGhost = selectedStackRoot.books[0]; // used for ghost visuals only
       // Debug.Log("Started nudging stack: " + selectedStackRoot.name);

        // Disable renderers on the actual stack
        originalRenderers = selectedStackRoot.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in originalRenderers)
        {
            rend.enabled = false;
        }
    }

    void UpdateGhostFollow()
    {
        if (ghostBookManager == null || heldGhost == null) return;

        ghostBookManager.UpdateGhost(heldGhost, null, playerCamera, tableSurfaceMask, ref currentYRotation);

        // Real-time placement validation for stacks
        Transform ghost = ghostBookManager.GhostBookInstance.transform;
        BoxCollider referenceCollider = heldGhost.GetComponent<BoxCollider>();
        if (referenceCollider == null)
        {
            Debug.LogWarning("No BoxCollider found on heldGhost for placement check.");
            return;
        }

        Vector3 worldSize = Vector3.Scale(referenceCollider.size, heldGhost.transform.lossyScale);
        Vector3 boxSize = worldSize;

        Vector3 boxCenter = ghost.position + Vector3.up * (boxSize.y * 0.5f);

        Quaternion ghostRotation = ghost.rotation;
        debugBoxCenter = boxCenter;
        debugBoxSize = boxSize;
        debugBoxRotation = ghostRotation;
        showDebugBox = true;


        Collider[] overlaps = Physics.OverlapBox(
    boxCenter,
    boxSize / 2f,
    ghostRotation, //Use the ghost's actual rotation
    bookLayer,
    QueryTriggerInteraction.Ignore
);


        bool collision = false;
        foreach (var col in overlaps)
        {
            BookInfo info = col.GetComponent<BookInfo>();
            if (info != null && selectedStackRoot != null && !selectedStackRoot.books.Contains(info.gameObject))
            {
                collision = true;
                break;
            }
        }

        isPlacementValid = !collision;
        ghostBookManager.SetGhostMaterial(isPlacementValid);

    }


    void HandleRotation()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f) return;

        float angleStep = IsShiftHeld() ? 90f : 15f;

        if (scroll > 0)
            rotationAmount += angleStep;
        else if (scroll < 0)
            rotationAmount -= angleStep;

        rotationAmount %= 360f;

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

        if (!isPlacementValid)
        {
            Debug.LogWarning("Placement blocked — ghost was red.");
            return;
        }

        Transform ghost = ghostBookManager.GhostBookInstance.transform;

        // Extract Y rotation from ghost and apply proper stack alignment
        float yRotation = ghost.rotation.eulerAngles.y;
        Quaternion correctRotation = Quaternion.Euler(0f, yRotation, 0f);

        // If valid, continue with placement
        Ray downRay = new Ray(ghost.position, Vector3.down);
        if (Physics.Raycast(downRay, out RaycastHit surfaceHit, 2f, tableSurfaceMask))
        {
            Vector3 contactPoint = surfaceHit.point;
            float bookHeight = 0.12f;
            Vector3 adjustedPos = contactPoint + Vector3.up * (bookHeight * 0.5f);

            selectedStackRoot.transform.SetPositionAndRotation(adjustedPos, correctRotation);
            selectedStackRoot.wasJustNudged = true;
            StartCoroutine(ClearNudgeFlag(selectedStackRoot));
        }
        else
        {
            Debug.LogWarning("No surface detected below ghost. Placing at ghost position.");
            selectedStackRoot.transform.SetPositionAndRotation(ghost.position, correctRotation);
        }

        // Re-enable visuals
        if (originalRenderers != null)
        {
            foreach (Renderer rend in originalRenderers)
            {
                if (rend != null)
                    rend.enabled = true;
            }
        }

        Debug.Log("Placed stack: " + selectedStackRoot.name);

        showDebugBox = false;
        selectedStackRoot = null;
        heldGhost = null;
        ghostBookManager.HideGhost();
        isNudging = false;
        IsNudging = false;
    }


    public void GhostSetValidity(bool isValid)
    {
        ghostBookManager.SetGhostMaterial(isValid);
    }


    private IEnumerator ClearNudgeFlag(BookStackRoot root)
    {
        yield return new WaitForSeconds(0.5f); // enough time to avoid overlap issues
        if (root != null)
            root.wasJustNudged = false;
    }

    private bool IsShiftHeld()
    {
        return GameInput.Instance.IsPrecisionModifierHeld();
    }

    private void OnDrawGizmos()
    {
        if (!showDebugBox) return;

        Gizmos.color = Color.red;

        // Save original matrix and apply rotation + position
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(debugBoxCenter, debugBoxRotation, Vector3.one);
        Gizmos.matrix = rotationMatrix;

        Gizmos.DrawWireCube(Vector3.zero, debugBoxSize);
    }

}
