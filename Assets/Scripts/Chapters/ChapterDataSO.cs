using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ChapterLocalizationDatabase
{
    public string defaultLanguageCode = "en";
    public List<ChapterJsonEntry> chapters = new();
}

[Serializable]
public sealed class ChapterJsonEntry
{
    public string chapterId = string.Empty;
    public List<ChapterLanguageContent> languages = new();
}

[Serializable]
public sealed class ChapterLanguageContent
{
    public string languageCode = string.Empty;
    public string chapterLabel = string.Empty;
    public string title = string.Empty;
}

public static class ChapterLocalizationJson
{
    private static readonly Dictionary<TextAsset, ChapterLocalizationDatabase> Cache = new();

    public static ChapterLanguageContent Resolve(TextAsset asset, string chapterId, string languageCode)
    {
        ChapterLocalizationDatabase database = Load(asset);
        ChapterJsonEntry entry = database?.chapters?.Find(item =>
            item != null && string.Equals(item.chapterId, chapterId, StringComparison.Ordinal));
        if (entry?.languages == null) return null;
        ChapterLanguageContent requested = Find(entry, languageCode);
        if (Usable(requested)) return requested;
        ChapterLanguageContent fallback = Find(entry, database.defaultLanguageCode);
        return Usable(fallback) ? fallback : entry.languages.Find(Usable);
    }

    private static ChapterLocalizationDatabase Load(TextAsset asset)
    {
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return null;
        if (!Cache.TryGetValue(asset, out ChapterLocalizationDatabase database))
        {
            database = JsonUtility.FromJson<ChapterLocalizationDatabase>(asset.text);
            Cache[asset] = database;
        }
        return database;
    }

    private static ChapterLanguageContent Find(ChapterJsonEntry entry, string code) =>
        string.IsNullOrWhiteSpace(code) ? null : entry.languages.Find(language =>
            language != null && string.Equals(language.languageCode, code, StringComparison.OrdinalIgnoreCase));

    private static bool Usable(ChapterLanguageContent content) =>
        content != null && !string.IsNullOrWhiteSpace(content.chapterLabel);
}

[CreateAssetMenu(fileName = "New Chapter", menuName = "The Noli/Chapter")]
public sealed class ChapterDataSO : ScriptableObject
{
    [Header("Localized JSON")]
    [SerializeField] private TextAsset localizedDataJson;

    [Header("Identity")]
    [SerializeField] private string chapterId;

    [Header("Availability")]
    [Tooltip("Only enable this after the chapter's gameplay content and scene setup are ready.")]
    [SerializeField] private bool contentAvailable;

    [Header("Player")]
    [SerializeField] private NPCInfoSO playerCharacter;

    [Header("Opening")]
    [SerializeField] private NarrationSequenceSO openingNarration;

    [Header("Mission Library")]
    [SerializeField] private MissionInfoSO[] missions = Array.Empty<MissionInfoSO>();
    [SerializeField] private MissionInfoSO startingMission;

    [Header("Completion Quiz")]
    [SerializeField] private MissionInfoSO finalMission;
    [SerializeField] private TextAsset completionQuizJson;
    [SerializeField] private string quizSceneName = SaveGameManager.QuizSceneName;

    [Header("Journal")]
    [SerializeField] private GlossaryDataSO glossary;

    public string ChapterId => chapterId;
    private ChapterLanguageContent LocalizedContent =>
        ChapterLocalizationJson.Resolve(localizedDataJson, chapterId, GameLanguage.CurrentCode);

    public string ChapterLabel => LocalizedContent?.chapterLabel ?? string.Empty;
    public string Title => LocalizedContent?.title ?? string.Empty;
    public bool ContentAvailable => contentAvailable;
    public NPCInfoSO PlayerCharacter => playerCharacter;
    public NarrationSequenceSO OpeningNarration => openingNarration;
    public MissionInfoSO[] Missions => missions ?? Array.Empty<MissionInfoSO>();
    public MissionInfoSO StartingMission => startingMission;
    public MissionInfoSO FinalMission => finalMission;
    public string StartingMissionId => startingMission != null ? startingMission.MissionId : string.Empty;
    public string FinalMissionId => finalMission != null ? finalMission.MissionId : string.Empty;
    public TextAsset CompletionQuizJson => completionQuizJson;
    public string QuizSceneName => string.IsNullOrWhiteSpace(quizSceneName)
        ? SaveGameManager.QuizSceneName
        : quizSceneName;
    public GlossaryDataSO Glossary => glossary;
}
