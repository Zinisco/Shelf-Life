using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject settingsRoot; // whole settings window

    [Header("Main Menu")]
    [SerializeField] private GameObject mainMenuPanel; // whole Main Menu window

    [Header("Tab Buttons")]
    [SerializeField] private Button generalButton;
    [SerializeField] private Button graphicsButton;
    [SerializeField] private Button accessibilityButton;
    [SerializeField] private Button audioButton;
    [SerializeField] private Button backButton;

    [Header("Panels (exactly one is active at a time)")]
    [SerializeField] private GameObject generalPanel;
    [SerializeField] private GameObject graphicsPanel;
    [SerializeField] private GameObject accessibilityPanel;
    [SerializeField] private GameObject audioPanel;

    // Optional: if you ever want to remember last-opened tab, flip this to true.
    [SerializeField] private bool alwaysStartOnGeneral = true;

    private GameObject[] _allPanels;

    void Awake()
    {
        _allPanels = new[] { generalPanel, graphicsPanel, accessibilityPanel, audioPanel };

        // Wire buttons
        if (generalButton) generalButton.onClick.AddListener(ShowGeneral);
        if (graphicsButton) graphicsButton.onClick.AddListener(ShowGraphics);
        if (accessibilityButton) accessibilityButton.onClick.AddListener(ShowAccessibility);
        if (audioButton) audioButton.onClick.AddListener(ShowAudio);
        if (backButton) backButton.onClick.AddListener(CloseSettings);

        // Start hidden; when opened we’ll default to General
        if (settingsRoot) settingsRoot.SetActive(false);
        SetActiveOnly(null); // ensure all panels hidden until opened
    }

    // Public API (call from MainMenu / PauseMenu)
    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);

        if (settingsRoot) settingsRoot.SetActive(true);
        if (alwaysStartOnGeneral || !AnyPanelActive())
            ShowGeneral();
    }

    public void CloseSettings()
    {
        mainMenuPanel.SetActive(true);

        if (settingsRoot) settingsRoot.SetActive(false);
    }

    // Tab handlers
    public void ShowGeneral() => SetActiveOnly(generalPanel);
    public void ShowGraphics() => SetActiveOnly(graphicsPanel);
    public void ShowAccessibility() => SetActiveOnly(accessibilityPanel);
    public void ShowAudio() => SetActiveOnly(audioPanel);

    // Core: hide all, show just one (can pass null to hide everything)
    private void SetActiveOnly(GameObject target)
    {
        foreach (var p in _allPanels)
            if (p) p.SetActive(p == target);
    }

    private bool AnyPanelActive()
    {
        foreach (var p in _allPanels)
            if (p && p.activeSelf) return true;
        return false;
    }
}
