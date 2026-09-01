using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SpeakToAmbientNPCsMissionStep : MissionStep
{
    [Header("Speak To Ambient NPCs Step")]
    [SerializeField] private AmbientNPCTag requiredTag = AmbientNPCTag.Girl;
    [SerializeField, Min(1)] private int requiredUniqueCount = 3;

    private readonly HashSet<string> spokenNpcIDs = new(StringComparer.OrdinalIgnoreCase);
    private bool isActive;

    private void OnEnable()
    {
        AmbientNPC.OnAmbientDialogueFinished += HandleAmbientDialogueFinished;
    }

    private void OnDisable()
    {
        AmbientNPC.OnAmbientDialogueFinished -= HandleAmbientDialogueFinished;
        isActive = false;
    }

    protected override void OnStepActivated()
    {
        if (requiredTag == AmbientNPCTag.None)
        {
            FailStep("Speak to ambient NPCs mission step needs a required tag.");
            return;
        }

        if (requiredUniqueCount < 1)
        {
            FailStep("Speak to ambient NPCs mission step needs a required count of at least one.");
            return;
        }

        isActive = true;
        RefreshProgressAndCompleteIfReady();
    }

    public override MissionStepProgressSaveData CaptureProgress()
    {
        MissionStepProgressSaveData progress = new();
        progress.completedTargetIds.AddRange(spokenNpcIDs);
        progress.Normalize();
        return progress;
    }

    protected override void RestoreProgress(MissionStepProgressSaveData savedProgress)
    {
        if (savedProgress?.completedTargetIds != null)
        {
            foreach (string npcID in savedProgress.completedTargetIds)
            {
                if (!string.IsNullOrWhiteSpace(npcID))
                    spokenNpcIDs.Add(npcID.Trim());
            }
        }

        RefreshProgressAndCompleteIfReady();
    }

    private void HandleAmbientDialogueFinished(AmbientNPCInfoSO npcData)
    {
        if (!isActive ||
            npcData == null ||
            !npcData.HasTag(requiredTag) ||
            string.IsNullOrWhiteSpace(npcData.NpcID) ||
            !spokenNpcIDs.Add(npcData.NpcID.Trim()))
        {
            return;
        }

        RefreshProgressAndCompleteIfReady();
    }

    private void RefreshProgressAndCompleteIfReady()
    {
        UpdateObjective($"{ObjectiveDescription} ({spokenNpcIDs.Count}/{requiredUniqueCount})");

        if (spokenNpcIDs.Count >= requiredUniqueCount)
            FinishStep();
    }
}
