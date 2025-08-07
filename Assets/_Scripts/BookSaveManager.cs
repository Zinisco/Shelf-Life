using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class SurfaceAnchorSaveData
{
    public string surfaceID;
    public Vector3 position;
    public Quaternion rotation;
}

public class BookSaveManager : MonoBehaviour
{
    [System.Serializable]
    public class SaveDataWrapper
    {
        public int saveVersion = 2;
        public int currentDay = 1;
        public List<BookSaveData> allBooks = new();
        public List<BookshelfSaveData> allShelves = new();
        public List<CrateSaveData> allCrates = new();
        public List<SurfaceAnchorSaveData> allSurfaces = new();
        public ComputerSaveData terminalData;
        public Vector3 playerPos;
    }

    [SerializeField] private DayNightCycle dayNightCycle;
    [SerializeField] private BookDatabase bookDatabase;
    private static BookSaveManager _I;
    public Vector3 playerPosition;

    private void Awake()
    {
        if (_I == null) _I = this;
        else { Destroy(gameObject); return; }

        if (bookDatabase == null)
            bookDatabase = Resources.Load<BookDatabase>("BookDatabase");
    }

    private void Start()
    {
        if (PlayerPrefs.GetInt("ContinueFlag", 0) == 1)
        {
            PlayerPrefs.SetInt("ContinueFlag", 0); // Reset
            LoadAll(); // or TriggerLoad() if it's static
        }
    }


    public void SaveAll()
    {
        var w = new SaveDataWrapper();
        w.currentDay = dayNightCycle != null ? dayNightCycle.GetCurrentDay() : 1;
        var stackIDs = new Dictionary<BookStackRoot, string>();
        int stackCounter = 0;

        var player = GameObject.FindWithTag("Player");
        if (player != null)
            playerPosition = player.transform.position;

        w.playerPos = playerPosition;

        foreach (var anchor in FindObjectsOfType<SurfaceAnchor>())
        {
            if (anchor == null || anchor.GetID() == null)
            {
                Debug.LogWarning("Null SurfaceAnchor or missing ID detected during save.");
                continue;
            }

            w.allSurfaces.Add(new SurfaceAnchorSaveData
            {
                surfaceID = anchor.GetID(),
                position = anchor.transform.position,
                rotation = anchor.transform.rotation
            });
        }


        foreach (var info in FindObjectsOfType<BookInfo>())
        {
            float[] safeColor = new float[] { 1f, 1f, 1f };
            if (info.definition != null)
            {
                safeColor = new float[] {
        info.definition.color.r,
        info.definition.color.g,
        info.definition.color.b
    };
            }

            var data = new BookSaveData
            {
                bookID = info.bookID,
                title = info.definition?.title ?? "",
                genre = info.definition?.genre ?? "",
                summary = info.definition?.summary ?? "",
                price = info.definition?.price ?? 0,
                cost = info.definition?.cost ?? 0,
                color = safeColor,
                tags = new List<string>(info.tags),
                shelfID = info.ObjectID,
                position = info.transform.position,
                rotation = info.transform.rotation
            };

            if (info.currentStackRoot != null)
            {
                if (!stackIDs.ContainsKey(info.currentStackRoot))
                    stackIDs[info.currentStackRoot] = "stack_" + stackCounter++;

                data.stackGroupID = stackIDs[info.currentStackRoot];
                data.stackIndex = info.currentStackRoot.GetBookIndex(info.gameObject);

                var stackRootTransform = info.currentStackRoot.transform;
                if (stackRootTransform != null && stackRootTransform.parent != null)
                {
                    var parentAnchor = stackRootTransform.parent.GetComponent<SurfaceAnchor>();
                    if (parentAnchor != null)
                        data.tableID = parentAnchor.GetID();
                }
            }


            w.allBooks.Add(data);
        }

        foreach (var crate in FindObjectsOfType<BookCrate>())
        {
            w.allCrates.Add(new CrateSaveData
            {
                crateID = crate.GetCrateID(),
                position = crate.transform.position,
                rotation = crate.transform.rotation,
                opened = crate.IsOpened()
            });
        }

        var terminal = FindObjectOfType<ComputerTerminal>();
        if (terminal != null)
        {
            w.terminalData = new ComputerSaveData
            {
                Position = terminal.transform.position,
                Rotation = terminal.transform.rotation
            };
        }

        File.WriteAllText(Path.Combine(Application.persistentDataPath, "booksave.json"),
                          JsonUtility.ToJson(w, true));
        Debug.Log("Saved!");
    }

