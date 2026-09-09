using UnityEngine;

public sealed class MinigameMissionStep : MissionStep
{
    [Header("Minigame Step")]
    [SerializeField] private string minigameSceneName;

    public string MinigameSceneName => minigameSceneName;

    protected override void OnStepActivated()
    {
        minigameSceneName = minigameSceneName?.Trim();
        if (string.IsNullOrWhiteSpace(minigameSceneName))
        {
            FailStep("Minigame mission step needs a scene name.");
            return;
        }

        if (!MinigameTransitionController.TryStart(
                minigameSceneName,
                null,
                HandleTransitionCompleted))
        {
            FailStep($"Could not start minigame scene '{minigameSceneName}'.");
        }
    }

    private void HandleTransitionCompleted(bool succeeded)
    {
        if (succeeded)
            FinishStep();
        else
            FailStep($"Minigame scene '{minigameSceneName}' could not be completed.");
    }
}
