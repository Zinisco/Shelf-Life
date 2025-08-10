using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private GameInput gameInput; // Custom input system
    [SerializeField] private Transform holdPosition; // Where the held book should appear

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
        pickableLayerMask = LayerMask.GetMask("Pickable", "Book");
    }

    private void Start()
    {
        // Subscribe to input events
        gameInput.OnPickUpObjectAction += GameInput_OnPickUpObjectAction;
        gameInput.OnShelveObjectAction += GameInput_OnShelveObjectAction;
        ghostBookManager.Init();

        // Ensure holdPosition has a kinematic Rigidbody
        if (!holdPosition.TryGetComponent(out holdRb))
        {
            holdRb = holdPosition.gameObject.AddComponent<Rigidbody>();
            holdRb.isKinematic = true;
        }
    }

    private void Update()
    {
        // Continuously update ghost placement while holding an object
        if (heldObject != null)
        {
            ghostBookManager.UpdateGhost(
                heldObject,
                playerCamera,
                shelfMask,
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
        TryShelveBook();
    }

    private void TryPickup()
    {
        // Cast a ray from the mouse to find a pickable object
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickableLayerMask))
        {
            // Validate tags
            if (!hit.collider.CompareTag("Pickable") && !hit.collider.CompareTag("BookCrate") && !hit.collider.CompareTag("Book")) return;

            GameObject baseBook = hit.collider.gameObject;
            heldObject = GetTopmostBook(baseBook); // Get topmost book in a stack
            
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

            // Prepare Rigidbody settings for held object
            if (heldObjectRb != null)
            {
                heldObjectRb.isKinematic = false;
                heldObjectRb.interpolation = RigidbodyInterpolation.Interpolate;
                heldObjectRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                heldObject.transform.position = holdPosition.position;

                // Face object away from the camera
                Quaternion rotation = Quaternion.LookRotation(Vector3.up, -Camera.main.transform.forward);
                heldObject.transform.rotation = rotation;
                heldObject.transform.Rotate(Vector3.right, 60f, Space.Self);
            }

            // Attach using a fixed joint
            holdJoint = holdPosition.gameObject.AddComponent<FixedJoint>();
            holdJoint.connectedBody = heldObjectRb;
            holdJoint.breakForce = Mathf.Infinity;
            holdJoint.breakTorque = Mathf.Infinity;
        }
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
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f, shelfMask))
        {
            Transform shelfRegionTransform = hit.transform;
            GameObject targetStackBook = ghostBookManager.GetStackTargetBook();
            Debug.Log($"[TryShelveBook] targetStackBook = {targetStackBook?.name ?? "null"}");
            BookInfo heldInfo = heldObject.GetComponent<BookInfo>();
            if (heldInfo == null) return;

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

                Vector3 preParentScale = heldObject.transform.localScale;

                heldObject.transform.SetPositionAndRotation(finalPos, finalRotation);
                heldObject.transform.SetParent(root.transform, worldPositionStays: true);
                heldObject.transform.localScale = preParentScale;

                FinalizeBookPlacement();

                return;
            }

            // Otherwise, place freely on the shelf
            Transform ghost = ghostBookManager.GhostBookInstance.transform;
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

            // before you parent:
            Vector3 originalLocalScale = heldObject.transform.localScale;

            // set its world position & rotation first
            heldObject.transform.SetPositionAndRotation(surfacePos, finalRot);

            // now parent it under the shelf
            heldObject.transform.SetParent(shelfRegionTransform, worldPositionStays: true);

            // restore its localScale so the world size stays exactly as before
            heldObject.transform.localScale = originalLocalScale;

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
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
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
            Transform ghost = ghostBookManager.GhostBookInstance.transform;
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

    private void FinalizeBookPlacement()
    {
        if (heldObject.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        heldObject.layer = LayerMask.NameToLayer("Book");
        EnablePlayerCollision(heldObject);
        ClearHeldBook();
        ghostBookManager.HideGhost();
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

    private void EnablePlayerCollision(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col != null && playerCollider != null)
            Physics.IgnoreCollision(col, playerCollider, false);
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

    public bool IsHoldingObject()
    {
        return heldObject != null;
    }
}
