using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GraphicSettingsController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Dropdown windowModeDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown vSyncDropdown;

    // --- PlayerPrefs keys ---
    const string KEY_MODE = "GFX_WindowMode";   // int (0..2)
    const string KEY_W = "GFX_Width";
    const string KEY_H = "GFX_Height";
    const string KEY_VS = "GFX_VSync";

    // --- Defaults ---
    const int DEFAULT_MODE = 2;   // 0=Windowed, 1=Borderless Fullscreen, 2=Exclusive Fullscreen
    const int DEFAULT_VSYNC = 1;

    // Internal state
    private bool _initializing;
    private List<(int w, int h)> _resList = new();

    void Awake()
    {
        _initializing = true;

        BuildWindowModeOptions();
        BuildVSyncOptions();
        BuildResolutionOptions();

        // Load saved or current system values
        int savedMode = PlayerPrefs.GetInt(KEY_MODE, DEFAULT_MODE);
        int savedVsync = PlayerPrefs.GetInt(KEY_VS, DEFAULT_VSYNC);
        int savedWidth = PlayerPrefs.GetInt(KEY_W, Screen.currentResolution.width);
        int savedHeight = PlayerPrefs.GetInt(KEY_H, Screen.currentResolution.height);

        // Set dropdowns (without events)
        windowModeDropdown.SetValueWithoutNotify(Mathf.Clamp(savedMode, 0, 2));
        vSyncDropdown.SetValueWithoutNotify(savedVsync == 1 ? 1 : 0);
        resolutionDropdown.SetValueWithoutNotify(IndexOfResolution(savedWidth, savedHeight));

        // Hook listeners
        windowModeDropdown.onValueChanged.AddListener(_ => OnAnyChanged());
        resolutionDropdown.onValueChanged.AddListener(_ => OnAnyChanged());
        vSyncDropdown.onValueChanged.AddListener(_ => OnAnyChanged());

        // Apply once on start (so MainMenu also enforces saved gfx)
        ApplyGraphics(forceApply: true);

        _initializing = false;
    }

    // Rebuild the resolution list
    private void BuildResolutionOptions()
    {
        resolutionDropdown.ClearOptions();
        _resList.Clear();

        var unique = new HashSet<string>();
        var resos = Screen.resolutions
            .Select(r => (r.width, r.height))
            .Distinct()
            .OrderByDescending(r => r.width * r.height)
            .ThenByDescending(r => r.height)
            .ToList();

        var opts = new List<string>();
        foreach (var r in resos)
        {
            string key = $"{r.width}x{r.height}";
            if (unique.Add(key))
            {
                _resList.Add((r.width, r.height));
                opts.Add($"{r.width} x {r.height}");
            }
        }

        if (_resList.Count == 0)
        {
            _resList.Add((Screen.currentResolution.width, Screen.currentResolution.height));
            opts.Add($"{Screen.currentResolution.width} x {Screen.currentResolution.height}");
        }

        resolutionDropdown.AddOptions(opts);

        int idx = IndexOfResolution(Screen.width, Screen.height);
        resolutionDropdown.SetValueWithoutNotify(idx);
        resolutionDropdown.RefreshShownValue();
    }

    private void BuildWindowModeOptions()
    {
        windowModeDropdown.ClearOptions();
        windowModeDropdown.AddOptions(new List<string>
        {
            "Windowed",              // has borders
            "Borderless Fullscreen", // borderless, fills screen
            "Exclusive Fullscreen"   // true exclusive mode
        });
    }

    private void BuildVSyncOptions()
    {
        vSyncDropdown.ClearOptions();
        vSyncDropdown.AddOptions(new List<string> { "Disabled", "Enabled" });
    }

    private int IndexOfResolution(int w, int h)
    {
        for (int i = 0; i < _resList.Count; i++)
            if (_resList[i].w == w && _resList[i].h == h) return i;

        int best = 0;
        var target = w * h;
        int bestDiff = int.MaxValue;
        for (int i = 0; i < _resList.Count; i++)
        {
            int diff = Mathf.Abs(_resList[i].w * _resList[i].h - target);
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        return best;
    }

    private void OnAnyChanged()
    {
        if (_initializing) return;
        ApplyGraphics(forceApply: false);
    }

    private void ApplyGraphics(bool forceApply)
    {
        int modeIdx = Mathf.Clamp(windowModeDropdown.value, 0, 2);
        var (w, h) = _resList[Mathf.Clamp(resolutionDropdown.value, 0, _resList.Count - 1)];
        bool vsyncOn = vSyncDropdown.value == 1;

        FullScreenMode fsMode = FullScreenMode.Windowed;
        Debug.Log($"Applied graphics: Mode={fsMode}, Res={w}x{h}, VSync={QualitySettings.vSyncCount}");

        switch (modeIdx)
        {
            case 0: // Windowed
                fsMode = FullScreenMode.Windowed;
                if (w == Display.main.systemWidth && h == Display.main.systemHeight)
                {
                    w = Mathf.RoundToInt(w * 0.8f);
                    h = Mathf.RoundToInt(h * 0.8f);
                }
                resolutionDropdown.interactable = true;
                break;

            case 1: // Borderless Fullscreen
                fsMode = FullScreenMode.FullScreenWindow;
                w = Display.main.systemWidth;
                h = Display.main.systemHeight;
                resolutionDropdown.interactable = false; // disable dropdown
                break;

            case 2: // Exclusive Fullscreen
                fsMode = FullScreenMode.ExclusiveFullScreen;
                resolutionDropdown.interactable = true;
                break;
        }

        QualitySettings.vSyncCount = vsyncOn ? 1 : 0;

        if (forceApply || Screen.width != w || Screen.height != h || Screen.fullScreenMode != fsMode)
        {
            Screen.SetResolution(w, h, fsMode);
        }

        PlayerPrefs.SetInt(KEY_MODE, modeIdx);
        PlayerPrefs.SetInt(KEY_W, w);
        PlayerPrefs.SetInt(KEY_H, h);
        PlayerPrefs.SetInt(KEY_VS, vsyncOn ? 1 : 0);
        PlayerPrefs.Save();
    }
}
