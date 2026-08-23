using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GameSaveData
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public string gameVersion = string.Empty;
    public string playthroughId = Guid.NewGuid().ToString("N");
    public int saveRevision;
    public string createdAtUtc = string.Empty;
    public string lastSavedAtUtc = string.Empty;
    public string lastSaveReason = string.Empty;
    public string activeChapterId = string.Empty;
    public PlayerSaveData player = new();
    public JournalSaveData journal = new();
    public List<ChapterSaveData> chapters = new();

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(playthroughId))
            playthroughId = Guid.NewGuid().ToString("N");
        else
            playthroughId = playthroughId.Trim();

        player ??= new PlayerSaveData();
        journal ??= new JournalSaveData();
        chapters ??= new List<ChapterSaveData>();

        journal.Normalize();

        for (int i = chapters.Count - 1; i >= 0; i--)
        {
            if (chapters[i] == null || string.IsNullOrWhiteSpace(chapters[i].chapterId))
            {
                chapters.RemoveAt(i);
                continue;
            }

            chapters[i].Normalize();
        }

        ChapterSaveData activeChapter = FindChapter(activeChapterId);

        if (activeChapter != null)
            activeChapter.isUnlocked = true;
    }

    public ChapterSaveData GetOrCreateChapter(string chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId))
            return null;

        string normalizedId = chapterId.Trim();

        foreach (ChapterSaveData chapter in chapters)
        {
            if (chapter != null &&
                string.Equals(chapter.chapterId, normalizedId, StringComparison.Ordinal))
            {
                return chapter;
            }
        }

        ChapterSaveData createdChapter = new()
        {
            chapterId = normalizedId,
            state = "InProgress"
        };

        chapters.Add(createdChapter);
        return createdChapter;
    }

    public ChapterSaveData FindChapter(string chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId) || chapters == null)
            return null;

        foreach (ChapterSaveData chapter in chapters)
        {
            if (chapter != null &&
                string.Equals(chapter.chapterId, chapterId, StringComparison.Ordinal))
            {
                return chapter;
            }
        }

        return null;
    }
}

[Serializable]
public sealed class PlayerSaveData
{
    public string currentCharacterId = string.Empty;
}

[Serializable]
public sealed class JournalSaveData
{
    public List<string> unlockedCharacterIds = new();
    public List<string> unlockedArtifactIds = new();
    public List<string> unlockedGlossaryChapterIds = new();

    public void Normalize()
    {
        unlockedCharacterIds = NormalizeIds(unlockedCharacterIds);
        unlockedArtifactIds = NormalizeIds(unlockedArtifactIds);
        unlockedGlossaryChapterIds = NormalizeIds(unlockedGlossaryChapterIds);
    }

    public bool AddUnlockedEntry(string collection, string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return false;

        List<string> entries = collection switch
        {
            JournalUnlockRegistry.CharacterCollection => unlockedCharacterIds,
            JournalUnlockRegistry.ArtifactCollection => unlockedArtifactIds,
            JournalUnlockRegistry.GlossaryChapterCollection => unlockedGlossaryChapterIds,
            _ => null
        };

        if (entries == null)
            return false;

        string normalizedId = entryId.Trim().ToLowerInvariant();

        if (entries.Contains(normalizedId))
            return false;

        entries.Add(normalizedId);
        entries.Sort(StringComparer.Ordinal);
        return true;
    }

    private static List<string> NormalizeIds(List<string> source)
    {
        List<string> normalized = new();

        if (source == null)
            return normalized;

        HashSet<string> uniqueIds = new(StringComparer.Ordinal);

        foreach (string entryId in source)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                continue;

            uniqueIds.Add(entryId.Trim().ToLowerInvariant());
        }

        normalized.AddRange(uniqueIds);
        normalized.Sort(StringComparer.Ordinal);
        return normalized;
    }
}

