using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class NudgableStackMover : MonoBehaviour
{
    [SerializeField] private float holdTime = 1.5f;
    [SerializeField] private LayerMask bookLayer;
    [SerializeField] private GhostBookManager ghostBookManager;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask tableSurfaceMask;
    [SerializeField] private LayerMask shelfSurfaceMask;

    [SerializeField] private GameObject holdVisualObject; // Assign this in the inspector
    [SerializeField] private UnityEngine.UI.Image holdVisualRing; // Assign the ring fill UI here
    [SerializeField] private float confirmCooldown = 0.25f; // seconds after entering nudging before confirm works
    private float confirmTimer;
    private bool canConfirm;


    public bool wasJustNudged = false;
    public static bool IsNudging = false;

    private float holdTimer = 0f;
    private bool isNudging = false;
    private BookStackRoot selectedStackRoot;
    private GameObject heldGhost;
    private float currentYRotation = 0f;
    private bool freeMoveHeld = false;

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

    private void OnEnable()
    {
        if (GameInput.Instance == null) return;
        GameInput.Instance.OnFreeMoveStarted += HandleFreeMoveStarted;
        GameInput.Instance.OnFreeMoveCanceled += HandleFreeMoveCanceled;
        GameInput.Instance.OnFreeMovePerformed += HandleFreeMovePerformed;

        // Optional: use existing actions for confirm/cancel + rotation on gamepad
        GameInput.Instance.OnPlaceFurnitureAction += HandleConfirm; // e.g. A / South
        GameInput.Instance.OnCancelMove += HandleCancel;  // e.g. B / East
        GameInput.Instance.OnRotateLeftAction += HandleRotateLeftStep;
        GameInput.Instance.OnRotateRightAction += HandleRotateRightStep;
    }

    private void OnDisable()
    {
        if (GameInput.Instance == null) return;
        GameInput.Instance.OnFreeMoveStarted -= HandleFreeMoveStarted;
        GameInput.Instance.OnFreeMoveCanceled -= HandleFreeMoveCanceled;
        GameInput.Instance.OnFreeMovePerformed -= HandleFreeMovePerformed;

        GameInput.Instance.OnPlaceFurnitureAction -= HandleConfirm;
        GameInput.Instance.OnCancelMove -= HandleCancel;
        GameInput.Instance.OnRotateLeftAction -= HandleRotateLeftStep;
        GameInput.Instance.OnRotateRightAction -= HandleRotateRightStep;
    }


    void Update()
    {

        // Hold-to-start using FreeMove
        if (freeMoveHeld && !isNudging)
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
                        if (holdVisualObject) holdVisualObject.SetActive(true);
                    }

                    holdTimer += Time.deltaTime;
                    if (holdVisualRing) holdVisualRing.fillAmount = Mathf.Clamp01(holdTimer / holdTime);

                    if (holdTimer >= holdTime)
                    {
                        BeginNudging();
                        if (holdVisualObject) holdVisualObject.SetActive(false);
                    }
                }
            }
        }
        else if (!freeMoveHeld && !isNudging)
        {
            holdTimer = 0f;
            selectedStackRoot = null; 
            if (holdVisualObject) holdVisualObject.SetActive(false);
        }


        if (isNudging)
        {
            UpdateGhostFollow();

            if (originalContext == StackContext.Shelf)
                currentYRotation = originalYRotation;
            else
                HandleRotation(); // still supports mouse scroll; gamepad uses RotateLeft/Right events
        }

        if (isNudging && !canConfirm)
        {
            confirmTimer -= Time.deltaTime;
            if (confirmTimer <= 0f) canConfirm = true;
        }
    }

    void BeginNudging()
    {
        if (selectedStackRoot == null)
        {
            Debug.LogWarning("BeginNudging called but selectedStackRoot is null!");
            return;
        }
        if (selectedStackRoot.books == null || selectedStackRoot.books.Count == 0)
        {
            Debug.LogWarning("Selected stack root has no books.");
            return;
        }

        originalPosition = selectedStackRoot.transform.position;
        originalContext = selectedStackRoot.context;
        originalRotation = selectedStackRoot.transform.rotation;
        originalYRotation = originalRotation.eulerAngles.y;

        // Enter nudging
        IsNudging = true;
        isNudging = true;

        // Stop hold logic and hide the ring immediately
        freeMoveHeld = false;
        holdTimer = 0f;
        confirmTimer = confirmCooldown;
        canConfirm = false; // disable confirming at the start

        if (holdVisualObject) holdVisualObject.SetActive(false);

        // Hide the real stack and show the ghost
        selectedStackRoot.transform.position += Vector3.up * 5f;

        heldGhost = selectedStackRoot.books[0];

        originalRenderers = selectedStackRoot.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in originalRenderers) rend.enabled = false;

        ghostBookManager.ShowGhost(heldGhost, selectedStackRoot.books.Count);  
    }

    void UpdateGhostFollow()
    {
        if (ghostBookManager == null || heldGhost == null || selectedStackRoot == null)
            return;

        ghostBookManager.UpdateGhost(
     heldGhost,
     playerCamera,
     shelfSurfaceMask,
     tableSurfaceMask,
     ref currentYRotation
 );
        Vector3 ghostBasePos = ghostBookManager.GhostBookInstance.transform.position;
        Quaternion ghostRot = ghostBookManager.GhostBookInstance.transform.rotation;
        ghostBookManager.UpdateGhostStackTransforms(ghostBasePos, ghostRot);

        // Real-time placement validation for stacks
        Transform ghost = ghostBookManager.GhostBookTopTransform;
        if (ghost == null) return; // Safety check
        BoxCollider referenceCollider = heldGhost.GetComponent<BoxCollider>();
        if (referenceCollider == null)
        {
            Debug.LogWarning("No BoxCollider found on heldGhost for placement check.");
            return;
        }

        float bookHeight = selectedStackRoot.bookThickness;
        int stackCount = selectedStackRoot.books.Count;

        Vector3 bookSize = Vector3.Scale(referenceCollider.size, heldGhost.transform.lossyScale);
        Vector3 stackSize = new Vector3(bookSize.x, bookHeight * stackCount, bookSize.z);

        Vector3 boxCenter = ghost.position + Vector3.up * (stackSize.y * 0.5f);
        Vector3 boxSize = stackSize;


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
        if (!canConfirm)
        {
            // Cooldown still active; ignore accidental confirms
            return;
        }

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

        Transform ghost = ghostBookManager.GhostBookTopTransform;

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

        if (holdVisualObject != null)
        {
            holdVisualObject.SetActive(false);
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

    private void HandleFreeMoveStarted()
    {
        if (isNudging)
        {
            // Second press confirms, but only after cooldown
            if (canConfirm) ConfirmPlacement();
            return;
        }

        // Begin hold-to-start
        freeMoveHeld = true;
        holdTimer = 0f;
        selectedStackRoot = null;
        if (holdVisualObject) holdVisualObject.SetActive(false);
    }

    private void HandleFreeMoveCanceled()
    {
        // This is the release of the key/button

        if (isNudging)
        {
            // While nudging, release should do nothing.
            // You’ll press again to place.
            return;
        }

        // Not nudging yet: cancel the hold progress / selection
        freeMoveHeld = false;
        holdTimer = 0f;
        selectedStackRoot = null;
        if (holdVisualObject) holdVisualObject.SetActive(false);
    }


    // If your FreeMove is a Vector2 (stick), you can read it here for optional nudging-offset usage later
    private Vector2 freeMoveVec = Vector2.zero;
    private void HandleFreeMovePerformed(InputAction.CallbackContext ctx)
    {
        if (ctx.control is StickControl || ctx.action.type == InputActionType.Value)
            freeMoveVec = ctx.ReadValue<Vector2>();
    }

    private void HandleConfirm(object _, EventArgs __) { if (isNudging) ConfirmPlacement(); }
    private void HandleCancel() { if (isNudging) CancelNudging(); }

    private void HandleRotateLeftStep(object _, EventArgs __) { if (isNudging && originalContext == StackContext.Table) rotationAmount = (rotationAmount - (GameInput.Instance.IsPrecisionModifierHeld() ? 15f : 90f)) % 360f; }
    private void HandleRotateRightStep(object _, EventArgs __) { if (isNudging && originalContext == StackContext.Table) rotationAmount = (rotationAmount + (GameInput.Instance.IsPrecisionModifierHeld() ? 15f : 90f)) % 360f; }


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
