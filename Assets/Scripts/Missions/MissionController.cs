using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class MissionController : MonoBehaviour
{
    public static MissionController Instance { get; private set; }
    public static bool IsMissionCompletionVisible { get; private set; }

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
        if (!string.IsNullOrWhiteSpace(missionToStart))
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

        Debug.Log($"Mission started: {mission.Info.DisplayName}");

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

    private void ActivateCurrentStep()
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
        activeStep.Initialize(activeMission.Info.MissionId, activeMission.CurrentStepIndex);
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

        Debug.Log($"Mission complete: {completedMission.Info.DisplayName}");

        RefreshMissionAvailability();
        StartCoroutine(ShowMissionCompletion(completedMission.Info.DisplayName));
    }

    private IEnumerator ShowMissionCompletion(string completedMissionName)
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
        if (missionNameText != null)
            missionNameText.SetText(missionName);

        if (objectiveDescriptionText != null)
            objectiveDescriptionText.SetText(objective);
    }
}
