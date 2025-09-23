using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AccessibilityApplier : MonoBehaviour
{
    [SerializeField] private Volume globalVolume;
    [Header("Post Processing LUTs (Colorblind Textures)")]
    [SerializeField] private Texture2D lutProtanopia;
    [SerializeField] private Texture2D lutDeuteranopia;
    [SerializeField] private Texture2D lutTritanopia;

    private MotionBlur motionBlur;
    private ColorLookup colorLookup;

    public static AccessibilityApplier Instance { get; private set; }

    const string KEY_COLORBLIND = "ACC_Colorblind";
    const string KEY_MOTIONBLUR = "ACC_MotionBlur";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        RefreshVolumeBindings();
        ApplyAll();
    }

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        RefreshVolumeBindings();
        ApplyAll();
    }

    private void RefreshVolumeBindings()
    {
        if (!globalVolume)
        {
            globalVolume = new GameObject("GlobalVolume").AddComponent<Volume>();
            globalVolume.isGlobal = true;
            globalVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            globalVolume.profile.Add<ColorLookup>(true);
            globalVolume.profile.Add<MotionBlur>(true);
        }


        if (globalVolume)
        {
            var profile = globalVolume.profile ?? globalVolume.sharedProfile;
            if (profile)
            {
                profile.TryGet(out motionBlur);
                profile.TryGet(out colorLookup);
            }
        }
    }

    public void ApplyAll()
    {
        ApplyColorblind(PlayerPrefs.GetInt(KEY_COLORBLIND, 0));
        ApplyMotionBlur(PlayerPrefs.GetInt(KEY_MOTIONBLUR, 0));
    }

    public void ApplyColorblind(int v)
    {
        if (!colorLookup) return;

        Texture2D lut = null;
        if (v == 1) lut = lutProtanopia;
        if (v == 2) lut = lutDeuteranopia;
        if (v == 3) lut = lutTritanopia;

        colorLookup.texture.overrideState = true;
        colorLookup.texture.value = lut;
        colorLookup.contribution.overrideState = true;
        colorLookup.contribution.value = lut ? 1f : 0f;

        Debug.Log($"[AccessibilityApplier] Colorblind = {v} ({(lut ? lut.name : "none")})");
    }

    public void ApplyMotionBlur(int v)
    {
        if (!motionBlur) return;

        motionBlur.active = (v == 1);
        motionBlur.intensity.overrideState = (v == 1);
        motionBlur.intensity.value = (v == 1 ? 1f : 0f);

        Debug.Log($"[AccessibilityApplier] MotionBlur = {(v == 1 ? "On" : "Off")}");
    }
}
