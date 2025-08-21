using UnityEngine;

public class GraphicsSettingsApplier : MonoBehaviour
{
    // Must match GraphicSettingsController keys
    private const string KEY_MODE = "GFX_WindowMode"; // 0=Windowed, 1=Windowed Fullscreen, 2=Fullscreen
    private const string KEY_W = "GFX_Width";
    private const string KEY_H = "GFX_Height";
    private const string KEY_VS = "GFX_VSync";      // 0/1

    // Sensible fallbacks if prefs not present (will be overwritten by SettingsBootstrap anyway)
    private const int DEFAULT_MODE = 2;
    private const int DEFAULT_VSYNC = 1;

    void Awake()
    {
        // Ensure defaults exist (safe to call multiple times)
        SettingsBootstrap.EnsureDefaultsSaved();

        // Read saved values
        int modeIdx = PlayerPrefs.GetInt(KEY_MODE, DEFAULT_MODE);
        int w = PlayerPrefs.GetInt(KEY_W, Screen.currentResolution.width);
        int h = PlayerPrefs.GetInt(KEY_H, Screen.currentResolution.height);
        bool vsync = PlayerPrefs.GetInt(KEY_VS, DEFAULT_VSYNC) == 1;

        // Apply VSync
        QualitySettings.vSyncCount = vsync ? 1 : 0;

        // Map to FullScreenMode
        FullScreenMode fsMode = FullScreenMode.Windowed;
        switch (modeIdx)
        {
            case 0: fsMode = FullScreenMode.Windowed; break;
            case 1: fsMode = FullScreenMode.FullScreenWindow; break;   // borderless/windowed fullscreen
            case 2: fsMode = FullScreenMode.ExclusiveFullScreen; break;
        }

        // Only call SetResolution if something differs (avoids flicker)
        bool needSize = (Screen.width != w || Screen.height != h);
        bool needMode = (Screen.fullScreenMode != fsMode);

        if (needSize || needMode)
            Screen.SetResolution(w, h, fsMode);
    }
}