    public void LoadAll()
    {
        string path = Path.Combine(Application.persistentDataPath, "booksave.json");
        if (!File.Exists(path)) { Debug.LogWarning("No save."); return; }

        var w = JsonUtility.FromJson<SaveDataWrapper>(File.ReadAllText(path));

        if (dayNightCycle != null)
            dayNightCycle.SetDay(w.currentDay);

        foreach (var b in FindObjectsOfType<BookInfo>()) Destroy(b.gameObject);
        foreach (var c in FindObjectsOfType<BookCrate>()) Destroy(c.gameObject);
        foreach (var t in FindObjectsOfType<SurfaceAnchor>()) Destroy(t.gameObject);

        if (w != null && GameObject.FindWithTag("Player") != null)
        {
            GameObject.FindWithTag("Player").transform.position = w.playerPos;
        }

        var terminal = FindObjectOfType<ComputerTerminal>();
        if (terminal != null) Destroy(terminal.gameObject);

        foreach (var sd in w.allShelves)
        {
            var go = Instantiate(Resources.Load<GameObject>("Bookshelf"));
            go.transform.position = sd.Position;
            go.transform.rotation = sd.Rotation;
        }

        foreach (var surfaceData in w.allSurfaces)
        {
            var prefab = Resources.Load<GameObject>("SurfaceTable");
            var go = Instantiate(prefab, surfaceData.position, surfaceData.rotation);
            var anchor = go.GetComponent<SurfaceAnchor>();
            typeof(SurfaceAnchor).GetField("surfaceID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                 .SetValue(anchor, surfaceData.surfaceID);
        }

        if (w.terminalData != null)
        {
            var go = Instantiate(Resources.Load<GameObject>("ComputerTerminal"));
            go.transform.position = w.terminalData.Position;
            go.transform.rotation = w.terminalData.Rotation;
        }

        var stacksByID = new Dictionary<string, List<(BookSaveData, GameObject)>>();

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

                var def = ScriptableObject.CreateInstance<BookDefinition>();
                def.bookID = bd.bookID;
                def.title = bd.title;
                def.genre = bd.genre;
                def.price = bd.price;
                def.cost = bd.cost;
                def.summary = bd.summary;

                if (bd.color != null && bd.color.Length == 3)
                    def.color = new Color(bd.color[0], bd.color[1], bd.color[2]);

                info.ApplyDefinition(def);
                info.tags = new List<string>(bd.tags ?? new List<string>());
                info.UpdateVisuals();

                if (!string.IsNullOrEmpty(bd.stackGroupID))
                {
                    if (!stacksByID.ContainsKey(bd.stackGroupID))
                        stacksByID[bd.stackGroupID] = new();

                    stacksByID[bd.stackGroupID].Add((bd, go));
                }
            }
        }

        foreach (var pair in stacksByID)
        {
            var stackList = pair.Value;
            stackList.Sort((a, b) => a.Item1.stackIndex.CompareTo(b.Item1.stackIndex));

            var rootGO = new GameObject("StackRoot");
            var root = rootGO.AddComponent<BookStackRoot>();
            root.stackTitle = stackList[0].Item1.title;
            rootGO.transform.position = stackList[0].Item1.position;
            rootGO.transform.rotation = stackList[0].Item1.rotation;

            if (!string.IsNullOrEmpty(stackList[0].Item1.tableID))
            {
                foreach (var anchor in FindObjectsOfType<SurfaceAnchor>())
                {
                    if (anchor.GetID() == stackList[0].Item1.tableID)
                    {
                        rootGO.transform.SetParent(anchor.transform);
                        break;
                    }
                }
            }

            foreach (var (bookData, bookGO) in stackList)
            {
                bookGO.transform.SetParent(rootGO.transform);
                root.AddBook(bookGO);

                var bookInfo = bookGO.GetComponent<BookInfo>();
                bookInfo.currentStackRoot = root;

                // Disable physics
                var rb = bookGO.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None;
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                }
            }

        }
    }

    public static void TriggerSave() => _I?.SaveAll();
    public static void TriggerLoad() => _I?.LoadAll();
}