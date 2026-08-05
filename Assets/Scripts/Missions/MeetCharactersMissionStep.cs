using System.Collections.Generic;
using UnityEngine;

public class MeetCharactersMissionStep : MissionStep
{
    [System.Serializable]
    private struct CharacterTarget
    {
        [SerializeField] public string npcId;
        [SerializeField] public string displayName;
    }

    [Header("Meet Characters Step")]
    [SerializeField] private CharacterTarget[] characters = new CharacterTarget[0];

    private readonly HashSet<string> targetNpcIds = new();
    private readonly HashSet<string> metNpcIds = new();

    void OnEnable()
    {
        NPC.OnNPCInteracted += HandleNpcInteracted;
    }

    void OnDisable()
    {
        NPC.OnNPCInteracted -= HandleNpcInteracted;
    }

    protected override void OnStepActivated()
    {
        if (characters == null || characters.Length == 0)
        {
            FailStep("Meet characters step has no character targets.");
            return;
        }

        foreach (CharacterTarget character in characters)
        {
            if (string.IsNullOrWhiteSpace(character.npcId))
            {
                FailStep("Every meet characters target needs an NPC ID.");
                return;
            }

            if (!targetNpcIds.Add(character.npcId))
            {
                FailStep($"Meet characters step contains duplicate NPC ID '{character.npcId}'.");
                return;
            }

            if (NPC.TryGetById(character.npcId, out NPC npc) && npc.beenInteracted)
                metNpcIds.Add(character.npcId);
        }

        UpdateProgressObjective();

        if (metNpcIds.Count == targetNpcIds.Count)
            FinishStep();
    }

    private void HandleNpcInteracted(NPCInfoSO dialogueData)
    {
        if (dialogueData == null ||
            !targetNpcIds.Contains(dialogueData.NpcID) ||
            !metNpcIds.Add(dialogueData.NpcID))
        {
            return;
        }

        UpdateProgressObjective();

        if (metNpcIds.Count == targetNpcIds.Count)
            FinishStep();
    }

    private void UpdateProgressObjective()
    {
        List<string> remainingNames = new();

        foreach (CharacterTarget character in characters)
        {
            if (!metNpcIds.Contains(character.npcId))
                remainingNames.Add(character.displayName);
        }

        string remaining = remainingNames.Count > 0
            ? $" Remaining: {string.Join(", ", remainingNames)}"
            : string.Empty;

        UpdateObjective(
            $"{ObjectiveDescription} ({metNpcIds.Count}/{targetNpcIds.Count}).{remaining}");
    }
}
