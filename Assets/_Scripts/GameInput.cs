using System.Collections;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;
using System;
using UnityEngine.EventSystems;

public class GameInput : MonoBehaviour
{
    public static GameInput Instance { get; private set; }

    public event EventHandler OnPickUpObjectAction;
    public event EventHandler OnRotateObjectAction;
    public event EventHandler OnShelveObjectAction;

    private PlayerInputActions playerInputActions;

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;

        playerInputActions = new PlayerInputActions();

        playerInputActions.Player.Enable();

        playerInputActions.Player.PickUpObject.performed += PickUpObject_performed;
        playerInputActions.Player.RotateObject.performed += RotateObject_performed;
        playerInputActions.Player.ShelveObject.performed += ShelveObject_performed;
    }

    private void OnDestroy()
    {
        playerInputActions.Player.PickUpObject.performed -= PickUpObject_performed;
        playerInputActions.Player.RotateObject.performed -= RotateObject_performed;

        playerInputActions.Dispose();
    }

    private void PickUpObject_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnPickUpObjectAction?.Invoke(this, EventArgs.Empty);
    }

    private void RotateObject_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnRotateObjectAction?.Invoke(this, EventArgs.Empty);
    }

    private void ShelveObject_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        OnShelveObjectAction?.Invoke(this, EventArgs.Empty);
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
}
