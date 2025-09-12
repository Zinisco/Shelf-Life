using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DesignPanelController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject itemEntryPrefab;
    [SerializeField] private Transform contentParent;

    [SerializeField] private GameObject reviewPanel;
    [SerializeField] private Transform reviewContentParent;
    [SerializeField] private GameObject reviewEntryPrefab;

    [SerializeField] private TMP_Text itemCountText;
    [SerializeField] private TMP_Text walletText;
    [SerializeField] private TMP_Text totalCostTextFirstPage;
    [SerializeField] private TMP_Text totalCostText;

    [SerializeField] private Button reviewButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button confirmButton;

    [Header("Category Buttons")]
    [SerializeField] private Button furnitureButton;
    [SerializeField] private Button lightingButton;
    [SerializeField] private Button decorButton;
    [SerializeField] private Button paintButton;

    [Header("Settings")]
    [SerializeField] private int maxItemsPerOrder = 20;
    [SerializeField] private int maxPerItemType = 5;

    [Header("Systems")]
    [SerializeField] private DesignItemDatabase designDatabase;
    //[SerializeField] private DeliveryManager deliveryManager;

    private List<DesignItem> availableItems = new List<DesignItem>();
    private Dictionary<DesignItem, int> currentOrder = new Dictionary<DesignItem, int>();
    private DesignItem.Category currentCategory = DesignItem.Category.Furniture;

    private void Start()
    {
        if (designDatabase != null)
            availableItems = designDatabase.Items;

        UpdateWalletUI();
        UpdateConfirmButtonState();

        reviewPanel.SetActive(false);
        ShowCategory(DesignItem.Category.Furniture);

        reviewButton.onClick.AddListener(OpenReviewPanel);
        backButton.onClick.AddListener(CloseReviewPanel);
        confirmButton.onClick.AddListener(ConfirmOrder);

        furnitureButton.onClick.AddListener(() => ShowCategory(DesignItem.Category.Furniture));
        lightingButton.onClick.AddListener(() => ShowCategory(DesignItem.Category.Lighting));
        decorButton.onClick.AddListener(() => ShowCategory(DesignItem.Category.Decor));
        paintButton.onClick.AddListener(() => ShowCategory(DesignItem.Category.Paint));
    }

    private void ShowCategory(DesignItem.Category category)
    {
        currentCategory = category;

        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        var itemsInCategory = availableItems.Where(i => i.category == category).ToList();

        foreach (var item in itemsInCategory)
        {
            GameObject entry = Instantiate(itemEntryPrefab, contentParent);
            var ui = entry.GetComponent<ItemEntryUI>();
            if (ui == null) continue;

            // Fill UI
            ui.itemName.text = item.itemName;
            ui.priceText.text = $"${item.price}";
            ui.quantityInputField.text = currentOrder.ContainsKey(item) ? currentOrder[item].ToString() : "0";

            if (ui.itemImage != null)
            {
                ui.itemImage.sprite = item.itemImage;
                ui.itemImage.preserveAspect = true;
            }

            // Hook up buttons
            ui.plusButton.onClick.AddListener(() => AddItem(item, ui.quantityInputField));
            ui.minusButton.onClick.AddListener(() => RemoveItem(item, ui.quantityInputField));

            // Hook up input
            ui.quantityInputField.onEndEdit.AddListener((val) =>
            {
                if (int.TryParse(val, out int newQty))
                {
                    newQty = Mathf.Clamp(newQty, 0, maxPerItemType);
                    SetItemQuantity(item, newQty, ui.quantityInputField);
                }
                else
                {
                    ui.quantityInputField.text = currentOrder.ContainsKey(item)
                        ? currentOrder[item].ToString()
                        : "0";
                }
            });
        }

        UpdateItemCountText();
    }

    private void AddItem(DesignItem item, TMP_InputField qtyInput)
    {
        int total = GetTotalItemCount();
        if (total >= maxItemsPerOrder) return;

        if (!currentOrder.ContainsKey(item))
            currentOrder[item] = 0;

        if (currentOrder[item] >= maxPerItemType) return;

        currentOrder[item]++;
        qtyInput.text = currentOrder[item].ToString();

        UpdateItemCountText();
        UpdateConfirmButtonState();
    }

    private void RemoveItem(DesignItem item, TMP_InputField qtyInput)
    {
        if (!currentOrder.ContainsKey(item)) return;

        currentOrder[item]--;
        if (currentOrder[item] <= 0)
            currentOrder.Remove(item);

        qtyInput.text = currentOrder.ContainsKey(item) ? currentOrder[item].ToString() : "0";

        UpdateItemCountText();
        UpdateConfirmButtonState();
    }

    private void SetItemQuantity(DesignItem item, int newQty, TMP_InputField qtyInput)
    {
        newQty = Mathf.Clamp(newQty, 0, maxPerItemType);

        if (newQty == 0)
            currentOrder.Remove(item);
        else
            currentOrder[item] = newQty;

        qtyInput.text = currentOrder.ContainsKey(item) ? currentOrder[item].ToString() : "0";

        UpdateItemCountText();
        UpdateConfirmButtonState();
    }

    private int GetTotalItemCount()
    {
        return currentOrder.Values.Sum();
    }

    private void UpdateItemCountText()
    {
        itemCountText.text = $"{GetTotalItemCount()}";
    }

    private void OpenReviewPanel()
    {
        reviewPanel.SetActive(true);

        foreach (Transform child in reviewContentParent)
            Destroy(child.gameObject);

        foreach (var pair in currentOrder)
        {
            DesignItem item = pair.Key;
            int quantity = pair.Value;

            GameObject entry = Instantiate(reviewEntryPrefab, reviewContentParent);
            var ui = entry.GetComponent<ReviewEntryUI>();
            if (ui == null) continue;

            // Fill UI
            ui.itemName.text = item.itemName;
            ui.itemType.text = item.category.ToString();
            ui.priceText.text = $"${item.price}";
            ui.quantityInputField.text = quantity.ToString();

            if (ui.itemImage != null)
            {
                ui.itemImage.sprite = item.itemImage;
                ui.itemImage.preserveAspect = true;
            }

            // Hook up buttons
            ui.plusButton.onClick.AddListener(() => AddItem(item, ui.quantityInputField));
            ui.minusButton.onClick.AddListener(() => RemoveItem(item, ui.quantityInputField));

            // Hook up input
            ui.quantityInputField.onEndEdit.AddListener((val) =>
            {
                if (int.TryParse(val, out int newQty))
                {
                    newQty = Mathf.Clamp(newQty, 0, maxPerItemType);
                    SetItemQuantity(item, newQty, ui.quantityInputField);
                }
                else
                {
                    ui.quantityInputField.text = currentOrder.ContainsKey(item)
                        ? currentOrder[item].ToString()
                        : "0";
                }
            });
        }

        UpdateConfirmButtonState();
    }

    private void UpdateConfirmButtonState()
    {
        int totalCost = GetTotalCost();

        // Update both total cost texts here so they stay in sync
        if (totalCostText != null)
            totalCostText.text = $"Total: ${totalCost}";
        if (totalCostTextFirstPage != null)
            totalCostTextFirstPage.text = $"Total: ${totalCost}";

        // Enable/disable confirm button based on currency
        confirmButton.interactable = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAfford(totalCost);
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
        List<DesignItem> finalOrder = GetFinalOrder();
        int totalCost = GetTotalCost();

        if (CurrencyManager.Instance != null && CurrencyManager.Instance.Spend(totalCost))
        {
            Debug.Log($"Spent ${totalCost} on design order with {finalOrder.Count} items.");
        }
        else
        {
            Debug.LogWarning("Not enough money!");
            return;
        }

        currentOrder.Clear();
        CloseAll();
        UpdateItemCountText();
        UpdateWalletUI();
        UpdateConfirmButtonState();

        // Reset first-page UI
        foreach (Transform child in contentParent)
        {
            var ui = child.GetComponent<ItemEntryUI>();
            if (ui != null)
                ui.quantityInputField.text = "0";
        }
    }

    public void CloseAll()
    {
        gameObject.SetActive(false);
        reviewPanel.SetActive(false);
    }

    private int GetTotalCost()
    {
        return currentOrder.Sum(pair => pair.Key.price * pair.Value);
    }

    public List<DesignItem> GetFinalOrder()
    {
        List<DesignItem> finalList = new List<DesignItem>();
        foreach (var pair in currentOrder)
        {
            for (int i = 0; i < pair.Value; i++)
                finalList.Add(pair.Key);
        }
        return finalList;
    }
}
