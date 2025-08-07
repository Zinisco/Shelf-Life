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

            ghostBookInstance.transform.position = point;
            currentRotation = Mathf.LerpAngle(currentRotation, rotationAmount, Time.deltaTime * rotationSmoothSpeed);
            Quaternion targetRot = Quaternion.Euler(0f, currentRotation, 0f);
            ghostBookInstance.transform.rotation = targetRot;

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

        // Hide ghost if not targeting anything
        if (!NudgableStackMover.IsNudging)
        {
            if (ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(false);
        }

        rotationLocked = false;
        latestGhostValid = false;
    }

    // Handles ghost placement on a shelf
    private void HandleShelfGhostPlacement(RaycastHit hit, GameObject heldObject, Camera camera, ref float currentRotationY)
    {
        stackTargetBook = null;

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

        // reset previous target
        stackTargetBook = null;

        // figure out “inward” direction of the shelf
        Vector3 shelfInward = hit.normal;

        // cast a short ray from just in front of the ghost back into the shelf
        Ray stackRay = new Ray(point + shelfInward * 0.01f, -shelfInward);
        float rayLength = heldBookDepth + 0.02f;  // just a hair longer than one book
        if (Physics.Raycast(stackRay, out RaycastHit bookHit, rayLength, LayerMask.GetMask("Book")))
        {
            var info = bookHit.collider.GetComponent<BookInfo>();
            if (info != null && info.title.Equals(heldObject.GetComponent<BookInfo>().title, System.StringComparison.OrdinalIgnoreCase))
            {
                // we found the base book!
                stackTargetBook = bookHit.collider.gameObject;
                UnityEngine.Debug.Log($"[Ghost] stacking onto: {stackTargetBook.name}");
            }
        }

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

        // Clamp ghost position inside shelf bounds
        Vector3 localPoint = shelfCollider.transform.InverseTransformPoint(point);
        Vector3 halfSize = shelfCollider.size * 0.5f;

        if (currentRotationY == 90 || currentRotationY == 270)
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
        Quaternion targetRot = Quaternion.Euler(270f, currentRotation + 180f, 90f); // assuming upright shelf book
        ghostBookInstance.transform.SetPositionAndRotation(point, targetRot);
        ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;

        // COVER FACING CHECK — only allow stacking if cover is facing forward (roughly 180° Y)
        // only stack when the ghost itself is rotated toward the player: 90° or 270°
        float ghostYaw = Mathf.Repeat(currentRotation, 360f);
        bool isFacingPlayer = Mathf.Abs(ghostYaw - 90f) < 10f || Mathf.Abs(ghostYaw - 270f) < 10f;
        if (!isFacingPlayer)
        {
            // not turned correctly, abort any stack?snap
            stackTargetBook = null;
                    // fall through to free?placement
                   goto FREE_PLACEMENT;
        }
        
        // COVER FACING CHECK — only allow stacking if cover is facing forward (roughly 180° Y)
        float yRot = Mathf.Repeat(currentRotation + 180f, 360f);
        bool isCoverFacingForward = Mathf.Abs(yRot - 180f) < 10f;

        // TITLE MATCH CHECK
        BookInfo heldInfo = heldObject.GetComponent<BookInfo>();
        if (isCoverFacingForward && heldInfo != null)
        {
            // how far to sample? half the book depth
            float sampleDist = heldBookDepth * 0.5f;

            // start just slightly inside the ghost
            Vector3 samplePos = point + shelfInward * sampleDist;

            // look for any book colliders right against that face
            Collider[] hits = Physics.OverlapSphere(
              samplePos,
              0.05f,                      // small radius
              LayerMask.GetMask("Book"),
              QueryTriggerInteraction.Ignore
            );

            foreach (var c in hits)
            {
                var info = c.GetComponent<BookInfo>();
                if (info != null && TitlesMatch(info.title, heldInfo.title))
                {
                    // we found the base book!
                    stackTargetBook = c.gameObject;
                    UnityEngine.Debug.Log($"[Ghost] stacking onto: {stackTargetBook.name}");
                    break;
                }
            }

            if (stackTargetBook != null)
            {
                var root = stackTargetBook.GetComponent<BookInfo>().currentStackRoot;
                if (root != null && root.CanStack(heldObject.GetComponent<BookInfo>().title))
                {
                    // 1) snap to the next slot along the root’s local up
                    Vector3 nextSlot = root.TopPosition;
                    Quaternion rot = stackTargetBook.transform.rotation;

                    ghostBookInstance.transform.SetPositionAndRotation(nextSlot, rot);
                    ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;

                    SetGhostMaterial(true);
                    latestGhostValid = true;

                    // so TryShelveBook knows exactly which book to attach to
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
                // real?stack case
                ghostBookInstance.transform.SetPositionAndRotation(
                    root.TopPosition,
                    stackTargetBook.transform.rotation
                );
                ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;
                SetGhostMaterial(true);
                latestGhostValid = true;

                // tell TryShelveBook to attach to the last book in that root
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

    private bool TitlesMatch(string a, string b)
    {
        return string.Equals(a?.Trim(), b?.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }


    public void ShowGhost(GameObject heldObject)
    {
        if (ghostBookInstance == null)
            ghostBookInstance = Instantiate(ghostBookPrefab);

        ghostBookInstance.SetActive(true);
        ghostBookInstance.transform.localScale = heldObject.transform.localScale;
    }

    public void HideGhost()
    {
        if (ghostBookInstance != null)
            ghostBookInstance.SetActive(false);

        rotationLocked = false;
    }

    private void ApplyGhostMaterial(Material targetMaterial)
    {
        if (ghostRenderer == null) return;

        Material[] mats = ghostRenderer.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            mats[i] = targetMaterial;
        }
        ghostRenderer.materials = mats;
    }

    public void SetGhostMaterial(bool isValid)
    {
        ApplyGhostMaterial(isValid ? validMaterial : invalidMaterial);
    }

    // Debug visuals in editor
    private void OnDrawGizmos()
    {
        if (ghostBookInstance != null && ghostBookInstance.activeSelf)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(ghostBookInstance.transform.position, new Vector3(0.25f, 0.02f, 0.15f));
            Gizmos.DrawLine(ghostBookInstance.transform.position + Vector3.up * 0.5f, ghostBookInstance.transform.position);
        }
    }

    public GameObject GhostBookInstance => ghostBookInstance;
}
