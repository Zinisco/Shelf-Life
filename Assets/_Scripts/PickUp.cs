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
    //[SerializeField] private float bookshelfScanRange = 5f;
    [SerializeField] private float shelfSnapRange = 1.5f; // How close the book needs to be to snap


    private void Start()
    {
        gameInput.OnPickUpObjectAction += GameInput_OnPickUpObjectAction;
        gameInput.OnShelveObjectAction += GameInput_OnShelveObjectAction;

        ghostBookManager.Init();

    }

    void Update()
    {
        shelfDetector.UpdateLookedAtShelf();
        ghostBookManager.UpdateGhostBook(heldObject, shelfDetector.CurrentLookedAtShelfSpot);

        // Force book to stick to hand
        if (heldObject != null)
        {
            heldObject.transform.position = holdPosition.position;
            heldObject.transform.rotation = holdPosition.rotation * Quaternion.Euler(0, 180, 90); // adjust as needed
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
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, pickupRange))
        {
            if (hit.collider.gameObject.CompareTag("Pickable"))
            {
                heldObject = hit.collider.gameObject;
                heldObjectRb = heldObject.GetComponent<Rigidbody>();

                // Clear shelf spot from the BookInfo
                BookInfo info = heldObject.GetComponent<BookInfo>();
                if (info != null && info.currentSpot != null)
                {
                    Debug.Log($"Clearing spot {info.currentSpot.name} from book {heldObject.name}");
                    info.currentSpot.SetOccupied(false);
                    info.ClearShelfSpot();
                }

                if (heldObjectRb != null)
                {
                    heldObjectRb.isKinematic = true;
                }

                // Ignore collision between player and held book
                Collider bookCollider = heldObject.GetComponent<Collider>();
                if (bookCollider != null && playerCollider != null)
                {
                    Physics.IgnoreCollision(bookCollider, playerCollider, true);
                }

                heldObject.transform.SetParent(holdPosition, worldPositionStays: false); // localPosition stays consistent
                heldObject.transform.localPosition = Vector3.zero;
                heldObject.transform.localRotation = Quaternion.Euler(0, 180, 90);


            }
        }


    }



    void DropObject()
    {
        if (currentShelfSpot != null && heldObject != null)
        {
            if (currentShelfSpot.IsOccupiedBy(heldObject))
            {
                Debug.Log($"Clearing occupied spot {currentShelfSpot.gameObject.name} for book {heldObject.name} on drop");
                currentShelfSpot.SetOccupied(false);
                currentShelfSpot = null;
            }
            else
            {
                Debug.Log($"ShelfSpot {currentShelfSpot.gameObject.name} is not occupied by {heldObject?.name}");
            }
        }

        if (heldObjectRb)
        {
            heldObjectRb.isKinematic = false; // Re-enable physics
        }

        if (heldObject != null && playerCollider != null)
        {
            Collider bookCollider = heldObject.GetComponent<Collider>();
            if (bookCollider != null)
            {
                Physics.IgnoreCollision(bookCollider, playerCollider, false);
            }
        }


        heldObject.transform.SetParent(null);
        heldObject.transform.position = holdPosition.position + Camera.main.transform.forward * 0.5f;
        heldObject = null;
        heldObjectRb = null;
    }


    public void TryShelveBook()
    {
        if (heldObject == null) return;

        ShelfSpot targetSpot = null;

        // Prioritize the spot we're looking at if it's valid
        ShelfSpot lookedAtShelfSpot = shelfDetector.CurrentLookedAtShelfSpot;
        if (lookedAtShelfSpot != null && !lookedAtShelfSpot.IsOccupied())
        {
            targetSpot = lookedAtShelfSpot;
        }

        // Fallback: Find closest unoccupied shelf spot if look failed
        if (targetSpot == null)
        {
            Bookshelf[] bookshelves = FindObjectsOfType<Bookshelf>();
            float closestDistance = Mathf.Infinity;

            foreach (Bookshelf shelf in bookshelves)
            {
                foreach (ShelfSpot spot in shelf.GetShelfSpots())
                {
                    if (spot.IsOccupied()) continue;

                    float distance = Vector3.Distance(heldObject.transform.position, spot.transform.position);
                    if (distance < shelfSnapRange && distance < closestDistance)
                    {
                        closestDistance = distance;
                        targetSpot = spot;
                    }
                }
            }
        }

        // If we found a valid spot, shelve it
        if (targetSpot != null)
        {
            currentShelfSpot = targetSpot;
            shelvedBook = heldObject;
            targetSpot.SetOccupied(true, heldObject);

            StartCoroutine(SmoothPlaceOnShelf(heldObject, targetSpot.transform));
            heldObjectRb.isKinematic = true;
            heldObject.transform.SetParent(null);
            heldObject = null;
            heldObjectRb = null;
        }
        else
        {
            Debug.Log("No valid shelf spot nearby.");
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
}