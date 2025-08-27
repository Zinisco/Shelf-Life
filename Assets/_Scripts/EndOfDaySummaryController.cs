using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

public class EndOfDaySummaryController : MonoBehaviour
{
    [Header("Prompt UI")]
    [SerializeField] private GameObject promptText; // "Press E or Y to continue"

    [Header("Summary Panel UI")]
    [SerializeField] private GameObject summaryPanel;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text customersText;
    [SerializeField] private TMP_Text booksSoldText;
    [SerializeField] private TMP_Text moneySpentText;
    [SerializeField] private TMP_Text moneyEarnedText;
    [SerializeField] private TMP_Text profitText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text tooltipText;

    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 1.5f;

    [SerializeField] private TMP_Text promptTextTMP;
    [SerializeField] private TMP_SpriteAsset keyboardSpriteAsset;
    [SerializeField] private TMP_SpriteAsset gamepadSpriteAsset;



    private bool promptActive = false;
    private bool summaryActive = false;

    public static bool IsUIOpen { get; private set; }

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinue);

        if (promptText != null) promptText.SetActive(false);
        if (summaryPanel != null) summaryPanel.SetActive(false);
        if (tooltipText != null) tooltipText.gameObject.SetActive(false);

    }

    private void OnEnable()
    {
        GameInput.Instance.OnControlSchemeChanged += HandleControlSchemeChanged;

        promptActive = false;
        summaryActive = false;
    }

    private void OnDisable()
    {
        if (GameInput.Instance != null)
            GameInput.Instance.OnControlSchemeChanged -= HandleControlSchemeChanged;

    }

    public void ShowPromptWithDelay(float delay)
    {
        Invoke(nameof(ShowPrompt), delay);
    }


    private void ShowPrompt()
    {
        promptActive = true;

        UpdatePromptText();

        if (promptText != null)
            promptText.SetActive(true);

        IsUIOpen = true;
    }



    private void Update()
    {
        if (promptActive && !summaryActive)
        {
            var storeSign = FindObjectOfType<StoreSignController>();
            bool storeIsClosed = storeSign != null && !storeSign.IsStoreOpen();

            if (Keyboard.current?.eKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonNorth.wasPressedThisFrame == true)
            {
                if (storeIsClosed)
                {
                    ShowSummary();
                    if (tooltipText != null) tooltipText.gameObject.SetActive(false); // Hide tooltip if it was showing
                }
                else
                {
                    // Show tooltip message
                    if (tooltipText != null)
                    {
                        tooltipText.text = "Store must be closed to continue...";
                        tooltipText.gameObject.SetActive(true);
                    }
                }
            }
        }
    }



    private void ShowSummary()
    {
        promptActive = false;
        summaryActive = true;

        if (promptText != null) promptText.SetActive(false);
        if (summaryPanel != null) summaryPanel.SetActive(true);

        // Stop time
        Time.timeScale = 0f;

        // Lock player movement/look
        var player = FindObjectOfType<PlayerMovement>();
        if (player != null)
            player.IsLocked = true;

        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Update summary data
        int currentDay = FindObjectOfType<DayNightCycle>().GetCurrentDay();
        var currency = CurrencyManager.Instance;
        int booksSold = currency.BooksSoldToday;
        float moneySpent = currency.MoneySpentToday;
        float moneyEarned = currency.MoneyEarnedToday;
        float profit = moneyEarned - moneySpent;

        dayText.text = $"Day {currentDay}";
        customersText.text = $"Customers: TBD";
        booksSoldText.text = $"Books Sold: {booksSold}";
        moneySpentText.text = $"Spent: ${moneySpent:F2}";
        moneyEarnedText.text = $"Earned: ${moneyEarned:F2}";
        profitText.text = $"Profit: ${profit:F2}";
    }


    private void OnContinue()
    {
        if (!summaryActive) return;
        StartCoroutine(FadeAndReset());
    }

    private void UpdatePromptText()
    {
        if (GameInput.Instance.IsGamepadActive)
        {
            promptTextTMP.spriteAsset = gamepadSpriteAsset;
            promptTextTMP.text = "Press \u00A0\u00A0\u00A0 <sprite name=\"buttonY\"> to continue";
        }
        else
        {
            promptTextTMP.spriteAsset = keyboardSpriteAsset;
            promptTextTMP.text = "Press \u00A0\u00A0\u00A0 <sprite name=\"keyE\"> to continue";
        }
    }

    private void HandleControlSchemeChanged(string newScheme)
    {
        if (promptActive)
            UpdatePromptText();
    }


    private IEnumerator FadeAndReset()
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaled time because Time.timeScale = 0
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            fadeCanvasGroup.alpha = t;
            yield return null;
        }

        if (GameModeConfig.CurrentMode == GameMode.Standard)
            SaveSystem.Save();

        var cycle = FindObjectOfType<DayNightCycle>();
        if (cycle != null)
            cycle.ResetDay();

        if (summaryPanel != null) summaryPanel.SetActive(false);
        gameObject.SetActive(false);
        IsUIOpen = false;

        // Resume time
        Time.timeScale = 1f;

        // Re-lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Unlock player
        var player = FindObjectOfType<PlayerMovement>();
        if (player != null)
            player.IsLocked = false;

        // Reset fade
        fadeCanvasGroup.alpha = 0f;

        var sign = FindObjectOfType<StoreSignController>();
        if (sign != null)
            sign.ResetForNewDay();
    }

}
