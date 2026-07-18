using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class TwoPointIsometricSort : MonoBehaviour, IIsometricSortable
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Vector2 lineStart = new(-1f, 0f);
    [SerializeField] private Vector2 lineEnd = new(1f, 0f);
    [SerializeField] private bool negativeSideBehind = true;
    [SerializeField] private int orderOffset;

    public MonoBehaviour SortBehaviour => this;
    public SpriteRenderer SortRenderer => spriteRenderer;
    public Vector2 SortAnchor => (WorldLineStart + WorldLineEnd) * 0.5f;
    public int NaturalSortOrder => orderOffset - Mathf.RoundToInt(SortAnchor.y * 10f);
    public bool DefinesSortBoundary => true;

    private Vector2 WorldLineStart => transform.TransformPoint(lineStart);
    private Vector2 WorldLineEnd => transform.TransformPoint(lineEnd);

    private void OnEnable() => IsometricSortingSystem.Register(this);

    private void OnDisable() => IsometricSortingSystem.Unregister(this);

    private void LateUpdate() => IsometricSortingSystem.SortAll();

    private void OnValidate() => IsometricSortingSystem.SortAll(true);

    public bool TryGetOtherInFront(Vector2 otherAnchor, out bool otherIsInFront)
    {
        Vector2 worldStart = WorldLineStart;
        Vector2 worldEnd = WorldLineEnd;
        Vector2 lineDirection = worldEnd - worldStart;

        if (lineDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            otherIsInFront = false;
            return false;
        }

        Vector2 referenceDirection = otherAnchor - worldStart;

        // For the usual diagonal isometric boundary, the relevant span is the
        // actor's horizontal position over the object, not its perpendicular
        // projection onto the line. The latter incorrectly rejects actors that
        // are clearly above or below a steep/long sprite.
        if (Mathf.Abs(lineDirection.x) > Mathf.Epsilon)
        {
            float positionAlongX = referenceDirection.x / lineDirection.x;
            if (positionAlongX < 0f || positionAlongX > 1f)
            {
                otherIsInFront = false;
                return false;
            }
        }
        else
        {
            float positionAlongY = referenceDirection.y / lineDirection.y;
            if (positionAlongY < 0f || positionAlongY > 1f)
            {
                otherIsInFront = false;
                return false;
            }
        }

        float side = lineDirection.x * referenceDirection.y -
                     lineDirection.y * referenceDirection.x;

        otherIsInFront = negativeSideBehind ? side < 0f : side > 0f;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 worldStart = transform.TransformPoint(lineStart);
        Vector3 worldEnd = transform.TransformPoint(lineEnd);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(worldStart, worldEnd);
        Gizmos.DrawSphere(worldStart, 0.08f);
        Gizmos.DrawSphere(worldEnd, 0.08f);
    }
}
