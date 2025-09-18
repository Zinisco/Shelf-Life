using UnityEngine;

public class CrateDimensions : MonoBehaviour
{
    public Vector3 Size => GetComponent<Collider>().bounds.size;
}
