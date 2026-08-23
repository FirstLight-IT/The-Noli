using NUnit.Framework;

public sealed class EngagementScoreTests
{
    [Test]
    public void Finalize_CalculatesWeightedScoreAfterOfficialCompletion()
    {
        ChapterAnalyticsSaveData analytics = CreateCompleteAnalytics();
        QuizAttemptResultSaveData quiz = CreateQuiz(score: 8, maxScore: 10);

        bool finalized = analytics.TryFinalizeEngagementScore(quiz, chapterCompleted: true);

        Assert.That(finalized, Is.True);
        Assert.That(analytics.hasEngagementScore, Is.True);
        Assert.That(analytics.engagementRatePercent, Is.EqualTo(73d));
    }

    [Test]
    public void Finalize_BeforeChapterCompletionLeavesScoreUnavailable()
    {
        ChapterAnalyticsSaveData analytics = CreateCompleteAnalytics();

        bool finalized = analytics.TryFinalizeEngagementScore(
            CreateQuiz(score: 8, maxScore: 10),
            chapterCompleted: false);

        Assert.That(finalized, Is.False);
        Assert.That(analytics.hasEngagementScore, Is.False);
        Assert.That(analytics.engagementRatePercent, Is.Zero);
    }

    [Test]
    public void Finalize_RequiresEveryScoreComponent()
    {
        ChapterAnalyticsSaveData analytics = CreateCompleteAnalytics();
        analytics.missionConversationLinesViewed = 0;
        analytics.Normalize();

        bool finalized = analytics.TryFinalizeEngagementScore(
            CreateQuiz(score: 8, maxScore: 10),
            chapterCompleted: true);

        Assert.That(finalized, Is.False);
        Assert.That(analytics.hasEngagementScore, Is.False);
    }

    [Test]
    public void FinalizedScoreDoesNotChangeAfterLaterAnalyticsUpdates()
    {
        ChapterAnalyticsSaveData analytics = CreateCompleteAnalytics();
        QuizAttemptResultSaveData quiz = CreateQuiz(score: 8, maxScore: 10);
        analytics.TryFinalizeEngagementScore(quiz, chapterCompleted: true);

        analytics.RecordArtifactDiscovery(25);
        analytics.RecordMissionConversationReading(10, 10);
        analytics.TryFinalizeEngagementScore(quiz, chapterCompleted: true);

        Assert.That(analytics.engagementRatePercent, Is.EqualTo(73d));
    }

    private static ChapterAnalyticsSaveData CreateCompleteAnalytics()
    {
        ChapterAnalyticsSaveData analytics = new()
        {
            artifactsUnlocked = 15,
            artifactsAvailable = 25,
            missionConversationLinesViewed = 10,
            missionConversationLinesSkipped = 2
        };
        analytics.Normalize();
        return analytics;
    }

    private static QuizAttemptResultSaveData CreateQuiz(int score, int maxScore) => new()
    {
        isRecorded = true,
        score = score,
        maxScore = maxScore
    };
}
