using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ChapterSelectionMenuController : MonoBehaviour
{
    private enum ChapterAction
    {
        None,
        Start,
        Continue,
        Replay
    }

    [Header("Menu")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button closeButton;

    [Header("Connections")]
    [SerializeField] private MainMenuController mainMenuController;
    [Tooltip("Assign Chapter 1, Chapter 2, then Chapter 3 in this exact order.")]
    [SerializeField] private ChapterDataSO[] chapters = new ChapterDataSO[3];
    [Tooltip("Assign the three matching Chapter Selection cards in the same order.")]
    [SerializeField] private ChapterSelectionView[] chapterViews = new ChapterSelectionView[3];

    private string pendingReplayChapterId = string.Empty;
    private Action backAction;
    private bool isBusy;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public bool ShowForSlot(int slotNumber, Action requestedBackAction, out string error)
    {
        if (!TryValidate(out error))
        {
            Debug.LogError(error, this);
            return false;
        }

        if (!SaveGameManager.TryLoadSlot(slotNumber, out error))
            return false;

        ShowLoadedSave(
            $"Save Slot {slotNumber} - Select Chapter",
            requestedBackAction);
        return true;
    }

    public bool ShowForCurrentClassroom(
        Action requestedBackAction,
        out string error)
    {
        if (!TryValidate(out error))
        {
            Debug.LogError(error, this);
            return false;
        }

        if (!SaveGameManager.IsUsingClassroomSave ||
            SaveGameManager.CurrentData == null)
        {
            error = "No classroom save is currently loaded.";
            return false;
        }

        ShowLoadedSave("Classroom Save - Select Chapter", requestedBackAction);
        error = string.Empty;
        return true;
    }

    private void ShowLoadedSave(string title, Action requestedBackAction)
    {
        isBusy = false;
        pendingReplayChapterId = string.Empty;
        backAction = requestedBackAction;
        closeButton.interactable = true;
        panelRoot.transform.SetAsLastSibling();
        panelRoot.SetActive(true);
        titleText.SetText(title);
        SetMessage(string.Empty);
        RefreshChapters();
    }

    public void Hide()
    {
        if (isBusy)
            return;

        pendingReplayChapterId = string.Empty;
        SetMessage(string.Empty);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        Action action = backAction;
        backAction = null;
        action?.Invoke();
    }

    private void RefreshChapters()
    {
        GameSaveData saveData = SaveGameManager.CurrentData;

        for (int index = 0; index < chapters.Length; index++)
        {
            ChapterDataSO definition = chapters[index];
            ChapterSaveData progress = saveData?.FindChapter(definition.ChapterId);
            ChapterAction action = GetAction(definition, progress);
            string status = GetStatus(definition, progress);
            string actionLabel = GetActionLabel(
                action,
                definition,
                progress);
            bool actionAvailable = action != ChapterAction.None && !isBusy;

            chapterViews[index].Bind(
                definition,
                status,
                actionLabel,
                actionAvailable,
                () => HandleChapterAction(definition, action));
        }
    }

    private void HandleChapterAction(ChapterDataSO chapter, ChapterAction action)
    {
        if (isBusy || chapter == null || action == ChapterAction.None)
            return;

        if (action == ChapterAction.Replay &&
            !string.Equals(
                pendingReplayChapterId,
                chapter.ChapterId,
                StringComparison.Ordinal))
        {
            pendingReplayChapterId = chapter.ChapterId;
            SetMessage(
                $"Replay {chapter.ChapterLabel}? Mission, position, world, and quiz progress " +
                "will restart. Journal unlocks and completion history will stay.");
            RefreshChapters();
            return;
        }

        BeginBusyState();
        bool started = action switch
        {
            ChapterAction.Continue => mainMenuController.TryContinueChapter(chapter),
            ChapterAction.Start => mainMenuController.TryStartChapter(chapter, false),
            ChapterAction.Replay => mainMenuController.TryStartChapter(chapter, true),
            _ => false
        };

        if (!started)
        {
            isBusy = false;
            closeButton.interactable = true;
            pendingReplayChapterId = string.Empty;
            SetMessage("The chapter could not be opened. Check the Console for details.");
            RefreshChapters();
        }
    }

    private void BeginBusyState()
    {
        isBusy = true;
        closeButton.interactable = false;

        foreach (ChapterSelectionView chapterView in chapterViews)
            chapterView.SetInteractable(false);
    }

    private ChapterAction GetAction(ChapterDataSO definition, ChapterSaveData progress)
    {
        if (definition == null || !definition.ContentAvailable || progress?.isUnlocked != true)
            return ChapterAction.None;

        if (string.Equals(progress.state, "InProgress", StringComparison.Ordinal))
            return ChapterAction.Continue;

        if (progress.completedEver ||
            string.Equals(progress.state, "Completed", StringComparison.Ordinal))
        {
            return ChapterAction.Replay;
        }

        return string.Equals(progress.state, "NotStarted", StringComparison.Ordinal)
            ? ChapterAction.Start
            : ChapterAction.None;
    }

    private static string GetStatus(ChapterDataSO definition, ChapterSaveData progress)
    {
        if (progress?.isUnlocked != true)
            return "Locked";

        if (!definition.ContentAvailable)
            return "Unlocked — Content Coming Soon";

        if (progress.completedEver ||
            string.Equals(progress.state, "Completed", StringComparison.Ordinal))
        {
            int completions = Math.Max(1, progress.completionCount);
            return completions == 1 ? "Completed once" : $"Completed {completions} times";
        }

        return string.Equals(progress.state, "InProgress", StringComparison.Ordinal)
            ? "In Progress"
            : "Not Started";
    }

    private string GetActionLabel(
        ChapterAction action,
        ChapterDataSO definition,
        ChapterSaveData progress)
    {
        return action switch
        {
            ChapterAction.Start => "Start Chapter",
            ChapterAction.Continue => "Continue",
            ChapterAction.Replay when string.Equals(
                pendingReplayChapterId,
                definition.ChapterId,
                StringComparison.Ordinal) => "Confirm Replay",
            ChapterAction.Replay => "Replay",
            _ when progress?.isUnlocked != true => "Locked",
            _ when !definition.ContentAvailable => "Coming Soon",
            _ => "Unavailable"
        };
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
            messageText.SetText(message ?? string.Empty);
    }

    private bool TryValidate(out string error)
    {
        if (panelRoot == null || titleText == null || closeButton == null ||
            mainMenuController == null || chapters == null || chapterViews == null ||
            chapters.Length == 0 || chapters.Length != chapterViews.Length)
        {
            error = "Chapter Selection Menu Controller has unassigned or mismatched UI references.";
            return false;
        }

        for (int index = 0; index < chapters.Length; index++)
        {
            if (chapters[index] == null)
            {
                error = $"Chapter definition {index + 1} is not assigned.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(chapters[index].ChapterId))
            {
                error = $"{chapters[index].name} has no Chapter ID.";
                return false;
            }

            if (chapterViews[index] == null)
            {
                error = $"Chapter Selection View {index + 1} is not assigned.";
                return false;
            }

            if (!chapterViews[index].TryValidate(out error))
                return false;
        }

        error = string.Empty;
        return true;
    }
}
