using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    public Button randomButton;
    public Button plusButton;
    public Button minusButton;
    public GameObject priceTextButton;

    [Header("Filter UI")]
    public TMP_Dropdown genreDropdown;

    [Header("Random Button Panel")]
    public GameObject randomQuantityPanel;
    public TMP_InputField quantityInputField;
    public Button confirmRandomButton;
    public Button cancelRandomButton;

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
        if (GameModeConfig.CurrentMode == GameMode.Zen)
        {
            priceTextButton.SetActive(false);
        }

        // Auto-fill from BookDatabase
        if (bookDatabase != null)
            availableBooks = bookDatabase.allBooks;

        // Populate dropdown with enum names
        genreDropdown.ClearOptions();
        var options = System.Enum.GetNames(typeof(BookDefinition.Genre)).ToList();
        options.Insert(0, "All"); // Add "All" option at the top
        genreDropdown.AddOptions(options);

        // Listen for changes
        genreDropdown.onValueChanged.AddListener(OnGenreFilterChanged);

        UpdateWalletUI();
        UpdateConfirmButtonState();

        reviewPanel.SetActive(false);
        PopulateAvailableBooks();

        reviewButton.onClick.AddListener(OpenReviewPanel);
        backButton.onClick.AddListener(CloseReviewPanel);
        confirmButton.onClick.AddListener(ConfirmOrder);
        randomButton.onClick.AddListener(() => randomQuantityPanel.SetActive(true));
        confirmRandomButton.onClick.AddListener(OnConfirmRandomSelection);
        cancelRandomButton.onClick.AddListener(() => randomQuantityPanel.SetActive(false));

        // Hook up random panel plus/minus
        plusButton.onClick.AddListener(() => ChangeRandomQuantity(1));
        minusButton.onClick.AddListener(() => ChangeRandomQuantity(-1));

        // Clamp typed values
        quantityInputField.onEndEdit.AddListener((val) =>
        {
            if (int.TryParse(val, out int newQty))
            {
                newQty = Mathf.Clamp(newQty, 1, maxBooksPerOrder);
                quantityInputField.text = newQty.ToString();
            }
            else
            {
                // fallback if input is invalid
                quantityInputField.text = "1";
            }

            UpdateRandomConfirmButtonState();
        });

        UpdateRandomConfirmButtonState();
    }


    private void OnEnable()
    {
        UpdateWalletUI();
        RefreshStockUI();        // update stock text in BuyPanel
        RefreshReviewStockUI();  // update stock text in ReviewPanel
    }


    private void PopulateAvailableBooks()
    {
        foreach (var book in availableBooks)
        {
            GameObject entry = Instantiate(bookEntryPrefab, contentParent);
            var ui = entry.GetComponent<BookEntryUI>();
            if (ui == null) continue;

            // Fill UI
            ui.titleText.text = book.title;
            ui.priceText.text = GameModeConfig.CurrentMode == GameMode.Zen ? " " : $"${book.cost}";
            ui.quantityInputField.text = "0";

            // NEW: show current stock
            int qtyOnHand = InventoryManager.Instance != null
                ? InventoryManager.Instance.GetQuantity(book.bookID)
                : 0;
            ui.onHandText.text = $"On Hand: {qtyOnHand}";

            if (ui.bookImage != null)
            {
                ui.bookImage.sprite = book.thumbnail;
                ui.bookImage.preserveAspect = true;
            }

            // Hook up buttons
            ui.plusButton.onClick.AddListener(() => AddBook(book, ui.quantityInputField));
            ui.minusButton.onClick.AddListener(() => RemoveBook(book, ui.quantityInputField));

            // Hook up input
            ui.quantityInputField.onEndEdit.AddListener((val) =>
            {
                if (int.TryParse(val, out int newQty))
                {
                    newQty = Mathf.Clamp(newQty, 0, maxPerBookType);
                    SetBookQuantity(book, newQty, ui.quantityInputField);
                }
                else
                {
                    ui.quantityInputField.text = currentOrder.ContainsKey(book)
                        ? currentOrder[book].ToString()
                        : "0";
                }
            });
        }

        UpdateBookCountText();
    }


    private void AddBook(BookDefinition book, TMP_InputField qtyInput)
    {
        int total = GetTotalBookCount();
        if (total >= maxBooksPerOrder) return;

        if (!currentOrder.ContainsKey(book))
            currentOrder[book] = 0;

        if (currentOrder[book] >= maxPerBookType) return;

        currentOrder[book]++;
        qtyInput.text = currentOrder[book].ToString();

        UpdateBookCountText();
        UpdateConfirmButtonState();
    }


    private void RemoveBook(BookDefinition book, TMP_InputField qtyInput)
    {
        if (!currentOrder.ContainsKey(book)) return;

        currentOrder[book]--;
        if (currentOrder[book] <= 0)
            currentOrder.Remove(book);

        qtyInput.text = currentOrder.ContainsKey(book) ? currentOrder[book].ToString() : "0";

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
            BookDefinition book = pair.Key;
            int quantity = pair.Value;

            GameObject entry = Instantiate(reviewEntryPrefab, reviewContentParent);
            var ui = entry.GetComponent<ReviewBookEntryUI>();

            // Fill UI
            ui.titleText.text = book.title;
            ui.genreText.text = book.genre.ToString();
            ui.priceText.text = GameModeConfig.CurrentMode == GameMode.Zen ? " " : $"${book.cost}";
            ui.quantityInputField.text = quantity.ToString();

            if (ui.bookImage != null)
            {
                ui.bookImage.sprite = book.thumbnail;
                ui.bookImage.preserveAspect = true;
            }

            // NEW: show current stock
            if (ui.onHandText != null && InventoryManager.Instance != null)
            {
                int qtyOnHand = InventoryManager.Instance.GetQuantity(book.bookID);
                ui.onHandText.text = $"On Hand: {qtyOnHand}";
            }

            // Hook up buttons
            ui.plusButton.onClick.AddListener(() => AddBook(book, ui.quantityInputField));
            ui.minusButton.onClick.AddListener(() => RemoveBook(book, ui.quantityInputField));

            // Hook up input
            ui.quantityInputField.onEndEdit.AddListener((val) =>
            {
                if (int.TryParse(val, out int newQty))
                {
                    newQty = Mathf.Clamp(newQty, 0, maxPerBookType);
                    SetBookQuantity(book, newQty, ui.quantityInputField);
                }
                else
                {
                    ui.quantityInputField.text = currentOrder.ContainsKey(book)
                        ? currentOrder[book].ToString()
                        : "0";
                }
            });
        }

        int totalCost = GetTotalCost();

        if (GameModeConfig.CurrentMode == GameMode.Zen)
        {
            if (totalCostText != null) totalCostText.text = "Free!";
        }
        else
        {
            if (totalCostText != null)
                totalCostText.text = $"Total: ${totalCost}";
        }

        UpdateConfirmButtonState();
        RefreshStockUI();
        RefreshReviewStockUI();
    }




    private void UpdateConfirmButtonState()
    {
        int totalCost = GetTotalCost();

        if (GameModeConfig.CurrentMode == GameMode.Zen)
        {
            confirmButton.interactable = true;
            if (totalCostText != null) totalCostText.text = " ";
            if (totalCostTextFirstPage != null) totalCostTextFirstPage.text = " ";
        }
        else
        {
            confirmButton.interactable = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAfford(totalCost);
            if (totalCostText != null) totalCostText.text = $"Total: ${totalCost}";
            if (totalCostTextFirstPage != null) totalCostTextFirstPage.text = $"Total: ${totalCost}";
        }

    }

    private void SetBookQuantity(BookDefinition book, int newQty, TMP_InputField qtyInput)
    {
        newQty = Mathf.Clamp(newQty, 0, maxPerBookType);

        if (newQty == 0)
        {
            currentOrder.Remove(book);
        }
        else
        {
            currentOrder[book] = newQty;
        }

        // Force UI update to show clamped value
        qtyInput.text = currentOrder.ContainsKey(book) ? currentOrder[book].ToString() : "0";

        UpdateBookCountText();
        UpdateConfirmButtonState();
    }


    private void CloseReviewPanel()
    {
        reviewPanel.SetActive(false);
    }

    private void CloseRandomPanel()
    {
        randomQuantityPanel.SetActive(false);
    }

    private void UpdateWalletUI()
    {
        if (walletText != null && CurrencyManager.Instance != null)
            walletText.text = $"$ {CurrencyManager.Instance.GetBalance()}";
    }

    private void OnConfirmRandomSelection()
    {
        int quantity = Mathf.Clamp(int.Parse(quantityInputField.text), 1, maxBooksPerOrder);
        AddRandomBooksToOrder(quantity);
        randomQuantityPanel.SetActive(false);
    }

    private void AddRandomBooksToOrder(int quantity)
    {
        int added = 0;

        while (added < quantity)
        {
            // Pick a completely random book
            BookDefinition randomBook = availableBooks[Random.Range(0, availableBooks.Count)];

            if (!currentOrder.ContainsKey(randomBook))
                currentOrder[randomBook] = 0;

            if (currentOrder[randomBook] >= maxPerBookType)
                continue; // skip, try again

            currentOrder[randomBook]++;
            added++;
        }

        RefreshUIQuantities();
        UpdateBookCountText();
        UpdateConfirmButtonState();
    }


    private void ConfirmOrder()
    {
        List<BookDefinition> finalOrder = GetFinalOrder();
        int totalCost = 0;
        foreach (var book in finalOrder)
            totalCost += book.cost;

        if (GameModeConfig.CurrentMode == GameMode.Zen)
        {
            // Zen mode: no payment required
            deliveryManager.DeliverCrate(finalOrder);
            Debug.Log($"[ZenMode] Delivered crate with {finalOrder.Count} books for free.");
        }
        else if (CurrencyManager.Instance != null && CurrencyManager.Instance.Spend(totalCost))
        {
            deliveryManager.DeliverCrate(finalOrder);
            Debug.Log($"Spent ${totalCost} on crate with {finalOrder.Count} books.");
        }
        else
        {
            Debug.LogWarning("Not enough money!");
            return;
        }

        // Clear order
        currentOrder.Clear();
        CloseAll();
        UpdateBookCountText();
        UpdateWalletUI();
        UpdateConfirmButtonState();
        RefreshStockUI();
        RefreshReviewStockUI();

        // Reset UI quantities on first page
        foreach (Transform child in contentParent)
        {
            var ui = child.GetComponent<BookEntryUI>(); // was ReviewBookEntryUI
            if (ui != null)
                ui.quantityInputField.text = "0";
        }

        foreach (var book in finalOrder)
        {
            InventoryManager.Instance.AddStock(book, 1);
        }

    }


    public void CloseAll()
    {
        this.gameObject.SetActive(false);
        reviewPanel.gameObject.SetActive(false);
    }

    private void RefreshUIQuantities()
    {
        foreach (Transform child in contentParent)
        {
            var ui = child.GetComponent<BookEntryUI>();
            if (ui == null) continue;

            var book = availableBooks.Find(b => b.title == ui.titleText.text);
            if (book != null && currentOrder.ContainsKey(book))
                ui.quantityInputField.text = currentOrder[book].ToString();
            else
                ui.quantityInputField.text = "0";
        }
    }

    private void RefreshStockUI()
    {
        foreach (Transform child in contentParent)
        {
            var ui = child.GetComponent<BookEntryUI>();
            if (ui == null) continue;

            var book = availableBooks.Find(b => b.title == ui.titleText.text);
            if (book != null && ui.onHandText != null)
            {
                int qtyOnHand = InventoryManager.Instance != null
                    ? InventoryManager.Instance.GetQuantity(book.bookID)
                    : 0;
                ui.onHandText.text = $"On Hand: {qtyOnHand}";
            }
        }
    }

    private void RefreshReviewStockUI()
    {
        foreach (Transform child in reviewContentParent)
        {
            var ui = child.GetComponent<ReviewBookEntryUI>();
            if (ui == null) continue;

            var book = availableBooks.Find(b => b.title == ui.titleText.text);
            if (book != null && ui.onHandText != null)
            {
                int qtyOnHand = InventoryManager.Instance != null
                    ? InventoryManager.Instance.GetQuantity(book.bookID)
                    : 0;
                ui.onHandText.text = $"On Hand: {qtyOnHand}";
            }
        }
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
            var ui = child.GetComponent<ReviewBookEntryUI>();
            if (ui != null)
                ui.quantityInputField.text = "0";
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

    private void ChangeRandomQuantity(int delta)
    {
        int current = 1;
        int.TryParse(quantityInputField.text, out current);

        current += delta;
        current = Mathf.Clamp(current, 1, maxBooksPerOrder);

        quantityInputField.text = current.ToString();
        UpdateRandomConfirmButtonState();
    }


    private void UpdateRandomConfirmButtonState()
    {
        if (int.TryParse(quantityInputField.text, out int qty))
        {
            confirmRandomButton.interactable = qty >= 1;
        }
        else
        {
            confirmRandomButton.interactable = false;
        }
    }



    private void OnGenreFilterChanged(int index)
    {
        // If "All" is selected
        if (index == 0)
        {
            availableBooks = bookDatabase.allBooks;
        }
        else
        {
            BookDefinition.Genre selectedGenre =
                (BookDefinition.Genre)System.Enum.Parse(typeof(BookDefinition.Genre), genreDropdown.options[index].text);

            availableBooks = bookDatabase.allBooks
                .Where(b => b.genre == selectedGenre)
                .ToList();
        }

        // Refresh UI with filtered list
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        PopulateAvailableBooks();
    }

}
