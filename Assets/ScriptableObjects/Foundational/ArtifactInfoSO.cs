using UnityEngine;

[CreateAssetMenu(fileName = "New Artifact", menuName = "Artifacts/Artifact Info")]
public class ArtifactInfoSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string artifactID;
    [SerializeField] private string displayName;
    [SerializeField] private string roomID;

    [Header("Journal Information")]
    [SerializeField] private Sprite image;
    [SerializeField, TextArea(3, 10)] private string[] description;

    public string ArtifactID => artifactID;
    public string DisplayName => displayName;
    public string RoomID => roomID;
    public Sprite Image => image;
    public string[] Description => description;
}
