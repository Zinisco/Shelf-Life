using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public class GhostBookManager : MonoBehaviour
{
    [SerializeField] private GameObject ghostBookPrefab; // Prefab for the translucent ghost book
    [SerializeField] private float surfaceOffset = 0.02f; // Offset above surface (table) for visual clarity
    [SerializeField] private float shelfOffset = 0.4f;    // Offset above shelf surface
    [SerializeField] private float rotationSmoothSpeed = 10f; // Speed of ghost book rotation interpolation

    [SerializeField] private Material validMaterial;   // Green material for valid placement
    [SerializeField] private Material invalidMaterial; // Red material for invalid placement
    [SerializeField] private Material defaultMaterial; // Default fallback material

    private List<GameObject> ghostStackBooks = new List<GameObject>();

    private Renderer ghostRenderer;           // Renderer used to update ghost material
    private GameObject ghostBookInstance;     // The actual ghost book instance
    private GameObject stackTargetBook;       // If stacking, the target book on the stack
    private bool rotationLocked = false;      // Whether the player has locked rotation

    private float rotationAmount = 0f;        // Desired Y-axis rotation
    private float currentRotation = 0f;       // Smoothly interpolated rotation

    private Vector3 latestGhostPosition;      // Last valid ghost position
    private Quaternion latestGhostRotation;   // Last valid ghost rotation
    private bool latestGhostValid = false;    // Whether the last placement is valid
    private Transform latestShelfTransform; // Shelf we hit last when placing

    // Called once to instantiate and initialize the ghost book
    public void Init()
    {
        if (ghostBookInstance != null)
            Destroy(ghostBookInstance);

        ghostBookInstance = Instantiate(ghostBookPrefab);
        ghostBookInstance.SetActive(false);

        ghostRenderer = ghostBookInstance.GetComponentInChildren<Renderer>();
        if (ghostRenderer == null)
            UnityEngine.Debug.LogWarning("GhostBookInstance has no Renderer!");

        UnityEngine.Debug.Log("GhostBookInstance created: " + ghostBookInstance.name);
    }

    // Continuously updates the ghost book position and rotation based on raycast from camera
    public void UpdateGhost(GameObject heldObject, Camera camera, LayerMask shelfMask, LayerMask tableMask, ref float currentRotationY)
    {
        // Hide ghost if no object is held
        if (heldObject == null)
        {
            if (ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(false);
            rotationLocked = false;
            return;
        }

        // Raycast from camera forward
        Ray ray = new Ray(camera.transform.position, camera.transform.forward);
        UnityEngine.Debug.DrawRay(ray.origin, ray.direction * 3f, Color.green, 0.1f);

        // Try placing on shelf first
        if (Physics.Raycast(ray, out RaycastHit shelfHit, 3f, shelfMask))
        {
            HandleShelfGhostPlacement(shelfHit, heldObject, camera, ref currentRotationY);
            return;
        }

        // If not on shelf, try table placement
        if (Physics.Raycast(ray, out RaycastHit hit, 3f, tableMask) &&
            Vector3.Dot(hit.normal, Vector3.up) > 0.9f)
        {
<<<<<<< HEAD
            ShowSingleGhost(heldObject);
=======

            UnityEngine.Debug.Log("Table hit: " + hit.collider.name + " | Layer: " + LayerMask.LayerToName(hit.collider.gameObject.layer));

            ShowSingleGhost(heldObject);
            UnityEngine.Debug.Log("GhostBookInstance Active: " + ghostBookInstance.activeSelf);

>>>>>>> origin/main

            if (!ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(true);

            Vector3 point = hit.point + hit.normal * surfaceOffset;

            float yRotation;
            if (rotationLocked)
            {
                yRotation = currentRotationY;
            }
            else
            {
                // Snap rotation to 90° based on camera forward
                Vector3 cameraForward = camera.transform.forward;
                cameraForward.y = 0f;
                cameraForward.Normalize();

                float angle = Mathf.Atan2(-cameraForward.x, -cameraForward.z) * Mathf.Rad2Deg;
                yRotation = Mathf.Round(angle / 90f) * 90f;
                currentRotationY = yRotation;
            }

            stackTargetBook = null;

            // Check if we’re placing on an existing stack
            if (!NudgableStackMover.IsNudging)
            {
                if (Physics.Raycast(ray, out RaycastHit stackHit, 3f, LayerMask.GetMask("Book")))
                {
                    GameObject hitBook = stackHit.collider.gameObject;
                    BookInfo hitInfo = hitBook.GetComponent<BookInfo>();
                    BookInfo heldInfo = heldObject.GetComponent<BookInfo>();

                    // Stack if titles match and not at max height
                    if (hitInfo != null && heldInfo != null && hitInfo.title == heldInfo.title)
                    {
                        BookStackRoot root = hitInfo.currentStackRoot;
                        GameObject topBook = hitBook;
                        if (root != null && root.books.Count > 0)
                            topBook = root.books[root.books.Count - 1];

                        int stackCount = root != null ? root.GetCount() : 1;
                        if (stackCount < 4)
                        {
                            SetGhostMaterial(true);
                            Vector3 topStackPos = topBook.transform.position + Vector3.up * 0.12f;
                            Quaternion finalRotation = topBook.transform.rotation;

                            ghostBookInstance.transform.SetPositionAndRotation(topStackPos, finalRotation);
                            ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;

                            stackTargetBook = topBook;
                            rotationLocked = false;
                            latestGhostPosition = topStackPos;
                            latestGhostRotation = finalRotation;
                            latestGhostValid = true;
                            return;
                        }
                    }
                }
            }

            if (heldObject != null && ghostBookInstance != null)
            {
                UnityEngine.Debug.Log("Trying to show ghost book on table.");
            }
            else
            {
                UnityEngine.Debug.LogWarning("Ghost not showing — missing heldObject or ghostBookInstance.");
            }


            // Handle rotation input via mouse scroll
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                currentRotationY += scroll > 0 ? 90f : -90f;
                currentRotationY = Mathf.Repeat(currentRotationY, 360f);
                rotationLocked = true;
            }

            // Smooth rotation interpolation
            float angleStep = 90f;
            if (scroll > 0) rotationAmount += angleStep;
            else if (scroll < 0) rotationAmount -= angleStep;

            rotationAmount %= 360f;

            currentRotation = Mathf.LerpAngle(currentRotation, rotationAmount, Time.deltaTime * rotationSmoothSpeed);
            Quaternion targetRot = Quaternion.Euler(0f, currentRotation, 0f);
            ghostBookInstance.transform.SetPositionAndRotation(point, targetRot);

            // Collision check to determine validity
            BoxCollider bookCollider = heldObject.GetComponent<BoxCollider>();
            if (bookCollider != null)
            {
                Vector3 size = Vector3.Scale(bookCollider.size, heldObject.transform.lossyScale);
                Vector3 halfExtents = size * 0.5f;

                bool blocked = Physics.CheckBox(
                    point,
                    halfExtents,
                    targetRot,
                    LayerMask.GetMask("Book"),
                    QueryTriggerInteraction.Ignore
                );

                SetGhostMaterial(!blocked);
                latestGhostValid = !blocked;
            }
            else
            {
                SetGhostMaterial(true);
                latestGhostValid = true;
            }

            latestGhostPosition = point;
            latestGhostRotation = targetRot;
            return;
        }
        else
        {
<<<<<<< HEAD
            // UnityEngine.Debug.Log("No table hit.");
=======
            UnityEngine.Debug.Log("No table hit.");
>>>>>>> origin/main
        }

        // Hide ghost if not targeting anything
        if (!NudgableStackMover.IsNudging)
        {
            if (ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(false);
        }

        rotationLocked = false;
        latestGhostValid = false;
    }

    // Helper method to check if a book's transform has the correct outward-facing orientation
    private bool IsBookOrientationValid(GameObject book, Vector3 shelfInward)
    {
        // Find the book parent renderer
        Transform bookTransform = book.GetComponent<Transform>();
        if (bookTransform == null)
        {
            UnityEngine.Debug.LogWarning($"Book {book.name} has no transform.");
            return false;
        }

        Vector3 eulerAngles = bookTransform.rotation.eulerAngles;

        // Normalize angles to [0, 360] for comparison
        float xAngle = Mathf.Repeat(eulerAngles.x, 360f);
        float yAngle = Mathf.Repeat(eulerAngles.y, 360f);
        float zAngle = Mathf.Repeat(eulerAngles.z, 360f);

        // Check if angles are CORRECT
        const float angleTolerance = 5f;
        bool isRotationValid = Mathf.Abs(xAngle - -90f) < angleTolerance &&
                              Mathf.Abs(yAngle - 0f) < angleTolerance &&
                              Mathf.Abs(zAngle - 0f) < angleTolerance;

        if (!isRotationValid)
        {
            UnityEngine.Debug.Log($"Book {book.name} orientation invalid: X={xAngle}, Y={yAngle}, Z={zAngle}");
            return false;
        }

        // Verify that the book's cover (local Y-axis) faces outward (opposite to shelfInward)
        Vector3 bookCoverDirection = bookTransform.TransformDirection(Vector3.up); // Local Y-axis (cover)
        Vector3 outwardDirection = -shelfInward.normalized;
        float dot = Vector3.Dot(bookCoverDirection.normalized, outwardDirection);
        const float directionTolerance = 0.95f; // Cosine of ~18 degrees
        bool isCoverFacingOutward = dot > directionTolerance;

        if (!isCoverFacingOutward)
        {
            UnityEngine.Debug.Log($"Book {book.name} cover not facing outward. Dot product: {dot}");
        }

        return isRotationValid && isCoverFacingOutward;
    }

    // Handles ghost placement on a shelf
    private void HandleShelfGhostPlacement(RaycastHit hit, GameObject heldObject, Camera camera, ref float currentRotationY)
    {
        stackTargetBook = null;

        // Always reset stackTargetBook when nudging
        if (NudgableStackMover.IsNudging)
        {
            stackTargetBook = null;
        }

        if (!ghostBookInstance.activeSelf)
            ghostBookInstance.SetActive(true);

        BoxCollider shelfCollider = hit.collider as BoxCollider;
        if (shelfCollider == null)
        {
            UnityEngine.Debug.LogWarning("Shelf hit did not have a BoxCollider.");
            return;
        }

        BoxCollider heldCollider = heldObject.GetComponent<BoxCollider>();
        float heldBookDepth = 0.12f;
        if (heldCollider != null)
            heldBookDepth = heldCollider.size.z * heldObject.transform.lossyScale.z;

        Bounds bounds = shelfCollider.bounds;
        Vector3 point = hit.point;
        latestShelfTransform = hit.collider.transform;

        // Handle scroll rotation input
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentRotationY += scroll > 0 ? 90f : -90f;
            currentRotationY = Mathf.Repeat(currentRotationY, 360f);
            rotationLocked = true;
        }

        float angleStep = 90f;
        if (scroll > 0) rotationAmount += angleStep;
        else if (scroll < 0) rotationAmount -= angleStep;

        rotationAmount %= 360f;

        // Figure out “inward” direction of the shelf
        Vector3 shelfInward = hit.normal;

        if (!NudgableStackMover.IsNudging)
        {
            // Cast a short ray from just in front of the ghost back into the shelf
            Ray stackRay = new Ray(point + shelfInward * 0.01f, -shelfInward);
            float rayLength = heldBookDepth + 0.02f;  // Just a hair longer than one book
            if (Physics.Raycast(stackRay, out RaycastHit bookHit, rayLength, LayerMask.GetMask("Book")))
            {
                var info = bookHit.collider.GetComponent<BookInfo>();
                if (info != null && info.title.Equals(heldObject.GetComponent<BookInfo>().title, System.StringComparison.OrdinalIgnoreCase))
                {
                    // Check if the book's orientation is valid (cover facing outward)
                    if (IsBookOrientationValid(bookHit.collider.gameObject, shelfInward))
                    {
                        stackTargetBook = bookHit.collider.gameObject;
                        UnityEngine.Debug.Log($"[Ghost] stacking onto: {stackTargetBook.name}");
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"[Ghost] cannot stack onto {bookHit.collider.gameObject.name}: invalid orientation");
                    }
                }
            }
        }

        // Clamp ghost position inside shelf bounds
        Vector3 localPoint = shelfCollider.transform.InverseTransformPoint(point);
        Vector3 halfSize = shelfCollider.size * 0.5f;

        if (currentRotationY == 0 || currentRotationY == 180)
        {
            localPoint.x = Mathf.Clamp(localPoint.x, -halfSize.x + 0.2f, halfSize.x - 0.2f);
            localPoint.z = Mathf.Clamp(localPoint.z, -halfSize.z + 0.47f, halfSize.z - 0.47f);
        }
        else
        {
            localPoint.x = Mathf.Clamp(localPoint.x, -halfSize.x + 0.05f, halfSize.x - 0.05f);
            localPoint.z = Mathf.Clamp(localPoint.z, -halfSize.z + 0.3f, halfSize.z - 0.3f);
        }

        point = shelfCollider.transform.TransformPoint(localPoint);
        point.y = bounds.min.y + shelfOffset;

        // Apply rotation and position
        currentRotation = Mathf.LerpAngle(currentRotation, rotationAmount, Time.deltaTime * rotationSmoothSpeed);
        // SHELF placement
        var inward = hit.normal.normalized;                     // Into the shelf
        var shelfRight = Vector3.Cross(Vector3.forward, inward).normalized; // +X of shelf region
        // Map model axes: +Z(top) -> Up, +Y(cover) -> shelfRight, => -X(spine) -> outward
        Quaternion orientShelf = Quaternion.LookRotation(Vector3.up, shelfRight);

        // Spin smoothly around vertical
        Quaternion spinY = Quaternion.AngleAxis(currentRotation, Vector3.up);

        // Final rotation
        Quaternion targetRot = spinY * orientShelf;
        ghostBookInstance.transform.SetPositionAndRotation(point, targetRot);

        ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;

        // Skip stacking logic entirely if nudging
        if (NudgableStackMover.IsNudging)
        {
            goto FREE_PLACEMENT;
        }

        // TITLE MATCH CHECK
        BookInfo heldInfo = heldObject.GetComponent<BookInfo>();
        if (heldInfo != null)
        {
            // How far to sample? Half the book depth
            float sampleDist = heldBookDepth * 0.5f;

            // Start just slightly inside the ghost
            Vector3 samplePos = point + shelfInward * sampleDist;

            // Look for any book colliders right against that face
            Collider[] hits = Physics.OverlapSphere(
                samplePos,
                0.05f,                      // Small radius
                LayerMask.GetMask("Book"),
                QueryTriggerInteraction.Ignore
            );

            foreach (var c in hits)
            {
                var info = c.GetComponent<BookInfo>();
                if (info != null && TitlesMatch(info.title, heldInfo.title))
                {
                    // Check if the book's orientation is valid (cover facing outward)
                    if (IsBookOrientationValid(c.gameObject, shelfInward))
                    {
                        stackTargetBook = c.gameObject;
                        UnityEngine.Debug.Log($"[Ghost] stacking onto: {stackTargetBook.name}");
                        break;
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"[Ghost] cannot stack onto {c.gameObject.name}: invalid orientation");
                    }
                }
            }

            if (stackTargetBook != null)
            {
                var root = stackTargetBook.GetComponent<BookInfo>().currentStackRoot;
                if (root != null && root.CanStack(heldObject.GetComponent<BookInfo>().title))
                {
                    // Snap to the next slot along the root’s local up
                    Vector3 nextSlot = root.TopPosition;
                    Quaternion rot = stackTargetBook.transform.rotation;

                    ghostBookInstance.transform.SetPositionAndRotation(nextSlot, rot);
                    ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;

                    SetGhostMaterial(true);
                    latestGhostValid = true;

                    // So TryShelveBook knows exactly which book to attach to
                    stackTargetBook = root.books.Last();
                    return;
                }
            }
        }

    FREE_PLACEMENT:

        // Collision check
        BoxCollider bookCollider = heldObject.GetComponent<BoxCollider>();
        bool blocked = false;

        if (bookCollider != null)
        {
            Vector3 size = Vector3.Scale(bookCollider.size, heldObject.transform.lossyScale);
            Vector3 halfExtents = size * 0.5f;

            blocked = Physics.CheckBox(
                point,
                halfExtents,
                targetRot,
                LayerMask.GetMask("Book"),
                QueryTriggerInteraction.Ignore
            );
        }

        // … after you’ve determined stackTargetBook and your blocked flag …

        bool validPlacement;

        // 1) If this book already has a StackRoot, snap via the root:
        if (stackTargetBook != null)
        {
            var root = stackTargetBook.GetComponent<BookInfo>().currentStackRoot;
            if (root != null && root.CanStack(heldObject.GetComponent<BookInfo>().title))
            {
                // Real stack case
                ghostBookInstance.transform.SetPositionAndRotation(
                    root.TopPosition,
                    stackTargetBook.transform.rotation
                );
                ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;
                SetGhostMaterial(true);
                latestGhostValid = true;

                // Tell TryShelveBook to attach to the last book in that root
                stackTargetBook = root.books.Last();
                return;
            }
        }

        // 2) Otherwise if it’s a matching bare book with no root yet, treat as 1-high stack:
        if (stackTargetBook != null && stackTargetBook.GetComponent<BookInfo>().currentStackRoot == null)
        {
            float thickness = stackTargetBook.GetComponent<BookStackRoot>()?.bookThickness ?? 0.12f;
            Vector3 nextSlot = stackTargetBook.transform.position + stackTargetBook.transform.up * thickness;
            Quaternion slotRot = stackTargetBook.transform.rotation;

            ghostBookInstance.transform.SetPositionAndRotation(nextSlot, slotRot);
            ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;
            SetGhostMaterial(true);
            latestGhostValid = true;
            return;
        }

        // 3) Fall-back to normal free placement:
        validPlacement = !blocked;
        ghostBookInstance.transform.SetPositionAndRotation(point, targetRot);
        latestGhostPosition = point;
        latestGhostRotation = targetRot;
        SetGhostMaterial(validPlacement);
        latestGhostValid = validPlacement;
    }

    public GameObject GetStackTargetBook()
    {
        return stackTargetBook;
    }

    private void ShowSingleGhost(GameObject heldObject)
    {
        if (ghostBookInstance == null) return;

        ghostBookInstance.transform.localScale = heldObject.transform.localScale;

        Renderer rend = ghostBookInstance.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Material[] mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = defaultMaterial;
            rend.materials = mats;
        }
    }

