using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GlossaryEntry
{
    public string Term { get; }
    public string Category { get; }
    public string Meaning { get; }

    public GlossaryEntry(string term, string category, string meaning)
    {
        Term = term ?? string.Empty;
        Category = category ?? string.Empty;
        Meaning = meaning ?? string.Empty;
    }
}

[Serializable]
public sealed class GlossaryJsonDatabase
{
    public string defaultLanguageCode = "en";
    public List<GlossaryJsonEntry> entries = new();
}

[Serializable]
public sealed class GlossaryJsonEntry
{
    public string entryId = string.Empty;
    public List<GlossaryLanguageContent> languages = new();
}

[Serializable]
public sealed class GlossaryLanguageContent
{
    public string languageCode = string.Empty;
    public string term = string.Empty;
    public string category = string.Empty;
    public string meaning = string.Empty;
}

public static class GlossaryJson
{
    private static readonly Dictionary<TextAsset, GlossaryJsonDatabase> Cache = new();

    public static IReadOnlyList<GlossaryEntry> Resolve(TextAsset asset, string languageCode)
    {
        GlossaryJsonDatabase database = Load(asset);
        if (database?.entries == null)
            return Array.Empty<GlossaryEntry>();

        List<GlossaryEntry> resolved = new(database.entries.Count);
        foreach (GlossaryJsonEntry entry in database.entries)
        {
            if (entry?.languages == null) continue;
            GlossaryLanguageContent content = Find(entry, languageCode);
            if (!Usable(content)) content = Find(entry, database.defaultLanguageCode);
            if (!Usable(content)) content = entry.languages.Find(Usable);
            if (Usable(content))
                resolved.Add(new GlossaryEntry(content.term, content.category, content.meaning));
        }
        return resolved;
    }

    private static GlossaryJsonDatabase Load(TextAsset asset)
    {
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return null;
        if (!Cache.TryGetValue(asset, out GlossaryJsonDatabase database))
        {
            database = JsonUtility.FromJson<GlossaryJsonDatabase>(asset.text);
            Cache[asset] = database;
        }
        return database;
    }

    private static GlossaryLanguageContent Find(GlossaryJsonEntry entry, string code) =>
        string.IsNullOrWhiteSpace(code) ? null : entry.languages.Find(language =>
            language != null && string.Equals(language.languageCode, code, StringComparison.OrdinalIgnoreCase));

    private static bool Usable(GlossaryLanguageContent content) =>
        content != null && !string.IsNullOrWhiteSpace(content.term);
}

[CreateAssetMenu(fileName = "New Chapter Glossary", menuName = "The Noli/Glossary/Chapter Glossary")]
public sealed class GlossaryDataSO : ScriptableObject
{
    [Header("Localized JSON")]
    [SerializeField] private TextAsset localizedDataJson;

    public IReadOnlyList<GlossaryEntry> Entries =>
        GlossaryJson.Resolve(localizedDataJson, GameLanguage.CurrentCode);

    public bool TryGetMeaning(string term, out string meaning)
    {
        if (!string.IsNullOrWhiteSpace(term))
        {
            foreach (GlossaryEntry entry in Entries)
            {
                if (string.Equals(entry.Term, term, StringComparison.OrdinalIgnoreCase))
                {
                    meaning = entry.Meaning;
                    return true;
                }
            }
        }

        meaning = string.Empty;
        return false;
    }
}
