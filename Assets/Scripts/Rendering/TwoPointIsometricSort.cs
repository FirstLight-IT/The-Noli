using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class TwoPointIsometricSort : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform referenceTransform;
    [SerializeField] private SpriteRenderer referenceRenderer;
    [SerializeField] private Vector2 lineStart = new(-1f, 0f);
    [SerializeField] private Vector2 lineEnd = new(1f, 0f);
    [SerializeField] private bool negativeSideBehind = true;
    [SerializeField, Min(1)] private int orderDifference = 1;

    private void OnEnable() => ApplySortingOrder();

    private void LateUpdate() => ApplySortingOrder();

    private void OnValidate() => ApplySortingOrder();

    private void ApplySortingOrder()
    {
        if (spriteRenderer == null || referenceTransform == null || referenceRenderer == null)
        {
            return;
        }

        Vector2 worldStart = transform.TransformPoint(lineStart);
        Vector2 worldEnd = transform.TransformPoint(lineEnd);
        Vector2 lineDirection = worldEnd - worldStart;
        Vector2 referenceDirection = (Vector2)referenceTransform.position - worldStart;
        float side = lineDirection.x * referenceDirection.y -
                     lineDirection.y * referenceDirection.x;

        bool renderBehind = negativeSideBehind ? side < 0f : side > 0f;
        spriteRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
        spriteRenderer.sortingOrder = referenceRenderer.sortingOrder +
                                      (renderBehind ? -orderDifference : orderDifference);
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
