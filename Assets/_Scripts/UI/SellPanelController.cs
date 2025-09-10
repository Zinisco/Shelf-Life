using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

            var titleText = FindByNameInChildren<TMP_Text>(entry.transform, "TitleText");
            var costText = FindByNameInChildren<TMP_Text>(entry.transform, "CostText");
            var profitText = FindByNameInChildren<TMP_Text>(entry.transform, "ProfitText");
            var priceInput = FindByNameInChildren<TMP_InputField>(entry.transform, "PriceInputField");

            // Image (deep find) + sprite hookup
            SetBookImage(entry.transform, book.thumbnail, book.color);

            if (titleText) titleText.text = book.title;
            if (costText) costText.text = $"Cost: ${book.cost}";
            if (priceInput) priceInput.text = book.price.ToString();

            // Wire price change → profit display + save back to definition
            if (priceInput && profitText)
            {
                priceInput.onValueChanged.AddListener(value =>
                {
                    if (int.TryParse(value, out int newPrice))
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

                profitText.text = $"Profit: ${book.price - book.cost}";
            }
        }
    }


    private static T FindByNameInChildren<T>(Transform root, string name) where T : Component
    {
        return root.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name);
    }

    private static void SetBookImage(Transform root, Sprite sprite, Color fallbackTint)
    {
        var img = FindByNameInChildren<Image>(root, "BookImage");
        if (!img) return;

        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
            img.color = Color.white;   // no tint over the art
            img.enabled = true;
        }
        else
        {
            // No thumbnail yet? Show colored placeholder so layout still looks right.
            img.sprite = null;
            img.color = fallbackTint;
            img.enabled = true;
        }
    }
}
