using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Settings")]
    [SerializeField] private SettingsManager settingsManager;

    [Header("Overwrite Prompt")]
    [SerializeField] private GameObject overwritePromptPanel;
    [SerializeField] private Button confirmOverwriteButton;
    [SerializeField] private Button cancelOverwriteButton;

    private void Start()
    {
        startButton.onClick.AddListener(OnStartPressed);
        continueButton.onClick.AddListener(ContinueGame);
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(QuitGame);

        cancelOverwriteButton.onClick.AddListener(CancelStartNewGame);

        overwritePromptPanel.SetActive(false);

        if (!SaveSystem.HasSave())
            continueButton.interactable = false;
    }

    private void OnStartPressed()
    {
        if (SaveSystem.HasSave())
        {
            overwritePromptPanel.SetActive(true); // Show confirmation
        }
        else
        {
            ConfirmStartNewGame();
        }
    }

    public void ConfirmStartNewGame()
    {
        SaveSystem.ClearSave(); // Wipe existing save
        SceneLoader.sceneToLoad = "GameScene"; // if using loading screen
        SceneManager.LoadScene("LoadScene");
    }

    private void CancelStartNewGame()
    {
        overwritePromptPanel.SetActive(false); // Just close the popup
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
        else
            Debug.LogWarning("SettingsManager not assigned.");
    }

    private void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
