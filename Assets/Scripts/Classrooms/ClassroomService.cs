using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

public static class ClassroomService
{
    private const string CreateClassroomScriptName = "CreateClassroom";
    private const string ListTeacherClassroomsScriptName = "ListTeacherClassrooms";
    private const string JoinClassroomScriptName = "JoinClassroom";
    private const string ListPlayerClassroomsScriptName = "ListPlayerClassrooms";
    private const string LeaveClassroomScriptName = "LeaveClassroom";
    private const string GetClassroomDetailsScriptName = "GetClassroomDetails";
    private const string SetClassroomStatusScriptName = "SetClassroomStatus";
    private const string ValidateClassroomAccessScriptName = "ValidateClassroomAccess";
    private const string SubmitClassroomProgressScriptName = "SubmitClassroomProgress";
    private const string GetClassroomDashboardScriptName = "GetClassroomDashboard";

    public static async Task<ClassroomOperationResult> CreateAsync(string roomName)
    {
        if (!PlayerSession.IsSignedIn ||
            PlayerSession.EffectiveRole != AccountRole.Teacher)
        {
            return ClassroomOperationResult.Failed(
                "Verified Teacher access is required.");
        }

        if (string.IsNullOrWhiteSpace(roomName))
            return ClassroomOperationResult.Failed("Enter a classroom name.");

        try
        {
            ClassroomSummary classroom = await CloudCodeService.Instance
                .CallEndpointAsync<ClassroomSummary>(
                    CreateClassroomScriptName,
                    new Dictionary<string, object>
                    {
                        ["roomName"] = roomName.Trim()
                    });

            return classroom != null && !string.IsNullOrWhiteSpace(classroom.roomId)
                ? ClassroomOperationResult.Succeeded(classroom)
                : ClassroomOperationResult.Failed("The classroom could not be created.");
        }
        catch (Exception exception)
        {
            return ClassroomOperationResult.Failed(exception.Message);
        }
    }

    public static async Task<TeacherClassroomListResult> GetTeacherClassroomsAsync()
    {
        if (!PlayerSession.IsSignedIn ||
            PlayerSession.EffectiveRole != AccountRole.Teacher)
        {
            return TeacherClassroomListResult.Failed(
                "Verified Teacher access is required.");
        }

        try
        {
            TeacherClassroomListResponse response = await CloudCodeService.Instance
                .CallEndpointAsync<TeacherClassroomListResponse>(
                    ListTeacherClassroomsScriptName);

            return TeacherClassroomListResult.Succeeded(
                response?.rooms ?? new List<ClassroomSummary>());
        }
        catch (Exception exception)
        {
            return TeacherClassroomListResult.Failed(exception.Message);
        }
    }

    public static async Task<ClassroomMembership> JoinAsync(string joinCode)
    {
        if (!PlayerSession.IsSignedIn ||
            PlayerSession.EffectiveRole != AccountRole.Player)
        {
            throw new InvalidOperationException(
                "Sign in with Player access before joining a classroom.");
        }

        if (string.IsNullOrWhiteSpace(joinCode))
            throw new InvalidOperationException("Enter a classroom code.");

        if (Application.internetReachability == NetworkReachability.NotReachable)
            throw new InvalidOperationException(
                "An internet connection is required to join a classroom.");

        JoinClassroomResponse response = await CloudCodeService.Instance
            .CallEndpointAsync<JoinClassroomResponse>(
            JoinClassroomScriptName,
            new Dictionary<string, object>
            {
                ["joinCode"] = joinCode.Trim().ToUpperInvariant()
            });
        if (response == null || !response.success || response.membership == null)
            throw new InvalidOperationException(
                response?.error ?? "The classroom could not be joined.");

        ClassroomLocalCache.AddOrUpdate(response.membership);
        return response.membership;
    }

