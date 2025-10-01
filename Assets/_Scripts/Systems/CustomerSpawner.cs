using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CustomerSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform[] sidewalkPoints;
    [SerializeField] private StoreSignController storeSign;
    [SerializeField] private Transform[] spawnPoints;   // positions outside your shop door
    [SerializeField] private GameObject customerPrefab; // your NPC prefab
    [SerializeField] private Transform storeEntryPoint; // NEW: assign in inspector (just inside doorway)

    [Header("Spawn Settings")]
    [SerializeField] private float spawnIntervalMin = 5f;
    [SerializeField] private float spawnIntervalMax = 12f;

    private Coroutine spawnRoutine;

    public List<GameObject> activeCustomers = new List<GameObject>();

    void Start()
    {
        if (storeSign == null)
            storeSign = FindObjectOfType<StoreSignController>();
    }

    void Update()
    {
        if (spawnRoutine == null)
        {
            spawnRoutine = StartCoroutine(SpawnLoop());
        }
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(spawnIntervalMin, spawnIntervalMax));
            SpawnCustomer();
        }
    }

    void SpawnCustomer()
    {
        if (customerPrefab == null || spawnPoints.Length == 0) return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Vector3 spawnPos = spawnPoint.position;

        // Snap to NavMesh surface
        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        GameObject customer = Instantiate(customerPrefab, spawnPos, spawnPoint.rotation);
        activeCustomers.Add(customer);

        CustomerAI ai = customer.GetComponent<CustomerAI>();
        ai.sidewalkPoints = sidewalkPoints;
        ai.storeEntryPoint = storeEntryPoint;

        // Remove from list on destroy
        customer.AddComponent<DespawnNotifier>().Init(this);
    }

    public class DespawnNotifier : MonoBehaviour
    {
        private CustomerSpawner spawner;
        public void Init(CustomerSpawner s) => spawner = s;
        void OnDestroy() { if (spawner) spawner.activeCustomers.Remove(gameObject); }
    }


}
