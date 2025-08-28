using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickUp : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask tableSurfaceMask; // Mask for detecting table surfaces
    [SerializeField] private LayerMask shelfMask; // Mask for detecting shelf regions
    [SerializeField] private Camera playerCamera; // Player's camera for raycasting
    [SerializeField] private Collider playerCollider; // Player's collider to avoid collision issues
    [SerializeField] private GhostBookManager ghostBookManager; // Manages the ghost book visual
    [SerializeField] private BookStackManager bookStackManager; // Manages stackability rules
    [SerializeField] private GhostBookDisplayManager ghostDisplayManager;
    [SerializeField] private GameInput gameInput; // Custom input system
    [SerializeField] private Transform holdPosition; // Where the held book should appear
    [SerializeField] private LayerMask bookDisplayMask;

    [Header("Pickup Settings")]
    [SerializeField] private float pickupRange = 4f; // Max pickup distance
    [SerializeField] private LayerMask pickableLayerMask; // Layers that can be picked up

    private GameObject heldObject; // Currently held object
    private Rigidbody heldObjectRb; // Rigidbody of held object
    private LayerMask heldObjectOriginalLayer; // Original layer of the held object
    private FixedJoint holdJoint; // Joint used to attach the object
    private Rigidbody holdRb; // Rigidbody for the hold position
    private float currentYRotation = 0f; // Rotation applied to ghost and object


    private void Awake()
    {
        // Ensure proper mask is set
        pickableLayerMask = LayerMask.GetMask("Pickable", "Book", "BookDisplay");
    }

    private void Start()
    {
        // Subscribe to input events
        gameInput.OnPickUpObjectAction += GameInput_OnPickUpObjectAction;
        gameInput.OnShelveObjectAction += GameInput_OnShelveObjectAction;
        ghostBookManager.Init();
        ghostDisplayManager.Init();

        // Ensure holdPosition has a kinematic Rigidbody
        if (!holdPosition.TryGetComponent(out holdRb))
        {
            holdRb = holdPosition.gameObject.AddComponent<Rigidbody>();
            holdRb.isKinematic = true;
        }
    }

    private void Update()
    {
        if (heldObject == null) return;

        if (heldObject.CompareTag("BookDisplay"))
        {
            ghostDisplayManager.UpdateGhost(heldObject, playerCamera, tableSurfaceMask);
        }
        else
        {
            ghostBookManager.UpdateGhost(
                heldObject,
                playerCamera,
                shelfMask,
                bookDisplayMask,
                tableSurfaceMask,
                ref currentYRotation
            );
        }
    }


    private void GameInput_OnPickUpObjectAction(object sender, System.EventArgs e)
    {
        if (heldObject == null)
        {
            if (NudgableStackMover.IsNudging) return;
            TryPickup();
        }
        else
        {
            DropObject();
        }
    }

    private void GameInput_OnShelveObjectAction(object sender, System.EventArgs e)
    {
        if (NudgableStackMover.IsNudging)
        {
            return; // let nudging system handle input
        }

        if (heldObject == null)
        {
            // New: Try to pick up from BookDisplay
            TryPickupBookDisplayBook();
            return;
        }

        TryShelveBook();
    }


    private void TryPickup()
    {
        // --- Pick a ray based on control scheme ---
        // KB&M: cursor ray. Gamepad: center-screen camera ray (with tiny aim assist).
        Ray ray;
        bool usingGamepad = gameInput != null && gameInput.IsGamepadActive;

        if (!usingGamepad && Mouse.current != null)
            ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        else
            ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        // First try a precise raycast on pickable layers
        RaycastHit hit;
        bool gotHit = Physics.Raycast(ray, out hit, pickupRange, pickableLayerMask);

        // If on gamepad and we missed, give a small sphere-assist along the same ray
        if (!gotHit && usingGamepad)
            gotHit = Physics.SphereCast(ray, 0.12f, out hit, pickupRange, pickableLayerMask);

        if (!gotHit) return;

        bool isBookDisplay = hit.collider.CompareTag("BookDisplay");

        if (isBookDisplay)
        {
            Transform anchor = hit.collider.transform.Find("BookAnchor");
            if (anchor != null)
            {
                BookInfo bookOnDisplay = anchor.GetComponentInChildren<BookInfo>();
                if (bookOnDisplay != null)
                {
                    Debug.Log("[TryPickup] Cannot pick up BookDisplay — it has a book on it.");
                    return;
                }
            }
        }


        if (!hit.collider.CompareTag("Pickable") &&
            !hit.collider.CompareTag("BookCrate") &&
            !hit.collider.CompareTag("Book") &&
            !isBookDisplay) return;


        GameObject baseBook = hit.collider.gameObject;

        if (!isBookDisplay &&
    baseBook.transform.parent != null &&
    baseBook.transform.parent.CompareTag("BookDisplay"))
        {
            Debug.Log("[TryPickup] Skipped pickup — book is on a BookDisplay.");
            return;
        }

        heldObject = GetTopmostBook(baseBook);

        // Detect if we're near a shelf when picking up
        Ray camRay = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        bool nearShelf = Physics.Raycast(camRay, 3f, shelfMask);

        bool nearShelfAndCoverFacingCamera = false;
        if (nearShelf)
        {
            Transform bookTransform = heldObject.transform;
            Vector3 bookCoverDirection = bookTransform.forward;  // +Z = cover
            Vector3 camToBook = (bookTransform.position - playerCamera.transform.position).normalized;
            float dot = Vector3.Dot(bookCoverDirection, camToBook);
            nearShelfAndCoverFacingCamera = dot > 0.5f;
        }

        // FORCE inward-facing default when grabbing from table near shelf
        currentYRotation = 0f;
        ghostBookManager.ResetRotation(true);

        heldObjectRb = heldObject.GetComponent<Rigidbody>();

        // Special handling for crates
        BookCrate crate = heldObject.GetComponent<BookCrate>();
        if (crate != null)
        {
            crate.SetHeld(true);
            Rigidbody crateRb = heldObject.GetComponent<Rigidbody>();
            if (crateRb != null)
                crateRb.constraints = RigidbodyConstraints.None;
        }

        // Change layer to avoid interaction with other colliders
        heldObjectOriginalLayer = heldObject.layer;
        heldObject.layer = LayerMask.NameToLayer("HeldObject");

        // Detach from stack if necessary
        BookInfo info = heldObject.GetComponent<BookInfo>();
        if (info != null && info.currentStackRoot != null)
        {
            BookStackRoot root = info.currentStackRoot;
            info.currentStackRoot = null;
            heldObject.transform.SetParent(null);
            StartCoroutine(DelayedRemoveFromStack(root, heldObject));
        }

        if (heldObjectRb == null)
        {
            Debug.LogError("Held object missing Rigidbody!");
            return;
        }

        // Prepare Rigidbody settings for held object
        if (heldObjectRb != null)
        {
            heldObjectRb.isKinematic = false;
            heldObjectRb.interpolation = RigidbodyInterpolation.Interpolate;
            heldObjectRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            heldObject.transform.position = holdPosition.position;

            Quaternion rotation = Quaternion.LookRotation(Vector3.up, -playerCamera.transform.forward);
            if (heldObject.CompareTag("BookDisplay"))
            {
                // Rotate 90° around Y so the display faces you
                heldObject.transform.rotation = rotation;
                heldObject.transform.Rotate(Vector3.right, 90f, Space.Self);
            }
            else
            {
                heldObject.transform.rotation = rotation;
                heldObject.transform.Rotate(Vector3.right, 60f, Space.Self);
            }

        }

        // Set ghost display held object only if this is a BookDisplay
        if (heldObject.CompareTag("BookDisplay"))
        {
            ghostDisplayManager.SetHeldObject(heldObject);
        }
        else
        {
            ghostDisplayManager.SetHeldObject(null); // disable ghost rotation / input
        }

        // Attach using a fixed joint
        holdJoint = holdPosition.gameObject.AddComponent<FixedJoint>();
        holdJoint.connectedBody = heldObjectRb;
        holdJoint.breakForce = Mathf.Infinity;
        holdJoint.breakTorque = Mathf.Infinity;

        Debug.Log($"Picked up: {heldObject.name} at {heldObject.transform.position}");

    }


    private void DropObject()
    {
        if (heldObject == null) return;

        Vector3 dropPosition = FindSafeDropPosition();
        heldObject.transform.SetParent(null);
        heldObject.transform.position = dropPosition;

        // Reset Rigidbody settings
        if (heldObjectRb != null)
        {
            heldObjectRb.isKinematic = false;
            heldObjectRb.interpolation = RigidbodyInterpolation.Interpolate;
            heldObjectRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        // Restore original layer
        heldObject.layer = heldObjectOriginalLayer;

        // Special handling for crates
        BookCrate crate = heldObject.GetComponent<BookCrate>();
        if (crate != null)
        {
            crate.SetHeld(false);
            Vector3 currentEuler = heldObject.transform.rotation.eulerAngles;
            heldObject.transform.rotation = Quaternion.Euler(0f, currentEuler.y, 0f);

            Rigidbody crateRb = heldObject.GetComponent<Rigidbody>();
            if (crateRb != null)
            {
                crateRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                crateRb.velocity = Vector3.zero;
                crateRb.angularVelocity = Vector3.zero;
            }
        }

        ghostDisplayManager.SetHeldObject(null);
        ghostDisplayManager.HideGhost();

        // Destroy joint
        if (holdJoint != null)
        {
            Destroy(holdJoint);
            holdJoint = null;
        }

        heldObject = null;
        heldObjectRb = null;
    }

    public void TryShelveBook()
    {
        // If you're currently nudging a stack, ignore Shelve (or forward it to confirm).
        if (NudgableStackMover.IsNudging)
        {
            Debug.Log("[PickUp] Shelve pressed while nudging — ignoring (nudging handles its own confirm).");
            return;
        }

        // Must be holding a book to shelve
        if (heldObject == null)
        {
            Debug.LogWarning("[PickUp] TryShelveBook called but heldObject is null.");
            return;
        }

        if (ghostBookManager == null)
        {
            Debug.LogError("[PickUp] GhostBookManager not assigned.");
            return;
        }

        BookInfo heldInfo = heldObject.GetComponent<BookInfo>();

        if (heldInfo == null)
        {
            if (heldObject.CompareTag("BookDisplay"))
            {
                Debug.Log("[PickUp] Shelving BookDisplay — redirecting to TryPlaceOnSurface.");
                TryPlaceOnSurface();
                return;
            }

            Debug.LogWarning("[PickUp] Held object has no BookInfo; cannot shelve.");
            return;
        }


        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f, shelfMask))
        {
            Transform shelfRegionTransform = hit.transform;
            GameObject targetStackBook = ghostBookManager.GetStackTargetBook();
            Debug.Log($"[TryShelveBook] targetStackBook = {targetStackBook?.name ?? "null"}");

            if (targetStackBook != null && bookStackManager.CanStack(targetStackBook, heldObject))
            {
                BookInfo targetInfo = targetStackBook.GetComponent<BookInfo>();
                BookStackRoot root = targetInfo.currentStackRoot;

                // Create a stack root if it doesn’t exist
                if (root == null)
                {
                    GameObject rootObj = new GameObject("StackRoot");
                    rootObj.transform.SetPositionAndRotation(targetStackBook.transform.position, targetStackBook.transform.rotation);
                    rootObj.transform.parent = shelfRegionTransform;

                    root = rootObj.AddComponent<BookStackRoot>();
                    root.stackTitle = targetInfo.title;
                    root.context = StackContext.Shelf;
                    targetStackBook.transform.SetParent(rootObj.transform);
                    root.AddBook(targetStackBook);
                    targetInfo.currentStackRoot = root;
                }

                if (root.GetCount() >= 4)
                {
                    Debug.Log("Stack limit reached.");
                    return;
                }

                // Add to stack
                root.AddBook(heldObject);
                heldInfo.currentStackRoot = root;

                // Stack in the direction the book is facing (outward from the back of the shelf)
                Vector3 stackDirection = targetStackBook.transform.up.normalized;
                Vector3 finalPos = targetStackBook.transform.position + stackDirection * 0.12f;
                Quaternion finalRotation = targetStackBook.transform.rotation;

                heldObject.transform.SetPositionAndRotation(finalPos, finalRotation);
                heldObject.transform.SetParent(root.transform, worldPositionStays: true);

                FinalizeBookPlacement();

                return;
            }

            // Otherwise, place freely on the shelf
            var ghostInst = ghostBookManager.GhostBookInstance;
            if (ghostInst == null)
            {
                Debug.LogWarning("[PickUp] Ghost instance is null; cannot free-place.");
                return;
            }

            Transform ghost = ghostInst.transform;
            Vector3 surfacePos = ghost.position;
            Quaternion finalRot = ghost.rotation;


            // Check for collision with other books
            BoxCollider bookCollider = heldObject.GetComponent<BoxCollider>();
            if (bookCollider != null)
            {
                Vector3 size = Vector3.Scale(bookCollider.size, heldObject.transform.lossyScale);
                Vector3 halfExtents = size * 0.5f;

                bool blocked = Physics.CheckBox(
                    surfacePos,
                    halfExtents,
                    finalRot,
                    LayerMask.GetMask("Book"),
                    QueryTriggerInteraction.Ignore
                );

                if (blocked)
                {
                    Debug.Log("Placement blocked: would overlap another book.");
                    return;
                }
            }

            // set its world position & rotation first
            heldObject.transform.SetPositionAndRotation(surfacePos, finalRot);

            // now parent it under the shelf
            heldObject.transform.SetParent(shelfRegionTransform, worldPositionStays: true);

            // finally finalize
            FinalizeBookPlacement();
        }
        else
        {
            // Attempt table placement as a fallback
            TryPlaceOnSurface();
        }
    }


    private void TryPlaceOnSurface()
    {
        if (heldObject == null)
        {
            Debug.LogWarning("[PickUp] TryPlaceOnSurface called but heldObject is null.");
            return;
        }
        if (ghostBookManager == null)
        {
            Debug.LogError("[PickUp] GhostBookManager not assigned.");
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (heldObject.CompareTag("BookDisplay"))
        {
            if (!ghostDisplayManager.IsValidPlacement())
            {
                Debug.Log("Invalid BookDisplay placement.");
                return;
            }

            Vector3 ghostPos = ghostDisplayManager.GetGhostPosition();
            Quaternion ghostRot = ghostDisplayManager.GetGhostRotation();

          
            if (Physics.Raycast(ray, out RaycastHit tableHit, 3f, tableSurfaceMask))
            {
                Transform tableTransform = tableHit.transform;

                heldObject.transform.SetPositionAndRotation(ghostPos, ghostRot);
                heldObject.transform.SetParent(tableTransform, worldPositionStays: true);

                Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None;
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                }

                heldObject.layer = LayerMask.NameToLayer("Book");
                EnablePlayerCollision(heldObject, ignore: true);
                FinalizeBookPlacement();
                return;
            }
        }

        if (Physics.Raycast(ray, out RaycastHit displayHit, 3f, bookDisplayMask))
        {
            Transform anchor = displayHit.transform.Find("BookAnchor");
            if (anchor != null)
            {
                // Check if a book already exists
                BookInfo existingBook = anchor.GetComponentsInChildren<BookInfo>()
    .FirstOrDefault(b => b.gameObject != heldObject);

                BookInfo heldInfo = heldObject.GetComponent<BookInfo>();

                if (existingBook != null && heldInfo != null)
                {
                    if (existingBook.title != heldInfo.title)
                    {
                        // SWAP order: place current held book, then pick up the one on the anchor

                        GameObject bookToSwap = heldObject;

                        // 1. Place the currently held book onto the anchor
                        Quaternion displayAnchorRotation = anchor.rotation * Quaternion.Euler(0f, 90f, 0f);
                        bookToSwap.transform.SetParent(anchor, worldPositionStays: true);
                        StartCoroutine(AnimateBookPlacement(bookToSwap, anchor, displayAnchorRotation));
                        FinalizeBookPlacement(); // clears heldObject


                        // 2. Detach and pick up the book that was already on the anchor
                        existingBook.transform.SetParent(null);
                        DropObject(existingBook.gameObject); // now becomes held

                        return;
                    }

                    else
                    {
                        Debug.Log("Titles match. No swap allowed.");
                        return; // Do nothing if same book
                    }
                }

                // Now place the held object on the anchor
                Quaternion anchorRotation = anchor.rotation * Quaternion.Euler(0f, 90f, 0f);
                heldObject.transform.SetPositionAndRotation(anchor.position, anchorRotation);
                heldObject.transform.SetParent(anchor, worldPositionStays: true);

                FinalizeBookPlacement();
                return;
            }
        }


        if (Physics.Raycast(ray, out RaycastHit hit, 3f, tableSurfaceMask))
        {
            Transform tableTransform = hit.transform;
            GameObject targetStackBook = ghostBookManager.GetStackTargetBook();
            BookInfo heldInfo = heldObject.GetComponent<BookInfo>();
            if (heldInfo == null) return;

            // Try to stack if valid
            if (targetStackBook != null && bookStackManager.CanStack(targetStackBook, heldObject))
            {
                BookInfo targetInfo = targetStackBook.GetComponent<BookInfo>();
                BookStackRoot root = targetInfo.currentStackRoot;

                // Create a stack root if it doesn’t exist
                if (root == null)
                {
                    GameObject rootObj = new GameObject("StackRoot");
                    rootObj.transform.SetPositionAndRotation(targetStackBook.transform.position, targetStackBook.transform.rotation);
                    rootObj.transform.parent = tableTransform;

                    root = rootObj.AddComponent<BookStackRoot>();
                    root.stackTitle = targetInfo.title;
                    root.context = StackContext.Table;
                    targetStackBook.transform.SetParent(rootObj.transform);
                    root.AddBook(targetStackBook);
                    targetInfo.currentStackRoot = root;
                }

                if (root.GetCount() >= 4)
                {
                    Debug.Log("Stack limit reached.");
                    return;
                }

                // Add to stack
                root.AddBook(heldObject);
                heldInfo.currentStackRoot = root;

                Vector3 finalPos = targetStackBook.transform.position + Vector3.up * 0.12f;
                Quaternion finalRotation = targetStackBook.transform.rotation;

                heldObject.transform.SetPositionAndRotation(finalPos, finalRotation);
                heldObject.transform.SetParent(root.transform);

                FinalizeBookPlacement();
                return;
            }

            // Otherwise, place freely on the surface
            var ghostInst = ghostBookManager.GhostBookInstance;
            if (ghostInst == null)
            {
                Debug.LogWarning("[PickUp] Ghost instance is null; cannot free-place.");
                return;
            }

            Transform ghost = ghostInst.transform;
            Vector3 surfacePos = ghost.position;
            Quaternion finalRot = ghost.rotation;


            // Check for collision with other books
            BoxCollider bookCollider = heldObject.GetComponent<BoxCollider>();
            if (bookCollider != null)
            {
                Vector3 size = Vector3.Scale(bookCollider.size, heldObject.transform.lossyScale);
                Vector3 halfExtents = size * 0.5f;

                bool blocked = Physics.CheckBox(
                    surfacePos,
                    halfExtents,
                    finalRot,
                    LayerMask.GetMask("Book"),
                    QueryTriggerInteraction.Ignore
                );

                if (blocked)
                {
                    Debug.Log("Placement blocked: would overlap another book.");
                    return;
                }
            }

            heldObject.transform.SetPositionAndRotation(surfacePos, finalRot);
            heldObject.transform.SetParent(tableTransform);
            FinalizeBookPlacement();
        }
    }

    private void DropObject(GameObject newHeld)
    {
        DropObject(); // Drops the current one

        heldObject = newHeld;
        heldObjectRb = heldObject.GetComponent<Rigidbody>();

        // Copy logic from TryPickup() to make it "held"
        heldObjectOriginalLayer = heldObject.layer;
        heldObject.layer = LayerMask.NameToLayer("HeldObject");

        if (heldObjectRb != null)
        {
            heldObjectRb.isKinematic = false;
            heldObjectRb.interpolation = RigidbodyInterpolation.Interpolate;
            heldObjectRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        heldObject.transform.position = holdPosition.position;
        Quaternion rotation = Quaternion.LookRotation(Vector3.up, -playerCamera.transform.forward);
        if (heldObject.CompareTag("BookDisplay"))
        {
            // Rotate 90° around Y so the display faces you
            rotation *= Quaternion.Euler(0f, 0f, 0f);
        }

        heldObject.transform.rotation = rotation;
        heldObject.transform.Rotate(Vector3.right, 60f, Space.Self);


        holdJoint = holdPosition.gameObject.AddComponent<FixedJoint>();
        holdJoint.connectedBody = heldObjectRb;
        holdJoint.breakForce = Mathf.Infinity;
        holdJoint.breakTorque = Mathf.Infinity;

        // Ensure ghost is reinitialized
        ghostBookManager.ResetRotation(true);
        ghostBookManager.UpdateGhost(
            heldObject,
            playerCamera,
            shelfMask,
            bookDisplayMask,
            tableSurfaceMask,
            ref currentYRotation
        );
    }

    private void FinalizeBookPlacement()
    {
        if (heldObject.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        heldObject.layer = LayerMask.NameToLayer("Book");
        EnablePlayerCollision(heldObject, ignore: true);
        ClearHeldBook();
        ghostBookManager.HideGhost();
        ghostDisplayManager.HideGhost();
        ghostDisplayManager.SetHeldObject(null);
        heldObject = null;
    }

    // Walks up the stack chain to get the topmost book matching title
    private GameObject GetTopmostBook(GameObject baseBook)
    {
        GameObject current = baseBook;
        float yOffset = 0.12f;
        string baseTitle = baseBook.GetComponent<BookInfo>()?.title;
        if (string.IsNullOrEmpty(baseTitle)) return current;

        for (int i = 0; i < 3; i++)
        {
            Vector3 checkPos = current.transform.position + Vector3.up * yOffset;
            Collider[] hits = Physics.OverlapSphere(checkPos, 0.05f, LayerMask.GetMask("Book"));
            bool found = false;

            foreach (var hit in hits)
            {
                if (hit.gameObject == current) continue;
                if (hit.GetComponent<BookInfo>()?.title == baseTitle)
                {
                    current = hit.gameObject;
                    found = true;
                    break;
                }
            }

            if (!found) break;
        }

        return current;
    }

    private void ClearHeldBook()
    {
        heldObject.layer = heldObjectOriginalLayer;
        heldObject = null;
        heldObjectRb = null;

        if (holdJoint != null)
        {
            Destroy(holdJoint);
            holdJoint = null;
        }
    }

    private void EnablePlayerCollision(GameObject obj, bool ignore)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col != null && playerCollider != null)
            Physics.IgnoreCollision(col, playerCollider, ignore);
    }


    // Attempts to find a safe place to drop the book using step increments
    private Vector3 FindSafeDropPosition()
    {
        if (heldObject == null) return holdPosition.position;

        Collider col = heldObject.GetComponent<Collider>();
        if (col == null) return holdPosition.position;

        Vector3 origin = holdPosition.position;
        Vector3 forward = Camera.main.transform.forward.normalized;
        Vector3 up = Vector3.up;
        Vector3 halfExtents = col.bounds.extents;
        Quaternion rotation = heldObject.transform.rotation;

        LayerMask obstacleMask = LayerMask.GetMask("Default", "Bookshelf", "Walls", "Furniture");
        float stepDistance = 0.05f;
        int steps = 30;

        for (int i = 0; i < steps; i++)
        {
            Vector3 testPos = origin + forward * (i * stepDistance);
            if (!Physics.CheckBox(testPos, halfExtents, rotation, obstacleMask))
            {
                return testPos + up * 0.01f;
            }
        }

        return origin + (forward * 0.3f) + (Camera.main.transform.right * 0.2f);
    }

    private void TryPickupBookDisplayBook()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f, bookDisplayMask))
        {
            Transform anchor = hit.transform.Find("BookAnchor");
            if (anchor == null) return;

            BookInfo bookToPickup = anchor.GetComponentInChildren<BookInfo>();
            if (bookToPickup == null) return;

            GameObject book = bookToPickup.gameObject;

            // Detach from display and pick it up
            book.transform.SetParent(null);
            DropObject(book); // reuses DropObject(GameObject) logic
        }
    }


    private IEnumerator DelayedRemoveFromStack(BookStackRoot root, GameObject book)
    {
        yield return new WaitForEndOfFrame();

        if (root != null)
        {
            root.RemoveBook(book);

            if (root.GetCount() == 0)
            {
                if (heldObject != book)
                    Destroy(root.gameObject);
                else
                    StartCoroutine(DestroyRootLater(root));
            }
        }
    }

    private IEnumerator DestroyRootLater(BookStackRoot root)
    {
        yield return new WaitForEndOfFrame();
        if (root != null) Destroy(root.gameObject);
    }

    private IEnumerator AnimateBookPlacement(GameObject book, Transform target, Quaternion rotation, float duration = 0.25f)
    {
        Vector3 startPos = book.transform.position;
        Quaternion startRot = book.transform.rotation;
        Vector3 endPos = target.position;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            book.transform.position = Vector3.Lerp(startPos, endPos, t);
            book.transform.rotation = Quaternion.Slerp(startRot, rotation, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        book.transform.position = endPos;
        book.transform.rotation = rotation;
    }


    public bool IsHoldingObject()
    {
        return heldObject != null;
    }
}