[Serializable]
public sealed class ChapterSaveData
{
    public string chapterId = string.Empty;
    public string state = "InProgress";
    public bool isUnlocked;
    public bool completedEver;
    public int completionCount;
    public string startedAtUtc = string.Empty;
    public string completedAtUtc = string.Empty;
    public string firstCompletedAtUtc = string.Empty;
    public ChapterCheckpointSaveData checkpoint = new();
    public List<MissionSaveData> missions = new();
    public List<string> worldFlags = new();
    public ChapterQuizSaveData quiz = new();
    public ChapterAnalyticsSaveData analytics = new();
    public OfficialChapterAnalyticsSaveData officialAnalytics = new();

    public void Normalize()
    {
        checkpoint ??= new ChapterCheckpointSaveData();
        checkpoint.Normalize();
        missions ??= new List<MissionSaveData>();

        for (int i = missions.Count - 1; i >= 0; i--)
        {
            if (missions[i] == null || string.IsNullOrWhiteSpace(missions[i].missionId))
            {
                missions.RemoveAt(i);
                continue;
            }

            missions[i].Normalize();
        }

        worldFlags = NormalizeWorldFlags(worldFlags);
        quiz ??= new ChapterQuizSaveData();
        quiz.Normalize();
        quiz.MigrateLegacyOfficialResult(
            completedEver &&
            completionCount <= 1 &&
            string.Equals(state, "Completed", StringComparison.Ordinal));
        analytics ??= new ChapterAnalyticsSaveData();
        analytics.Normalize();
        analytics.TryFinalizeEngagementScore(quiz.officialAttempt, completedEver);
        officialAnalytics ??= new OfficialChapterAnalyticsSaveData();
        officialAnalytics.Normalize();

        if (!officialAnalytics.isRecorded && completedEver && completionCount <= 1)
        {
            string officialTimestamp = !string.IsNullOrWhiteSpace(firstCompletedAtUtc)
                ? firstCompletedAtUtc
                : completedAtUtc;
            officialAnalytics.RecordIfMissing(
                quiz.officialAttempt,
                analytics,
                officialTimestamp);
        }
    }

    public bool HasWorldFlag(string flagId)
    {
        return !string.IsNullOrWhiteSpace(flagId) &&
               worldFlags != null &&
               worldFlags.Contains(flagId.Trim());
    }

    public bool AddWorldFlag(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
            return false;

        worldFlags ??= new List<string>();
        string normalizedFlag = flagId.Trim();

        if (worldFlags.Contains(normalizedFlag))
            return false;

        worldFlags.Add(normalizedFlag);
        worldFlags.Sort(StringComparer.Ordinal);
        return true;
    }

    private static List<string> NormalizeWorldFlags(List<string> source)
    {
        HashSet<string> uniqueFlags = new(StringComparer.Ordinal);

        if (source != null)
        {
            foreach (string flag in source)
            {
                if (!string.IsNullOrWhiteSpace(flag))
                    uniqueFlags.Add(flag.Trim());
            }
        }

        List<string> normalizedFlags = new(uniqueFlags);
        normalizedFlags.Sort(StringComparer.Ordinal);
        return normalizedFlags;
    }
}

[Serializable]
public sealed class ChapterQuizSaveData
{
    public string state = QuizProgressState.NotStarted.ToString();
    public bool isPracticeAttempt;
    public int attemptNumber;
    public int selectionSeed;
    public string languageCode = "en";
    public List<string> selectedQuestionIds = new();
    public List<QuizOptionOrderSaveData> optionOrders = new();
    public List<QuizAnswerSaveData> answers = new();
    public int score;
    public int maxScore;
    public string startedAtUtc = string.Empty;
    public string submittedAtUtc = string.Empty;
    public string completedAtUtc = string.Empty;
    public QuizAttemptResultSaveData officialAttempt = new();
    public List<QuizAttemptResultSaveData> practiceAttempts = new();

    public bool HasOfficialResult => officialAttempt?.isRecorded == true;

