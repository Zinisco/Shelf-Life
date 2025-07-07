using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;

public class ComputerUI : MonoBehaviour
{
    public GameObject uiRoot;

    private void Start()
    {
        uiRoot.SetActive(false);
    }

    private void Update()
    {
        if (uiRoot.activeSelf && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleUI(false);
        }
    }

    public void ToggleUI(bool show)
    {
        Debug.Log("Showing UI");
        StopAllCoroutines(); // Stop any previous animations

        if (show)
        {
            uiRoot.SetActive(true); // Enable first so we can animate
            StartCoroutine(AnimateScale(uiRoot.transform, Vector3.zero, Vector3.one, 0.25f));
        }
        else
        {
            StartCoroutine(AnimateScale(uiRoot.transform, uiRoot.transform.localScale, Vector3.zero, 0.2f, () =>
            {
                uiRoot.SetActive(false); // Hide after shrinking
            }));
        }

        // Lock/unlock player movement
        var player = FindObjectOfType<PlayerMovement>();
        if (player != null)
            player.IsLocked = show;

        // Show/hide cursor
        Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = show;
    }

    private IEnumerator AnimateScale(Transform target, Vector3 from, Vector3 to, float duration, System.Action onComplete = null)
    {
        float elapsed = 0f;
        target.localScale = from;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // SmoothStep-like easing
            target.localScale = Vector3.Lerp(from, to, t);

            elapsed += Time.unscaledDeltaTime; // unaffected by timescale
            yield return null;
        }

        target.localScale = to;
        onComplete?.Invoke();
    }

}
