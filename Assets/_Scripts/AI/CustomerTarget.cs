using UnityEngine;
using UnityEngine.AI;

public class CustomerTarget : MonoBehaviour
{
    [Tooltip("How long customers linger here before moving on.")]
    public float lingerTime = 10f;

    [Header("Target Weight")]
    [Tooltip("Relative chance this target gets chosen. Shelves can be higher.")]
    public int weight = 1;

    [Header("Facing Settings")]
    public bool useFront = false;
    public bool invertForward = false;
    public float frontDistance = 1.2f;
    public float lateralSpread = 0.8f;

    public Vector3 GetRandomFrontPoint(NavMeshAgent agent = null)
    {
        Vector3 forwardDir = invertForward ? -transform.forward : transform.forward;
        float clearance = (agent != null ? agent.radius * 2f : 0f);
        float randomPush = Random.Range(0.4f, 0.8f);
        Vector3 basePoint = transform.position + forwardDir * (frontDistance + clearance + randomPush);

        float offset = Random.Range(-lateralSpread, lateralSpread);
        return basePoint + transform.right * offset;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!useFront) return;

        Vector3 forwardDir = invertForward ? -transform.forward : transform.forward;
        float clearance = 0.5f; // editor-only default visualization
        Vector3 basePoint = transform.position + forwardDir * (frontDistance + clearance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, basePoint);

        Vector3 left = basePoint - transform.right * lateralSpread;
        Vector3 right = basePoint + transform.right * lateralSpread;

        Gizmos.DrawLine(left, right);
        Gizmos.DrawLine(left, transform.position);
        Gizmos.DrawLine(right, transform.position);

        Gizmos.DrawSphere(left, 0.05f);
        Gizmos.DrawSphere(right, 0.05f);
    }
#endif
}
