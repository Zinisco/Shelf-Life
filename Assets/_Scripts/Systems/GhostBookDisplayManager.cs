using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the ghost visual for placing Book Display objects (e.g., featured display stands).
/// </summary>
public class GhostBookDisplayManager : MonoBehaviour
{
    [SerializeField] private GameInput gameInput;

    [Header("Ghost Display Settings")]
    [SerializeField] private GameObject ghostDisplayPrefab;
    [SerializeField] private float surfaceOffset = 0.01f;
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    [SerializeField] private float rotationSmoothSpeed = 10f;

    private GameObject ghostInstance;
    private Renderer[] ghostRenderers;
    private bool isPlacementValid = false;
    private float currentYRotation = 0f;
    private float rotationAmount = 0f;

    [Header("Tracking Held Object")]
    [SerializeField] private GameObject heldObject;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask validSurfaceMask;

    private void OnEnable()
    {
        if (gameInput == null)
        {
            Debug.LogError("GameInput is not assigned to FurnitureMover!");
            return;
        }

        gameInput.OnRotateLeftAction += OnRotateLeft;
        gameInput.OnRotateRightAction += OnRotateRight;
        ;
    }

    private void OnDisable()
    {
        gameInput.OnRotateLeftAction -= OnRotateLeft;
        gameInput.OnRotateRightAction -= OnRotateRight;
    }

    private void Update()
    {
        if (heldObject == null) return;
        UpdateGhost(heldObject, playerCamera, validSurfaceMask);

        HandleRotationInput();
    }

    public void Init()
    {
        if (ghostInstance != null)
            Destroy(ghostInstance);

        ghostInstance = Instantiate(ghostDisplayPrefab);
        ghostInstance.SetActive(false);
        ghostRenderers = ghostInstance.GetComponentsInChildren<Renderer>(true);
    }

    public void UpdateGhost(GameObject held, Camera cam, LayerMask validMask)
    {
        if (!ghostInstance || !held) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f, validMask))
        {
            // Only accept surfaces that face upwards enough
            if (Vector3.Dot(hit.normal, Vector3.up) > 0.75f) // ~41 degrees from vertical
            {
                ghostInstance.transform.position = hit.point + Vector3.up * surfaceOffset;
                currentYRotation = Mathf.LerpAngle(currentYRotation, rotationAmount, Time.deltaTime * rotationSmoothSpeed);
                ghostInstance.transform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
                Quaternion targetRot = Quaternion.Euler(0f, currentYRotation, 0f);

                SetGhostMaterial(true);
                isPlacementValid = true;
                ghostInstance.SetActive(true);
            }
            else
            {
                // Invalid orientation
                SetGhostMaterial(false);
                isPlacementValid = false;
                ghostInstance.SetActive(false);
            }
        }

    }

    private void SetGhostMaterial(bool isValid)
    {
        if (ghostRenderers == null) return;
        Material mat = isValid ? validMaterial : invalidMaterial;

        foreach (var r in ghostRenderers)
        {
            if (r != null)
                r.material = mat;
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


    private bool IsShiftHeld()
    {
        return GameInput.Instance.IsPrecisionModifierHeld();
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

    public void HideGhost()
    {
        if (ghostInstance)
            ghostInstance.SetActive(false);
    }

    public Vector3 GetGhostPosition() => ghostInstance ? ghostInstance.transform.position : Vector3.zero;
    public Quaternion GetGhostRotation() => ghostInstance ? ghostInstance.transform.rotation : Quaternion.identity;
    public bool IsValidPlacement() => isPlacementValid;
    public GameObject GhostInstance => ghostInstance;

    // Allow setting externally
    public void SetHeldObject(GameObject obj)
    {
        heldObject = obj;
        if (obj == null)
        {
            HideGhost();
        }
    }


    public void SetCamera(Camera cam) => playerCamera = cam;
    public void SetValidMask(LayerMask mask) => validSurfaceMask = mask;
}
