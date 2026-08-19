using System.Collections.Generic;
using UnityEngine;

public class InspectArtifactsMissionStep : MissionStep
{
    [Header("Inspect Artifacts Step")]
    [SerializeField] private string roomID;
    [SerializeField, Min(1)] private int requiredArtifactCount = 5;

    private readonly HashSet<string> availableArtifactIDs = new();
    private readonly HashSet<string> inspectedArtifactIDs = new();

    void OnEnable()
    {
        Artifact.OnArtifactInteracted += HandleArtifactInteracted;
    }

    void OnDisable()
    {
        Artifact.OnArtifactInteracted -= HandleArtifactInteracted;
    }

    protected override void OnStepActivated()
    {
        List<Artifact> availableArtifacts = Artifact.GetActiveInRoom(roomID);

        if (availableArtifacts.Count < requiredArtifactCount)
        {
            FailStep(
                $"Inspect artifacts step needs {requiredArtifactCount} artifacts in room '{roomID}', " +
                $"but only found {availableArtifacts.Count}.");
            return;
        }

        foreach (Artifact artifact in availableArtifacts)
            availableArtifactIDs.Add(artifact.ArtifactID);

        // Journal discoveries are permanent across chapter replays, but this
        // collection represents interactions in the current chapter attempt.
        // Continue/load restores it through RestoreProgress instead.

        RefreshProgressAndCompleteIfReady();
    }

    public override MissionStepProgressSaveData CaptureProgress()
    {
        MissionStepProgressSaveData progress = new();
        progress.completedTargetIds.AddRange(inspectedArtifactIDs);
        progress.Normalize();
        return progress;
    }

    protected override void RestoreProgress(MissionStepProgressSaveData savedProgress)
    {
        if (savedProgress?.completedTargetIds != null)
        {
            foreach (string artifactID in savedProgress.completedTargetIds)
            {
                if (availableArtifactIDs.Contains(artifactID))
                    inspectedArtifactIDs.Add(artifactID);
            }
        }

        RefreshProgressAndCompleteIfReady();
    }

    private void HandleArtifactInteracted(string artifactID)
    {
        if (!availableArtifactIDs.Contains(artifactID) || !inspectedArtifactIDs.Add(artifactID))
            return;

        RefreshProgressAndCompleteIfReady();
    }

    private void RefreshProgressAndCompleteIfReady()
    {
        UpdateProgressObjective();

        if (inspectedArtifactIDs.Count >= requiredArtifactCount)
            FinishStep();
    }

    private void UpdateProgressObjective()
    {
        UpdateObjective($"{ObjectiveDescription} ({inspectedArtifactIDs.Count}/{requiredArtifactCount})");
    }

}
