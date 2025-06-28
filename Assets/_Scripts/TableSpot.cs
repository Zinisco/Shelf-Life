using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class TableSpot : MonoBehaviour
{
    [SerializeField] private int maxStackHeight = 4;
    [SerializeField] private float stackHeightOffset = 0.12f;

    [SerializeField] public Transform StackAnchor;


    private List<GameObject> stackedBooks = new List<GameObject>();

    public string objectID;

    private void Awake()
    {
        // 1) ensure every spot actually has an Anchor
        if (StackAnchor == null)
        {
            var found = transform.Find("Anchor");
            if (found != null)
                StackAnchor = found;
            else
            {
                var go = new GameObject("Anchor");
                go.transform.SetParent(transform, false);
                StackAnchor = go.transform;
            }
        }

        // 2) if you forgot to assign an ID in the inspector, give it one now
        if (string.IsNullOrEmpty(objectID))
            objectID = GUID.Generate().ToString();
    }


    public TableSaveData GetSaveData()
    {
        TableSaveData data = new TableSaveData();
        data.tableID = objectID;

        foreach (GameObject book in stackedBooks)
        {
            BookInfo info = book.GetComponent<BookInfo>();
            if (info != null)
            {
                data.stackedBooks.Add(new BookSaveData
                {
                    bookID = info.bookID,
                    position = book.transform.position,
                    rotation = book.transform.rotation
                });
            }
        }
        return data;
    }

    public void LoadBooksFromData(TableSaveData data, BookDatabase bookDatabase)
    {
        if (data == null || bookDatabase == null) return;

        // 1) clear out the old children
        foreach (Transform c in StackAnchor)
            Destroy(c.gameObject);
        stackedBooks.Clear();

        // 2) re-spawn each book *as a true child* of the Anchor
        for (int i = 0; i < data.stackedBooks.Count; i++)
        {
            var entry = data.stackedBooks[i];
            GameObject prefab = bookDatabase.GetBookPrefabByID(entry.bookID);
            if (prefab == null)
            {
                Debug.LogWarning($"[TableSpot:{objectID}] no prefab for '{entry.bookID}'");
                continue;
            }

            GameObject book = Instantiate(prefab);
            BookInfo info = book.GetComponent<BookInfo>();
            if (info != null)
            {
                info.ObjectID = this.objectID;
                info.SpotIndex = -1; // stacked books aren't on shelves
            }

            // parent under the anchor, *without* preserving world coords
            book.transform.SetParent(StackAnchor, false);

            // slot it at the correct *local* height
            book.transform.localPosition = new Vector3(0f, i * stackHeightOffset, 0f);

            // orient so cover is up, spine to player’s left
            Vector3 dir = Camera.main.transform.forward;
            dir.y = 0f; dir.Normalize();
            float snapped = Mathf.Round(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg / 90f) * 90f;
            Quaternion baseRot = Quaternion.Euler(-90f, 0f, 0f);
            Quaternion faceY = Quaternion.Euler(0f, snapped, 0f);
            Quaternion flipZ = Quaternion.Euler(0f, 0f, 180f);
            book.transform.localRotation = faceY * baseRot * flipZ;

            // d) Disable physics
            FreezeBookPhysics(book);

            stackedBooks.Add(book);

            // Add this to mark the book as stacked but NOT shelved
            if (info != null)
            {
                info.bookID = entry.bookID;
                info.ObjectID = ""; // Not on a shelf
                info.SpotIndex = -1;
            }
        }
    }

    public bool IsEmpty => stackedBooks.Count == 0;

    public bool CanStack(BookInfo newBookInfo)
    {
        if (IsEmpty) return true;

        BookInfo topBookInfo = stackedBooks[0].GetComponent<BookInfo>();
        return newBookInfo.bookID == topBookInfo.bookID && stackedBooks.Count < maxStackHeight;
    }

    public void StackBook(GameObject book)
    {
        if (book == null || StackAnchor == null) return;

        // Just in case it's already in another stack, detach first
        TableSpot oldSpot = book.GetComponentInParent<TableSpot>();
        if (oldSpot != null && oldSpot != this)
        {
            oldSpot.RemoveBook(book);
        }

        // Match ghost logic
        Vector3 stackPosition = GetStackedPosition();
        Vector3 playerDir = Camera.main.transform.forward;
        playerDir.y = 0f;
        playerDir.Normalize();

        // Snap to nearest 90°
        float angle = Mathf.Atan2(playerDir.x, playerDir.z) * Mathf.Rad2Deg;
        float snappedAngle = Mathf.Round(angle / 90f) * 90f;

        // Base rotation: +Z (cover)  +Y (up), +X (spine) -Z (left), +Y (top)  -X (bottom)
        Quaternion baseRotation = Quaternion.Euler(-90f, 0f, 0f);
        Quaternion facingRotation = Quaternion.Euler(0f, snappedAngle, 0f);

        // Final rotation including Z flip to correct bottom-to-player direction
        Quaternion finalRotation = facingRotation * baseRotation * Quaternion.Euler(0f, 0f, 180f);

        book.transform.SetParent(StackAnchor, false); // local space preserved
        book.transform.localPosition = new Vector3(0f, stackedBooks.Count * stackHeightOffset, 0f);
        book.transform.localRotation = finalRotation;

        FreezeBookPhysics(book);

        stackedBooks.Add(book);
        Debug.Log($"Book stacked. New count: {stackedBooks.Count}");
    }


    public GameObject PeekTopBook()
    {
        if (stackedBooks.Count == 0) return null;
        return stackedBooks[stackedBooks.Count - 1];
    }

    public GameObject PopTopBook()
    {
        if (stackedBooks.Count == 0) return null;

        GameObject topBook = stackedBooks[stackedBooks.Count - 1];
        stackedBooks.RemoveAt(stackedBooks.Count - 1);
        topBook.transform.SetParent(null);
        return topBook;
    }

    public int GetStackCount()
    {
        return stackedBooks.Count;
    }

    public BookInfo GetStackBookInfo()
    {
        if (IsEmpty) return null;
        return stackedBooks[0].GetComponent<BookInfo>();
    }

    public Vector3 GetNextStackPosition()
    {
        Vector3 basePos = transform.position;
        Vector3 offset = Vector3.up * (stackedBooks.Count * stackHeightOffset);
        return basePos + offset;
    }

    public void RemoveBook(GameObject book)
    {
        if (stackedBooks.Contains(book))
        {
            stackedBooks.Remove(book);
        }
    }

    public List<GameObject> GetStackedBooks()
    {
        return stackedBooks;
    }

    public void ForceAddBookToStack(GameObject book)
    {
        if (book == null || StackAnchor == null) return;

        // a) parent under Anchor in local-space
        book.transform.SetParent(StackAnchor, false);

        int idx = stackedBooks.Count;

        // b) slot it exactly in the next position
        book.transform.localPosition = new Vector3(0f, idx * stackHeightOffset, 0f);

        // c) re-use the same cover-up/spine-left rotation
        Vector3 dir = Camera.main.transform.forward;
        dir.y = 0f; dir.Normalize();
        float snapped = Mathf.Round(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg / 90f) * 90f;
        Quaternion baseRot = Quaternion.Euler(-90f, 0f, 0f);
        Quaternion faceY = Quaternion.Euler(0f, snapped, 0f);
        Quaternion flipZ = Quaternion.Euler(0f, 0f, 180f);
        book.transform.localRotation = faceY * baseRot * flipZ;

        FreezeBookPhysics(book);

        stackedBooks.Add(book);
    }


    public void InitializeFromVisual(GameObject visual)
    {
        // Look for the Anchor under visual
        Transform foundAnchor = visual.transform.Find("Anchor");
        if (foundAnchor != null)
        {
            StackAnchor = foundAnchor;
        }
        else
        {
            Debug.LogError($"ShelfSpot '{name}' could not find 'Anchor' under visual!");
        }
    }
    void OnDrawGizmos()
    {
        if (StackAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(StackAnchor.position, 0.02f);
            Gizmos.DrawLine(StackAnchor.position, StackAnchor.position + StackAnchor.up * 0.2f);
        }
    }

    public Vector3 GetStackedPosition()
    {
        return StackAnchor.position + StackAnchor.up * (stackedBooks.Count * stackHeightOffset);
    }

    public void ClearStack()
    {
        foreach (GameObject book in stackedBooks)
        {
            Destroy(book);
        }
        stackedBooks.Clear();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(objectID))
        {
            objectID = GUID.Generate().ToString();
            EditorUtility.SetDirty(this);
            //Debug.Log("Generated persistent objectID: " + objectID);
        }
    }

    private void FreezeBookPhysics(GameObject book)
    {
        var rb = book.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
    }

    public Transform GetStackAnchor() => StackAnchor;
}
