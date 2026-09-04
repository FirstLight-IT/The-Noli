using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum AmbientNPCTag
{
    None = 0,
    Girl = 1 << 0,
    Gentleman = 1 << 1
}

[Serializable]
public sealed class AmbientNPCDatabase
{
    public string defaultLanguageCode = "en";
    public List<AmbientNPCEntry> npcs = new();
}

[Serializable]
public sealed class AmbientNPCEntry
{
    public string npcId = string.Empty;
    public List<AmbientNPCLanguageContent> languages = new();
}

[Serializable]
public sealed class AmbientNPCLanguageContent
{
    public string languageCode = string.Empty;
    public string displayName = string.Empty;
    public NPCDialogueSequence[] dialogueVariations = Array.Empty<NPCDialogueSequence>();
    public AmbientNPCMissionDialogue[] missionDialogueOverrides =
        Array.Empty<AmbientNPCMissionDialogue>();
}

[Serializable]
public sealed class AmbientNPCMissionDialogue
{
    public string missionId = string.Empty;
    public NPCDialogueSequence[] dialogueVariations = Array.Empty<NPCDialogueSequence>();
}

public static class AmbientNPCJson
{
    private sealed class CacheEntry
    {
        public string Json;
        public AmbientNPCDatabase Database;
    }

    private static readonly Dictionary<TextAsset, CacheEntry> Cache = new();

    public static AmbientNPCLanguageContent Resolve(TextAsset asset, string npcId, string languageCode)
    {
        AmbientNPCDatabase database = Load(asset);
        AmbientNPCEntry entry = database?.npcs?.Find(item =>
            item != null && string.Equals(item.npcId, npcId, StringComparison.Ordinal));
        if (entry?.languages == null)
            return null;

        AmbientNPCLanguageContent requested = Find(entry, languageCode);
        if (Usable(requested)) return requested;
        AmbientNPCLanguageContent fallback = Find(entry, database.defaultLanguageCode);
        return Usable(fallback) ? fallback : entry.languages.Find(Usable);
    }

    private static AmbientNPCDatabase Load(TextAsset asset)
    {
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return null;

        string json = asset.text;
        if (!Cache.TryGetValue(asset, out CacheEntry cached) || cached.Json != json)
        {
            cached = new CacheEntry
            {
                Json = json,
                Database = JsonUtility.FromJson<AmbientNPCDatabase>(json)
            };
            Cache[asset] = cached;
        }

        return cached.Database;
    }

    private static AmbientNPCLanguageContent Find(AmbientNPCEntry entry, string code) =>
        string.IsNullOrWhiteSpace(code) ? null : entry.languages.Find(language =>
            language != null && string.Equals(language.languageCode, code, StringComparison.OrdinalIgnoreCase));

    private static bool Usable(AmbientNPCLanguageContent content) =>
        content != null && !string.IsNullOrWhiteSpace(content.displayName);

}

[CreateAssetMenu(fileName = "New Ambient NPC", menuName = "Characters/Ambient NPC Info")]
public class AmbientNPCInfoSO : ScriptableObject
{
    [Header("Localized JSON")]
    [SerializeField] private TextAsset localizedDataJson;

    [Header("Identity")]
    [SerializeField] private string npcID;

    [Header("Mission Classification")]
    [SerializeField] private AmbientNPCTag tags;

    private AmbientNPCLanguageContent LocalizedContent =>
        AmbientNPCJson.Resolve(localizedDataJson, npcID, GameLanguage.CurrentCode);

    public string NpcID => npcID;
    public AmbientNPCTag Tags => tags;
    public string DisplayName => LocalizedContent?.displayName ?? string.Empty;
    public NPCDialogueSequence[] DialogueVariations =>
        LocalizedContent?.dialogueVariations ?? Array.Empty<NPCDialogueSequence>();

    public NPCDialogueSequence[] GetMissionDialogueVariations(string missionId)
    {
        if (string.IsNullOrWhiteSpace(missionId))
            return Array.Empty<NPCDialogueSequence>();

        AmbientNPCMissionDialogue[] overrides = LocalizedContent?.missionDialogueOverrides;
        if (overrides == null)
            return Array.Empty<NPCDialogueSequence>();

        foreach (AmbientNPCMissionDialogue missionDialogue in overrides)
        {
            if (missionDialogue != null &&
                string.Equals(
                    missionDialogue.missionId,
                    missionId,
                    StringComparison.Ordinal))
            {
                return missionDialogue.dialogueVariations ??
                    Array.Empty<NPCDialogueSequence>();
            }
        }

        return Array.Empty<NPCDialogueSequence>();
    }

    public bool HasTag(AmbientNPCTag tag) =>
        tag != AmbientNPCTag.None && (tags & tag) == tag;
}
