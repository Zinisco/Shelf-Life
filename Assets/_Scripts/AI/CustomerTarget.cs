using UnityEngine;

public class CustomerTarget : MonoBehaviour
{
    [Tooltip("How long customers linger here before picking another target.")]
    public float lingerTime = 10f;

    [Header("Optional Facing Settings")]
    [Tooltip("If true, customers will only stand in front of this target.")]
    public bool useFront = false;

    [Tooltip("If true, use -Z as the 'front' instead of +Z.")]
    public bool invertForward = false;

    [Tooltip("How far in front customers should stand.")]
    public float frontDistance = 1.2f;

    [Tooltip("How wide customers can spread out while in front.")]
    public float lateralSpread = 0.8f;

    public Vector3 GetRandomFrontPoint()
    {
        Vector3 forwardDir = invertForward ? -transform.forward : transform.forward;
        Vector3 basePoint = transform.position + forwardDir * frontDistance;

        float offset = Random.Range(-lateralSpread, lateralSpread);
        Vector3 sideways = transform.right * offset;

        return basePoint + sideways;
    }

}
