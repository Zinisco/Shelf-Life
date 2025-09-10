using System.Text;
using UnityEngine;
using TMPro;

public class SignManager : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private Runtime3DText signText; // your 3D text builder

    [Header("Defaults")]
    [SerializeField] private string defaultStoreName = "BOOKSTORE";
    [SerializeField] private int maxChars = 20;

    [Tooltip("Characters players are allowed to use on the sign.")]
    [SerializeField]
    private string allowedChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 $@&.-'!";

    // Simple persistence key (swap for your Save/Load system when ready)
    private const string PrefKey = "StoreDisplayName";

    private void Awake()
    {
        // Load saved name or show default on first run
        var name = PlayerPrefs.GetString(PrefKey, defaultStoreName);
        name = ValidateAndSanitize(name);
        ApplyNameInternal(name, save: false);
    }

    /// <summary>Public API: call this from your Design panel when the player confirms.</summary>
    public bool TrySetStoreName(string rawInput)
    {
        var clean = ValidateAndSanitize(rawInput);
        if (string.IsNullOrEmpty(clean)) return false;

        ApplyNameInternal(clean, save: true);
        return true;
    }

    private void ApplyNameInternal(string name, bool save)
    {
        if (signText == null) return;
        signText.SetText(name);

        if (save)
        {
            PlayerPrefs.SetString(PrefKey, name);
            PlayerPrefs.Save();
        }
    }

    private string ValidateAndSanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return defaultStoreName;

        // Trim and enforce length
        s = s.Trim();
        if (s.Length > maxChars) s = s.Substring(0, maxChars);

        // Filter to allowed characters
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (allowedChars.IndexOf(c) >= 0) sb.Append(c);
            // else skip disallowed characters silently (or replace with space)
        }

        // Collapse double spaces
        var cleaned = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s{2,}", " ");
        // Convert to uppercase before sending to 3D system
        return string.IsNullOrEmpty(cleaned) ? defaultStoreName : cleaned.ToUpperInvariant();
    }
}
