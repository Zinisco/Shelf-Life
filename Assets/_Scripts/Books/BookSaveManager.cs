using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

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
        public int saveVersion; // version of the save schema
        public int currentDay = 1;
        public int walletMoney = 0;
        public List<BookSaveData> allBooks = new();
        public List<CrateSaveData> allCrates = new();
        public List<FreeformShelfSaveData> allFreeformShelves = new();
        public List<SurfaceAnchorSaveData> allSurfaces = new();
        public List<BookDisplaySaveData> allBookDisplays = new();
        public ComputerSaveData terminalData;
        public Vector3 playerPos;
        public float playerYaw;       // rotation around Y for the player body
        public float cameraPitch;    // local X rotation for your camera
    }
    // === Save versioning ===
    // Bump this whenever you change the save schema.
    private const int CURRENT_SAVE_VERSION = 7;

    // While in development, only accept the current version.
    private const int MIN_COMPATIBLE_VERSION = CURRENT_SAVE_VERSION;

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
        w.saveVersion = CURRENT_SAVE_VERSION;
        w.currentDay = dayNightCycle != null ? dayNightCycle.GetCurrentDay() : 1;
        var stackIDs = new Dictionary<BookStackRoot, string>();
        int stackCounter = 0;

        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var pm = player.GetComponent<PlayerMovement>();
            playerPosition = player.transform.position;

            w.playerPos = playerPosition;
            w.playerYaw = player.transform.eulerAngles.y;

            // get camera pitch from your PlayerMovement (or directly from cam.localEulerAngles.x)
            if (pm != null)
            {
                // expose a getter for pitch on PlayerMovement (see below)
                w.cameraPitch = pm.GetCameraPitch();
            }
            else
            {
                var cam = player.GetComponentInChildren<Camera>()?.transform;
                w.cameraPitch = cam ? cam.localEulerAngles.x : 0f;
            }
        }


        if (CurrencyManager.Instance != null)
            w.walletMoney = CurrencyManager.Instance.GetBalance();
        else
            w.walletMoney = 0;

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

            if (string.IsNullOrEmpty(info.bookID) && info.definition != null)
            {
                info.ApplyDefinition(info.definition);
            }


            if (string.IsNullOrEmpty(info.bookID))
            {
                Debug.LogError($"[Save] Book '{info.name}' has no valid bookID (missing/invalid BookDefinition). Skipping.");
                continue;
            }

            float[] safeColor = new float[] { 1f, 1f, 1f };
            if (info.definition != null)
                safeColor = new float[] { info.definition.color.r, info.definition.color.g, info.definition.color.b };

            var data = new BookSaveData
            {
                bookID = info.bookID,                   // now guaranteed non-empty
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
                    // Check TABLE using the ROOT's parent, not the book's transform
                    var parentAnchor = stackRootTransform.parent.GetComponent<SurfaceAnchor>();
                    if (parentAnchor != null)
                    {
                        data.tableID = parentAnchor.GetID();
                        data.shelfRef = null; // exclusive
                    }
                    else
                    {
                        // SHELF via the ROOT's parent (robust region detection)
                        var regionTransform = FindShelfRegionTransform(stackRootTransform.parent);
                        var shelf = regionTransform ? regionTransform.GetComponentInParent<FreeformBookshelf>() : null;
                        if (shelf != null)
                        {
                            string shelfID = shelf.GetID();
                            if (!string.IsNullOrEmpty(shelfID))
                            {
                                data.tableID = null;
                                data.shelfRef = new ShelfRef
                                {
                                    shelfObjectID = shelfID,
                                    regionName = GetRelativePath(shelf.transform, regionTransform)
                                };
                            }
                        }
                    }
                }
            }
            else
            {
                // Not in a stack — detect SHELF parent robustly
                var regionTransform = FindShelfRegionTransform(info.transform); // <-- start at the book, not parent
                var shelf = regionTransform ? regionTransform.GetComponentInParent<FreeformBookshelf>() : null;
                if (shelf != null)
                {
                    string shelfID = shelf.GetID();
                    if (!string.IsNullOrEmpty(shelfID))
                    {
                        data.tableID = null; // exclusive
                        data.shelfRef = new ShelfRef
                        {
                            shelfObjectID = shelfID,
                            regionName = GetRelativePath(shelf.transform, regionTransform) // <-- save a path, not a single name
                        };
                    }
                }

                // Only if no shelfRef, try TABLE parent for singles
                if (string.IsNullOrEmpty(data.stackGroupID) && data.shelfRef == null)
                {
                    var anchor = info.transform.GetComponentInParent<SurfaceAnchor>();
                    if (anchor != null)
                    {
                        data.tableID = anchor.GetID();
                        data.shelfRef = null; // exclusive
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

        foreach (var display in FindObjectsOfType<BookDisplay>())
        {
            string id = display.GetComponent<BookDisplay>()?.GetID(); // assumes BookDisplay has a unique ID
            if (string.IsNullOrEmpty(id)) continue;

            string tableID = null;
            var anchor = display.transform.parent?.GetComponent<SurfaceAnchor>();
            if (anchor != null)
            {
                tableID = anchor.GetID();
            }

            string attachedBookID = null;
            if (display.attachedBook != null)
            {
                var info = display.attachedBook.GetComponent<BookInfo>();
                if (info != null && !string.IsNullOrEmpty(info.bookID))
                    attachedBookID = info.bookID;
            }

            w.allBookDisplays.Add(new BookDisplaySaveData
            {
                objectID = id,
                position = display.transform.position,
                rotation = display.transform.rotation,
                attachedBookID = attachedBookID,
                tableID = tableID // save anchor ID
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

        var json = File.ReadAllText(path);
        var w = JsonUtility.FromJson<SaveDataWrapper>(json);

        // --- Version guard ---
        if (w == null)
        {
            Debug.LogWarning("[Load] Save file unreadable. Clearing.");
            System.IO.File.Delete(path);
            return;
        }

        if (w.saveVersion < MIN_COMPATIBLE_VERSION || w.saveVersion > CURRENT_SAVE_VERSION)
        {
            Debug.LogWarning($"[Load] Save version {w.saveVersion} incompatible with " +
                             $"engine ({CURRENT_SAVE_VERSION}). Deleting old save.");
            System.IO.File.Delete(path);
            return;
        }

        // Restore Day
        if (dayNightCycle != null)
            dayNightCycle.SetDay(w.currentDay);

        // Restore wallet
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.SetBalance(w.walletMoney);

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            var pm = playerGO.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.Teleport(w.playerPos, w.playerYaw, w.cameraPitch);
            }
            else
            {
                // fallback if no PlayerMovement (still disable CC)
                var cc = playerGO.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                playerGO.transform.position = w.playerPos;
                playerGO.transform.rotation = Quaternion.Euler(0f, w.playerYaw, 0f);
                Physics.SyncTransforms();
                if (cc) cc.enabled = true;
            }
        }

        foreach (var b in FindObjectsOfType<BookInfo>()) Destroy(b.gameObject);
        foreach (var c in FindObjectsOfType<BookCrate>()) Destroy(c.gameObject);
        foreach (var t in FindObjectsOfType<SurfaceAnchor>()) Destroy(t.gameObject);
        foreach (var ff in FindObjectsOfType<FreeformBookshelf>()) Destroy(ff.gameObject);
        foreach (var bd in FindObjectsOfType<BookDisplay>()) Destroy(bd.gameObject);

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

        // Build lookup: shelfObjectID -> list of shelves (handles legacy duplicate IDs safely)
        var shelvesByID = new Dictionary<string, List<FreeformBookshelf>>();
        foreach (var ff in FindObjectsOfType<FreeformBookshelf>())
        {
            var id = ff.GetID();
            if (string.IsNullOrEmpty(id)) continue;

            if (!shelvesByID.TryGetValue(id, out var list))
                shelvesByID[id] = list = new List<FreeformBookshelf>();
            list.Add(ff);

            var names = string.Join(", ", ff.GetShelfNames());
            Debug.Log($"[Load] Shelf present: {id} with regions: [{names}] at {ff.transform.position}");
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
        var displaysByID = new Dictionary<string, BookDisplay>();

        foreach (var bdd in w.allBookDisplays)
        {
            var prefab = Resources.Load<GameObject>("BookDisplay");
            var go = Instantiate(prefab, bdd.position, bdd.rotation);
            var display = go.GetComponent<BookDisplay>();
            if (display == null) continue;

            display.SetID(bdd.objectID);
            displaysByID[bdd.objectID] = display;

            // Parent to table if needed
            if (!string.IsNullOrEmpty(bdd.tableID))
            {
                foreach (var anchor in FindObjectsOfType<SurfaceAnchor>())
                {
                    if (anchor.GetID() == bdd.tableID)
                    {
                        go.transform.SetParent(anchor.transform, worldPositionStays: true);
                        break;
                    }
                }
            }
        }

        Dictionary<string, List<GameObject>> instantiatedBooksByID = new();

        foreach (var bd in w.allBooks)
        {

            var prefab = bookDatabase.GetBookPrefabByID(bd.bookID);
            if (prefab == null)
            {
                Debug.LogError($"[Load] No prefab found for bookID '{bd.bookID}'. Skipping this book.");
                continue;
            }

            var go = Instantiate(prefab, bd.position, bd.rotation);
            var info = go.GetComponent<BookInfo>();
            if (info != null)
            {
                info.ObjectID = bd.shelfID;

                // Prefer the canonical DB definition for that bookID
                var dbDef = bookDatabase.GetDefinitionByID(bd.bookID);

                if (dbDef != null)
                {
                    info.ApplyDefinition(dbDef); // <-- sets bookID internally
                }
                else
                {
                    // Fallback: reconstruct a definition snapshot from JSON
                    var snap = ScriptableObject.CreateInstance<BookDefinition>();
                    snap.bookID = bd.bookID; // still keep the saved ID
                    snap.title = bd.title;
                    snap.genre = bd.genre;
                    snap.price = bd.price;
                    snap.cost = bd.cost;
                    snap.summary = bd.summary;
                    if (bd.color != null && bd.color.Length == 3)
                        snap.color = new Color(bd.color[0], bd.color[1], bd.color[2]);

                    Debug.LogWarning($"[Load] DB missing definition for '{bd.bookID}'. Using snapshot from save.");
                    info.ApplyDefinition(snap);
                }

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
                    var shelf = PickShelfByProximity(shelvesByID, bd.shelfRef.shelfObjectID, bd.position);
                    if (shelf != null)
                    {
                        Transform region = ResolveShelfRegion(shelf, bd.shelfRef.regionName);
                        go.transform.SetParent(region, worldPositionStays: true);
                        var rb = go.GetComponent<Rigidbody>();
                        if (rb) { rb.isKinematic = true; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                        go.layer = LayerMask.NameToLayer("Book");
                        Debug.Log($"[Load] Parent '{bd.title}' under {region.GetHierarchyPath()}");
                    }
                    else
                    {
                        Debug.LogWarning($"[Load] Shelf '{bd.shelfRef.shelfObjectID}' not found for '{bd.title}'.");
                    }
                }

                if (!instantiatedBooksByID.TryGetValue(bd.bookID, out var list))
                    instantiatedBooksByID[bd.bookID] = list = new List<GameObject>();
                list.Add(go);

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
                            }

                            go.layer = LayerMask.NameToLayer("Book");
                            Debug.Log($"[Load] Parent '{bd.title}' under {anchor.name} (singular table book, kinematic on).");
                            break;
                        }
                    }
                }

            }
        }

        foreach (var bdd in w.allBookDisplays)
        {
            if (string.IsNullOrEmpty(bdd.attachedBookID)) continue;

            if (displaysByID.TryGetValue(bdd.objectID, out var display))
            {
                if (instantiatedBooksByID.TryGetValue(bdd.attachedBookID, out var candidates) && candidates.Count > 0)
                {
                    var go = candidates[0];
                    candidates.RemoveAt(0); // prevent reuse

                    var bookInfo = go.GetComponent<BookInfo>();
                    if (bookInfo != null)
                    {
                        go.transform.SetParent(display.transform.Find("BookAnchor"), worldPositionStays: false);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localRotation = Quaternion.Euler(0, 90, 0);

                        display.attachedBook = go;

                        var rb = go.GetComponent<Rigidbody>();
                        if (rb)
                        {
                            rb.isKinematic = true;
                            rb.interpolation = RigidbodyInterpolation.None;
                            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                        }

                        bookInfo.currentStackRoot = null;
                        bookInfo.UpdateVisuals();
                    }
                }
                else
                {
                    Debug.LogWarning($"[Load] No remaining instance for bookID {bdd.attachedBookID} to place on display {bdd.objectID}.");
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
                var shelf = PickShelfByProximity(shelvesByID, first.shelfRef.shelfObjectID, first.position);
                if (shelf != null)
                {
                    Transform region = ResolveShelfRegion(shelf, first.shelfRef.regionName);
                    rootGO.transform.SetParent(region, worldPositionStays: true);
                    root.context = StackContext.Shelf;
                    parented = true;
                }
                else
                {
                    Debug.LogWarning($"[Load] Shelf '{first.shelfRef.shelfObjectID}' not found for stack '{pair.Key}'.");
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
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None;
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                }
                bookGO.layer = LayerMask.NameToLayer("Book");
            }
        }
    }

    private static Transform ResolveShelfRegion(FreeformBookshelf shelf, string regionNameHint)
    {
        if (shelf == null || string.IsNullOrEmpty(regionNameHint)) return shelf ? shelf.transform : null;

        // Normalize hint once
        string Norm(string s) => s.Replace("(Clone)", "").Trim().ToLowerInvariant();
        string hint = Norm(regionNameHint);

        // 0) If the hint looks like a path (has '/'), try Transform.Find (supports paths)
        if (regionNameHint.Contains("/"))
        {
            var tPath = shelf.transform.Find(regionNameHint);
            if (tPath != null) return tPath;
            // also try normalized path (remove "(Clone)" on each segment)
            var segs = regionNameHint.Split('/');
            for (int i = 0; i < segs.Length; i++) segs[i] = segs[i].Replace("(Clone)", "").Trim();
            var tPath2 = shelf.transform.Find(string.Join("/", segs));
            if (tPath2 != null) return tPath2;
        }

        // 1) Depth-first search over ALL descendants (exact, case-insensitive)
        Transform exact = null;
        var stack = new Stack<Transform>();
        stack.Push(shelf.transform);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            // push children
            for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));

            if (Norm(t.name) == hint) { exact = t; break; }
        }
        if (exact != null) return exact;

        // 2) Loose match (contains/startsWith), still over ALL descendants
        Transform loose = null;
        stack.Clear();
        stack.Push(shelf.transform);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));

            string nm = Norm(t.name);
            if (nm.Contains(hint) || hint.Contains(nm) || nm.StartsWith(hint))
            {
                loose = t; break;
            }
        }
        if (loose != null) return loose;

        // 3) Try shelf API names (closest by distance)
        Transform best = null;
        float bestDist = float.PositiveInfinity;
        foreach (var name in shelf.GetShelfNames())
        {
            var region = shelf.GetRegionByName(name)?.transform;
            if (region == null) continue;
            float d = (region.position - shelf.transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = region; }
            // Also allow direct name equality ignoring clone suffix
            if (Norm(name) == hint) { best = region; break; }
        }
        if (best != null) return best;

        // 4) Final fallback: shelf root (better than null)
        return shelf.transform;
    }

    private static Transform FindShelfRegionTransform(Transform t)
    {
        if (t == null) return null;

        // climb until we hit a FreeformBookshelf or root
        Transform current = t;
        while (current != null)
        {
            var shelf = current.GetComponentInParent<FreeformBookshelf>();
            if (shelf == null) return null; // not under a shelf at all

            // We want the direct child under the shelf that represents a region.
            // Accept either: layer == ShelfRegion OR has a BoxCollider that matches shelf regions.
            if (current.parent == shelf.transform)
            {
                // layer match?
                bool isShelfRegionLayer = current.gameObject.layer == LayerMask.NameToLayer("ShelfRegion");
                // collider match?
                var bc = current.GetComponent<BoxCollider>();
                bool looksLikeRegion = isShelfRegionLayer || bc != null;

                if (looksLikeRegion) return current;
            }

            current = current.parent;
        }
        return null;
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (root == null || target == null) return null;
        var stack = new System.Collections.Generic.Stack<string>();
        var t = target;
        while (t != null && t != root)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        if (t != root) return target.name; // fallback: not actually a descendant
        return string.Join("/", stack);
    }

    private static FreeformBookshelf PickShelfByProximity(
    Dictionary<string, List<FreeformBookshelf>> map,
    string shelfID,
    Vector3 bookPos)
    {
        if (!map.TryGetValue(shelfID, out var list) || list == null || list.Count == 0)
            return null;
        if (list.Count == 1) return list[0];

        FreeformBookshelf best = null;
        float bestDist = float.PositiveInfinity;
        foreach (var s in list)
        {
            float d = (s.transform.position - bookPos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }

    // Example migration hook (currently unused)
    private bool TryMigrateSave(SaveDataWrapper w)
    {
        // Example:
        // if (w.saveVersion == 3) { MigrateFromV3ToV4(w); w.saveVersion = 4; }
        // return true if all steps succeed
        return false; // while in dev, we just say "nope" and delete the save
    }

    // private void MigrateFromV3ToV4(SaveDataWrapper w)
    // {
    //     // ...do transformations on w to match the new schema...
    // }


    public static void TriggerSave() => _I?.SaveAll();
    public static void TriggerLoad() => _I?.LoadAll();
}