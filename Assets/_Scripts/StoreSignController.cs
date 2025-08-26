using UnityEngine;
using UnityEngine.InputSystem;

public class StoreSignController : MonoBehaviour
{
    [SerializeField] private Transform signVisual;
    [SerializeField] private DayNightCycle dayNightCycle;
    [SerializeField] private float rotationDuration = 0.5f;

    private bool hasDayStarted = false;
    private bool storeIsOpen = false;
    private bool isRotating = false;
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
                if (hit.collider.gameObject == gameObject && !isRotating)
                {
                    ToggleStore();
                }
            }
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ResetStoreForNextDay();
        }
    }

    void ToggleStore()
    {
        if (!hasDayStarted)
        {
            // First click to open store
            hasDayStarted = true;
            storeIsOpen = true;
            dayNightCycle.StartTime();
            StartCoroutine(SmoothRotateSign(180f));
            Debug.Log("Store opened!");
        }
        else if (storeIsOpen)
        {
            storeIsOpen = false;
            StartCoroutine(SmoothRotateSign(180f));
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
            StartCoroutine(SmoothRotateSign(180f));
            Debug.Log("Store reopened!");
        }
        else
        {
            Debug.Log("Store is already closed for the day.");
        }
    }

    public void ResetStoreForNextDay()
    {
        // Only reset if day was started AND completed
        if (hasDayStarted && !storeIsOpen && wasDayCompleted)
        {
            hasDayStarted = false;
            wasDayCompleted = false;
            dayNightCycle.ResetDay();
            Debug.Log("System reset. Ready for next day.");
        }
        else
        {
            Debug.Log("Cannot reset: Day not completed.");
        }
    }

    public void ForceCompleteDay()
    {
        wasDayCompleted = true;
    }


    System.Collections.IEnumerator SmoothRotateSign(float angle)
    {
        isRotating = true;

        Quaternion startRot = signVisual.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(0f, angle, 0f);

        float elapsed = 0f;
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rotationDuration);
            signVisual.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        signVisual.rotation = endRot;
        isRotating = false;
    }

    public bool HasDayEnded => !storeIsOpen && wasDayCompleted;
    public bool IsStoreOpen() => storeIsOpen;
    public bool HasDayStarted => hasDayStarted;

}
