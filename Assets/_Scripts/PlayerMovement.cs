using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private float lookSensitivity = 0.2f;
    [SerializeField] private Transform cam;
    [SerializeField] private float controllerLookSensitivity = 3f;
    [SerializeField] private float aimAssistRange = 5f;
    [SerializeField] private float aimAssistStrength = 2f; // Higher = faster adjustment
    [SerializeField] private LayerMask bookLayerMask;

    private float currentSensitivity;

    private CharacterController controller;
    private float verticalVelocity;
    private float xRotation = 0f;

    private bool isGrounded;
    private Vector3 groundCheckOffset = new Vector3(0, -0.5f, 0); // Adjust if needed

    private void Start()
    {
        pickUp = FindObjectOfType<PickUp>(); // Or GetComponentInChildren/FindObjectOfType if needed
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        UpdateSensitivity(GameInput.Instance.IsGamepadActive ? "Gamepad" : "KeyboardMouse");

        GameInput.Instance.OnControlSchemeChanged += UpdateSensitivity;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();

        if (gameInput.IsRightTriggerHeld() && pickUp != null && !pickUp.IsHoldingObject())
        {
            ApplySoftAimAssist();
        }

        Debug.Log("Current Input: " + gameInput.IsUsingGamepad());

    }

    private void UpdateSensitivity(string controlScheme)
    {
        currentSensitivity = controlScheme == "Gamepad" ? controllerLookSensitivity : lookSensitivity;
        Debug.Log("Sensitivity updated to: " + currentSensitivity);
    }

    void HandleMovement()
    {
        isGrounded = Physics.CheckSphere(transform.position + groundCheckOffset, groundCheckDistance, groundMask);

        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // Small downward force to keep grounded
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector2 inputVector = gameInput.GetMovementVectorNormalized();
        Vector3 move = transform.right * inputVector.x + transform.forward * inputVector.y;

        float currentSpeed = moveSpeed;

        // Disable running if moving furniture
        bool isMovingFurniture = FindObjectOfType<FurnitureMover>()?.IsMovingFurniture() ?? false;
        bool canRun = gameInput.IsRunHeld() && !isMovingFurniture;

        if (canRun)
        {
            currentSpeed *= runMultiplier;
        }

        controller.Move((move * currentSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);

    }

    void HandleLook()
    {
        Vector2 mouseDelta = gameInput.GetMouseDelta();
        mouseDelta = Vector2.ClampMagnitude(mouseDelta, 10f);
        mouseDelta *= currentSensitivity;

        xRotation -= mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseDelta.x);
    }

    private void OnDestroy()
    {
        if (GameInput.Instance != null)
            GameInput.Instance.OnControlSchemeChanged -= UpdateSensitivity;
    }


    void ApplySoftAimAssist()
    {
        Vector3 origin = cam.position - cam.forward * 0.1f; // Offset back a bit
        Vector3 direction = cam.forward;
        float sphereRadius = 0.5f; // Adjust as needed for sensitivity

        // Perform a SphereCast to find nearby books
        if (Physics.SphereCast(origin, sphereRadius, direction, out RaycastHit hit, aimAssistRange, bookLayerMask))
        {
            if (hit.collider.CompareTag("Pickable"))
            {
                Vector3 targetDirection = (hit.point - cam.position).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

                // Y-axis rotation toward the book
                Vector3 flatDirection = new Vector3(targetDirection.x, 0f, targetDirection.z);
                Quaternion targetYRotation = Quaternion.LookRotation(flatDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetYRotation, aimAssistStrength * Time.deltaTime * 100f);

                // X-axis (pitch) look up/down
                float pitch = -Mathf.Asin(targetDirection.y) * Mathf.Rad2Deg;
                xRotation = Mathf.MoveTowards(xRotation, pitch, aimAssistStrength * Time.deltaTime * 100f);
                cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);


            }
        }

        Debug.DrawRay(origin, direction * aimAssistRange, Color.green);

    }

}
