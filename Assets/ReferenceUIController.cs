using TMPro;
using UnityEngine;

public class ReferenceUIController : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text genreText;
    public TMP_Text priceText;
    public TMP_Text summaryText;

    private Transform target;
    private Vector3 offset = new Vector3(0, 0.5f, 0); // offset above the book

    void Awake()
    {
        Debug.Log("[ReferenceUIController] Awake called!");
    }

    void Start()
    {
        Debug.Log("[ReferenceUIController] Start called!");
    }

    public void Show(BookDefinition def, Transform followTarget)
    {
        Debug.Log("[ReferenceUIController] Show() called!");

        if (!gameObject.activeSelf)
        {
            Debug.LogWarning("GameObject was inactive — activating.");
            gameObject.SetActive(true);
        }

        titleText.text = def.title;
        genreText.text = $"Genre: {def.genre}";
        priceText.text = $"Price: ${def.price}";
        summaryText.text = def.summary;

        target = followTarget;
    }




    public void Hide()
    {
        target = null;
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }
}
