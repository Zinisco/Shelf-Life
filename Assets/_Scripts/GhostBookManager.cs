using UnityEngine;

public class GhostBookManager : MonoBehaviour
{
    [SerializeField] private GameObject ghostBookPrefab;
    [SerializeField] private float surfaceOffset = 0.02f;

    private GameObject ghostBookInstance;
    private bool rotationLocked = false;
    private float lockedYRotation = 0f;

    public void Init()
    {
        ghostBookInstance = Instantiate(ghostBookPrefab);
        ghostBookInstance.SetActive(false);
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
            ghostBookInstance.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;

            rotationLocked = false;
            return;
        }

        // Table Surface Mode
        Ray ray = new Ray(camera.transform.position, camera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f, tableMask) &&
            Vector3.Dot(hit.normal, Vector3.up) > 0.9f)
        {
            if (!ghostBookInstance.activeSelf)
                ghostBookInstance.SetActive(true);

            Vector3 point = hit.point + hit.normal * surfaceOffset;

            // Calculate yRotation
            float yRotation = currentRotationY;
            if (!rotationLocked)
            {
                Vector3 playerDir = camera.transform.forward;
                playerDir.y = 0f;
                playerDir.Normalize();

                float angle = Mathf.Atan2(playerDir.x, playerDir.z) * Mathf.Rad2Deg;
                float snappedAngle = Mathf.Round(angle / 90f) * 90f;

                yRotation = snappedAngle;
                currentRotationY = yRotation;
            }

            bool isStacking = false;

            // STACKING CHECK (do this first so we know if rotation should be locked)
            if (Physics.Raycast(ray, out RaycastHit stackHit, 3f, LayerMask.GetMask("Book")))
            {
                GameObject targetBook = stackHit.collider.gameObject;
                BookInfo targetInfo = targetBook.GetComponent<BookInfo>();
                BookInfo heldInfo = heldObject.GetComponent<BookInfo>();

                if (targetInfo != null && heldInfo != null &&
                    targetInfo.title == heldInfo.title &&
                    targetBook.transform.childCount < 5)
                {
                    Vector3 topStackPos = targetBook.transform.position + Vector3.up * 0.12f;

                    Quaternion baseRotation = Quaternion.Euler(0f, 90f, 90f);
                    Quaternion facingRotation = Quaternion.Euler(0f, currentRotationY, 0f);
                    Quaternion finalRotation = facingRotation * baseRotation;

                    ghostBookInstance.transform.SetPositionAndRotation(topStackPos, finalRotation);
                    ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;

                    isStacking = true;
                    rotationLocked = false; // disable locking while stacking
                    return;
                }
            }

            // Only allow rotation when not stacking
            if (!isStacking)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    currentRotationY += scroll > 0 ? 15f : -15f;
                    currentRotationY = Mathf.Repeat(currentRotationY, 360f);
                    lockedYRotation = currentRotationY;
                    rotationLocked = true;
                }
            }

            // REGULAR TABLE PLACEMENT
            Quaternion baseRot = Quaternion.Euler(0f, 90f, 90f);
            Quaternion faceRot = Quaternion.Euler(0f, yRotation, 0f);
            Quaternion finalRot = faceRot * baseRot;

            ghostBookInstance.transform.SetPositionAndRotation(point, finalRot);
            ghostBookInstance.transform.localScale = heldObject.transform.lossyScale;
            return;
        }

        // Nothing hit
        if (ghostBookInstance.activeSelf)
            ghostBookInstance.SetActive(false);
        rotationLocked = false;
    }

    public void HideGhost()
    {
        if (ghostBookInstance != null)
            ghostBookInstance.SetActive(false);

        rotationLocked = false;
    }
}
