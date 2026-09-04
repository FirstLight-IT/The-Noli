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
    private const string ActiveSlotPlayerPrefsKey = "TheNoli.NormalSave.ActiveSlot";
    private const string AccountActiveSlotPlayerPrefsKeyPrefix =
        "TheNoli.AccountSave.ActiveSlot.";
    private const string ClassroomActiveSlotPlayerPrefsKeyPrefix =
        "TheNoli.ClassroomSave.ActiveSlot.";
    public const int SaveSlotCount = 3;
    public const string QuizSceneName = "ChapterQuiz";

    public static SaveGameManager Instance { get; private set; }
    public static GameSaveData CurrentData => Instance != null ? Instance.currentData : null;
    public static bool IsUsingClassroomSave =>
        Instance != null && !string.IsNullOrWhiteSpace(Instance.activeClassroomRoomId);
    public static string AutosavePath => Instance != null ? Instance.fileService.SavePath : string.Empty;
    public static int ActiveSlotNumber => Instance != null
        ? Instance.activeSlotNumber
        : SaveFileService.MinimumSlotNumber;
    public static bool IsAutosaveRestorePending =>
        Instance != null && Instance.autosaveRestorePending;

    private SaveFileService fileService;
    private string persistentDataPath;
    private string saveDirectory;
    private int activeSlotNumber = SaveFileService.MinimumSlotNumber;
    private GameSaveData currentData;
    private Coroutine pendingSaveRoutine;
    private string pendingSaveReason;
    private bool applicationPaused;
    private bool manuallyPaused;
    private bool skipNextPlayTimeFrame;
    private float timeSinceLastSave;
    private bool autosaveRestorePending;
    private bool isApplyingRestore;
    private string activeClassroomRoomId = string.Empty;

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
        persistentDataPath = Application.persistentDataPath;

        if (!SaveFileService.TryMigrateLegacySaveToSlotOne(
                persistentDataPath,
                out bool migrated,
                out string migrationError))
        {
            Debug.LogError(migrationError, this);
        }
        else if (migrated)
        {
            Debug.Log("The previous autosave was safely migrated into Save Slot 1.", this);
        }

        string guestSaveDirectory = SaveStorageScope.GetGuestSaveDirectory(persistentDataPath);

        if (!SaveFileService.TryMigrateUnscopedSlots(
                persistentDataPath,
                guestSaveDirectory,
                out bool guestSavesMigrated,
                out string guestMigrationError))
        {
            Debug.LogError(guestMigrationError, this);
        }
        else if (guestSavesMigrated)
        {
            Debug.Log("Existing device saves were safely migrated into Guest storage.", this);
        }

        saveDirectory = SaveStorageScope.GetCurrentSaveDirectory(persistentDataPath);
        activeSlotNumber = Mathf.Clamp(
            PlayerPrefs.GetInt(
                GetActiveSlotPlayerPrefsKey(),
                SaveFileService.MinimumSlotNumber),
            SaveFileService.MinimumSlotNumber,
            SaveFileService.MaximumSlotNumber);
        fileService = new SaveFileService(saveDirectory, activeSlotNumber);
    }

    private void OnEnable()
    {
        JournalUnlockRegistry.OnEntryUnlocked += HandleJournalEntryUnlocked;
        MissionController.OnMissionStatesChanged += HandleMissionStatesChanged;
        MissionController.OnMissionStepAdvanced += HandleMissionStepFinished;
        DialogueController.OnConversationReadingCompleted += HandleConversationReadingCompleted;
        Artifact.OnArtifactCatalogAvailable += HandleArtifactCatalogAvailable;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        PlayerSession.Changed += HandlePlayerSessionChanged;
    }

    private void OnDisable()
    {
        JournalUnlockRegistry.OnEntryUnlocked -= HandleJournalEntryUnlocked;
        MissionController.OnMissionStatesChanged -= HandleMissionStatesChanged;
        MissionController.OnMissionStepAdvanced -= HandleMissionStepFinished;
        DialogueController.OnConversationReadingCompleted -= HandleConversationReadingCompleted;
        Artifact.OnArtifactCatalogAvailable -= HandleArtifactCatalogAvailable;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        PlayerSession.Changed -= HandlePlayerSessionChanged;
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

    public static bool HasAnySaveSlot()
    {
        EnsureInstance();

        for (int slotNumber = SaveFileService.MinimumSlotNumber;
             slotNumber <= SaveFileService.MaximumSlotNumber;
             slotNumber++)
        {
            if (Instance.HasLoadableSlot(slotNumber))
                return true;
        }

        return false;
    }

    public static SaveSlotInfo GetSaveSlotInfo(int slotNumber)
    {
        EnsureInstance();
        return Instance.GetSaveSlotInfoInternal(slotNumber);
    }

    public static bool TryGetSaveSlotData(
        int slotNumber,
        out GameSaveData saveData,
        out string error)
    {
        EnsureInstance();
        return Instance.TryGetSaveSlotDataInternal(slotNumber, out saveData, out error);
    }

    public static bool BeginNewGameInSlot(
        int slotNumber,
        string chapterId,
        out string error)
    {
        EnsureInstance();
        return Instance.BeginNewGameInSlotInternal(
            slotNumber,
            chapterId,
            overwriteExisting: false,
            out error);
    }

    public static bool TryLoadSlot(int slotNumber, out string error)
    {
        EnsureInstance();
        return Instance.TryLoadSlotInternal(slotNumber, out error);
    }

    public static bool TryOpenClassroomSave(
        string roomId,
        string firstChapterId,
        out bool createdNewSave,
        out string error)
    {
        EnsureInstance();
        return Instance.TryOpenClassroomSaveInternal(
            roomId, firstChapterId, out createdNewSave, out error);
    }

    public static bool UseNormalSaveScope(out string error)
    {
        EnsureInstance();
        return Instance.SwitchSaveScope(string.Empty, out error);
    }

    public static bool TryDeleteSlot(int slotNumber, out string error)
    {
        EnsureInstance();
        return Instance.TryDeleteSlotInternal(slotNumber, out error);
    }

    public static bool SelectChapterForContinue(string chapterId, out string error)
    {
        EnsureInstance();
        return Instance.SelectChapterForContinueInternal(chapterId, out error);
    }

    public static bool StartChapter(
        string chapterId,
        bool replayCompletedChapter,
        out string error)
    {
        EnsureInstance();
        return Instance.StartChapterInternal(
            chapterId,
            replayCompletedChapter,
            out error);
    }

    public static bool RestartActiveChapter(out string error)
    {
        EnsureInstance();
        return Instance.RestartActiveChapterInternal(out error);
    }

    public static bool CanRestartActiveChapter(out string error)
    {
        EnsureInstance();
        return Instance.TryGetRestartableActiveChapter(out _, out error);
    }

    public static bool SaveImmediately(string reason, out string error)
    {
        EnsureInstance();
        return Instance.SaveImmediatelyInternal(reason, out error);
    }

    public static void SetManualPause(bool paused)
    {
        EnsureInstance();
        Instance.manuallyPaused = paused;

        if (!paused)
            Instance.skipNextPlayTimeFrame = true;
    }

    public static bool BeginNewGame(string chapterId)
    {
        EnsureInstance();
        return Instance.BeginNewGameInSlotInternal(
            Instance.activeSlotNumber,
            chapterId,
            overwriteExisting: true,
            out _);
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

    public static bool HasActiveChapterWorldFlag(string flagId)
    {
        EnsureInstance();

        if (Instance.currentData == null || string.IsNullOrWhiteSpace(flagId))
            return false;

        ChapterSaveData chapter = Instance.currentData.FindChapter(
            Instance.currentData.activeChapterId);
        return chapter != null && chapter.HasWorldFlag(flagId);
    }

    public static void RecordActiveChapterWorldFlag(string flagId)
    {
        EnsureInstance();

        if (string.IsNullOrWhiteSpace(flagId))
            return;

        Instance.EnsureCurrentSave();
        ChapterSaveData chapter = Instance.currentData.GetOrCreateChapter(
            Instance.currentData.activeChapterId);

        if (chapter != null && chapter.AddWorldFlag(flagId))
            Instance.QueueAutosave("ChapterWorldStateChanged");
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

        bool isPracticeAttempt = chapter.completedEver || progress.HasOfficialResult;
        int nextAttemptNumber = isPracticeAttempt
            ? progress.practiceAttempts.Count + 1
            : 1;
        int seed = unchecked(Environment.TickCount ^ currentData.saveRevision ^ nextAttemptNumber);
        progress.state = QuizProgressState.InProgress.ToString();
        progress.isPracticeAttempt = isPracticeAttempt;
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
            bool isPracticeAttempt = progress.isPracticeAttempt;
            progress.state = QuizProgressState.Completed.ToString();
            progress.completedAtUtc = now;

            if (isPracticeAttempt)
                progress.RecordPracticeResult();
            else
            {
                progress.RecordOfficialResultIfMissing();
                chapter.analytics.TryFinalizeEngagementScore(
                    progress.officialAttempt,
                    chapterCompleted: true);

                if (!chapter.completedEver)
                {
                    chapter.officialAnalytics ??= new OfficialChapterAnalyticsSaveData();
                    chapter.officialAnalytics.RecordIfMissing(
                        progress.officialAttempt,
                        chapter.analytics,
                        now);
                }
            }

            chapter.state = "Completed";
            chapter.completedAtUtc = now;
            chapter.completionCount++;

            if (!chapter.completedEver)
            {
                chapter.completedEver = true;
                chapter.firstCompletedAtUtc = now;
            }

            if (!isPracticeAttempt && !string.IsNullOrWhiteSpace(quiz.NextChapterId))
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

        string saveReason = progress.isPracticeAttempt
            ? "PracticeQuizCompleted"
            : "ChapterCompleted";

        if (!SaveNow(saveReason))
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

    private bool BeginNewGameInSlotInternal(
        int slotNumber,
        string chapterId,
        bool overwriteExisting,
        out string error)
    {
        if (!TryValidateSlotNumber(slotNumber, out error))
            return false;

        SaveFileService targetService = new(saveDirectory, slotNumber);
        bool hasExistingProgress = targetService.HasValidSave() ||
                                   (activeSlotNumber == slotNumber && currentData != null);

        if (hasExistingProgress && !overwriteExisting)
        {
            error = $"Save Slot {slotNumber} already contains a game.";
            return false;
        }

        if (currentData != null &&
            activeSlotNumber != slotNumber &&
            !SaveNow("SaveSlotChanged"))
        {
            error = $"The active Save Slot {activeSlotNumber} could not be saved before switching slots.";
            return false;
        }

        ActivateSlot(slotNumber);
        autosaveRestorePending = false;
        isApplyingRestore = false;
        JournalUnlockRegistry.Clear();
        currentData = CreateNewSave(chapterId);

        if (!SaveNow("NewGameStarted", startFresh: true))
        {
            error = $"A new game could not be created in Save Slot {slotNumber}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool SelectChapterForContinueInternal(string chapterId, out string error)
    {
        if (!TryGetSelectableChapter(chapterId, out ChapterSaveData chapter, out error))
            return false;

        if (!string.Equals(chapter.state, "InProgress", StringComparison.Ordinal))
        {
            error = $"Chapter '{chapterId}' is not currently in progress.";
            return false;
        }

        string previousChapterId = currentData.activeChapterId;
        bool changedChapter = !string.Equals(
            previousChapterId,
            chapter.chapterId,
            StringComparison.Ordinal);

        currentData.activeChapterId = chapter.chapterId;

        if (changedChapter)
            chapter.analytics.sessionCount++;

        if (!SaveNow("ActiveChapterSelected"))
        {
            currentData.activeChapterId = previousChapterId;

            if (changedChapter)
                chapter.analytics.sessionCount = Math.Max(0, chapter.analytics.sessionCount - 1);

            error = "The selected chapter could not be saved.";
            return false;
        }

        autosaveRestorePending = !string.Equals(
            SceneManager.GetActiveScene().name,
            QuizSceneName,
            StringComparison.Ordinal);
        error = string.Empty;
        return true;
    }

    private bool StartChapterInternal(
        string chapterId,
        bool replayCompletedChapter,
        out string error)
    {
        if (!TryGetSelectableChapter(chapterId, out ChapterSaveData existingChapter, out error))
            return false;

        if (replayCompletedChapter && !existingChapter.completedEver)
        {
            error = $"Chapter '{chapterId}' has not been completed and cannot be replayed yet.";
            return false;
        }

        if (!replayCompletedChapter &&
            !string.Equals(existingChapter.state, "NotStarted", StringComparison.Ordinal))
        {
            error = $"Chapter '{chapterId}' has already been started.";
            return false;
        }

        string reason = replayCompletedChapter
            ? "ChapterReplayStarted"
            : "ChapterStarted";
        return ResetChapterAttempt(existingChapter, reason, false, out error);
    }

    private bool RestartActiveChapterInternal(out string error)
    {
        if (!TryGetRestartableActiveChapter(out ChapterSaveData existingChapter, out error))
            return false;

        return ResetChapterAttempt(existingChapter, "ChapterRestarted", true, out error);
    }

    private bool TryGetRestartableActiveChapter(
        out ChapterSaveData chapter,
        out string error)
    {
        chapter = null;
        if (currentData == null || string.IsNullOrWhiteSpace(currentData.activeChapterId))
        {
            error = "No active chapter is available to restart.";
            return false;
        }

        if (!TryGetSelectableChapter(currentData.activeChapterId, out chapter, out error))
            return false;

        if (!string.IsNullOrWhiteSpace(activeClassroomRoomId) && !chapter.completedEver)
        {
            error = "Classroom chapters cannot be restarted during their first playthrough.";
            chapter = null;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool ResetChapterAttempt(
        ChapterSaveData existingChapter,
        string reason,
        bool countAsRestart,
        out string error)
    {
        int chapterIndex = currentData.chapters.IndexOf(existingChapter);
        string previousChapterId = currentData.activeChapterId;
        bool previousRestorePending = autosaveRestorePending;
        string now = GetUtcTimestamp();
        ChapterSaveData resetChapter = new()
        {
            chapterId = existingChapter.chapterId,
            state = "InProgress",
            isUnlocked = true,
            completedEver = existingChapter.completedEver,
            completionCount = existingChapter.completionCount,
            startedAtUtc = now,
            completedAtUtc = string.Empty,
            firstCompletedAtUtc = existingChapter.firstCompletedAtUtc,
            checkpoint = new ChapterCheckpointSaveData(),
            missions = new List<MissionSaveData>(),
            worldFlags = new List<string>(),
            quiz = existingChapter.quiz?.CreateFreshAttempt() ?? new ChapterQuizSaveData(),
            analytics = existingChapter.analytics ?? new ChapterAnalyticsSaveData()
        };

        resetChapter.analytics.sessionCount++;

        if (countAsRestart)
            resetChapter.analytics.chapterRestarts++;

        currentData.chapters[chapterIndex] = resetChapter;
        currentData.activeChapterId = resetChapter.chapterId;
        autosaveRestorePending = false;
        isApplyingRestore = false;

        CancelPendingAutosave();

        if (!SaveNow(reason, captureRuntimeState: false))
        {
            resetChapter.analytics.sessionCount = Math.Max(
                0,
                resetChapter.analytics.sessionCount - 1);

            if (countAsRestart)
            {
                resetChapter.analytics.chapterRestarts = Math.Max(
                    0,
                    resetChapter.analytics.chapterRestarts - 1);
            }

            currentData.chapters[chapterIndex] = existingChapter;
            currentData.activeChapterId = previousChapterId;
            autosaveRestorePending = previousRestorePending;
            error = "The chapter could not be prepared and autosaved.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool SaveImmediatelyInternal(string reason, out string error)
    {
        if (currentData == null)
        {
            error = "There is no active game to save.";
            return false;
        }

        CancelPendingAutosave();

        if (!SaveNow(reason))
        {
            error = "The game could not be saved. Check the Console for details.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryGetSelectableChapter(
        string chapterId,
        out ChapterSaveData chapter,
        out string error)
    {
        chapter = null;

        if (currentData == null)
        {
            error = "Load a save slot before selecting a chapter.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(chapterId))
        {
            error = "A chapter ID is required.";
            return false;
        }

        chapter = currentData.FindChapter(chapterId.Trim());

        if (chapter == null || !chapter.isUnlocked)
        {
            error = $"Chapter '{chapterId}' is locked.";
            chapter = null;
            return false;
        }

        chapter.Normalize();
        error = string.Empty;
        return true;
    }

    private bool TryLoadAutosaveInternal(out string error)
    {
        return TryLoadSlotInternal(activeSlotNumber, out error);
    }

    private bool TryOpenClassroomSaveInternal(
        string roomId,
        string firstChapterId,
        out bool createdNewSave,
        out string error)
    {
        createdNewSave = false;
        if (!PlayerSession.IsSignedIn || string.IsNullOrWhiteSpace(PlayerSession.AccountId))
        {
            error = "Sign in before opening a classroom save.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            error = "A classroom ID is required.";
            return false;
        }

        if (!SwitchSaveScope(roomId.Trim(), out error))
            return false;

        if (HasLoadableSlot(SaveFileService.MinimumSlotNumber))
            return TryLoadSlotInternal(SaveFileService.MinimumSlotNumber, out error);

        createdNewSave = true;
        return BeginNewGameInSlotInternal(
            SaveFileService.MinimumSlotNumber,
            firstChapterId,
            overwriteExisting: false,
            out error);
    }

    private bool SwitchSaveScope(string classroomRoomId, out string error)
    {
        string requestedRoomId = classroomRoomId?.Trim() ?? string.Empty;
        string targetDirectory = string.IsNullOrEmpty(requestedRoomId)
            ? SaveStorageScope.GetCurrentSaveDirectory(persistentDataPath)
            : SaveStorageScope.GetClassroomSaveDirectory(
                persistentDataPath, PlayerSession.AccountId, requestedRoomId);

        if (string.Equals(saveDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
        {
            activeClassroomRoomId = requestedRoomId;
            error = string.Empty;
            return true;
        }

        if (currentData != null && !SaveNow("SaveScopeChanged"))
        {
            error = "The current game could not be saved before changing save types.";
            return false;
        }

        CancelPendingAutosave();
        currentData = null;
        autosaveRestorePending = false;
        isApplyingRestore = false;
        timeSinceLastSave = 0f;
        JournalUnlockRegistry.Clear();
        activeClassroomRoomId = requestedRoomId;
        saveDirectory = targetDirectory;
        activeSlotNumber = SaveFileService.MinimumSlotNumber;
        fileService = new SaveFileService(saveDirectory, activeSlotNumber);
        error = string.Empty;
        return true;
    }

    private bool TryLoadSlotInternal(int slotNumber, out string error)
    {
        if (!TryValidateSlotNumber(slotNumber, out error))
            return false;

        if (currentData != null && !SaveNow("BeforeSaveSlotLoad"))
        {
            error = $"Save Slot {activeSlotNumber} could not be saved before loading another slot.";
            return false;
        }

        SaveFileService targetService = new(saveDirectory, slotNumber);

        if (!targetService.TryLoad(out GameSaveData loadedData, out error))
        {
            error = $"Save Slot {slotNumber} could not be loaded. {error}";
            return false;
        }

        if (!TryValidateContinuation(loadedData, out error))
            return false;

        ActivateSlot(slotNumber);
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

    private bool TryDeleteSlotInternal(int slotNumber, out string error)
    {
        if (!TryValidateSlotNumber(slotNumber, out error))
            return false;

        SaveFileService targetService = new(saveDirectory, slotNumber);

        if (!targetService.TryDeleteAll(out error))
        {
            error = $"Save Slot {slotNumber} could not be deleted. {error}";
            return false;
        }

        if (slotNumber == activeSlotNumber)
        {
            CancelPendingAutosave();
            currentData = null;
            autosaveRestorePending = false;
            isApplyingRestore = false;
            timeSinceLastSave = 0f;
            JournalUnlockRegistry.Clear();
        }

        error = string.Empty;
        return true;
    }

    private SaveSlotInfo GetSaveSlotInfoInternal(int slotNumber)
    {
        if (!TryValidateSlotNumber(slotNumber, out string error))
            throw new ArgumentOutOfRangeException(nameof(slotNumber), error);

        SaveFileService slotService = new(saveDirectory, slotNumber);

        return slotService.TryLoad(out GameSaveData saveData, out _) &&
               TryValidateContinuation(saveData, out _)
            ? SaveSlotInfo.FromSave(slotNumber, saveData)
            : SaveSlotInfo.Empty(slotNumber);
    }

    private bool TryGetSaveSlotDataInternal(
        int slotNumber,
        out GameSaveData saveData,
        out string error)
    {
        saveData = null;

        if (!TryValidateSlotNumber(slotNumber, out error))
            return false;

        SaveFileService slotService = new(saveDirectory, slotNumber);
        return slotService.TryLoad(out saveData, out error);
    }

    private bool HasLoadableAutosave()
    {
        return fileService.TryLoad(out GameSaveData saveData, out _) &&
               TryValidateContinuation(saveData, out _);
    }

    private bool HasLoadableSlot(int slotNumber)
    {
        if (!TryValidateSlotNumber(slotNumber, out _))
            return false;

        SaveFileService slotService = new(saveDirectory, slotNumber);
        return slotService.TryLoad(out GameSaveData saveData, out _) &&
               TryValidateContinuation(saveData, out _);
    }

    private void ActivateSlot(int slotNumber)
    {
        CancelPendingAutosave();
        activeSlotNumber = slotNumber;
        fileService = new SaveFileService(saveDirectory, activeSlotNumber);
        PlayerPrefs.SetInt(GetActiveSlotPlayerPrefsKey(), activeSlotNumber);
        PlayerPrefs.Save();
    }

    private void HandlePlayerSessionChanged()
    {
        if (currentData != null && !SaveNow("SaveOwnerChanged"))
        {
            Debug.LogError(
                "The current save could not be written before changing save owners.",
                this);
        }

        CancelPendingAutosave();
        currentData = null;
        autosaveRestorePending = false;
        isApplyingRestore = false;
        timeSinceLastSave = 0f;
        JournalUnlockRegistry.Clear();

        activeClassroomRoomId = string.Empty;
        saveDirectory = SaveStorageScope.GetCurrentSaveDirectory(persistentDataPath);
        activeSlotNumber = Mathf.Clamp(
            PlayerPrefs.GetInt(
                GetActiveSlotPlayerPrefsKey(),
                SaveFileService.MinimumSlotNumber),
            SaveFileService.MinimumSlotNumber,
            SaveFileService.MaximumSlotNumber);
        fileService = new SaveFileService(saveDirectory, activeSlotNumber);
    }

    private static string GetActiveSlotPlayerPrefsKey()
    {
        if (Instance != null && !string.IsNullOrWhiteSpace(Instance.activeClassroomRoomId))
        {
            return ClassroomActiveSlotPlayerPrefsKeyPrefix +
                   SaveStorageScope.GetCurrentOwnerKey() + "." +
                   Instance.activeClassroomRoomId;
        }

        return PlayerSession.IsGuest
            ? ActiveSlotPlayerPrefsKey
            : AccountActiveSlotPlayerPrefsKeyPrefix + SaveStorageScope.GetCurrentOwnerKey();
    }

    private void CancelPendingAutosave()
    {
        if (pendingSaveRoutine != null)
            StopCoroutine(pendingSaveRoutine);

        pendingSaveRoutine = null;
        pendingSaveReason = null;
    }

    private static bool TryValidateSlotNumber(int slotNumber, out string error)
    {
        if (slotNumber >= SaveFileService.MinimumSlotNumber &&
            slotNumber <= SaveFileService.MaximumSlotNumber)
        {
            error = string.Empty;
            return true;
        }

        error =
            $"Save slot must be between {SaveFileService.MinimumSlotNumber} " +
            $"and {SaveFileService.MaximumSlotNumber}.";
        return false;
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
            {
                int totalArtifactCount = Artifact.TryGetById(entryId, out Artifact artifact)
                    ? artifact.ArtifactData.TotalArtifactCount
                    : activeChapter.analytics.artifactsAvailable;
                activeChapter.analytics.RecordArtifactDiscovery(totalArtifactCount);
            }
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

        if (activeChapter != null &&
            (MissionController.Instance == null ||
             MissionController.Instance.IsPlayerObjectiveStep(missionId, stepIndex)))
        {
            activeChapter.analytics.missionStepsCompleted++;
        }

        QueueAutosave("MissionStepFinished");
    }

    private void HandleConversationReadingCompleted(ConversationReadingResult result)
    {
        if (isApplyingRestore || result == null)
            return;

        EnsureCurrentSave();

        ChapterSaveData activeChapter = GetActiveChapter();

        if (activeChapter == null)
            return;

        activeChapter.analytics.RecordMissionConversationReading(
            result.LinesViewed,
            result.RapidlySkippedLines);
        QueueAutosave("ConversationReadingCompleted");
    }

    private void HandleArtifactCatalogAvailable(int totalArtifactCount)
    {
        if (isApplyingRestore || currentData == null || totalArtifactCount <= 0)
            return;

        ChapterSaveData activeChapter = currentData.FindChapter(currentData.activeChapterId);

        if (activeChapter == null)
            return;

        activeChapter.analytics.SetArtifactsAvailable(totalArtifactCount);
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
        if (applicationPaused || manuallyPaused ||
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

    private bool SaveNow(
        string reason,
        bool startFresh = false,
        bool captureRuntimeState = true)
    {
        if (currentData == null)
            return false;

        if (fileService == null)
        {
            if (string.IsNullOrWhiteSpace(saveDirectory))
            {
                Debug.LogWarning("The save location was unavailable during shutdown.", this);
                return false;
            }

            fileService = new SaveFileService(saveDirectory, activeSlotNumber);
        }

        if (captureRuntimeState)
            CaptureCommonState();
        else
            currentData.Normalize();

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
            if (!string.IsNullOrWhiteSpace(activeClassroomRoomId) &&
                ShouldSyncClassroomProgress(currentData.lastSaveReason))
            {
                ClassroomProgressSyncService.Queue(activeClassroomRoomId, currentData);
            }
            return true;
        }

        currentData.saveRevision = previousRevision;
        currentData.lastSavedAtUtc = previousSavedAt;
        currentData.lastSaveReason = previousReason;
        Debug.LogError(error, this);
        return false;
    }

    private static bool ShouldSyncClassroomProgress(string reason)
    {
        // The sync service only serializes finalized official chapter analytics.
        // Navigation saves remain retry points for chapters completed while offline.
        return reason == "ChapterCompleted" ||
               reason == "ActiveChapterSelected" ||
               reason == "ReturnToMainMenu" ||
               reason == "SaveScopeChanged";
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
