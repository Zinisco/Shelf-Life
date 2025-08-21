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
    [Header("Root & Focus")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject firstSelected; // focus when opening pause

    [Header("Top-level Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;  // opens confirm panel
    [SerializeField] private Button saveButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("Restart Confirm Panel")]
    [SerializeField] private GameObject restartConfirmPanel;
    [SerializeField] private HoldToConfirmButton restartHoldButton; // attach your script
    [SerializeField] private Button restartCancelButton;
    [SerializeField] private GameObject restartFirstSelected;       // focus when confirm panel opens

    [Header("Optional save feedback")]
    [SerializeField] private GameObject saveFeedback; // e.g. "Saved!" text; auto hides

    [Header("Save Hook (optional)")]
    [SerializeField] private MonoBehaviour saveServiceBehaviour; // drag a component that implements IGameSaver
    private IGameSaver saveService;

    private bool _isPaused;

    void Awake()
    {
        // Optional save service handshake
        saveService = saveServiceBehaviour as IGameSaver;

        // Start hidden & unpaused
        SetPaused(false, force: true);
        HideRestartPrompt();

        // Wire buttons
        if (resumeButton) resumeButton.onClick.AddListener(Resume);
        if (restartButton) restartButton.onClick.AddListener(ShowRestartPrompt);
        if (saveButton) saveButton.onClick.AddListener(SaveGame);
        if (mainMenuButton) mainMenuButton.onClick.AddListener(QuitToMainMenu);
        if (quitButton) quitButton.onClick.AddListener(QuitToDesktop);

        if (restartCancelButton) restartCancelButton.onClick.AddListener(HideRestartPrompt);
        if (restartHoldButton) restartHoldButton.onCompleted.AddListener(DoRestartConfirmed);
    }

    void Update()
    {
        // Esc toggles pause unless the confirm panel is showing (then Esc cancels it)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (restartConfirmPanel && restartConfirmPanel.activeSelf)
                HideRestartPrompt();
            else
                TogglePause();
        }
    }

    // ---------- Public API ----------
    public void TogglePause() => SetPaused(!_isPaused);
    public void Resume() => SetPaused(false);

    // ---------- Restart flow ----------
    private void ShowRestartPrompt()
    {
        if (!restartConfirmPanel) { DoRestartConfirmed(); return; } // safety: restart immediately if no UI
        restartConfirmPanel.SetActive(true);
        // Set gamepad/keyboard focus
        if (restartFirstSelected) EventSystem.current?.SetSelectedGameObject(restartFirstSelected);
        // Reset the hold button visual
        if (restartHoldButton) restartHoldButton.ResetHold();
    }

    private void HideRestartPrompt()
    {
        if (restartConfirmPanel) restartConfirmPanel.SetActive(false);
        // Return focus to top-level pause menu
        if (firstSelected && _isPaused) EventSystem.current?.SetSelectedGameObject(firstSelected);
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
        SaveSystem.Save();

        if (saveFeedback)
        {
            StopAllCoroutines();
            saveFeedback.SetActive(true);
            StartCoroutine(HideAfterSeconds(saveFeedback, 1.2f)); // little “Saved!” toast
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
    private void SetPaused(bool paused, bool force = false)
    {
        if (_isPaused == paused && !force) return;
        _isPaused = paused;

        if (pauseMenuRoot) pauseMenuRoot.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;

        Cursor.visible = paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;

        // Clear/Set selection for nav
        if (paused)
            EventSystem.current?.SetSelectedGameObject(firstSelected ? firstSelected : null);
        else
            EventSystem.current?.SetSelectedGameObject(null);

        // If closing, also close any confirm panels
        if (!paused) HideRestartPrompt();
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

    void OnDestroy()
    {
        if (_isPaused) Time.timeScale = 1f; // safety
    }
}

/// <summary>
/// Optional save interface—implement this somewhere in your game and assign it in the inspector.
/// </summary>
public interface IGameSaver
{
    void SaveGame();
}
