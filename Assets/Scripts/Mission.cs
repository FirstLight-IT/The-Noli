public class Mission
{
    public MissionInfoSO Info { get; }
    public MissionState State { get; set; }
    public int CurrentStepIndex { get; set; }

    public Mission(MissionInfoSO info, MissionState startingState)
    {
        Info = info;
        State = startingState;
        CurrentStepIndex = 0;
    }

    public bool HasAnotherStep => CurrentStepIndex < Info.MissionStepPrefabs.Length;
}
