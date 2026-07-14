using UnityEngine;

public class ArtifactMissionStep : MissionStep
{
    [Header("Artifact Step")]
    [SerializeField] private ArtifactInfoSO targetArtifactInfo;

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
        if (targetArtifactInfo == null)
        {
            FailStep("Artifact mission step needs target Artifact Info.");
            return;
        }

        if (!Artifact.TryGetById(targetArtifactInfo.ArtifactID, out _))
        {
            FailStep($"Could not find an active artifact with ID '{targetArtifactInfo.ArtifactID}'.");
            return;
        }
    }

    private void HandleArtifactInteracted(string artifactId)
    {
        if (targetArtifactInfo == null || artifactId != targetArtifactInfo.ArtifactID)
            return;

        FinishStep();
    }
}
