using UnityEngine;

[CreateAssetMenu(fileName = "New Mission", menuName = "Missions/Mission Info")]
public class MissionInfoSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string missionId;
    [SerializeField] private string displayName;

    [Header("Requirements")]
    [SerializeField] private MissionInfoSO[] prerequisites = new MissionInfoSO[0];
    [SerializeField] private bool autoStartWhenAvailable;

    [Header("Ordered Steps")]
    [SerializeField] private MissionStep[] missionStepPrefabs = new MissionStep[0];

    public string MissionId => missionId;
    public string DisplayName => displayName;
    public MissionInfoSO[] Prerequisites => prerequisites;
    public bool AutoStartWhenAvailable => autoStartWhenAvailable;
    public MissionStep[] MissionStepPrefabs => missionStepPrefabs;
}
