using NUnit.Framework;

public sealed class OfficialChapterAnalyticsTests
{
    [Test]
    public void RecordIfMissing_CapturesTheFiveDashboardMetrics()
    {
        OfficialChapterAnalyticsSaveData snapshot = new();
        ChapterAnalyticsSaveData analytics = CreateAnalytics();

        bool recorded = snapshot.RecordIfMissing(
            CreateQuiz(8, 10),
            analytics,
            "2026-08-23T04:00:00Z");

        Assert.That(recorded, Is.True);
        Assert.That(snapshot.isRecorded, Is.True);
        Assert.That(snapshot.quizScoreRatePercent, Is.EqualTo(80d));
        Assert.That(snapshot.engagementRatePercent, Is.EqualTo(73d));
        Assert.That(snapshot.dialogueSkipRatePercent, Is.EqualTo(20d));
        Assert.That(snapshot.artifactDiscoveryRatePercent, Is.EqualTo(60d));
        Assert.That(snapshot.playTimeSeconds, Is.EqualTo(900d));
    }

    [Test]
    public void RecordIfMissing_DoesNotChangeAfterLaterReplayActivity()
    {
        OfficialChapterAnalyticsSaveData snapshot = new();
        ChapterAnalyticsSaveData analytics = CreateAnalytics();
        snapshot.RecordIfMissing(CreateQuiz(8, 10), analytics, "first");

        analytics.playTimeSeconds = 5000d;
        analytics.RecordArtifactDiscovery(25);
        analytics.RecordMissionConversationReading(10, 10);

        bool recordedAgain = snapshot.RecordIfMissing(
            CreateQuiz(10, 10),
            analytics,
            "replay");

        Assert.That(recordedAgain, Is.False);
        Assert.That(snapshot.recordedAtUtc, Is.EqualTo("first"));
        Assert.That(snapshot.quizScoreRatePercent, Is.EqualTo(80d));
        Assert.That(snapshot.engagementRatePercent, Is.EqualTo(73d));
        Assert.That(snapshot.dialogueSkipRatePercent, Is.EqualTo(20d));
        Assert.That(snapshot.artifactDiscoveryRatePercent, Is.EqualTo(60d));
        Assert.That(snapshot.playTimeSeconds, Is.EqualTo(900d));
    }

    [Test]
    public void RecordIfMissing_RequiresAnOfficialQuizResult()
    {
        OfficialChapterAnalyticsSaveData snapshot = new();

        bool recorded = snapshot.RecordIfMissing(
            new QuizAttemptResultSaveData(),
            CreateAnalytics(),
            "now");

        Assert.That(recorded, Is.False);
        Assert.That(snapshot.isRecorded, Is.False);
    }

    [Test]
    public void RecordIfMissing_RequiresAFinalizedEngagementScore()
    {
        OfficialChapterAnalyticsSaveData snapshot = new();
        ChapterAnalyticsSaveData incompleteAnalytics = new()
        {
            playTimeSeconds = 100d
        };

        bool recorded = snapshot.RecordIfMissing(
            CreateQuiz(8, 10),
            incompleteAnalytics,
            "now");

        Assert.That(recorded, Is.False);
        Assert.That(snapshot.isRecorded, Is.False);
    }

    [Test]
    public void Normalize_MigratesAFirstCompletionButNotAReplayedChapter()
    {
        ChapterSaveData firstCompletion = CreateCompletedChapter(completionCount: 1);
        ChapterSaveData replayedChapter = CreateCompletedChapter(completionCount: 2);

        firstCompletion.Normalize();
        replayedChapter.Normalize();

        Assert.That(firstCompletion.officialAnalytics.isRecorded, Is.True);
        Assert.That(replayedChapter.officialAnalytics.isRecorded, Is.False);
    }

    private static ChapterAnalyticsSaveData CreateAnalytics()
    {
        ChapterAnalyticsSaveData analytics = new()
        {
            playTimeSeconds = 900d,
            artifactsUnlocked = 15,
            artifactsAvailable = 25,
            missionConversationLinesViewed = 10,
            missionConversationLinesSkipped = 2
        };
        analytics.Normalize();
        analytics.TryFinalizeEngagementScore(CreateQuiz(8, 10), chapterCompleted: true);
        return analytics;
    }

    private static QuizAttemptResultSaveData CreateQuiz(int score, int maxScore) => new()
    {
        isRecorded = true,
        score = score,
        maxScore = maxScore
    };

    private static ChapterSaveData CreateCompletedChapter(int completionCount)
    {
        ChapterSaveData chapter = new()
        {
            completedEver = true,
            completionCount = completionCount,
            firstCompletedAtUtc = "first-completion",
            analytics = CreateAnalytics()
        };
        chapter.quiz.officialAttempt = CreateQuiz(8, 10);
        return chapter;
    }
}
