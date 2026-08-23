using System;
using System.Collections.Generic;

[Serializable]
public sealed class GlobalAnalyticsSubmission
{
    public int schemaVersion = 1;
    public string accountId = string.Empty;
    public string playthroughId = string.Empty;
    public string gameVersion = string.Empty;
    public string playthroughCreatedAtUtc = string.Empty;
    public List<GlobalChapterAnalyticsResult> chapters = new();
}

[Serializable]
public sealed class GlobalChapterAnalyticsResult
{
    public string chapterId = string.Empty;
    public string recordedAtUtc = string.Empty;
    public int quizScore;
    public int quizMaxScore;
    public double quizScoreRatePercent;
    public bool hasEngagementScore;
    public double engagementRatePercent;
    public double dialogueSkipRatePercent;
    public double artifactDiscoveryRatePercent;
    public double playTimeSeconds;
}

public static class GlobalAnalyticsSubmissionFactory
{
    public static bool TryCreateForCurrentAccount(
        GameSaveData saveData,
        out GlobalAnalyticsSubmission submission,
        out string error)
    {
        submission = null;

        if (!PlayerSession.CanSubmitGlobalAnalytics ||
            string.IsNullOrWhiteSpace(PlayerSession.AccountId))
        {
            error = "Guest saves cannot submit Global Analytics.";
            return false;
        }

        if (saveData == null)
        {
            error = "A save file is required for Global Analytics.";
            return false;
        }

        saveData.Normalize();
        GlobalAnalyticsSubmission created = new()
        {
            accountId = PlayerSession.AccountId,
            playthroughId = saveData.playthroughId,
            gameVersion = saveData.gameVersion?.Trim() ?? string.Empty,
            playthroughCreatedAtUtc = saveData.createdAtUtc?.Trim() ?? string.Empty
        };

        foreach (ChapterSaveData chapter in saveData.chapters)
        {
            if (chapter?.officialAnalytics?.isRecorded != true ||
                !chapter.officialAnalytics.hasEngagementScore)
                continue;

            OfficialChapterAnalyticsSaveData official = chapter.officialAnalytics;
            created.chapters.Add(new GlobalChapterAnalyticsResult
            {
                chapterId = chapter.chapterId?.Trim() ?? string.Empty,
                recordedAtUtc = official.recordedAtUtc,
                quizScore = official.quizScore,
                quizMaxScore = official.quizMaxScore,
                quizScoreRatePercent = official.quizScoreRatePercent,
                hasEngagementScore = official.hasEngagementScore,
                engagementRatePercent = official.engagementRatePercent,
                dialogueSkipRatePercent = official.dialogueSkipRatePercent,
                artifactDiscoveryRatePercent = official.artifactDiscoveryRatePercent,
                playTimeSeconds = official.playTimeSeconds
            });
        }

        if (created.chapters.Count == 0)
        {
            error = "This save has no completed chapter analytics to submit.";
            return false;
        }

        created.chapters.Sort((left, right) => string.Compare(
            left.chapterId,
            right.chapterId,
            StringComparison.Ordinal));
        submission = created;
        error = string.Empty;
        return true;
    }
}
