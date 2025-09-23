// PlantMover.cs
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlantMover : MonoBehaviour
{
    public enum PlantType { Table, Floor }

    [Header("Layers & Placement")]
    [SerializeField] private LayerMask tableLayer;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask placementObstacles;
    [SerializeField] private float maxPlaceDistance = 3f;

    [Header("Visuals")]
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;
    [SerializeField] private Image progressRingUI;

    [Header("Input & Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameInput gameInput;
    [SerializeField] private float holdTime = 1.5f;

    private FurnitureMover furnitureMover;
    private GameObject selectedPlant;
    private GameObject ghost;
    private Renderer[] ghostRenderers;
    private Transform originalParent;

    private PlantType currentType;
    private float currentRotation = 0f;

    private bool isMoving = false;
    private bool isHoldingToMove = false;
    private float holdTimer = 0f;

    private void OnEnable()
    {
        gameInput.OnFreeMoveStarted += HandleMovePressed;
        gameInput.OnFreeMoveCanceled += HandleCancelInput;
        gameInput.OnFreeMovePerformed += HandleConfirm;
        gameInput.OnRotateLeftAction += OnRotateLeft;
        gameInput.OnRotateRightAction += OnRotateRight;
        gameInput.OnCancel += HandleCancel;
    }

    private void OnDisable()
    {
        gameInput.OnFreeMoveStarted -= HandleMovePressed;
        gameInput.OnFreeMoveCanceled -= HandleCancelInput;
        gameInput.OnFreeMovePerformed -= HandleConfirm;
        gameInput.OnRotateLeftAction -= OnRotateLeft;
        gameInput.OnRotateRightAction -= OnRotateRight;
        gameInput.OnCancel -= HandleCancel;
    }

    private void Start()
    {
        furnitureMover = FindObjectOfType<FurnitureMover>();
    }


    private void Update()
    {
        if ((furnitureMover != null && furnitureMover.IsMovingFurniture()) ||
    PauseMenuController.Instance?.IsPaused == true)
        {
            return;
        }

        if (!isMoving && isHoldingToMove && TryFindPlant(out GameObject plantGO))
        {
            holdTimer -= Time.deltaTime;
            progressRingUI.fillAmount = 1f - (holdTimer / holdTime);
            progressRingUI.gameObject.SetActive(true);

            if (holdTimer <= 0f)
            {
                StartMove(plantGO);
                isHoldingToMove = false;
                progressRingUI.gameObject.SetActive(false);
            }
        }

        if (isMoving)
        {
            UpdateGhostPosition();
            HandleRotationInput();
        }
    }

    private void HandleMovePressed()
    {
        if (isMoving || isHoldingToMove) return;

        isHoldingToMove = true;
        holdTimer = holdTime;
        progressRingUI.fillAmount = 0f;
        progressRingUI.gameObject.SetActive(true);
    }

    private void HandleCancelInput()
    {
        if (!isMoving) CancelHold();
    }

    private void CancelHold()
    {
        isHoldingToMove = false;
        holdTimer = holdTime;
        progressRingUI.fillAmount = 0f;
        progressRingUI.gameObject.SetActive(false);
    }

    private void StartMove(GameObject plantGO)
    {
        selectedPlant = plantGO;

        var navObstacle = selectedPlant.GetComponent<MovableNavMeshObstacle>();
        if (navObstacle) navObstacle.OnPickup();

        MovablePlant plant = selectedPlant.GetComponent<MovablePlant>();
        currentType = plant.Type;
        ghost = plant.GetGhostVisual();
        originalParent = selectedPlant.transform.parent;

        TogglePlantRenderers(false);
        ghost.SetActive(true);
        ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
        isMoving = true;
    }

    private void HandleConfirm(InputAction.CallbackContext ctx)
    {
        if (!isMoving || !IsValidPlacement(out RaycastHit hit)) return;

        selectedPlant.transform.position = hit.point;
        selectedPlant.transform.rotation = Quaternion.Euler(0f, currentRotation, 0f);
        selectedPlant.transform.SetParent(currentType == PlantType.Table ? hit.collider.transform : originalParent);

        var navObstacle = selectedPlant.GetComponent<MovableNavMeshObstacle>();
        if (navObstacle) navObstacle.OnPlace();

        TogglePlantRenderers(true);
        ghost.SetActive(false);
        selectedPlant = null;
        isMoving = false;
        PauseMenuController.Instance?.BlockPauseFor(0.1f);
    }

    private void HandleCancel()
    {
        if (!isMoving)
        {
            CancelHold();
            return;
        }

        var navObstacle = selectedPlant.GetComponent<MovableNavMeshObstacle>();
        if (navObstacle) navObstacle.OnPlace();

        TogglePlantRenderers(true);
        ghost.SetActive(false);
        selectedPlant = null;
        ghost = null;
        ghostRenderers = null;
        currentRotation = 0f;
        isMoving = false;
        CancelHold();
        PauseMenuController.Instance?.BlockPauseFor(0.1f);
    }

    private void TogglePlantRenderers(bool enabled)
    {
        foreach (var r in selectedPlant.GetComponentsInChildren<Renderer>())
            r.enabled = enabled;
        foreach (var c in selectedPlant.GetComponentsInChildren<Collider>())
            c.enabled = enabled;
    }

    private bool TryFindPlant(out GameObject plantGO)
    {
        plantGO = null;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxPlaceDistance))
        {
            var plant = hit.collider.GetComponentInParent<MovablePlant>();
            if (plant != null && plant.CanMove())
            {
                plantGO = plant.gameObject;
                return true;
            }
        }
        return false;
    }

    private void UpdateGhostPosition()
    {
        Ray ray;
        LayerMask snapLayer = currentType == PlantType.Floor ? groundLayer : tableLayer;

        if (currentType == PlantType.Floor)
        {
            Vector3 forwardPos = playerCamera.transform.position + playerCamera.transform.forward * maxPlaceDistance;
            Vector3 rayOrigin = forwardPos + Vector3.up * 2f;
            ray = new Ray(rayOrigin, Vector3.down);
        }
        else
        {
            ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        }

        if (Physics.Raycast(ray, out RaycastHit hit, maxPlaceDistance, snapLayer))
        {
            ghost.transform.position = hit.point + Vector3.up * 0.01f;
            ghost.transform.rotation = Quaternion.Euler(0f, currentRotation, 0f);
            ghost.SetActive(true);

            bool canPlace = CanPlaceGhost();
            SetGhostMaterial(canPlace ? validMaterial : invalidMaterial);
        }
        else
        {
            ghost.transform.position = playerCamera.transform.position + playerCamera.transform.forward * maxPlaceDistance;
            ghost.SetActive(true);
            SetGhostMaterial(invalidMaterial);
        }
    }

    private bool CanPlaceGhost()
    {
        if (ghost == null || selectedPlant == null)
            return false;

        Bounds bounds = new Bounds(ghost.transform.position, Vector3.zero);
        Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>();

        foreach (Renderer rend in renderers)
        {
            bounds.Encapsulate(rend.bounds);
        }

        Vector3 center = bounds.center;
        Vector3 halfExtents = bounds.extents;

        Collider[] overlaps = Physics.OverlapBox(
            center,
            halfExtents,
            ghost.transform.rotation,
            placementObstacles,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in overlaps)
        {
            if (!hit.transform.IsChildOf(selectedPlant.transform) &&
                !hit.transform.IsChildOf(ghost.transform))
                return false;
        }

        return true;
    }

    private bool IsValidPlacement(out RaycastHit hit)
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        LayerMask mask = currentType == PlantType.Floor ? groundLayer : tableLayer;
        if (Physics.Raycast(ray, out hit, maxPlaceDistance, mask))
        {
            return IsSurfaceFlat(hit.normal) && CanPlaceGhost();
        }
        return false;
    }

    private bool IsSurfaceFlat(Vector3 normal)
    {
        return Vector3.Angle(normal, Vector3.up) < 10f;
    }

    private void SetGhostMaterial(Material mat)
    {
        foreach (var rend in ghostRenderers)
        {
            if (rend == null) continue;

            Material[] ghostMats = new Material[rend.materials.Length];
            for (int i = 0; i < ghostMats.Length; i++)
                ghostMats[i] = mat;

            rend.materials = ghostMats;
        }
    }

    private void OnRotateLeft(object sender, EventArgs e)
    {
        float step = gameInput.IsPrecisionModifierHeld() ? 15f : 90f;
        currentRotation = (currentRotation - step) % 360f;
    }

    private void OnRotateRight(object sender, EventArgs e)
    {
        float step = gameInput.IsPrecisionModifierHeld() ? 15f : 90f;
        currentRotation = (currentRotation + step) % 360f;
    }

    private void HandleRotationInput()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        float step = gameInput.IsPrecisionModifierHeld() ? 15f : 90f;
        currentRotation += scroll > 0 ? step : -step;
        currentRotation %= 360f;
    }

    public bool IsMovingPlant()
    {
        return isMoving;
    }


}