<<<<<<< HEAD
=======

>>>>>>> origin/main
    private bool TitlesMatch(string a, string b)
    {
        return string.Equals(a?.Trim(), b?.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

<<<<<<< HEAD
=======

>>>>>>> origin/main
    public void ShowGhost(GameObject heldObject, int stackCount = 1)
    {
        ClearGhostStack();

        for (int i = 0; i < stackCount; i++)
        {
            GameObject ghost = Instantiate(ghostBookPrefab);
            ghost.transform.localScale = heldObject.transform.localScale;
            ghost.SetActive(true);

            Renderer rend = ghost.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Material[] mats = rend.materials;
                for (int j = 0; j < mats.Length; j++)
                    mats[j] = defaultMaterial;
                rend.materials = mats;
            }

            ghostStackBooks.Add(ghost);
        }
    }


    public void HideGhost()
    {
        ClearGhostStack();
        rotationLocked = false;

        if (ghostBookInstance != null)
            ghostBookInstance.SetActive(false);
    }

    private void ClearGhostStack()
    {
        foreach (GameObject ghost in ghostStackBooks)
        {
            if (ghost != null)
                Destroy(ghost);
        }
        ghostStackBooks.Clear();
    }

    private void ApplyGhostMaterial(Material targetMaterial)
    {
        // Update main ghost book
        if (ghostRenderer != null)
        {
            Material[] mats = ghostRenderer.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = targetMaterial;
            }
            ghostRenderer.materials = mats;
<<<<<<< HEAD
        }

        // Update stack ghost books
        foreach (GameObject ghost in ghostStackBooks)
        {
            if (ghost == null) continue;

            Renderer rend = ghost.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Material[] mats = rend.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = targetMaterial;
                }
                rend.materials = mats;
            }
        }
    }

    public void UpdateGhostStackTransforms(Vector3 basePos, Quaternion baseRot)
    {
        if (ghostStackBooks.Count == 0)
            return;

        Transform topGhost = ghostStackBooks[0].transform;
        topGhost.SetPositionAndRotation(basePos, baseRot);

        // Determine stack direction
        // Always stack along the ghost book’s local up axis
        Vector3 stackDirection = topGhost.up;

        for (int i = 1; i < ghostStackBooks.Count; i++)
        {
            Vector3 offset = stackDirection.normalized * 0.12f * i;
            ghostStackBooks[i].transform.position = topGhost.position + offset;
            ghostStackBooks[i].transform.rotation = topGhost.rotation;
        }
    }

    public void ResetRotation(bool isNearShelf = false)
    {
        rotationLocked = false;

        // Correct inward-facing orientation
        rotationAmount = isNearShelf ? 0f : 270f; // Changed from 90f to 0f
        currentRotation = rotationAmount;
=======
        }

        // Update stack ghost books
        foreach (GameObject ghost in ghostStackBooks)
        {
            if (ghost == null) continue;

            Renderer rend = ghost.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Material[] mats = rend.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = targetMaterial;
                }
                rend.materials = mats;
            }
        }
>>>>>>> origin/main
    }


    public void UpdateGhostStackTransforms(Vector3 basePos, Quaternion baseRot)
    {
        if (ghostStackBooks.Count > 0)
        {
            Transform topGhost = ghostStackBooks[0].transform;
            topGhost.SetPositionAndRotation(basePos, baseRot);

            for (int i = 1; i < ghostStackBooks.Count; i++)
            {
                Vector3 offset = Vector3.up * 0.12f * i; // Or use BookStackRoot.bookThickness if available
                ghostStackBooks[i].transform.position = topGhost.position + offset;
                ghostStackBooks[i].transform.rotation = topGhost.rotation;
            }
        }
    }


    public void SetGhostMaterial(bool isValid)
    {
        ApplyGhostMaterial(isValid ? validMaterial : invalidMaterial);
    }

    public Transform GhostBookTopTransform
    {
        get
        {
            if (ghostStackBooks != null && ghostStackBooks.Count > 0)
                return ghostStackBooks[0].transform;
            return null;
        }
    }


    public GameObject GhostBookInstance => ghostBookInstance;
}