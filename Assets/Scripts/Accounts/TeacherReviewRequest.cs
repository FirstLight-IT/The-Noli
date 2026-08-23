using System;
using System.Collections.Generic;

[Serializable]
public sealed class TeacherReviewRequest
{
    public string accountId = string.Empty;
    public string username = string.Empty;
    public string inGameName = string.Empty;
    public string fullName = string.Empty;
    public string schoolEmail = string.Empty;
    public string teacherVerificationStatus = string.Empty;
}

[Serializable]
public sealed class PendingTeacherListResponse
{
    public List<TeacherReviewRequest> requests = new();
}

public sealed class TeacherRequestListResult
{
    public bool Success { get; }
    public string Error { get; }
    public IReadOnlyList<TeacherReviewRequest> Requests { get; }

    private TeacherRequestListResult(
        bool success,
        string error,
        IReadOnlyList<TeacherReviewRequest> requests)
    {
        Success = success;
        Error = error ?? string.Empty;
        Requests = requests ?? Array.Empty<TeacherReviewRequest>();
    }

    public static TeacherRequestListResult Succeeded(
        IReadOnlyList<TeacherReviewRequest> requests)
    {
        return new TeacherRequestListResult(true, string.Empty, requests);
    }

    public static TeacherRequestListResult Failed(string error)
    {
        return new TeacherRequestListResult(false, error, Array.Empty<TeacherReviewRequest>());
    }
}
