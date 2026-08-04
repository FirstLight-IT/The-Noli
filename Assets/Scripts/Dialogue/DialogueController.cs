using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueController : MonoBehaviour
{
    public static event Action<string> OnConversationFinished;
    public static event Action<string, string> OnConversationFailed;

    public static DialogueController Instance { get; private set; }
    public bool IsDialogueActive { get; private set; }

    [SerializeField] private SpeakerRegistry speakerRegistry;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Button dialogueCloseButton;

    private NPCDialogueData activeNPCDialogue;
    private string[] activeNPCDialogueLines;
    private Conversation activeConversation;
    private bool isTyping;
    private bool announceNPCUnlockOnClose;
    private int currentLineIndex;
    private readonly HashSet<string> introducedNPCs = new();
    private readonly Dictionary<string, int> nextRepeatDialogueByNPC = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandleNPCInteraction(NPCDialogueData data)
    {
        if (!IsDialogueActive)
        {
            StartNPCDialogue(data);
            return;
        }

        if (activeNPCDialogue == data)
            AdvanceNPCDialogue();
    }

    private void StartNPCDialogue(NPCDialogueData data)
    {
        if (data == null)
        {
            Debug.LogError("Cannot start dialogue for a null NPC.", this);
            return;
        }

        string npcID = data.NpcID;
        bool hasBeenIntroduced = !string.IsNullOrWhiteSpace(npcID) && introducedNPCs.Contains(npcID);
        string[] selectedLines = hasBeenIntroduced
            ? GetNextRepeatDialogue(data)
            : data.introductionLines;

        if (!HasDialogueLines(selectedLines))
        {
            string dialogueType = hasBeenIntroduced ? "repeat" : "introduction";
            Debug.LogError($"NPC '{data.NPCName}' is missing its {dialogueType} dialogue lines.", data);
            return;
        }

        if (!string.IsNullOrWhiteSpace(npcID))
            introducedNPCs.Add(npcID);

        activeNPCDialogue = data;
        announceNPCUnlockOnClose = !hasBeenIntroduced;
        activeNPCDialogueLines = selectedLines;
        activeConversation = null;
        currentLineIndex = 0;
        IsDialogueActive = true;

        dialoguePanel.SetActive(true);
        dialogueCloseButton.gameObject.SetActive(false);
        nameText.SetText(data.NPCName);
        portraitImage.sprite = data.portrait;
        StartCoroutine(TypeNPCLine());
    }

    private void AdvanceNPCDialogue()
    {
        if (isTyping)
        {
            StopAllCoroutines();
            isTyping = false;
            dialogueText.SetText(activeNPCDialogueLines[currentLineIndex]);
            ShowNPCCloseButton();
            return;
        }

        currentLineIndex++;

        if (currentLineIndex >= activeNPCDialogueLines.Length)
        {
            EndNPCDialogue();
            return;
        }

        StartCoroutine(TypeNPCLine());
    }

    private IEnumerator TypeNPCLine()
    {
        isTyping = true;
        dialogueText.SetText(string.Empty);

        foreach (char letter in activeNPCDialogueLines[currentLineIndex])
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(activeNPCDialogue.typingSpeed);
        }

        isTyping = false;
        ShowNPCCloseButton();
    }

    private void ShowNPCCloseButton()
    {
        dialogueCloseButton.gameObject.SetActive(
            currentLineIndex == activeNPCDialogueLines.Length - 1);
    }

    public void EndNPCDialogue()
    {
        StopAllCoroutines();
        NPCDialogueData finishedNPC = activeNPCDialogue;
        bool shouldAnnounceUnlock = announceNPCUnlockOnClose;
        activeNPCDialogue = null;
        activeNPCDialogueLines = null;
        announceNPCUnlockOnClose = false;
        ResetDialogueUI();

        if (shouldAnnounceUnlock && finishedNPC != null)
        {
            JournalUnlockRegistry.Unlock("characters", finishedNPC.NpcID);
            UnlockNotificationController.ShowCharacter(finishedNPC.NPCName, finishedNPC.portrait);
        }
    }

    private string[] GetNextRepeatDialogue(NPCDialogueData data)
    {
        if (data.repeatDialogues == null || data.repeatDialogues.Length == 0)
            return null;

        int nextIndex = nextRepeatDialogueByNPC.TryGetValue(data.NpcID, out int savedIndex)
            ? savedIndex
            : 0;

        // Skip accidentally empty sequences without making the NPC unusable.
        for (int offset = 0; offset < data.repeatDialogues.Length; offset++)
        {
            int index = (nextIndex + offset) % data.repeatDialogues.Length;
            string[] lines = data.repeatDialogues[index]?.lines;
            if (!HasDialogueLines(lines))
                continue;

            nextRepeatDialogueByNPC[data.NpcID] = (index + 1) % data.repeatDialogues.Length;
            return lines;
        }

        return null;
    }

    private static bool HasDialogueLines(string[] lines)
    {
        if (lines == null || lines.Length == 0)
            return false;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;
        }

        return true;
    }

    // Kept as the common UI entry point because existing Unity Button events
    // are serialized with this method name.
    public void EndDialogue()
    {
        if (activeConversation != null)
        {
            EndConversation();
            return;
        }

        EndNPCDialogue();
    }

    private void HandleConversationInteraction(Conversation conversation)
    {
        if (!IsDialogueActive)
        {
            StartConversation(conversation);
            return;
        }

        if (activeConversation == conversation)
            AdvanceConversation();
    }

    public void StartConversation(Conversation conversation)
    {
        if (!ValidateConversation(conversation, out string reason))
        {
            string conversationId = conversation?.conversationId;
            Debug.LogError(reason, this);
            OnConversationFailed?.Invoke(conversationId, reason);
            return;
        }

        activeConversation = conversation;
        activeNPCDialogue = null;
        currentLineIndex = 0;
        IsDialogueActive = true;

        dialoguePanel.SetActive(true);
        dialogueCloseButton.gameObject.SetActive(false);
        StartCoroutine(TypeConversationLine());
    }

    private void AdvanceConversation()
    {
        if (isTyping)
        {
            StopAllCoroutines();
            isTyping = false;
            dialogueText.SetText(activeConversation.lines[currentLineIndex].text);
            ShowConversationCloseButton();
            return;
        }

        currentLineIndex++;

        if (currentLineIndex >= activeConversation.lines.Count)
        {
            EndConversation();
            return;
        }

        StartCoroutine(TypeConversationLine());
    }

    private IEnumerator TypeConversationLine()
    {
        DialogueLine line = activeConversation.lines[currentLineIndex];
        speakerRegistry.TryGetSpeaker(line.speaker, out NPCDialogueData speaker);

        nameText.SetText(speaker.NPCName);
        portraitImage.sprite = speaker.portrait;
        dialogueText.SetText(string.Empty);
        isTyping = true;

        foreach (char letter in line.text)
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(speaker.typingSpeed);
        }

        isTyping = false;
        ShowConversationCloseButton();
    }

    private void ShowConversationCloseButton()
    {
        dialogueCloseButton.gameObject.SetActive(
            currentLineIndex == activeConversation.lines.Count - 1);
    }

    public void EndConversation()
    {
        StopAllCoroutines();
        string finishedConversationId = activeConversation?.conversationId;
        activeConversation = null;
        ResetDialogueUI();

        if (!string.IsNullOrWhiteSpace(finishedConversationId))
            OnConversationFinished?.Invoke(finishedConversationId);
    }

    private bool ValidateConversation(Conversation conversation, out string reason)
    {
        if (conversation == null)
        {
            reason = "Cannot start a null conversation.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(conversation.conversationId))
        {
            reason = "Conversation is missing its conversation ID.";
            return false;
        }

        if (conversation.lines == null || conversation.lines.Count == 0)
        {
            reason = $"Conversation '{conversation.conversationId}' has no lines.";
            return false;
        }

        foreach (DialogueLine line in conversation.lines)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.text))
            {
                reason = $"Conversation '{conversation.conversationId}' contains an empty line.";
                return false;
            }

            if (speakerRegistry == null || !speakerRegistry.TryGetSpeaker(line.speaker, out _))
            {
                reason = $"Conversation '{conversation.conversationId}' references unknown speaker '{line?.speaker}'.";
                return false;
            }
        }

        reason = null;
        return true;
    }

    private void ResetDialogueUI()
    {
        currentLineIndex = 0;
        isTyping = false;
        IsDialogueActive = false;
        dialogueText.SetText(string.Empty);
        nameText.SetText(string.Empty);
        portraitImage.sprite = null;
        dialogueCloseButton.gameObject.SetActive(false);
        dialoguePanel.SetActive(false);
    }

    void OnEnable()
    {
        NPC.OnNPCInteracted += HandleNPCInteraction;
        NPC.OnMissionConversationInteracted += HandleConversationInteraction;
    }

    void OnDisable()
    {
        NPC.OnNPCInteracted -= HandleNPCInteraction;
        NPC.OnMissionConversationInteracted -= HandleConversationInteraction;
    }
}
