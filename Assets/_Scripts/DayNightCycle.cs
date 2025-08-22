using TMPro;
using UnityEngine;

public class DayNightCycle : MonoBehaviour
{

    [Header("Day Counter UI")]
    [SerializeField] private TMP_Text dayDisplay;
    private int dayCounter = 1;


    [Header("Time Settings")]
    [Range(0f, 24f)]
    public float currentTime = 9f;
    public float dayDurationInMinutes = 1f;
    private float timeSpeed;
    private bool timeRunning = false;

    [Header("UI")]
    [SerializeField] private TMP_Text timeDisplay;
    [SerializeField] private TMP_Text computerTimeDisplay;

    [Header("Sun Settings")]
    public Light sunLight;
    public Gradient lightColorOverTime;
    public AnimationCurve sunIntensityCurve;

    [Header("Rotation Settings")]
    public Transform sunPivot;
    public Vector3 rotationAxis = Vector3.right;

    public bool IsTimeRunning => timeRunning; // External read access
    public float CurrentHour => Mathf.Floor(currentTime);

    void Start()
    {
        if (GameModeConfig.CurrentMode == GameMode.Zen)
        {
            Debug.Log("[ZenMode] DayNightCycle + UI disabled.");

            // Disable associated UI elements
            if (dayDisplay != null) dayDisplay.gameObject.SetActive(false);
            if (timeDisplay != null) timeDisplay.gameObject.SetActive(false);
            if (computerTimeDisplay != null) computerTimeDisplay.gameObject.SetActive(false);

            // Disable this component's behavior
            enabled = false;
            return;
        }

        timeSpeed = 24f / (dayDurationInMinutes * 60f);

        if (dayDisplay != null)
            dayDisplay.text = $"Day {dayCounter:00}";
    }



    void Update()
    {
        if (!timeRunning) return;

        currentTime += Time.deltaTime * timeSpeed;
        if (currentTime >= 24f) currentTime -= 24f;

        string currentFormattedTime = GetFormattedTime();
        if (timeDisplay != null) timeDisplay.text = currentFormattedTime;
        if (computerTimeDisplay != null) computerTimeDisplay.text = currentFormattedTime;

        if (GameModeConfig.CurrentMode == GameMode.Standard)
        {
            UpdateSun();
        }

        // Automatically stop time at 9PM
        if (currentTime >= 21f)
        {
            StopTime();
            Debug.Log("Store hours ended. Please close up.");
        }
    }

    void UpdateSun()
    {
        float t = currentTime / 24f;
        float sunAngle = t * 360f - 150f;
        sunPivot.localRotation = Quaternion.Euler(new Vector3(sunAngle, 0f, 0f));

        if (sunLight != null)
        {
            sunLight.color = lightColorOverTime.Evaluate(t);
            sunLight.intensity = sunIntensityCurve.Evaluate(t);
        }
    }

    public void ResetDay()
    {
        currentTime = 9f;
        StopTime();
        UpdateSun();

        // Increment day
        dayCounter++;

        // Update time display immediately
        string currentFormattedTime = GetFormattedTime();
        if (timeDisplay != null) timeDisplay.text = currentFormattedTime;
        if (computerTimeDisplay != null) computerTimeDisplay.text = currentFormattedTime;

        // Update day display
        if (dayDisplay != null) dayDisplay.text = $"Day {dayCounter:00}";
    }

    public int GetCurrentDay() => dayCounter;

    public void SetDay(int day)
    {
        dayCounter = Mathf.Max(1, day);
        //Debug.Log($"SetDay called with value: {dayCounter}");

        if (dayDisplay != null)
        {
            //Debug.Log("Updating dayDisplay text");
            dayDisplay.text = $"Day {dayCounter:00}";
        }
        else
        {
            Debug.LogWarning("dayDisplay is null when setting day!");
        }
    }

    private string GetFormattedTime()
    {
        int hours = Mathf.FloorToInt(currentTime);
        int minutes = Mathf.FloorToInt((currentTime - hours) * 60f);
        string suffix = hours >= 12 ? "PM" : "AM";
        int displayHour = hours % 12;
        if (displayHour == 0) displayHour = 12;
        return $"{displayHour:00}:{minutes:00} {suffix}";
    }

    public void StartTime() => timeRunning = true;
    public void StopTime() => timeRunning = false;
}
