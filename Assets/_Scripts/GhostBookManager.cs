using System.Diagnostics;
using UnityEngine;

public class GhostBookManager : MonoBehaviour
{
    [SerializeField] private GameObject ghostBookPrefab;
    [SerializeField] private float surfaceOffset = 0.02f;
    [SerializeField] private float rotationSmoothSpeed = 10f;

    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;
    [SerializeField] private Material defaultMaterial;  // White/default ghost material


    private Renderer ghostRenderer;


    private GameObject ghostBookInstance;
    private GameObject stackTargetBook;
    private bool rotationLocked = false;

    private float rotationAmount = 0f;
    private float currentRotation = 0f;

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

    public void UpdateGhost(GameObject heldObject, ShelfSpot shelfSpot, Camera camera, LayerMask tableMask, ref float currentRotationY)
    {
        if (heldObject == null)
        {
            if (ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(false);
            rotationLocked = false;
            return;
        }

        // Shelf Mode
        if (shelfSpot != null && shelfSpot.GetBookAnchor() != null)
        {
            if (!ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(true);

            Transform anchor = shelfSpot.GetBookAnchor();
            ghostBookInstance.transform.SetPositionAndRotation(
    anchor.position,
    anchor.rotation * Quaternion.Euler(90f, 0f, 90f)
);

            ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;

            ApplyGhostMaterial(defaultMaterial); // keep ghost white when shelving

            rotationLocked = false;
            return;
        }



        // Table Surface Mode
        Ray ray = new Ray(camera.transform.position, camera.transform.forward);
        UnityEngine.Debug.DrawRay(ray.origin, ray.direction * 3f, Color.green, 0.1f);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f, tableMask) &&
            Vector3.Dot(hit.normal, Vector3.up) > 0.9f)
        {
            if (!ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(true);

            Vector3 point = hit.point + hit.normal * surfaceOffset;

            // Calculate yRotation
            float yRotation;

            // If user rotated manually, use currentRotationY
            if (rotationLocked)
            {
                yRotation = currentRotationY;
            }
            else
            {
                // Align book so its -Z (bottom) faces camera forward
                Vector3 cameraForward = camera.transform.forward;
                cameraForward.y = 0f;
                cameraForward.Normalize();

                // We want the book's local -Z to face cameraForward, so calculate angle from -cameraForward
                float angle = Mathf.Atan2(-cameraForward.x, -cameraForward.z) * Mathf.Rad2Deg;
                yRotation = Mathf.Round(angle / 90f) * 90f;

                currentRotationY = yRotation;
            }


            bool isStacking = false;
            stackTargetBook = null; // reset each frame

            // Only allow stacking if we're NOT nudging
            if (!NudgableStackMover.IsNudging)
            {
                // STACKING CHECK (do this first so we know if rotation should be locked)
                if (Physics.Raycast(ray, out RaycastHit stackHit, 3f, LayerMask.GetMask("Book")))
                {
                    GameObject hitBook = stackHit.collider.gameObject;
                    BookInfo hitInfo = hitBook.GetComponent<BookInfo>();
                    BookInfo heldInfo = heldObject.GetComponent<BookInfo>();

                    if (hitInfo != null && heldInfo != null && hitInfo.title == heldInfo.title)
                    {
                        BookStackRoot root = hitInfo.currentStackRoot;

                        // Find the topmost book in the stack
                        GameObject topBook = hitBook;
                        if (root != null && root.books.Count > 0)
                        {
                            topBook = root.books[root.books.Count - 1];
                        }

                        int stackCount = root != null ? root.GetCount() : 1;
                        if (stackCount < 4)
                        {
                            SetGhostMaterial(true); // Valid stacking

                            Vector3 topStackPos = topBook.transform.position + Vector3.up * 0.12f;

                            Quaternion finalRotation = topBook.transform.rotation;

                            ghostBookInstance.transform.SetPositionAndRotation(topStackPos, finalRotation);
                            ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;

                            stackTargetBook = topBook;
                            rotationLocked = false;
                            return;
                        }
                    }
                }
            }


            // Only allow rotation when not stacking
            if (!isStacking)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    currentRotationY += scroll > 0 ? 90f : -90f;
                    currentRotationY = Mathf.Repeat(currentRotationY, 360f);
                    rotationLocked = true;
                }

                float angleStep = 90f;

                if (scroll > 0)
                    rotationAmount += angleStep;
                else if (scroll < 0)
                    rotationAmount -= angleStep;

                rotationAmount %= 360f;
            }

            // REGULAR TABLE PLACEMENT
            ghostBookInstance.transform.position = point;
            currentRotation = Mathf.LerpAngle(currentRotation, rotationAmount, Time.deltaTime * rotationSmoothSpeed);
            Quaternion targetRot = Quaternion.Euler(0f, currentRotation, 0f);
            ghostBookInstance.transform.rotation = targetRot;

            // Real-time validity check (collision with other books)
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
            }
            else
            {
                // fallback to valid if no collider
                SetGhostMaterial(true);
            }

            return;
        }

        // Nothing hit
        if (!NudgableStackMover.IsNudging)
        {
            if (ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(false);
        }
        rotationLocked = false;
    }

    public GameObject GetStackTargetBook()
    {
        return stackTargetBook;
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


    private void OnDrawGizmos()
    {
        if (ghostBookInstance != null && ghostBookInstance.activeSelf)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(ghostBookInstance.transform.position, new Vector3(0.25f, 0.02f, 0.15f));
            Gizmos.DrawLine(ghostBookInstance.transform.position + Vector3.up * 0.5f, ghostBookInstance.transform.position); // Downward arrow
        }
    }


    public GameObject GhostBookInstance => ghostBookInstance;

}
