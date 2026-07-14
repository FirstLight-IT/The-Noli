using UnityEngine;

public abstract class MissionStep : MonoBehaviour
{
    [SerializeField, TextArea] private string objectiveDescription;

    public string ObjectiveDescription => objectiveDescription;

    protected string MissionId { get; private set; }
    protected int StepIndex { get; private set; }

    private bool isFinished;

    public void Initialize(string missionId, int stepIndex)
    {
        MissionId = missionId;
        StepIndex = stepIndex;
        OnStepActivated();
    }

    protected abstract void OnStepActivated();

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
