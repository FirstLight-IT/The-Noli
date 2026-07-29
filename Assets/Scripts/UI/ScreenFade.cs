using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ScreenFade : MonoBehaviour
{
    public static ScreenFade Instance { get; private set; }
    public static bool IsTransitioning { get; private set; }

    [Header("Timing")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.25f;
    [SerializeField, Min(0f)] private float holdDuration = 0.05f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.25f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one ScreenFade exists in the scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsTransitioning = false;
        }
    }

    public bool BeginTransition(Action actionAtFullFade)
    {
        if (IsTransitioning || !isActiveAndEnabled)
        {
            return false;
        }

        StartCoroutine(FadeOutAndIn(actionAtFullFade));
        return true;
    }

    private IEnumerator FadeOutAndIn(Action actionAtFullFade)
    {
        IsTransitioning = true;
        canvasGroup.blocksRaycasts = true;

        yield return FadeTo(1f, fadeOutDuration);

        actionAtFullFade?.Invoke();

        if (holdDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(holdDuration);
        }

        yield return FadeTo(0f, fadeInDuration);

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        IsTransitioning = false;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
}
