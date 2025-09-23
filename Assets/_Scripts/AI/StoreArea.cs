using UnityEngine;
using UnityEngine.AI;

public class StoreArea : MonoBehaviour
{
    public static StoreArea Instance;

    [SerializeField] private Vector3 size = new Vector3(10f, 5f, 10f);

    void Awake()
    {
        Instance = this;
    }

    public Vector3 GetRandomNavMeshPoint()
    {
        Vector3 center = transform.position;

        for (int i = 0; i < 20; i++) // try up to 20 times
        {
            float x = Random.Range(center.x - size.x / 2f, center.x + size.x / 2f);
            float z = Random.Range(center.z - size.z / 2f, center.z + size.z / 2f);
            Vector3 candidate = new Vector3(x, center.y, z);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return hit.position; // always guaranteed to be on NavMesh
            }
        }

        Debug.LogWarning("StoreArea: Could not find NavMesh point inside store, using center.");
        return center;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, size);
    }
}
