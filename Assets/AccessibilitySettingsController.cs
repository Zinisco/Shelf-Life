using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

public class AccessibilitySettingsController : MonoBehaviour
{
    [Header("UI Elements (optional in GameScene)")]
    [SerializeField] private TMP_Dropdown subtitlesDropdown;
    [SerializeField] private TMP_Dropdown subtitleSizeDropdown;
    [SerializeField] private TMP_Dropdown colorblindDropdown;
    [SerializeField] private TMP_Dropdown motionBlurDropdown;

    [Header("Post Processing (GameScene only)")]
    [SerializeField] private Volume globalVolume;

    private MotionBlur motionBlur;
    private ColorLookup colorLookup;

    // Keys
    const string KEY_SUBTITLES = "ACC_Subtitles";
    const string KEY_SUBSIZE = "ACC_SubtitleSize";
    const string KEY_COLORBLIND = "ACC_Colorblind";
    const string KEY_MOTIONBLUR = "ACC_MotionBlur";

    void Awake()
    {
        if (globalVolume)
        {
            var profile = globalVolume.profile != null ? globalVolume.profile : globalVolume.sharedProfile;
            if (profile != null)
            {
                if (profile.TryGet(out MotionBlur mb)) motionBlur = mb;
                if (profile.TryGet(out ColorLookup cl)) colorLookup = cl;
            }
        }

        // Build dropdowns
        BuildSubtitleOptions();
        BuildSubtitleSizeOptions();
        BuildColorblindOptions();
        BuildMotionBlurOptions();

        // Hook listeners (but don’t apply yet)
        if (colorblindDropdown)
            colorblindDropdown.onValueChanged.AddListener(v => SaveAndApply(KEY_COLORBLIND, v));
        if (motionBlurDropdown)
            motionBlurDropdown.onValueChanged.AddListener(v => SaveAndApply(KEY_MOTIONBLUR, v));
        ;
    }

    void Start()
    {
        int subtitles = PlayerPrefs.GetInt(KEY_SUBTITLES, 1);
        int size = PlayerPrefs.GetInt(KEY_SUBSIZE, 1);
        int colorblind = PlayerPrefs.GetInt(KEY_COLORBLIND, 0);
        int motion = PlayerPrefs.GetInt(KEY_MOTIONBLUR, 0);

        // Sync dropdowns (UI side, if they exist)
        subtitlesDropdown?.SetValueWithoutNotify(subtitles);
        subtitleSizeDropdown?.SetValueWithoutNotify(size);
        colorblindDropdown?.SetValueWithoutNotify(colorblind);
        motionBlurDropdown?.SetValueWithoutNotify(motion);
    }


    // --- Dropdown Builders ---
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
        colorblindDropdown?.AddOptions(new System.Collections.Generic.List<string> { "Off", "Protanopia", "Deuteranopia", "Tritanopia" });
    }

    private void BuildMotionBlurOptions()
    {
        motionBlurDropdown?.ClearOptions();
        motionBlurDropdown?.AddOptions(new System.Collections.Generic.List<string> { "Off", "On" });
    }

    private void SaveAndApply(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
        AccessibilityApplier.Instance?.ApplyAll();

        // keep dropdowns synced if needed
        if (key == KEY_COLORBLIND) colorblindDropdown?.SetValueWithoutNotify(value);
        if (key == KEY_MOTIONBLUR) motionBlurDropdown?.SetValueWithoutNotify(value);
    }


}
