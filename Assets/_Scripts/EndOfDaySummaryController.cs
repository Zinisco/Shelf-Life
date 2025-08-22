using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

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

    private bool promptActive = false;
    private bool summaryActive = false;

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinue);

        if (promptText != null) promptText.SetActive(false);
        if (summaryPanel != null) summaryPanel.SetActive(false);
    }

    private void OnEnable()
    {
        promptActive = false;
        summaryActive = false;
        Invoke(nameof(ShowPrompt), 1.5f); // Delay to avoid accidental input
    }

    private void ShowPrompt()
    {
        promptActive = true;
        if (promptText != null)
            promptText.SetActive(true);
    }

    private void Update()
    {
        if (promptActive && !summaryActive)
        {
            if (Keyboard.current?.eKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonNorth.wasPressedThisFrame == true)
            {
                ShowSummary();
            }
        }
    }

    private void ShowSummary()
    {
        promptActive = false;
        summaryActive = true;
        if (promptText != null) promptText.SetActive(false);
        if (summaryPanel != null) summaryPanel.SetActive(true);

        int currentDay = FindObjectOfType<DayNightCycle>().GetCurrentDay();

        var currency = CurrencyManager.Instance;
        int booksSold = currency.BooksSoldToday;
        float moneySpent = currency.MoneySpentToday;
        float moneyEarned = currency.MoneyEarnedToday;
        float profit = moneyEarned - moneySpent;

        // Update UI Text
        dayText.text = $"Day {currentDay}";
        customersText.text = $"Customers: TBD"; // If you add tracking later
        booksSoldText.text = $"Books Sold: {booksSold}";
        moneySpentText.text = $"Spent: ${moneySpent:F2}";
        moneyEarnedText.text = $"Earned: ${moneyEarned:F2}";
        profitText.text = $"Profit: ${profit:F2}";
    }


    private void OnContinue()
    {
        if (GameModeConfig.CurrentMode == GameMode.Standard)
            SaveSystem.Save();

        var cycle = FindObjectOfType<DayNightCycle>();
        if (cycle != null)
            cycle.ResetDay();

        if (summaryPanel != null) summaryPanel.SetActive(false);
        gameObject.SetActive(false);
    }
}
