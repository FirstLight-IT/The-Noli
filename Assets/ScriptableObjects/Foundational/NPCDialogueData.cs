using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class NPCDialogueSequence
{
    [TextArea(2, 5)]
    public string[] lines;
}

[CreateAssetMenu(fileName = "NewNPCDialogue", menuName = "NPC Dialogue")]
public class NPCDialogueData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string npcID;

    public string NpcID => npcID;

    [Header("Dialogue")]
    public string NPCName;
    public Sprite portrait;
    
    public float typingSpeed = 0.05f;

    [Tooltip("Played only the first time the player talks to this NPC.")]
    [FormerlySerializedAs("dialogueLines")]
    public string[] introductionLines;

    [Tooltip("Played on later interactions. If there is more than one sequence, they rotate in order.")]
    public NPCDialogueSequence[] repeatDialogues;

}
