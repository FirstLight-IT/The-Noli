using UnityEngine;

[CreateAssetMenu(fileName = "New Artifact", menuName = "Artifacts/Artifact Info")]
public class ArtifactInfoSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string artifactID;
    [SerializeField] private string displayName;
    [SerializeField] private string floorID = "ground_floor";

    [Header("Journal Information")]
    [SerializeField] private Sprite image;
    [SerializeField, TextArea] private string description;

    public string ArtifactID => artifactID;
    public string DisplayName => displayName;
    public string FloorID => floorID;
    public Sprite Image => image;
    public string Description => description;
}