    public void Normalize()
    {
        if (!Enum.TryParse(state, false, out QuizProgressState _))
            state = QuizProgressState.NotStarted.ToString();

        attemptNumber = Math.Max(0, attemptNumber);
        score = Math.Max(0, score);
        maxScore = Math.Max(0, maxScore);
        languageCode = string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode.Trim();
        selectedQuestionIds ??= new List<string>();
        optionOrders ??= new List<QuizOptionOrderSaveData>();
        answers ??= new List<QuizAnswerSaveData>();
        officialAttempt ??= new QuizAttemptResultSaveData();
        officialAttempt.Normalize();
        practiceAttempts ??= new List<QuizAttemptResultSaveData>();

        for (int index = practiceAttempts.Count - 1; index >= 0; index--)
        {
            QuizAttemptResultSaveData attempt = practiceAttempts[index];

            if (attempt == null || !attempt.isRecorded)
            {
                practiceAttempts.RemoveAt(index);
                continue;
            }

            attempt.Normalize();
        }

        HashSet<string> uniqueQuestions = new(StringComparer.Ordinal);
        List<string> normalizedQuestions = new();

        foreach (string questionId in selectedQuestionIds)
        {
            if (string.IsNullOrWhiteSpace(questionId))
                continue;

            string normalizedId = questionId.Trim();

            if (uniqueQuestions.Add(normalizedId))
                normalizedQuestions.Add(normalizedId);
        }

        selectedQuestionIds = normalizedQuestions;

        HashSet<string> orderedQuestions = new(StringComparer.Ordinal);

        for (int i = optionOrders.Count - 1; i >= 0; i--)
        {
            QuizOptionOrderSaveData order = optionOrders[i];

            if (order == null || string.IsNullOrWhiteSpace(order.questionId))
            {
                optionOrders.RemoveAt(i);
                continue;
            }

            order.Normalize();

            if (order.optionIds.Count == 0 || !orderedQuestions.Add(order.questionId))
                optionOrders.RemoveAt(i);
        }

        HashSet<string> answeredQuestions = new(StringComparer.Ordinal);

        for (int i = answers.Count - 1; i >= 0; i--)
        {
            QuizAnswerSaveData answer = answers[i];

            if (answer == null ||
                string.IsNullOrWhiteSpace(answer.questionId) ||
                string.IsNullOrWhiteSpace(answer.selectedAnswerId))
            {
                answers.RemoveAt(i);
                continue;
            }

            answer.questionId = answer.questionId.Trim();
            answer.selectedAnswerId = answer.selectedAnswerId.Trim();

            if (!answeredQuestions.Add(answer.questionId))
                answers.RemoveAt(i);
        }
    }

    public ChapterQuizSaveData CreateFreshAttempt()
    {
        Normalize();
        List<QuizAttemptResultSaveData> preservedPracticeAttempts = new();

        foreach (QuizAttemptResultSaveData attempt in practiceAttempts)
            preservedPracticeAttempts.Add(attempt.Clone());

        return new ChapterQuizSaveData
        {
            languageCode = languageCode,
            officialAttempt = officialAttempt.Clone(),
            practiceAttempts = preservedPracticeAttempts
        };
    }

    public void RecordOfficialResultIfMissing()
    {
        officialAttempt ??= new QuizAttemptResultSaveData();

        if (!officialAttempt.isRecorded)
            officialAttempt.CopyFrom(this);
    }

    public void RecordPracticeResult()
    {
        practiceAttempts ??= new List<QuizAttemptResultSaveData>();
        QuizAttemptResultSaveData recordedAttempt = new();
        recordedAttempt.CopyFrom(this);
        practiceAttempts.Add(recordedAttempt);
    }

    public void MigrateLegacyOfficialResult(bool shouldMigrate)
    {
        if (!shouldMigrate || HasOfficialResult || maxScore <= 0)
            return;

        if (string.Equals(state, QuizProgressState.Submitted.ToString(), StringComparison.Ordinal) ||
            string.Equals(state, QuizProgressState.Completed.ToString(), StringComparison.Ordinal))
        {
            RecordOfficialResultIfMissing();
        }
    }

