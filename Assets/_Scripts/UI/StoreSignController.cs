using UnityEngine;
using UnityEngine.InputSystem;

public class StoreSignController : MonoBehaviour
{
    [SerializeField] private DayNightCycle dayNightCycle;

    [SerializeField] private Renderer signRenderer1; // e.g., neon tube
    [SerializeField] private Renderer signRenderer2; // e.g., glowing text

    [SerializeField] private Material closedMaterial; // e.g. black/dark
    [SerializeField] private Material openMaterial;   // e.g. glowing red

    private bool hasDayStarted = false;
    private bool storeIsOpen = false;
    private bool wasDayCompleted = false;

    void Start()
    {
        if (GameModeConfig.CurrentMode == GameMode.Zen)
        {
            Debug.Log("[ZenMode] StoreSignController disabled.");
            gameObject.SetActive(false); // Disable the whole sign
        }
    }


    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                if (hit.collider.gameObject == this.gameObject)
                {
                    ToggleStore();
                }
            }
        }
    }

    void ToggleStore()
    {
        if (!hasDayStarted)
        {
            hasDayStarted = true;
            storeIsOpen = true;
            dayNightCycle.StartTime();
            SetSignMaterial(openMaterial);
            Debug.Log("Store opened!");
        }
        else if (storeIsOpen)
        {
            storeIsOpen = false;
            SetSignMaterial(closedMaterial);
            Debug.Log("Store closed to customers.");

            if (dayNightCycle.CurrentHour >= 21f)
            {
                wasDayCompleted = true;

                var endPrompt = FindObjectOfType<EndOfDaySummaryController>();
                if (endPrompt != null)
                    endPrompt.gameObject.SetActive(true);
            }
        }
        else if (!storeIsOpen && dayNightCycle.CurrentHour < 21f)
        {
            storeIsOpen = true;
            SetSignMaterial(openMaterial);
            Debug.Log("Store reopened!");
        }
        else
        {
            Debug.Log("Store is already closed for the day.");
        }
    }

    public void ResetForNewDay()
    {
        storeIsOpen = false;
        wasDayCompleted = false;
        hasDayStarted = false;
    }

    public void ForceCompleteDay()
    {
        wasDayCompleted = true;
    }


    void SetSignMaterial(Material newMaterial)
    {
        if (newMaterial == null) return;

        if (signRenderer1 != null)
            signRenderer1.material = newMaterial;

        if (signRenderer2 != null)
            signRenderer2.material = newMaterial;
    }



    public bool HasDayEnded => !storeIsOpen && wasDayCompleted;
    public bool IsStoreOpen() => storeIsOpen;
    public bool HasDayStarted => hasDayStarted;

}
