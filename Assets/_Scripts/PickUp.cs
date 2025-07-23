using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickUp : MonoBehaviour
{

    private ShelfSpot currentShelfSpot; // Tracks where the book is shelved
    private GameObject shelvedBook;

    [SerializeField] private LayerMask tableSurfaceMask;
    private float currentYRotation = 0f;


    [SerializeField] private Collider playerCollider;
    [SerializeField] private Camera playerCamera;

    [SerializeField] private ShelfDetector shelfDetector;
    [SerializeField] private GhostBookManager ghostBookManager;
    [SerializeField] private BookStackManager bookStackManager;


    [SerializeField] private LayerMask pickableLayerMask;
    private LayerMask heldObjectOriginalLayer;

    [Header("Game Input")]
    [SerializeField] private GameInput gameInput;

    [Header("Pick Up Object Settings")]
    [SerializeField] private float pickupRange = 4f;  // Max distance to pick up objects
    [SerializeField] private Transform holdPosition;  // Where the object will be held
    private GameObject heldObject;
    private Rigidbody heldObjectRb;

    [Header("Bookshelf Settings")]
    [SerializeField] private float shelfSnapRange = 1.5f; // How close the book needs to be to snap

    private FixedJoint holdJoint;
    private Rigidbody holdRb;

    private void Awake()
    {
        pickableLayerMask = LayerMask.GetMask("Pickable", "Book");
    }

    private void Start()
    {
        gameInput.OnPickUpObjectAction += GameInput_OnPickUpObjectAction;
        gameInput.OnShelveObjectAction += GameInput_OnShelveObjectAction;

        ghostBookManager.Init();

        // Ensure holdPosition has a Rigidbody (needed for FixedJoint)
        if (!holdPosition.TryGetComponent(out holdRb))
        {
            holdRb = holdPosition.gameObject.AddComponent<Rigidbody>();
            holdRb.isKinematic = true;
        }
    }

    void Update()
    {
        shelfDetector.UpdateLookedAtShelf();
   
        if (heldObject != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                currentYRotation += scroll > 0 ? 15f : -15f;
                currentYRotation = Mathf.Repeat(currentYRotation, 360f);
            }

            ghostBookManager.UpdateGhost(
             heldObject,
             shelfDetector.CurrentLookedAtShelfSpot,
             playerCamera,
            tableSurfaceMask,
            ref currentYRotation
             );
        }
    }


    private void GameInput_OnShelveObjectAction(object sender, System.EventArgs e)
    {
        TryShelveBook();
    }

    private void GameInput_OnPickUpObjectAction(object sender, System.EventArgs e)
    {
        if (heldObject == null)
        {
            TryPickup();
            //Debug.Log("Pick Up Object");
        }
        else
        {
            DropObject();
           //Debug.Log("Dropped Object");
        }
    }

    private void TryPickup()
    {
        // Reset current shelf spot when picking up a new book
        currentShelfSpot = null;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, pickupRange, pickableLayerMask))
        {
            if (hit.collider.gameObject.CompareTag("Pickable") || hit.collider.gameObject.CompareTag("BookCrate") || hit.collider.gameObject.CompareTag("Book"))
            {
                GameObject baseBook = hit.collider.gameObject;
                heldObject = GetTopmostBook(baseBook);

                heldObjectRb = heldObject.GetComponent<Rigidbody>();

                var crate = heldObject.GetComponent<BookCrate>();
                if (crate != null)
                {
                    crate.SetHeld(true);

                    Rigidbody crateRb = heldObject.GetComponent<Rigidbody>();
                    if (crateRb != null)
                        crateRb.constraints = RigidbodyConstraints.None; // unfreeze all while held
                }

                heldObjectOriginalLayer = heldObject.layer;
                heldObject.layer = LayerMask.NameToLayer("HeldObject");

                BookInfo info = heldObject.GetComponent<BookInfo>();
                if (info != null && info.currentSpot != null)
                {
                    Debug.Log($"Clearing spot {info.currentSpot.name} from book {heldObject.name}");
                    info.currentSpot.SetOccupied(false);
                    info.ClearShelfSpot();
                }

                if (info != null && info.currentStackRoot != null)
                {
                    BookStackRoot root = info.currentStackRoot;
                    info.currentStackRoot = null;

                    heldObject.transform.SetParent(null); // Unparent the book from the stack root

                    StartCoroutine(DelayedRemoveFromStack(root, heldObject));
                }



                if (heldObject == null)
                {
                    Debug.LogWarning("Picked up object was null after stack logic.");
                    return;
                }

                if (heldObjectRb != null)
                {
                    heldObjectRb.isKinematic = false;
                    heldObjectRb.interpolation = RigidbodyInterpolation.Interpolate;
                    heldObjectRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    heldObject.transform.position = holdPosition.position;
                    // Align book so cover faces player, spine left, top up
                    Vector3 forward = -Camera.main.transform.forward; // cover toward camera
                    Vector3 up = Vector3.up; // top up
                    heldObject.transform.rotation = Quaternion.LookRotation(forward, up);
                }

                // Attach FixedJoint
                holdJoint = holdPosition.gameObject.AddComponent<FixedJoint>();
                holdJoint.connectedBody = heldObjectRb;
                holdJoint.breakForce = Mathf.Infinity;
                holdJoint.breakTorque = Mathf.Infinity;
            }
        }
    }


    void DropObject()
    {
        if (heldObject == null) return;

        // Only clear shelf spot if the heldObject is really at that spot
        if (currentShelfSpot != null)
        {
            float distToSpot = Vector3.Distance(heldObject.transform.position, currentShelfSpot.transform.position);
            if (distToSpot < shelfSnapRange && currentShelfSpot.IsOccupiedBy(heldObject))
            {
                Debug.Log($"Clearing spot '{currentShelfSpot.name}' for book '{heldObject.name}' on drop (distance: {distToSpot})");
                currentShelfSpot.SetOccupied(false);
                currentShelfSpot = null;
            }
            else
            {
                Debug.Log($"NOT clearing shelf spot '{currentShelfSpot.name}' on drop. Book not near spot or not occupying.");
            }
        }

        // Always safely drop the book no matter what
        Vector3 dropPosition = FindSafeDropPosition();

        // Actually drop the book
        heldObject.transform.SetParent(null);
        heldObject.transform.position = dropPosition;

        if (heldObjectRb != null)
        {
            heldObjectRb.isKinematic = false;
            heldObjectRb.interpolation = RigidbodyInterpolation.Interpolate;
            heldObjectRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        heldObject.layer = heldObjectOriginalLayer;

        if (heldObject.CompareTag("BookCrate"))
        {
            // Force upright rotation (optional visual polish)
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

        // Remove the joint if it exists
        if (holdJoint != null)
        {
            Destroy(holdJoint);
            holdJoint = null;
        }

        var crate = heldObject.GetComponent<BookCrate>();
        if (crate != null) crate.SetHeld(false);

        // Clear references
        heldObject = null;
        heldObjectRb = null;

    }

    public void TryShelveBook()
    {
        if (heldObject == null)
            return;

        BookInfo bookInfo = heldObject.GetComponent<BookInfo>();
        if (bookInfo == null)
        {
            Debug.LogWarning("Held object has no BookInfo component.");
            return;
        }

        // Try to shelve
        ShelfSpot targetSpot = GetTargetShelfSpot();
        if (targetSpot != null)
        {
            GameObject occupyingBook = targetSpot.GetOccupyingBook();
            if (occupyingBook != null)
                SwapBooks(bookInfo, targetSpot, occupyingBook);
            else
                PlaceBookOnEmptyShelf(bookInfo, targetSpot);
            return;
        }

        // 3. Try placing on table surface
        TryPlaceOnSurface();
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

            // Check if we're stacking
            if (targetStackBook != null && bookStackManager.CanStack(targetStackBook, heldObject))
            {
                BookInfo targetInfo = targetStackBook.GetComponent<BookInfo>();
                BookStackRoot root = targetInfo.currentStackRoot;
                if (root == null)
                {
                    // Create new stack root at base book's position and rotation
                    GameObject rootObj = new GameObject("StackRoot");
                    rootObj.transform.SetPositionAndRotation(targetStackBook.transform.position, targetStackBook.transform.rotation);
                    rootObj.transform.parent = tableTransform;

                    root = rootObj.AddComponent<BookStackRoot>();
                    root.stackTitle = targetInfo.title;

                    // Parent base book to root and register it
                    targetStackBook.transform.SetParent(rootObj.transform);
                    root.AddBook(targetStackBook);
                    targetInfo.currentStackRoot = root;
                }


                if (root.GetCount() >= 4)
                {
                    Debug.Log("Stack limit reached.");
                    return;
                }

                root.AddBook(heldObject);
                heldInfo.currentStackRoot = root;


                Vector3 finalPos = targetStackBook.transform.position + Vector3.up * 0.12f;

                Quaternion finalRotation = targetStackBook.transform.rotation;

                heldObject.transform.SetPositionAndRotation(finalPos, finalRotation);
                heldObject.transform.SetParent(root.transform);

                FinalizeBookPlacement();
                return;
            }

            // Not stacking - place directly on surface
            Vector3 surfacePos = hit.point + Vector3.up * 0.07f;
            Quaternion baseRot = Quaternion.Euler(-90f, 90f, 90f);
            Quaternion faceRot = Quaternion.Euler(0f, currentYRotation, 0f);
            Quaternion finalRot = faceRot * baseRot;

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

    private ShelfSpot GetTargetShelfSpot()
    {
        ShelfSpot target = shelfDetector.CurrentLookedAtShelfSpot;

        if (target != null)
            return target;

        // Fallback: find nearest available shelf spot
        Bookshelf[] shelves = FindObjectsOfType<Bookshelf>();
        float closestDist = Mathf.Infinity;

        foreach (Bookshelf shelf in shelves)
        {
            foreach (ShelfSpot spot in shelf.GetShelfSpots())
            {
                float dist = Vector3.Distance(heldObject.transform.position, spot.transform.position);
                if (dist < shelfSnapRange && dist < closestDist)
                {
                    target = spot;
                    closestDist = dist;
                }
            }
        }

        return target;
    }

    private void SwapBooks(BookInfo heldInfo, ShelfSpot targetSpot, GameObject occupyingBook)
    {
        BookInfo occupyingInfo = occupyingBook.GetComponent<BookInfo>();
        if (occupyingInfo != null && occupyingInfo.currentSpot != null)
        {
            occupyingInfo.currentSpot.SetOccupied(false);
            occupyingInfo.ClearShelfSpot();
        }

        PrepareBookForPickup(occupyingBook);
        ShelveBookToSpot(heldObject, heldInfo, targetSpot);

        heldObject = occupyingBook;
        heldObjectRb = occupyingBook.GetComponent<Rigidbody>();
        AttachHeldBook();
    }

    private void PlaceBookOnEmptyShelf(BookInfo heldInfo, ShelfSpot targetSpot)
    {
        currentShelfSpot = targetSpot;
        ShelveBookToSpot(heldObject, heldInfo, targetSpot);
        ClearHeldBook();
    }


    IEnumerator SmoothPlaceOnShelf(GameObject book, Transform anchorTransform)
    {
        float duration = 0.5f;
        float elapsed = 0f;

        Vector3 startPosition = book.transform.position;
        Quaternion startRotation = book.transform.rotation;

        Vector3 targetPosition = anchorTransform.position;

        // Define the final desired local rotation when shelved
        Quaternion finalLocalRotation = Quaternion.Euler(0, 90, 0); // cover right, spine out

        // Get target world rotation by applying local rotation to anchor
        Quaternion targetRotation = anchorTransform.rotation * finalLocalRotation;

        // Disable collider during animation
        Collider bookCollider = book.GetComponent<Collider>();
        if (bookCollider != null) bookCollider.enabled = false;

        // Remove hold joint
        if (holdJoint != null)
        {
            Destroy(holdJoint);
            holdJoint = null;
        }

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            book.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            book.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap to final placement
        book.transform.position = targetPosition;
        book.transform.rotation = targetRotation;

        book.transform.SetParent(anchorTransform, false);
        book.transform.localPosition = Vector3.zero;
        book.transform.localRotation = finalLocalRotation;
        book.transform.localScale = Vector3.one;

        if (bookCollider != null) bookCollider.enabled = true;

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        ghostBookManager.HideGhost();

        //Debug.Log($"Final Book Position: {book.transform.position}, Local Scale: {book.transform.localScale}");
    }


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

            // Check if this position has room for the whole object
            bool blocked = Physics.CheckBox(testPos, halfExtents, rotation, obstacleMask);
            if (!blocked)
            {
                return testPos + up * 0.01f; // Slight offset to avoid ground clipping
            }
        }

        // As a last resort, drop directly at hand height but shifted slightly to side
        return origin + (forward * 0.3f) + (Camera.main.transform.right * 0.2f);
    }

    public bool IsHoldingObject()
    {
        return heldObject != null;
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


    private void ShelveBookToSpot(GameObject book, BookInfo info, ShelfSpot spot)
    {
        spot.Occupy(info);
        StartCoroutine(SmoothPlaceOnShelf(book, spot.GetBookAnchor()));
        book.layer = LayerMask.NameToLayer("Book");
    }

    private void PrepareBookForPickup(GameObject book)
    {
        book.transform.SetParent(null);
        book.transform.position = holdPosition.position;
        book.transform.rotation = Quaternion.LookRotation(-Camera.main.transform.forward, Vector3.up);

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        Collider col = book.GetComponent<Collider>();
        if (col != null && playerCollider != null)
            Physics.IgnoreCollision(col, playerCollider, true);
    }

    private void AttachHeldBook()
    {
        if (heldObjectRb == null)
            return;

        holdJoint = holdPosition.gameObject.AddComponent<FixedJoint>();
        holdJoint.connectedBody = heldObjectRb;
        holdJoint.breakForce = Mathf.Infinity;
        holdJoint.breakTorque = Mathf.Infinity;
    }

    private void EnablePlayerCollision(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col != null && playerCollider != null)
            Physics.IgnoreCollision(col, playerCollider, false);
    }

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

                string title = hit.GetComponent<BookInfo>()?.title;
                if (title == baseTitle)
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


    private int CountBooksInStack(GameObject baseBook)
    {
        int count = 1;
        float yOffset = 0.12f;
        float checkRadius = 0.08f;

        string baseTitle = baseBook.GetComponent<BookInfo>()?.title;
        if (string.IsNullOrEmpty(baseTitle)) return count;

        GameObject current = baseBook;

        for (int i = 0; i < 3; i++) // check 3 books above
        {
            Vector3 checkPos = current.transform.position + Vector3.up * yOffset;
            Collider[] hits = Physics.OverlapSphere(checkPos, checkRadius, LayerMask.GetMask("Book"));

            // Sort hits by Y height to prefer higher books
            System.Array.Sort(hits, (a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

            bool found = false;
            foreach (var hit in hits)
            {
                if (hit.gameObject == current) continue;

                string title = hit.GetComponent<BookInfo>()?.title;
                if (title == baseTitle)
                {
                    current = hit.gameObject;
                    count++;
                    found = true;
                    break;
                }
            }

            if (!found) break;
        }

        return count;
    }

    private void FinalizeBookPlacement()
    {
        Rigidbody rb = heldObject.GetComponent<Rigidbody>();
        if (rb != null)
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

    private IEnumerator DelayedRemoveFromStack(BookStackRoot root, GameObject book)
    {
        yield return new WaitForEndOfFrame(); // Safer than just `null`

        if (root != null)
        {
            root.RemoveBook(book);

            if (root.GetCount() == 0)
            {
                // Double-check book is not still in hand
                bool isStillHeld = heldObject == book;
                if (!isStillHeld)
                {
                    Destroy(root.gameObject);
                }
                else
                {
                    // Defer destruction one more frame if needed
                    StartCoroutine(DestroyRootLater(root));
                }
            }
        }
    }

    private IEnumerator DestroyRootLater(BookStackRoot root)
    {
        yield return new WaitForEndOfFrame();
        if (root != null) Destroy(root.gameObject);
    }



}