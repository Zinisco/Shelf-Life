using System;
using UnityEngine;
using static DesignItem;

[RequireComponent(typeof(Collider))]
public class DesignItemCrate : MonoBehaviour
{
    [Header("Crate Settings")]
    [SerializeField] private string crateID;

    [SerializeField] private MeshRenderer labelRenderer;

    [SerializeField] private Transform placementAnchor;
    public Transform GetPlacementAnchor() => placementAnchor;


    [Header("Game Input")]
    [SerializeField] private GameInput gameInput;

    [Header("Crate Contents")]
    [SerializeField] private ParticleSystem openEffect;

    private DesignItem designItem;
    private bool _playerInRange = false;
    private bool _opened = false;
    private bool isHeld = false;

    private void Awake()
    {
        // Generate unique ID
        if (string.IsNullOrEmpty(crateID))
            crateID = Guid.NewGuid().ToString();
    }

    private void Start()
    {
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        gameInput = GameInput.Instance;
        if (gameInput != null)
        {
            gameInput.OnInteractAction += GameInput_OnInteractAction;
        }
    }


    private void OnDestroy()
    {
        if (gameInput != null)
        {
            gameInput.OnInteractAction -= GameInput_OnInteractAction;
        }
    }

    private void GameInput_OnInteractAction(object sender, EventArgs e)
    {
        OpenCrate();
    }

    public void SetDesignItem(DesignItem item)
    {
        designItem = item;

        if (labelRenderer != null && designItem.itemImage != null)
        {
            // Clone the material so each crate gets its own instance
            Material matInstance = new Material(labelRenderer.sharedMaterial);
            matInstance.mainTexture = designItem.itemImage.texture;
            labelRenderer.material = matInstance;
        }
    }


    public void OpenCrate()
    {
        if (_opened || !_playerInRange || isHeld) return;
        _opened = true;

        if (gameInput != null)
            gameInput.OnInteractAction -= GameInput_OnInteractAction;

        if (designItem != null)
        {
            // Disable crate collider so raycasts won’t hit it
            Collider myCol = GetComponent<Collider>();
            if (myCol != null) myCol.enabled = false;

            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            GameObject go = Instantiate(designItem.itemPrefab, spawnPos, Quaternion.identity);

            Vector3 finalPos = spawnPos;

            // -------------------
            // FLOOR ITEMS
            // -------------------
            if (designItem.placement == PlacementType.Floor)
            {
                // Always raycast down to "Ground" only
                if (Physics.Raycast(spawnPos + Vector3.up, Vector3.down, out RaycastHit groundHit, 20f, LayerMask.GetMask("Ground")))
                {
                    finalPos = groundHit.point + Vector3.up * designItem.verticalOffset;
                }

                // Collision check + push outward if blocked
                Collider prefabCol = go.GetComponentInChildren<Collider>();
                if (prefabCol != null)
                {
                    Vector3 halfExtents = prefabCol.bounds.extents;
                    if (IsBlocked(finalPos, halfExtents, go.transform.rotation))
                    {
                        finalPos = FindNearestGroundSpot(finalPos, halfExtents, go.transform.rotation, 1.0f, 15);
                    }
                }
            }
            // -------------------
            // TABLE ITEMS
            // -------------------
            else if (designItem.placement == PlacementType.Table)
            {
                // These are allowed to sit on furniture surfaces
                if (Physics.Raycast(spawnPos + Vector3.up, Vector3.down, out RaycastHit hit, 10f,
                    LayerMask.GetMask("Table", "Desk", "Furniture", "Ground")))
                {
                    finalPos = hit.point + Vector3.up * designItem.verticalOffset;
                }
            }
            // -------------------
            // CEILING ITEMS
            // -------------------
            else if (designItem.placement == PlacementType.Ceiling)
            {
                if (Physics.Raycast(spawnPos, Vector3.up, out RaycastHit hit, 10f, LayerMask.GetMask("Ceiling")))
                {
                    finalPos = hit.point - Vector3.up * designItem.verticalOffset;
                    go.transform.rotation = Quaternion.LookRotation(Vector3.down);
                }
            }

            go.transform.position = finalPos;

            if (openEffect != null)
                Instantiate(openEffect, transform.position, Quaternion.identity);

            Debug.Log($"Opened Design Crate: {crateID}, spawned {designItem.itemName}");
            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("DesignItem not assigned to crate.");
        }
    }

    private bool IsBlocked(Vector3 pos, Vector3 halfExtents, Quaternion rot)
    {
        return Physics.CheckBox(
            pos, halfExtents, rot,
            LayerMask.GetMask("Book", "Furniture", "Plant", "Crate", "BookDisplay", "Table", "Desk"),
            QueryTriggerInteraction.Ignore
        );
    }

    /// <summary>
    /// Searches outward for the nearest free *ground* spot.
    /// </summary>
    private Vector3 FindNearestGroundSpot(Vector3 start, Vector3 halfExtents, Quaternion rot, float step, int maxChecks)
    {
        for (int i = 1; i <= maxChecks; i++)
        {
            for (int dir = 0; dir < 8; dir++)
            {
                float angle = dir * Mathf.PI / 4f;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (i * step);
                Vector3 candidateXZ = start + offset;

                // Snap candidate down to ground
                if (Physics.Raycast(candidateXZ + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 10f, LayerMask.GetMask("Ground")))
                {
                    Vector3 candidate = groundHit.point + Vector3.up * designItem.verticalOffset;

                    if (!IsBlocked(candidate, halfExtents, rot))
                    {
                        Debug.Log($"[DesignItemCrate] Pushed {designItem.itemName} to free ground spot {offset.magnitude:F2}m away");
                        return candidate;
                    }
                }
            }
        }

        Debug.LogWarning("[DesignItemCrate] No free ground spot found, placing at start anyway");
        return start;
    }



    public void EnablePhysics()
    {
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = false;
    }

    public void SetHeld(bool held)
    {
        isHeld = held;

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            if (isHeld)
            {
                rb.isKinematic = false;  // stays dynamic so joint can drive it
                rb.useGravity = false;   // no gravity while held
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.None; // let joint fully control motion
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
            else
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.None;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }
    }



    // Accessors
    public string GetCrateID() => crateID;
    public bool IsOpened() => _opened;
}
