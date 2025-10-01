using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Debug (Editor-only at runtime)")]
    [SerializeField] private bool overrideSaveDetection = false;
    [SerializeField] private bool fakeHasSave = false;

    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject overwritePromptPanel;
    [SerializeField] private GameObject reallySurePromptPanel;
    [SerializeField] private GameObject modeChooserPanel;

    [Header("Menu Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Settings")]
    [SerializeField] private SettingsManager settingsManager;

    [Header("Prompt A: Overwrite?")]
    [SerializeField] private Button confirmOverwriteButton;
    [SerializeField] private Button cancelOverwriteButton;

    [Header("Prompt B: REALLY sure?")]
    [SerializeField] private Button confirmReallySureButton;
    [SerializeField] private Button cancelReallySureButton;

    [Header("Game Mode Chooser")]
    [SerializeField] private Button newStandardButton;
    [SerializeField] private Button newZenModeButton;
    [SerializeField] private Button cancelGameModeButton; 

    private void Start()
    {
        // Top-level
        startButton.onClick.AddListener(OnStartPressed);
        continueButton.onClick.AddListener(ContinueGame);
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(QuitGame);

        // Prompt A
        cancelOverwriteButton.onClick.AddListener(ClosePromptA);
        confirmOverwriteButton.onClick.AddListener(AdvanceToPromptB);

        // Prompt B
        cancelReallySureButton.onClick.AddListener(BackToPromptA);
        confirmReallySureButton.onClick.AddListener(ShowModeChooser);

        // Mode chooser
        if (newStandardButton) newStandardButton.onClick.AddListener(() => ConfirmStartNewGame(GameMode.Standard));
        if (newZenModeButton) newZenModeButton.onClick.AddListener(() => ConfirmStartNewGame(GameMode.Zen));
        if (cancelGameModeButton) cancelGameModeButton.onClick.AddListener(CloseModeChooser); // NEW

        // Init states
        overwritePromptPanel.SetActive(false);
        reallySurePromptPanel.SetActive(false);
        modeChooserPanel.SetActive(false);
        mainMenuPanel.SetActive(true);

        // Enable/disable Continue based on (editor-safe) HasSave()
        continueButton.interactable = HasSave();
    }

    private bool HasSave()
    {
#if UNITY_EDITOR
        if (overrideSaveDetection)
            return fakeHasSave;
        else
            return SaveSystem.HasSave();
#else
        return SaveSystem.HasSave();
#endif
    }

    private void OnStartPressed()
    {
        if (HasSave())
        {
            mainMenuPanel.SetActive(false);
            overwritePromptPanel.SetActive(true);
            reallySurePromptPanel.SetActive(false);
            modeChooserPanel.SetActive(false);
        }
        else
        {
            mainMenuPanel.SetActive(false);
            ShowModeChooser();
        }
    }

    private void AdvanceToPromptB()
    {
        overwritePromptPanel.SetActive(false);
        reallySurePromptPanel.SetActive(true);
    }

    private void ShowModeChooser()
    {
        overwritePromptPanel.SetActive(false);
        reallySurePromptPanel.SetActive(false);
        modeChooserPanel.SetActive(true);
    }

    private void CloseModeChooser() 
    {
        modeChooserPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void ConfirmStartNewGame(GameMode mode)
    {
        modeChooserPanel.SetActive(false);

        SaveSystem.ClearSave();
        GameModeConfig.StartNewGame(mode);

        SceneLoader.sceneToLoad = "GameScene";
        SceneManager.LoadScene("LoadScene");
    }

    private void ClosePromptA()
    {
        overwritePromptPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    private void BackToPromptA()
    {
        reallySurePromptPanel.SetActive(false);
        overwritePromptPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    private void ContinueGame()
    {
        SaveSystem.Load();
        SceneLoader.sceneToLoad = "GameScene";
        SceneManager.LoadScene("LoadScene");
    }

    private void OpenSettings()
    {
        if (settingsManager != null)
            settingsManager.OpenSettings();
    }

    private void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
