using System.Collections.Generic;
using UnityEngine;

public class BookStackManager : MonoBehaviour
{
    public bool CanStack(GameObject baseBook, GameObject incomingBook)
    {
        if (baseBook == null || incomingBook == null) return false;

        BookInfo baseInfo = baseBook.GetComponent<BookInfo>();
        BookInfo incomingInfo = incomingBook.GetComponent<BookInfo>();

        if (baseInfo == null || incomingInfo == null) return false;

        // Prevent stacking if baseBook is on a BookDisplay
        if (baseBook.transform.parent != null && baseBook.transform.parent.CompareTag("BookDisplay"))
            return false;

        // Match by title
        return string.Equals(baseInfo.title?.Trim(),
                             incomingInfo.title?.Trim(),
                             System.StringComparison.OrdinalIgnoreCase);
    }


}
