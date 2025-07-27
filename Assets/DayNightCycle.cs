using TMPro;
using UnityEngine;
using TMPro;

public class DayNightCycle : MonoBehaviour
{
    [Header("Time Settings")]
    [Range(0f, 24f)]
    public float currentTime = 9f; // Start at 9 AM
    public float dayDurationInMinutes = 1f; // How long a full day lasts (in real-time minutes)
   
    [Header("UI")]
    [SerializeField] private TMP_Text timeDisplay;
    [SerializeField] private TMP_Text computerTimeDisplay;

    [Header("Sun Settings")]
    public Light sunLight;
    public Gradient lightColorOverTime;
    public AnimationCurve sunIntensityCurve;

    [Header("Rotation Settings")]
    public Transform sunPivot; // Rotate the directional light around this
    public Vector3 rotationAxis = Vector3.right;

    private float timeSpeed;

    void Start()
    {
        timeSpeed = 24f / (dayDurationInMinutes * 60f);
    }

    void Update()
    {
        currentTime += Time.deltaTime * timeSpeed;
        if (currentTime >= 24f) currentTime -= 24f;

        string currentFormattedTime = GetFormattedTime();

        if (timeDisplay != null)
            timeDisplay.text = currentFormattedTime;

        if (computerTimeDisplay != null)
            computerTimeDisplay.text = currentFormattedTime;



        UpdateSun();
    }

    void UpdateSun()
    {
        float t = currentTime / 24f;

        // Rotate sun based on time
        float sunAngle = t * 360f - 150f;
        sunPivot.localRotation = Quaternion.Euler(new Vector3(sunAngle, 0f, 0f));


        // Update light color & intensity
        if (sunLight != null)
        {
            sunLight.color = lightColorOverTime.Evaluate(t);
            sunLight.intensity = sunIntensityCurve.Evaluate(t);
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


}
