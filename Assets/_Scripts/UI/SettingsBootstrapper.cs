using UnityEngine;

public static class SettingsBootstrap
{
    private const string INIT_KEY = "SettingsInitialized_v1";

    // Keys must match GeneralSettingsController
    private const string KEY_FOV = "FOV";
    private const string KEY_MS = "MouseSensitivity";
    private const string KEY_CS = "ControllerSensitivity";
    private const string KEY_INVX = "InvertX";
    private const string KEY_INVY = "InvertY";

    // Defaults (keep in sync with your controller)
    private const float DEFAULT_FOV = 75f;
    private const float DEFAULT_MS = 0.20f;
    private const float DEFAULT_CS = 3.00f;
    private const int DEFAULT_INVX = 0;   // 0 = No, 1 = Yes
    private const int DEFAULT_INVY = 0;

    public static void EnsureDefaultsSaved()
    {
        if (PlayerPrefs.GetInt(INIT_KEY, 0) == 1) return;

        PlayerPrefs.SetFloat(KEY_FOV, DEFAULT_FOV);
        PlayerPrefs.SetFloat(KEY_MS, DEFAULT_MS);
        PlayerPrefs.SetFloat(KEY_CS, DEFAULT_CS);
        PlayerPrefs.SetInt(KEY_INVX, DEFAULT_INVX);
        PlayerPrefs.SetInt(KEY_INVY, DEFAULT_INVY);
        PlayerPrefs.SetFloat("MasterVolume", 0.70f);
        PlayerPrefs.SetFloat("MusicVolume", 0.70f);
        PlayerPrefs.SetFloat("SFXVolume", 0.70f);


        PlayerPrefs.SetInt(INIT_KEY, 1);
        PlayerPrefs.Save();
    }
}
