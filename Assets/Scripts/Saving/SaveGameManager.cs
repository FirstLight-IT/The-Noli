using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class SaveGameManager : MonoBehaviour
{
    private const float PeriodicCheckpointInterval = 30f;
    public const string QuizSceneName = "ChapterQuiz";

    public static SaveGameManager Instance { get; private set; }
    public static GameSaveData CurrentData => Instance != null ? Instance.currentData : null;
    public static string AutosavePath => Instance != null ? Instance.fileService.SavePath : string.Empty;
    public static bool IsAutosaveRestorePending =>
        Instance != null && Instance.autosaveRestorePending;

    private SaveFileService fileService;
    private GameSaveData currentData;
    private Coroutine pendingSaveRoutine;
    private string pendingSaveReason;
    private bool applicationPaused;
    private bool skipNextPlayTimeFrame;
    private float timeSinceLastSave;
    private bool autosaveRestorePending;
    private bool isApplyingRestore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        GameObject root = new("Save Game Manager");
        root.AddComponent<SaveGameManager>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        fileService = new SaveFileService(Application.persistentDataPath);
    }

    private void OnEnable()
    {
        JournalUnlockRegistry.OnEntryUnlocked += HandleJournalEntryUnlocked;
        MissionController.OnMissionStatesChanged += HandleMissionStatesChanged;
        MissionController.OnMissionStepAdvanced += HandleMissionStepFinished;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        JournalUnlockRegistry.OnEntryUnlocked -= HandleJournalEntryUnlocked;
        MissionController.OnMissionStatesChanged -= HandleMissionStatesChanged;
        MissionController.OnMissionStepAdvanced -= HandleMissionStepFinished;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static bool HasAutosave()
    {
        EnsureInstance();
        return Instance.HasLoadableAutosave();
    }

    public static bool BeginNewGame(string chapterId)
    {
        EnsureInstance();
        return Instance.BeginNewGameInternal(chapterId);
    }

    public static bool TryLoadAutosave(out string error)
    {
        EnsureInstance();
        return Instance.TryLoadAutosaveInternal(out error);
    }

    public static void RequestAutosave(string reason)
    {
        EnsureInstance();
        Instance.QueueAutosave(reason);
    }

    public static void RecordPlayerDoorTransition()
    {
        EnsureInstance();
        Instance.RecordPlayerDoorTransitionInternal();
    }

    public static string GetContinueSceneName()
    {
        EnsureInstance();
        ChapterSaveData chapter = Instance.currentData?.FindChapter(
            Instance.currentData.activeChapterId);

        if (chapter?.quiz != null &&
            (IsQuizState(chapter.quiz, QuizProgressState.InProgress) ||
             IsQuizState(chapter.quiz, QuizProgressState.Submitted)))
        {
            return QuizSceneName;
        }

        return "Mansion";
    }

    public static bool BeginChapterQuiz(ChapterQuizDefinition quiz, out string error)
    {
        EnsureInstance();
        return Instance.BeginChapterQuizInternal(quiz, out error);
    }

    public static ChapterQuizSaveData GetActiveQuizProgress()
    {
        EnsureInstance();
        return Instance.GetActiveChapter()?.quiz;
    }

    public static bool RecordQuizAnswer(
        ChapterQuizDefinition quiz,
        string questionId,
        string optionId,
        out string error)
    {
        EnsureInstance();
        return Instance.RecordQuizAnswerInternal(quiz, questionId, optionId, out error);
    }

    public static bool SubmitChapterQuiz(ChapterQuizDefinition quiz, out string error)
    {
        EnsureInstance();
        return Instance.SubmitChapterQuizInternal(quiz, out error);
    }

    public static bool SetQuizLanguage(
        ChapterQuizDefinition quiz,
        string languageCode,
        out string error)
    {
        EnsureInstance();
        return Instance.SetQuizLanguageInternal(quiz, languageCode, out error);
    }

    public static bool CompleteChapterQuiz(ChapterQuizDefinition quiz, out string error)
    {
        EnsureInstance();
        return Instance.CompleteChapterQuizInternal(quiz, out error);
    }

    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        Bootstrap();
    }

    private bool BeginChapterQuizInternal(ChapterQuizDefinition quiz, out string error)
    {
        if (!TryValidateActiveQuiz(quiz, out ChapterSaveData chapter, out error))
            return false;

        ChapterQuizSaveData progress = chapter.quiz;

        if (IsQuizState(progress, QuizProgressState.InProgress) ||
            IsQuizState(progress, QuizProgressState.Submitted) ||
            IsQuizState(progress, QuizProgressState.Completed))
        {
            if (!ValidateSavedQuizSelection(quiz, progress, out error) ||
                !EnsureQuizOptionOrders(quiz, progress, out bool addedOrders, out error))
            {
                return false;
            }

            if (addedOrders && !SaveNow("QuizOptionOrderPrepared"))
            {
                error = "The randomized answer order could not be autosaved.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        int nextAttemptNumber = Math.Max(1, progress.attemptNumber + 1);
        int seed = unchecked(Environment.TickCount ^ currentData.saveRevision ^ nextAttemptNumber);
        progress.state = QuizProgressState.InProgress.ToString();
        progress.attemptNumber = nextAttemptNumber;
        progress.selectionSeed = seed;
        progress.languageCode = quiz.ResolveLanguageCode(progress.languageCode);
        progress.selectedQuestionIds = QuizEvaluation.SelectQuestionIds(
            quiz.Questions,
            quiz.QuestionsPerAttempt,
            seed);
        progress.optionOrders.Clear();
        progress.answers.Clear();
        progress.score = 0;
        progress.maxScore = progress.selectedQuestionIds.Count;
        progress.startedAtUtc = GetUtcTimestamp();
        progress.submittedAtUtc = string.Empty;
        progress.completedAtUtc = string.Empty;

        if (!EnsureQuizOptionOrders(quiz, progress, out _, out error))
            return false;

        if (!SaveNow("ChapterQuizStarted"))
        {
            error = "The quiz was prepared, but its autosave could not be written.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool SetQuizLanguageInternal(
        ChapterQuizDefinition quiz,
        string languageCode,
        out string error)
    {
        if (!TryValidateActiveQuiz(quiz, out ChapterSaveData chapter, out error))
            return false;

        string resolvedCode = quiz.ResolveLanguageCode(languageCode);

        if (!string.Equals(resolvedCode, languageCode, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Quiz language '{languageCode}' is not available.";
            return false;
        }

        chapter.quiz.languageCode = resolvedCode;

        if (!SaveNow("QuizLanguageChanged"))
        {
            error = "The selected quiz language could not be autosaved.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool RecordQuizAnswerInternal(
        ChapterQuizDefinition quiz,
        string questionId,
        string optionId,
        out string error)
    {
        if (!TryValidateActiveQuiz(quiz, out ChapterSaveData chapter, out error))
            return false;

        ChapterQuizSaveData progress = chapter.quiz;

        if (!IsQuizState(progress, QuizProgressState.InProgress))
        {
            error = "Answers can only be changed before the quiz is submitted.";
            return false;
        }

        if (!progress.selectedQuestionIds.Contains(questionId))
        {
            error = $"Question '{questionId}' is not part of this attempt.";
            return false;
        }

        QuizQuestionDefinition question = quiz.FindQuestion(questionId);

        QuizQuestionLocalization localization = quiz.GetQuestionLocalization(
            questionId,
            chapter.quiz.languageCode);

        if (localization?.FindOption(optionId) == null)
        {
            error = $"Choice '{optionId}' is not valid for question '{questionId}'.";
            return false;
        }

        progress.SetAnswer(questionId, optionId);

        if (!SaveNow("QuizAnswerChanged"))
        {
            error = "The selected answer could not be autosaved.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool SubmitChapterQuizInternal(ChapterQuizDefinition quiz, out string error)
    {
        if (!TryValidateActiveQuiz(quiz, out ChapterSaveData chapter, out error))
            return false;

        ChapterQuizSaveData progress = chapter.quiz;

        if (IsQuizState(progress, QuizProgressState.Submitted) ||
            IsQuizState(progress, QuizProgressState.Completed))
        {
            error = string.Empty;
            return true;
        }

        if (!IsQuizState(progress, QuizProgressState.InProgress))
        {
            error = "The quiz has not started.";
            return false;
        }

        foreach (string questionId in progress.selectedQuestionIds)
        {
            if (string.IsNullOrWhiteSpace(progress.GetSelectedAnswerId(questionId)))
            {
                error = "Please answer every question before submitting.";
                return false;
            }
        }

        progress.score = QuizEvaluation.CalculateScore(quiz, progress, out int maximumScore);
        progress.maxScore = maximumScore;
        progress.state = QuizProgressState.Submitted.ToString();
        progress.submittedAtUtc = GetUtcTimestamp();

        if (!SaveNow("ChapterQuizSubmitted"))
        {
            error = "Your result was calculated, but its autosave could not be written.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool CompleteChapterQuizInternal(ChapterQuizDefinition quiz, out string error)
    {
        if (!TryValidateActiveQuiz(quiz, out ChapterSaveData chapter, out error))
            return false;

        ChapterQuizSaveData progress = chapter.quiz;

        if (!IsQuizState(progress, QuizProgressState.Submitted) &&
            !IsQuizState(progress, QuizProgressState.Completed))
        {
            error = "Submit the quiz before completing the chapter.";
            return false;
        }

        if (!IsQuizState(progress, QuizProgressState.Completed))
        {
            string now = GetUtcTimestamp();
            progress.state = QuizProgressState.Completed.ToString();
            progress.completedAtUtc = now;
            chapter.state = "Completed";
            chapter.completedAtUtc = now;
            chapter.completionCount++;

            if (!chapter.completedEver)
            {
                chapter.completedEver = true;
                chapter.firstCompletedAtUtc = now;
            }

            if (!string.IsNullOrWhiteSpace(quiz.NextChapterId))
            {
                ChapterSaveData nextChapter = currentData.GetOrCreateChapter(quiz.NextChapterId);
                nextChapter.isUnlocked = true;

                if (string.Equals(nextChapter.state, "InProgress", StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(nextChapter.startedAtUtc))
                {
                    nextChapter.state = "NotStarted";
                }
            }
        }

        if (!SaveNow("ChapterCompleted"))
        {
            error = "Chapter completion could not be autosaved.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryValidateActiveQuiz(
        ChapterQuizDefinition quiz,
        out ChapterSaveData chapter,
        out string error)
    {
        chapter = null;

        if (quiz == null)
        {
            error = "No chapter quiz was provided.";
            return false;
        }

        if (!quiz.TryValidate(out error))
            return false;

        EnsureCurrentSave();

        if (!string.Equals(currentData.activeChapterId, quiz.ChapterId, StringComparison.Ordinal))
        {
            error = $"Quiz chapter '{quiz.ChapterId}' does not match active chapter '{currentData.activeChapterId}'.";
            return false;
        }

        chapter = currentData.GetOrCreateChapter(currentData.activeChapterId);
        chapter.quiz ??= new ChapterQuizSaveData();
        error = string.Empty;
        return true;
    }

    private static bool ValidateSavedQuizSelection(
        ChapterQuizDefinition quiz,
        ChapterQuizSaveData progress,
        out string error)
    {
        if (progress.selectedQuestionIds == null ||
            progress.selectedQuestionIds.Count != quiz.QuestionsPerAttempt)
        {
            error = "The saved quiz selection is incomplete.";
            return false;
        }

        foreach (string questionId in progress.selectedQuestionIds)
        {
            if (quiz.FindQuestion(questionId) == null)
            {
                error = $"Saved quiz question '{questionId}' no longer exists in the quiz asset.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool EnsureQuizOptionOrders(
        ChapterQuizDefinition quiz,
        ChapterQuizSaveData progress,
        out bool changed,
        out string error)
    {
        changed = false;

        foreach (string questionId in progress.selectedQuestionIds)
        {
            QuizQuestionLocalization question = quiz.GetQuestionLocalization(
                questionId,
                quiz.DefaultLanguageCode);

            if (question?.options == null || question.options.Count < 2)
            {
                error = $"Question '{questionId}' has no choices to randomize.";
                return false;
            }

            HashSet<string> expectedIds = new(StringComparer.Ordinal);

            foreach (QuizOptionLocalization option in question.options)
                expectedIds.Add(option.optionId);

            string savedAnswerId = progress.GetSelectedAnswerId(questionId);

            if (!string.IsNullOrWhiteSpace(savedAnswerId) && !expectedIds.Contains(savedAnswerId))
            {
                progress.RemoveAnswer(questionId);
                changed = true;
            }

            IReadOnlyList<string> savedOrder = progress.GetOptionOrder(questionId);
            HashSet<string> savedIds = new(savedOrder, StringComparer.Ordinal);

            if (savedOrder.Count == expectedIds.Count && savedIds.SetEquals(expectedIds))
                continue;

            int optionSeed = QuizEvaluation.CreateStableQuestionSeed(
                progress.selectionSeed,
                questionId);
            progress.SetOptionOrder(
                questionId,
                QuizEvaluation.ShuffleOptionIds(question.options, optionSeed));
            changed = true;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsQuizState(ChapterQuizSaveData progress, QuizProgressState state)
    {
        return progress != null &&
               string.Equals(progress.state, state.ToString(), StringComparison.Ordinal);
    }

    private bool BeginNewGameInternal(string chapterId)
    {
        autosaveRestorePending = false;
        isApplyingRestore = false;
        JournalUnlockRegistry.Clear();
        currentData = CreateNewSave(chapterId);

        if (pendingSaveRoutine != null)
        {
            StopCoroutine(pendingSaveRoutine);
            pendingSaveRoutine = null;
            pendingSaveReason = null;
        }

        return SaveNow("NewGameStarted", startFresh: true);
    }

    private bool TryLoadAutosaveInternal(out string error)
    {
        if (!fileService.TryLoad(out GameSaveData loadedData, out error))
            return false;

        if (!TryValidateContinuation(loadedData, out error))
            return false;

        currentData = loadedData;
        RestoreJournal(currentData.journal);
        autosaveRestorePending = !string.Equals(
            SceneManager.GetActiveScene().name,
            QuizSceneName,
            StringComparison.Ordinal);
        timeSinceLastSave = 0f;

        ChapterSaveData activeChapter = currentData.GetOrCreateChapter(
            currentData.activeChapterId);

        if (activeChapter != null)
            activeChapter.analytics.sessionCount++;

        return true;
    }

    private bool HasLoadableAutosave()
    {
        return fileService.TryLoad(out GameSaveData saveData, out _) &&
               TryValidateContinuation(saveData, out _);
    }

    private static bool TryValidateContinuation(GameSaveData saveData, out string error)
    {
        if (saveData == null)
        {
            error = "The autosave contains no data.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(saveData.activeChapterId))
        {
            error = "The autosave does not identify an active chapter.";
            return false;
        }

        if (saveData.FindChapter(saveData.activeChapterId) == null)
        {
            error = $"The autosave has no progress for chapter '{saveData.activeChapterId}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void HandleJournalEntryUnlocked(string collection, string entryId)
    {
        if (isApplyingRestore)
            return;

        EnsureCurrentSave();

        if (!currentData.journal.AddUnlockedEntry(collection, entryId))
            return;

        ChapterSaveData activeChapter = GetActiveChapter();

        if (activeChapter != null)
        {
            if (collection == JournalUnlockRegistry.ArtifactCollection)
                activeChapter.analytics.artifactsUnlocked++;
            else if (collection == JournalUnlockRegistry.CharacterCollection)
                activeChapter.analytics.charactersUnlocked++;
        }

        QueueAutosave("JournalEntryUnlocked");
    }

    private void HandleMissionStatesChanged()
    {
        if (isApplyingRestore)
            return;

        EnsureCurrentSave();
        QueueAutosave("MissionProgressChanged");
    }

    private void HandleMissionStepFinished(string missionId, int stepIndex)
    {
        if (isApplyingRestore)
            return;

        EnsureCurrentSave();

        ChapterSaveData activeChapter = GetActiveChapter();

        if (activeChapter != null)
            activeChapter.analytics.missionStepsCompleted++;

        QueueAutosave("MissionStepFinished");
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!autosaveRestorePending || currentData == null)
            return;

        ChapterSaveData activeChapter = currentData.FindChapter(
            currentData.activeChapterId);

        if (activeChapter == null)
        {
            Debug.LogError(
                $"Could not restore chapter '{currentData.activeChapterId}' because its save data is missing.",
                this);
            autosaveRestorePending = false;
            return;
        }

        if (string.Equals(scene.name, QuizSceneName, StringComparison.Ordinal))
        {
            autosaveRestorePending = false;
            QueueAutosave("GameLoaded");
            return;
        }

        ChapterController chapterController = ChapterController.Instance;

        if (chapterController == null || chapterController.ActiveChapter == null)
        {
            Debug.LogError(
                "The autosave could not be applied because no active Chapter Controller was found.",
                this);
            autosaveRestorePending = false;
            return;
        }

        if (!string.Equals(
                chapterController.ActiveChapter.ChapterId,
                currentData.activeChapterId,
                StringComparison.Ordinal))
        {
            Debug.LogError(
                $"Loaded chapter '{chapterController.ActiveChapter.ChapterId}' does not match saved chapter " +
                $"'{currentData.activeChapterId}'.",
                this);
            autosaveRestorePending = false;
            return;
        }

        isApplyingRestore = true;

        try
        {
            RestoreMissions(activeChapter, chapterController.ActiveChapter);
            RestoreCheckpoint(activeChapter, scene);
        }
        finally
        {
            isApplyingRestore = false;
            autosaveRestorePending = false;
        }

        QueueAutosave("GameLoaded");
    }

    private void RestoreMissions(ChapterSaveData chapter, ChapterDataSO chapterDefinition)
    {
        if (MissionController.Instance == null)
        {
            Debug.LogWarning("The saved missions could not be restored because no Mission Controller is active.", this);
            return;
        }

        if (MissionController.Instance.RestoreMissionProgress(chapter.missions))
            return;

        if (chapterDefinition != null &&
            !string.IsNullOrWhiteSpace(chapterDefinition.StartingMissionId))
        {
            Debug.LogWarning(
                "The autosave predates mission persistence. The chapter's starting mission will be used.",
                this);
            MissionController.Instance.StartMission(chapterDefinition.StartingMissionId);
        }
    }

    private void RestoreCheckpoint(ChapterSaveData chapter, Scene loadedScene)
    {
        ChapterCheckpointSaveData checkpoint = chapter.checkpoint;

        if (checkpoint == null || !checkpoint.hasPosition)
            return;

        if (!string.IsNullOrWhiteSpace(checkpoint.sceneName) &&
            !string.Equals(checkpoint.sceneName, loadedScene.name, StringComparison.Ordinal))
        {
            Debug.LogWarning(
                $"Saved checkpoint belongs to scene '{checkpoint.sceneName}', not '{loadedScene.name}'. " +
                "The scene's default player position will be used.",
                this);
            return;
        }

        if (PlayerCharacter.Instance == null)
        {
            Debug.LogWarning("The player checkpoint could not be restored because no Player Character is active.", this);
            return;
        }

        Vector2 savedPosition = checkpoint.position.ToVector2();
        Rigidbody2D playerBody = PlayerCharacter.Instance.GetComponent<Rigidbody2D>();

        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = savedPosition;
        }
        else
        {
            PlayerCharacter.Instance.transform.position = savedPosition;
        }

        Physics2D.SyncTransforms();
    }

    private void RecordPlayerDoorTransitionInternal()
    {
        EnsureCurrentSave();

        ChapterSaveData activeChapter = GetActiveChapter();

        if (activeChapter != null)
            activeChapter.analytics.doorTransitions++;

        QueueAutosave("PlayerDoorTransition");
    }

    private void Update()
    {
        if (applicationPaused ||
            currentData == null ||
            (ChapterController.Instance == null &&
             !string.Equals(SceneManager.GetActiveScene().name, QuizSceneName, StringComparison.Ordinal)) ||
            string.IsNullOrWhiteSpace(currentData.activeChapterId))
        {
            return;
        }

        if (skipNextPlayTimeFrame)
        {
            skipNextPlayTimeFrame = false;
            return;
        }

        ChapterSaveData activeChapter = currentData.GetOrCreateChapter(
            currentData.activeChapterId);

        if (activeChapter != null)
            activeChapter.analytics.playTimeSeconds += Time.unscaledDeltaTime;

        timeSinceLastSave += Time.unscaledDeltaTime;

        if (timeSinceLastSave >= PeriodicCheckpointInterval)
            QueueAutosave("PeriodicCheckpoint");
    }

    private void EnsureCurrentSave()
    {
        if (currentData != null)
            return;

        string chapterId = ChapterController.Instance != null &&
                           ChapterController.Instance.ActiveChapter != null
            ? ChapterController.Instance.ActiveChapter.ChapterId
            : string.Empty;

        currentData = CreateNewSave(chapterId);
    }

    private static GameSaveData CreateNewSave(string chapterId)
    {
        string now = GetUtcTimestamp();
        GameSaveData saveData = new()
        {
            schemaVersion = GameSaveData.CurrentSchemaVersion,
            gameVersion = Application.version,
            createdAtUtc = now,
            lastSavedAtUtc = now,
            activeChapterId = chapterId?.Trim() ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(saveData.activeChapterId))
        {
            ChapterSaveData chapter = saveData.GetOrCreateChapter(saveData.activeChapterId);
            chapter.isUnlocked = true;
            chapter.startedAtUtc = now;
            chapter.analytics.sessionCount = 1;
        }

        CapturePlayerCharacter(saveData);
        return saveData;
    }

    private void QueueAutosave(string reason)
    {
        EnsureCurrentSave();

        pendingSaveReason = string.IsNullOrWhiteSpace(pendingSaveReason)
            ? reason
            : pendingSaveReason == reason
                ? reason
                : "ProgressUpdated";

        if (pendingSaveRoutine == null)
            pendingSaveRoutine = StartCoroutine(SaveAfterCurrentFrame());
    }

    private IEnumerator SaveAfterCurrentFrame()
    {
        yield return null;

        string reason = pendingSaveReason;
        pendingSaveRoutine = null;
        pendingSaveReason = null;
        SaveNow(reason);
    }

    private bool SaveNow(string reason, bool startFresh = false)
    {
        if (currentData == null)
            return false;

        CaptureCommonState();

        int previousRevision = currentData.saveRevision;
        string previousSavedAt = currentData.lastSavedAtUtc;
        string previousReason = currentData.lastSaveReason;

        currentData.saveRevision++;
        currentData.lastSavedAtUtc = GetUtcTimestamp();
        currentData.lastSaveReason = string.IsNullOrWhiteSpace(reason)
            ? "Autosave"
            : reason.Trim();

        bool saved = startFresh
            ? fileService.TrySaveFresh(currentData, out string error)
            : fileService.TrySave(currentData, out error);

        if (saved)
        {
            timeSinceLastSave = 0f;
            return true;
        }

        currentData.saveRevision = previousRevision;
        currentData.lastSavedAtUtc = previousSavedAt;
        currentData.lastSaveReason = previousReason;
        Debug.LogError(error, this);
        return false;
    }

    private void CaptureCommonState()
    {
        string chapterId = ChapterController.Instance != null &&
                           ChapterController.Instance.ActiveChapter != null
            ? ChapterController.Instance.ActiveChapter.ChapterId
            : currentData.activeChapterId;

        if (!string.IsNullOrWhiteSpace(chapterId))
        {
            currentData.activeChapterId = chapterId;
            currentData.GetOrCreateChapter(chapterId);
        }

        CaptureJournal(currentData);
        CaptureMissions(currentData);
        CaptureCheckpoint(currentData);
        CapturePlayerCharacter(currentData);
        currentData.Normalize();
    }

    private ChapterSaveData GetActiveChapter()
    {
        CaptureCommonState();
        return currentData.GetOrCreateChapter(currentData.activeChapterId);
    }

    private static void CapturePlayerCharacter(GameSaveData saveData)
    {
        if (saveData == null ||
            PlayerCharacter.Instance == null ||
            PlayerCharacter.Instance.CurrentCharacter == null)
        {
            return;
        }

        saveData.player.currentCharacterId =
            PlayerCharacter.Instance.CurrentCharacter.NpcID;
    }

    private static void CaptureCheckpoint(GameSaveData saveData)
    {
        if (saveData == null ||
            PlayerCharacter.Instance == null ||
            string.IsNullOrWhiteSpace(saveData.activeChapterId))
        {
            return;
        }

        ChapterSaveData activeChapter = saveData.GetOrCreateChapter(
            saveData.activeChapterId);
        Rigidbody2D playerBody = PlayerCharacter.Instance.GetComponent<Rigidbody2D>();
        Vector2 playerPosition = playerBody != null
            ? playerBody.position
            : (Vector2)PlayerCharacter.Instance.transform.position;

        activeChapter.checkpoint.hasPosition = true;
        activeChapter.checkpoint.sceneName = SceneManager.GetActiveScene().name;
        activeChapter.checkpoint.position.Set(playerPosition);
    }

    private static void CaptureJournal(GameSaveData saveData)
    {
        if (saveData == null)
            return;

        saveData.journal ??= new JournalSaveData();
        saveData.journal.unlockedCharacterIds =
            JournalUnlockRegistry.GetUnlockedEntryIDs(
                JournalUnlockRegistry.CharacterCollection);
        saveData.journal.unlockedArtifactIds =
            JournalUnlockRegistry.GetUnlockedEntryIDs(
                JournalUnlockRegistry.ArtifactCollection);
        saveData.journal.unlockedGlossaryChapterIds =
            JournalUnlockRegistry.GetUnlockedEntryIDs(
                JournalUnlockRegistry.GlossaryChapterCollection);
    }

    private static void CaptureMissions(GameSaveData saveData)
    {
        MissionController missionController = MissionController.Instance;

        if (saveData == null ||
            missionController == null ||
            string.IsNullOrWhiteSpace(saveData.activeChapterId))
        {
            return;
        }

        ChapterSaveData activeChapter = saveData.GetOrCreateChapter(
            saveData.activeChapterId);
        List<MissionSaveData> missionProgress = new();

        foreach (MissionInfoSO missionInfo in missionController.MissionInfos)
        {
            if (missionInfo == null || string.IsNullOrWhiteSpace(missionInfo.MissionId))
                continue;

            missionProgress.Add(new MissionSaveData
            {
                missionId = missionInfo.MissionId,
                state = missionController.GetMissionState(missionInfo.MissionId).ToString(),
                currentStepIndex = missionController.GetMissionStepIndex(missionInfo.MissionId),
                stepProgress = missionController.GetMissionStepProgress(missionInfo.MissionId)
            });
        }

        activeChapter.missions = missionProgress;
    }

    private static void RestoreJournal(JournalSaveData journal)
    {
        JournalUnlockRegistry.Clear();

        if (journal == null)
            return;

        JournalUnlockRegistry.Restore(
            JournalUnlockRegistry.CharacterCollection,
            journal.unlockedCharacterIds);
        JournalUnlockRegistry.Restore(
            JournalUnlockRegistry.ArtifactCollection,
            journal.unlockedArtifactIds);
        JournalUnlockRegistry.Restore(
            JournalUnlockRegistry.GlossaryChapterCollection,
            journal.unlockedGlossaryChapterIds);
    }

    private void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;

        if (!paused)
        {
            skipNextPlayTimeFrame = true;
            return;
        }

        if (paused && currentData != null)
            SaveNow("ApplicationPaused");
    }

    private void OnApplicationQuit()
    {
        if (currentData != null)
            SaveNow("ApplicationQuit");
    }

    private static string GetUtcTimestamp()
    {
        return DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    }
}
