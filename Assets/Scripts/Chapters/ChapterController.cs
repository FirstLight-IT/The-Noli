using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ChapterController : MonoBehaviour
{
    [Serializable]
    private sealed class ChapterContent
    {
        [SerializeField] private ChapterDataSO chapter;
        [SerializeField] private GameObject contentRoot;

        public ChapterDataSO Chapter => chapter;
        public GameObject ContentRoot => contentRoot;
    }

    public static ChapterController Instance { get; private set; }
    public static bool IsChapterOpening { get; private set; }

    private static string requestedChapterId;

    [Header("Chapter Selection")]
    [SerializeField] private ChapterDataSO defaultChapter;
    [Tooltip("Only the selected chapter's content root will be active. Shared Mansion objects should not be listed here.")]
    [SerializeField] private ChapterContent[] chapterContents = Array.Empty<ChapterContent>();

    [Header("Shared Opening UI")]
    [SerializeField] private bool showTitleCard;
    [SerializeField] private ChapterTitleCardController titleCard;
    [SerializeField] private NarrationController narrationController;

    private ChapterDataSO activeChapter;

    public ChapterDataSO ActiveChapter => activeChapter;

    public IEnumerable<ChapterDataSO> ConfiguredChapters
    {
        get
        {
            HashSet<ChapterDataSO> yielded = new();

            if (defaultChapter != null && yielded.Add(defaultChapter))
                yield return defaultChapter;

            foreach (ChapterContent content in chapterContents)
            {
                if (content?.Chapter != null && yielded.Add(content.Chapter))
                    yield return content.Chapter;
            }
        }
    }

    public static void RequestChapter(string chapterId)
    {
        requestedChapterId = chapterId;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        IsChapterOpening = false;
        requestedChapterId = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one Chapter Controller can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;
        IsChapterOpening = true;
        activeChapter = ResolveActiveChapter();
        ConfigureChapterContent();
        UnlockActiveChapterGlossary();
    }

    private IEnumerator Start()
    {
        if (activeChapter == null)
        {
            Debug.LogError("Chapter Controller has no valid chapter to open.", this);
            IsChapterOpening = false;
            yield break;
        }

        if (narrationController == null)
            narrationController = NarrationController.Instance;

        if (titleCard != null)
        {
            if (showTitleCard)
                titleCard.Prepare(activeChapter);
            else
                titleCard.HideImmediately();
        }

        // Give ScreenFade.Start time to begin the Mansion's scene-entry fade.
        yield return null;

        while (ScreenFade.IsTransitioning)
            yield return null;

        bool narrationStarted = false;

        if (showTitleCard && titleCard != null)
        {
            yield return titleCard.DisplayPreparedCard(
                () => narrationStarted = TryStartOpeningNarration());
        }
        else
        {
            narrationStarted = TryStartOpeningNarration();
        }

        IsChapterOpening = false;

        if (!narrationStarted && !string.IsNullOrWhiteSpace(activeChapter.StartingMissionId))
        {
            if (MissionController.Instance != null)
                MissionController.Instance.StartMission(activeChapter.StartingMissionId);
            else
                Debug.LogWarning("The chapter's starting mission could not begin because no Mission Controller is active.", this);
        }
    }

    private bool TryStartOpeningNarration()
    {
        return narrationController != null &&
               narrationController.Play(
                   activeChapter.OpeningNarration,
                   activeChapter.StartingMissionId);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        IsChapterOpening = false;
    }

    private ChapterDataSO ResolveActiveChapter()
    {
        if (!string.IsNullOrWhiteSpace(requestedChapterId))
        {
            foreach (ChapterContent content in chapterContents)
            {
                if (content?.Chapter != null &&
                    string.Equals(content.Chapter.ChapterId, requestedChapterId, StringComparison.Ordinal))
                {
                    return content.Chapter;
                }
            }

            if (defaultChapter != null &&
                string.Equals(defaultChapter.ChapterId, requestedChapterId, StringComparison.Ordinal))
            {
                return defaultChapter;
            }

            Debug.LogWarning($"Requested chapter '{requestedChapterId}' is not configured. Using the default chapter.", this);
        }

        if (defaultChapter != null)
            return defaultChapter;

        foreach (ChapterContent content in chapterContents)
        {
            if (content?.Chapter != null)
                return content.Chapter;
        }

        return null;
    }

    private void ConfigureChapterContent()
    {
        foreach (ChapterContent content in chapterContents)
        {
            if (content?.ContentRoot == null)
                continue;

            content.ContentRoot.SetActive(content.Chapter == activeChapter);
        }
    }

    private void UnlockActiveChapterGlossary()
    {
        if (activeChapter?.Glossary == null)
            return;

        JournalUnlockRegistry.Unlock(GlossaryJournalController.CollectionID, activeChapter.ChapterId);
    }
}
