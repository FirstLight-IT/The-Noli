using UnityEngine;

public class NPCWaypoint : MonoBehaviour
{
    [SerializeField] private NPCWaypoint[] neighbours = new NPCWaypoint[0];

    public NPCWaypoint[] Neighbours => neighbours;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);

        if (neighbours == null)
            return;

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.45f);
        foreach (NPCWaypoint neighbour in neighbours)
        {
            if (neighbour != null)
                Gizmos.DrawLine(transform.position, neighbour.transform.position);
        }
    }
}
