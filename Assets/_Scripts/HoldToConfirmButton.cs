using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Selectable))]
public class HoldToConfirmButton : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ISubmitHandler
{
    [SerializeField] private float holdDuration = 2.0f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private Image fillImage;          // Filled image (radial or horizontal)

    // <-- This is what MainMenuManager expects:
    public UnityEvent onCompleted = new UnityEvent();

    private float timer;
    private bool isHolding;
    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        ResetVisual();
    }

    private void OnEnable() => ResetVisual();
    private void OnDisable() => CancelHold();

    private void Update()
    {
        if (!isHolding) return;
        if (selectable && !selectable.interactable) { CancelHold(); return; }

        timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float pct = Mathf.Clamp01(timer / Mathf.Max(0.01f, holdDuration));
        if (fillImage) fillImage.fillAmount = pct;

        if (pct >= 1f)
        {
            isHolding = false;
            onCompleted?.Invoke();
            ResetVisual(); // ready for next use
        }
    }

    // Mouse / touch
    public void OnPointerDown(PointerEventData _) { BeginHold(); }
    public void OnPointerUp(PointerEventData _) { CancelHold(); }
    public void OnPointerExit(PointerEventData _) { CancelHold(); }

    // Keyboard / gamepad “Submit” (e.g., Enter / A)
    public void OnSubmit(BaseEventData _) { BeginHold(); }

    private void BeginHold()
    {
        if (selectable && !selectable.interactable) return;
        timer = 0f;
        isHolding = true;
        if (fillImage) fillImage.fillAmount = 0f;
    }

    private void CancelHold()
    {
        isHolding = false;
        ResetVisual();
    }

    private void ResetVisual()
    {
        timer = 0f;
        if (fillImage) fillImage.fillAmount = 0f;
    }

    // <-- This is what MainMenuManager calls:
    public void ResetHold() => CancelHold();
}
