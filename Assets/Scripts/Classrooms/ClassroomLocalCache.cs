using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ClassroomLocalCache
{
    private const string CacheFileName = "memberships.json";

    [Serializable]
    private sealed class MembershipCacheData
    {
        public List<ClassroomMembership> memberships = new();
    }

    public static IReadOnlyList<ClassroomMembership> Load()
    {
        if (!TryGetCachePath(out string path) || !File.Exists(path))
            return Array.Empty<ClassroomMembership>();

        try
        {
            MembershipCacheData data = JsonUtility.FromJson<MembershipCacheData>(
                File.ReadAllText(path));
            return data?.memberships ?? new List<ClassroomMembership>();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"The local classroom list could not be read: {exception.Message}");
            return Array.Empty<ClassroomMembership>();
        }
    }

    public static void Save(IEnumerable<ClassroomMembership> memberships)
    {
        if (!TryGetCachePath(out string path))
            return;

        try
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            MembershipCacheData data = new();
            if (memberships != null)
                data.memberships.AddRange(memberships);

            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporaryPath, path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"The local classroom list could not be saved: {exception.Message}");
        }
    }

    public static void AddOrUpdate(ClassroomMembership membership)
    {
        if (membership == null || string.IsNullOrWhiteSpace(membership.roomId))
            return;

        List<ClassroomMembership> memberships = new(Load());
        int index = memberships.FindIndex(item => item.roomId == membership.roomId);
        if (index >= 0)
            memberships[index] = membership;
        else
            memberships.Add(membership);
        Save(memberships);
    }

    public static void Remove(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return;

        List<ClassroomMembership> memberships = new(Load());
        memberships.RemoveAll(item => item != null && item.roomId == roomId);
        Save(memberships);
    }

    public static bool TryDeleteRoomSave(string roomId, out string error)
    {
        error = string.Empty;
        if (!PlayerSession.IsSignedIn || string.IsNullOrWhiteSpace(PlayerSession.AccountId))
        {
            error = "A signed-in account is required.";
            return false;
        }

        string directory = SaveStorageScope.GetClassroomSaveDirectory(
            Application.persistentDataPath, PlayerSession.AccountId, roomId);
        SaveFileService save = new(directory, SaveFileService.MinimumSlotNumber);
        return save.TryDeleteAll(out error);
    }

    private static bool TryGetCachePath(out string path)
    {
        path = string.Empty;
        if (!PlayerSession.IsSignedIn || string.IsNullOrWhiteSpace(PlayerSession.AccountId))
            return false;

        path = Path.Combine(
            SaveStorageScope.GetAccountClassroomsDirectory(
                Application.persistentDataPath,
                PlayerSession.AccountId),
            CacheFileName);
        return true;
    }
}
