using System;
using System.Collections.Generic;

public sealed class SaveSlotInfo
{
    public int SlotNumber { get; }
    public bool HasSave { get; }
    public string ActiveChapterId { get; }
    public string ActiveChapterState { get; }
    public bool ActiveChapterCompleted { get; }
    public double TotalPlayTimeSeconds { get; }
    public string CreatedAtUtc { get; }
    public string LastSavedAtUtc { get; }
    public string PlaythroughId { get; }
    public IReadOnlyList<string> OfficialAnalyticsChapterIds { get; }

    private SaveSlotInfo(
        int slotNumber,
        bool hasSave,
        string activeChapterId,
        string activeChapterState,
        bool activeChapterCompleted,
        double totalPlayTimeSeconds,
        string createdAtUtc,
        string lastSavedAtUtc,
        string playthroughId,
        IReadOnlyList<string> officialAnalyticsChapterIds)
    {
        SlotNumber = slotNumber;
        HasSave = hasSave;
        ActiveChapterId = activeChapterId ?? string.Empty;
        ActiveChapterState = activeChapterState ?? string.Empty;
        ActiveChapterCompleted = activeChapterCompleted;
        TotalPlayTimeSeconds = Math.Max(0d, totalPlayTimeSeconds);
        CreatedAtUtc = createdAtUtc ?? string.Empty;
        LastSavedAtUtc = lastSavedAtUtc ?? string.Empty;
        PlaythroughId = playthroughId ?? string.Empty;
        OfficialAnalyticsChapterIds = officialAnalyticsChapterIds ?? Array.Empty<string>();
    }

    public static SaveSlotInfo Empty(int slotNumber)
    {
        return new SaveSlotInfo(
            slotNumber,
            false,
            string.Empty,
            string.Empty,
            false,
            0d,
            string.Empty,
            string.Empty,
            string.Empty,
            Array.Empty<string>());
    }

    public static SaveSlotInfo FromSave(int slotNumber, GameSaveData saveData)
    {
        if (saveData == null)
            return Empty(slotNumber);

        saveData.Normalize();
        ChapterSaveData activeChapter = saveData.FindChapter(saveData.activeChapterId);
        double totalPlayTime = 0d;
        List<string> officialChapterIds = new();

        foreach (ChapterSaveData chapter in saveData.chapters)
        {
            if (chapter?.analytics != null)
                totalPlayTime += Math.Max(0d, chapter.analytics.playTimeSeconds);

            if (chapter?.officialAnalytics?.isRecorded == true &&
                chapter.officialAnalytics.hasEngagementScore)
                officialChapterIds.Add(chapter.chapterId);
        }

        officialChapterIds.Sort(StringComparer.Ordinal);

        return new SaveSlotInfo(
            slotNumber,
            true,
            saveData.activeChapterId,
            activeChapter?.state,
            activeChapter?.completedEver == true,
            totalPlayTime,
            saveData.createdAtUtc,
            saveData.lastSavedAtUtc,
            saveData.playthroughId,
            officialChapterIds);
    }
}
