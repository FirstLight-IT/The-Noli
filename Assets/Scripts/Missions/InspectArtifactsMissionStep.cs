using System.Collections.Generic;
using UnityEngine;

public class InspectArtifactsMissionStep : MissionStep
{
    [Header("Inspect Artifacts Step")]
    [SerializeField] private string floorID = "ground_floor";
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
        List<Artifact> availableArtifacts = Artifact.GetActiveOnFloor(floorID);

        if (availableArtifacts.Count < requiredArtifactCount)
        {
            FailStep(
                $"Inspect artifacts step needs {requiredArtifactCount} artifacts on floor '{floorID}', " +
                $"but only found {availableArtifacts.Count}.");
            return;
        }

        foreach (Artifact artifact in availableArtifacts)
            availableArtifactIDs.Add(artifact.ArtifactID);

        UpdateProgressObjective();
    }

    private void HandleArtifactInteracted(string artifactID)
    {
        if (!availableArtifactIDs.Contains(artifactID) || !inspectedArtifactIDs.Add(artifactID))
            return;

        UpdateProgressObjective();

        if (inspectedArtifactIDs.Count >= requiredArtifactCount)
            FinishStep();
    }

    private void UpdateProgressObjective()
    {
        UpdateObjective($"{ObjectiveDescription} ({inspectedArtifactIDs.Count}/{requiredArtifactCount})");
    }

}
