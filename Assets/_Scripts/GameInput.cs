using System.Collections;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;
using System;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GameInput : MonoBehaviour
{
    public static GameInput Instance { get; private set; }

    public event EventHandler OnPickUpObjectAction;
    public event EventHandler OnShelveObjectAction;
    public event EventHandler<InputAction.CallbackContext> OnScrollRotate;
    public event EventHandler OnStartMoveFurnitureAction;
    public event EventHandler OnPlaceFurnitureAction;
    public event EventHandler OnRotateLeftAction;
    public event EventHandler OnRotateRightAction;
    public event EventHandler OnInteractAction;
    public event EventHandler OnRunAction;

    public event Action OnMoveFurniturePressed;
    public event Action OnMoveFurnitureReleased;
    public event Action OnCancelMove;


    [SerializeField] private PlayerInput playerInput;
    public bool IsGamepadActive => playerInput != null && playerInput.currentControlScheme == "Gamepad";
    public bool IsKeyboardMouseActive => playerInput != null && playerInput.currentControlScheme == "KeyboardMouse";


    private enum ControlType { KeyboardMouse, Gamepad }
    private ControlType lastUsedControlType = ControlType.KeyboardMouse;

    public event Action<string> OnControlSchemeChanged;


    private PlayerInputActions playerInputActions;

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;

        playerInput = GetComponent<PlayerInput>();

        if (playerInput == null)
        {
            Debug.LogError("PlayerInput component not found!");
            return;
        }

        Debug.Log("Initial scheme: " + playerInput.currentControlScheme);

        playerInput.onControlsChanged += OnControlsChanged;

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();

        playerInputActions.Player.PickUpObject.performed += PickUpObject_performed;
        playerInputActions.Player.ShelveObject.performed += ShelveObject_performed;
        playerInputActions.Player.ScrollRotate.performed += ScrollRotate_performed;
        playerInputActions.Player.RotateLeft.performed += RotateLeft_performed;
        playerInputActions.Player.RotateRight.performed += RotateRight_performed;
        playerInputActions.Player.Interact.performed += Interact_performed;
        playerInputActions.Player.Run.performed += Run_performed;

        playerInputActions.Player.MoveFurniture.started += ctx => OnMoveFurniturePressed?.Invoke();
        playerInputActions.Player.MoveFurniture.canceled += ctx => OnMoveFurnitureReleased?.Invoke();
        playerInputActions.Player.Cancel.performed += ctx => OnCancelMove?.Invoke();

        playerInputActions.Player.MoveFurniture.performed += MoveFurniture_performed;

    }

    private void OnDestroy()
    {
        playerInputActions.Player.PickUpObject.performed -= PickUpObject_performed;
        playerInputActions.Player.ShelveObject.performed -= ShelveObject_performed;
        playerInputActions.Player.ScrollRotate.performed -= ScrollRotate_performed;
        playerInputActions.Player.MoveFurniture.performed -= MoveFurniture_performed;
        playerInputActions.Player.RotateLeft.performed -= RotateLeft_performed;
        playerInputActions.Player.RotateRight.performed -= RotateRight_performed;
        playerInputActions.Player.Interact.performed -= Interact_performed;
        playerInputActions.Player.Run.performed -= Run_performed;

        playerInputActions.Player.Cancel.performed -= ctx => OnCancelMove?.Invoke();

        playerInputActions.Dispose();
    }


    private void MoveFurniture_performed(InputAction.CallbackContext context)
    {
        OnPlaceFurnitureAction?.Invoke(this, EventArgs.Empty);
    }

    private void PickUpObject_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnPickUpObjectAction?.Invoke(this, EventArgs.Empty);
    }

    private void ShelveObject_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnShelveObjectAction?.Invoke(this, EventArgs.Empty);
    }

    private void RotateLeft_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnRotateLeftAction?.Invoke(this, EventArgs.Empty);
    }

    private void RotateRight_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnRotateRightAction?.Invoke(this, EventArgs.Empty);
    }
    private void ScrollRotate_performed(InputAction.CallbackContext context)
    {
        OnScrollRotate?.Invoke(this, context);
    }

    private void Interact_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnInteractAction?.Invoke(this, EventArgs.Empty);
    }

    private void Run_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnRunAction?.Invoke(this, EventArgs.Empty);
    }

    public Vector2 GetMovementVectorNormalized()
    {
        Vector2 input = playerInputActions.Player.Move.ReadValue<Vector2>();

        if (Gamepad.current != null && input.sqrMagnitude > 0.01f)
        {
            lastUsedControlType = ControlType.Gamepad;
        }

        return input.normalized;
    }


    public Vector2 GetMouseDelta()
    {
        Vector2 input = playerInputActions.Player.Look.ReadValue<Vector2>();

        if (Mouse.current != null && input.sqrMagnitude > 0.01f)
        {
            lastUsedControlType = ControlType.KeyboardMouse;
        }

        return input;
    }

    public bool IsPrecisionModifierHeld()
    {
        return playerInputActions.Player.PrecisionModifier.IsPressed();
    }

    public bool IsGamepad()
    {
        return Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;
    }

    public bool IsRightTriggerHeld()
    {
        return playerInputActions.Player.Aim.IsPressed(); // Assuming 'Aim' is mapped to RightTrigger
    }

    public bool IsRunHeld()
    {
        return playerInputActions.Player.Run.IsPressed();
    }

    private void OnControlsChanged(PlayerInput input)
    {
        //Debug.Log("Control Scheme Changed To: " + input.currentControlScheme);
        OnControlSchemeChanged?.Invoke(input.currentControlScheme);
    }

    public bool IsUsingGamepad() => IsGamepadActive;


}
