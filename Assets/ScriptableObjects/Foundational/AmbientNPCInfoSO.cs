using UnityEngine;

[CreateAssetMenu(fileName = "New Ambient NPC", menuName = "Characters/Ambient NPC Info")]
public class AmbientNPCInfoSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string displayName;

    [Header("Generic Dialogue")]
    [Tooltip("A different dialogue sequence is used after each interaction, then the list loops.")]
    [SerializeField] private NPCDialogueSequence[] dialogueVariations;

    public string DisplayName => displayName;
    public NPCDialogueSequence[] DialogueVariations => dialogueVariations;
}