    public string GetSelectedAnswerId(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId) || answers == null)
            return string.Empty;

        foreach (QuizAnswerSaveData answer in answers)
        {
            if (answer != null &&
                string.Equals(answer.questionId, questionId, StringComparison.Ordinal))
            {
                return answer.selectedAnswerId ?? string.Empty;
            }
        }

        return string.Empty;
    }

    public void SetAnswer(string questionId, string answerId)
    {
        if (string.IsNullOrWhiteSpace(questionId) || string.IsNullOrWhiteSpace(answerId))
            return;

        answers ??= new List<QuizAnswerSaveData>();

        foreach (QuizAnswerSaveData answer in answers)
        {
            if (answer != null &&
                string.Equals(answer.questionId, questionId, StringComparison.Ordinal))
            {
                answer.selectedAnswerId = answerId.Trim();
                return;
            }
        }

        answers.Add(new QuizAnswerSaveData
        {
            questionId = questionId.Trim(),
            selectedAnswerId = answerId.Trim()
        });
    }

    public void RemoveAnswer(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId) || answers == null)
            return;

        for (int index = answers.Count - 1; index >= 0; index--)
        {
            QuizAnswerSaveData answer = answers[index];

            if (answer != null &&
                string.Equals(answer.questionId, questionId, StringComparison.Ordinal))
            {
                answers.RemoveAt(index);
            }
        }
    }

    public IReadOnlyList<string> GetOptionOrder(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId) || optionOrders == null)
            return Array.Empty<string>();

        foreach (QuizOptionOrderSaveData order in optionOrders)
        {
            if (order != null &&
                string.Equals(order.questionId, questionId, StringComparison.Ordinal))
            {
                return order.optionIds;
            }
        }

        return Array.Empty<string>();
    }

    public void SetOptionOrder(string questionId, IEnumerable<string> optionIds)
    {
        if (string.IsNullOrWhiteSpace(questionId))
            return;

        optionOrders ??= new List<QuizOptionOrderSaveData>();
        QuizOptionOrderSaveData order = null;

        foreach (QuizOptionOrderSaveData existing in optionOrders)
        {
            if (existing != null &&
                string.Equals(existing.questionId, questionId, StringComparison.Ordinal))
            {
                order = existing;
                break;
            }
        }

        if (order == null)
        {
            order = new QuizOptionOrderSaveData { questionId = questionId.Trim() };
            optionOrders.Add(order);
        }

        order.optionIds = optionIds != null ? new List<string>(optionIds) : new List<string>();
        order.Normalize();
    }
}

[Serializable]
public sealed class QuizAttemptResultSaveData
{
    public bool isRecorded;
    public int attemptNumber;
    public int selectionSeed;
    public string languageCode = "en";
    public List<string> selectedQuestionIds = new();
    public List<QuizOptionOrderSaveData> optionOrders = new();
    public List<QuizAnswerSaveData> answers = new();
    public int score;
    public int maxScore;
    public string startedAtUtc = string.Empty;
    public string submittedAtUtc = string.Empty;
    public string completedAtUtc = string.Empty;

    public void CopyFrom(ChapterQuizSaveData source)
    {
        if (source == null)
            return;

        isRecorded = true;
        attemptNumber = source.attemptNumber;
        selectionSeed = source.selectionSeed;
        languageCode = source.languageCode;
        selectedQuestionIds = new List<string>(source.selectedQuestionIds);
        optionOrders = CloneOptionOrders(source.optionOrders);
        answers = CloneAnswers(source.answers);
        score = source.score;
        maxScore = source.maxScore;
        startedAtUtc = source.startedAtUtc;
        submittedAtUtc = source.submittedAtUtc;
        completedAtUtc = source.completedAtUtc;
        Normalize();
    }

