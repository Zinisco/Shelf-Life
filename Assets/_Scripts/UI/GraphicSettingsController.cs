using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GraphicSettingsController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Dropdown windowModeDropdown; // Windowed, Windowed Fullscreen, Fullscreen
    [SerializeField] private TMP_Dropdown resolutionDropdown; // 1920 x 1080, ...
    [SerializeField] private TMP_Dropdown vSyncDropdown;      // Disabled, Enabled

    // --- PlayerPrefs keys ---
    const string KEY_MODE = "GFX_WindowMode";   // int (0..2)
    const string KEY_W = "GFX_Width";        // int
    const string KEY_H = "GFX_Height";       // int
    const string KEY_VS = "GFX_VSync";        // int (0/1)

    // --- Defaults ---
    const int DEFAULT_MODE = 2;   // 0=Windowed, 1=Windowed Fullscreen, 2=Fullscreen
    const int DEFAULT_VSYNC = 1;

    // Internal state
    private bool _initializing;
    private List<(int w, int h)> _resList = new();

    void Awake()
    {
        _initializing = true;

        BuildWindowModeOptions();
        BuildVSyncOptions();
        BuildResolutionOptions();     // collects & fills the resolution dropdown

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

    // Rebuild the resolution list (call if displays change)
    private void BuildResolutionOptions()
    {
        resolutionDropdown.ClearOptions();
        _resList.Clear();

        // Dedup by WxH; sort descending
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

        // Fallback: at least current resolution
        if (_resList.Count == 0)
        {
            _resList.Add((Screen.currentResolution.width, Screen.currentResolution.height));
            opts.Add($"{Screen.currentResolution.width} x {Screen.currentResolution.height}");
        }

        resolutionDropdown.AddOptions(opts);

        // Select current screen size if nothing saved
        int idx = IndexOfResolution(Screen.width, Screen.height);
        resolutionDropdown.SetValueWithoutNotify(idx);
        resolutionDropdown.RefreshShownValue();
    }

    private void BuildWindowModeOptions()
    {
        windowModeDropdown.ClearOptions();
        windowModeDropdown.AddOptions(new List<string>
        {
            "Windowed",
            "Windowed Fullscreen",
            "Fullscreen"
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

        // If not found, pick closest by pixel count
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
        // Read selections
        int modeIdx = Mathf.Clamp(windowModeDropdown.value, 0, 2);
        var (w, h) = _resList[Mathf.Clamp(resolutionDropdown.value, 0, _resList.Count - 1)];
        bool vsyncOn = vSyncDropdown.value == 1;

        // Map mode
        FullScreenMode fsMode = FullScreenMode.Windowed;
        switch (modeIdx)
        {
            case 0: fsMode = FullScreenMode.Windowed; break;                // windowed
            case 1: fsMode = FullScreenMode.FullScreenWindow; break;        // borderless/windowed fullscreen
            case 2: fsMode = FullScreenMode.ExclusiveFullScreen; break;     // exclusive fullscreen (if supported)
        }

        // Apply VSync
        QualitySettings.vSyncCount = vsyncOn ? 1 : 0;

        // Only set resolution if it actually differs (prevents flicker)
        bool sizeDiff = (Screen.width != w || Screen.height != h);
        bool modeDiff = (Screen.fullScreenMode != fsMode);

        if (forceApply || sizeDiff || modeDiff)
        {
            // NOTE: On some platforms ExclusiveFullScreen may be ignored; Unity falls back automatically.
            Screen.SetResolution(w, h, fsMode);
        }

        // Persist
        PlayerPrefs.SetInt(KEY_MODE, modeIdx);
        PlayerPrefs.SetInt(KEY_W, w);
        PlayerPrefs.SetInt(KEY_H, h);
        PlayerPrefs.SetInt(KEY_VS, vsyncOn ? 1 : 0);
        PlayerPrefs.Save();
    }
}
