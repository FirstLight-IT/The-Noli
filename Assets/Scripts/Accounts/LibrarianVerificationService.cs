using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.Core;

public static class LibrarianVerificationService
{
    private const string ListScriptName = "ListPendingTeachers";
    private const string ReviewScriptName = "ReviewTeacherVerification";

    public static async Task<TeacherRequestListResult> GetPendingTeachersAsync()
    {
        if (PlayerSession.EffectiveRole != AccountRole.Librarian)
            return TeacherRequestListResult.Failed("Librarian access is required.");

        try
        {
            PendingTeacherListResponse response = await CloudCodeService.Instance
                .CallEndpointAsync<PendingTeacherListResponse>(
                    ListScriptName,
                    new Dictionary<string, object>());

            return TeacherRequestListResult.Succeeded(
                response?.requests ?? new List<TeacherReviewRequest>());
        }
        catch (Exception exception) when (
            exception is RequestFailedException || exception is InvalidOperationException)
        {
            return TeacherRequestListResult.Failed(exception.Message);
        }
    }

    public static async Task<AccountOperationResult> ReviewAsync(
        string targetAccountId,
        bool approve)
    {
        if (PlayerSession.EffectiveRole != AccountRole.Librarian)
            return AccountOperationResult.Failed("Librarian access is required.");

        if (string.IsNullOrWhiteSpace(targetAccountId))
            return AccountOperationResult.Failed("Select a Teacher request first.");

        try
        {
            Dictionary<string, object> arguments = new()
            {
                ["targetAccountId"] = targetAccountId,
                ["decision"] = approve ? "approve" : "reject"
            };

            await CloudCodeService.Instance.CallEndpointAsync<Dictionary<string, object>>(
                ReviewScriptName,
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
