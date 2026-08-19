using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class MissionController : MonoBehaviour
{
    public static MissionController Instance { get; private set; }
    public static bool IsMissionCompletionVisible { get; private set; }
    public static event Action OnMissionStatesChanged;
    public static event Action<string, int> OnMissionStepAdvanced;
    public static event Action<string> OnMissionCompletionPresented;

    [Header("Mission Library")]
    [SerializeField] private MissionInfoSO[] missionInfos = new MissionInfoSO[0];
    [SerializeField] private string missionToStart;

    [Header("UI")]
    [SerializeField] private TMP_Text missionNameText;
    [FormerlySerializedAs("objectiveText")]
    [SerializeField] private TMP_Text objectiveDescriptionText;
    [SerializeField] private GameObject missionCompletedPanel;
    [SerializeField] private TMP_Text completedMissionNameText;
    [SerializeField, Min(0f)] private float completionDisplayDuration = 3f;

    private readonly Dictionary<string, Mission> missions = new();
    private Mission activeMission;
    private MissionStep activeStep;
    private bool isShowingMissionCompletion;
    private bool hasRestoredProgress;
    private string currentObjective = string.Empty;

    void OnEnable()
    {
        MissionEvents.OnMissionStepFinished += HandleMissionStepFinished;
        MissionEvents.OnMissionStepFailed += HandleMissionStepFailed;
        MissionEvents.OnMissionObjectiveUpdated += HandleMissionObjectiveUpdated;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one Mission Controller can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;
        IsMissionCompletionVisible = false;

        if (missionCompletedPanel != null)
            missionCompletedPanel.SetActive(false);

        BuildMissionLibrary();
    }

    void Start()
    {
        if (!hasRestoredProgress && !string.IsNullOrWhiteSpace(missionToStart))
            StartMission(missionToStart);
    }

    void OnDisable()
    {
        MissionEvents.OnMissionStepFinished -= HandleMissionStepFinished;
        MissionEvents.OnMissionStepFailed -= HandleMissionStepFailed;
        MissionEvents.OnMissionObjectiveUpdated -= HandleMissionObjectiveUpdated;

        IsMissionCompletionVisible = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandleMissionStepFailed(string missionId, int stepIndex, string reason)
    {
        if (activeMission == null ||
            activeMission.Info.MissionId != missionId ||
            activeMission.CurrentStepIndex != stepIndex)
        {
            return;
        }

        Mission failedMission = activeMission;
        failedMission.State = MissionState.Failed;
        activeMission = null;
        OnMissionStatesChanged?.Invoke();

        if (activeStep != null)
            Destroy(activeStep.gameObject);

        activeStep = null;
        UpdateObjectiveUI(failedMission.Info.DisplayName, "Mission could not continue.");
        Debug.LogError($"Mission '{missionId}' failed at step {stepIndex}: {reason}", failedMission.Info);
    }

    public void StartMission(string missionId)
    {
        if (isShowingMissionCompletion)
        {
            Debug.LogWarning($"Cannot start '{missionId}' while mission completion is being shown.", this);
            return;
        }

        if (activeMission != null)
        {
            Debug.LogWarning($"Cannot start '{missionId}' while '{activeMission.Info.MissionId}' is active.", this);
            return;
        }

        if (!missions.TryGetValue(missionId, out Mission mission))
        {
            Debug.LogWarning($"Mission '{missionId}' is not registered.", this);
            return;
        }

        RefreshMissionAvailability();

        if (mission.State != MissionState.Available)
        {
            Debug.LogWarning($"Mission '{missionId}' is currently {mission.State}.", this);
            return;
        }

        mission.State = MissionState.InProgress;
        activeMission = mission;
        OnMissionStatesChanged?.Invoke();

        if (mission.Info.MissionStepPrefabs == null || mission.Info.MissionStepPrefabs.Length == 0)
        {
            HandleMissionStepFailed(mission.Info.MissionId, 0, "Mission has no step prefabs.");
            return;
        }

        ActivateCurrentStep();
    }

    public MissionState GetMissionState(string missionId)
    {
        return missions.TryGetValue(missionId, out Mission mission)
            ? mission.State
            : MissionState.Locked;
    }

    public int GetMissionStepIndex(string missionId)
    {
        return missions.TryGetValue(missionId, out Mission mission)
            ? mission.CurrentStepIndex
            : 0;
    }

    public MissionStepProgressSaveData GetMissionStepProgress(string missionId)
    {
        if (activeMission == null ||
            activeStep == null ||
            !string.Equals(activeMission.Info.MissionId, missionId, StringComparison.Ordinal))
        {
            return new MissionStepProgressSaveData();
        }

        return activeStep.CaptureProgress() ?? new MissionStepProgressSaveData();
    }

    public bool RestoreMissionProgress(IEnumerable<MissionSaveData> savedMissions)
    {
        if (savedMissions == null)
        {
            Debug.LogWarning("Cannot restore missions because the save contains no mission data.", this);
            return false;
        }

        List<MissionSaveData> savedProgress = new();

        foreach (MissionSaveData savedMission in savedMissions)
        {
            if (savedMission != null && !string.IsNullOrWhiteSpace(savedMission.missionId))
                savedProgress.Add(savedMission);
        }

        if (savedProgress.Count == 0)
        {
            Debug.LogWarning(
                "The save contains no mission progress, so the chapter will use its normal starting mission.",
                this);
            return false;
        }

        hasRestoredProgress = true;
        StopAllCoroutines();
        isShowingMissionCompletion = false;
        IsMissionCompletionVisible = false;

        if (missionCompletedPanel != null)
            missionCompletedPanel.SetActive(false);

        if (activeStep != null)
            Destroy(activeStep.gameObject);

        activeStep = null;
        activeMission = null;
        currentObjective = string.Empty;

        foreach (Mission mission in missions.Values)
        {
            mission.State = MissionState.Locked;
            mission.CurrentStepIndex = 0;
        }

        HashSet<string> restoredMissionIds = new(StringComparer.Ordinal);
        MissionStepProgressSaveData activeStepProgress = null;

        foreach (MissionSaveData savedMission in savedProgress)
        {
            if (!restoredMissionIds.Add(savedMission.missionId))
            {
                Debug.LogWarning(
                    $"The save contains duplicate mission progress for '{savedMission.missionId}'.",
                    this);
                continue;
            }

            if (!missions.TryGetValue(savedMission.missionId, out Mission mission))
            {
                Debug.LogWarning(
                    $"Saved mission '{savedMission.missionId}' is not registered in this chapter.",
                    this);
                continue;
            }

            if (!Enum.TryParse(savedMission.state, false, out MissionState restoredState))
            {
                Debug.LogWarning(
                    $"Saved mission '{savedMission.missionId}' has invalid state '{savedMission.state}'.",
                    this);
                continue;
            }

            int stepCount = mission.Info.MissionStepPrefabs?.Length ?? 0;
            mission.CurrentStepIndex = Mathf.Clamp(savedMission.currentStepIndex, 0, stepCount);

            if (restoredState == MissionState.InProgress)
            {
                if (activeMission != null)
                {
                    Debug.LogWarning(
                        $"Only one mission can be restored in progress. '{savedMission.missionId}' was reset to Available.",
                        this);
                    restoredState = MissionState.Available;
                }
                else if (mission.CurrentStepIndex >= stepCount)
                {
                    restoredState = MissionState.Finished;
                }
                else
                {
                    activeMission = mission;
                    activeStepProgress = savedMission.stepProgress;
                }
            }

            mission.State = restoredState;
        }

        RefreshMissionAvailability();
        RestoreCompletedMissionStepWorldState();

        if (activeMission != null)
        {
            ActivateCurrentStep(activeStepProgress);
        }
        else
        {
            if (missionNameText != null)
                missionNameText.SetText(string.Empty);

            if (objectiveDescriptionText != null)
                objectiveDescriptionText.SetText(string.Empty);

            OnMissionStatesChanged?.Invoke();
        }

        return true;
    }

    private void RestoreCompletedMissionStepWorldState()
    {
        foreach (MissionInfoSO info in missionInfos)
        {
            if (info == null ||
                !missions.TryGetValue(info.MissionId, out Mission mission) ||
                info.MissionStepPrefabs == null)
            {
                continue;
            }

            int completedStepCount = Mathf.Clamp(
                mission.CurrentStepIndex,
                0,
                info.MissionStepPrefabs.Length);

            for (int stepIndex = 0; stepIndex < completedStepCount; stepIndex++)
            {
                MissionStep stepPrefab = info.MissionStepPrefabs[stepIndex];

                if (stepPrefab is NPCMovementMissionStep movementStep)
                    movementStep.ApplyCompletedWorldState();
            }
        }
    }

    public IEnumerable<MissionInfoSO> MissionInfos => missionInfos;
    public MissionInfoSO ActiveMissionInfo => activeMission?.Info;
    public int ActiveMissionStepIndex => activeMission?.CurrentStepIndex ?? -1;
    public string CurrentObjective => currentObjective;

    public bool HasMissionStarted(MissionInfoSO missionInfo)
    {
        if (missionInfo == null)
            return true;

        MissionState state = GetMissionState(missionInfo.MissionId);
        return state == MissionState.InProgress || state == MissionState.Finished;
    }

    private void BuildMissionLibrary()
    {
        missions.Clear();

        foreach (MissionInfoSO info in missionInfos)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.MissionId))
            {
                Debug.LogWarning("Every registered mission needs a Mission Info asset and a non-empty ID.", this);
                continue;
            }

            if (missions.ContainsKey(info.MissionId))
            {
                Debug.LogError($"Duplicate mission ID: '{info.MissionId}'.", info);
                continue;
            }

            missions.Add(info.MissionId, new Mission(info, MissionState.Locked));
        }

        RefreshMissionAvailability();
    }

    private void RefreshMissionAvailability()
    {
        foreach (Mission mission in missions.Values)
        {
            if (mission.State != MissionState.Locked)
                continue;

            if (ArePrerequisitesFinished(mission.Info))
                mission.State = MissionState.Available;
        }
    }

    private bool ArePrerequisitesFinished(MissionInfoSO info)
    {
        foreach (MissionInfoSO prerequisite in info.Prerequisites)
        {
            if (prerequisite == null ||
                !missions.TryGetValue(prerequisite.MissionId, out Mission prerequisiteMission) ||
                prerequisiteMission.State != MissionState.Finished)
            {
                return false;
            }
        }

        return true;
    }

    private void ActivateCurrentStep(MissionStepProgressSaveData savedProgress = null)
    {
        if (activeMission == null)
            return;

        if (!activeMission.HasAnotherStep)
        {
            CompleteActiveMission();
            return;
        }

        MissionStep stepPrefab = activeMission.Info.MissionStepPrefabs[activeMission.CurrentStepIndex];

        if (stepPrefab == null)
        {
            HandleMissionStepFailed(
                activeMission.Info.MissionId,
                activeMission.CurrentStepIndex,
                "Mission contains an empty step prefab.");
            return;
        }

        activeStep = Instantiate(stepPrefab, transform);
        UpdateObjectiveUI(activeMission.Info.DisplayName, activeStep.ObjectiveDescription);
        activeStep.Initialize(
            activeMission.Info.MissionId,
            activeMission.CurrentStepIndex,
            savedProgress);
    }

    private void HandleMissionObjectiveUpdated(string missionId, int stepIndex, string objective)
    {
        if (activeMission == null ||
            activeMission.Info.MissionId != missionId ||
            activeMission.CurrentStepIndex != stepIndex)
        {
            return;
        }

        UpdateObjectiveUI(activeMission.Info.DisplayName, objective);
    }

    private void HandleMissionStepFinished(string missionId, int stepIndex)
    {
        if (activeMission == null ||
            activeMission.Info.MissionId != missionId ||
            activeMission.CurrentStepIndex != stepIndex)
        {
            return;
        }

        if (activeStep != null)
            Destroy(activeStep.gameObject);

        activeStep = null;
        activeMission.CurrentStepIndex++;
        ActivateCurrentStep();
        OnMissionStepAdvanced?.Invoke(missionId, stepIndex);
    }

    private void CompleteActiveMission()
    {
        Mission completedMission = activeMission;
        completedMission.State = MissionState.Finished;
        activeMission = null;

        if (missionNameText != null)
            missionNameText.SetText(string.Empty);

        if (objectiveDescriptionText != null)
            objectiveDescriptionText.SetText(string.Empty);

        currentObjective = string.Empty;

        RefreshMissionAvailability();
        OnMissionStatesChanged?.Invoke();
        StartCoroutine(ShowMissionCompletion(
            completedMission.Info.MissionId,
            completedMission.Info.DisplayName));
    }

    private IEnumerator ShowMissionCompletion(string completedMissionId, string completedMissionName)
    {
        isShowingMissionCompletion = true;

        // A mission can finish from the same interaction that opens dialogue.
        // Keep the completion graphic out of the way until every dialogue UI closes.
        while (IsAnyDialogueActive())
            yield return null;

        if (completedMissionNameText != null)
            completedMissionNameText.SetText(completedMissionName);

        if (missionCompletedPanel != null)
            missionCompletedPanel.SetActive(true);

        IsMissionCompletionVisible = true;

        yield return new WaitForSeconds(completionDisplayDuration);

        IsMissionCompletionVisible = false;

        if (missionCompletedPanel != null)
            missionCompletedPanel.SetActive(false);

        isShowingMissionCompletion = false;
        OnMissionCompletionPresented?.Invoke(completedMissionId);
        TryStartNextAutomaticMission();
    }

    private static bool IsAnyDialogueActive()
    {
        return (NarrationController.Instance != null && NarrationController.Instance.IsNarrationActive) ||
               (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) ||
               (ArtifactDialogueController.Instance != null && ArtifactDialogueController.Instance.IsDialogueActive);
    }

    private void TryStartNextAutomaticMission()
    {
        foreach (MissionInfoSO info in missionInfos)
        {
            if (info == null || !info.AutoStartWhenAvailable)
                continue;

            if (missions.TryGetValue(info.MissionId, out Mission mission) &&
                mission.State == MissionState.Available)
            {
                StartMission(info.MissionId);
                return;
            }
        }
    }

    private void UpdateObjectiveUI(string missionName, string objective)
    {
        currentObjective = objective ?? string.Empty;

        if (missionNameText != null)
            missionNameText.SetText(missionName);

        if (objectiveDescriptionText != null)
            objectiveDescriptionText.SetText(objective);

        OnMissionStatesChanged?.Invoke();
    }
}
