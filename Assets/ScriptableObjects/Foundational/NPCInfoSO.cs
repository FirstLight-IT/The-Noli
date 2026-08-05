using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class NPCDialogueSequence
{
    [TextArea(2, 5)]
    public string[] lines;
}

[CreateAssetMenu(fileName = "New NPC Info", menuName = "Characters/NPC Info")]
public class NPCInfoSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string npcID;
    [FormerlySerializedAs("NPCName")]
    [SerializeField] private string displayName;
    [SerializeField] private string characterFullName;
    [SerializeField] private string alias;
    [SerializeField] private string characterCategory;

    [Header("Gallery Information")]
    [SerializeField] private Sprite portrait;
    [SerializeField, TextArea(3, 8)] private string description;
    [SerializeField, TextArea(5, 15)] private string biography;
    [SerializeField, TextArea(2, 5)] private string[] characterFacts;

    public string NpcID => npcID;
    public string DisplayName => displayName;
    public string CharacterFullName => string.IsNullOrWhiteSpace(characterFullName) ? displayName : characterFullName;
    public string Alias => alias;
    public string CharacterCategory => characterCategory;
    public Sprite Portrait => portrait;
    public string Description => description;
    public string Biography => biography;
    public string[] CharacterFacts => characterFacts;

    [Header("Dialogue")]
    
    [Tooltip("Played only the first time the player talks to this NPC.")]
    [FormerlySerializedAs("dialogueLines")]
    public string[] introductionLines;

    [Tooltip("Played on later interactions. If there is more than one sequence, they rotate in order.")]
    public NPCDialogueSequence[] repeatDialogues;

}
