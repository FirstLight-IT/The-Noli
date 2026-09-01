using UnityEngine;

public static class TeacherClassroomDashboardLaunchContext
{
    public const string SceneName = "TeacherClassroomDashboard";

    public static string RoomId { get; private set; } = string.Empty;
    public static string RoomName { get; private set; } = string.Empty;
    public static string JoinCode { get; private set; } = string.Empty;
    public static string RoomStatus { get; private set; } = string.Empty;

    public static bool HasRoom => !string.IsNullOrWhiteSpace(RoomId);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Clear();
    }

    public static void Set(ClassroomSummary classroom)
    {
        RoomId = classroom?.roomId?.Trim() ?? string.Empty;
        RoomName = classroom?.roomName?.Trim() ?? string.Empty;
        JoinCode = classroom?.joinCode?.Trim() ?? string.Empty;
        RoomStatus = classroom?.status?.Trim() ?? string.Empty;
    }

    public static void Clear()
    {
        RoomId = string.Empty;
        RoomName = string.Empty;
        JoinCode = string.Empty;
        RoomStatus = string.Empty;
    }
}
