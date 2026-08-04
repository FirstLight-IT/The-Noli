using UnityEngine;

[CreateAssetMenu(fileName = "Unlock Notification Settings", menuName = "The Noli/UI/Unlock Notification Settings")]
public sealed class UnlockNotificationSettings : ScriptableObject
{
    [Header("Colors")]
    [SerializeField] private Color backgroundColor = new(0.08f, 0.065f, 0.05f, 0.96f);
    [SerializeField] private Color accentColor = new(0.78f, 0.58f, 0.25f, 1f);
    [SerializeField] private Color categoryTextColor = new(0.86f, 0.67f, 0.34f, 1f);
    [SerializeField] private Color nameTextColor = Color.white;

    [Header("Position and Size")]
    [SerializeField] private Vector2 cardSize = new(360f, 92f);
    [SerializeField] private Vector2 visiblePosition = new(32f, 32f);
    [SerializeField] private Vector2 hiddenPosition = new(-390f, 32f);

    [Header("Timing")]
    [Min(0.01f)] [SerializeField] private float slideInDuration = 0.28f;
    [Min(0f)] [SerializeField] private float visibleDuration = 2.6f;
    [Min(0.01f)] [SerializeField] private float slideOutDuration = 0.24f;

    public Color BackgroundColor => backgroundColor;
    public Color AccentColor => accentColor;
    public Color CategoryTextColor => categoryTextColor;
    public Color NameTextColor => nameTextColor;
    public Vector2 CardSize => cardSize;
    public Vector2 VisiblePosition => visiblePosition;
    public Vector2 HiddenPosition => hiddenPosition;
    public float SlideInDuration => slideInDuration;
    public float VisibleDuration => visibleDuration;
    public float SlideOutDuration => slideOutDuration;
}
