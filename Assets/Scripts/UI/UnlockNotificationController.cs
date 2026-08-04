using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates and queues the lower-left journal unlock notifications.</summary>
public sealed class UnlockNotificationController : MonoBehaviour
{
    private struct Notification
    {
        public string Category;
        public string Name;
        public Sprite Icon;
    }

    private static UnlockNotificationController instance;
    private readonly Queue<Notification> pending = new();
    private CanvasGroup canvasGroup;
    private RectTransform card;
    private Image icon;
    private TMP_Text categoryText;
    private TMP_Text nameText;
    private Coroutine displayRoutine;
    private UnlockNotificationSettings settings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new("Unlock Notification");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<UnlockNotificationController>();
        instance.BuildUI();
    }

    public static void ShowCharacter(string characterName, Sprite portrait)
    {
        Enqueue("CHARACTER UNLOCKED", characterName, portrait);
    }

    public static void ShowArtifact(string artifactName, Sprite artifactImage)
    {
        Enqueue("ARTIFACT UNLOCKED", artifactName, artifactImage);
    }

    private static void Enqueue(string category, string entryName, Sprite entryIcon)
    {
        EnsureInstance();
        instance.pending.Enqueue(new Notification
        {
            Category = category,
            Name = entryName,
            Icon = entryIcon
        });

        if (instance.displayRoutine == null)
            instance.displayRoutine = instance.StartCoroutine(instance.DisplayQueue());
    }

    private void BuildUI()
    {
        settings = Resources.Load<UnlockNotificationSettings>("Unlock Notification Settings");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<UnlockNotificationSettings>();
            Debug.LogWarning("Unlock Notification Settings could not be found in Resources. Using defaults.", this);
        }

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject cardObject = new("Unlock Card", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        cardObject.transform.SetParent(transform, false);
        card = cardObject.GetComponent<RectTransform>();
        card.anchorMin = new Vector2(0f, 0f);
        card.anchorMax = new Vector2(0f, 0f);
        card.pivot = new Vector2(0f, 0f);
        card.anchoredPosition = settings.HiddenPosition;
        card.sizeDelta = settings.CardSize;
        cardObject.GetComponent<Image>().color = settings.BackgroundColor;
        canvasGroup = cardObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        GameObject accent = CreateImage("Accent", card, settings.AccentColor);
        SetRect(accent.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(5f, 92f));

        GameObject iconObject = CreateImage("Icon", card, Color.white);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        SetRect(iconRect, new Vector2(17f, 14f), new Vector2(64f, 64f));
        icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;

        categoryText = CreateText("Category", card, 15, settings.CategoryTextColor);
        SetRect(categoryText.rectTransform, new Vector2(96f, 48f), new Vector2(245f, 25f));
        categoryText.fontStyle = FontStyles.Bold;

        nameText = CreateText("Entry Name", card, 22, settings.NameTextColor);
        SetRect(nameText.rectTransform, new Vector2(96f, 16f), new Vector2(245f, 34f));
        nameText.fontStyle = FontStyles.Bold;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 14f;
        nameText.fontSizeMax = 22f;
    }

    private IEnumerator DisplayQueue()
    {
        while (pending.Count > 0)
        {
            Notification notification = pending.Dequeue();
            categoryText.SetText(notification.Category);
            nameText.SetText(notification.Name);
            icon.sprite = notification.Icon;
            icon.enabled = notification.Icon != null;

            yield return Animate(settings.HiddenPosition, settings.VisiblePosition, 0f, 1f, settings.SlideInDuration);
            yield return new WaitForSecondsRealtime(settings.VisibleDuration);
            yield return Animate(settings.VisiblePosition, settings.HiddenPosition, 1f, 0f, settings.SlideOutDuration);
        }

        displayRoutine = null;
    }

    private IEnumerator Animate(Vector2 from, Vector2 to, float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            card.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            yield return null;
        }
        card.anchoredPosition = to;
        canvasGroup.alpha = toAlpha;
    }

    private static GameObject CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject result = new(objectName, typeof(RectTransform), typeof(Image));
        result.transform.SetParent(parent, false);
        result.GetComponent<Image>().color = color;
        return result;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, float size, Color color)
    {
        GameObject result = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        result.transform.SetParent(parent, false);
        TMP_Text text = result.GetComponent<TMP_Text>();
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
