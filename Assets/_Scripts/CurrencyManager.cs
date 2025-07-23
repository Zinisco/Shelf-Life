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
        currentMoney = startingMoney;
        UpdateUI();
    }

    public bool CanAfford(int amount)
    {
        return currentMoney >= amount;
    }

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

    public int GetBalance()
    {
        return currentMoney;
    }

    private void UpdateUI()
    {
        if (walletText != null)
            walletText.text = $"$ {currentMoney}";
    }
}
