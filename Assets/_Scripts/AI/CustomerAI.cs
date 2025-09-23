using UnityEngine;
using UnityEngine.AI;

public class CustomerAI : MonoBehaviour
{
    private NavMeshAgent agent;

    [Header("Behavior Settings")]
    public Transform[] sidewalkPoints; // assigned from spawner
    [SerializeField] private float storeEntryChance = 0.3f;
    [SerializeField] private float storeStayMin = 8f;
    [SerializeField] private float storeStayMax = 20f;

    private int currentPoint = 0;
    private StoreSignController storeSign;
    private bool inStore = false;
    private bool leavingStore = false;
    private float storeTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        storeSign = FindObjectOfType<StoreSignController>();

        if (sidewalkPoints != null && sidewalkPoints.Length > 0)
            GoToNextSidewalkPoint();
    }

    void Update()
    {
        if (inStore)
        {
            storeTimer -= Time.deltaTime;
            if (storeTimer <= 0f)
            {
                LeaveStore();
            }
            return;
        }

        if (leavingStore)
        {
            // Once customer reaches the exit door, rejoin sidewalk loop
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                leavingStore = false;
                Debug.Log($"{gameObject.name} exited store and rejoined sidewalk.");
                GoToClosestSidewalkPoint();
            }
            return;
        }

        // Normal sidewalk walking
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            TryEnterStore();
            GoToNextSidewalkPoint();
        }
    }

    void GoToNextSidewalkPoint()
    {
        if (sidewalkPoints == null || sidewalkPoints.Length == 0) return;

        if (agent.isOnNavMesh)
        {
            agent.SetDestination(sidewalkPoints[currentPoint].position);
            currentPoint = (currentPoint + 1) % sidewalkPoints.Length;
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} is not on NavMesh!");
        }
    }


    void GoToClosestSidewalkPoint()
    {
        if (sidewalkPoints == null || sidewalkPoints.Length == 0) return;

        // Find the closest sidewalk waypoint after leaving the store
        Transform closest = sidewalkPoints[0];
        float minDist = Vector3.Distance(transform.position, closest.position);

        foreach (var point in sidewalkPoints)
        {
            float dist = Vector3.Distance(transform.position, point.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = point;
            }
        }

        agent.SetDestination(closest.position);
        // resume loop from this point
        for (int i = 0; i < sidewalkPoints.Length; i++)
        {
            if (sidewalkPoints[i] == closest)
            {
                currentPoint = (i + 1) % sidewalkPoints.Length;
                break;
            }
        }
    }

    void TryEnterStore()
    {
        SidewalkPoint point = sidewalkPoints[currentPoint].GetComponent<SidewalkPoint>();
        if (point != null && point.canEnterStore && storeSign.IsStoreOpen())
        {
            if (Random.value < storeEntryChance)
                EnterStore();
        }
    }

    void EnterStore()
    {
        GameObject[] spots = GameObject.FindGameObjectsWithTag("CustomerSpot");
        if (spots.Length == 0) return;

        inStore = true;
        storeTimer = Random.Range(storeStayMin, storeStayMax);

        Transform spot = spots[Random.Range(0, spots.Length)].transform;
        agent.SetDestination(spot.position);

        Debug.Log($"{gameObject.name} entered store.");
    }

    void LeaveStore()
    {
        GameObject exit = GameObject.FindWithTag("StoreExit");
        if (exit != null)
        {
            agent.SetDestination(exit.transform.position);
            leavingStore = true;
        }

        inStore = false;
        Debug.Log($"{gameObject.name} leaving store...");
    }
}
