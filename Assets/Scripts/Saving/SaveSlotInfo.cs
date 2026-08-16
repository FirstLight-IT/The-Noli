using System;

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

    private SaveSlotInfo(
        int slotNumber,
        bool hasSave,
        string activeChapterId,
        string activeChapterState,
        bool activeChapterCompleted,
        double totalPlayTimeSeconds,
        string createdAtUtc,
        string lastSavedAtUtc)
    {
        SlotNumber = slotNumber;
        HasSave = hasSave;
        ActiveChapterId = activeChapterId ?? string.Empty;
        ActiveChapterState = activeChapterState ?? string.Empty;
        ActiveChapterCompleted = activeChapterCompleted;
        TotalPlayTimeSeconds = Math.Max(0d, totalPlayTimeSeconds);
        CreatedAtUtc = createdAtUtc ?? string.Empty;
        LastSavedAtUtc = lastSavedAtUtc ?? string.Empty;
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
            string.Empty);
    }

    public static SaveSlotInfo FromSave(int slotNumber, GameSaveData saveData)
    {
        if (saveData == null)
            return Empty(slotNumber);

        saveData.Normalize();
        ChapterSaveData activeChapter = saveData.FindChapter(saveData.activeChapterId);
        double totalPlayTime = 0d;

        foreach (ChapterSaveData chapter in saveData.chapters)
        {
            if (chapter?.analytics != null)
                totalPlayTime += Math.Max(0d, chapter.analytics.playTimeSeconds);
        }

        return new SaveSlotInfo(
            slotNumber,
            true,
            saveData.activeChapterId,
            activeChapter?.state,
            activeChapter?.completedEver == true,
            totalPlayTime,
            saveData.createdAtUtc,
            saveData.lastSavedAtUtc);
    }
}
