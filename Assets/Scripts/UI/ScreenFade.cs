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

    [Header("Scene Entry")]
    [Tooltip("Start black and fade in when this scene opens.")]
    [SerializeField] private bool fadeInOnStart;
    [Tooltip("Time to keep the new scene covered while its first-frame setup finishes.")]
    [SerializeField, Min(0f)] private float fadeInDelay = 0.25f;
    [Tooltip("Fade duration used only when this scene first opens.")]
    [SerializeField, Min(0f)] private float sceneEntryFadeDuration = 1.5f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // Scene-local fades replace the outgoing scene's reference immediately.
        // This is necessary because the new scene can awaken before Unity finishes
        // destroying every object from the previous scene.
        Instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
        transform.SetAsLastSibling();
        canvasGroup.alpha = fadeInOnStart ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = fadeInOnStart;
    }

    private void Start()
    {
        if (fadeInOnStart)
            StartCoroutine(FadeInOnSceneStart());
    }

    private IEnumerator FadeInOnSceneStart()
    {
        IsTransitioning = true;

        // Guarantee that the incoming scene renders fully black at least once
        // before the alpha begins changing.
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();

        if (fadeInDelay > 0f)
            yield return new WaitForSecondsRealtime(fadeInDelay);

        yield return FadeTo(0f, sceneEntryFadeDuration);

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        IsTransitioning = false;
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
            float progress = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.SmoothStep(startAlpha, targetAlpha, progress);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
}