    public QuizAttemptResultSaveData Clone()
    {
        return new QuizAttemptResultSaveData
        {
            isRecorded = isRecorded,
            attemptNumber = attemptNumber,
            selectionSeed = selectionSeed,
            languageCode = languageCode,
            selectedQuestionIds = new List<string>(selectedQuestionIds),
            optionOrders = CloneOptionOrders(optionOrders),
            answers = CloneAnswers(answers),
            score = score,
            maxScore = maxScore,
            startedAtUtc = startedAtUtc,
            submittedAtUtc = submittedAtUtc,
            completedAtUtc = completedAtUtc
        };
    }

    public void Normalize()
    {
        attemptNumber = Math.Max(0, attemptNumber);
        score = Math.Max(0, score);
        maxScore = Math.Max(0, maxScore);
        languageCode = string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode.Trim();
        selectedQuestionIds ??= new List<string>();
        optionOrders ??= new List<QuizOptionOrderSaveData>();
        answers ??= new List<QuizAnswerSaveData>();

        for (int index = optionOrders.Count - 1; index >= 0; index--)
        {
            if (optionOrders[index] == null)
                optionOrders.RemoveAt(index);
            else
                optionOrders[index].Normalize();
        }

        for (int index = answers.Count - 1; index >= 0; index--)
        {
            QuizAnswerSaveData answer = answers[index];

            if (answer == null || string.IsNullOrWhiteSpace(answer.questionId))
                answers.RemoveAt(index);
        }
    }

    private static List<QuizOptionOrderSaveData> CloneOptionOrders(
        IEnumerable<QuizOptionOrderSaveData> source)
    {
        List<QuizOptionOrderSaveData> clones = new();

        if (source == null)
            return clones;

        foreach (QuizOptionOrderSaveData order in source)
        {
            if (order == null)
                continue;

            clones.Add(new QuizOptionOrderSaveData
            {
                questionId = order.questionId,
                optionIds = order.optionIds != null
                    ? new List<string>(order.optionIds)
                    : new List<string>()
            });
        }

        return clones;
    }

    private static List<QuizAnswerSaveData> CloneAnswers(IEnumerable<QuizAnswerSaveData> source)
    {
        List<QuizAnswerSaveData> clones = new();

        if (source == null)
            return clones;

        foreach (QuizAnswerSaveData answer in source)
        {
            if (answer == null)
                continue;

            clones.Add(new QuizAnswerSaveData
            {
                questionId = answer.questionId,
                selectedAnswerId = answer.selectedAnswerId
            });
        }

        return clones;
    }
}

[Serializable]
public sealed class QuizOptionOrderSaveData
{
    public string questionId = string.Empty;
    public List<string> optionIds = new();

    public void Normalize()
    {
        questionId = questionId?.Trim() ?? string.Empty;
        optionIds ??= new List<string>();
        HashSet<string> uniqueIds = new(StringComparer.Ordinal);
        List<string> normalizedIds = new();

        foreach (string optionId in optionIds)
        {
            if (string.IsNullOrWhiteSpace(optionId))
                continue;

            string normalizedId = optionId.Trim();

            if (uniqueIds.Add(normalizedId))
                normalizedIds.Add(normalizedId);
        }

        optionIds = normalizedIds;
    }
}

[Serializable]
public sealed class QuizAnswerSaveData
{
    public string questionId = string.Empty;
    public string selectedAnswerId = string.Empty;
}

public enum QuizProgressState
{
    NotStarted,
    InProgress,
    Submitted,
    Completed
}

[Serializable]
public sealed class ChapterCheckpointSaveData
{
    public bool hasPosition;
    public string sceneName = string.Empty;
    public SavePositionData position = new();

    public void Normalize()
    {
        position ??= new SavePositionData();
    }
}

[Serializable]
public sealed class SavePositionData
{
    public float x;
    public float y;

    public Vector2 ToVector2()
    {
        return new Vector2(x, y);
    }

    public void Set(Vector2 value)
    {
        x = value.x;
        y = value.y;
    }
}

[Serializable]
public sealed class MissionSaveData
{
    public string missionId = string.Empty;
    public string state = "Locked";
    public int currentStepIndex;
    public MissionStepProgressSaveData stepProgress = new();

