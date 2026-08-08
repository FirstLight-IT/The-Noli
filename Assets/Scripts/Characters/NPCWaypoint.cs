using UnityEngine;

public class NPCWaypoint : MonoBehaviour
{
    [SerializeField] private NPCWaypoint[] neighbours = new NPCWaypoint[0];

    [Header("Patrol Wait")]
    [SerializeField] private bool overrideWaitTime;
    [SerializeField, Min(0f)] private float waitTime;

    public NPCWaypoint[] Neighbours => neighbours;

    public float GetWaitTime(float defaultWaitTime)
    {
        return overrideWaitTime ? waitTime : defaultWaitTime;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);

        if (neighbours == null)
            return;

        NPCIsometricGrid grid = GetComponentInParent<NPCIsometricGrid>();
        foreach (NPCWaypoint neighbour in neighbours)
        {
            if (neighbour != null)
            {
                Gizmos.color = grid == null || grid.IsAxisAligned(transform.position, neighbour.transform.position)
                    ? new Color(0.2f, 0.85f, 1f, 0.6f)
                    : new Color(1f, 0.3f, 0.15f, 0.9f);
                Gizmos.DrawLine(transform.position, neighbour.transform.position);
            }
        }
    }
}
