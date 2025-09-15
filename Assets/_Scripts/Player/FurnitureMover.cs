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

    private PlantMover plantMover;
    private GameObject selectedFurniture;
    private GameObject ghostVisual;
    private Renderer ghostRenderer;
    private Renderer[] originalRenderers;
    private Vector3 ghostOffset = Vector3.zero;
    private Renderer arrowRenderer;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Transform visualTransform;
    private Vector3 visualOriginalLocalPos;

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
    private Renderer[] ghostRenderers;

    private Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();


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
        plantMover = FindObjectOfType<PlantMover>();
        playerCol = PlayerCollider;

        // Get correct layer index from Unity
        movingFurnitureLayer = LayerMask.NameToLayer("MovingFurniture");

        if (movingFurnitureLayer == -1)
            Debug.LogWarning("Layer 'MovingFurniture' not found. Please create it in Unity.");
    }


    private void Update()
    {
        if (ComputerUI.IsUIOpen ||
       (pickUp != null && pickUp.IsHoldingObject()) ||
       (plantMover != null && plantMover.IsMovingPlant()))
        {
            return;
        }

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
        if (ComputerUI.IsUIOpen) return; // Prevent initiating furniture move

        if (pickUp != null && pickUp.IsHoldingObject()) return;

        if (isMoving || !TryFindFurniture(out selectedFurniture))
        {
            return;
        }

        isMoving = true;
        ghostVisual = selectedFurniture.transform.Find("Ghost")?.gameObject;

        originalPosition = selectedFurniture.transform.position;
        originalRotation = selectedFurniture.transform.rotation;

        originalFurnitureLayer = selectedFurniture.layer;
        selectedFurniture.layer = movingFurnitureLayer;

        foreach (Transform child in selectedFurniture.GetComponentsInChildren<Transform>(true))
        {
            if (ghostVisual != null && child.IsChildOf(ghostVisual.transform))
                continue;

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

        MovableFurniture movable = selectedFurniture.GetComponent<MovableFurniture>();
        visualTransform = movable?.GetVisualRoot();

        if (visualTransform == null)
        {
            Debug.LogWarning($"No visual root assigned in MovableFurniture on '{selectedFurniture.name}'!");
        }


        // Assign ghostRenderer BEFORE disabling other renderers
        if (ghostVisual != null)
            ghostRenderer = ghostVisual.GetComponentInChildren<Renderer>();

        originalRenderers = selectedFurniture.GetComponentsInChildren<Renderer>();
        originalMaterials.Clear();

        foreach (var rend in originalRenderers)
        {
            if (rend != null && !rend.transform.IsChildOf(ghostVisual.transform))
            {
                // Just hide renderer
                rend.enabled = false;
            }
        }

        Debug.Log("Selected Furniture: " + selectedFurniture.name);
        foreach (Transform child in selectedFurniture.transform)
        {
            Debug.Log(" - Child: " + child.name);
        }


        if (ghostVisual != null)
        {
            ghostVisual.SetActive(true);

            ghostRenderers = ghostVisual.GetComponentsInChildren<Renderer>();

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

        if (!CanPlaceGhost())
        {
            Debug.Log("Invalid placement location.");
            return;
        }

        // Apply ghost transform to the root
        Vector3 ghostPos = ghostVisual.transform.position;
        float y = ghostPos.y;

        if (Physics.Raycast(ghostPos + Vector3.up, Vector3.down, out RaycastHit hitInfo, 5f, groundMask))
            y = hitInfo.point.y;

        Vector3 correctPosition = new Vector3(ghostPos.x, y, ghostPos.z);
        selectedFurniture.transform.SetPositionAndRotation(correctPosition, ghostVisual.transform.rotation);

        // Restore collisions
        foreach (var c in selectedFurniture.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(c, playerCol, false);


        // Re-enable the renderers
        if (originalRenderers != null)
        {
            foreach (var rend in originalRenderers)
                if (rend != null && !rend.transform.IsChildOf(ghostVisual.transform))
                    rend.enabled = true;
        }

        // Reset layers
        selectedFurniture.layer = originalFurnitureLayer;
        foreach (var kvp in originalLayers)
            if (kvp.Key != null) kvp.Key.layer = kvp.Value;
        originalLayers.Clear();

        ghostVisual.SetActive(false);

        // Reset state
        selectedFurniture = null;
        ghostVisual = null;
        ghostRenderer = null;
        rotationAmount = 0f;
        isMoving = false;
        ghostRenderers = null;
        visualTransform = null;

        postPlaceTimer = postPlaceCooldown;
        PauseMenuController.Instance?.BlockPauseFor(0.1f);
    }


    private bool TryFindFurniture(out GameObject furniture)
    {
        furniture = null;
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, 3f))
        {
            return false;
        }

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

        // Project forward from camera
        Vector3 forwardPos = playerCamera.position + playerCamera.forward * moveDistance;
        Vector3 rayOrigin = forwardPos + Vector3.up * 2f;
        Ray ray = new Ray(rayOrigin, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, 5f, groundMask))
            forwardPos.y = hit.point.y;
        else
            forwardPos.y = 0f;

        if (visualTransform != null)
        {
            Debug.DrawLine(visualTransform.position, selectedFurniture.transform.position, Color.red);
            Debug.Log($"[Visual] {visualTransform.name} localPos: {visualTransform.localPosition}, worldPos: {visualTransform.position}");
        }

        // Move the root only
        selectedFurniture.transform.position = forwardPos;

        // Smoothly rotate the root
        currentRotation = Mathf.LerpAngle(currentRotation, rotationAmount, Time.deltaTime * rotationSmoothSpeed);
        Quaternion targetRot = Quaternion.Euler(0f, currentRotation, 0f);
        selectedFurniture.transform.rotation = targetRot;

        // Keep ghost aligned with the root
        ghostVisual.transform.localPosition = Vector3.zero;
        ghostVisual.transform.localRotation = Quaternion.identity;

        // Set ghost materials (valid/invalid)
        bool canPlace = CanPlaceGhost();
        Material ghostMat = canPlace ? validMaterial : invalidMaterial;

        if (ghostRenderers != null)
        {
            foreach (var rend in ghostRenderers)
            {
                if (rend != null)
                {
                    Material[] ghostMats = new Material[rend.materials.Length];
                    for (int i = 0; i < ghostMats.Length; i++)
                        ghostMats[i] = ghostMat;
                    rend.materials = ghostMats;
                }
            }
        }

        if (arrowRenderer != null)
            arrowRenderer.material = ghostMat;
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
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused)
            return;

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

        PauseMenuController.Instance?.BlockPauseFor(0.1f);

        // Restore original transform
        selectedFurniture.transform.position = originalPosition;
        selectedFurniture.transform.rotation = originalRotation;

        // Re-enable renderers
        foreach (var rend in originalRenderers)
            if (rend != null) rend.enabled = true;

        if (originalMaterials != null)
        {
            foreach (var kvp in originalMaterials)
            {
                if (kvp.Key != null)
                    kvp.Key.materials = kvp.Value;
            }
            originalMaterials.Clear();
        }

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
        ghostRenderers = null;

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
