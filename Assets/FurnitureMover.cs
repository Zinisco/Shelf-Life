using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class FurnitureMover : MonoBehaviour
{
    private PickUp pickUp;

    [SerializeField] private Transform playerCamera;
    [SerializeField] private float moveDistance = 3f;
    [SerializeField] private LayerMask placementObstacles;
    [SerializeField] private GameInput gameInput;
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    private GameObject selectedFurniture;
    private GameObject ghostVisual;
    private Renderer ghostRenderer;
    private Renderer[] originalRenderers;
    private Vector3 ghostOffset = Vector3.zero;
    private Renderer arrowRenderer;

    private bool isMoving = false;
    private float rotationAmount = 0f;

    private void OnEnable()
    {
        if (gameInput == null)
        {
            Debug.LogError("GameInput is not assigned to FurnitureMover!");
            return;
        }

        gameInput.OnStartMoveFurnitureAction += HandleStartMove;
        gameInput.OnPlaceFurnitureAction += HandlePlaceFurniture;

        gameInput.OnRotateLeftAction += OnRotateLeft;
        gameInput.OnRotateRightAction += OnRotateRight;
    }

    private void OnDisable()
    {
        gameInput.OnStartMoveFurnitureAction -= HandleStartMove;
        gameInput.OnPlaceFurnitureAction -= HandlePlaceFurniture;

        gameInput.OnRotateLeftAction += OnRotateLeft;
        gameInput.OnRotateRightAction += OnRotateRight;
    }

    private void Start()
    {
        pickUp = FindObjectOfType<PickUp>();
    }

    private void Update()
    {
        // Completely prevent all moving logic while holding a book
        if (pickUp != null && pickUp.IsHoldingObject())
            return;

        if (!isMoving)
            return;

        UpdateGhostPosition();
        HandleRotationInput();
    }


    private void HandleStartMove(object sender, EventArgs e)
    {
        if (pickUp != null && pickUp.IsHoldingObject()) return;

        if (isMoving || !TryFindFurniture(out selectedFurniture)) return;

        isMoving = true;
        ghostVisual = selectedFurniture.transform.Find("Ghost")?.gameObject;

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
        Vector3 correctPosition = new Vector3(ghostPos.x, -bottomOffset, ghostPos.z);

        selectedFurniture.transform.position = correctPosition;

        selectedFurniture.transform.rotation = ghostVisual.transform.rotation;

        if (originalRenderers != null)
        {
            foreach (var rend in originalRenderers)
            {
                if (rend != null)
                    rend.enabled = true;
            }
        }

        ghostVisual.SetActive(false);

        selectedFurniture = null;
        ghostVisual = null;
        ghostRenderer = null;
        rotationAmount = 0f;
        isMoving = false;
    }

    private bool TryFindFurniture(out GameObject furniture)
    {
        furniture = null;
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f))
        {
            if (hit.collider.CompareTag("Furniture"))
            {
                furniture = hit.collider.gameObject;
                return true;
            }
        }
        return false;
    }

    private void UpdateGhostPosition()
    {
        if (ghostVisual == null || selectedFurniture == null) return;

        Vector3 targetPos = playerCamera.position + playerCamera.forward * moveDistance;
        targetPos.y = 0f;
        selectedFurniture.transform.position = targetPos;

        Quaternion targetRot = Quaternion.Euler(0f, rotationAmount, 0f);
        selectedFurniture.transform.rotation = targetRot;

        // Apply precomputed ghost offset to keep visual on the ground
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

        // Get combined bounds of all renderers (excluding ghost itself)
        Bounds bounds = new Bounds(selectedFurniture.transform.position, Vector3.zero);
        Renderer[] renderers = selectedFurniture.GetComponentsInChildren<Renderer>();

        bool hasValidRenderer = false;
        foreach (Renderer rend in renderers)
        {
            if (rend.gameObject == ghostVisual) continue;
            bounds.Encapsulate(rend.bounds);
            hasValidRenderer = true;
        }

        if (!hasValidRenderer)
            return false;

        Vector3 center = bounds.center;
        Vector3 halfExtents = bounds.extents;

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            selectedFurniture.transform.rotation,
            placementObstacles,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in hits)
        {
            if (hit.transform.root != selectedFurniture.transform)
            {
                return false; // Colliding with something that’s not this furniture
            }
        }

        return true;
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

}
