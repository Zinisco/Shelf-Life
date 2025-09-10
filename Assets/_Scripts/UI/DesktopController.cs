using UnityEngine;
using UnityEngine.UI;

public class DesktopMenuController : MonoBehaviour
{
    public GameObject booksPanel;
    public GameObject designPanel;
    public GameObject managePanel;
    public GameObject sellPanel;

    public GameObject sellButton;
    public GameObject manageButton;

    public BuyPanelController buyPanelController;

    private void Awake()
    {
        if (GameModeConfig.CurrentMode == GameMode.Zen)
        {
            sellButton.gameObject.SetActive(false);
            manageButton.gameObject.SetActive(false);
        }
        else
        {
            sellButton.gameObject.SetActive(true);
            manageButton.gameObject.SetActive(true);
        }
    }

    public void OpenBuy()
    {
        //Debug.Log("Buy button pressed!");
        ShowOnly(booksPanel);
    }

    public void OpenDesign()
    {
        ShowOnly(designPanel);
    }

    public void OpenManage()
    {
        ShowOnly(managePanel);
    }

    public void OpenSell()
    {
        ShowOnly(sellPanel);
    }

    private void ShowOnly(GameObject panelToShow)
    {
        booksPanel.SetActive(false);
        designPanel.SetActive(false);
        managePanel.SetActive(false);
        sellPanel.SetActive(false);

        if (panelToShow != null)
            panelToShow.SetActive(true);
    }

    public void CloseAll()
    {
        buyPanelController.ResetOrder();
        ShowOnly(null); // Hides all sub-panels
    }
}
