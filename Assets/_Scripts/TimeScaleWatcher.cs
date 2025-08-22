using UnityEngine;

public class TimeScaleWatcher : MonoBehaviour
{
    private float lastScale = -1f;

    void Update()
    {
        if (Time.timeScale != lastScale)
        {
            Debug.LogWarning($"[TimeScaleWatcher] Time.timeScale changed to {Time.timeScale}");
            lastScale = Time.timeScale;
        }
    }
}
