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
        public int saveVersion = 3;
        public int currentDay = 1;
        public List<BookSaveData> allBooks = new();
        public List<CrateSaveData> allCrates = new();
        public List<FreeformShelfSaveData> allFreeformShelves = new();
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

        // Save all FreeformBookshelf placements
        foreach (var shelf in FindObjectsOfType<FreeformBookshelf>())
        {
            string objectID = shelf.GetID();
            if (string.IsNullOrEmpty(objectID))
            {
                Debug.LogWarning("[Save] FreeformBookshelf missing ObjectID; skipping.");
                continue;
            }

            w.allFreeformShelves.Add(new FreeformShelfSaveData
            {
                shelfObjectID = objectID,
                position = shelf.transform.position,
                rotation = shelf.transform.rotation,
                localScale = shelf.transform.localScale
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

                // inside SaveAll, when info.currentStackRoot != null ...
                var stackRootTransform = info.currentStackRoot.transform;
                if (stackRootTransform != null && stackRootTransform.parent != null)
                {
                    var parentAnchor = stackRootTransform.parent.GetComponent<SurfaceAnchor>();
                    if (parentAnchor != null)
                    {
                        // TABLE parent
                        data.tableID = parentAnchor.GetID();
                        data.shelfRef = null; // make exclusive
                    }
                    else
                    {
                        // Try SHELF parent (FreeformBookshelf region)
                        var regionTransform = stackRootTransform.parent;
                        var shelf = regionTransform.GetComponentInParent<FreeformBookshelf>();
                        if (shelf != null)
                        {
                            string shelfID = shelf.GetID();
                            if (!string.IsNullOrEmpty(shelfID))
                            {
                                data.tableID = null;
                                data.shelfRef = new ShelfRef
                                {
                                    shelfObjectID = shelfID,
                                    regionName = regionTransform.name
                                };
                            }
                        }
                    }
                }
            }
            else
            {
                // Not in a stack — if it’s parented under a shelf region, record it too
                if (info.transform.parent != null)
                {
                    var regionTransform = info.transform.parent;
                    var shelf = regionTransform.GetComponentInParent<FreeformBookshelf>();
                    if (shelf != null)
                    {
                        string shelfID = shelf.GetID();
                        if (!string.IsNullOrEmpty(shelfID))
                        {
                            data.shelfRef = new ShelfRef
                            {
                                shelfObjectID = shelfID,
                                regionName = regionTransform.name
                            };
                        }
                    }
                }
            }


            w.allBooks.Add(data);
        }

        int tableSingles = 0, shelfSingles = 0, tableStacks = 0, shelfStacks = 0;
        foreach (var b in w.allBooks)
        {
            bool inStack = !string.IsNullOrEmpty(b.stackGroupID);
            bool onTable = !string.IsNullOrEmpty(b.tableID);
            bool onShelf = b.shelfRef != null && !string.IsNullOrEmpty(b.shelfRef.shelfObjectID);

            if (inStack) { if (onTable) tableStacks++; else if (onShelf) shelfStacks++; }
            else { if (onTable) tableSingles++; else if (onShelf) shelfSingles++; }
        }
        Debug.Log($"[Save] Books: tableSingles={tableSingles}, shelfSingles={shelfSingles}, tableStacks={tableStacks}, shelfStacks={shelfStacks}, total={w.allBooks.Count}");


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
        foreach (var ff in FindObjectsOfType<FreeformBookshelf>()) Destroy(ff.gameObject);

        if (w != null && GameObject.FindWithTag("Player") != null)
        {
            GameObject.FindWithTag("Player").transform.position = w.playerPos;
        }

        var terminal = FindObjectOfType<ComputerTerminal>();
        if (terminal != null) Destroy(terminal.gameObject);

        // Recreate freeform shelves
        foreach (var fsd in w.allFreeformShelves)
        {
            var prefab = Resources.Load<GameObject>("FreeformBookshelf"); // name your prefab accordingly
            if (prefab == null) { Debug.LogError("Missing Resources/FreeformBookshelf prefab."); continue; }

            var go = Instantiate(prefab, fsd.position, fsd.rotation);
            go.transform.localScale = fsd.localScale;
            var shelf = go.GetComponent<FreeformBookshelf>();
            if (shelf == null) { Debug.LogError("FreeformBookshelf component missing on prefab."); continue; }

            // set private ObjectID back
            shelf.SetID(fsd.shelfObjectID);

            // ensure regions exist
            // (GenerateShelfRegions() is called in Awake already)
        }

        // Build lookup: shelfObjectID -> FreeformBookshelf
        var shelvesByID = new Dictionary<string, FreeformBookshelf>();
        foreach (var ff in FindObjectsOfType<FreeformBookshelf>())
        {
            var id = ff.GetID();
            if (!string.IsNullOrEmpty(id))
            {
                shelvesByID[id] = ff;
                // DEBUG: list regions we can parent to
                var names = string.Join(", ", ff.GetShelfNames());
                Debug.Log($"[Load] Shelf present: {id} with regions: [{names}]");
            }
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

                bool inStack = !string.IsNullOrEmpty(bd.stackGroupID);
                bool hasShelfRef = bd.shelfRef != null && !string.IsNullOrEmpty(bd.shelfRef.shelfObjectID);
                bool hasTableID = !string.IsNullOrEmpty(bd.tableID);
                Debug.Log($"[Load] {bd.title}: inStack={inStack}, tableID={bd.tableID}, shelfRef={(hasShelfRef ? (bd.shelfRef.shelfObjectID + "/" + bd.shelfRef.regionName) : "null")}");

                // still inside: foreach (var bd in w.allBooks)
                if (!string.IsNullOrEmpty(bd.stackGroupID))
                {
                    if (!stacksByID.ContainsKey(bd.stackGroupID))
                        stacksByID[bd.stackGroupID] = new List<(BookSaveData, GameObject)>();

                    stacksByID[bd.stackGroupID].Add((bd, go));
                }

                // If this book is NOT part of a stack (no stackGroupID) but has a shelfRef, parent it
                if (string.IsNullOrEmpty(bd.stackGroupID) && bd.shelfRef != null && !string.IsNullOrEmpty(bd.shelfRef.shelfObjectID))
                {
                    if (shelvesByID.TryGetValue(bd.shelfRef.shelfObjectID, out var shelf))
                    {
                        // Try direct child with that name, then the dictionary, then a fallback.
                        Transform region =
                            shelf.transform.Find(bd.shelfRef.regionName) ??
                            shelf.GetRegionByName(bd.shelfRef.regionName)?.transform;

                        if (region == null)
                        {
                            // Fallback 1: first known region
                            foreach (var name in shelf.GetShelfNames())
                            {
                                region = shelf.GetRegionByName(name)?.transform;
                                if (region != null) { Debug.LogWarning($"[Load] Missing region '{bd.shelfRef.regionName}' on shelf '{bd.shelfRef.shelfObjectID}', using '{name}'"); break; }
                            }
                        }

                        if (region == null)
                        {
                            // Fallback 2: shelf root
                            Debug.LogWarning($"[Load] No region found at all on shelf '{bd.shelfRef.shelfObjectID}'. Parenting to shelf root.");
                            region = shelf.transform;
                        }

                        go.transform.SetParent(region, worldPositionStays: true);
                        Debug.Log($"[Load] Parent '{bd.title}' under {region.GetHierarchyPath()}");
                        var rb = go.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.isKinematic = true;
                            rb.interpolation = RigidbodyInterpolation.None;
                            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                        // ensure the correct layer like runtime placement
                        go.layer = LayerMask.NameToLayer("Book");
                        Debug.Log($"[Load] Parent '{bd.title}' under {region.name} (singular shelf book, kinematic on).");
                    }
                    else
                    {
                        Debug.LogWarning($"[Load] Shelf '{bd.shelfRef.shelfObjectID}' not found in scene. Book stays at world pose.");
                    }
                }

                // If this book is NOT part of a stack but has a tableID, parent it and freeze
                if (string.IsNullOrEmpty(bd.stackGroupID) && !string.IsNullOrEmpty(bd.tableID))
                {
                    foreach (var anchor in FindObjectsOfType<SurfaceAnchor>())
                    {
                        if (anchor.GetID() == bd.tableID)
                        {
                            go.transform.SetParent(anchor.transform, worldPositionStays: true);

                            var rb = go.GetComponent<Rigidbody>();
                            if (rb != null)
                            {
                                rb.isKinematic = true;
                                rb.interpolation = RigidbodyInterpolation.None;
                                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                                rb.velocity = Vector3.zero;
                                rb.angularVelocity = Vector3.zero;
                            }

                            go.layer = LayerMask.NameToLayer("Book");
                            Debug.Log($"[Load] Parent '{bd.title}' under {anchor.name} (singular table book, kinematic on).");
                            break;
                        }
                    }
                }

            }
        }

        foreach (var pair in stacksByID)
        {
            var stackList = pair.Value;
            stackList.Sort((a, b) => a.Item1.stackIndex.CompareTo(b.Item1.stackIndex));

            var rootGO = new GameObject("StackRoot");
            var root = rootGO.AddComponent<BookStackRoot>();

            // Use the first book’s saved pose for the root
            var first = stackList[0].Item1;
            rootGO.transform.SetPositionAndRotation(first.position, first.rotation);

            // --- Choose ONE parent for the stack root (table OR shelf) BEFORE adding books ---
            bool parented = false;

            // TABLE?
            if (!string.IsNullOrEmpty(first.tableID))
            {
                foreach (var anchor in FindObjectsOfType<SurfaceAnchor>())
                {
                    if (anchor.GetID() == first.tableID)
                    {
                        rootGO.transform.SetParent(anchor.transform, worldPositionStays: true);
                        root.context = StackContext.Table;
                        parented = true;
                        break;
                    }
                }
            }

            // SHELF? (only if not already parented to a table)
            if (!parented && first.shelfRef != null && !string.IsNullOrEmpty(first.shelfRef.shelfObjectID))
            {
                if (shelvesByID.TryGetValue(first.shelfRef.shelfObjectID, out var shelf))
                {
                    Transform region = shelf.transform.Find(first.shelfRef.regionName)
                                      ?? shelf.GetRegionByName(first.shelfRef.regionName)?.transform;

                    if (region != null)
                    {
                        rootGO.transform.SetParent(region, worldPositionStays: true);
                        root.context = StackContext.Shelf;
                        parented = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[Load] Stack region '{first.shelfRef.regionName}' missing on shelf {first.shelfRef.shelfObjectID}.");
                    }
                }
            }

            // Fallback: leave unparented and default to Table behavior
            if (!parented) root.context = StackContext.Table;

            // --- Now add/freeze the books exactly once ---
            foreach (var (bookData, bookGO) in stackList)
            {
                bookGO.transform.SetParent(rootGO.transform);
                root.AddBook(bookGO);

                var info = bookGO.GetComponent<BookInfo>();
                if (info) info.currentStackRoot = root;

                var rb = bookGO.GetComponent<Rigidbody>();
                if (rb)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None;
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                }
                bookGO.layer = LayerMask.NameToLayer("Book");
            }
        }
    }

    public static void TriggerSave() => _I?.SaveAll();
    public static void TriggerLoad() => _I?.LoadAll();
}