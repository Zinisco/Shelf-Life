using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private PickUp pickUp;

    [Header("Game Input")]
    [SerializeField] private GameInput gameInput;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runMultiplier = 1.5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float groundCheckDistance = 0.4f;
    [SerializeField] private LayerMask groundMask;

    [Header("Look Settings")]
    [SerializeField] private float lookSensitivity = 0.2f;     // Mouse sensitivity
    [SerializeField] private float controllerLookSensitivity = 0.7f; // Gamepad sensitivity
    [SerializeField] private Transform cam;
    [SerializeField] private float aimAssistRange = 5f;
    [SerializeField] private float aimAssistStrength = 2f; // Higher = faster adjustment
    [SerializeField] private LayerMask bookLayerMask;

    // Exposed so settings menu can change them
    public float MouseSensitivity { get => lookSensitivity; set => lookSensitivity = value; }
    public float ControllerSensitivity { get => controllerLookSensitivity; set => controllerLookSensitivity = value; }
    public bool InvertX { get; set; } = false;
    public bool InvertY { get; set; } = false;

    private CharacterController controller;
    private float verticalVelocity;
    private float xRotation = 0f;

    private bool isGrounded;
    private Vector3 groundCheckOffset = new Vector3(0, -0.5f, 0); // Adjust if needed

    public bool IsLocked { get; set; } = false;

    private void Start()
    {
        pickUp = FindObjectOfType<PickUp>();
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Optional: listen to scheme changes if you need debug logs, but no more caching
        GameInput.Instance.OnControlSchemeChanged += OnControlSchemeChanged;
    }

    private void Update()
    {
        // Allow Escape to close UI even when locked
        if (IsLocked && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (GameObject.FindObjectOfType<ComputerUI>() is ComputerUI ui)
            {
                ui.ToggleUI(false);
                ExitUI();
            }
        }

        // Block movement/look when locked
        if (IsLocked) return;

        HandleLook();
        HandleMovement();

        if (gameInput.IsRightTriggerHeld() && pickUp != null && !pickUp.IsHoldingObject())
        {
            ApplySoftAimAssist();
        }
    }

    private void HandleMovement()
    {
        isGrounded = Physics.CheckSphere(transform.position + groundCheckOffset, groundCheckDistance, groundMask);

        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector2 inputVector = gameInput.GetMovementVectorNormalized();
        Vector3 move = transform.right * inputVector.x + transform.forward * inputVector.y;

        float currentSpeed = moveSpeed;

        bool isMovingFurniture = FindObjectOfType<FurnitureMover>()?.IsMovingFurniture() ?? false;
        bool isRunInputHeld = gameInput.IsRunHeld();
        bool canRun = isRunInputHeld && !isMovingFurniture;

        if (canRun) currentSpeed *= runMultiplier;

        controller.Move((move * currentSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    private void HandleLook()
    {
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused)
            return;

        Vector2 mouseDelta = gameInput.GetMouseDelta();
        mouseDelta = Vector2.ClampMagnitude(mouseDelta, 10f);

        // Get sensitivity dynamically — no caching
        float sensitivity = GameInput.Instance.IsGamepadActive ? controllerLookSensitivity : lookSensitivity;
        mouseDelta *= sensitivity;

        // Horizontal (yaw)
        float sx = InvertX ? -1f : 1f;
        transform.Rotate(Vector3.up * (sx * mouseDelta.x));

        // Vertical (pitch)
        float sy = InvertY ? 1f : -1f;
        xRotation += sy * mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    public void ExitUI()
    {
        IsLocked = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDestroy()
    {
        if (GameInput.Instance != null)
            GameInput.Instance.OnControlSchemeChanged -= OnControlSchemeChanged;
    }

    private void OnControlSchemeChanged(string scheme)
    {
        // Optional debug feedback — not needed for sensitivity anymore
        Debug.Log($"Control scheme changed: {scheme}");
    }

    private void ApplySoftAimAssist()
    {
        Vector3 origin = cam.position - cam.forward * 0.1f;
        Vector3 direction = cam.forward;
        float sphereRadius = 0.5f;

        if (Physics.SphereCast(origin, sphereRadius, direction, out RaycastHit hit, aimAssistRange, bookLayerMask))
        {
            if (hit.collider.CompareTag("Pickable"))
            {
                Vector3 targetDirection = (hit.point - cam.position).normalized;

                // Y-axis (yaw)
                Vector3 flatDirection = new Vector3(targetDirection.x, 0f, targetDirection.z);
                Quaternion targetYRotation = Quaternion.LookRotation(flatDirection);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetYRotation,
                    aimAssistStrength * Time.deltaTime * 100f);

                // X-axis (pitch)
                float pitch = -Mathf.Asin(targetDirection.y) * Mathf.Rad2Deg;
                xRotation = Mathf.MoveTowards(xRotation, pitch, aimAssistStrength * Time.deltaTime * 100f);
                cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            }
        }

        Debug.DrawRay(origin, direction * aimAssistRange, Color.green);
    }

    public float GetCameraPitch() => xRotation;

    /// <summary>Teleport safely with the CharacterController.</summary>
    public void Teleport(Vector3 pos, float yawDegrees, float pitchDegrees)
    {
        if (!controller) controller = GetComponent<CharacterController>();

        bool wasEnabled = controller.enabled;
        controller.enabled = false;

        transform.position = pos;
        transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

        xRotation = pitchDegrees;
        if (cam) cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        verticalVelocity = 0f;
        Physics.SyncTransforms();

        controller.enabled = wasEnabled;
    }
}
