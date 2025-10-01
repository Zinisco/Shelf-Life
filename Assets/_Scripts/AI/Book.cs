using UnityEngine;

public class Book : MonoBehaviour
{
    public string title;
    public bool isTaken = false;

    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void PickUp(Transform handTransform)
    {
        if (isTaken) return;

        isTaken = true;

        var info = GetComponent<BookInfo>();
        if (info != null && info.currentStackRoot != null)
        {
            // remove cleanly without collapsing the rest
            info.currentStackRoot.RemoveBook(gameObject, rebuild: true);
            info.currentStackRoot = null;
        }

        transform.SetParent(handTransform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }


    public void PutBack(Vector3 position, Quaternion rotation)
    {
        isTaken = false;

        var info = GetComponent<BookInfo>();
        if (info != null)
        {
            info.RestoreOrigin();
        }
        else
        {
            // fallback: free placement
            transform.SetParent(null);
            transform.position = position;
            transform.rotation = rotation;
        }

        transform.localScale = originalScale; // reset!
    }

}