    public void Normalize()
    {
        stepProgress ??= new MissionStepProgressSaveData();
        stepProgress.Normalize();
    }
}

[Serializable]
public sealed class MissionStepProgressSaveData
{
    public List<string> completedTargetIds = new();

    public void Normalize()
    {
        List<string> normalizedIds = new();
        HashSet<string> uniqueIds = new(StringComparer.Ordinal);

        if (completedTargetIds != null)
        {
            foreach (string targetId in completedTargetIds)
            {
                if (string.IsNullOrWhiteSpace(targetId))
                    continue;

                uniqueIds.Add(targetId.Trim().ToLowerInvariant());
            }
        }

        normalizedIds.AddRange(uniqueIds);
        normalizedIds.Sort(StringComparer.Ordinal);
        completedTargetIds = normalizedIds;
    }
}

[Serializable]
public sealed class OfficialChapterAnalyticsSaveData
{
    public bool isRecorded;
    public string recordedAtUtc = string.Empty;
    public int quizScore;
    public int quizMaxScore;
    public double quizScoreRatePercent;
    public bool hasEngagementScore;
    public double engagementRatePercent;
    public double dialogueSkipRatePercent;
    public double artifactDiscoveryRatePercent;
    public double playTimeSeconds;

    public bool RecordIfMissing(
        QuizAttemptResultSaveData officialQuizResult,
        ChapterAnalyticsSaveData analytics,
        string timestampUtc)
    {
        if (isRecorded)
            return false;

        if (officialQuizResult?.isRecorded != true ||
            analytics == null ||
            !analytics.hasEngagementScore)
            return false;

        analytics.Normalize();
        quizScore = Math.Max(0, officialQuizResult.score);
        quizMaxScore = Math.Max(0, officialQuizResult.maxScore);
        quizScoreRatePercent = quizMaxScore > 0
            ? Math.Clamp(quizScore * 100d / quizMaxScore, 0d, 100d)
            : 0d;
        hasEngagementScore = analytics.hasEngagementScore;
        engagementRatePercent = hasEngagementScore
            ? analytics.engagementRatePercent
            : 0d;
        dialogueSkipRatePercent = analytics.missionConversationSkipRatePercent;
        artifactDiscoveryRatePercent = analytics.artifactDiscoveryRatePercent;
        playTimeSeconds = Math.Max(0d, analytics.playTimeSeconds);
        recordedAtUtc = timestampUtc?.Trim() ?? string.Empty;
        isRecorded = true;
        Normalize();
        return true;
    }

    public void Normalize()
    {
        recordedAtUtc ??= string.Empty;
        quizScore = Math.Max(0, quizScore);
        quizMaxScore = Math.Max(0, quizMaxScore);
        quizScoreRatePercent = Math.Clamp(quizScoreRatePercent, 0d, 100d);
        engagementRatePercent = hasEngagementScore
            ? Math.Clamp(engagementRatePercent, 0d, 100d)
            : 0d;
        dialogueSkipRatePercent = Math.Clamp(dialogueSkipRatePercent, 0d, 100d);
        artifactDiscoveryRatePercent = Math.Clamp(artifactDiscoveryRatePercent, 0d, 100d);
        playTimeSeconds = Math.Max(0d, playTimeSeconds);
    }
}

[Serializable]
public sealed class ChapterAnalyticsSaveData
{
    private const double QuizWeight = 0.40d;
    private const double ArtifactDiscoveryWeight = 0.35d;
    private const double DialogueAttentionWeight = 0.25d;

    public int sessionCount;
    public int chapterRestarts;
    public double playTimeSeconds;
    public int missionStepsCompleted;
    public int artifactsUnlocked;
    public int artifactsAvailable;
    public double artifactDiscoveryRatePercent;
    public int charactersUnlocked;
    public int dialogueInteractions;
    public int doorTransitions;
    public int missionConversationsCompleted;
    public int missionConversationLinesViewed;
    public int missionConversationLinesSkipped;
    public double missionConversationSkipRatePercent;
    public bool hasEngagementScore;
    public double engagementRatePercent;
    public List<AnalyticsCounterSaveData> customCounters = new();

