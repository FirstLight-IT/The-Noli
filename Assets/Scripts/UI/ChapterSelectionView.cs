using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ChapterSelectionView : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text chapterLabelText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text primaryButtonText;

    [Header("Button")]
    [SerializeField] private Button primaryButton;

    public bool TryValidate(out string error)
    {
        if (chapterLabelText == null || titleText == null || statusText == null ||
            primaryButtonText == null || primaryButton == null)
        {
            error = $"{name} has unassigned chapter-selection UI references.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void Bind(
        ChapterDataSO chapter,
        string status,
        string actionLabel,
        bool actionAvailable,
        Action action)
    {
        chapterLabelText.SetText(chapter != null ? chapter.ChapterLabel : "Chapter");
        titleText.SetText(chapter != null ? chapter.Title : "Unavailable");
        statusText.SetText(status ?? string.Empty);
        primaryButtonText.SetText(actionLabel ?? string.Empty);
        primaryButton.interactable = actionAvailable;
        primaryButton.onClick.RemoveAllListeners();

        if (actionAvailable)
            primaryButton.onClick.AddListener(() => action?.Invoke());
    }

    public void SetInteractable(bool interactable)
    {
        primaryButton.interactable = interactable && primaryButton.interactable;
    }
}
