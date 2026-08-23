using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

public static class GlobalAnalyticsSubmissionService
{
    private const string SubmitScriptName = "SubmitGlobalAnalytics";
    private const string StatusScriptName = "GetGlobalAnalyticsSubmissionStatus";

    public static async Task<GlobalAnalyticsStatusResult> GetStatusAsync()
    {
        if (!PlayerSession.CanSubmitGlobalAnalytics)
        {
            return GlobalAnalyticsStatusResult.Failed(
                "Guest saves cannot use Global Analytics.");
        }

        try
        {
            GlobalAnalyticsSubmissionStatus response = await CloudCodeService.Instance
                .CallEndpointAsync<GlobalAnalyticsSubmissionStatus>(
                    StatusScriptName,
                    new Dictionary<string, object>());
            return GlobalAnalyticsStatusResult.Succeeded(response);
        }
        catch (Exception exception) when (
            exception is RequestFailedException || exception is InvalidOperationException)
        {
            return GlobalAnalyticsStatusResult.Failed(exception.Message);
        }
    }

    public static async Task<AccountOperationResult> SubmitAsync(GameSaveData saveData)
    {
        if (!GlobalAnalyticsSubmissionFactory.TryCreateForCurrentAccount(
                saveData,
                out GlobalAnalyticsSubmission submission,
                out string error))
        {
            return AccountOperationResult.Failed(error);
        }

        try
        {
            Dictionary<string, object> arguments = new()
            {
                ["submissionJson"] = JsonUtility.ToJson(submission)
            };

            await CloudCodeService.Instance.CallEndpointAsync<GlobalAnalyticsSubmissionResponse>(
                SubmitScriptName,
                arguments);
            return AccountOperationResult.Succeeded();
        }
        catch (Exception exception) when (
            exception is RequestFailedException || exception is InvalidOperationException)
        {
            return AccountOperationResult.Failed(exception.Message);
        }
    }
}

[Serializable]
public sealed class GlobalAnalyticsSubmissionResponse
{
    public string status = string.Empty;
    public int newlyAcceptedChapters;
    public int totalAcceptedChapters;
    public string officialPlaythroughId = string.Empty;
}

[Serializable]
public sealed class GlobalAnalyticsSubmissionStatus
{
    public bool hasOfficialPlaythrough;
    public string officialPlaythroughId = string.Empty;
    public List<string> acceptedChapterIds = new();
}

public sealed class GlobalAnalyticsStatusResult
{
    public bool Success { get; }
    public GlobalAnalyticsSubmissionStatus Status { get; }
    public string Error { get; }

    private GlobalAnalyticsStatusResult(
        bool success,
        GlobalAnalyticsSubmissionStatus status,
        string error)
    {
        Success = success;
        Status = status;
        Error = error ?? string.Empty;
    }

    public static GlobalAnalyticsStatusResult Succeeded(
        GlobalAnalyticsSubmissionStatus status)
    {
        status ??= new GlobalAnalyticsSubmissionStatus();
        status.officialPlaythroughId ??= string.Empty;
        status.acceptedChapterIds ??= new List<string>();
        return new GlobalAnalyticsStatusResult(true, status, string.Empty);
    }

    public static GlobalAnalyticsStatusResult Failed(string error)
    {
        return new GlobalAnalyticsStatusResult(
            false,
            new GlobalAnalyticsSubmissionStatus(),
            error);
    }
}
