using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MissionObjectiveLocalizationDatabase
{
    public string defaultLanguageCode = "en";
    public List<MissionObjectiveJsonEntry> objectives = new();
}

[Serializable]
public sealed class MissionObjectiveJsonEntry
{
    public string objectiveId = string.Empty;
    public List<MissionObjectiveLanguageContent> languages = new();
}

[Serializable]
public sealed class MissionObjectiveLanguageContent
{
    public string languageCode = string.Empty;
    public string description = string.Empty;
}

public static class MissionObjectiveLocalizationJson
{
    private static readonly Dictionary<TextAsset, MissionObjectiveLocalizationDatabase> Cache = new();

    public static void ClearCache()
    {
        Cache.Clear();
    }

    public static string Resolve(TextAsset asset, string objectiveId, string languageCode)
    {
        MissionObjectiveLocalizationDatabase database = Load(asset);
        MissionObjectiveJsonEntry entry = database?.objectives?.Find(item =>
            item != null && string.Equals(item.objectiveId, objectiveId, StringComparison.Ordinal));
        if (entry?.languages == null)
            return null;

        MissionObjectiveLanguageContent requested = Find(entry, languageCode);
        if (Usable(requested))
            return requested.description;

        MissionObjectiveLanguageContent fallback = Find(entry, database.defaultLanguageCode);
        return Usable(fallback) ? fallback.description : entry.languages.Find(Usable)?.description;
    }

    private static MissionObjectiveLocalizationDatabase Load(TextAsset asset)
    {
        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            return null;

        if (!Cache.TryGetValue(asset, out MissionObjectiveLocalizationDatabase database))
        {
            database = JsonUtility.FromJson<MissionObjectiveLocalizationDatabase>(asset.text);
            Cache[asset] = database;
        }

        return database;
    }

    private static MissionObjectiveLanguageContent Find(MissionObjectiveJsonEntry entry, string code) =>
        string.IsNullOrWhiteSpace(code) ? null : entry.languages.Find(language =>
            language != null && string.Equals(language.languageCode, code, StringComparison.OrdinalIgnoreCase));

    private static bool Usable(MissionObjectiveLanguageContent content) =>
        content != null && !string.IsNullOrWhiteSpace(content.description);
}

public abstract class MissionStep : MonoBehaviour
{
    [Header("Localized Objective")]
    [SerializeField] private TextAsset localizedObjectiveJson;
    [SerializeField] private string objectiveId;

    [Header("Fallback Objective")]
    [SerializeField, TextArea] private string objectiveDescription;

    public string ObjectiveDescription =>
        MissionObjectiveLocalizationJson.Resolve(
            localizedObjectiveJson,
            objectiveId,
            GameLanguage.CurrentCode) ?? objectiveDescription;
    public virtual string JournalDescription => ObjectiveDescription;

    protected string MissionId { get; private set; }
    protected int StepIndex { get; private set; }

    private bool isFinished;

    public void Initialize(string missionId, int stepIndex)
    {
        Initialize(missionId, stepIndex, null);
    }

    public void Initialize(
        string missionId,
        int stepIndex,
        MissionStepProgressSaveData savedProgress)
    {
        MissionId = missionId;
        StepIndex = stepIndex;
        OnStepActivated();

        if (!isFinished && savedProgress != null)
            RestoreProgress(savedProgress);
    }

    protected abstract void OnStepActivated();

    public virtual MissionStepProgressSaveData CaptureProgress()
    {
        return new MissionStepProgressSaveData();
    }

    protected virtual void RestoreProgress(MissionStepProgressSaveData savedProgress)
    {
    }

    protected void UpdateObjective(string objective)
    {
        MissionEvents.UpdateMissionObjective(MissionId, StepIndex, objective);
    }

    protected void FinishStep()
    {
        if (isFinished)
            return;

        isFinished = true;
        MissionEvents.FinishMissionStep(MissionId, StepIndex);
    }

    protected void FailStep(string reason)
    {
        if (isFinished)
            return;

        isFinished = true;
        MissionEvents.FailMissionStep(MissionId, StepIndex, reason);
    }
}
