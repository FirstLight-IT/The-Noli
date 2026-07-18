using System;
using System.Collections;
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
    private Conversation activeConversation;
    private bool isTyping;
    private int currentLineIndex;

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
        if (data == null || data.dialogueLines == null || data.dialogueLines.Length == 0)
        {
            Debug.LogError("NPC dialogue is missing its dialogue lines.", data);
            return;
        }

        activeNPCDialogue = data;
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
            dialogueText.SetText(activeNPCDialogue.dialogueLines[currentLineIndex]);
            ShowNPCCloseButton();
            return;
        }

        currentLineIndex++;

        if (currentLineIndex >= activeNPCDialogue.dialogueLines.Length)
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

        foreach (char letter in activeNPCDialogue.dialogueLines[currentLineIndex])
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
            currentLineIndex == activeNPCDialogue.dialogueLines.Length - 1);
    }

    public void EndNPCDialogue()
    {
        StopAllCoroutines();
        activeNPCDialogue = null;
        ResetDialogueUI();
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
