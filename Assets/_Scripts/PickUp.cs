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

    [SerializeField] private TableSpotDetector tableSpotDetector; // Like ShelfDetector
    //[SerializeField] private float tableSnapRange = 1.5f;


    [Header("Game Input")]
    [SerializeField] private GameInput gameInput;

    [Header("Pick Up Object Settings")]
    [SerializeField] private float pickupRange = 4f;  // Max distance to pick up objects
    [SerializeField] private Transform holdPosition;  // Where the object will be held
    private GameObject heldObject;
    private Rigidbody heldObjectRb;

    [Header("Bookshelf Settings")]
    [SerializeField] private float shelfSnapRange = 1.5f; // How close the book needs to be to snap

    [Header("Table Stack Settings")]
    [SerializeField] private float tableStackOffset = 0.12f;

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
        tableSpotDetector.UpdateLookedAtTable();

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
            if (hit.collider.gameObject.CompareTag("Pickable") || hit.collider.gameObject.CompareTag("BookCrate"))
            {
                heldObject = hit.collider.gameObject;
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

                TableSpot tableSpot = heldObject.GetComponentInParent<TableSpot>();
                if (tableSpot != null)
                {
                    Debug.Log($"Removing book {heldObject.name} from table spot {tableSpot.name}");
                    tableSpot.RemoveBook(heldObject);
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

        // 1. Try to stack
        if (TryStackOnTable(bookInfo)) return;

        // 2. Try to shelve
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
            // First, try to stack on existing book
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Book"))
            {
                GameObject targetBook = hit.collider.gameObject;
                BookInfo targetInfo = targetBook.GetComponent<BookInfo>();
                BookInfo heldInfo = heldObject.GetComponent<BookInfo>();

                if (bookStackManager.CanStack(targetBook, heldObject) && targetBook.transform.childCount < 5)
                {
                    Vector3 stackPos = targetBook.transform.position + Vector3.up * 0.12f;
                    heldObject.transform.SetPositionAndRotation(stackPos, targetBook.transform.rotation);
                    heldObject.transform.SetParent(targetBook.transform);

                    Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;

                    heldObject.layer = LayerMask.NameToLayer("Book");
                    EnablePlayerCollision(heldObject);
                    ClearHeldBook();
                    ghostBookManager.HideGhost();
                    return;
                }
            }

            // Else, place on surface normally
            if (Vector3.Dot(hit.normal, Vector3.up) > 0.9f)
            {
                Vector3 point = hit.point + hit.normal * 0.07f;

                Quaternion baseRotation = Quaternion.Euler(-90f, 90f, 90f);
                Quaternion facingRotation = Quaternion.Euler(0f, currentYRotation, 0f);
                Quaternion finalRotation = facingRotation * baseRotation;

                Collider[] overlaps = Physics.OverlapBox(
                    point,
                    heldObject.GetComponent<Collider>().bounds.extents * 0.9f,
                    finalRotation,
                    LayerMask.GetMask("Book")
                );

                if (overlaps.Length > 0) return;

                heldObject.transform.SetPositionAndRotation(point, finalRotation);
                heldObject.transform.SetParent(hit.transform); // Attach to table
                Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                heldObject.layer = LayerMask.NameToLayer("Book");
                EnablePlayerCollision(heldObject);
                ClearHeldBook();
                ghostBookManager.HideGhost();
            }
        }
    }


    private bool TryStackOnTable(BookInfo bookInfo)
    {
        TableSpot stackSpot = tableSpotDetector.CurrentLookedAtTableSpot;

        if (stackSpot != null && stackSpot.CanStack(bookInfo))
        {
            stackSpot.StackBook(heldObject);
            bookInfo.ClearShelfSpot();
            EnablePlayerCollision(heldObject);
            ClearHeldBook();
            return true;
        }

        return false;
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


    private bool IsDropPositionSafe(Vector3 position)
    {
        if (heldObject == null) return false;

        Collider col = heldObject.GetComponent<Collider>();
        if (col == null) return false;

        Vector3 halfExtents = col.bounds.extents;
        Quaternion rotation = heldObject.transform.rotation; // Match book orientation

        LayerMask obstacleMask = LayerMask.GetMask("Default", "Bookshelf", "Walls", "Furniture");
        return !Physics.CheckBox(position, halfExtents, rotation, obstacleMask);
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


}