using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class CustomerAI : MonoBehaviour
{
    private NavMeshAgent agent;

    public enum CustomerState
    {
        WalkingSidewalk,
        GoingToEntry,
        InStore,
        LeavingStore
    }

    [Header("Behavior Settings")]
    public Transform[] sidewalkPoints; // assigned from spawner
    public Transform storeEntryPoint;  // assign in inspector
    [SerializeField] private float storeEntryChance = 0.3f;
    [SerializeField] private float storeStayMin = 8f;
    [SerializeField] private float storeStayMax = 20f;

    private int currentPoint = 0;
    private float wanderCooldown = 0f;
    private StoreSignController storeSign;
    private CustomerState state = CustomerState.WalkingSidewalk;
    private float storeTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.avoidancePriority = Random.Range(20, 80);
        storeSign = FindObjectOfType<StoreSignController>();

        if (sidewalkPoints != null && sidewalkPoints.Length > 0)
            GoToNextSidewalkPoint();
    }

    void Update()
    {
        switch (state)
        {
            case CustomerState.WalkingSidewalk:
                HandleSidewalk();
                break;

            case CustomerState.GoingToEntry:
                HandleGoingToEntry();
                break;

            case CustomerState.InStore:
                HandleInStore();
                break;

            case CustomerState.LeavingStore:
                HandleLeavingStore();
                break;
        }
    }

    // --- State Handlers ---

    void HandleSidewalk()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            TryEnterStore();
            if (state == CustomerState.WalkingSidewalk) // still sidewalk? then keep looping
                GoToNextSidewalkPoint();
        }
    }

    void HandleGoingToEntry()
    {
        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance &&
            agent.pathStatus == NavMeshPathStatus.PathComplete)
        {
            state = CustomerState.InStore;
            storeTimer = Random.Range(storeStayMin, storeStayMax);
            WanderInStore();
            Debug.Log($"{gameObject.name} successfully entered store at {transform.position}");
        }
    }

    void HandleInStore()
    {
        storeTimer -= Time.deltaTime;
        wanderCooldown -= Time.deltaTime;

        if (!agent.pathPending && agent.remainingDistance < 0.5f && wanderCooldown <= 0f)
        {
            WanderInStore();
        }

        if (storeTimer <= 0f)
        {
            LeaveStore();
        }
    }

    void HandleLeavingStore()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            state = CustomerState.WalkingSidewalk;
            Debug.Log($"{gameObject.name} exited store and rejoined sidewalk.");

            // Force them to a *non-entry* sidewalk point to avoid re-trigger
            GoToRandomSidewalkPoint(excludeEntry: true);
        }
    }

    // --- Store Logic ---

    void WanderInStore()
    {
        if (agent == null) return;

        CustomerTarget[] targets = FindObjectsOfType<CustomerTarget>();
        if (targets.Length > 0)
        {
            CustomerTarget target = targets[Random.Range(0, targets.Length)];
            Vector3 candidate;

            if (target.useFront)
            {
                candidate = target.GetRandomFrontPoint();
            }
            else
            {
                // Tables and generic targets: any angle is fine
                Vector2 randomDir = Random.insideUnitCircle.normalized * Random.Range(0.5f, 1.5f);
                candidate = target.transform.position + new Vector3(randomDir.x, 0f, randomDir.y);
            }

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                wanderCooldown = target.lingerTime + Random.Range(1f, 3f);

                StartCoroutine(LookAtTargetWhenArrived(target.transform));

                Debug.Log($"{gameObject.name} wandering near {target.name} at {hit.position}");
                return;
            }
        }

        // fallback
        Vector3 fallback = StoreArea.Instance?.GetRandomNavMeshPoint() ?? transform.position;
        agent.SetDestination(fallback);
        wanderCooldown = Random.Range(2f, 5f);
    }




    void TryEnterStore()
    {
        int lastIndex = (currentPoint - 1 + sidewalkPoints.Length) % sidewalkPoints.Length;
        SidewalkPoint point = sidewalkPoints[lastIndex].GetComponent<SidewalkPoint>();

        if (point == null) return;

        Debug.Log($"{gameObject.name} at {point.name} | canEnter={point.canEnterStore} | storeOpen={storeSign.IsStoreOpen()}");

        if (point.canEnterStore && storeSign.IsStoreOpen())
        {
            GoToEntryPoint();
        }
    }

    void GoToEntryPoint()
    {
        if (storeEntryPoint == null || !agent.isOnNavMesh)
        {
            Debug.LogWarning($"{gameObject.name} has no storeEntryPoint or agent not on NavMesh!");
            return;
        }

        if (NavMesh.SamplePosition(storeEntryPoint.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            state = CustomerState.GoingToEntry;

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                Debug.LogWarning($"{gameObject.name} cannot find a path to store entry point!");
            else
                Debug.Log($"{gameObject.name} heading to store entry point at {hit.position}");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} storeEntryPoint is not on NavMesh!");
        }
    }

    void LeaveStore()
    {
        GameObject exit = GameObject.FindWithTag("StoreExit");
        if (exit != null)
        {
            agent.SetDestination(exit.transform.position);
            state = CustomerState.LeavingStore;
        }

        Debug.Log($"{gameObject.name} leaving store...");
    }

    // --- Sidewalk Logic ---

    void GoToNextSidewalkPoint()
    {
        if (sidewalkPoints == null || sidewalkPoints.Length == 0) return;

        if (agent.isOnNavMesh)
        {
            agent.SetDestination(sidewalkPoints[currentPoint].position);
            Debug.Log($"{gameObject.name} walking to sidewalk point {currentPoint}");
            currentPoint = (currentPoint + 1) % sidewalkPoints.Length;
        }
    }

    void GoToRandomSidewalkPoint(bool excludeEntry = false)
    {
        if (sidewalkPoints == null || sidewalkPoints.Length == 0) return;

        int index = Random.Range(0, sidewalkPoints.Length);
        if (excludeEntry)
        {
            int safety = 0;
            while (sidewalkPoints[index].GetComponent<SidewalkPoint>()?.canEnterStore == true && safety < 10)
            {
                index = Random.Range(0, sidewalkPoints.Length);
                safety++;
            }
        }

        agent.SetDestination(sidewalkPoints[index].position);
        currentPoint = (index + 1) % sidewalkPoints.Length;
    }

    private IEnumerator LookAtTargetWhenArrived(Transform target)
    {
        // Wait until we reach destination
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
            yield return null;

        // Face the target while lingering
        float timer = wanderCooldown;
        while (timer > 0f)
        {
            Vector3 lookPos = target.position;
            lookPos.y = transform.position.y; // keep rotation flat
            Vector3 dir = (lookPos - transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 3f);
            }

            timer -= Time.deltaTime;
            yield return null;
        }
    }

}
