using UnityEngine;

public class NPCIsometricGrid : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float spacing = 1f;
    [SerializeField, Range(1, 80)] private int gridRadius = 8;
    [SerializeField] private bool alwaysShowGrid = true;

    public float Spacing => spacing;

    public Vector3 SnapPosition(Vector3 worldPosition)
    {
        GetGridCoordinates(worldPosition, out float right, out float left);
        return GetWorldPosition(Mathf.Round(right), Mathf.Round(left), worldPosition.z);
    }

    public bool IsAxisAligned(Vector3 from, Vector3 to)
    {
        GetGridCoordinates(from, out float fromRight, out float fromLeft);
        GetGridCoordinates(to, out float toRight, out float toLeft);

        const float tolerance = 0.01f;
        return Mathf.Abs(fromRight - toRight) <= tolerance ||
               Mathf.Abs(fromLeft - toLeft) <= tolerance;
    }

    public NPCWaypoint[] GetChildWaypoints()
    {
        return GetComponentsInChildren<NPCWaypoint>(true);
    }

    private void GetGridCoordinates(Vector3 worldPosition, out float right, out float left)
    {
        Vector2 offset = worldPosition - transform.position;
        float radians = IsometricGeometry.GroundAngle * Mathf.Deg2Rad;
        float horizontal = Mathf.Cos(radians) * spacing;
        float vertical = Mathf.Sin(radians) * spacing;

        right = (offset.x / horizontal + offset.y / vertical) * 0.5f;
        left = (offset.y / vertical - offset.x / horizontal) * 0.5f;
    }

    private Vector3 GetWorldPosition(float right, float left, float z)
    {
        Vector2 offset = spacing *
                         (IsometricGeometry.RightAxis * right +
                          IsometricGeometry.LeftAxis * left);

        return new Vector3(
            transform.position.x + offset.x,
            transform.position.y + offset.y,
            z);
    }

    private void OnDrawGizmos()
    {
        if (!alwaysShowGrid)
            return;

        DrawGrid();
    }

    private void OnDrawGizmosSelected()
    {
        if (!alwaysShowGrid)
            DrawGrid();
    }

    private void DrawGrid()
    {
        Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.2f);

        for (int coordinate = -gridRadius; coordinate <= gridRadius; coordinate++)
        {
            Gizmos.DrawLine(
                GetWorldPosition(-gridRadius, coordinate, transform.position.z),
                GetWorldPosition(gridRadius, coordinate, transform.position.z));
            Gizmos.DrawLine(
                GetWorldPosition(coordinate, -gridRadius, transform.position.z),
                GetWorldPosition(coordinate, gridRadius, transform.position.z));
        }

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.12f);
    }
}
