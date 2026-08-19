using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class NarrationJsonDatabase
{
    public string narrationId = string.Empty;
    public string defaultLanguageCode = "en";
    public List<NarrationLanguageContent> languages = new();
}

[Serializable]
public sealed class NarrationLanguageContent
{
    public string languageCode = string.Empty;
    public string[] passages = Array.Empty<string>();
}

public static class NarrationJson
{
    private static readonly Dictionary<TextAsset, NarrationJsonDatabase> Cache = new();

    public static string[] Resolve(TextAsset asset, string narrationId, string languageCode)
    {
        NarrationJsonDatabase database = Load(asset);
        if (database?.languages == null ||
            !string.Equals(database.narrationId, narrationId, StringComparison.Ordinal))
            return Array.Empty<string>();

        NarrationLanguageContent requested = Find(database, languageCode);
        if (Usable(requested)) return requested.passages;
        NarrationLanguageContent fallback = Find(database, database.defaultLanguageCode);
        if (Usable(fallback)) return fallback.passages;
        return database.languages.Find(Usable)?.passages ?? Array.Empty<string>();
    }

    private static NarrationJsonDatabase Load(TextAsset asset)
    {
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return null;
        if (!Cache.TryGetValue(asset, out NarrationJsonDatabase database))
        {
            database = JsonUtility.FromJson<NarrationJsonDatabase>(asset.text);
            Cache[asset] = database;
        }
        return database;
    }

    private static NarrationLanguageContent Find(NarrationJsonDatabase database, string code) =>
        string.IsNullOrWhiteSpace(code) ? null : database.languages.Find(language =>
            language != null && string.Equals(language.languageCode, code, StringComparison.OrdinalIgnoreCase));

    private static bool Usable(NarrationLanguageContent content) =>
        content?.passages != null && content.passages.Length > 0 &&
        Array.TrueForAll(content.passages, passage => !string.IsNullOrWhiteSpace(passage));
}

[CreateAssetMenu(fileName = "New Narration Sequence", menuName = "The Noli/Narration Sequence")]
public class NarrationSequenceSO : ScriptableObject
{
    [Header("Localized JSON")]
    [SerializeField] private TextAsset localizedDataJson;

    [Header("Identity")]
    [SerializeField] private string narrationId;

    public string[] Passages =>
        NarrationJson.Resolve(localizedDataJson, narrationId, GameLanguage.CurrentCode);
}