    public static async Task<IReadOnlyList<ClassroomMembership>>
        GetPlayerClassroomsAsync()
    {
        if (!PlayerSession.IsSignedIn ||
            PlayerSession.EffectiveRole != AccountRole.Player)
        {
            throw new InvalidOperationException(
                "Player access is required to view joined classrooms.");
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
            return ClassroomLocalCache.Load();

        try
        {
            PlayerClassroomListResponse response = await CloudCodeService.Instance
                .CallEndpointAsync<PlayerClassroomListResponse>(
                    ListPlayerClassroomsScriptName);
            List<ClassroomMembership> memberships =
                response?.memberships ?? new List<ClassroomMembership>();
            ReconcileDeletedClassrooms(response?.deletedRoomIds);
            ClassroomLocalCache.Save(memberships);
            return memberships;
        }
        catch
        {
            IReadOnlyList<ClassroomMembership> cached = ClassroomLocalCache.Load();
            if (cached.Count > 0)
                return cached;
            throw;
        }
    }

    public static async Task LeaveAsync(string roomId)
    {
        if (!PlayerSession.IsSignedIn ||
            PlayerSession.EffectiveRole != AccountRole.Player)
            throw new InvalidOperationException("Player access is required.");

        if (Application.internetReachability == NetworkReachability.NotReachable)
            throw new InvalidOperationException(
                "An internet connection is required to leave a classroom.");

        ClassroomActionResponse response = await CloudCodeService.Instance
            .CallEndpointAsync<ClassroomActionResponse>(
                LeaveClassroomScriptName,
                new Dictionary<string, object> { ["roomId"] = roomId });
        if (response == null || !response.success)
            throw new InvalidOperationException(
                response?.error ?? "The classroom could not be left.");

        if (!ClassroomLocalCache.TryDeleteRoomSave(roomId, out string deleteError))
            throw new InvalidOperationException(
                $"The classroom was left, but its local save could not be removed. {deleteError}");
        ClassroomLocalCache.Remove(roomId);
    }

    public static async Task<IReadOnlyList<ClassroomMemberSummary>>
        GetClassroomMembersAsync(string roomId)
    {
        ClassroomDetailsResponse response = await CloudCodeService.Instance
            .CallEndpointAsync<ClassroomDetailsResponse>(
                GetClassroomDetailsScriptName,
                new Dictionary<string, object> { ["roomId"] = roomId });
        if (response == null || !response.success)
            throw new InvalidOperationException(
                response?.error ?? "The classroom roster could not be loaded.");
        return response.members ?? new List<ClassroomMemberSummary>();
    }

    public static async Task<string> SetClassroomStatusAsync(
        string roomId, string status)
    {
        ClassroomStatusResponse response = await CloudCodeService.Instance
            .CallEndpointAsync<ClassroomStatusResponse>(
                SetClassroomStatusScriptName,
                new Dictionary<string, object>
                {
                    ["roomId"] = roomId,
                    ["status"] = status
                });
        if (response == null || !response.success)
            throw new InvalidOperationException(
                response?.error ?? "The classroom status could not be changed.");
        return response.status;
    }

    public static async Task<ClassroomAccessResponse> ValidateAccessAsync(string roomId)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
            return new ClassroomAccessResponse
            {
                success = true,
                status = "OfflineCached"
            };

        ClassroomAccessResponse response = await CloudCodeService.Instance
            .CallEndpointAsync<ClassroomAccessResponse>(
                ValidateClassroomAccessScriptName,
                new Dictionary<string, object> { ["roomId"] = roomId });
        return response ?? new ClassroomAccessResponse
        {
            success = false,
            error = "Classroom access could not be verified."
        };
    }

    public static async Task<ClassroomActionResponse> SubmitProgressAsync(
        string roomId,
        string progressJson)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            return new ClassroomActionResponse
            {
                success = false,
                error = "Classroom progress will sync when an internet connection is available."
            };
        }

        try
        {
            ClassroomActionResponse response = await CloudCodeService.Instance
                .CallEndpointAsync<ClassroomActionResponse>(
                    SubmitClassroomProgressScriptName,
                    new Dictionary<string, object>
                    {
                        ["roomId"] = roomId,
                        ["progressJson"] = progressJson
                    });
            return response ?? new ClassroomActionResponse
            {
                success = false,
                error = "Classroom progress could not be synchronized."
            };
        }
        catch (Exception exception)
        {
            return new ClassroomActionResponse
            {
                success = false,
                error = exception.Message
            };
        }
    }

    public static async Task<ClassroomDashboardResponse> GetDashboardAsync(string roomId)
    {
        if (!PlayerSession.IsSignedIn ||
            PlayerSession.EffectiveRole != AccountRole.Teacher)
        {
            return new ClassroomDashboardResponse
            {
                success = false,
                error = "Verified Teacher access is required."
            };
        }

        try
        {
            ClassroomDashboardResponse response = await CloudCodeService.Instance
                .CallEndpointAsync<ClassroomDashboardResponse>(
                    GetClassroomDashboardScriptName,
                    new Dictionary<string, object> { ["roomId"] = roomId });
            return response ?? new ClassroomDashboardResponse
            {
                success = false,
                error = "The classroom dashboard could not be loaded."
            };
        }
        catch (Exception exception)
        {
            return new ClassroomDashboardResponse
            {
                success = false,
                error = exception.Message
            };
        }
    }

    private static void ReconcileDeletedClassrooms(
        IReadOnlyCollection<string> deletedRoomIds)
    {
        if (deletedRoomIds == null)
            return;

        foreach (string roomId in deletedRoomIds)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                continue;

            if (!ClassroomLocalCache.TryDeleteRoomSave(roomId, out string error))
                Debug.LogWarning($"Deleted classroom save cleanup failed: {error}");
            ClassroomLocalCache.Remove(roomId);
        }
    }
}
