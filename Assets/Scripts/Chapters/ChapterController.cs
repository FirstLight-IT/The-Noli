using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-200)]
public sealed class ChapterController : MonoBehaviour
{
    public static ChapterController Instance { get; private set; }
    public static bool IsChapterOpening { get; private set; }

    private static string requestedChapterId;

    [Header("Chapter Selection")]
    [SerializeField] private ChapterDataSO defaultChapter;

    [SerializeField, HideInInspector] private ChapterDataSO editorPlaytestChapter;

    [Header("Shared Opening UI")]
    [SerializeField] private bool showTitleCard;
    [SerializeField] private ChapterTitleCardController titleCard;
    [SerializeField] private NarrationController narrationController;

    private ChapterDataSO activeChapter;
    private ChapterContentRoot[] contentRoots = Array.Empty<ChapterContentRoot>();
    private bool resumeFromAutosave;
    private bool quizTransitionStarted;

    public ChapterDataSO ActiveChapter => activeChapter;
    public ChapterDataSO DefaultChapter => defaultChapter;
    public ChapterDataSO EditorPlaytestChapter => editorPlaytestChapter;
    public IReadOnlyList<ChapterContentRoot> ContentRoots => contentRoots;

    public IEnumerable<ChapterDataSO> ConfiguredChapters
    {
        get
        {
            HashSet<ChapterDataSO> yielded = new();

            if (defaultChapter != null && yielded.Add(defaultChapter))
                yield return defaultChapter;

            foreach (ChapterContentRoot contentRoot in contentRoots)
            {
                if (contentRoot != null &&
                    contentRoot.Chapter != null &&
                    yielded.Add(contentRoot.Chapter))
                {
                    yield return contentRoot.Chapter;
                }
            }
        }
    }

    public static void RequestChapter(string chapterId)
    {
        requestedChapterId = chapterId;
    }

