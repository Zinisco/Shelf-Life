using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SellPanelController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pricingEntryPrefab;
    public Transform contentParent;

    [Header("Systems")]
    public BookDatabase bookDatabase;

    private void Start()
    {
        PopulateSellPanel();
    }

    private void PopulateSellPanel()
    {
        if (bookDatabase == null) return;

        foreach (var book in bookDatabase.allBooks)
        {
            GameObject entry = Instantiate(pricingEntryPrefab, contentParent);

            TMP_Text titleText = null;
            TMP_Text costText = null;
            TMP_Text profitText = null;
            TMP_InputField priceInput = null;
            Image bookImage = null;

            foreach (var text in entry.GetComponentsInChildren<TMP_Text>(true))
            {
                switch (text.name)
                {
                    case "TitleText": titleText = text; break;
                    case "CostText": costText = text; break;
                    case "ProfitText": profitText = text; break;
                }
            }

            foreach (var input in entry.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (input.name == "PriceInputField")
                    priceInput = input;
            }

            foreach (var image in entry.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "BookImage")
                    bookImage = image;
            }


            titleText.text = book.title;
            costText.text = $"Cost: ${book.cost}";
            priceInput.text = book.price.ToString();
            bookImage.color = book.color;

            priceInput.onValueChanged.AddListener((value) => {
                int newPrice;
                if (int.TryParse(value, out newPrice))
                {
                    book.price = newPrice;
                    int profit = newPrice - book.cost;
                    profitText.text = $"Profit: ${profit}";
                }
                else
                {
                    profitText.text = "Profit: $0";
                }
            });

            int initialProfit = book.price - book.cost;
            profitText.text = $"Profit: ${initialProfit}";
        }
    }
}
