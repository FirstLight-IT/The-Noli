using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string speaker;
    public string text;
}

[System.Serializable]
public class Conversation
{
    public string conversationId;
    public List<DialogueLine> lines;
}

public sealed class ConversationReadingResult
{
    public string ConversationId { get; }
    public int LinesViewed { get; }
    public int TypewriterSkippedLines { get; }
    public int RapidlySkippedLines { get; }
    public double SkipRatePercent => LinesViewed > 0
        ? RapidlySkippedLines * 100d / LinesViewed
        : 0d;

    public ConversationReadingResult(
        string conversationId,
        int linesViewed,
        int typewriterSkippedLines,
        int rapidlySkippedLines)
    {
        ConversationId = conversationId ?? string.Empty;
        LinesViewed = Mathf.Max(0, linesViewed);
        TypewriterSkippedLines = Mathf.Clamp(
            typewriterSkippedLines,
            0,
            LinesViewed);
        RapidlySkippedLines = Mathf.Clamp(
            rapidlySkippedLines,
            0,
            TypewriterSkippedLines);
    }
}

public sealed class ConversationSkipTracker
{
    private readonly float rapidAdvanceWindowSeconds;
    private int linesViewed;
    private int typewriterSkippedLines;
    private int rapidlySkippedLines;
    private bool lineActive;
    private bool lineCompleted;
    private bool typewriterWasSkipped;
    private float typewriterSkippedAt;

    public ConversationSkipTracker(float rapidAdvanceWindowSeconds)
    {
        this.rapidAdvanceWindowSeconds = Mathf.Max(0f, rapidAdvanceWindowSeconds);
    }

    public void BeginLine()
    {
        lineActive = true;
        lineCompleted = false;
        typewriterWasSkipped = false;
        typewriterSkippedAt = 0f;
    }

    public void MarkTypewriterSkipped(float timestamp)
    {
        if (!lineActive || lineCompleted || typewriterWasSkipped)
            return;

        typewriterWasSkipped = true;
        typewriterSkippedAt = timestamp;
        typewriterSkippedLines++;
    }

    public void CompleteLine(float timestamp)
    {
        if (!lineActive || lineCompleted)
            return;

        lineCompleted = true;
        linesViewed++;

        float elapsedAfterReveal = timestamp - typewriterSkippedAt;

        if (typewriterWasSkipped &&
            elapsedAfterReveal >= 0f &&
            elapsedAfterReveal <= rapidAdvanceWindowSeconds)
        {
            rapidlySkippedLines++;
        }
    }

    public ConversationReadingResult CompleteConversation(string conversationId)
    {
        return new ConversationReadingResult(
            conversationId,
            linesViewed,
            typewriterSkippedLines,
            rapidlySkippedLines);
    }
}

