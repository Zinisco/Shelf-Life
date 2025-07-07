using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickUp : MonoBehaviour
{

    private ShelfSpot currentShelfSpot; // Tracks where the book is shelved
    private GameObject shelvedBook;

    [SerializeField] private Collider playerCollider;

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

        ghostBookManager.UpdateGhost(
      heldObject,
      shelfDetector.CurrentLookedAtShelfSpot,
      tableSpotDetector.CurrentLookedAtTableSpot,
      tableStackOffset
  );

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

        heldObject.layer = LayerMask.NameToLayer("Pickable");

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
        {
            //Debug.Log("Tried to shelve, but no object is held.");
            return;
        }

        BookInfo bookInfo = heldObject.GetComponent<BookInfo>();
        if (bookInfo == null)
        {
            Debug.LogWarning("Held object has no BookInfo component.");
            return;
        }

        if (tableSpotDetector == null)
        {
            Debug.LogError("tableSpotDetector is not assigned!");
            return;
        }

        // Remove any joints
        if (holdJoint != null)
        {
            Destroy(holdJoint);
            holdJoint = null;
        }

        if (bookInfo != null)
        {
            TableSpot stackSpot = tableSpotDetector.CurrentLookedAtTableSpot;

            if (stackSpot != null && stackSpot.CanStack(bookInfo))
            {
                stackSpot.StackBook(heldObject);

                // Optional: update BookInfo if needed
                bookInfo.ClearShelfSpot();

                // Detach logic
                Collider bookCol = heldObject.GetComponent<Collider>();
                if (bookCol != null && playerCollider != null)
                {
                    Physics.IgnoreCollision(bookCol, playerCollider, false);
                }

                ClearHeldBook(); // <- move AFTER

                return;
            }
        }

        ShelfSpot targetSpot = shelfDetector.CurrentLookedAtShelfSpot;

        // No spot at all? Fallback to nearest valid spot
        if (targetSpot == null)
        {
            Bookshelf[] bookshelves = FindObjectsOfType<Bookshelf>();
            float closestDistance = Mathf.Infinity;

            foreach (Bookshelf shelf in bookshelves)
            {
                foreach (ShelfSpot spot in shelf.GetShelfSpots())
                {
                    float distance = Vector3.Distance(heldObject.transform.position, spot.transform.position);
                    if (distance < shelfSnapRange && distance < closestDistance)
                    {
                        closestDistance = distance;
                        targetSpot = spot;
                    }
                }
            }
        }

        if (targetSpot == null)
        {
            Debug.Log("No valid shelf spot found.");
            ClearHeldBook(); // Ensure we clean up properly
            return;
        }

        TableSpot targetTableSpot = tableSpotDetector.CurrentLookedAtTableSpot;

        if (targetSpot == null && targetTableSpot != null)
        {
            bookStackManager.TryStackBook(heldObject, targetTableSpot);
            ClearHeldBook();
            return;
        }


        GameObject occupyingBook = targetSpot.GetOccupyingBook();

        if (occupyingBook != null)
        {
            Debug.Log($"Swapping books: placing '{heldObject.name}' and picking up '{occupyingBook.name}'");

            // Detach occupying book and pick it up
            BookInfo info = occupyingBook.GetComponent<BookInfo>();
            if (info != null && info.currentSpot != null)
            {
                info.currentSpot.SetOccupied(false);
                info.ClearShelfSpot();
            }

            Rigidbody occupyingRb = occupyingBook.GetComponent<Rigidbody>();
            if (occupyingRb != null)
            {
                occupyingRb.isKinematic = true;

            }

            Collider occupyingCollider = occupyingBook.GetComponent<Collider>();
            if (occupyingCollider != null && playerCollider != null)
            {
                Physics.IgnoreCollision(occupyingCollider, playerCollider, true);
            }

            // Shelve the current held book
            shelvedBook = heldObject;
            shelvedBook.layer = LayerMask.NameToLayer("Pickable");

            StartCoroutine(SmoothPlaceOnShelf(heldObject, targetSpot.GetBookAnchor()));

            BookInfo newInfo = heldObject.GetComponent<BookInfo>();
            if (newInfo != null)
            {
                targetSpot.Occupy(newInfo);
            }


            if (heldObjectRb != null)
            {
                heldObjectRb.isKinematic = true;
                heldObjectRb.interpolation = RigidbodyInterpolation.None;
                heldObjectRb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            }

            Collider shelvedCol = shelvedBook.GetComponent<Collider>();
            if (shelvedCol != null && playerCollider != null)
            {
                Physics.IgnoreCollision(shelvedCol, playerCollider, false);
            }

            // Final cleanup for shelved book
            shelvedBook = null;
            heldObjectRb = null;

            // Don't assign currentShelfSpot to the swapped book
            // The new heldObject is not shelved

            // Now pick up the occupying book
            heldObject = occupyingBook;
            heldObjectRb = occupyingBook.GetComponent<Rigidbody>();


            if (heldObjectRb != null)
            {
                heldObjectRb.isKinematic = false;
                heldObjectRb.interpolation = RigidbodyInterpolation.Interpolate;
                heldObjectRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            }

            // Detach from any parent and set position/rotation
            heldObject.transform.SetParent(null);
            heldObject.transform.position = holdPosition.position;
            // Align book so cover faces player, spine left, top up
            Vector3 forward = -Camera.main.transform.forward; // cover toward camera
            Vector3 up = Vector3.up; // top up
            heldObject.transform.rotation = Quaternion.LookRotation(forward, up);

            // Re-ignore collision
            Collider occupyingCol = heldObject.GetComponent<Collider>();
            if (occupyingCol != null && playerCollider != null)
            {
                Physics.IgnoreCollision(occupyingCol, playerCollider, true);
            }

            // Recreate FixedJoint
            holdJoint = holdPosition.gameObject.AddComponent<FixedJoint>();
            holdJoint.connectedBody = heldObjectRb;
            holdJoint.breakForce = Mathf.Infinity;
            holdJoint.breakTorque = Mathf.Infinity;

        }
        else
        {
            // Normal shelving if spot is unoccupied
            currentShelfSpot = targetSpot;
            shelvedBook = heldObject;
            BookInfo newInfo = heldObject.GetComponent<BookInfo>();
            if (newInfo != null)
            {
                targetSpot.Occupy(newInfo);
            }


            StartCoroutine(SmoothPlaceOnShelf(heldObject, targetSpot.GetBookAnchor()));

            heldObject.layer = LayerMask.NameToLayer("Pickable");
            heldObject = null;
            heldObjectRb = null;
        }
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

        //Debug.Log($"Final Book Position: {book.transform.position}, Local Scale: {book.transform.localScale}");
    }



    private bool IsDropPositionSafe(Vector3 position)
    {
        if (heldObject == null) return false;

        Collider col = heldObject.GetComponent<Collider>();
        if (col == null) return false;

        Vector3 halfExtents = col.bounds.extents;
        Quaternion rotation = heldObject.transform.rotation; // Match book orientation

        LayerMask obstacleMask = LayerMask.GetMask("Default", "Bookshelf", "Walls");
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

        LayerMask obstacleMask = LayerMask.GetMask("Default", "Bookshelf", "Walls");

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


    private void OnDrawGizmos()
    {
        if (heldObject != null)
        {
            BoxCollider boxCol = heldObject.GetComponent<BoxCollider>();
            if (boxCol == null) return;

            Vector3 size = boxCol.size;            // local size of the collider
            Vector3 scale = heldObject.transform.lossyScale;  // global scale of the object

            Vector3 scaledSize = new Vector3(
                size.x * scale.x,
                size.y * scale.y,
                size.z * scale.z
            );

            // Save old Gizmos matrix
            Matrix4x4 oldGizmosMatrix = Gizmos.matrix;

            // Set Gizmos matrix to the book's position and rotation (no scale because we scale the size manually)
            Gizmos.matrix = Matrix4x4.TRS(heldObject.transform.position, heldObject.transform.rotation, Vector3.one);
            Gizmos.color = Color.red;

            // Draw wire cube centered at zero with scaled size
            Gizmos.DrawWireCube(Vector3.zero, scaledSize);

            // Restore Gizmos matrix
            Gizmos.matrix = oldGizmosMatrix;
        }
    }

    private void ClearHeldBook()
    {
        heldObject.layer = LayerMask.NameToLayer("Pickable");
        heldObject = null;
        heldObjectRb = null;

        if (holdJoint != null)
        {
            Destroy(holdJoint);
            holdJoint = null;
        }
    }


}