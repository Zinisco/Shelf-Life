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



    private PlayerInputActions playerInputActions;

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;

        playerInputActions = new PlayerInputActions();

        playerInputActions.Player.Enable();

        playerInputActions.Player.PickUpObject.performed += PickUpObject_performed;
        playerInputActions.Player.ShelveObject.performed += ShelveObject_performed;
        playerInputActions.Player.ScrollRotate.performed += ScrollRotate_performed;
        playerInputActions.Player.MoveFurniture.started += MoveFurniture_started;
        playerInputActions.Player.MoveFurniture.performed += MoveFurniture_performed;
        playerInputActions.Player.RotateLeft.performed += RotateLeft_performed;
        playerInputActions.Player.RotateRight.performed += RotateRight_performed;


    }

    private void OnDestroy()
    {
        playerInputActions.Player.PickUpObject.performed -= PickUpObject_performed;
        playerInputActions.Player.ShelveObject.performed -= ShelveObject_performed;
        playerInputActions.Player.ScrollRotate.performed -= ScrollRotate_performed;
        playerInputActions.Player.MoveFurniture.started -= MoveFurniture_started;
        playerInputActions.Player.MoveFurniture.performed -= MoveFurniture_performed;
        playerInputActions.Player.RotateLeft.performed -= RotateLeft_performed;
        playerInputActions.Player.RotateRight.performed -= RotateRight_performed;



        playerInputActions.Dispose();
    }

    private void MoveFurniture_started(InputAction.CallbackContext context)
    {
        OnStartMoveFurnitureAction?.Invoke(this, EventArgs.Empty); // Hold initiated
    }

    private void MoveFurniture_performed(InputAction.CallbackContext context)
    {
        OnPlaceFurnitureAction?.Invoke(this, EventArgs.Empty); // Press again to place
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

    public Vector2 GetMovementVectorNormalized()
    {
        Vector2 inputVector = playerInputActions.Player.Move.ReadValue<Vector2>();

        inputVector = inputVector.normalized;

        return inputVector;
    }

    public Vector2 GetMouseDelta()
    {
        Vector2 inputLookVector = playerInputActions.Player.Look.ReadValue<Vector2>();

        return inputLookVector;
    }

    public bool IsPrecisionModifierHeld()
    {
        return playerInputActions.Player.PrecisionModifier.IsPressed();
    }

}
