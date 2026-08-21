using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ArtifactDatabase
{
    public string defaultLanguageCode = "en";
    public List<ArtifactJsonEntry> artifacts = new();
}

[Serializable]
public sealed class ArtifactJsonEntry
{
    public string artifactId = string.Empty;
    public List<ArtifactLanguageContent> languages = new();
}

[Serializable]
public sealed class ArtifactLanguageContent
{
    public string languageCode = string.Empty;
    public string displayName = string.Empty;
    public string shortName = string.Empty;
    public string[] description = Array.Empty<string>();
    public string[] hintDialogueLines = Array.Empty<string>();
}

public static class ArtifactJson
{
    private static readonly Dictionary<TextAsset, ArtifactDatabase> Cache = new();

    public static ArtifactLanguageContent Resolve(TextAsset asset, string artifactId, string languageCode)
    {
        ArtifactDatabase database = Load(asset);
        ArtifactJsonEntry entry = database?.artifacts?.Find(item =>
            item != null && string.Equals(item.artifactId, artifactId, StringComparison.Ordinal));
        if (entry?.languages == null) return null;
        ArtifactLanguageContent requested = Find(entry, languageCode);
        if (Usable(requested)) return requested;
        ArtifactLanguageContent fallback = Find(entry, database.defaultLanguageCode);
        return Usable(fallback) ? fallback : entry.languages.Find(Usable);
    }

    public static string[] ResolveHintDialogueLines(
        TextAsset asset,
        string artifactId,
        string languageCode)
    {
        ArtifactDatabase database = Load(asset);
        ArtifactJsonEntry entry = database?.artifacts?.Find(item =>
            item != null && string.Equals(item.artifactId, artifactId, StringComparison.Ordinal));
        if (entry?.languages == null)
            return Array.Empty<string>();

        ArtifactLanguageContent requested = Find(entry, languageCode);
        if (HasUsableHint(requested))
            return requested.hintDialogueLines;

        ArtifactLanguageContent fallback = Find(entry, database.defaultLanguageCode);
        return HasUsableHint(fallback)
            ? fallback.hintDialogueLines
            : Array.Empty<string>();
    }

    private static ArtifactDatabase Load(TextAsset asset)
    {
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return null;
        if (!Cache.TryGetValue(asset, out ArtifactDatabase database))
        {
            database = JsonUtility.FromJson<ArtifactDatabase>(asset.text);
            Cache[asset] = database;
        }
        return database;
    }

    private static ArtifactLanguageContent Find(ArtifactJsonEntry entry, string code) =>
        string.IsNullOrWhiteSpace(code) ? null : entry.languages.Find(language =>
            language != null && string.Equals(language.languageCode, code, StringComparison.OrdinalIgnoreCase));

    private static bool Usable(ArtifactLanguageContent content) =>
        content != null && !string.IsNullOrWhiteSpace(content.displayName);

    private static bool HasUsableHint(ArtifactLanguageContent content) =>
        content?.hintDialogueLines != null &&
        content.hintDialogueLines.Length > 0 &&
        Array.TrueForAll(
            content.hintDialogueLines,
            line => !string.IsNullOrWhiteSpace(line));
}

[CreateAssetMenu(fileName = "New Artifact", menuName = "Artifacts/Artifact Info")]
public class ArtifactInfoSO : ScriptableObject
{
    [Header("Localized JSON")]
    [SerializeField] private TextAsset localizedDataJson;

    [Header("Identity")]
    [SerializeField] private string artifactID;
    [SerializeField] private string roomID;

    [Header("Unity Asset References")]
    [SerializeField] private Sprite image;

    private ArtifactLanguageContent LocalizedContent =>
        ArtifactJson.Resolve(localizedDataJson, artifactID, GameLanguage.CurrentCode);

    public string ArtifactID => artifactID;
    public string DisplayName => LocalizedContent?.displayName ?? string.Empty;
    public string ShortName => string.IsNullOrWhiteSpace(LocalizedContent?.shortName)
        ? DisplayName
        : LocalizedContent.shortName;
    public string RoomID => roomID;
    public Sprite Image => image;
    public string[] Description => LocalizedContent?.description ?? Array.Empty<string>();
    public string[] HintDialogueLines => ArtifactJson.ResolveHintDialogueLines(
        localizedDataJson,
        artifactID,
        GameLanguage.CurrentCode);
}
