using UnityEngine;

public class SettingsApplier : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;            // assign in Game scene
    [SerializeField] private PlayerMovement playerMovement;  // assign in Game scene

    // Must match your keys/ranges
    const string KEY_FOV = "FOV";
    const string KEY_MS = "MouseSensitivity";
    const string KEY_CS = "ControllerSensitivity";
    const string KEY_INVX = "InvertX";
    const string KEY_INVY = "InvertY";

    const float MIN_FOV = 40f, MAX_FOV = 120f;
    const float MIN_MS = 0.05f, MAX_MS = 2.0f;
    const float MIN_CS = 0.50f, MAX_CS = 10f;

    void Awake()
    {
        // Make sure defaults exist
        SettingsBootstrap.EnsureDefaultsSaved();

        // Read + clamp
        float fov = Mathf.Clamp(PlayerPrefs.GetFloat(KEY_FOV), MIN_FOV, MAX_FOV);
        float ms = Mathf.Clamp(PlayerPrefs.GetFloat(KEY_MS), MIN_MS, MAX_MS);
        float cs = Mathf.Clamp(PlayerPrefs.GetFloat(KEY_CS), MIN_CS, MAX_CS);
        bool invX = PlayerPrefs.GetInt(KEY_INVX) == 1;
        bool invY = PlayerPrefs.GetInt(KEY_INVY) == 1;

        // Apply
        if (playerCamera) playerCamera.fieldOfView = fov;
        if (playerMovement)
        {
            playerMovement.MouseSensitivity = ms;
            playerMovement.ControllerSensitivity = cs;
            playerMovement.InvertX = invX;
            playerMovement.InvertY = invY;
        }
    }
}
