using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ColorblindManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Volume globalVolume; // assign your global post-processing volume

    private ColorLookup colorLookup;        // For LUT mode
    private ColorAdjustments colorAdjust;   // Fallback tint mode

    void Awake()
    {
        if (!globalVolume) return;

        var profile = globalVolume.profile != null ? globalVolume.profile : globalVolume.sharedProfile;

        if (profile)
        {
            profile.TryGet(out colorLookup);
            profile.TryGet(out colorAdjust);
        }
    }

    /// <summary>
    /// 0 = Off, 1 = Protanopia, 2 = Deuteranopia, 3 = Tritanopia
    /// </summary>
    public void ApplyColorblind(int mode)
    {
        if (colorLookup != null)
        {
            ApplyUsingLUT(mode);
        }
        else if (colorAdjust != null)
        {
            ApplyUsingColorAdjustments(mode);
        }
        else
        {
            Debug.LogWarning("No Color Lookup or Color Adjustments in Volume!");
        }
    }

    // --- LUT implementation ---
    private void ApplyUsingLUT(int mode)
    {
        Texture2D lut = null;

        switch (mode)
        {
            case 1: lut = Resources.Load<Texture2D>("LUTs/Protanopia"); break;
            case 2: lut = Resources.Load<Texture2D>("LUTs/Deuteranopia"); break;
            case 3: lut = Resources.Load<Texture2D>("LUTs/Tritanopia"); break;
            default: lut = null; break;
        }

        if (lut != null)
        {
            colorLookup.texture.overrideState = true;
            colorLookup.texture.value = lut;
            colorLookup.contribution.overrideState = true;
            colorLookup.contribution.value = 1f;
            Debug.Log("Applied Colorblind LUT: " + mode);
        }
        else
        {
            // fallback to normal
            colorLookup.contribution.overrideState = false;
            colorLookup.texture.value = null;
            Debug.Log("Colorblind Off (no LUT applied)");
        }
    }

    // --- Fallback: ColorAdjustments ---
    private void ApplyUsingColorAdjustments(int mode)
    {
        colorAdjust.saturation.overrideState = true;
        colorAdjust.contrast.overrideState = true;
        colorAdjust.colorFilter.overrideState = true;

        switch (mode)
        {
            case 1: // Protanopia - reduce red
                colorAdjust.colorFilter.value = new Color(0.6f, 1f, 1f);
                break;
            case 2: // Deuteranopia - reduce green
                colorAdjust.colorFilter.value = new Color(1f, 0.6f, 1f);
                break;
            case 3: // Tritanopia - reduce blue
                colorAdjust.colorFilter.value = new Color(1f, 1f, 0.6f);
                break;
            default: // Off
                colorAdjust.colorFilter.overrideState = false;
                break;
        }

        Debug.Log("Applied Colorblind Fallback (ColorAdjustments): " + mode);
    }
}
