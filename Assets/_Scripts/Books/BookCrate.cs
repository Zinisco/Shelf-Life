using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;   // for Keyboard.current

/// <summary>
/// When "opened", this crate will spawn a random assortment of books from your BookDatabase.
/// </summary>
public class BookCrate : MonoBehaviour
{

    [SerializeField] private string crateID;


    [Header("Game Input")]
    [SerializeField] private GameInput gameInput;

    [SerializeField] private ParticleSystem openParticles;

    [Tooltip("The ScriptableObject holding all your BookDefinitions")]
    [SerializeField] private BookDatabase bookDatabase;

    [Tooltip("How many books to deliver in this crate")]
    [SerializeField] private int crateSize = 5;

    [Tooltip("If false, ensures each book is unique (requires crateSize <= database count)")]
    [SerializeField] private bool allowDuplicates = false;

    [SerializeField] private bool _opened = false;
    [SerializeField] private bool _playerInRange = false;

    [SerializeField] private bool isHeld = false;

    [SerializeField] private float animationDuration = 0.1f; // Adjust to your animation length

    private List<BookDefinition> customBooks = null;



    private Animator animator;

    private void Awake()
    {
        if (string.IsNullOrEmpty(crateID))
            crateID = System.Guid.NewGuid().ToString();

        animator = GetComponent<Animator>();
    }


    private void Start()
    {

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        gameInput = GameInput.Instance;
        if (gameInput != null)
        {
            gameInput.OnInteractAction += GameInput_OnInteractAction;
        }

    }

    private void GameInput_OnInteractAction(object sender, System.EventArgs e)
    {
        Open();
    }

    /// <summary>
    /// Call this (e.g. via an interact button) to spill the books out of the crate.
    /// </summary>
    public void Open()
    {
        if (_opened || !_playerInRange || isHeld) return;

        Debug.Log("Opening Crate...");
        _opened = true;

        if (gameInput != null)
            gameInput.OnInteractAction -= GameInput_OnInteractAction;

        if (animator != null)
        {
            //animator.SetTrigger("Open");
            StartCoroutine(SpawnAndDestroyAfterDelay(animationDuration));
        }
        else
        {
            // If no animator is present, fallback immediately
            SpawnBooks();
            PlayParticleEffect();
            Destroy(gameObject);
        }
    }


    private void OnDestroy()
    {
        if (gameInput != null)
            gameInput.OnInteractAction -= GameInput_OnInteractAction;
    }


    private void Reset()
    {
        // make sure there's a trigger collider on this GameObject
        var col = GetComponent<Collider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInRange = false;
    }

    public void MarkUnopened()
    {
        _opened = false;
    }

    public void SetCrateID(string id)
    {
        crateID = id;
    }

    public void PlayParticleEffect()
    {
        if (openParticles != null)
            openParticles.Play();
    }

    private IEnumerator SpawnAndDestroyAfterDelay(float delay)
    {
        // Wait for animation to finish
        yield return new WaitForSeconds(delay);

        // Play particles right before destruction
        PlayParticleEffect();

        // Optional: delay destroy a tiny bit to let particles play
        yield return new WaitForSeconds(.3f);

        // Spawn books after animation and particle effect
        SpawnBooks();

        Destroy(gameObject);
    }

    private void SpawnBooks()
    {
        List<BookDefinition> booksToSpawn = customBooks ?? GetRandomBooks();

        foreach (var def in booksToSpawn)
        {
            Vector3 rnd = Random.insideUnitCircle * 0.3f;
            Vector3 origin = transform.position;
            Vector3 spawnPos = origin + new Vector3(rnd.x, 0f, rnd.y);
            Quaternion spawnRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var go = Instantiate(def.prefab, spawnPos, spawnRot);
            if (go.TryGetComponent<BookVisual>(out var vis))
                vis.ApplyDefinition(def);

            if (go.TryGetComponent<BookInfo>(out var info))
                info.ApplyDefinition(def);
        }
    }

    private List<BookDefinition> GetRandomBooks()
    {
        var allDefs = bookDatabase.allBooks;
        if (allDefs == null || allDefs.Count == 0)
        {
            Debug.LogWarning("BookCrate: no definitions in BookDatabase");
            return new List<BookDefinition>();
        }

        List<BookDefinition> pool = new List<BookDefinition>(allDefs);
        if (!allowDuplicates && crateSize <= pool.Count)
            Shuffle(pool);

        List<BookDefinition> result = new List<BookDefinition>();
        for (int i = 0; i < crateSize; i++)
        {
            BookDefinition def = allowDuplicates
                ? allDefs[Random.Range(0, allDefs.Count)]
                : pool[i];

            result.Add(def);
        }

        return result;
    }


    public void SetCustomBooks(List<BookDefinition> books)
    {
        customBooks = new List<BookDefinition>(books); // clone to avoid external mutation
        crateSize = customBooks.Count;
        allowDuplicates = true; // make sure spawn logic doesn't restrict
    }


    // Fisher–Yates shuffle
    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    public bool IsOpened() => _opened;
    public string GetCrateID() => crateID;

    public void SetHeld(bool held) => isHeld = held;

}