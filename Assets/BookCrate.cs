using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;   // for Keyboard.current

/// <summary>
/// When "opened", this crate will spawn a random assortment of books from your BookDatabase.
/// </summary>
public class BookCrate : MonoBehaviour
{
    [Tooltip("The ScriptableObject holding all your BookDefinitions")]
    [SerializeField] private BookDatabase bookDatabase;

    [Tooltip("How many books to deliver in this crate")]
    [SerializeField] private int crateSize = 5;

    [Tooltip("If false, ensures each book is unique (requires crateSize <= database count)")]
    [SerializeField] private bool allowDuplicates = false;

    private bool _opened = false;
    private bool _playerInRange = false;

    private void Update()
    {
        // as soon as E is pressed and we're in range, open it
        if (!_opened && _playerInRange && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Open();
        }
    }

    /// <summary>
    /// Call this (e.g. via an interact button) to spill the books out of the crate.
    /// </summary>
    public void Open()
    {
        _opened = true;

        Debug.Log("Opening Crate...");

        var allDefs = bookDatabase.allBooks;
        if (allDefs == null || allDefs.Count == 0)
        {
            Debug.LogWarning("BookCrate: no definitions in BookDatabase");
            return;
        }

        // pick your crate pool…
        List<BookDefinition> pool = new List<BookDefinition>(allDefs);
        if (!allowDuplicates && crateSize <= pool.Count)
            Shuffle(pool);

        for (int i = 0; i < crateSize; i++)
        {
            BookDefinition def = allowDuplicates
                ? allDefs[Random.Range(0, allDefs.Count)]
                : pool[i];

            Vector3 rnd = Random.insideUnitCircle * 0.3f;
            Vector3 origin = transform.position;       // the crate’s position
            Vector3 spawnPos = origin + new Vector3(rnd.x, 0f, rnd.y);
            Quaternion spawnRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var go = Instantiate(def.prefab, spawnPos, spawnRot);
            if (go.TryGetComponent<BookVisual>(out var vis))
                vis.ApplyDefinition(def);

            if (go.TryGetComponent<BookInfo>(out var info))
                info.ApplyDefinition(def);
        }

        Destroy(this.gameObject);

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
}