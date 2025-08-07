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
    [SerializeField] private LayerMask shelfSurfaceMask;

    [SerializeField] private GameObject holdVisualPrefab;
    private GameObject holdVisualInstance;


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
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private StackContext originalContext;
    private float originalYRotation;

    private bool isPlacementValid = false;

    void Update()
    {

        if (Keyboard.current.fKey.isPressed && !isNudging)
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
                        holdTimer = 0f;

                        // Spawn visual at target position
                        if (holdVisualPrefab != null)
                        {
                            if (holdVisualInstance != null) Destroy(holdVisualInstance);

                            holdVisualInstance = Instantiate(holdVisualPrefab, hitInfo.transform.position + Vector3.up * 0.2f, Quaternion.identity);
                        }
                    }

                    holdTimer += Time.deltaTime;

                    // Update ring UI
                    if (holdVisualInstance != null)
                    {
                        var ring = holdVisualInstance.GetComponentInChildren<UnityEngine.UI.Image>();
                        if (ring != null)
                            ring.fillAmount = holdTimer / holdTime;

                        // Optional: match target position in case player moves aim
                        holdVisualInstance.transform.position = hitInfo.transform.position + Vector3.up * 0.2f;
                    }

                    if (holdTimer >= holdTime)
                    {
                        BeginNudging();

                        if (holdVisualInstance != null)
                        {
                            Destroy(holdVisualInstance);
                            holdVisualInstance = null;
                        }
                    }
                }
            }
        }
        else if (!Keyboard.current.fKey.isPressed && !isNudging)
        {
            holdTimer = 0f;
            selectedStackRoot = null;

            if (holdVisualInstance != null)
            {
                Destroy(holdVisualInstance);
                holdVisualInstance = null;
            }
        }


        if (isNudging)
        {
            UpdateGhostFollow();
            HandleRotation();

            // lock rotation on shelf nudges
               if (originalContext == StackContext.Shelf)
               {
                currentYRotation = originalYRotation;
               }
                 
            UpdateGhostFollow();
            
             // only allow scroll-to-rotate on tables
            if (originalContext == StackContext.Table)
                HandleRotation();

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelNudging();

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

        originalPosition = selectedStackRoot.transform.position;
        originalContext = selectedStackRoot.context;
        originalRotation = selectedStackRoot.transform.rotation;
        originalYRotation = originalRotation.eulerAngles.y;


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

        ghostBookManager.ShowGhost(heldGhost);
    }

    void UpdateGhostFollow()
    {
        if (ghostBookManager == null || heldGhost == null) return;

        ghostBookManager.UpdateGhost(
     heldGhost,
     playerCamera,
     shelfSurfaceMask,
     tableSurfaceMask,
     ref currentYRotation
 );

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

        // Copy the ghost’s exact orientation (so X=270,Z=90 from shelf logic is preserved)
        Quaternion correctRotation = ghost.rotation;


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

        Vector3 finalPos = originalPosition;

       if (originalContext == StackContext.Table)
        {
            // standard table: drop anywhere on the tableSurfaceMask
            if (Physics.Raycast(ghost.position, Vector3.down, out var hit, 2f, tableSurfaceMask))
                finalPos = hit.point + Vector3.up * (0.5f * selectedStackRoot.bookThickness);
            else
                finalPos = ghost.position;
        }
        else // ShelfContext: slide only along the shelf’s local X axis
        {
            // compute how far the ghost moved along shelf right
            Vector3 shelfRight = selectedStackRoot.transform.right;
            Vector3 delta      = ghost.position - originalPosition;
            float distAlongX   = Vector3.Dot(delta, shelfRight);
            finalPos = originalPosition + shelfRight * distAlongX;
            // lock Y & Z to original shelf rail
            finalPos.y = originalPosition.y;
            finalPos.z = originalPosition.z;
        }

        selectedStackRoot.transform.SetPositionAndRotation(finalPos, correctRotation);
        selectedStackRoot.wasJustNudged = true;
        StartCoroutine(ClearNudgeFlag(selectedStackRoot));

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

    private void CancelNudging()
    {
        if (selectedStackRoot != null)
        {
            // Reset to original position and rotation
            selectedStackRoot.transform.SetPositionAndRotation(originalPosition, originalRotation);

            // Re-enable visuals
            if (originalRenderers != null)
            {
                foreach (Renderer rend in originalRenderers)
                {
                    if (rend != null)
                        rend.enabled = true;
                }
            }
        }

        if (holdVisualInstance != null)
        {
            Destroy(holdVisualInstance);
            holdVisualInstance = null;
        }

        holdTimer = 0f;
        showDebugBox = false;
        selectedStackRoot = null;
        heldGhost = null;
        ghostBookManager.HideGhost();
        isNudging = false;
        IsNudging = false;

        Debug.Log("Nudging canceled.");
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
