using NUnit.Framework;

public sealed class GlobalAnalyticsSubmissionTests
{
    [SetUp]
    public void SetUp()
    {
        PlayerSession.ReturnToGuest();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerSession.ReturnToGuest();
    }

    [Test]
    public void GuestCannotCreateSubmission()
    {
        bool created = GlobalAnalyticsSubmissionFactory.TryCreateForCurrentAccount(
            CreateSaveWithOfficialChapter(),
            out _,
            out string error);

        Assert.That(created, Is.False);
        Assert.That(error, Does.Contain("Guest"));
    }

    [Test]
    public void SignedInAccountCreatesPacketFromFrozenResultsOnly()
    {
        Assert.That(PlayerSession.TryBeginAccountSession(
            new AccountProfile
            {
                accountId = "account-123",
                username = "meep",
                inGameName = "Meep"
            },
            out string signInError), Is.True, signInError);

        GameSaveData save = CreateSaveWithOfficialChapter();
        save.GetOrCreateChapter("chapter_2");

        bool created = GlobalAnalyticsSubmissionFactory.TryCreateForCurrentAccount(
            save,
            out GlobalAnalyticsSubmission submission,
            out string error);

        Assert.That(created, Is.True, error);
        Assert.That(submission.accountId, Is.EqualTo("account-123"));
        Assert.That(submission.playthroughId, Is.EqualTo("playthrough-123"));
        Assert.That(submission.chapters, Has.Count.EqualTo(1));
        Assert.That(submission.chapters[0].chapterId, Is.EqualTo("chapter_1"));
        Assert.That(submission.chapters[0].quizScoreRatePercent, Is.EqualTo(80d));
        Assert.That(submission.chapters[0].engagementRatePercent, Is.EqualTo(73d));
        Assert.That(submission.chapters[0].dialogueSkipRatePercent, Is.EqualTo(20d));
        Assert.That(submission.chapters[0].artifactDiscoveryRatePercent, Is.EqualTo(60d));
        Assert.That(submission.chapters[0].playTimeSeconds, Is.EqualTo(900d));
    }

    private static GameSaveData CreateSaveWithOfficialChapter()
    {
        GameSaveData save = new()
        {
            playthroughId = "playthrough-123",
            gameVersion = "test-version",
            createdAtUtc = "playthrough-created"
        };
        ChapterSaveData chapter = save.GetOrCreateChapter("chapter_1");
        chapter.officialAnalytics = new OfficialChapterAnalyticsSaveData
        {
            isRecorded = true,
            recordedAtUtc = "chapter-completed",
            quizScore = 8,
            quizMaxScore = 10,
            quizScoreRatePercent = 80d,
            hasEngagementScore = true,
            engagementRatePercent = 73d,
            dialogueSkipRatePercent = 20d,
            artifactDiscoveryRatePercent = 60d,
            playTimeSeconds = 900d
        };
        return save;
    }
}
