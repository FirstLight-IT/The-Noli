using System;
using System.Collections.Generic;

[Serializable]
public sealed class ClassroomSummary
{
    public string roomId = string.Empty;
    public string roomName = string.Empty;
    public string joinCode = string.Empty;
    public string status = string.Empty;
    public string createdAtUtc = string.Empty;
    public int memberCount;
}

[Serializable]
public sealed class ClassroomMembership
{
    public string roomId = string.Empty;
    public string roomName = string.Empty;
    public string teacherAccountId = string.Empty;
    public string teacherInGameName = string.Empty;
    public string status = string.Empty;
    public string joinedAtUtc = string.Empty;
    public string leftAtUtc = string.Empty;
}

[Serializable]
public sealed class TeacherClassroomListResponse
{
    public List<ClassroomSummary> rooms = new();
}

[Serializable]
public sealed class PlayerClassroomListResponse
{
    public List<ClassroomMembership> memberships = new();
    public List<string> deletedRoomIds = new();
}

[Serializable]
public sealed class JoinClassroomResponse
{
    public bool success;
    public string error = string.Empty;
    public ClassroomMembership membership;
}

[Serializable]
public sealed class ClassroomActionResponse
{
    public bool success;
    public string error = string.Empty;
    public int newlyUploadedChapters;
    public int totalUploadedChapters;
}

[Serializable]
public sealed class ClassroomMemberSummary
{
    public string accountId = string.Empty;
    public string inGameName = string.Empty;
    public string status = string.Empty;
    public string joinedAtUtc = string.Empty;
    public string leftAtUtc = string.Empty;
}

[Serializable]
public sealed class ClassroomDetailsResponse
{
    public bool success;
    public string error = string.Empty;
    public List<ClassroomMemberSummary> members = new();
}

[Serializable]
public sealed class ClassroomStatusResponse
{
    public bool success;
    public string error = string.Empty;
    public string status = string.Empty;
}

[Serializable]
public sealed class ClassroomAccessResponse
{
    public bool success;
    public string error = string.Empty;
    public string status = string.Empty;
}

[Serializable]
public sealed class ClassroomProgressSubmission
{
    public int schemaVersion = 2;
    public string roomId = string.Empty;
    public string accountId = string.Empty;
    public string playthroughId = string.Empty;
    public string lastSyncedAtUtc = string.Empty;
    public List<ClassroomChapterAnalyticsSubmission> chapters = new();
}

[Serializable]
public sealed class ClassroomChapterAnalyticsSubmission
{
    public string chapterId = string.Empty;
    public string recordedAtUtc = string.Empty;
    public int quizScore;
    public int quizMaxScore;
    public double quizScoreRatePercent;
    public bool hasEngagementScore;
    public double engagementRatePercent;
    public double dialogueSkipRatePercent;
    public double artifactDiscoveryRatePercent;
    public double playTimeSeconds;
}

[Serializable]
public sealed class ClassroomDashboardMember
{
    public string accountId = string.Empty;
    public string inGameName = string.Empty;
    public bool hasUploadedAnalytics;
    public int uploadedChapterCount;
    public string lastUploadedAtUtc = string.Empty;
    public List<string> uploadedChapterIds = new();
}

[Serializable]
public sealed class ClassroomChapterAnalyticsAggregate
{
    public string chapterId = string.Empty;
    public int participantCount;
    public double averageEngagementRatePercent;
    public double averageQuizScoreRatePercent;
    public double averageDialogueSkipRatePercent;
    public double averageArtifactDiscoveryRatePercent;
    public double averagePlayTimeSeconds;
}

[Serializable]
public sealed class ClassroomDashboardResponse
{
    public bool success;
    public string error = string.Empty;
    public string roomId = string.Empty;
    public string roomName = string.Empty;
    public string joinCode = string.Empty;
    public string status = string.Empty;
    public int participantCount;
    public List<ClassroomDashboardMember> members = new();
    public List<ClassroomChapterAnalyticsAggregate> chapters = new();
}

public sealed class TeacherClassroomListResult
{
    public bool Success { get; }
    public string Error { get; }
    public IReadOnlyList<ClassroomSummary> Classrooms { get; }

    private TeacherClassroomListResult(
        bool success,
        string error,
        IReadOnlyList<ClassroomSummary> classrooms)
    {
        Success = success;
        Error = error ?? string.Empty;
        Classrooms = classrooms ?? Array.Empty<ClassroomSummary>();
    }

    public static TeacherClassroomListResult Succeeded(
        IReadOnlyList<ClassroomSummary> classrooms) =>
        new(true, string.Empty, classrooms);

    public static TeacherClassroomListResult Failed(string error) =>
        new(false, error, Array.Empty<ClassroomSummary>());
}

public sealed class ClassroomOperationResult
{
    public bool Success { get; }
    public string Error { get; }
    public ClassroomSummary Classroom { get; }

    private ClassroomOperationResult(bool success, string error, ClassroomSummary classroom)
    {
        Success = success;
        Error = error ?? string.Empty;
        Classroom = classroom;
    }

    public static ClassroomOperationResult Succeeded(ClassroomSummary classroom) =>
        new(true, string.Empty, classroom);

    public static ClassroomOperationResult Failed(string error) =>
        new(false, error, null);
}
