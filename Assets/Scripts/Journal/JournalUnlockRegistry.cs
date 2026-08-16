using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Discovery state shared by journal collections. SaveGameManager mirrors this
/// registry into the cumulative journal section of the autosave.
/// </summary>
public static class JournalUnlockRegistry
{
    public const string CharacterCollection = "characters";
    public const string ArtifactCollection = "artifacts";
    public const string GlossaryChapterCollection = "glossary_chapters";

    public static event Action<string, string> OnEntryUnlocked;

    private static readonly HashSet<string> UnlockedEntries = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetRuntimeState()
    {
        Clear();
    }

    public static bool IsUnlocked(string collection, string entryID)
    {
        return !string.IsNullOrWhiteSpace(collection) &&
               !string.IsNullOrWhiteSpace(entryID) &&
               UnlockedEntries.Contains(GetKey(collection, entryID));
    }

    public static bool Unlock(string collection, string entryID)
    {
        if (string.IsNullOrWhiteSpace(collection) || string.IsNullOrWhiteSpace(entryID) ||
            IsUnlocked(collection, entryID))
            return false;

        string normalizedCollection = collection.Trim().ToLowerInvariant();
        string normalizedEntryID = entryID.Trim().ToLowerInvariant();

        UnlockedEntries.Add(GetKey(normalizedCollection, normalizedEntryID));
        OnEntryUnlocked?.Invoke(normalizedCollection, normalizedEntryID);
        return true;
    }

    public static void Clear()
    {
        UnlockedEntries.Clear();
    }

    public static void Restore(string collection, IEnumerable<string> entryIDs)
    {
        if (string.IsNullOrWhiteSpace(collection) || entryIDs == null)
            return;

        foreach (string entryID in entryIDs)
        {
            if (string.IsNullOrWhiteSpace(entryID))
                continue;

            UnlockedEntries.Add(GetKey(collection, entryID));
        }
    }

    public static List<string> GetUnlockedEntryIDs(string collection)
    {
        List<string> entryIDs = new();

        if (string.IsNullOrWhiteSpace(collection))
            return entryIDs;

        string prefix = collection.Trim().ToLowerInvariant() + ":";

        foreach (string key in UnlockedEntries)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                entryIDs.Add(key.Substring(prefix.Length));
        }

        entryIDs.Sort(StringComparer.Ordinal);
        return entryIDs;
    }

    private static string GetKey(string collection, string entryID)
    {
        return collection.Trim().ToLowerInvariant() + ":" + entryID.Trim().ToLowerInvariant();
    }
}
