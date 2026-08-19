using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MissionLocalizationDatabase
{
    public string defaultLanguageCode = "en";
    public List<MissionJsonEntry> missions = new();
}

[Serializable]
public sealed class MissionJsonEntry
{
    public string missionId = string.Empty;
    public List<MissionLanguageContent> languages = new();
}

[Serializable]
public sealed class MissionLanguageContent
{
    public string languageCode = string.Empty;
    public string displayName = string.Empty;
}

public static class MissionLocalizationJson
{
    private static readonly Dictionary<TextAsset, MissionLocalizationDatabase> Cache = new();

    public static MissionLanguageContent Resolve(TextAsset asset, string missionId, string languageCode)
    {
        MissionLocalizationDatabase database = Load(asset);
        MissionJsonEntry entry = database?.missions?.Find(item =>
            item != null && string.Equals(item.missionId, missionId, StringComparison.Ordinal));
        if (entry?.languages == null) return null;
        MissionLanguageContent requested = Find(entry, languageCode);
        if (Usable(requested)) return requested;
        MissionLanguageContent fallback = Find(entry, database.defaultLanguageCode);
        return Usable(fallback) ? fallback : entry.languages.Find(Usable);
    }

    private static MissionLocalizationDatabase Load(TextAsset asset)
    {
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return null;
        if (!Cache.TryGetValue(asset, out MissionLocalizationDatabase database))
        {
            database = JsonUtility.FromJson<MissionLocalizationDatabase>(asset.text);
            Cache[asset] = database;
        }
        return database;
    }

    private static MissionLanguageContent Find(MissionJsonEntry entry, string code) =>
        string.IsNullOrWhiteSpace(code) ? null : entry.languages.Find(language =>
            language != null && string.Equals(language.languageCode, code, StringComparison.OrdinalIgnoreCase));

    private static bool Usable(MissionLanguageContent content) =>
        content != null && !string.IsNullOrWhiteSpace(content.displayName);
}

[CreateAssetMenu(fileName = "New Mission", menuName = "Missions/Mission Info")]
public class MissionInfoSO : ScriptableObject
{
    [Header("Localized JSON")]
    [SerializeField] private TextAsset localizedDataJson;

    [Header("Identity")]
    [SerializeField] private string missionId;

    [Header("Requirements")]
    [SerializeField] private MissionInfoSO[] prerequisites = new MissionInfoSO[0];
    [SerializeField] private bool autoStartWhenAvailable;

    [Header("Ordered Steps")]
    [SerializeField] private MissionStep[] missionStepPrefabs = new MissionStep[0];

    public string MissionId => missionId;
    public string DisplayName =>
        MissionLocalizationJson.Resolve(localizedDataJson, missionId, GameLanguage.CurrentCode)?.displayName
        ?? string.Empty;
    public MissionInfoSO[] Prerequisites => prerequisites;
    public bool AutoStartWhenAvailable => autoStartWhenAvailable;
    public MissionStep[] MissionStepPrefabs => missionStepPrefabs;
}
