using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class CustomerSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform[] sidewalkPoints;
    [SerializeField] private StoreSignController storeSign;
    [SerializeField] private Transform[] spawnPoints;   // positions outside your shop door
    [SerializeField] private GameObject customerPrefab; // your NPC prefab

    [Header("Spawn Settings")]
    [SerializeField] private float spawnIntervalMin = 5f;
    [SerializeField] private float spawnIntervalMax = 12f;

    private Coroutine spawnRoutine;

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
        if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        GameObject customer = Instantiate(customerPrefab, spawnPos, spawnPoint.rotation);

        CustomerAI ai = customer.GetComponent<CustomerAI>();
        ai.sidewalkPoints = sidewalkPoints;
    }


}
