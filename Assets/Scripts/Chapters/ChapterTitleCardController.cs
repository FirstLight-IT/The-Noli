using System;
using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
[ExecuteAlways]
public sealed class ChapterTitleCardController : MonoBehaviour
{
    [Header("Editor")]
    [Tooltip("Show the title card while editing its layout. This does not affect Play Mode.")]
    [SerializeField] private bool previewInEditor;

    [Header("Text")]
    [SerializeField] private TMP_Text chapterLabelText;
    [SerializeField] private TMP_Text chapterTitleText;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float displayDuration = 2.5f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.75f;
    [Tooltip("When the next opening element begins: 0 is the start of the fade, 1 is after the card is gone.")]
    [SerializeField, Range(0f, 1f)] private float bleedInStart = 0.45f;

    private CanvasGroup canvasGroup;
    private bool editorPreviewDirty;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            editorPreviewDirty = true;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            editorPreviewDirty = true;
    }

    private void Update()
    {
        if (Application.isPlaying || !editorPreviewDirty)
            return;

        editorPreviewDirty = false;
        ApplyEditorPreview();
    }

    public void Prepare(ChapterDataSO chapter)
    {
        if (chapter == null)
        {
            SetVisible(false);
            return;
        }

        if (chapterLabelText != null)
            chapterLabelText.SetText(chapter.ChapterLabel);

        if (chapterTitleText != null)
            chapterTitleText.SetText(chapter.Title);

        SetVisible(true);
    }

    public IEnumerator DisplayPreparedCard(Action onBleedIn)
    {
        if (!gameObject.activeInHierarchy)
        {
            onBleedIn?.Invoke();
            yield break;
        }

        if (displayDuration > 0f)
            yield return new WaitForSecondsRealtime(displayDuration);

        bool bleedInStarted = false;

        if (fadeOutDuration > 0f)
        {
            float elapsed = 0f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeOutDuration);
                canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);

                if (!bleedInStarted && progress >= bleedInStart)
                {
                    bleedInStarted = true;
                    onBleedIn?.Invoke();
                }

                yield return null;
            }
        }

        if (!bleedInStarted)
            onBleedIn?.Invoke();

        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = visible;
        gameObject.SetActive(visible);
    }

    private void ApplyEditorPreview()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            return;

        canvasGroup.alpha = previewInEditor ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
