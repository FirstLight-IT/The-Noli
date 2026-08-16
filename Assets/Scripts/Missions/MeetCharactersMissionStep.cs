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

    public override string JournalDescription => BuildCharacterList(includeMetCharacters: true);

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

            if (JournalUnlockRegistry.IsUnlocked(
                    JournalUnlockRegistry.CharacterCollection,
                    character.npcId) ||
                (NPC.TryGetById(character.npcId, out NPC npc) && npc.beenInteracted))
            {
                metNpcIds.Add(character.npcId);
            }
        }

        RefreshProgressAndCompleteIfReady();
    }

    public override MissionStepProgressSaveData CaptureProgress()
    {
        MissionStepProgressSaveData progress = new();
        progress.completedTargetIds.AddRange(metNpcIds);
        progress.Normalize();
        return progress;
    }

    protected override void RestoreProgress(MissionStepProgressSaveData savedProgress)
    {
        if (savedProgress?.completedTargetIds != null)
        {
            foreach (string npcId in savedProgress.completedTargetIds)
            {
                if (targetNpcIds.Contains(npcId))
                    metNpcIds.Add(npcId);
            }
        }

        RefreshProgressAndCompleteIfReady();
    }

    private void HandleNpcInteracted(NPCInfoSO dialogueData)
    {
        if (dialogueData == null ||
            !targetNpcIds.Contains(dialogueData.NpcID) ||
            !metNpcIds.Add(dialogueData.NpcID))
        {
            return;
        }

        RefreshProgressAndCompleteIfReady();
    }

    private void RefreshProgressAndCompleteIfReady()
    {
        UpdateProgressObjective();

        if (metNpcIds.Count == targetNpcIds.Count)
            FinishStep();
    }

    private void UpdateProgressObjective()
    {
        UpdateObjective(BuildCharacterList(includeMetCharacters: false));
    }

    private string BuildCharacterList(bool includeMetCharacters)
    {
        List<string> names = new();

        if (characters != null)
        {
            foreach (CharacterTarget character in characters)
            {
                if (includeMetCharacters || !metNpcIds.Contains(character.npcId))
                    names.Add($"• {character.displayName}");
            }
        }

        return names.Count == 0
            ? ObjectiveDescription
            : $"{ObjectiveDescription}\n{string.Join("\n", names)}";
    }
}
