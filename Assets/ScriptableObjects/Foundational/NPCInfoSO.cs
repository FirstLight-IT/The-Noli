using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NPCDialogueSequence
{
    [TextArea(2, 5)]
    public string[] lines;
}

[Serializable]
public sealed class PrimaryNPCDatabase
{
    public int schemaVersion = 1;
    public string defaultLanguageCode = "en";
    public List<PrimaryNPCEntry> npcs = new();
}

[Serializable]
public sealed class PrimaryNPCEntry
{
    public string npcId = string.Empty;
    public List<PrimaryNPCLanguageContent> languages = new();
}

[Serializable]
public sealed class PrimaryNPCLanguageContent
{
    public string languageCode = string.Empty;
    public string displayName = string.Empty;
    public string characterFullName = string.Empty;
    public string alias = string.Empty;
    public string characterCategory = string.Empty;
    public string description = string.Empty;
    public string biography = string.Empty;
    public string[] characterFacts = Array.Empty<string>();
    public string[] introductionLines = Array.Empty<string>();
    public PrimaryNPCDialogueSequence[] repeatDialogues = Array.Empty<PrimaryNPCDialogueSequence>();
}

[Serializable]
public sealed class PrimaryNPCDialogueSequence
{
    public string[] lines = Array.Empty<string>();
}

public static class PrimaryNPCJson
{
    private sealed class CachedDatabase
    {
        public string SourceText;
        public PrimaryNPCDatabase Database;
    }

    private static readonly Dictionary<TextAsset, CachedDatabase> Cache = new();

    public static PrimaryNPCLanguageContent Resolve(
        TextAsset jsonAsset,
        string npcId,
        string requestedLanguageCode)
    {
        PrimaryNPCDatabase database = Load(jsonAsset);
        if (database?.npcs == null || string.IsNullOrWhiteSpace(npcId))
            return null;

        PrimaryNPCEntry entry = database.npcs.Find(candidate =>
            candidate != null &&
            string.Equals(candidate.npcId, npcId, StringComparison.Ordinal));

        if (entry?.languages == null)
            return null;

        PrimaryNPCLanguageContent requested = FindLanguage(entry, requestedLanguageCode);
        if (HasUsableContent(requested))
            return requested;

        PrimaryNPCLanguageContent fallback = FindLanguage(entry, database.defaultLanguageCode);
        if (HasUsableContent(fallback))
            return fallback;

        return entry.languages.Find(HasUsableContent);
    }

    private static PrimaryNPCDatabase Load(TextAsset jsonAsset)
    {
        if (jsonAsset == null || string.IsNullOrWhiteSpace(jsonAsset.text))
            return null;

        if (Cache.TryGetValue(jsonAsset, out CachedDatabase cached) &&
            cached.SourceText == jsonAsset.text)
        {
            return cached.Database;
        }

        try
        {
            PrimaryNPCDatabase database = JsonUtility.FromJson<PrimaryNPCDatabase>(jsonAsset.text);
            Cache[jsonAsset] = new CachedDatabase
            {
                SourceText = jsonAsset.text,
                Database = database
            };
            return database;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Primary NPC JSON could not be read: {exception.Message}", jsonAsset);
            return null;
        }
    }

    private static PrimaryNPCLanguageContent FindLanguage(
        PrimaryNPCEntry entry,
        string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return null;

        return entry.languages.Find(language =>
            language != null &&
            string.Equals(language.languageCode, languageCode, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasUsableContent(PrimaryNPCLanguageContent content)
    {
        return content != null && !string.IsNullOrWhiteSpace(content.displayName);
    }
}

[CreateAssetMenu(fileName = "New NPC Info", menuName = "Characters/NPC Info")]
public class NPCInfoSO : ScriptableObject
{
    [Header("Localized JSON")]
    [SerializeField] private TextAsset localizedDataJson;

    [Header("Identity")]
    [SerializeField] private string npcID;

    [Header("Unity Asset References")]
    [SerializeField] private Sprite portrait;

    public string NpcID => npcID;
    private PrimaryNPCLanguageContent LocalizedContent =>
        PrimaryNPCJson.Resolve(localizedDataJson, npcID, GameLanguage.CurrentCode);

    public string DisplayName => LocalizedContent?.displayName ?? string.Empty;
    public string CharacterFullName
    {
        get
        {
            string localizedName = LocalizedContent?.characterFullName;
            return string.IsNullOrWhiteSpace(localizedName) ? DisplayName : localizedName;
        }
    }
    public string Alias => LocalizedContent?.alias ?? string.Empty;
    public string CharacterCategory => LocalizedContent?.characterCategory ?? string.Empty;
    public Sprite Portrait => portrait;
    public string Description => LocalizedContent?.description ?? string.Empty;
    public string Biography => LocalizedContent?.biography ?? string.Empty;
    public string[] CharacterFacts => LocalizedContent?.characterFacts ?? Array.Empty<string>();
    public string[] IntroductionLines => LocalizedContent?.introductionLines ?? Array.Empty<string>();

    public string[] GetRepeatDialogueLines(int index)
    {
        PrimaryNPCDialogueSequence[] localizedDialogues = LocalizedContent?.repeatDialogues;
        if (localizedDialogues != null && index >= 0 && index < localizedDialogues.Length)
            return localizedDialogues[index]?.lines ?? Array.Empty<string>();

        return Array.Empty<string>();
    }

    public int RepeatDialogueCount =>
        LocalizedContent?.repeatDialogues?.Length ?? 0;

}
