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
        ghostBookManager.UpdateGhostBook(heldObject, shelfDetector.CurrentLookedAtShelfSpot);

        if (heldObject != null)
        {
            Debug.Log($"[Held Book] Parent: {heldObject.transform.parent}, Position: {heldObject.transform.position}");
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
            Debug.Log("Pick Up Object");
        }
        else
        {
            DropObject();
            Debug.Log("Drop Up Object");
        }
    }

    private void TryPickup()
    {
        // Reset current shelf spot when picking up a new book
        currentShelfSpot = null;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, pickupRange))
        {
            if (hit.collider.gameObject.CompareTag("Pickable"))
            {
                heldObject = hit.collider.gameObject;
                heldObjectRb = heldObject.GetComponent<Rigidbody>();

                BookInfo info = heldObject.GetComponent<BookInfo>();
                if (info != null && info.currentSpot != null)
                {
                    Debug.Log($"Clearing spot {info.currentSpot.name} from book {heldObject.name}");
                    info.currentSpot.SetOccupied(false);
                    info.ClearShelfSpot();
                }

                if (heldObjectRb != null)
                {
                    heldObjectRb.isKinematic = false;
                    heldObjectRb.interpolation = RigidbodyInterpolation.Interpolate;
                    heldObjectRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    heldObject.transform.position = holdPosition.position;
                    heldObject.transform.rotation = holdPosition.rotation * Quaternion.Euler(0, 180, 90);

                }

                // Ignore collision between player and held book
                Collider bookCollider = heldObject.GetComponent<Collider>();
                if (bookCollider != null && playerCollider != null)
                {
                    Physics.IgnoreCollision(bookCollider, playerCollider, true);
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

        // Re-enable collision with player
        Collider bookCol = heldObject.GetComponent<Collider>();
        if (bookCol != null && playerCollider != null)
        {
            Physics.IgnoreCollision(bookCol, playerCollider, false);
        }

        // Remove the joint if it exists
        if (holdJoint != null)
        {
            Destroy(holdJoint);
            holdJoint = null;
        }

        // Clear references
        heldObject = null;
        heldObjectRb = null;
    }


    public void TryShelveBook()
    {
        //Do not shelve books if holding nothing
        if (heldObject == null) return;

        // Remove any joints
        if (holdJoint != null)
        {
            Destroy(holdJoint);
            holdJoint = null;
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
            StartCoroutine(SmoothPlaceOnShelf(shelvedBook, targetSpot.transform));
            targetSpot.SetOccupied(true, shelvedBook);

            BookInfo newInfo = shelvedBook.GetComponent<BookInfo>();
            if (newInfo != null)
            {
                newInfo.SetShelfSpot(targetSpot);
            }

            // Final cleanup: remove joint, reset physics, etc.
            if (holdJoint != null)
            {
                Destroy(holdJoint);
                holdJoint = null;
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
            heldObject.transform.rotation = holdPosition.rotation * Quaternion.Euler(0, 180, 90);

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
            targetSpot.SetOccupied(true, heldObject);

            BookInfo newInfo = heldObject.GetComponent<BookInfo>();
            if (newInfo != null)
            {
                newInfo.SetShelfSpot(targetSpot); // or: newInfo.currentSpot = targetSpot;
            }

            StartCoroutine(SmoothPlaceOnShelf(heldObject, targetSpot.transform));
            heldObjectRb.isKinematic = true;
            heldObject.transform.SetParent(null);
            heldObject = null;
            heldObjectRb = null;
        }
    }

    IEnumerator SmoothPlaceOnShelf(GameObject book, Transform shelfSpot)
    {
        float duration = 0.5f;
        float elapsed = 0f;

        Vector3 startPosition = book.transform.position;
        Quaternion startRotation = book.transform.rotation;

        Vector3 targetPosition = shelfSpot.position;
        Quaternion baseRotation = Quaternion.LookRotation(-shelfSpot.right, Vector3.up);
        Quaternion targetRotation = baseRotation * Quaternion.Euler(0, 180, 90); // Rotate cover to correct orientation




        while (elapsed < duration)
        {
            float t = elapsed / duration;
            book.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            book.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        book.transform.position = targetPosition;
        book.transform.rotation = targetRotation;
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

}