using UnityEngine;

[CreateAssetMenu(fileName = "NewNPCDialogue", menuName = "NPC Dialogue")]
public class NPCDialogueData : ScriptableObject
{
    public string NPCName;
    public Sprite portrait;
    
    public float typingSpeed;
    public string[] dialogueLines;
    public bool[] autoProgressLines;
    public float autoProgressDelay;

}
