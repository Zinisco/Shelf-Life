using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CustomerAI : MonoBehaviour
{
    private NavMeshAgent agent;

    public enum CustomerState
    {
        WalkingSidewalk,
        GoingToEntry,
        InStore,
        ReturningBook,   
        LeavingStore
    }

    [Header("Behavior Settings")]
    public Transform[] sidewalkPoints; // assigned from spawner
    public Transform storeEntryPoint;  // assign in inspector
    [SerializeField] private float storeEntryChance = 0.3f;
    [SerializeField] private float storeStayMin = 8f;
    [SerializeField] private float storeStayMax = 20f;
    [SerializeField] private Transform handAnchor;
    private List<CustomerTarget> targetPool = new List<CustomerTarget>();

    private Book heldBook;

    // --- NEW: cooldown to avoid re-picking the same book
    private Book lastInteractedBook;
    private float lastInteractedUntil = 0f;
    [SerializeField] private float bookCooldown = 10f; // seconds to ignore same book

    private int currentPoint = 0;
    private float wanderCooldown = 0f;
    private StoreSignController storeSign;
    private CustomerState state = CustomerState.WalkingSidewalk;
    private float storeTimer;
    private bool isReturningBook = false;

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
            case CustomerState.WalkingSidewalk: HandleSidewalk(); break;
            case CustomerState.GoingToEntry: HandleGoingToEntry(); break;
            case CustomerState.InStore: HandleInStore(); break;
            case CustomerState.ReturningBook: HandleReturningBook(); break; 
            case CustomerState.LeavingStore: HandleLeavingStore(); break;
        }
    }

    void HandleReturningBook()
    {
        // Do nothing — coroutine drives this state
        // Just here so Update doesn’t fall through to LeavingStore too early
    }



    // --- State Handlers ---
    void HandleSidewalk()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            SidewalkPoint point = sidewalkPoints[currentPoint].GetComponent<SidewalkPoint>();

            // If this is a despawn point, remove customer
            if (point != null && point.isDespawnPoint)
            {
                Debug.Log($"{gameObject.name} reached despawn point, removing.");
                Destroy(gameObject);
                return;
            }

            // Otherwise continue sidewalk logic
            TryEnterStore();
            if (state == CustomerState.WalkingSidewalk)
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
            // After leaving store, go find a despawn sidewalk point
            SidewalkPoint[] points = FindObjectsOfType<SidewalkPoint>();
            foreach (var p in points)
            {
                if (p.isDespawnPoint)
                {
                    agent.SetDestination(p.transform.position);
                    state = CustomerState.WalkingSidewalk;
                    Debug.Log($"{gameObject.name} leaving store and heading to despawn point {p.name}");
                    return;
                }
            }

            // Fallback: loop sidewalk if no despawn point found
            GoToRandomSidewalkPoint(excludeEntry: true);
        }
    }

    // --- Store Logic ---
    void WanderInStore()
    {
        if (agent == null) return;

        // Gather all targets
        CustomerTarget[] allTargets = FindObjectsOfType<CustomerTarget>();
        if (allTargets.Length == 0) return;

        // --- Weighted random selection ---
        List<CustomerTarget> weighted = new List<CustomerTarget>();
        foreach (var t in allTargets)
        {
            for (int i = 0; i < Mathf.Max(1, t.weight); i++)
                weighted.Add(t);
        }

        CustomerTarget target = weighted[Random.Range(0, weighted.Count)];

        // --- Candidate destination ---
        Vector3 candidate;
        if (target.useFront)
            candidate = target.GetRandomFrontPoint(agent);
        else
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized * Random.Range(0.5f, 1.5f);
            candidate = target.transform.position + new Vector3(randomDir.x, 0f, randomDir.y);
        }

        // --- Set destination ---
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            wanderCooldown = target.lingerTime + Random.Range(1f, 3f);

            StartCoroutine(LookAtTargetWhenArrived(target.transform));

            Debug.Log($"{gameObject.name} wandering near {target.name} (weight={target.weight}) at {hit.position}");
        }
        else
        {
            // fallback
            Vector3 fallback = StoreArea.Instance?.GetRandomNavMeshPoint() ?? transform.position;
            agent.SetDestination(fallback);
            wanderCooldown = Random.Range(2f, 5f);
            Debug.LogWarning($"[CustomerAI] Failed NavMesh at {candidate} for {target.name}");
        }
    }


    // --- Sidewalk Logic ---
    void GoToNextSidewalkPoint() 
    { 
        if (sidewalkPoints == null || sidewalkPoints.Length == 0) 
            return; 
        if (agent.isOnNavMesh) 
        { 
            agent.SetDestination(sidewalkPoints[currentPoint].position); 
            Debug.Log($"{gameObject.name} walking to sidewalk point {currentPoint}"); 
            currentPoint = (currentPoint + 1) % sidewalkPoints.Length; 
        } 
    } 
    void GoToRandomSidewalkPoint(bool excludeEntry = false) 
    { 
        if (sidewalkPoints == null || sidewalkPoints.Length == 0)
            return; 
        int index = Random.Range(0, sidewalkPoints.Length); 

        if (excludeEntry) 
        { 
            int safety = 0; 
            while (sidewalkPoints[index].GetComponent<SidewalkPoint>()?.canEnterStore == true && safety < 10) 
            { 
                index = Random.Range(0, sidewalkPoints.Length); safety++; 
            } 
        
        } 
        agent.SetDestination(sidewalkPoints[index].position); 
        currentPoint = (index + 1) % sidewalkPoints.Length; 
    }

    void TryEnterStore()
    {
        int lastIndex = (currentPoint - 1 + sidewalkPoints.Length) % sidewalkPoints.Length;
        SidewalkPoint point = sidewalkPoints[lastIndex].GetComponent<SidewalkPoint>();

        if (point == null) return;

        Debug.Log($"{gameObject.name} at {point.name} | canEnter={point.canEnterStore} | storeOpen={storeSign.IsStoreOpen()}");

        if (point.canEnterStore && storeSign.IsStoreOpen())
            GoToEntryPoint();
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
        if (heldBook != null && !isReturningBook)
        {
            Debug.Log($"{gameObject.name} is holding {heldBook.name}, going to return it before exiting.");
            isReturningBook = true;
            state = CustomerState.ReturningBook;
            StartCoroutine(ReturnBookAndExit(heldBook));
        }
        else
        {
            GoToExit();
        }
    }



    private IEnumerator ReturnBookAndExit(Book book)
    {
        if (book == null) { GoToExit(); yield break; }

        // Walk in front of the book's remembered origin
        BookInfo info = book.GetComponent<BookInfo>();
        if (info != null && info.lastOrigin != null)
        {
            Vector3 worldPos = info.lastOrigin.parent != null
                ? info.lastOrigin.parent.TransformPoint(info.lastOrigin.localPos)
                : book.transform.position;

            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
                    yield return null;
            }
        }

        // Look briefly
        float lookTime = 1.5f;
        while (lookTime > 0f)
        {
            if (book == null) break;
            Vector3 lookPos = book.transform.position;
            lookPos.y = transform.position.y;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation((lookPos - transform.position).normalized),
                Time.deltaTime * 3f
            );
            lookTime -= Time.deltaTime;
            yield return null;
        }

        // Restore precisely where it came from
        if (info != null) info.RestoreOrigin();
        book.PutBack(book.transform.position, book.transform.rotation);

        Debug.Log($"{gameObject.name} returned {info?.title ?? book.name}");

        heldBook = null;
        isReturningBook = false;

        // Move on
        GoToExit();
    }



    private void GoToExit()
    {
        if (heldBook != null)
        {
            Debug.LogWarning($"{gameObject.name} still holding {heldBook.name} — forcing return now!");
            var info = heldBook.GetComponent<BookInfo>();
            if (info != null) info.RestoreOrigin();
            heldBook.PutBack(heldBook.transform.position, heldBook.transform.rotation);
            heldBook = null;
        }

        GameObject exit = GameObject.FindWithTag("StoreExit");
        if (exit != null)
        {
            agent.SetDestination(exit.transform.position);
            state = CustomerState.LeavingStore;
            StartCoroutine(FaceExit(exit.transform));
        }
    }


    private IEnumerator FaceExit(Transform exit)
    {
        if (exit == null) yield break;

        // Smoothly rotate until the agent starts moving
        float timer = 1.5f; // seconds max to align
        while (timer > 0f && agent != null && agent.velocity.sqrMagnitude < 0.1f)
        {
            Vector3 lookPos = exit.position;
            lookPos.y = transform.position.y;
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

    private IEnumerator LookAtTargetWhenArrived(Transform target)
    {
        if (target == null) yield break;

        Book book = FindNearestBook(target);
        if (book == null || book.isTaken) yield break;
       

        // Walk to a point in front of the book
        Vector3 forwardDir = -book.transform.forward;
        Vector3 frontPoint = book.transform.position + forwardDir * 0.6f;

        if (NavMesh.SamplePosition(frontPoint, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            while (agent.pathPending || agent.remainingDistance > (agent.stoppingDistance + 0.1f))
            {
                if (book == null) yield break;
                yield return null;
            }
        }

        // Face the target object itself
        Vector3 lookPos = target.position;
        lookPos.y = transform.position.y;
        Quaternion lookRot = Quaternion.LookRotation((lookPos - transform.position).normalized);
        transform.rotation = lookRot;

        float timer = wanderCooldown;

        // --- PICKUP ---
        var info = book.GetComponent<BookInfo>();
        if (heldBook == null && book != null && !book.isTaken)
        {
            if (info != null)
            {
                Vector3 lookBook = book.transform.position;
                lookBook.y = transform.position.y;
                transform.rotation = Quaternion.LookRotation((lookBook - transform.position).normalized);

                info.RememberOrigin();
                book.PickUp(handAnchor);
                heldBook = book;

                lastInteractedBook = book;
                lastInteractedUntil = Time.time + bookCooldown;

                agent.ResetPath();
                Debug.Log($"{gameObject.name} picked up {info.title}");
            }
        }

        // --- LINGER ---
        while (timer > 0f)
        {
            if (book == null) yield break;
            Vector3 lookBook = book.transform.position;
            lookBook.y = transform.position.y;
            Vector3 dir = (lookBook - transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 3f);
            }
            timer -= Time.deltaTime;
            yield return null;
        }

        // --- RETURN BOOK BEFORE MOVING ON ---
        // Instead of always returning, make it random or timed
        if (heldBook != null)
        {
            bool returnNow = Random.value < 0.5f; // 50% chance to put back right away

            if (returnNow)
            {
                BookInfo heldInfo = heldBook.GetComponent<BookInfo>();
                if (heldInfo != null)
                {
                    heldInfo.RestoreOrigin();
                    heldBook.PutBack(heldBook.transform.position, heldBook.transform.rotation);
                    Debug.Log($"{gameObject.name} returned {heldInfo.title} immediately");
                }
                heldBook = null;
            }
            else
            {
                // Keep holding book, customer will carry it until LeaveStore()
                Debug.Log($"{gameObject.name} kept {heldBook.GetComponent<BookInfo>()?.title}");
            }

            wanderCooldown = 3f;
        }
    }

    private GameObject GetTopmostBook(GameObject baseBook)
    {
        GameObject current = baseBook;
        float yOffset = 0.12f; // match BookStackRoot thickness
        string baseTitle = baseBook.GetComponent<BookInfo>()?.title;
        if (string.IsNullOrEmpty(baseTitle)) return current;

        // climb up the stack until no higher book of the same title is found
        for (int i = 0; i < 3; i++) // stack limit = 4, so 3 steps max
        {
            Vector3 checkPos = current.transform.position + Vector3.up * yOffset;
            Collider[] hits = Physics.OverlapSphere(checkPos, 0.05f, LayerMask.GetMask("Book"));
            bool found = false;

            foreach (var hit in hits)
            {
                if (hit.gameObject == current) continue;
                if (hit.GetComponent<BookInfo>()?.title == baseTitle)
                {
                    current = hit.gameObject;
                    found = true;
                    break;
                }
            }

            if (!found) break;
        }

        return current;
    }


    private Book FindNearestBook(Transform target)
    {
        if (target == null) return null;

        float bestDist = Mathf.Infinity;
        Book bestBook = null;

        // search colliders in range
        Collider[] hits = Physics.OverlapSphere(target.position, 1.8f, LayerMask.GetMask("Book"));
        foreach (Collider col in hits)
        {
            if (col == null) continue;
            Book b = col.GetComponent<Book>();
            if (b == null || b.isTaken) continue;

            // enforce top-of-stack
            GameObject candidate = GetTopmostBook(b.gameObject);
            Book topBook = candidate.GetComponent<Book>();
            if (topBook == null || topBook.isTaken) continue;

            // skip cooldown
            if (topBook == lastInteractedBook && Time.time < lastInteractedUntil)
                continue;

            float d = Vector3.Distance(target.position, topBook.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                bestBook = topBook;
            }
        }

        return bestBook;
    }
}
