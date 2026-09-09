using System.Collections.Generic;
using NUnit.Framework;

public sealed class ConversationSkipTrackerTests
{
    [Test]
    public void ConversationInterruption_ResolvesOnlyAfterConfiguredLine()
    {
        ConversationInterruption configured = new()
        {
            interruptionId = "sala_music_interruption",
            afterLineIndex = 7,
            minigameSceneName = "Social Interruption Minigame"
        };
        Conversation conversation = new()
        {
            interruptions = new List<ConversationInterruption> { configured }
        };

        Assert.That(conversation.TryGetInterruptionAfterLine(6, out _), Is.False);
        Assert.That(conversation.TryGetInterruptionAfterLine(7, out ConversationInterruption result), Is.True);
        Assert.That(result, Is.SameAs(configured));
    }

    [Test]
    public void TypewriterSkipFollowedByRapidAdvance_CountsAsSkipped()
    {
        ConversationSkipTracker tracker = new(1f);

        tracker.BeginLine();
        tracker.MarkTypewriterSkipped(10f);
        tracker.CompleteLine(10.75f);

        ConversationReadingResult result = tracker.CompleteConversation("test_conversation");

        Assert.That(result.LinesViewed, Is.EqualTo(1));
        Assert.That(result.TypewriterSkippedLines, Is.EqualTo(1));
        Assert.That(result.RapidlySkippedLines, Is.EqualTo(1));
        Assert.That(result.SkipRatePercent, Is.EqualTo(100d));
    }

    [Test]
    public void TypewriterSkipFollowedByEnoughReadingTime_DoesNotCountAsSkipped()
    {
        ConversationSkipTracker tracker = new(1f);

        tracker.BeginLine();
        tracker.MarkTypewriterSkipped(10f);
        tracker.CompleteLine(12f);

        ConversationReadingResult result = tracker.CompleteConversation("test_conversation");

        Assert.That(result.LinesViewed, Is.EqualTo(1));
        Assert.That(result.TypewriterSkippedLines, Is.EqualTo(1));
        Assert.That(result.RapidlySkippedLines, Is.Zero);
        Assert.That(result.SkipRatePercent, Is.Zero);
    }

    [Test]
    public void RapidAdvanceWithoutTypewriterSkip_DoesNotCountAsSkipped()
    {
        ConversationSkipTracker tracker = new(1f);

        tracker.BeginLine();
        tracker.CompleteLine(0.1f);

        ConversationReadingResult result = tracker.CompleteConversation("test_conversation");

        Assert.That(result.LinesViewed, Is.EqualTo(1));
        Assert.That(result.TypewriterSkippedLines, Is.Zero);
        Assert.That(result.RapidlySkippedLines, Is.Zero);
    }

    [Test]
    public void ChapterAnalytics_AggregatesConversationResultsByLineCount()
    {
        ChapterAnalyticsSaveData analytics = new();

        analytics.RecordMissionConversationReading(10, 2);
        analytics.RecordMissionConversationReading(20, 6);

        Assert.That(analytics.missionConversationsCompleted, Is.EqualTo(2));
        Assert.That(analytics.missionConversationLinesViewed, Is.EqualTo(30));
        Assert.That(analytics.missionConversationLinesSkipped, Is.EqualTo(8));
        Assert.That(
            analytics.missionConversationSkipRatePercent,
            Is.EqualTo(26.666666666666668d).Within(0.000001d));
    }
}
