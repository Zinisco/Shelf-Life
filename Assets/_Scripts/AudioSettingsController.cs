using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

public class AudioSettingsController : MonoBehaviour
{
    [Header("Mixer")]
    [SerializeField] private AudioMixer audioMixer; // expose parameters: MasterVolume, MusicVolume, SFXVolume

    [Header("Master")]
    [SerializeField] private Slider masterSlider;          // 0..1
    [SerializeField] private TMP_InputField masterInput;   // shows 0..100

    [Header("Music")]
    [SerializeField] private Slider musicSlider;           // 0..1
    [SerializeField] private TMP_InputField musicInput;    // shows 0..100

    [Header("SFX")]
    [SerializeField] private Slider sfxSlider;             // 0..1
    [SerializeField] private TMP_InputField sfxInput;      // shows 0..100

    // PlayerPrefs keys (keep consistent elsewhere)
    private const string KEY_MASTER = "MasterVolume";
    private const string KEY_MUSIC = "MusicVolume";
    private const string KEY_SFX = "SFXVolume";

    // Defaults (0..1)
    private const float DEFAULT_VOL = 0.70f;

    // Slider range
    private const float MIN_VOL = 0.0f;
    private const float MAX_VOL = 1.0f;

    private bool _initializing;

    void Awake()
    {
        _initializing = true;

        // 1) Configure sliders
        SetupSlider(masterSlider);
        SetupSlider(musicSlider);
        SetupSlider(sfxSlider);

        // 2) Load saved (or default) values
        float m = Clamp01(PlayerPrefs.GetFloat(KEY_MASTER, DEFAULT_VOL));
        float mu = Clamp01(PlayerPrefs.GetFloat(KEY_MUSIC, DEFAULT_VOL));
        float s = Clamp01(PlayerPrefs.GetFloat(KEY_SFX, DEFAULT_VOL));

        // 3) Push to UI without spamming events
        SetLinked(masterSlider, masterInput, m);
        SetLinked(musicSlider, musicInput, mu);
        SetLinked(sfxSlider, sfxInput, s);

        // 4) Hook events
        masterSlider.onValueChanged.AddListener(v => { v = Clamp01(v); SyncInput(masterInput, v); Apply("MasterVolume", v); Save(KEY_MASTER, v); });
        musicSlider.onValueChanged.AddListener(v => { v = Clamp01(v); SyncInput(musicInput, v); Apply("MusicVolume", v); Save(KEY_MUSIC, v); });
        sfxSlider.onValueChanged.AddListener(v => { v = Clamp01(v); SyncInput(sfxInput, v); Apply("SFXVolume", v); Save(KEY_SFX, v); });

        masterInput.onEndEdit.AddListener(sv => { if (TryParsePercent(sv, out var v)) { SyncSlider(masterSlider, v); Apply("MasterVolume", v); Save(KEY_MASTER, v); } });
        musicInput.onEndEdit.AddListener(sv => { if (TryParsePercent(sv, out var v)) { SyncSlider(musicSlider, v); Apply("MusicVolume", v); Save(KEY_MUSIC, v); } });
        sfxInput.onEndEdit.AddListener(sv => { if (TryParsePercent(sv, out var v)) { SyncSlider(sfxSlider, v); Apply("SFXVolume", v); Save(KEY_SFX, v); } });

        // 5) Apply once at start
        Apply("MasterVolume", m);
        Apply("MusicVolume", mu);
        Apply("SFXVolume", s);

        _initializing = false;
    }

    // --- Public helper (optional button) ---
    public void ResetAudioToDefault()
    {
        float v = DEFAULT_VOL;
        SetLinked(masterSlider, masterInput, v);
        SetLinked(musicSlider, musicInput, v);
        SetLinked(sfxSlider, sfxInput, v);

        Apply("MasterVolume", v); Save(KEY_MASTER, v);
        Apply("MusicVolume", v); Save(KEY_MUSIC, v);
        Apply("SFXVolume", v); Save(KEY_SFX, v);
        PlayerPrefs.Save();
    }

    // --- Internals ---
    private void Apply(string mixerParam, float linear01)
    {
        if (!audioMixer) return;
        // Convert 0..1 -> dB. Use a small floor to avoid -inf dB.
        float dB = Mathf.Log10(Mathf.Max(linear01, 0.0001f)) * 20f;
        audioMixer.SetFloat(mixerParam, dB);
    }

    private static void SetupSlider(Slider s)
    {
        if (!s) return;
        s.minValue = MIN_VOL;
        s.maxValue = MAX_VOL;
        s.wholeNumbers = false;
    }

    // Link slider (0..1) with input (0..100%)
    private static void SetLinked(Slider s, TMP_InputField input, float v01)
    {
        if (s) s.SetValueWithoutNotify(v01);
        if (input) input.SetTextWithoutNotify(ToPercent(v01));
    }

    private static void SyncInput(TMP_InputField input, float v01)
    {
        if (input) input.SetTextWithoutNotify(ToPercent(v01));
    }

    private static void SyncSlider(Slider s, float v01)
    {
        if (s) s.SetValueWithoutNotify(v01);
    }

    private static string ToPercent(float v01)
    {
        // Show as whole number percent (e.g., 70)
        return Mathf.RoundToInt(Mathf.Clamp01(v01) * 100f).ToString();
    }

    private static bool TryParsePercent(string text, out float v01)
    {
        // Accept "70" or "70%" or "0.7"
        text = (text ?? "").Trim().TrimEnd('%');

        if (float.TryParse(text, out float v))
        {
            // If user typed 0..1, treat as linear; if >1, treat as percentage
            if (v <= 1.0001f) v01 = Mathf.Clamp01(v);
            else v01 = Mathf.Clamp01(v / 100f);
            return true;
        }
        v01 = 0.7f; // fallback
        return false;
    }

    private static void Save(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }


    private static float Clamp01(float v) => Mathf.Clamp01(v);
}
