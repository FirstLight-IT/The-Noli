using System;
using UnityEngine;

[Serializable]
public class AmbientArtifactHint
{
    [SerializeField] private ArtifactInfoSO artifact;
    [SerializeField, Min(1)] private int passesRequired = 8;
    [SerializeField, TextArea(2, 5)] private string[] dialogueLines;

    [NonSerialized] public int passCount;
    [NonSerialized] public bool isReady;

    public ArtifactInfoSO Artifact => artifact;
    public int PassesRequired => passesRequired;
    public string[] DialogueLines => dialogueLines;
}

public class AmbientNPC : MonoBehaviour, IInteractable
{
    [SerializeField] private AmbientNPCInfoSO npcData;
    [SerializeField] private AmbientArtifactHint[] artifactHints;
    [SerializeField] private GameObject interactionIcon;
    [SerializeField] private GameObject exclamationIcon;
    [SerializeField] private InteractableOutline interactionOutline;

    private AmbientArtifactHint activeHint;
    private int nextDialogueVariation;

    private void Awake()
    {
        if (interactionOutline == null)
            interactionOutline = GetComponent<InteractableOutline>();

        if (interactionOutline == null)
            interactionOutline = gameObject.AddComponent<InteractableOutline>();

        SetHintIcon(false);
    }

    private void OnEnable()
    {
        Artifact.OnArtifactPassed += HandleArtifactPassed;
        Artifact.OnArtifactUnlocked += HandleArtifactDiscovered;
    }

    private void OnDisable()
    {
        Artifact.OnArtifactPassed -= HandleArtifactPassed;
        Artifact.OnArtifactUnlocked -= HandleArtifactDiscovered;
    }

    private void HandleArtifactPassed(ArtifactInfoSO artifact)
    {
        if (artifact == null || artifactHints == null)
            return;

        foreach (AmbientArtifactHint hint in artifactHints)
        {
            if (hint == null || hint.Artifact != artifact || hint.isReady)
                continue;

            hint.passCount++;
            if (hint.passCount < hint.PassesRequired)
                continue;

            hint.isReady = true;
            SelectReadyHint();
        }
    }

    private void HandleArtifactDiscovered(ArtifactInfoSO artifact)
    {
        if (artifact == null || artifactHints == null)
            return;

        foreach (AmbientArtifactHint hint in artifactHints)
        {
            if (hint == null || hint.Artifact != artifact)
                continue;

            hint.passCount = 0;
            hint.isReady = false;
        }

        SelectReadyHint();
    }

    private void SelectReadyHint()
    {
        activeHint = null;

        if (artifactHints != null)
        {
            foreach (AmbientArtifactHint hint in artifactHints)
            {
                if (hint != null && hint.isReady)
                {
                    activeHint = hint;
                    break;
                }
            }
        }

        SetHintIcon(activeHint != null);
    }

    public void interact()
    {
        if (DialogueController.Instance == null || npcData == null)
            return;

        if (activeHint != null)
        {
            AmbientArtifactHint shownHint = activeHint;
            DialogueController.Instance.ShowAmbientDialogue(
                npcData,
                shownHint.DialogueLines,
                () => FinishHint(shownHint));
            return;
        }

        string[] dialogue = GetNextGenericDialogue();
        if (dialogue != null)
            DialogueController.Instance.ShowAmbientDialogue(npcData, dialogue);
    }

    private string[] GetNextGenericDialogue()
    {
        NPCDialogueSequence[] variations = npcData.DialogueVariations;
        if (variations == null || variations.Length == 0)
            return null;

        // Skip empty Inspector entries without preventing the NPC from speaking.
        for (int offset = 0; offset < variations.Length; offset++)
        {
            int index = (nextDialogueVariation + offset) % variations.Length;
            string[] lines = variations[index]?.lines;

            if (lines == null || lines.Length == 0)
                continue;

            nextDialogueVariation = (index + 1) % variations.Length;
            return lines;
        }

        return null;
    }

    private void FinishHint(AmbientArtifactHint hint)
    {
        hint.passCount = 0;
        hint.isReady = false;
        SelectReadyHint();
    }

    public void showIcon(bool visible)
    {
        if (interactionIcon != null)
            interactionIcon.SetActive(visible);
    }

    public void showHighlight(bool visible) => interactionOutline?.SetHighlighted(visible);
    public void setInteracted() { }
    public bool canInteract() => true;
    public int incrementCounter() => 0;

    private void SetHintIcon(bool visible)
    {
        if (exclamationIcon != null)
            exclamationIcon.SetActive(visible);
    }
}
