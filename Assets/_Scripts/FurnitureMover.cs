using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FurnitureMover : MonoBehaviour
{
    private PickUp pickUp;

    [SerializeField] private Collider playerCollider;
    public Collider PlayerCollider => playerCollider;

    [SerializeField] private Transform playerCamera;
    [SerializeField] private float moveDistance = 3f;
    [SerializeField] private float rotationSmoothSpeed = 10f;
    [SerializeField] private LayerMask placementObstacles;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private GameInput gameInput;
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    [SerializeField] private Image progressRingUI;

    private GameObject selectedFurniture;
    private GameObject ghostVisual;
    private Renderer ghostRenderer;
    private Renderer[] originalRenderers;
    private Vector3 ghostOffset = Vector3.zero;
    private Renderer arrowRenderer;
    private Vector3 originalPosition;
    private Quaternion originalRotation;


    private float currentRotation = 0f;
    private float postPlaceCooldown = 0.2f; // short buffer after placement
    private float postPlaceTimer = 0f;

    private int movingFurnitureLayer;
    private int originalFurnitureLayer;


    [SerializeField] private float holdTime = 1.5f;
    private float holdTimer = 0f;
    private bool isHoldingToMove = false;

    private Collider playerCol;

    private bool isMoving = false;
    private float rotationAmount = 0f;

    private Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();


    private void OnEnable()
    {
        if (gameInput == null)
        {
            Debug.LogError("GameInput is not assigned to FurnitureMover!");
            return;
        }

        gameInput.OnRotateLeftAction += OnRotateLeft;
        gameInput.OnRotateRightAction += OnRotateRight;

        gameInput.OnMoveFurniturePressed += HandleMovePressed;   // start/continue holding
        gameInput.OnMoveFurnitureReleased += HandleMoveReleased;  // abort hold
        gameInput.OnPlaceFurnitureAction += HandlePlaceFurniture; // confirm placement (click/tap)
        gameInput.OnCancel += HandleCancelInput;
    }

    private void OnDisable()
    {

        gameInput.OnRotateLeftAction -= OnRotateLeft;
        gameInput.OnRotateRightAction -= OnRotateRight;

        gameInput.OnMoveFurniturePressed -= HandleMovePressed;
        gameInput.OnMoveFurnitureReleased -= HandleMoveReleased;
        gameInput.OnPlaceFurnitureAction -= HandlePlaceFurniture;
        gameInput.OnCancel -= HandleCancelInput;
    }

    private void Start()
    {
        pickUp = FindObjectOfType<PickUp>();
        playerCol = PlayerCollider;

        // Get correct layer index from Unity
        movingFurnitureLayer = LayerMask.NameToLayer("MovingFurniture");

        if (movingFurnitureLayer == -1)
            Debug.LogWarning("Layer 'MovingFurniture' not found. Please create it in Unity.");
    }


    private void Update()
    {
        if (ComputerUI.IsUIOpen || (pickUp != null && pickUp.IsHoldingObject()))
            return;

        if (postPlaceTimer > 0f)
        {
            postPlaceTimer -= Time.deltaTime;
            return;
        }

        if (!isMoving)
        {
            // We’re in pre-move state, waiting for the hold to finish.
            if (isHoldingToMove)
            {
                // Only proceed if we’re actually aiming at furniture
                if (TryFindFurniture(out GameObject previewFurniture))
                {
                    holdTimer -= Time.deltaTime;

                    if (progressRingUI != null)
                    {
                        float pct = 1f - (holdTimer / holdTime);
                        progressRingUI.fillAmount = Mathf.Clamp01(pct);
                        progressRingUI.gameObject.SetActive(true);
                    }

                    if (holdTimer <= 0f)
                    {
                        HandleStartMove(this, EventArgs.Empty);
                        isHoldingToMove = false;

                        if (progressRingUI != null)
                        {
                            progressRingUI.fillAmount = 0f;
                            progressRingUI.gameObject.SetActive(false);
                        }
                    }
                }
                else
                {
                    // Not aiming at furniture -> cancel the hold UI
                    isHoldingToMove = false;
                    holdTimer = holdTime;
                    if (progressRingUI != null)
                    {
                        progressRingUI.fillAmount = 0f;
                        progressRingUI.gameObject.SetActive(false);
                    }
                }
            }
        }
        else
        {
            // actively moving
            UpdateGhostPosition();
            HandleRotationInput();
        }
    }


    private void HandleStartMove(object sender, EventArgs e)
    {
        Debug.Log("Trying to start furniture move...");

        if (ComputerUI.IsUIOpen) return; // Prevent initiating furniture move

        if (pickUp != null && pickUp.IsHoldingObject()) return;

        if (isMoving || !TryFindFurniture(out selectedFurniture))
        {
            Debug.Log("Cannot move: already moving or no furniture found.");
            return;
        }
        Debug.Log($"Started moving: {selectedFurniture.name}");

        isMoving = true;
        ghostVisual = selectedFurniture.transform.Find("Ghost")?.gameObject;

        originalPosition = selectedFurniture.transform.position;
        originalRotation = selectedFurniture.transform.rotation;

        originalFurnitureLayer = selectedFurniture.layer;
        selectedFurniture.layer = movingFurnitureLayer;

        // Also change all children
        originalLayers.Clear(); // Reset

        foreach (Transform child in selectedFurniture.GetComponentsInChildren<Transform>(true))
        {
            originalLayers[child.gameObject] = child.gameObject.layer;
            child.gameObject.layer = movingFurnitureLayer;
        }

        // Cache all the furniture’s colliders
        Collider[] furnitureCols = selectedFurniture.GetComponentsInChildren<Collider>();

        // Turn OFF collisions between furniture and player
        foreach (var c in furnitureCols)
        {
            Physics.IgnoreCollision(c, playerCol, true);
        }


        // Assign ghostRenderer BEFORE disabling other renderers
        if (ghostVisual != null)
            ghostRenderer = ghostVisual.GetComponentInChildren<Renderer>();

        originalRenderers = selectedFurniture.GetComponentsInChildren<Renderer>();
        foreach (var rend in originalRenderers)
        {
            if (rend != ghostRenderer) // Don't disable the ghost
                rend.enabled = false;
        }

        if (ghostVisual != null)
        {
            ghostVisual.SetActive(true);

            Transform arrowTransform = ghostVisual.transform.Find("Arrow");
            if (arrowTransform != null)
                arrowRenderer = arrowTransform.GetComponent<Renderer>();

            Renderer renderer = ghostVisual.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                float bottomY = renderer.bounds.min.y;
                float visualWorldY = ghostVisual.transform.position.y;
                float offsetY = bottomY - visualWorldY;
                ghostOffset = new Vector3(0f, -offsetY, 0f);
            }
        }
    }





    private void HandlePlaceFurniture(object sender, EventArgs e)
    {
        if (!isMoving || selectedFurniture == null || ghostVisual == null) return;

        // Only place if valid location
        if (!CanPlaceGhost())
        {
            Debug.Log("Invalid placement location.");
            return;
        }

        // Apply ghost position/rotation to furniture
        // Set X and Z from ghost, but calculate Y so furniture sits flush with floor
        Bounds bounds = selectedFurniture.GetComponent<Collider>().bounds;
        float bottomOffset = bounds.center.y - bounds.extents.y;

        Vector3 ghostPos = ghostVisual.transform.position;

        // Raycast to get ground height
        RaycastHit hitInfo;
        float y = ghostPos.y; // fallback

        if (Physics.Raycast(ghostPos + Vector3.up, Vector3.down, out hitInfo, 5f, groundMask))
        {
            y = hitInfo.point.y;
        }

        Vector3 correctPosition = new Vector3(ghostPos.x, y, ghostPos.z);
        selectedFurniture.transform.position = correctPosition;


        selectedFurniture.transform.position = correctPosition;

        selectedFurniture.transform.rotation = ghostVisual.transform.rotation;

        // Cache all the furniture’s colliders
        Collider[] furnitureCols = selectedFurniture.GetComponentsInChildren<Collider>();

        // Turn OFF collisions between furniture and player
        foreach (var c in furnitureCols)
        {
            Physics.IgnoreCollision(c, playerCol, false);
        }


        if (originalRenderers != null)
        {
            foreach (var rend in originalRenderers)
            {
                if (rend != null)
                    rend.enabled = true;
            }
        }

        selectedFurniture.layer = originalFurnitureLayer;

        if (originalLayers != null)
        {
            foreach (var kvp in originalLayers)
            {
                if (kvp.Key != null)
                    kvp.Key.layer = kvp.Value;
            }
        }
        originalLayers.Clear(); // Clean up

        ghostVisual.SetActive(false);

        selectedFurniture = null;
        ghostVisual = null;
        ghostRenderer = null;
        rotationAmount = 0f;
        isMoving = false;

        postPlaceTimer = postPlaceCooldown;
    }

    private bool TryFindFurniture(out GameObject furniture)
    {
        furniture = null;
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, 3f))
            return false;

        // fallback to any tagged Furniture root
        MovableFurniture movable = hit.collider.GetComponentInParent<MovableFurniture>();
        if (movable != null && movable.CanMove())
        {
            furniture = movable.gameObject;
            return true;
        }


        return false;
    }

    private void UpdateGhostPosition()
    {
        if (ghostVisual == null || selectedFurniture == null) return;

        Vector3 forwardPos = playerCamera.position + playerCamera.forward * moveDistance;
        Vector3 rayOrigin = forwardPos + Vector3.up * 2f; // cast from above to ensure it hits
        Ray ray = new Ray(rayOrigin, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, 5f, groundMask))
        {
            forwardPos.y = hit.point.y;
        }
        else
        {
            forwardPos.y = 0f; // fallback
        }

        selectedFurniture.transform.position = forwardPos;


        // Smoothly interpolate rotation
        currentRotation = Mathf.LerpAngle(currentRotation, rotationAmount, Time.deltaTime * rotationSmoothSpeed);
        Quaternion targetRot = Quaternion.Euler(0f, currentRotation, 0f);
        selectedFurniture.transform.rotation = targetRot;

        ghostVisual.transform.localPosition = ghostOffset;

        if (ghostRenderer != null)
        {
            bool canPlace = CanPlaceGhost();
            Material mat = canPlace ? validMaterial : invalidMaterial;
            ghostRenderer.material = mat;
            if (arrowRenderer != null)
                arrowRenderer.material = mat;
        }
    }

    private void HandleMovePressed()
    {
        if (isMoving) return; // already moving
        if (ComputerUI.IsUIOpen) return;
        if (pickUp != null && pickUp.IsHoldingObject()) return;

        if (!isHoldingToMove)
        {
            isHoldingToMove = true;
            holdTimer = holdTime;
            if (progressRingUI != null)
            {
                progressRingUI.fillAmount = 0f;
                progressRingUI.gameObject.SetActive(true);
            }
        }
    }

    private void HandleMoveReleased()
    {
        // Button released before finishing the hold
        if (!isMoving)
        {
            isHoldingToMove = false;
            holdTimer = holdTime;
            if (progressRingUI != null)
            {
                progressRingUI.fillAmount = 0f;
                progressRingUI.gameObject.SetActive(false);
            }
        }
    }


    private void HandleRotationInput()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f) return;


        float angleStep = IsShiftHeld() ? 15f : 90f;

        if (scroll > 0)
            rotationAmount += angleStep;
        else if (scroll < 0)
            rotationAmount -= angleStep;

        rotationAmount %= 360f;
    }


    private bool CanPlaceGhost()
    {
        if (ghostVisual == null || selectedFurniture == null)
            return false;

        if (originalLayers != null)
        {
            foreach (var kvp in originalLayers)
            {
                if (kvp.Key != null)
                    kvp.Key.layer = kvp.Value;
            }
        }
        originalLayers.Clear(); // Clean up

        Bounds bounds = new Bounds(selectedFurniture.transform.position, Vector3.zero);
        Renderer[] renderers = selectedFurniture.GetComponentsInChildren<Renderer>();

        bool hasValidRenderer = false;
        foreach (Renderer rend in renderers)
        {
            if (ghostVisual != null && rend.transform.IsChildOf(ghostVisual.transform))
                continue;

            bounds.Encapsulate(rend.bounds);
            hasValidRenderer = true;
        }

        if (!hasValidRenderer)
            return false;

        Vector3 center = bounds.center;
        Vector3 halfExtents = bounds.extents;

        // Store all colliders of the furniture being moved
        Collider[] selfColliders = selectedFurniture.GetComponentsInChildren<Collider>();

        // Check for overlaps with placement obstacles (now including Furniture layer)
        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            selectedFurniture.transform.rotation,
            placementObstacles,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in hits)
        {
            // Ignore self-collisions
            if (selfColliders.Contains(hit))
                continue;

            return false; // Found an obstacle that's not part of this furniture
        }

        return true;
    }

    private void HandleCancelInput()
    {
        // If we're in the hold-to-start phase, cancel the hold UI
        if (!isMoving)
        {
            if (isHoldingToMove)
            {
                isHoldingToMove = false;
                holdTimer = holdTime;
                if (progressRingUI != null)
                {
                    progressRingUI.fillAmount = 0f;
                    progressRingUI.gameObject.SetActive(false);
                }
            }
            return;
        }

        // If we are actively moving, cancel the move and restore everything
        CancelMove();
    }


    private void OnRotateLeft(object sender, EventArgs e)
    {
        float angleStep = IsShiftHeld() ? 15f : 90f;
        rotationAmount -= angleStep;
        rotationAmount %= 360f;
    }

    private void OnRotateRight(object sender, EventArgs e)
    {
        float angleStep = IsShiftHeld() ? 15f : 90f;
        rotationAmount += angleStep;
        rotationAmount %= 360f;
    }

    private bool IsShiftHeld()
    {
        return GameInput.Instance.IsPrecisionModifierHeld();
    }

    public bool IsMovingFurniture()
    {
        return isMoving;
    }

    private void CancelMove()
    {
        if (selectedFurniture == null) return;

        // Restore original transform
        selectedFurniture.transform.position = originalPosition;
        selectedFurniture.transform.rotation = originalRotation;

        // Re-enable renderers
        foreach (var rend in originalRenderers)
            if (rend != null) rend.enabled = true;

        // Stop ignoring collisions with player
        var furnitureCols = selectedFurniture.GetComponentsInChildren<Collider>();
        foreach (var c in furnitureCols)
            Physics.IgnoreCollision(c, playerCol, false);

        // Turn off ghost
        if (ghostVisual != null)
            ghostVisual.SetActive(false);

        // Reset layers (root + all children)
        selectedFurniture.layer = originalFurnitureLayer;
        if (originalLayers != null)
        {
            foreach (var kvp in originalLayers)
                if (kvp.Key != null) kvp.Key.layer = kvp.Value;
            originalLayers.Clear();
        }

        // Reset state
        selectedFurniture = null;
        ghostVisual = null;
        ghostRenderer = null;
        rotationAmount = 0f;
        isMoving = false;

        // Also ensure hold UI is off
        isHoldingToMove = false;
        holdTimer = holdTime;
        if (progressRingUI != null)
        {
            progressRingUI.fillAmount = 0f;
            progressRingUI.gameObject.SetActive(false);
        }
    }

}
