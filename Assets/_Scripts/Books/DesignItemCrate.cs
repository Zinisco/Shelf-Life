using UnityEngine;
using System;

[RequireComponent(typeof(Collider))]
public class DesignItemCrate : MonoBehaviour
{
    [Header("Crate Settings")]
    [SerializeField] private string crateID;

    [SerializeField] private Transform placementAnchor;
    public Transform GetPlacementAnchor() => placementAnchor;


    [Header("Game Input")]
    [SerializeField] private GameInput gameInput;

    [Header("Crate Contents")]
    [SerializeField] private ParticleSystem openEffect;

    private DesignItem designItem;
    private bool _playerInRange = false;
    private bool _opened = false;
    private bool isHeld = false;

    private void Awake()
    {
        // Generate unique ID
        if (string.IsNullOrEmpty(crateID))
            crateID = Guid.NewGuid().ToString();
    }

    private void Start()
    {
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        gameInput = GameInput.Instance;
        if (gameInput != null)
        {
            gameInput.OnInteractAction += GameInput_OnInteractAction;
        }
    }


    private void OnDestroy()
    {
        if (gameInput != null)
        {
            gameInput.OnInteractAction -= GameInput_OnInteractAction;
        }
    }

    private void GameInput_OnInteractAction(object sender, EventArgs e)
    {
        OpenCrate();
    }

    public void SetDesignItem(DesignItem item)
    {
        designItem = item;
    }

    public void OpenCrate()
    {
        if (_opened || !_playerInRange || isHeld) return;

        _opened = true;

        if (gameInput != null)
            gameInput.OnInteractAction -= GameInput_OnInteractAction;

        // Spawn the design item
        if (designItem != null)
        {
            Instantiate(designItem.itemPrefab, transform.position + Vector3.up, Quaternion.identity);

            if (openEffect != null)
                Instantiate(openEffect, transform.position, Quaternion.identity);

            Debug.Log($"Opened Design Crate: {crateID}");

            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("DesignItem not assigned to crate.");
        }
    }

    public void EnablePhysics()
    {
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
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

    public void SetHeld(bool held)
    {
        isHeld = held;
        if (isHeld)
            EnablePhysics();
    }

    // Accessors
    public string GetCrateID() => crateID;
    public bool IsOpened() => _opened;
}
