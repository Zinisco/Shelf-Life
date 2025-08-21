using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Debug (Editor-only at runtime)")]
    [Tooltip("Editor-only: if true, pretend there IS/ISN'T a save based on Fake Has Save below.")]
    [SerializeField] private bool overrideSaveDetection = false;
    [SerializeField] private bool fakeHasSave = false;

    [Header("Menu Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Settings")]
    [SerializeField] private SettingsManager settingsManager;

    [Header("Prompt A: Overwrite?")]
    [SerializeField] private GameObject overwritePromptPanel;
    [SerializeField] private HoldToConfirmButton confirmOverwriteHold;
    [SerializeField] private Button cancelOverwriteButton;

    [Header("Prompt B: REALLY sure?")]
    [SerializeField] private GameObject reallySurePromptPanel;
    [SerializeField] private HoldToConfirmButton confirmReallySureHold;
    [SerializeField] private Button cancelReallySureButton;

    [Header("New Game Mode Chooser (if implemented)")]
    [SerializeField] private Button newStandardButton;     // “New Game (Standard)”
    [SerializeField] private Button newZenModeButton;  // “New Game (Zen Mode)”
    [SerializeField] private GameObject modeChooserPanel;  // small panel shown only if CanChooseModeNow()

    private GameMode _selectedNewMode = GameMode.Standard;

    private void Start()
    {
        startButton.onClick.AddListener(OnStartPressed);
        continueButton.onClick.AddListener(ContinueGame);
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(QuitGame);

        // Prompt A
        cancelOverwriteButton.onClick.AddListener(ClosePromptA);
        confirmOverwriteHold.onCompleted.AddListener(AdvanceToPromptB);

        // Prompt B
        cancelReallySureButton.onClick.AddListener(BackToPromptA);
        confirmReallySureHold.onCompleted.AddListener(ConfirmStartNewGame);

        overwritePromptPanel.SetActive(false);
        reallySurePromptPanel.SetActive(false);

        // Enable/disable Continue based on (editor-safe) HasSave()
        continueButton.interactable = HasSave();

        if (newStandardButton) newStandardButton.onClick.AddListener(() => SelectModeAndStart(GameMode.Standard));
        if (newZenModeButton) newZenModeButton.onClick.AddListener(() => SelectModeAndStart(GameMode.Zen));

        if (modeChooserPanel)
            modeChooserPanel.SetActive(GameModeConfig.CanChooseModeNow());
    }

    /// <summary>
    /// Editor-safe save detection:
    /// - In Editor: can force the result with overrideSaveDetection/fakeHasSave.
    /// - In Player build: always uses real SaveSystem.HasSave() (override compiled out).
    /// </summary>
    private bool HasSave()
    {
        bool result;

        // This whole block is compiled ONLY in the Editor.
#if UNITY_EDITOR
        if (overrideSaveDetection)
        {
            result = fakeHasSave;
            Debug.Log($"[MainMenuManager] DEBUG OVERRIDE ACTIVE (Editor) = Forcing HasSave() = {result}");
        }
        else
        {
            result = SaveSystem.HasSave();
            Debug.Log($"[MainMenuManager] Editor using real SaveSystem.HasSave() = {result}");
        }
#else
        // Player build: override is impossible (compiled out).
        result = SaveSystem.HasSave();
        if (overrideSaveDetection) // still serialized in builds, but this line runs:
            Debug.LogWarning("[MainMenuManager] overrideSaveDetection is ignored in Player builds.");
        Debug.Log($"[MainMenuManager] Player build = SaveSystem.HasSave() = {result}");
#endif

        return result;
    }

    private void OnStartPressed()
    {
        if (HasSave())
        {
            Debug.Log("[MainMenuManager] Start pressed = HasSave() TRUE = Showing Prompt A (Overwrite?)");
            overwritePromptPanel.SetActive(true);
            reallySurePromptPanel.SetActive(false);
            confirmOverwriteHold.ResetHold();
        }
        else
        {
            Debug.Log("[MainMenuManager] Start pressed = HasSave() FALSE = Starting new game immediately");
            ConfirmStartNewGame();
        }
    }

    private void AdvanceToPromptB()
    {
        Debug.Log("[MainMenuManager] Prompt A completed = Showing Prompt B (REALLY sure?)");
        overwritePromptPanel.SetActive(false);
        reallySurePromptPanel.SetActive(true);
        confirmReallySureHold.ResetHold();
    }

    private void SelectModeAndStart(GameMode mode)
    {
        // If a save exists we should NOT allow choosing a mode — offer overwrite flow first.
        if (!GameModeConfig.CanChooseModeNow())
        {
            OnStartPressed();
            return;
        }

        _selectedNewMode = mode;
        OnStartPressed(); 
    }

    public void ConfirmStartNewGame()
    {
        overwritePromptPanel.SetActive(false);
        reallySurePromptPanel.SetActive(false);

        // Clear any old save and set the new mode
        SaveSystem.ClearSave();
        GameModeConfig.StartNewGame(_selectedNewMode);

        SceneLoader.sceneToLoad = "GameScene";
        SceneManager.LoadScene("LoadScene");
    }


    private void ClosePromptA()
    {
        Debug.Log("[MainMenuManager] Cancelled at Prompt A");
        overwritePromptPanel.SetActive(false);
        confirmOverwriteHold.ResetHold();
    }

    private void BackToPromptA()
    {
        Debug.Log("[MainMenuManager] Cancelled at Prompt B = Closing prompts");
        reallySurePromptPanel.SetActive(false);
        overwritePromptPanel.SetActive(false);
        confirmReallySureHold.ResetHold();
        confirmOverwriteHold.ResetHold();
    }

    private void ContinueGame()
    {
        Debug.Log("[MainMenuManager] Continue pressed = Loading existing save");
        SaveSystem.Load();
        SceneLoader.sceneToLoad = "GameScene";
        SceneManager.LoadScene("LoadScene");
    }

    private void OpenSettings()
    {
        if (settingsManager != null)
        {
            Debug.Log("[MainMenuManager] Opening Settings");
            settingsManager.OpenSettings();
        }
        else
        {
            Debug.LogWarning("[MainMenuManager] SettingsManager not assigned.");
        }
    }

    private void QuitGame()
    {
        Debug.Log("[MainMenuManager] Quit pressed");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
