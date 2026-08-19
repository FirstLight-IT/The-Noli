using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string speakerId;
    public string text;
}

[System.Serializable]
public class ConversationLanguageContent
{
    public string languageCode;
    public List<DialogueLine> lines;
}

[System.Serializable]
public class Conversation
{
    public string conversationId;
    public string defaultLanguageCode = "en";
    public List<ConversationLanguageContent> languages;

    public List<DialogueLine> ResolveLines(string requestedLanguageCode)
    {
        ConversationLanguageContent selected = FindLanguage(requestedLanguageCode) ??
                                               FindLanguage(defaultLanguageCode) ??
                                               FirstUsableLanguage();
        return selected?.lines;
    }

    private ConversationLanguageContent FindLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) || languages == null)
            return null;

        foreach (ConversationLanguageContent language in languages)
        {
            if (language != null &&
                HasUsableLines(language) &&
                string.Equals(
                    language.languageCode,
                    languageCode,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return language;
            }
        }

        return null;
    }

    private ConversationLanguageContent FirstUsableLanguage()
    {
        if (languages == null)
            return null;

        foreach (ConversationLanguageContent language in languages)
        {
            if (HasUsableLines(language))
                return language;
        }

        return null;
    }

    private static bool HasUsableLines(ConversationLanguageContent language)
    {
        if (language?.lines == null || language.lines.Count == 0)
            return false;

        foreach (DialogueLine line in language.lines)
        {
            if (line == null ||
                string.IsNullOrWhiteSpace(line.speakerId) ||
                string.IsNullOrWhiteSpace(line.text))
            {
                return false;
            }
        }

        return true;
    }
}

public static class GameLanguage
{
    private const string PreferenceKey = "TheNoli.Language";
    private const string DefaultCode = "en";

    public static string CurrentCode => PlayerPrefs.GetString(PreferenceKey, DefaultCode);

    public static void Set(string languageCode)
    {
        string code = string.IsNullOrWhiteSpace(languageCode)
            ? DefaultCode
            : languageCode.Trim().ToLowerInvariant();
        PlayerPrefs.SetString(PreferenceKey, code);
        PlayerPrefs.Save();
    }
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

