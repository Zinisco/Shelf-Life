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

        // Match by title or book ID
        return baseInfo.title == incomingInfo.title;
    }

}
