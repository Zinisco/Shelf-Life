using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickUp : MonoBehaviour
{

    private ShelfSpot currentShelfSpot; // Tracks where the book is shelved
    private GameObject shelvedBook;

    [Header("Game Input")]
    [SerializeField] private GameInput gameInput;

    [Header("Pick Up Object Settings")]
    [SerializeField] private float pickupRange = 3f;  // Max distance to pick up objects
    [SerializeField] private Transform holdPosition;  // Where the object will be held
    private GameObject heldObject;
    private Rigidbody heldObjectRb;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationAmount = 90f; // Rotate by 90 degrees per click
    [SerializeField] private float rotationSpeed = 5f;   // Speed of the smooth rotation

    [Header("Bookshelf Settings")]
    //[SerializeField] private float bookshelfScanRange = 5f;
    [SerializeField] private float shelfSnapRange = 1.5f; // How close the book needs to be to snap

    private bool isRotating = false;

    private Quaternion targetRotation;

    private void Start()
    {
        gameInput.OnPickUpObjectAction += GameInput_OnPickUpObjectAction;
        gameInput.OnRotateObjectAction += GameInput_OnRotateObjectAction;
        gameInput.OnShelveObjectAction += GameInput_OnShelveObjectAction;

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

    private void GameInput_OnRotateObjectAction(object sender, System.EventArgs e)
    {
        if (heldObject != null)
        {
            if (!isRotating)
            {
                StartCoroutine(RotateObjectSmoothly(Quaternion.Euler(rotationAmount, 0, 0)));
            }
        }
    }

    void TryPickup()
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

                heldObject.transform.SetParent(holdPosition);
                heldObject.transform.localPosition = Vector3.zero;
                transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 90);
                heldObject.transform.LookAt(Camera.main.transform);
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

        heldObject.transform.SetParent(null);
        heldObject = null;
        heldObjectRb = null;
        isRotating = false;
    }


    public void TryShelveBook()
    {
        if (heldObject == null) return;

        Bookshelf[] bookshelves = FindObjectsOfType<Bookshelf>();
        ShelfSpot closestSpot = null;
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
                    closestSpot = spot;
                }
            }
        }

        if (closestSpot != null)
        {
            currentShelfSpot = closestSpot;
            shelvedBook = heldObject;
            closestSpot.SetOccupied(true, heldObject);

            BookInfo info = heldObject.GetComponent<BookInfo>();
            if (info != null)
            {
                info.SetShelfSpot(closestSpot);
            }

            StartCoroutine(SmoothPlaceOnShelf(heldObject, closestSpot.transform));
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

    IEnumerator RotateObjectSmoothly(Quaternion localRotation)
    {
        isRotating = true;
        Quaternion startRotation = heldObject.transform.localRotation;  // Use localRotation instead of world rotation
        Quaternion targetRotation = startRotation * localRotation;

        float elapsedTime = 0f;
        while (elapsedTime < 1f)
        {
            elapsedTime += Time.deltaTime * rotationSpeed;
            heldObject.transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime);  // Apply local rotation
            yield return null;
        }

        heldObject.transform.localRotation = targetRotation; // Ensure exact local rotation
        isRotating = false;
    }

}