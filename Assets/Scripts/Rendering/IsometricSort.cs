using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class IsometricSort : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private int orderOffset;
    [SerializeField, Min(1)] private int unitsPerLayer = 10;
    [SerializeField] private float sortPointYOffset;

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        ApplySortingOrder();
    }

    private void OnEnable() => ApplySortingOrder();

    private void LateUpdate() => ApplySortingOrder();

    private void OnValidate() => ApplySortingOrder();

    private void ApplySortingOrder()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float sortY = transform.position.y + sortPointYOffset;
        spriteRenderer.sortingOrder = orderOffset - Mathf.RoundToInt(sortY * unitsPerLayer);
    }
}
