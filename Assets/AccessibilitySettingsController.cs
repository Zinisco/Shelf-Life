using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

public class AccessibilitySettingsController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Dropdown subtitlesDropdown;
    [SerializeField] private TMP_Dropdown subtitleSizeDropdown;
    [SerializeField] private TMP_Dropdown colorblindDropdown;
    [SerializeField] private TMP_Dropdown motionBlurDropdown;

    [Header("Post Processing")]
    [SerializeField] private Volume globalVolume; // Assign your Global Volume in Inspector

    [SerializeField] private ColorblindManager colorblindManager;

    private MotionBlur motionBlur;

    // Keys
    const string KEY_SUBTITLES = "ACC_Subtitles";
    const string KEY_SUBSIZE = "ACC_SubtitleSize";
    const string KEY_COLORBLIND = "ACC_Colorblind";
    const string KEY_MOTIONBLUR = "ACC_MotionBlur";

    void Awake()
    {
        var profile = globalVolume.profile != null
    ? globalVolume.profile
    : globalVolume.sharedProfile;

        if (profile != null && profile.TryGet(out MotionBlur mb))
        {
            motionBlur = mb;
        }
 

        // Build dropdown options
        BuildSubtitleOptions();
        BuildSubtitleSizeOptions();
        BuildColorblindOptions();
        BuildMotionBlurOptions();

        // Load saved prefs
        int subtitles = PlayerPrefs.GetInt(KEY_SUBTITLES, 1);
        int size = PlayerPrefs.GetInt(KEY_SUBSIZE, 1);
        int colorblind = PlayerPrefs.GetInt(KEY_COLORBLIND, 0);
        int motion = PlayerPrefs.GetInt(KEY_MOTIONBLUR, 0); // default OFF

        // Set dropdowns without firing events
        subtitlesDropdown.SetValueWithoutNotify(subtitles);
        subtitleSizeDropdown.SetValueWithoutNotify(size);
        colorblindDropdown.SetValueWithoutNotify(colorblind);
        motionBlurDropdown.SetValueWithoutNotify(motion);

        // Hook listeners
        subtitlesDropdown.onValueChanged.AddListener(v => { PlayerPrefs.SetInt(KEY_SUBTITLES, v); PlayerPrefs.Save(); ApplySubtitles(v); });
        subtitleSizeDropdown.onValueChanged.AddListener(v => { PlayerPrefs.SetInt(KEY_SUBSIZE, v); PlayerPrefs.Save(); ApplySubtitleSize(v); });
        colorblindDropdown.onValueChanged.AddListener(v => { PlayerPrefs.SetInt(KEY_COLORBLIND, v); PlayerPrefs.Save(); ApplyColorblind(v); });
        motionBlurDropdown.onValueChanged.AddListener(v => { PlayerPrefs.SetInt(KEY_MOTIONBLUR, v); PlayerPrefs.Save(); ApplyMotionBlur(v); });

        // Apply current values
        ApplySubtitles(subtitles);
        ApplySubtitleSize(size);
        ApplyColorblind(colorblind);
        ApplyMotionBlur(motion);
    }

    // --- Build dropdown lists ---
    private void BuildSubtitleOptions()
    {
        subtitlesDropdown?.ClearOptions();
        subtitlesDropdown?.AddOptions(new System.Collections.Generic.List<string> { "Off", "On" });
    }

    private void BuildSubtitleSizeOptions()
    {
        subtitleSizeDropdown?.ClearOptions();
        subtitleSizeDropdown?.AddOptions(new System.Collections.Generic.List<string> { "Small", "Medium", "Large" });
    }

    private void BuildColorblindOptions()
    {
        colorblindDropdown?.ClearOptions();
        colorblindDropdown?.AddOptions(new System.Collections.Generic.List<string> {
            "Off", "Protanopia", "Deuteranopia", "Tritanopia"
        });
    }

    private void BuildMotionBlurOptions()
    {
        motionBlurDropdown?.ClearOptions();
        motionBlurDropdown?.AddOptions(new System.Collections.Generic.List<string> { "Off", "On" });
    }

    // --- Apply ---
    private void ApplySubtitles(int v)
    {
        Debug.Log("Subtitles " + (v == 1 ? "On" : "Off"));
    }

    private void ApplySubtitleSize(int v)
    {
        Debug.Log("Subtitle size index: " + v);
    }

    private void ApplyColorblind(int v)
    {
        if (colorblindManager)
        {
            colorblindManager.ApplyColorblind(v);
        }
        else
        {
            Debug.LogWarning("No ColorblindManager assigned!");
        }
    }

    private void ApplyMotionBlur(int v)
    {
        if (!motionBlur)
        {
            Debug.LogWarning("MotionBlur not found in Global Volume!");
            return;
        }

        if (v == 1) // On
        {
            motionBlur.intensity.overrideState = true;
            motionBlur.intensity.value = 1f;
            motionBlur.active = true; // keep effect alive
        }
        else // Off
        {
            motionBlur.intensity.overrideState = false; // release control
            motionBlur.active = false;                  // fully disable effect
            motionBlur.intensity.value = 0f;            // safety
        }

        Debug.Log($"Motion Blur Applied : {(v == 1 ? "On" : "Off")}");
    }


}