    public void SetEditorPlaytestChapter(ChapterDataSO chapter)
    {
        editorPlaytestChapter = chapter;
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
        RefreshContentRoots();
        activeChapter = ResolveActiveChapter();
        resumeFromAutosave = SaveGameManager.IsAutosaveRestorePending;
        ConfigureChapterContent();
        ConfigurePlayer();
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

        if (resumeFromAutosave)
        {
            if (titleCard != null)
                titleCard.HideImmediately();

            IsChapterOpening = false;
            yield return null;
            TryResumePendingQuiz();
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

    private void OnEnable()
    {
        MissionController.OnMissionCompletionPresented += HandleMissionCompletionPresented;
    }

    private void OnDisable()
    {
        MissionController.OnMissionCompletionPresented -= HandleMissionCompletionPresented;
    }

    private void HandleMissionCompletionPresented(string missionId)
    {
        if (activeChapter == null ||
            !string.Equals(missionId, activeChapter.FinalMissionId, StringComparison.Ordinal))
        {
            return;
        }

        BeginQuizTransition();
    }

    private void TryResumePendingQuiz()
    {
        if (activeChapter == null ||
            activeChapter.CompletionQuizJson == null ||
            MissionController.Instance == null ||
            string.IsNullOrWhiteSpace(activeChapter.FinalMissionId))
        {
            return;
        }

        ChapterSaveData savedChapter = SaveGameManager.CurrentData?.FindChapter(activeChapter.ChapterId);
        bool quizHasNotStarted = savedChapter?.quiz == null ||
                                 string.Equals(
                                     savedChapter.quiz.state,
                                     QuizProgressState.NotStarted.ToString(),
                                     StringComparison.Ordinal);

        if (quizHasNotStarted &&
            MissionController.Instance.GetMissionState(activeChapter.FinalMissionId) == MissionState.Finished)
        {
            BeginQuizTransition();
        }
    }

    private void BeginQuizTransition()
    {
        if (quizTransitionStarted || activeChapter?.CompletionQuizJson == null)
            return;

        if (!ChapterQuizJsonLoader.TryLoad(
                activeChapter.CompletionQuizJson,
                out ChapterQuizDefinition quiz,
                out string error) ||
            !SaveGameManager.BeginChapterQuiz(quiz, out error))
        {
            Debug.LogError($"The chapter quiz could not begin: {error}", this);
            return;
        }

        quizTransitionStarted = true;
        StartCoroutine(TransitionToQuizWhenReady());
    }

    private IEnumerator TransitionToQuizWhenReady()
    {
        while (ScreenFade.IsTransitioning)
            yield return null;

        if (titleCard != null)
            yield return titleCard.DisplayChapterCompletion(activeChapter);

        while (ScreenFade.IsTransitioning)
            yield return null;

        void LoadQuiz()
        {
            SceneManager.LoadScene(activeChapter.QuizSceneName);
        }

        if (ScreenFade.Instance == null || !ScreenFade.Instance.BeginTransition(LoadQuiz))
            LoadQuiz();
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
        bool hasExplicitChapterRequest = !string.IsNullOrWhiteSpace(requestedChapterId);
        if (hasExplicitChapterRequest)
        {
            foreach (ChapterContentRoot contentRoot in contentRoots)
            {
                if (contentRoot != null &&
                    contentRoot.Chapter != null &&
                    string.Equals(
                        contentRoot.Chapter.ChapterId,
                        requestedChapterId,
                        StringComparison.Ordinal))
                {
                    return contentRoot.Chapter;
                }
            }

            if (defaultChapter != null &&
                string.Equals(defaultChapter.ChapterId, requestedChapterId, StringComparison.Ordinal))
            {
                return defaultChapter;
            }

            Debug.LogWarning($"Requested chapter '{requestedChapterId}' is not configured. Using the default chapter.", this);
        }

        if (!hasExplicitChapterRequest && Application.isEditor && editorPlaytestChapter != null)
        {
            foreach (ChapterContentRoot contentRoot in contentRoots)
            {
                if (contentRoot != null && contentRoot.Chapter == editorPlaytestChapter)
                    return editorPlaytestChapter;
            }

            Debug.LogWarning(
                $"Editor playtest chapter '{editorPlaytestChapter.ChapterId}' is not configured in this scene. Using the default chapter.",
                this);
        }

        if (defaultChapter != null)
            return defaultChapter;

        foreach (ChapterContentRoot contentRoot in contentRoots)
        {
            if (contentRoot != null && contentRoot.Chapter != null)
                return contentRoot.Chapter;
        }

        return null;
    }

    private void ConfigureChapterContent()
    {
        foreach (ChapterContentRoot contentRoot in contentRoots)
        {
            if (contentRoot == null)
                continue;

            contentRoot.gameObject.SetActive(contentRoot.Chapter == activeChapter);
        }
    }

    public void PreviewChapterContent(ChapterDataSO chapter)
    {
        RefreshContentRoots();

        foreach (ChapterContentRoot contentRoot in contentRoots)
        {
            if (contentRoot != null)
                contentRoot.gameObject.SetActive(contentRoot.Chapter == chapter);
        }
    }

    private void RefreshContentRoots()
    {
        ChapterContentRoot[] discoveredRoots = FindObjectsByType<ChapterContentRoot>(
            FindObjectsInactive.Include);
        List<ChapterContentRoot> sceneRoots = new();

        foreach (ChapterContentRoot contentRoot in discoveredRoots)
        {
            if (contentRoot != null && contentRoot.gameObject.scene == gameObject.scene)
                sceneRoots.Add(contentRoot);
        }

        contentRoots = sceneRoots.ToArray();
    }

    private void ConfigurePlayer()
    {
        if (activeChapter == null || PlayerCharacter.Instance == null)
            return;

        if (activeChapter.PlayerCharacter != null)
            PlayerCharacter.Instance.SetCharacter(activeChapter.PlayerCharacter);

        // Continue/load restores the saved checkpoint after scene load. Only a
        // fresh chapter attempt should use the authored starting marker.
        if (resumeFromAutosave)
            return;

        ChapterPlayerSpawn spawnPoint = FindPlayerSpawn(activeChapter);
        if (spawnPoint == null)
        {
            Debug.LogWarning(
                $"Chapter '{activeChapter.ChapterId}' has no player spawn point in this scene.",
                this);
            return;
        }

        Rigidbody2D playerBody = PlayerCharacter.Instance.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = spawnPoint.transform.position;
        }
        else
        {
            PlayerCharacter.Instance.transform.position = spawnPoint.transform.position;
        }

        Physics2D.SyncTransforms();
    }

    private ChapterPlayerSpawn FindPlayerSpawn(ChapterDataSO chapter)
    {
        ChapterPlayerSpawn matchingSpawn = null;

        foreach (ChapterPlayerSpawn spawn in FindObjectsByType<ChapterPlayerSpawn>(
                     FindObjectsInactive.Include))
        {
            if (spawn == null ||
                spawn.gameObject.scene != gameObject.scene ||
                spawn.Chapter != chapter)
            {
                continue;
            }

            if (matchingSpawn != null)
            {
                Debug.LogError(
                    $"Chapter '{chapter.ChapterId}' has more than one player spawn point.",
                    this);
                return null;
            }

            matchingSpawn = spawn;
        }

        return matchingSpawn;
    }

    private void UnlockActiveChapterGlossary()
    {
        if (activeChapter?.Glossary == null)
            return;

        JournalUnlockRegistry.Unlock(GlossaryJournalController.CollectionID, activeChapter.ChapterId);
    }
}
