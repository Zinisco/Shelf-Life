// EndOfDaySummaryController.cs (example)
using UnityEngine;
using UnityEngine.UI;

public class EndOfDaySummaryController : MonoBehaviour
{
    [SerializeField] private Button continueButton;

    private void Awake()
    {
        if (continueButton) continueButton.onClick.AddListener(OnContinue);
    }

    private void OnContinue()
    {
        if (GameModeConfig.CurrentMode == GameMode.Standard)
            SaveSystem.Save();

        // Use DayNightCycle instead of DayManager
        var cycle = FindObjectOfType<DayNightCycle>();
        if (cycle != null)
            cycle.ResetDay();

        gameObject.SetActive(false);
    }
}
