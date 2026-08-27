using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class ClassroomProgressSyncService
{
    private static readonly Dictionary<string, string> PendingJsonByRoom = new(
        StringComparer.Ordinal);
    private static readonly HashSet<string> RunningRooms = new(StringComparer.Ordinal);

    public static void Queue(string roomId, GameSaveData saveData)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable ||
            string.IsNullOrWhiteSpace(roomId) || saveData == null ||
            !PlayerSession.IsSignedIn ||
            string.IsNullOrWhiteSpace(PlayerSession.AccountId))
        {
            return;
        }

        ClassroomProgressSubmission submission = CreateSubmission(roomId, saveData);
        if (submission.chapters.Count == 0)
            return;

        string normalizedRoomId = roomId.Trim();
        PendingJsonByRoom[normalizedRoomId] = JsonUtility.ToJson(submission);

        if (RunningRooms.Add(normalizedRoomId))
            _ = DrainAsync(normalizedRoomId);
    }

    private static ClassroomProgressSubmission CreateSubmission(
        string roomId,
        GameSaveData saveData)
    {
        saveData.Normalize();
        ClassroomProgressSubmission submission = new()
        {
            roomId = roomId.Trim(),
            accountId = PlayerSession.AccountId,
            playthroughId = saveData.playthroughId?.Trim() ?? string.Empty
        };

        foreach (ChapterSaveData chapter in saveData.chapters)
        {
            if (chapter == null || string.IsNullOrWhiteSpace(chapter.chapterId))
                continue;

            OfficialChapterAnalyticsSaveData official = chapter.officialAnalytics;
            if (official?.isRecorded != true || !official.hasEngagementScore)
                continue;

            submission.chapters.Add(new ClassroomChapterAnalyticsSubmission
            {
                chapterId = chapter.chapterId.Trim(),
                recordedAtUtc = official.recordedAtUtc?.Trim() ?? string.Empty,
                quizScore = Math.Max(0, official.quizScore),
                quizMaxScore = Math.Max(0, official.quizMaxScore),
                quizScoreRatePercent = Math.Clamp(
                    official.quizScoreRatePercent, 0d, 100d),
                hasEngagementScore = true,
                engagementRatePercent = Math.Clamp(
                    official.engagementRatePercent, 0d, 100d),
                dialogueSkipRatePercent = Math.Clamp(
                    official.dialogueSkipRatePercent, 0d, 100d),
                artifactDiscoveryRatePercent = Math.Clamp(
                    official.artifactDiscoveryRatePercent, 0d, 100d),
                playTimeSeconds = Math.Max(0d, official.playTimeSeconds)
            });
        }

        submission.chapters.Sort((left, right) => string.Compare(
            left.chapterId,
            right.chapterId,
            StringComparison.Ordinal));
        return submission;
    }

    private static async Task DrainAsync(string roomId)
    {
        try
        {
            while (PendingJsonByRoom.Remove(roomId, out string progressJson))
            {
                ClassroomActionResponse response = await ClassroomService
                    .SubmitProgressAsync(roomId, progressJson);
                if (response == null || !response.success)
                {
                    Debug.LogWarning(
                        response?.error ?? "Classroom progress could not be synchronized.");
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Classroom progress sync failed: {exception.Message}");
        }
        finally
        {
            RunningRooms.Remove(roomId);
            if (PendingJsonByRoom.ContainsKey(roomId) && RunningRooms.Add(roomId))
                _ = DrainAsync(roomId);
        }
    }
}
