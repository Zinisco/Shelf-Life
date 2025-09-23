using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshObstacle))]
public class MovableNavMeshObstacle : MonoBehaviour
{
    private NavMeshObstacle obstacle;

    void Awake()
    {
        obstacle = GetComponent<NavMeshObstacle>();
        obstacle.carving = true;
    }

    public void OnPickup()
    {
        // While moving, disable so agents don’t avoid phantom shelves
        obstacle.enabled = false;
    }

    public void OnPlace()
    {
        // Re-enable when placed so NavMesh carves around it again
        obstacle.enabled = true;
    }
}
