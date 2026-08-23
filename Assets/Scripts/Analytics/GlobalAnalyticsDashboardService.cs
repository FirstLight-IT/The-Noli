using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.Core;

public static class GlobalAnalyticsDashboardService
{
    private const string GetAnalyticsScriptName = "GetGlobalAnalytics";

    public static async Task<GlobalAnalyticsDashboardResult> LoadAsync()
    {
        if (PlayerSession.EffectiveRole != AccountRole.Librarian)
        {
            return GlobalAnalyticsDashboardResult.Failed(
                "Librarian access is required.");
        }

        try
        {
            GlobalAnalyticsDashboardResponse response = await CloudCodeService.Instance
                .CallEndpointAsync<GlobalAnalyticsDashboardResponse>(
                    GetAnalyticsScriptName,
                    new Dictionary<string, object>());
            return GlobalAnalyticsDashboardResult.Succeeded(response);
        }
        catch (Exception exception) when (
            exception is RequestFailedException || exception is InvalidOperationException)
        {
            return GlobalAnalyticsDashboardResult.Failed(exception.Message);
        }
    }
}

[Serializable]
public sealed class GlobalAnalyticsDashboardResponse
{
    public int participantCount;
    public List<GlobalChapterAnalyticsAggregate> chapters = new();
}

[Serializable]
public sealed class GlobalChapterAnalyticsAggregate
{
    public string chapterId = string.Empty;
    public int participantCount;
    public double averageEngagementRatePercent;
    public double averageQuizScoreRatePercent;
    public double averageDialogueSkipRatePercent;
    public double averageArtifactDiscoveryRatePercent;
    public double averagePlayTimeSeconds;
}

public sealed class GlobalAnalyticsDashboardResult
{
    public bool Success { get; }
    public GlobalAnalyticsDashboardResponse Response { get; }
    public string Error { get; }

    private GlobalAnalyticsDashboardResult(
        bool success,
        GlobalAnalyticsDashboardResponse response,
        string error)
    {
        Success = success;
        Response = response;
        Error = error ?? string.Empty;
    }

    public static GlobalAnalyticsDashboardResult Succeeded(
        GlobalAnalyticsDashboardResponse response)
    {
        response ??= new GlobalAnalyticsDashboardResponse();
        response.chapters ??= new List<GlobalChapterAnalyticsAggregate>();
        return new GlobalAnalyticsDashboardResult(true, response, string.Empty);
    }

    public static GlobalAnalyticsDashboardResult Failed(string error)
    {
        return new GlobalAnalyticsDashboardResult(
            false,
            new GlobalAnalyticsDashboardResponse(),
            error);
    }
}
