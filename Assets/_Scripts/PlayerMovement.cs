using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Game Input")]
    [SerializeField] private GameInput gameInput;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Look Settings")]
    [SerializeField] private float lookSensitivity = 0.2f;
    [SerializeField] private Transform cam;

    private CharacterController controller;
    private float verticalVelocity;
    private float xRotation = 0f;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;  // Lock cursor for FPS
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleMovement();
        HandleLook();
    }

    void HandleMovement()
    {
        Vector2 inputVector = gameInput.GetMovementVectorNormalized();

        Vector3 move = transform.right * inputVector.x + transform.forward * inputVector.y;

        if (!controller.isGrounded)
        {
            verticalVelocity += gravity * Time.deltaTime; // Apply gravity
        }
        else if (Input.GetButtonDown("Jump"))  // Check Jump
        {
            verticalVelocity = jumpForce;
        }

        controller.Move((move * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    void HandleLook()
    {
        Vector2 mouseDelta = gameInput.GetMouseDelta() * lookSensitivity;

        xRotation -= mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseDelta.x);
    }
}
