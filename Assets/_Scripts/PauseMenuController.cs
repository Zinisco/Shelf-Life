using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause menu with: Resume, Restart (hold-to-confirm), Save, Main Menu, Quit.
/// TimeScale is set to 0 while paused; UI uses unscaled time.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    public event EventHandler OnGamePaused;
    public event EventHandler OnGameUnpaused;

    [Header("Root & Focus")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject firstSelected; // focus when opening pause
    [SerializeField] private GameObject pauseMenuLayout;

    [Header("Top-level Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;  // opens confirm panel
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("Restart Confirm Panel")]
    [SerializeField] private GameObject restartConfirmPanel;
    [SerializeField] private HoldToConfirmButton restartHoldButton; 
    [SerializeField] private Button restartCancelButton;
    [SerializeField] private GameObject restartFirstSelected;       // focus when confirm panel opens

    [Header("Optional save feedback")]
    [SerializeField] private GameObject saveFeedback; // e.g. "Saved!" text; auto hides

    [Header("Save Hook (optional)")]
    [SerializeField] private MonoBehaviour saveServiceBehaviour; // drag a component that implements IGameSaver
    private IGameSaver saveService;

    private bool pauseToggleRequested = false;


    private bool isGamePaused;
    public bool IsPaused => isGamePaused;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // save service handshake
        saveService = saveServiceBehaviour as IGameSaver;

        HideRestartPrompt();

        // Wire buttons
        if (resumeButton) resumeButton.onClick.AddListener(TogglePauseGame);
        if (restartButton) restartButton.onClick.AddListener(ShowRestartPrompt);
        if(settingsButton) settingsButton.onClick.AddListener(() =>
        {
            SettingsManager settingsManager = FindObjectOfType<SettingsManager>();
            if (settingsManager != null)
            {
                settingsManager.OpenSettings();
            }
        });
        if (saveButton) saveButton.onClick.AddListener(SaveGame);
        if (mainMenuButton) mainMenuButton.onClick.AddListener(QuitToMainMenu);
        if (quitButton) quitButton.onClick.AddListener(QuitToDesktop);

        if (restartCancelButton) restartCancelButton.onClick.AddListener(HideRestartPrompt);
        if (restartHoldButton) restartHoldButton.onCompleted.AddListener(DoRestartConfirmed);

        bool allowManualSave = (GameModeConfig.CurrentMode == GameMode.Zen);
        if (saveButton)
        {
            saveButton.interactable = allowManualSave;
            saveButton.gameObject.SetActive(true);
        }
        else
        {
            saveButton.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        GameInput.Instance.OnPauseAction += GameInput_OnPauseAction;
    }

    private void GameInput_OnPauseAction(object sender, EventArgs e)
    {
        TogglePauseGame();
    }

    // ---------- Restart flow ----------
    private void ShowRestartPrompt()
    {
        if (!restartConfirmPanel) { DoRestartConfirmed(); return; } // safety: restart immediately if no UI
        restartConfirmPanel.SetActive(true);
        pauseMenuLayout.SetActive(false);
        // Set gamepad/keyboard focus
        if (restartFirstSelected) EventSystem.current?.SetSelectedGameObject(restartFirstSelected);
        // Reset the hold button visual
        if (restartHoldButton) restartHoldButton.ResetHold();
    }

    private void HideRestartPrompt()
    {
        if (restartConfirmPanel)
        {
            restartConfirmPanel.SetActive(false);
            pauseMenuLayout.SetActive(true);
        }

        // Return focus to top-level pause menu
        if (firstSelected && isGamePaused) EventSystem.current?.SetSelectedGameObject(firstSelected);
    }

    private void DoRestartConfirmed()
    {
        // Restore time before loading
        Time.timeScale = 1f;
        HideRestartPrompt();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- Save / Quit ----------
    public void SaveGame()
    {
        // Hard gate in case someone re-enables the button at runtime
        if (GameModeConfig.CurrentMode != GameMode.Zen)
        {
            // Show a small “Manual saving disabled in Standard mode” toast
            if (saveFeedback)
            {
                StopAllCoroutines();
                saveFeedback.SetActive(true);
                StartCoroutine(HideAfterSeconds(saveFeedback, 1.2f));
            }
            return;
        }

        SaveSystem.Save();

        if (saveFeedback)
        {
            StopAllCoroutines();
            saveFeedback.SetActive(true);
            StartCoroutine(HideAfterSeconds(saveFeedback, 1.2f));
        }
    }


    private void HandleCancelAction()
    {
        if (!isGamePaused) return;

        if (restartConfirmPanel != null && restartConfirmPanel.activeSelf)
        {
            HideRestartPrompt();
        }
    }


    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitToDesktop()
    {
        // If you want a confirm step, add a second hold-to-confirm panel like restart.
        Application.Quit();
    }

    // ---------- Core ----------
    public void TogglePauseGame()
    {
        isGamePaused = !isGamePaused;

        if (isGamePaused)
        {
            Time.timeScale = 0f;
            pauseMenuRoot.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            EventSystem.current?.SetSelectedGameObject(firstSelected); // Optional: set initial button focus
            OnGamePaused?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Time.timeScale = 1f;
            pauseMenuRoot.SetActive(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            OnGameUnpaused?.Invoke(this, EventArgs.Empty);
        }
    }



    private System.Collections.IEnumerator HideAfterSeconds(GameObject go, float t)
    {
        float elapsed = 0f;
        while (elapsed < t)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled so it works while paused
            yield return null;
        }
        if (go) go.SetActive(false);
    }
}

/// <summary>
/// Optional save interface—implement this somewhere in your game and assign it in the inspector.
/// </summary>
public interface IGameSaver
{
    void SaveGame();
}