    public void RecordArtifactDiscovery(int totalArtifactCount)
    {
        artifactsUnlocked = Math.Max(0, artifactsUnlocked) + 1;
        artifactsAvailable = Math.Max(artifactsAvailable, Math.Max(0, totalArtifactCount));
        UpdateArtifactDiscoveryRate();
    }

    public void SetArtifactsAvailable(int totalArtifactCount)
    {
        artifactsAvailable = Math.Max(artifactsAvailable, Math.Max(0, totalArtifactCount));
        UpdateArtifactDiscoveryRate();
    }

    public void RecordMissionConversationReading(int linesViewed, int linesSkipped)
    {
        int safeLinesViewed = Math.Max(0, linesViewed);
        int safeLinesSkipped = Math.Clamp(linesSkipped, 0, safeLinesViewed);

        missionConversationsCompleted = Math.Max(0, missionConversationsCompleted) + 1;
        missionConversationLinesViewed = Math.Max(0, missionConversationLinesViewed) +
                                         safeLinesViewed;
        missionConversationLinesSkipped = Math.Max(0, missionConversationLinesSkipped) +
                                          safeLinesSkipped;
        UpdateMissionConversationSkipRate();
    }

    public bool TryFinalizeEngagementScore(
        QuizAttemptResultSaveData officialQuizResult,
        bool chapterCompleted)
    {
        if (hasEngagementScore)
            return true;

        if (!chapterCompleted ||
            officialQuizResult?.isRecorded != true ||
            officialQuizResult.maxScore <= 0 ||
            artifactsAvailable <= 0 ||
            missionConversationLinesViewed <= 0)
        {
            engagementRatePercent = 0d;
            return false;
        }

        double quizRate = Math.Clamp(
            officialQuizResult.score * 100d / officialQuizResult.maxScore,
            0d,
            100d);
        double dialogueAttentionRate = 100d - missionConversationSkipRatePercent;

        engagementRatePercent =
            quizRate * QuizWeight +
            artifactDiscoveryRatePercent * ArtifactDiscoveryWeight +
            dialogueAttentionRate * DialogueAttentionWeight;
        engagementRatePercent = Math.Clamp(engagementRatePercent, 0d, 100d);
        hasEngagementScore = true;
        return true;
    }

    public void Normalize()
    {
        artifactsUnlocked = Math.Max(0, artifactsUnlocked);
        artifactsAvailable = Math.Max(0, artifactsAvailable);
        dialogueInteractions = Math.Max(0, dialogueInteractions);
        missionConversationsCompleted = Math.Max(0, missionConversationsCompleted);
        missionConversationLinesViewed = Math.Max(0, missionConversationLinesViewed);
        missionConversationLinesSkipped = Math.Clamp(
            missionConversationLinesSkipped,
            0,
            missionConversationLinesViewed);
        customCounters ??= new List<AnalyticsCounterSaveData>();
        engagementRatePercent = hasEngagementScore
            ? Math.Clamp(engagementRatePercent, 0d, 100d)
            : 0d;
        UpdateArtifactDiscoveryRate();
        UpdateMissionConversationSkipRate();
    }

    private void UpdateArtifactDiscoveryRate()
    {
        artifactDiscoveryRatePercent = artifactsAvailable > 0
            ? Math.Min(artifactsUnlocked, artifactsAvailable) * 100d / artifactsAvailable
            : 0d;
    }

    private void UpdateMissionConversationSkipRate()
    {
        missionConversationSkipRatePercent = missionConversationLinesViewed > 0
            ? missionConversationLinesSkipped * 100d / missionConversationLinesViewed
            : 0d;
    }
}

[Serializable]
public sealed class AnalyticsCounterSaveData
{
    public string key = string.Empty;
    public double value;
}
