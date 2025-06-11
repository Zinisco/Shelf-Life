using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Collections;

public class BookSaveManager : MonoBehaviour
{
    [System.Serializable]
    public class BookSaveData
    {
        public string Genre;
        public string Title;
        public string SpineTitle;
        public Color CoverColor;
        public string ShelfID;
        public int SpotIndex;
        public Vector3 Position;     // Used if not shelved
        public Quaternion Rotation;
    }

    [System.Serializable]
    public class SaveDataWrapper
    {
        public List<BookSaveData> allBooks;
    }

    [SerializeField] private GameObject bookPrefab;


    private static BookSaveManager instance;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    public static void SaveBooks()
    {
        var allBookInfos = FindObjectsOfType<BookInfo>();
        List<BookSaveData> dataList = new List<BookSaveData>();

        foreach (var book in allBookInfos)
        {
            var data = new BookSaveData
            {
                Genre = book.Genre,
                Title = book.Title,
                SpineTitle = book.SpineTitle,
                CoverColor = book.CoverColor,
                ShelfID = book.ShelfID,
                SpotIndex = book.SpotIndex
            };

            if (string.IsNullOrEmpty(book.ShelfID) || book.SpotIndex < 0)
            {
                // Save world position and rotation if not shelved
                data.Position = book.transform.position;
                data.Rotation = book.transform.rotation;
            }


            dataList.Add(data);
        }

        string json = JsonUtility.ToJson(new SaveDataWrapper { allBooks = dataList }, true);
        File.WriteAllText(Application.persistentDataPath + "/booksave.json", json);
        Debug.Log("Books saved to: " + Application.persistentDataPath);
    }

    public static void LoadBooks()
    {
        string path = Application.persistentDataPath + "/booksave.json";
        if (!File.Exists(path))
        {
            Debug.LogWarning("No save file found.");
            return;
        }

        string json = File.ReadAllText(path);
        var wrapper = JsonUtility.FromJson<SaveDataWrapper>(json);

        // Clean up existing books
        foreach (var oldBook in FindObjectsOfType<BookInfo>())
        {
            Destroy(oldBook.gameObject);
        }

        foreach (var data in wrapper.allBooks)
        {
            GameObject newBook = Instantiate(Resources.Load<GameObject>("Book"));
            RandomBookGenerator generator = newBook.GetComponent<RandomBookGenerator>();
            BookInfo info = newBook.GetComponent<BookInfo>();

            if (generator != null)
                generator.ApplyBookInfo(data);

            if (info != null)
            {
                info.ShelfID = data.ShelfID;
                info.SpotIndex = data.SpotIndex;
            }

            if (string.IsNullOrEmpty(data.ShelfID) || data.SpotIndex < 0)
            {
                // Not shelved — position manually
                newBook.transform.position = data.Position;
                newBook.transform.rotation = data.Rotation;
            }
        }

        instance.StartCoroutine(instance.ReconnectShelfSpotsAfterDelay());
    }

    private IEnumerator ReconnectShelfSpotsAfterDelay()
    {
        yield return new WaitForSeconds(0.25f); // Give shelves time to initialize

        Dictionary<string, Bookshelf> shelvesByID = new Dictionary<string, Bookshelf>();
        foreach (var shelf in FindObjectsOfType<Bookshelf>())
        {
            shelvesByID[shelf.GetID()] = shelf;
        }

        foreach (var book in FindObjectsOfType<BookInfo>())
        {
            if (string.IsNullOrEmpty(book.ShelfID) || book.SpotIndex < 0) continue;

            if (shelvesByID.TryGetValue(book.ShelfID, out Bookshelf shelf))
            {
                List<ShelfSpot> spots = shelf.GetShelfSpots();
                if (book.SpotIndex >= 0 && book.SpotIndex < spots.Count)
                {
                    ShelfSpot targetSpot = spots[book.SpotIndex];

                    if (targetSpot.IsOccupied())
                    {
                        Debug.LogWarning($"Spot {book.SpotIndex} on shelf {book.ShelfID} is already occupied.");
                        continue;
                    }

                    Transform anchor = targetSpot.GetBookAnchor();
                    if (anchor == null)
                    {
                        Debug.LogError($"Missing anchor on ShelfSpot '{targetSpot.name}'");
                        continue;
                    }

                    // Parent the book to the anchor and reset its transform
                    book.transform.SetParent(anchor);
                    book.transform.localPosition = Vector3.zero;
                    book.transform.localRotation = Quaternion.Euler(0, 90, 0);

                    // Disable physics interaction
                    Rigidbody rb = book.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = true;
                    }

                    // Track spot data
                    book.SetShelfSpot(targetSpot, book.ShelfID, book.SpotIndex);
                    targetSpot.SetOccupied(true, book.gameObject);
                }
            }
        }
    }

}
