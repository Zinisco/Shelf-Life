using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GhostBookManager : MonoBehaviour
{
    [SerializeField] private GameObject ghostBookPrefab; // Prefab for the translucent ghost book
    [SerializeField] private float surfaceOffset = 0.03f; // Offset above surface (table) for visual clarity
    [SerializeField] private float shelfOffset = 0.4f;    // Offset above shelf surface
    [SerializeField] private float rotationSmoothSpeed = 10f; // Speed of ghost book rotation interpolation
    [SerializeField] private float sideOnBackRelax = 0.03f; // pull forward at ±90° so it isn't too deep


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
        if (ghostBookInstance != null) Destroy(ghostBookInstance);

        ghostBookInstance = Instantiate(ghostBookPrefab);
        ghostBookInstance.transform.SetParent(transform, worldPositionStays: false);
        ghostBookInstance.transform.localPosition = Vector3.zero;
        ghostBookInstance.transform.localRotation = Quaternion.identity;
        ghostBookInstance.transform.localScale = Vector3.one; // important
        ghostBookInstance.SetActive(false);

        ghostRenderer = ghostBookInstance.GetComponentInChildren<Renderer>();
    }


    private void OnEnable()
    {
        if (GameInput.Instance != null)
        {
            GameInput.Instance.OnRotateLeftAction += HandleRotateLeft;
            GameInput.Instance.OnRotateRightAction += HandleRotateRight;
        }
    }

    private void OnDisable()
    {
        if (GameInput.Instance != null)
        {
            GameInput.Instance.OnRotateLeftAction -= HandleRotateLeft;
            GameInput.Instance.OnRotateRightAction -= HandleRotateRight;
        }
    }

    private void HandleRotateLeft(object sender, EventArgs e)
    {
        float step = GameInput.Instance.IsPrecisionModifierHeld() ? 15f : 90f;
        RotateGhostStep(-step);
    }

    private void HandleRotateRight(object sender, EventArgs e)
    {
        float step = GameInput.Instance.IsPrecisionModifierHeld() ? 15f : 90f;
        RotateGhostStep(step);
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
            ShowSingleGhost(heldObject);

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

                float angle = Mathf.Atan2(cameraForward.x, cameraForward.z) * Mathf.Rad2Deg;
                yRotation = Mathf.Round(angle / 90f) * 90f;
                currentRotationY = yRotation;

                rotationAmount = currentRotationY;
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
                            ghostBookInstance.transform.localScale = WorldScaleToLocal(
                            heldObject.transform,
                            ghostBookInstance.transform.parent
                            );


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
            currentRotationY = Mathf.Repeat(Mathf.Round(rotationAmount / 90f) * 90f, 360f);
            rotationAmount = currentRotationY;
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
            // UnityEngine.Debug.Log("No table hit.");
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

    // Helper method to check if a book's parent transform has the correct orientation (X-axis facing ShelfRegion's -Z)
    private bool IsBookFacingOutward(GameObject book, Vector3 shelfOutward, float maxAngleDeg = 40f)
    {
        if (!book) return false;

        // Change this to transform.up if your cover is +Y
        Vector3 coverDir = book.transform.up;

        float dot = Vector3.Dot(coverDir.normalized, shelfOutward.normalized);
        float cosLimit = Mathf.Cos(maxAngleDeg * Mathf.Deg2Rad);
        bool facingOutward = dot >= cosLimit;

#if UNITY_EDITOR
        // Visualize in Scene view
        Debug.DrawRay(book.transform.position, coverDir * 0.3f, facingOutward ? Color.green : Color.red, 0.05f);
        Debug.DrawRay(book.transform.position, shelfOutward.normalized * 0.3f, Color.cyan, 0.05f);
        Debug.Log($"[Ghost][FacingTest] book='{book.name}' dot={dot:F3} angle={Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg:F1} degrees  pass={facingOutward}");
#endif
        return facingOutward;
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

        // Shelf-local frame (never use world axes here)
        Transform shelfRegion = hit.collider.transform;

        Vector3 shelfOutward = -shelfRegion.forward; // toward player/out of shelf
        Vector3 shelfInward = shelfRegion.forward; // into shelf
        Vector3 shelfRight = shelfRegion.right;   // +X of shelf region
        Vector3 shelfUp = shelfRegion.up;      // vertical for this shelf

        if (!NudgableStackMover.IsNudging)
        {
            Ray stackRay = new Ray(point + shelfOutward * 0.01f, shelfInward);
            float rayLength = heldBookDepth + 0.02f;
            if (Physics.Raycast(stackRay, out RaycastHit bookHit, rayLength, LayerMask.GetMask("Book")))
            {
                var info = bookHit.collider.GetComponent<BookInfo>();
                if (info != null && info.title.Equals(heldObject.GetComponent<BookInfo>().title, System.StringComparison.OrdinalIgnoreCase))
                {
                    // Check if the book's orientation is valid (X-axis facing ShelfRegion's -Z)
                    if (IsBookFacingOutward(bookHit.collider.gameObject, shelfOutward))
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

        // --- Work in the shelf collider's local space ---
        Transform shelfTf = shelfCollider.transform;

        // Convert hit to local space
        Vector3 localPoint = shelfTf.InverseTransformPoint(point);

        // After you have shelfRight / shelfInward / shelfUp and 'localPoint'
        Vector3 centered = localPoint - shelfCollider.center;
        Vector3 halfSize = shelfCollider.size * 0.5f;

        // Get the held collider and its world-space size
        var col = heldObject.GetComponent<BoxCollider>();
        Vector3 worldSize = Vector3.Scale(col != null ? col.size : Vector3.one, heldObject.transform.lossyScale);
        float hx = worldSize.x * 0.5f; // book local right
        float hy = worldSize.y * 0.5f; // book local up (cover normal)
        float hz = worldSize.z * 0.5f; // book local forward (top direction)

        // Project the rotated half-extents onto the shelf X/Z axes
        Transform bt = ghostBookInstance.transform; // current ghost pose
        float halfFootX =
            Mathf.Abs(Vector3.Dot(bt.right, shelfRight)) * hx +
            Mathf.Abs(Vector3.Dot(bt.up, shelfRight)) * hy +
            Mathf.Abs(Vector3.Dot(bt.forward, shelfRight)) * hz;

        float halfFootZ =
            Mathf.Abs(Vector3.Dot(bt.right, shelfInward)) * hx +
            Mathf.Abs(Vector3.Dot(bt.up, shelfInward)) * hy +
            Mathf.Abs(Vector3.Dot(bt.forward, shelfInward)) * hz;

        // --- choose the BACK edge regardless of rotation ---
        const float padX = 0.01f, padZ = 0.01f;

        // X clamp as usual
        centered.x = Mathf.Clamp(
            centered.x,
            -halfSize.x + halfFootX + padX,
             halfSize.x - halfFootX - padX
        );

        // Try both Z edges and pick the deeper one along shelfInward
        float inset = halfSize.z - (halfFootZ + padZ);
        float zA = +inset;   // +Z edge candidate
        float zB = -inset;   // -Z edge candidate

        Vector3 cA = centered; cA.z = zA;
        Vector3 cB = centered; cB.z = zB;

        // Convert to world to compare depth along shelfInward
        Vector3 wA = shelfTf.TransformPoint(cA + shelfCollider.center);
        Vector3 wB = shelfTf.TransformPoint(cB + shelfCollider.center);

        // Larger dot with shelfInward == deeper (more "back")
        float depthA = Vector3.Dot(wA - shelfTf.position, shelfInward);
        float depthB = Vector3.Dot(wB - shelfTf.position, shelfInward);

        centered.z = (depthA >= depthB) ? zA : zB;

        // If the book is side-on (±90°), pull slightly toward the player (still near the back).
        bool sideOn =
            Mathf.Approximately(Mathf.Repeat(currentRotationY + 90f, 180f), 0f); // 90° or 270°

        if (sideOn && sideOnBackRelax > 0f)
        {
            // Reduce the magnitude of centered.z by a small amount (toward shelf center/front).
            float sign = Mathf.Sign(centered.z);                // which back edge we chose (+Z or -Z)
            float newMag = Mathf.Max(Mathf.Abs(centered.z) - sideOnBackRelax, 0f);
            centered.z = sign * newMag;
        }

        // Keep on the shelf floor
        centered.y = -halfSize.y;


        // Back to world
        localPoint = centered + shelfCollider.center;
        point = shelfTf.TransformPoint(localPoint);
        point += shelfUp * shelfOffset;


        // Apply rotation and position
        currentRotation = Mathf.LerpAngle(currentRotation, rotationAmount, Time.deltaTime * rotationSmoothSpeed);
        // SHELF placement
        currentRotationY = Mathf.Repeat(Mathf.Round(rotationAmount / 90f) * 90f, 360f);
        rotationAmount = currentRotationY;

        // build shelf-local axes (shelfInward/shelfRight/shelfUp)
        // update currentRotation / rotationAmount

        Quaternion orientShelf = Quaternion.LookRotation(shelfUp, shelfRight);
        Quaternion spinAroundShelfUp = Quaternion.AngleAxis(currentRotation, shelfUp);
        Quaternion targetRot = spinAroundShelfUp * orientShelf;

        ghostBookInstance.transform.SetPositionAndRotation(point, targetRot);
        ghostBookInstance.transform.localScale =
            WorldScaleToLocal(heldObject.transform, ghostBookInstance.transform.parent);

        // use the same 'point' and 'targetRot' for Physics.CheckBox etc.


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
                    // Check if the book's orientation is valid (X-axis facing ShelfRegion's -Z)
                    if (IsBookFacingOutward(c.gameObject, shelfOutward))
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
                    ghostBookInstance.transform.localScale = WorldScaleToLocal(
                    heldObject.transform,
                    ghostBookInstance.transform.parent
                    );


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
                ghostBookInstance.transform.localScale = WorldScaleToLocal(
                heldObject.transform,
                ghostBookInstance.transform.parent
                );

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
            ghostBookInstance.transform.localScale = WorldScaleToLocal(
                heldObject.transform,
                ghostBookInstance.transform.parent
            );
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
        if (!ghostBookInstance) return;
        ghostBookInstance.transform.localScale = WorldScaleToLocal(
            heldObject.transform,
            ghostBookInstance.transform.parent
        );

        Renderer rend = ghostBookInstance.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Material[] mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = defaultMaterial;
            rend.materials = mats;
        }
    }

    private bool TitlesMatch(string a, string b)
    {
        return string.Equals(a?.Trim(), b?.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    public void ShowGhost(GameObject heldObject, int stackCount = 1)
    {
        ClearGhostStack();

        for (int i = 0; i < stackCount; i++)
        {
            GameObject ghost = Instantiate(ghostBookPrefab, transform, false);
            ghost.transform.localPosition = Vector3.zero;
            ghost.transform.localRotation = Quaternion.identity;
            ghost.transform.localScale = WorldScaleToLocal(
                heldObject.transform,
                transform
            );
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

    public void RotateGhostStep(float deltaDegrees)
    {
        if (ghostBookInstance == null || !ghostBookInstance.activeSelf) return;
        rotationLocked = true;
        rotationAmount = Mathf.Repeat(rotationAmount + deltaDegrees, 360f);
    }


    public void ResetRotation(bool isNearShelf = false)
    {
        rotationLocked = false;

        // Correct inward-facing orientation
        rotationAmount = isNearShelf ? 0f : 270f; // Changed from 90f to 0f
        currentRotation = rotationAmount;
    }

    private static Vector3 WorldScaleToLocal(Transform source, Transform targetParent)
    {
        Vector3 srcWorld = source.lossyScale;
        Vector3 parentWorld = targetParent ? targetParent.lossyScale : Vector3.one;

        // Avoid divide-by-zero if something is scaled to 0
        float sx = parentWorld.x != 0 ? srcWorld.x / parentWorld.x : srcWorld.x;
        float sy = parentWorld.y != 0 ? srcWorld.y / parentWorld.y : srcWorld.y;
        float sz = parentWorld.z != 0 ? srcWorld.z / parentWorld.z : srcWorld.z;

        return new Vector3(sx, sy, sz);
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