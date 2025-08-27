using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GeneralSettingsController : MonoBehaviour
{
    [Header("Refs (optional in Main Menu)")]
    [SerializeField] private Camera playerCamera;            // can be null in Main Menu
    [SerializeField] private PlayerMovement playerMovement;  // can be null in Main Menu

    [Header("FOV")]
    [SerializeField] private Slider fovSlider;
    [SerializeField] private TMP_InputField fovInput;

    [Header("Mouse Sensitivity")]
    [SerializeField] private Slider mouseSlider;
    [SerializeField] private TMP_InputField mouseInput;

    [Header("Controller Sensitivity")]
    [SerializeField] private Slider controllerSlider;
    [SerializeField] private TMP_InputField controllerInput;

    [Header("Invert")]
    [SerializeField] private TMP_Dropdown invertXDropdown; // 0 = No, 1 = Yes
    [SerializeField] private TMP_Dropdown invertYDropdown; // 0 = No, 1 = Yes

    // --- Keys ---
    const string KEY_FOV = "FOV";
    const string KEY_MSENS = "MouseSensitivity";
    const string KEY_CSENS = "ControllerSensitivity";
    const string KEY_INVX = "InvertX";
    const string KEY_INVY = "InvertY";

    // --- Defaults ---
    const float DEFAULT_FOV = 60f;
    const float DEFAULT_MSENS = 0.20f;
    const float DEFAULT_CSENS = 2.00f;

    // --- Ranges ---
    const float MIN_FOV = 40f;
    const float MAX_FOV = 120f;

    const float MIN_MSENS = 0.05f;
    const float MAX_MSENS = 2.00f;

    const float MIN_CSENS = 0.50f;
    const float MAX_CSENS = 10.0f;

    void Awake()
    {
        // 1) Configure sliders’ ranges (and optional stepping)
        SetupSlider(fovSlider, MIN_FOV, MAX_FOV, wholeNumbers: false);
        SetupSlider(mouseSlider, MIN_MSENS, MAX_MSENS, wholeNumbers: false);
        SetupSlider(controllerSlider, MIN_CSENS, MAX_CSENS, wholeNumbers: false);

        // 2) Configure dropdowns (No/Yes)
        SetupYesNoDropdown(invertXDropdown);
        SetupYesNoDropdown(invertYDropdown);

        // 3) Load saved values (clamped to ranges)
        float fov = Clamp(PlayerPrefs.GetFloat(KEY_FOV, DEFAULT_FOV), MIN_FOV, MAX_FOV);
        float ms = Clamp(PlayerPrefs.GetFloat(KEY_MSENS, DEFAULT_MSENS), MIN_MSENS, MAX_MSENS);
        float cs = Clamp(PlayerPrefs.GetFloat(KEY_CSENS, DEFAULT_CSENS), MIN_CSENS, MAX_CSENS);
        bool invX = PlayerPrefs.GetInt(KEY_INVX, 0) == 1;
        bool invY = PlayerPrefs.GetInt(KEY_INVY, 0) == 1;

        // 4) Push to UI (without triggering listeners)
        SetLinked(fovSlider, fovInput, fov, roundInt: false);
        SetLinked(mouseSlider, mouseInput, ms, roundInt: false);
        SetLinked(controllerSlider, controllerInput, cs, roundInt: false);
        invertXDropdown.SetValueWithoutNotify(invX ? 1 : 0);
        invertYDropdown.SetValueWithoutNotify(invY ? 1 : 0);

        // 5) Hook UI events
        fovSlider.onValueChanged.AddListener(v => {
            v = Clamp(v, MIN_FOV, MAX_FOV);
            SyncInput(fovInput, v, roundInt: false);
            ApplyFOV(v); SaveFOV(v);
        });
        fovInput.onEndEdit.AddListener(s => {
            if (TryParseClamped(s, MIN_FOV, MAX_FOV, out var v))
            {
                SyncSlider(fovSlider, v);
                ApplyFOV(v); SaveFOV(v);
            }
        });

        mouseSlider.onValueChanged.AddListener(v => {
            v = Clamp(v, MIN_MSENS, MAX_MSENS);
            SyncInput(mouseInput, v, roundInt: false);
            ApplyMouse(v); SaveMouse(v);
        });
        mouseInput.onEndEdit.AddListener(s => {
            if (TryParseClamped(s, MIN_MSENS, MAX_MSENS, out var v))
            {
                SyncSlider(mouseSlider, v);
                ApplyMouse(v); SaveMouse(v);
            }
        });

        controllerSlider.onValueChanged.AddListener(v => {
            v = Clamp(v, MIN_CSENS, MAX_CSENS);
            SyncInput(controllerInput, v, roundInt: false);
            ApplyController(v); SaveController(v);
        });
        controllerInput.onEndEdit.AddListener(s => {
            if (TryParseClamped(s, MIN_CSENS, MAX_CSENS, out var v))
            {
                SyncSlider(controllerSlider, v);
                ApplyController(v); SaveController(v);
            }
        });

        invertXDropdown.onValueChanged.AddListener(_ => { ApplyInvert(); SaveInvert(); });
        invertYDropdown.onValueChanged.AddListener(_ => { ApplyInvert(); SaveInvert(); });

        // 6) Apply to live objects if present
        ApplyFOV(fov);
        ApplyMouse(ms);
        ApplyController(cs);
        ApplyInvert();
    }

    // --- Apply ---

    private void ApplyFOV(float v)
    {
        if (playerCamera) playerCamera.fieldOfView = Clamp(v, MIN_FOV, MAX_FOV);
    }

    private void ApplyMouse(float v)
    {
        if (playerMovement) playerMovement.MouseSensitivity = Mathf.Max(MIN_MSENS, v);
    }

    private void ApplyController(float v)
    {
        if (playerMovement) playerMovement.ControllerSensitivity = Mathf.Max(MIN_CSENS, v);
    }

    private void ApplyInvert()
    {
        bool invX = invertXDropdown.value == 1;
        bool invY = invertYDropdown.value == 1;
        if (playerMovement)
        {
            playerMovement.InvertX = invX;
            playerMovement.InvertY = invY;
        }
    }

    // --- Save ---

    private void SaveFOV(float v) { PlayerPrefs.SetFloat(KEY_FOV, Clamp(v, MIN_FOV, MAX_FOV)); }
    private void SaveMouse(float v) { PlayerPrefs.SetFloat(KEY_MSENS, Clamp(v, MIN_MSENS, MAX_MSENS)); }
    private void SaveController(float v) { PlayerPrefs.SetFloat(KEY_CSENS, Clamp(v, MIN_CSENS, MAX_CSENS)); }
    private void SaveInvert()
    {
        PlayerPrefs.SetInt(KEY_INVX, invertXDropdown.value == 1 ? 1 : 0);
        PlayerPrefs.SetInt(KEY_INVY, invertYDropdown.value == 1 ? 1 : 0);
        PlayerPrefs.Save();
    }

    // --- UI helpers ---

    private static void SetupSlider(Slider s, float min, float max, bool wholeNumbers)
    {
        if (!s) return;
        s.minValue = min;
        s.maxValue = max;
        s.wholeNumbers = wholeNumbers;
    }

    private static void SetupYesNoDropdown(TMP_Dropdown dd)
    {
        if (!dd) return;
        dd.ClearOptions();
        dd.AddOptions(new System.Collections.Generic.List<string> { "No", "Yes" });
    }

    private static void SetLinked(Slider s, TMP_InputField i, float v, bool roundInt)
    {
        if (s) s.SetValueWithoutNotify(v);
        if (i) i.SetTextWithoutNotify(roundInt ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##"));
    }

    private static void SyncInput(TMP_InputField i, float v, bool roundInt)
    {
        if (i) i.SetTextWithoutNotify(roundInt ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##"));
    }

    private static void SyncSlider(Slider s, float v)
    {
        if (s) s.SetValueWithoutNotify(v);
    }

    private static bool TryParseClamped(string s, float min, float max, out float v)
    {
        if (float.TryParse(s, out v))
        {
            v = Clamp(v, min, max);
            return true;
        }
        v = min;
        return false;
    }

    private static float Clamp(float v, float min, float max) => Mathf.Clamp(v, min, max);
}
