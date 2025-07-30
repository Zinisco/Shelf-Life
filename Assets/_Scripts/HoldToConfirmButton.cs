using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class HoldToConfirmButton : MonoBehaviour
{
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private Image fillImage;
    [SerializeField] private UnityEvent onHoldComplete;

    private float holdTimer;
    private bool isHolding;

    private void Update()
    {
        if (isHolding)
        {
            holdTimer += Time.deltaTime;
            if (fillImage)
                fillImage.fillAmount = holdTimer / holdDuration;

            if (holdTimer >= holdDuration)
            {
                isHolding = false;
                onHoldComplete?.Invoke();
            }
        }
    }

    public void OnPointerDown()
    {
        holdTimer = 0f;
        isHolding = true;
    }

    public void OnPointerUp()
    {
        isHolding = false;
        if (fillImage)
            fillImage.fillAmount = 0f;
    }
}
