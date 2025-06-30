using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BookSaveManager : MonoBehaviour
{

    [System.Serializable]
    public class SaveDataWrapper
    {
        public int saveVersion = 1;
        public List<BookSaveData> allBooks = new();
        public List<BookshelfSaveData> allShelves = new();
        public List<TableSaveData> allTables = new();
        public List<TableSpotSaveData> allTableSpots = new();
    }

    [SerializeField] private BookDatabase bookDatabase;
    private static BookSaveManager _I;

    private void Awake()
    {
        if (_I == null) _I = this;
        else { Destroy(gameObject); return; }

        if (bookDatabase == null)
            bookDatabase = Resources.Load<BookDatabase>("BookDatabase");
    }

    public void SaveAll()
    {
        var w = new SaveDataWrapper();
        w.saveVersion = 1; // current format version

        // --- books ---
        foreach (var info in FindObjectsOfType<BookInfo>())
        {
            if (info.currentTableSpot != null)
                continue; // skip stacked books

            // only shelved books should set shelfID and spotIndex
            w.allBooks.Add(new BookSaveData
            {
                bookID = info.bookID,
                title = info.definition?.title ?? "",
                genre = info.definition?.genre ?? "",
                summary = info.definition?.summary ?? "",
                color = new float[] {
        info.definition.color.r,
        info.definition.color.g,
        info.definition.color.b
    },
                tags = new List<string>(info.tags),
                shelfID = info.ObjectID,
                spotIndex = info.SpotIndex,
                position = info.transform.position,
                rotation = info.transform.rotation,
            });
        }

        // --- shelves & tables ---
        foreach (var shelf in FindObjectsOfType<Bookshelf>())
            w.allShelves.Add(new BookshelfSaveData
            {
                ShelfID = shelf.GetID(),
                Position = shelf.transform.position,
                Rotation = shelf.transform.rotation
            });

        foreach (var table in FindObjectsOfType<BookTable>())
        {
            var data = new TableSaveData
            {
                tableID = table.GetID(),
                position = table.transform.position,
                rotation = table.transform.rotation,
                stackedBooks = new List<BookSaveData>()
            };

            var spots = table.GetTableSpots();
            foreach (var spot in spots)
            {
                spot.RefreshStack(); // rebuild internal list
                var spotData = spot.GetSaveData();

                foreach (var book in spotData.stackedBooks)
                {
                    book.spotIndex = spot.SpotIndex; // save correct spot index
                    data.stackedBooks.Add(book);
                }
            }

            w.allTables.Add(data);
            Debug.Log($"[SAVE] Table '{data.tableID}' saved with {data.stackedBooks.Count} books.");
        }


        foreach (var book in w.allBooks)
        {
            Debug.Log($"[SAVE] Book '{book.bookID}' — shelfID: {book.shelfID}, spotIndex: {book.spotIndex}");
        }

        File.WriteAllText(Path.Combine(Application.persistentDataPath, "booksave.json"),
                          JsonUtility.ToJson(w, true));
        Debug.Log("Saved!");
    }

    public void LoadAll()
    {
        var path = Path.Combine(Application.persistentDataPath, "booksave.json");
        if (!File.Exists(path)) { Debug.LogWarning("No save."); return; }

        var w = JsonUtility.FromJson<SaveDataWrapper>(File.ReadAllText(path));

        // handle versioning
        if (w.saveVersion < 1)
        {
            Debug.LogWarning($"[Load] Unsupported save version: {w.saveVersion}");
            return;
        }
        else if (w.saveVersion < 2)
        {
            // Upgrade logic here: Add missing fields, change defaults, etc.
            MigrateV1ToV2(w);
        }

        // Destroy old books, shelves, tables
        foreach (var b in FindObjectsOfType<BookInfo>()) Destroy(b.gameObject);
        foreach (var s in FindObjectsOfType<Bookshelf>()) Destroy(s.gameObject);
        foreach (var t in FindObjectsOfType<BookTable>()) Destroy(t.gameObject);

        // Recreate shelves
        foreach (var sd in w.allShelves)
        {
            var go = Instantiate(Resources.Load<GameObject>("Bookshelf"));
            go.transform.position = sd.Position;
            go.transform.rotation = sd.Rotation;
            go.GetComponent<Bookshelf>().SetID(sd.ShelfID);
        }

        // Recreate tables
        foreach (var td in w.allTables)
        {
            var go = Instantiate(Resources.Load<GameObject>("BookTable"));
            go.transform.position = td.position;
            go.transform.rotation = td.rotation;

            var table = go.GetComponent<BookTable>();
            table.SetID(td.tableID);

            var spots = table.GetTableSpots();

            // Group books by SpotIndex
            var booksBySpot = new Dictionary<int, List<BookSaveData>>();
            foreach (var book in td.stackedBooks)
            {
                if (!booksBySpot.ContainsKey(book.spotIndex))
                    booksBySpot[book.spotIndex] = new List<BookSaveData>();
                booksBySpot[book.spotIndex].Add(book);
            }

            foreach (var spot in spots)
            {
                if (booksBySpot.TryGetValue(spot.SpotIndex, out var stack))
                {
                    var tableData = new TableSaveData
                    {
                        tableID = td.tableID,
                        stackedBooks = stack
                    };

                    spot.LoadBooksFromData(tableData, bookDatabase);
                }
            }
        }

        // STEP 1: Collect all stacked books from table data
        var stackedBookHashes = new HashSet<string>();

        foreach (var table in w.allTables)
        {
            foreach (var stackedBook in table.stackedBooks)
            {
                string hash = GenerateBookHash(stackedBook);
                stackedBookHashes.Add(hash);
            }
        }

        // STEP 2: Filter out any books from allBooks that match stacked books
        int beforeCount = w.allBooks.Count;
        w.allBooks.RemoveAll(book =>
        {
            string hash = GenerateBookHash(book);
            return stackedBookHashes.Contains(hash);
        });
        int afterCount = w.allBooks.Count;
        Debug.Log($"[Cleaner] Removed {beforeCount - afterCount} duplicate stacked books from allBooks.");



        // Recreate books (stacked & shelved)
        foreach (var bd in w.allBooks)
        {
            var prefab = bookDatabase.GetBookPrefabByID(bd.bookID);
            if (prefab == null) continue;

            var go = Instantiate(prefab, bd.position, bd.rotation);
            var info = go.GetComponent<BookInfo>();
            if (info != null)
            {
                info.bookID = bd.bookID;
                info.ObjectID = bd.shelfID;
                info.SpotIndex = bd.spotIndex;

                // Rebuild definition
                BookDefinition def = ScriptableObject.CreateInstance<BookDefinition>();
                def.bookID = bd.bookID;
                def.title = bd.title;
                def.genre = bd.genre;
                def.summary = bd.summary;

                if (bd.color != null && bd.color.Length == 3)
                    def.color = new Color(bd.color[0], bd.color[1], bd.color[2]);

                info.ApplyDefinition(def);
                info.tags = new List<string>(bd.tags ?? new List<string>());
                info.UpdateVisuals();

                Debug.Log($"[LoadAll] Spawned shelved book: {def.title}, shelfID={bd.shelfID}, spot={bd.spotIndex}");
            }
        }


        StartCoroutine(ReconnectShelves());
    }

    private static string GenerateBookHash(BookSaveData book)
    {
        Vector3 pos = book.position;
        int spot = book.spotIndex;

        return $"{book.bookID}_{Mathf.RoundToInt(pos.x * 100f)}_{Mathf.RoundToInt(pos.y * 100f)}_{Mathf.RoundToInt(pos.z * 100f)}_{spot}";
    }

    private IEnumerator ReconnectShelves()
    {     
        yield return new WaitForSeconds(0.2f);

        var byID = new Dictionary<string, Bookshelf>();
        foreach (var s in FindObjectsOfType<Bookshelf>())
            byID[s.GetID()] = s;


        foreach (var info in FindObjectsOfType<BookInfo>())
        {
            // skip books that were just spawned onto tables (not shelved)
            if (info.transform.parent != null && info.transform.parent.GetComponent<TableSpot>() != null)
                continue;

            if (string.IsNullOrEmpty(info.ObjectID) || info.SpotIndex < 0)
            {
                Debug.LogWarning($"[Reconnect] Skipping book {info.name} — shelfID: {info.ObjectID}, spotIndex: {info.SpotIndex}");
                continue;
            }


            if (string.IsNullOrEmpty(info.ObjectID) || info.SpotIndex < 0)
            {
                Debug.LogWarning($"Book '{info.name}' missing shelfID or spot index.");
                continue;
            }

            if (!byID.TryGetValue(info.ObjectID, out var shelf))
            {
                Debug.LogError($"Shelf '{info.ObjectID}' not found for book '{info.name}'.");
                continue;
            }

            var spots = shelf.GetShelfSpots();
            if (info.SpotIndex >= spots.Count)
            {
                Debug.LogError($"Invalid spot index '{info.SpotIndex}' for shelf '{shelf.GetID()}'.");
                continue;
            }

            var target = spots[info.SpotIndex];
            var anchor = target.GetBookAnchor();
            if (anchor == null)
            {
                Debug.LogError($"Missing anchor for spot '{target.name}'.");
                continue;
            }

            info.transform.SetParent(anchor, false);
            info.transform.localPosition = Vector3.zero;
            info.transform.localRotation = Quaternion.Euler(0, 90, 0);

            var rb = info.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
            
            info.SetShelfSpot(target, info.ObjectID, info.SpotIndex);
            target.SetOccupied(true, info.gameObject);
            info.UpdateVisuals();
        }

    }

    private void MigrateV1ToV2(SaveDataWrapper w)
    {
        foreach (var book in w.allBooks)
        {
            if (book.tags == null)
                book.tags = new List<string>(); // new field
        }

        w.saveVersion = 2;
        Debug.Log("[Migration] Upgraded save file to version 2.");
    }


    // static shortcuts
    public static void TriggerSave() => _I?.SaveAll();
    public static void TriggerLoad() => _I?.LoadAll();
}
