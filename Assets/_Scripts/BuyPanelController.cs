using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuyPanelController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject bookEntryPrefab;
    public Transform contentParent;
    public GameObject reviewPanel;
    public Transform reviewContentParent;
    public GameObject reviewEntryPrefab;
    public TMP_Text bookCountText;
    public TMP_Text walletText;
    public TMP_Text totalCostTextFirstPage;
    public TMP_Text totalCostText;
    public Button reviewButton;
    public Button backButton;
    public Button confirmButton;

    [Header("Settings")]
    public int maxBooksPerOrder = 20;
    public int maxPerBookType = 5;

    [Header("Systems")]
    public CrateDeliveryManager deliveryManager;
    public BookDatabase bookDatabase;

    public List<BookDefinition> availableBooks = new List<BookDefinition>();
    private Dictionary<BookDefinition, int> currentOrder = new Dictionary<BookDefinition, int>();

    private void Start()
    {
        // Auto-fill from BookDatabase
        if (bookDatabase != null)
            availableBooks = bookDatabase.allBooks;

        UpdateWalletUI();
        UpdateConfirmButtonState(); // shows "Total: $0" on load

        reviewPanel.SetActive(false);
        PopulateAvailableBooks();

        reviewButton.onClick.AddListener(OpenReviewPanel);
        backButton.onClick.AddListener(CloseReviewPanel);
        confirmButton.onClick.AddListener(ConfirmOrder);
    }

    private void PopulateAvailableBooks()
    {
        foreach (var book in availableBooks)
        {
            GameObject entry = Instantiate(bookEntryPrefab, contentParent);
            entry.transform.Find("TitleText").GetComponent<TMP_Text>().text = book.title;

            Button plusButton = entry.transform.Find("PlusButton").GetComponent<Button>();
            Button minusButton = entry.transform.Find("MinusButton").GetComponent<Button>();
            TMP_Text quantityText = entry.transform.Find("QuantityText").GetComponent<TMP_Text>();
            TMP_Text priceText = entry.transform.Find("PriceText").GetComponent<TMP_Text>();


            quantityText.text = "0";

            plusButton.onClick.AddListener(() => {
                AddBook(book, quantityText);
            });

            minusButton.onClick.AddListener(() => {
                RemoveBook(book, quantityText);
            });
        }

        UpdateBookCountText();
    }

    private void AddBook(BookDefinition book, TMP_Text quantityText)
    {
        int total = GetTotalBookCount();
        if (total >= maxBooksPerOrder)
            return;

        if (!currentOrder.ContainsKey(book))
            currentOrder[book] = 0;

        // Check per-book limit
        if (currentOrder[book] >= maxPerBookType)
            return;

        if (currentOrder[book] >= maxPerBookType)
        {
            Debug.Log("Can't order more than 5 of the same book.");
            return;
        }

        currentOrder[book]++;
        quantityText.text = currentOrder[book].ToString();
        UpdateBookCountText();
        UpdateConfirmButtonState();
    }


    private void RemoveBook(BookDefinition book, TMP_Text quantityText)
    {
        if (!currentOrder.ContainsKey(book))
            return;

        currentOrder[book]--;
        if (currentOrder[book] <= 0)
            currentOrder.Remove(book);

        quantityText.text = currentOrder.ContainsKey(book) ? currentOrder[book].ToString() : "0";
        UpdateBookCountText();
        UpdateConfirmButtonState();
    }


    private int GetTotalBookCount()
    {
        int total = 0;
        foreach (var pair in currentOrder)
            total += pair.Value;
        return total;
    }

    private void UpdateBookCountText()
    {
        bookCountText.text = $"{GetTotalBookCount()}";
    }

    private void OpenReviewPanel()
    {
        reviewPanel.SetActive(true);

        foreach (Transform child in reviewContentParent)
            Destroy(child.gameObject);

        foreach (var pair in currentOrder)
        {
            GameObject entry = Instantiate(reviewEntryPrefab, reviewContentParent);

            TMP_Text[] texts = entry.GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text txt in texts)
            {
                if (txt.name == "TitleText")
                    txt.text = pair.Key.title;
                else if (txt.name == "QuantityText")
                    txt.text = $"x{pair.Value}";
                else if (txt.name == "GenreText")
                    txt.text = pair.Key.genre;
                else if (txt.name == "PriceText")
                    txt.text = $"${pair.Key.cost}";
            }
        }

        int totalCost = GetTotalCost();

        if (totalCostText != null)
            totalCostText.text = $"Total: ${totalCost}";

        UpdateConfirmButtonState();
    }

    private void UpdateConfirmButtonState()
    {
        int totalCost = GetTotalCost();

        confirmButton.interactable = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAfford(totalCost);

        if (totalCostText != null)
            totalCostText.text = $"Total: ${totalCost}";

        if (totalCostTextFirstPage != null)
            totalCostTextFirstPage.text = $"Total: ${totalCost}";
    }


    private void CloseReviewPanel()
    {
        reviewPanel.SetActive(false);
    }

    private void UpdateWalletUI()
    {
        if (walletText != null && CurrencyManager.Instance != null)
            walletText.text = $"$ {CurrencyManager.Instance.GetBalance()}";
    }


    private void ConfirmOrder()
    {
        List<BookDefinition> finalOrder = GetFinalOrder();
        int totalCost = 0;
        foreach (var book in finalOrder)
            totalCost += book.cost;

        if (CurrencyManager.Instance != null && CurrencyManager.Instance.Spend(totalCost))
        {
            deliveryManager.DeliverCrate(finalOrder);
            Debug.Log($"Spent ${totalCost} on crate with {finalOrder.Count} books.");
        }
        else
        {
            Debug.LogWarning("Not enough money!");
            return;
        }

        currentOrder.Clear();
        CloseAll();
        UpdateBookCountText();
        UpdateWalletUI(); // Refresh after spending
        UpdateConfirmButtonState();

        foreach (Transform child in contentParent)
        {
            TMP_Text qtyText = child.Find("QuantityText").GetComponent<TMP_Text>();
            qtyText.text = "0";
        }
    }

    public void CloseAll()
    {
        this.gameObject.SetActive(false);
        reviewPanel.gameObject.SetActive(false);
    }

    private int GetTotalCost()
    {
        int total = 0;
        foreach (var pair in currentOrder)
            total += pair.Key.cost * pair.Value;
        return total;
    }

    public void ResetOrder()
    {
        currentOrder.Clear();
        UpdateBookCountText();
        UpdateConfirmButtonState();

        foreach (Transform child in contentParent)
        {
            TMP_Text qtyText = child.Find("QuantityText").GetComponent<TMP_Text>();
            if (qtyText != null)
                qtyText.text = "0";
        }
    }


    public List<BookDefinition> GetFinalOrder()
    {
        List<BookDefinition> finalList = new List<BookDefinition>();
        foreach (var pair in currentOrder)
        {
            for (int i = 0; i < pair.Value; i++)
                finalList.Add(pair.Key);
        }
        return finalList;
    }
}
