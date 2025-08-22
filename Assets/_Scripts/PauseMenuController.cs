using System;
using TMPro;
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
    [SerializeField] private GameObject settingsMenu;
    private StoreSignController storeSign;


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

    [Header("Quit Confirm Panel")]
    [SerializeField] private GameObject quitConfirmPanel;
    [SerializeField] private HoldToConfirmButton quitHoldButton;
    [SerializeField] private Button quitCancelButton;
    [SerializeField] private GameObject quitFirstSelected;

    [Header("Tooltip")]
    [SerializeField] private GameObject saveTooltip;

    [Header("Optional save feedback")]
    [SerializeField] private GameObject saveFeedback; // e.g. "Saved!" text; auto hides

    [Header("Save Hook (optional)")]
    [SerializeField] private MonoBehaviour saveServiceBehaviour; // drag a component that implements IGameSaver
    private IGameSaver saveService;

    private bool isBeforeStoreOpens = true;


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

        if (quitCancelButton) quitCancelButton.onClick.AddListener(HideQuitPrompt);
        if (quitHoldButton) quitHoldButton.onCompleted.AddListener(DoQuitConfirmed);


        if (saveButton)
        {
            saveButton.interactable = true; // Always allow interaction
            saveButton.gameObject.SetActive(true);
        }

    }

    private void Start()
    {
        storeSign = FindObjectOfType<StoreSignController>();

        GameInput.Instance.OnPauseAction += GameInput_OnPauseAction;
    }

    private void Update()
    {
        DayNightCycle dayCycle = FindObjectOfType<DayNightCycle>();
        if (dayCycle == null)
        {
            Debug.LogWarning("DayNightCycle not found. Cannot determine save availability.");
            return;
        }

        bool isBefore = false;
        bool isDayEnded = false;

        if (dayCycle != null)
            isBefore = !dayCycle.IsTimeRunning;

        if (storeSign != null)
            isDayEnded = storeSign.HasDayEnded;

        // Only allow save before store opens
        bool allowSave = isBefore && !isDayEnded;

        if (saveButton != null)
        {
            saveButton.interactable = allowSave;

            if (saveTooltip != null)
                saveTooltip.SetActive(!allowSave);
        }

        // Reset condition (e.g., if returning to pre-day state)
        if (!isBeforeStoreOpens && allowSave)
        {
            Debug.Log("Save re-enabled at start of new day.");
            if (saveButton != null) saveButton.interactable = true;
            if (saveTooltip != null) saveTooltip.SetActive(false);
        }

        isBeforeStoreOpens = allowSave;
    }



    private void GameInput_OnPauseAction(object sender, EventArgs e)
    {
        TogglePauseGame();
    }

    // ---------- Restart flow ----------
    private void ShowRestartPrompt()
    {
        if (!restartConfirmPanel)
        {
            DoRestartConfirmed(); // fallback
            return;
        }

        restartConfirmPanel.SetActive(true);
        restartHoldButton.ResetHold();
        pauseMenuLayout.SetActive(false);

        if (restartFirstSelected)
            EventSystem.current?.SetSelectedGameObject(restartFirstSelected);
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
        if (storeSign == null || saveButton == null) return;

        bool canSave = saveButton.interactable;

        if (canSave)
        {
            SaveSystem.Save();

            if (saveFeedback)
            {
                StopAllCoroutines();
                saveFeedback.SetActive(true);
                StartCoroutine(HideAfterSeconds(saveFeedback, 1.2f));
            }
        }
        else
        {
            if (saveFeedback && saveFeedback.TryGetComponent<TMP_Text>(out var feedbackText))
            {
                StopAllCoroutines();
                feedbackText.text = "Saving disabled after store has opened.";
                saveFeedback.SetActive(true);
                StartCoroutine(HideAfterSeconds(saveFeedback, 1.5f));
            }

            Debug.Log("Cannot save during or after store hours.");
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
        if (GameModeConfig.CurrentMode == GameMode.Zen)
        {
            Application.Quit();
        }
        else
        {
            ShowQuitPrompt();
        }
    }

    private void ShowQuitPrompt()
    {
        if (!quitConfirmPanel)
        {
            DoQuitConfirmed(); // fallback if panel is missing
            return;
        }

        quitConfirmPanel.SetActive(true);
        quitHoldButton.ResetHold();
        pauseMenuLayout.SetActive(false);

        if (quitFirstSelected)
            EventSystem.current?.SetSelectedGameObject(quitFirstSelected);
    }

    private void HideQuitPrompt()
    {
        if (quitConfirmPanel)
        {
            quitConfirmPanel.SetActive(false);
            pauseMenuLayout.SetActive(true);
        }

        if (firstSelected && isGamePaused)
            EventSystem.current?.SetSelectedGameObject(firstSelected);
    }

    private void DoQuitConfirmed()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }

    // ---------- Pause / Unpause ----------

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
            settingsMenu.SetActive(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            HideRestartPrompt(); // ensure restart panel closes
            HideQuitPrompt(); 

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
