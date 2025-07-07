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
    public Button reviewButton;
    public Button backButton;
    public Button confirmButton;

    [Header("Settings")]
    public int maxBooksPerOrder = 10;

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

        currentOrder[book]++;
        quantityText.text = currentOrder[book].ToString();
        UpdateBookCountText();
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
                else if(txt.name == "GenreText")
                    txt.text = pair.Key.genre;
            }
        }
    }

    private void CloseReviewPanel()
    {
        reviewPanel.SetActive(false);
    }


    private void ConfirmOrder()
    {
        if (deliveryManager != null)
        {
            List<BookDefinition> finalOrder = GetFinalOrder();
            deliveryManager.DeliverCrate(finalOrder);
        }

        currentOrder.Clear();
        CloseReviewPanel();
        UpdateBookCountText();
        foreach (Transform child in contentParent)
        {
            TMP_Text qtyText = child.Find("QuantityText").GetComponent<TMP_Text>();
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
