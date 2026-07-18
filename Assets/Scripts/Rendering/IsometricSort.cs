using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class IsometricSort : MonoBehaviour, IIsometricSortable
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private int orderOffset;
    [SerializeField, Min(1)] private int unitsPerLayer = 10;
    [SerializeField] private float sortPointYOffset;

    public MonoBehaviour SortBehaviour => this;
    public SpriteRenderer SortRenderer => spriteRenderer;
    public Vector2 SortAnchor => (Vector2)transform.position + Vector2.up * sortPointYOffset;
    public int NaturalSortOrder => orderOffset - Mathf.RoundToInt(SortAnchor.y * unitsPerLayer);
    public bool DefinesSortBoundary => false;

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        IsometricSortingSystem.SortAll(true);
    }

    private void OnEnable() => IsometricSortingSystem.Register(this);

    private void OnDisable() => IsometricSortingSystem.Unregister(this);

    private void LateUpdate() => IsometricSortingSystem.SortAll();

    private void OnValidate() => IsometricSortingSystem.SortAll(true);

    public bool TryGetOtherInFront(Vector2 otherAnchor, out bool otherIsInFront)
    {
        otherIsInFront = otherAnchor.y < SortAnchor.y;
        return false;
    }
}
