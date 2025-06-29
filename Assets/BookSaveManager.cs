using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BookSaveManager : MonoBehaviour
{

    [System.Serializable]
    public class SaveDataWrapper
    {
        public List<BookSaveData> allBooks = new();
        public List<BookshelfSaveData> allShelves = new();
        public List<TableSaveData> allTables = new();
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

        // --- books ---
        foreach (var info in FindObjectsOfType<BookInfo>())
        {
            // SKIP if book is stacked (parented to a TableSpot)
            if (info.transform.parent != null && info.transform.parent.GetComponent<TableSpot>() != null)
                continue;

            // Save only if shelved or loose
            w.allBooks.Add(new BookSaveData
            {
                bookID = info.bookID,
                title = info.definition != null ? info.definition.title : "",
                genre = info.definition != null ? info.definition.genre : "",
                summary = info.definition != null ? info.definition.summary : "",
                color = info.definition != null ? new float[] {
            info.definition.color.r,
            info.definition.color.g,
            info.definition.color.b
        } : new float[] { 1f, 1f, 1f },

                shelfID = info.ObjectID,
                spotIndex = info.SpotIndex,
                position = info.transform.position,
                rotation = info.transform.rotation
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
                rotation = table.transform.rotation
            };

            var spot = table.GetComponentInChildren<TableSpot>();
            if (spot != null)
            {
                spot.RefreshStack();
                var stacked = spot.GetSaveData(); // this returns a TableSaveData with stackedBooks
                data.stackedBooks = stacked.stackedBooks;
            }

            w.allTables.Add(data);
            Debug.Log($"[SAVE] Table '{data.tableID}' has {data.stackedBooks.Count} books.");
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

        // destroy old books, shelves, and tables
        foreach (var b in FindObjectsOfType<BookInfo>()) Destroy(b.gameObject);
        foreach (var s in FindObjectsOfType<Bookshelf>()) Destroy(s.gameObject);
        foreach (var t in FindObjectsOfType<BookTable>()) Destroy(t.gameObject);

        // recreate shelves
        foreach (var sd in w.allShelves)
        {
            var go = Instantiate(Resources.Load<GameObject>("Bookshelf"));
            go.transform.position = sd.Position;
            go.transform.rotation = sd.Rotation;
            go.GetComponent<Bookshelf>().SetID(sd.ShelfID);
        }

        // recreate ALL books — shelved AND table/loose
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

                // Create a definition from save
                BookDefinition def = ScriptableObject.CreateInstance<BookDefinition>();
                def.bookID = bd.bookID;
                def.title = bd.title;
                def.genre = bd.genre;
                def.summary = bd.summary;

                if (bd.color != null && bd.color.Length == 3)
                    def.color = new Color(bd.color[0], bd.color[1], bd.color[2]);

                info.ApplyDefinition(def);
                info.UpdateVisuals();
            }
        }



        foreach (var tableData in w.allTables)
        {
            var prefab = Resources.Load<GameObject>("BookTable");
            var tableGO = Instantiate(prefab);
            tableGO.transform.position = tableData.position;
            tableGO.transform.rotation = tableData.rotation;

            var tableComp = tableGO.GetComponent<BookTable>();
            tableComp.SetID(tableData.tableID);

            // Now safely get the TableSpot and load books
            var tableSpot = tableGO.GetComponentInChildren<TableSpot>();
            if (tableSpot != null)
            {
                tableSpot.LoadBooksFromData(tableData, bookDatabase);
            }
            else
            {
                Debug.LogWarning($"[LoadAll] No TableSpot found under {tableGO.name}");
            }

            Debug.Log($"[LoadAll] Loaded table '{tableData.tableID}' and applied book data with {tableData.stackedBooks.Count} books.");

        }



        // **reload table?stacks on the *new* spots**  
        //StartCoroutine(DelayedTableRestore(w));


        // finally put the shelved books back on shelves
        StartCoroutine(ReconnectShelves());

        Debug.Log("[LoadAll] Completed book table restore phase.");

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

    private IEnumerator DelayedTableRestore(SaveDataWrapper w)
    {
        yield return new WaitForSeconds(0.1f); // Let Unity finish instantiating the hierarchy

        foreach (var spot in FindObjectsOfType<TableSpot>())
        {
            Debug.Log($"[DelayedRestore] Found TableSpot with ID: {spot.objectID}");
            var data = w.allTables.Find(t => t.tableID == spot.objectID);
            if (data != null)
            {
                Debug.Log($"[DelayedRestore] Found table data with {data.stackedBooks.Count} books");
                spot.LoadBooksFromData(data, bookDatabase);
            }
            else
            {
                Debug.LogWarning($"[DelayedRestore] No data found for table ID: {spot.objectID}");
            }
        }
    }


    // static shortcuts
    public static void TriggerSave() => _I?.SaveAll();
    public static void TriggerLoad() => _I?.LoadAll();
}
