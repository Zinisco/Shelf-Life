using UnityEngine;
using UnityEngine.InputSystem;

public class SaveLoadController : MonoBehaviour
{
    private InputAction loadAction;
    private InputAction saveAction;

    private void OnEnable()
    {
        loadAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/l");
        saveAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/k");

        loadAction.performed += OnLoad;
        saveAction.performed += OnSave;

        loadAction.Enable();
        saveAction.Enable();
    }

    private void OnDisable()
    {
        if (loadAction != null)
        {
            loadAction.performed -= OnLoad;
            loadAction.Disable();
        }

        if (saveAction != null)
        {
            saveAction.performed -= OnSave;
            saveAction.Disable();
        }
    }

    private void OnLoad(InputAction.CallbackContext context)
    {
        Debug.Log("Loading books...");
        BookSaveManager.LoadBooks();
    }

    private void OnSave(InputAction.CallbackContext context)
    {
        Debug.Log("Saving books...");
        BookSaveManager.SaveBooks();
    }
}
