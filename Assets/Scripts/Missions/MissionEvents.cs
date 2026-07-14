using System;

public static class MissionEvents
{
    public static event Action<string, int> OnMissionStepFinished;
    public static event Action<string, int, string> OnMissionStepFailed;
    public static event Action<string, int, string> OnMissionObjectiveUpdated;

    public static void FinishMissionStep(string missionId, int stepIndex)
    {
        OnMissionStepFinished?.Invoke(missionId, stepIndex);
    }

    public static void UpdateMissionObjective(string missionId, int stepIndex, string objective)
    {
        OnMissionObjectiveUpdated?.Invoke(missionId, stepIndex, objective);
    }

    public static void FailMissionStep(string missionId, int stepIndex, string reason)
    {
        OnMissionStepFailed?.Invoke(missionId, stepIndex, reason);
    }
}
