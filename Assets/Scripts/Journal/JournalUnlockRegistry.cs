using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only discovery state shared by journal collections.
/// Save/load integration will be added separately when the game's JSON save system is ready.
/// </summary>
public static class JournalUnlockRegistry
{
    public static event Action<string, string> OnEntryUnlocked;

    private static readonly HashSet<string> UnlockedEntries = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetRuntimeState()
    {
        UnlockedEntries.Clear();
    }

    public static bool IsUnlocked(string collection, string entryID)
    {
        return !string.IsNullOrWhiteSpace(entryID) &&
               UnlockedEntries.Contains(GetKey(collection, entryID));
    }

    public static bool Unlock(string collection, string entryID)
    {
        if (string.IsNullOrWhiteSpace(collection) || string.IsNullOrWhiteSpace(entryID) ||
            IsUnlocked(collection, entryID))
            return false;

        UnlockedEntries.Add(GetKey(collection, entryID));
        OnEntryUnlocked?.Invoke(collection, entryID);
        return true;
    }

    private static string GetKey(string collection, string entryID)
    {
        return collection.Trim().ToLowerInvariant() + ":" + entryID.Trim().ToLowerInvariant();
    }
}
