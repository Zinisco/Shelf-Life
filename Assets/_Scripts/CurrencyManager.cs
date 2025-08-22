using UnityEngine;
using TMPro;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [SerializeField] private int startingMoney = 100;
    [SerializeField] private TMP_Text walletText;

    private int currentMoney;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // On a fresh run (no save), start with the default
        currentMoney = startingMoney;
        UpdateUI();
    }

    public bool CanAfford(int amount) => currentMoney >= amount;

    public bool Spend(int amount)
    {
        if (!CanAfford(amount)) return false;
        currentMoney -= amount;
        UpdateUI();
        return true;
    }

    public void Add(int amount)
    {
        currentMoney += amount;
        UpdateUI();
    }

    public int GetBalance() => currentMoney;

    // NEW: allow save system to restore the exact balance
    public void SetBalance(int amount)
    {
        currentMoney = Mathf.Max(0, amount);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (walletText != null)
        {
            if (GameModeConfig.CurrentMode == GameMode.Zen)
                walletText.text = "Zen Mode";
            else
                walletText.text = $"$ {currentMoney}";
        }
    }
}
