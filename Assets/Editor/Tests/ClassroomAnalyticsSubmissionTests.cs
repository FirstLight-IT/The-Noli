using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

public sealed class ClassroomAnalyticsSubmissionTests
{
    [Test]
    public void SubmissionIncludesOnlyFinalizedOfficialChapterAnalytics()
    {
        GameSaveData saveData = new()
        {
            playthroughId = "playthrough_1",
            chapters = new List<ChapterSaveData>
            {
                new()
                {
                    chapterId = "chapter_1",
                    officialAnalytics = new OfficialChapterAnalyticsSaveData
                    {
                        isRecorded = true,
                        recordedAtUtc = "2026-08-28T00:00:00Z",
                        quizScore = 8,
                        quizMaxScore = 10,
                        quizScoreRatePercent = 80d,
                        hasEngagementScore = true,
                        engagementRatePercent = 73d,
                        dialogueSkipRatePercent = 20d,
                        artifactDiscoveryRatePercent = 60d,
                        playTimeSeconds = 300d
                    }
                },
                new()
                {
                    chapterId = "chapter_2",
                    analytics = new ChapterAnalyticsSaveData
                    {
                        engagementRatePercent = 95d
                    }
                }
            }
        };

        MethodInfo factory = typeof(ClassroomProgressSyncService).GetMethod(
            "CreateSubmission",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(factory, Is.Not.Null);

        ClassroomProgressSubmission submission =
            (ClassroomProgressSubmission)factory.Invoke(
                null,
                new object[] { "room_1", saveData });

        Assert.That(submission.schemaVersion, Is.EqualTo(2));
        Assert.That(submission.chapters, Has.Count.EqualTo(1));
        Assert.That(submission.chapters[0].chapterId, Is.EqualTo("chapter_1"));
        Assert.That(submission.chapters[0].engagementRatePercent, Is.EqualTo(73d));
    }
}
