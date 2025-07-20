using System.Collections.Generic;
using UnityEngine;

public class BookStackManager : MonoBehaviour
{
    public bool CanStack(GameObject baseBook, GameObject incomingBook)
    {
        var baseInfo = baseBook.GetComponent<BookInfo>();
        var incomingInfo = incomingBook.GetComponent<BookInfo>();
        if (baseInfo == null || incomingInfo == null) return false;

        return baseInfo.bookID == incomingInfo.bookID || baseInfo.title == incomingInfo.title;
    }
}
