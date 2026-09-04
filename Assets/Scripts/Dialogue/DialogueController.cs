using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueController : MonoBehaviour
{
    private const float TypingSpeed = 0.05f;

    public static event Action<string> OnConversationFinished;
    public static event Action<string, string> OnConversationFailed;
    public static event Action<ConversationReadingResult> OnConversationReadingCompleted;

    public static DialogueController Instance { get; private set; }
    public bool IsDialogueActive { get; private set; }

    [SerializeField] private SpeakerRegistry speakerRegistry;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Button dialogueCloseButton;
    [Tooltip("A line counts as skipped when the player reveals it, then advances within this many seconds.")]
    [SerializeField, Min(0f)] private float rapidAdvanceWindowSeconds = 1f;

    private NPCInfoSO activeNPCDialogue;
    private AmbientNPCInfoSO activeAmbientNPCDialogue;
    private string[] activeNPCDialogueLines;
    private Conversation activeConversation;
    private List<DialogueLine> activeConversationLines;
    private ConversationSkipTracker conversationSkipTracker;
    private bool isTyping;
    private bool announceNPCUnlockOnClose;
    private Action ambientDialogueFinished;
    private int currentLineIndex;
    private readonly HashSet<string> introducedNPCs = new();
    private readonly HashSet<string> newlyUnlockedNPCs = new();
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

    private void HandleNPCInteraction(NPCInfoSO data)
    {
        if (!IsDialogueActive)
        {
            StartNPCDialogue(data);
            return;
        }

        if (activeNPCDialogue == data)
            AdvanceNPCDialogue();
    }

    private void StartNPCDialogue(NPCInfoSO data)
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
            : data.IntroductionLines;

        if (!HasDialogueLines(selectedLines))
        {
            string dialogueType = hasBeenIntroduced ? "repeat" : "introduction";
            Debug.LogError($"NPC '{data.DisplayName}' is missing its {dialogueType} dialogue lines.", data);
            return;
        }

        if (!string.IsNullOrWhiteSpace(npcID))
            introducedNPCs.Add(npcID);

        activeNPCDialogue = data;
        activeAmbientNPCDialogue = null;
        ambientDialogueFinished = null;
        announceNPCUnlockOnClose = newlyUnlockedNPCs.Remove(npcID);
        activeNPCDialogueLines = selectedLines;
        activeConversation = null;
        currentLineIndex = 0;
        IsDialogueActive = true;

        dialoguePanel.SetActive(true);
        dialogueCloseButton.gameObject.SetActive(false);
        nameText.SetText(data.DisplayName);
        portraitImage.gameObject.SetActive(true);
        portraitImage.sprite = data.Portrait;
        StartCoroutine(TypeNPCLine());
    }

    public bool ShowCharacterLine(NPCInfoSO speaker, string line)
    {
        if (IsDialogueActive || speaker == null || string.IsNullOrWhiteSpace(line))
            return false;

        activeNPCDialogue = speaker;
        activeAmbientNPCDialogue = null;
        ambientDialogueFinished = null;
        activeNPCDialogueLines = new[] { line };
        activeConversation = null;
        announceNPCUnlockOnClose = false;
        currentLineIndex = 0;
        IsDialogueActive = true;

        dialoguePanel.SetActive(true);
        dialogueCloseButton.gameObject.SetActive(false);
        nameText.SetText(speaker.DisplayName);
        portraitImage.gameObject.SetActive(true);
        portraitImage.sprite = speaker.Portrait;
        StartCoroutine(TypeNPCLine());
        return true;
    }

    public bool ShowAmbientDialogue(
        AmbientNPCInfoSO speaker,
        string[] lines,
        Action onFinished = null)
    {
        if (IsDialogueActive || speaker == null || !HasDialogueLines(lines))
            return false;

        activeNPCDialogue = null;
        activeAmbientNPCDialogue = speaker;
        activeNPCDialogueLines = lines;
        activeConversation = null;
        ambientDialogueFinished = onFinished;
        announceNPCUnlockOnClose = false;
        currentLineIndex = 0;
        IsDialogueActive = true;

        dialoguePanel.SetActive(true);
        dialogueCloseButton.gameObject.SetActive(false);
        nameText.SetText(speaker.DisplayName);
        portraitImage.sprite = null;
        portraitImage.gameObject.SetActive(false);
        StartCoroutine(TypeNPCLine());
        return true;
    }

    public bool AdvanceActiveDialogue()
    {
        if (!IsDialogueActive)
            return false;

        if (activeConversation != null)
            AdvanceConversation();
        else if (activeNPCDialogue != null || activeAmbientNPCDialogue != null)
            AdvanceNPCDialogue();
        else
            return false;

        return true;
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

        string line = activeNPCDialogueLines[currentLineIndex];
        if (string.Equals(line, "...", StringComparison.Ordinal))
        {
            dialogueText.SetText("...");
            dialogueText.ForceMeshUpdate();
            isTyping = false;
            ShowNPCCloseButton();
            yield break;
        }

        foreach (char letter in line)
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(TypingSpeed);
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
        NPCInfoSO finishedNPC = activeNPCDialogue;
        Action finishedAmbientDialogue = ambientDialogueFinished;
        bool shouldAnnounceUnlock = announceNPCUnlockOnClose;
        activeNPCDialogue = null;
        activeAmbientNPCDialogue = null;
        activeNPCDialogueLines = null;
        ambientDialogueFinished = null;
        announceNPCUnlockOnClose = false;
        ResetDialogueUI();

        if (shouldAnnounceUnlock && finishedNPC != null)
        {
            UnlockNotificationController.ShowCharacter(finishedNPC.DisplayName, finishedNPC.Portrait);
        }

        finishedAmbientDialogue?.Invoke();
    }

    private string[] GetNextRepeatDialogue(NPCInfoSO data)
    {
        if (data.RepeatDialogueCount == 0)
            return null;

        int nextIndex = nextRepeatDialogueByNPC.TryGetValue(data.NpcID, out int savedIndex)
            ? savedIndex
            : 0;

        // Skip accidentally empty sequences without making the NPC unusable.
        for (int offset = 0; offset < data.RepeatDialogueCount; offset++)
        {
            int index = (nextIndex + offset) % data.RepeatDialogueCount;
            string[] lines = data.GetRepeatDialogueLines(index);
            if (!HasDialogueLines(lines))
                continue;

            nextRepeatDialogueByNPC[data.NpcID] = (index + 1) % data.RepeatDialogueCount;
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
        activeConversationLines = conversation.ResolveLines(GameLanguage.CurrentCode);
        conversationSkipTracker = new ConversationSkipTracker(rapidAdvanceWindowSeconds);
        activeNPCDialogue = null;
        activeAmbientNPCDialogue = null;
        ambientDialogueFinished = null;
        currentLineIndex = 0;
        IsDialogueActive = true;

        dialoguePanel.SetActive(true);
        portraitImage.gameObject.SetActive(true);
        dialogueCloseButton.gameObject.SetActive(false);
        StartCoroutine(TypeConversationLine());
    }

    private void AdvanceConversation()
    {
        if (isTyping)
        {
            StopAllCoroutines();
            isTyping = false;
            dialogueText.SetText(activeConversationLines[currentLineIndex].text);
            conversationSkipTracker.MarkTypewriterSkipped(Time.unscaledTime);
            ShowConversationCloseButton();
            return;
        }

        conversationSkipTracker.CompleteLine(Time.unscaledTime);
        currentLineIndex++;

        if (currentLineIndex >= activeConversationLines.Count)
        {
            EndConversation();
            return;
        }

        StartCoroutine(TypeConversationLine());
    }

    private IEnumerator TypeConversationLine()
    {
        DialogueLine line = activeConversationLines[currentLineIndex];
        speakerRegistry.TryGetSpeaker(line.speakerId, out NPCInfoSO speaker);
        conversationSkipTracker.BeginLine();

        nameText.SetText(speaker.DisplayName);
        portraitImage.sprite = speaker.Portrait;
        dialogueText.SetText(string.Empty);
        isTyping = true;

        foreach (char letter in line.text)
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(TypingSpeed);
        }

        isTyping = false;
        ShowConversationCloseButton();
    }

    private void ShowConversationCloseButton()
    {
        dialogueCloseButton.gameObject.SetActive(
            currentLineIndex == activeConversationLines.Count - 1);
    }

    public void EndConversation()
    {
        StopAllCoroutines();
        string finishedConversationId = activeConversation?.conversationId;
        ConversationReadingResult readingResult = null;

        if (activeConversation != null && conversationSkipTracker != null)
        {
            conversationSkipTracker.CompleteLine(Time.unscaledTime);
            readingResult = conversationSkipTracker.CompleteConversation(
                finishedConversationId);
        }

        activeConversation = null;
        activeConversationLines = null;
        conversationSkipTracker = null;
        ResetDialogueUI();

        if (readingResult != null)
            OnConversationReadingCompleted?.Invoke(readingResult);

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

        List<DialogueLine> resolvedLines = conversation.ResolveLines(GameLanguage.CurrentCode);

        if (resolvedLines == null || resolvedLines.Count == 0)
        {
            reason = $"Conversation '{conversation.conversationId}' has no usable language block.";
            return false;
        }

        foreach (DialogueLine line in resolvedLines)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.text))
            {
                reason = $"Conversation '{conversation.conversationId}' contains an empty line.";
                return false;
            }

            if (speakerRegistry == null || !speakerRegistry.TryGetSpeaker(line.speakerId, out _))
            {
                reason = $"Conversation '{conversation.conversationId}' references unknown speaker '{line?.speakerId}'.";
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
        NPC.OnNPCUnlocked += HandleNPCUnlocked;
        NPC.OnNPCInteracted += HandleNPCInteraction;
        NPC.OnMissionConversationInteracted += HandleConversationInteraction;
    }

    void OnDisable()
    {
        NPC.OnNPCUnlocked -= HandleNPCUnlocked;
        NPC.OnNPCInteracted -= HandleNPCInteraction;
        NPC.OnMissionConversationInteracted -= HandleConversationInteraction;
        newlyUnlockedNPCs.Clear();
    }

    private void HandleNPCUnlocked(NPCInfoSO data)
    {
        if (data != null && !string.IsNullOrWhiteSpace(data.NpcID))
            newlyUnlockedNPCs.Add(data.NpcID);
    }
}
