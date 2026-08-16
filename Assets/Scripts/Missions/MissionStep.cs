using UnityEngine;

public abstract class MissionStep : MonoBehaviour
{
    [SerializeField, TextArea] private string objectiveDescription;

    public string ObjectiveDescription => objectiveDescription;
    public virtual string JournalDescription => objectiveDescription;

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
