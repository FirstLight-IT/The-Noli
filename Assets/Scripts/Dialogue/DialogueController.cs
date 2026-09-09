using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class DialogueController : MonoBehaviour
{
    private const float TypingSpeed = 0.05f;

    private enum SpeakerCameraState
    {
        None,
        FocusingSpeaker,
        TrackingSpeaker,
        ReturningToPlayer
    }

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

    [Header("Speaker Camera Focus")]
    [Tooltip("Smoothly pan the active Cinemachine camera toward whoever is speaking.")]
    [SerializeField] private bool focusCameraOnSpeakers = true;
    [SerializeField, Min(0f)] private float speakerPanDuration = 0.45f;
    [SerializeField, Min(0f)] private float speakerReturnDuration = 0.35f;
    [Tooltip("Apply a subtle zoom while the camera is focused on a dialogue speaker.")]
    [SerializeField] private bool zoomCameraOnSpeakers = true;
    [Tooltip("Multiplier applied to the normal camera size. A smaller value zooms in farther.")]
    [SerializeField, Range(0.25f, 1f)] private float speakerZoomScale = 0.9f;

    private NPCInfoSO activeNPCDialogue;
    private AmbientNPCInfoSO activeAmbientNPCDialogue;
    private string[] activeNPCDialogueLines;
    private Conversation activeConversation;
    private List<DialogueLine> activeConversationLines;
    private ConversationSkipTracker conversationSkipTracker;
    private bool isTyping;
    private bool isConversationInterrupted;
    private bool announceNPCUnlockOnClose;
    private Action ambientDialogueFinished;
    private int currentLineIndex;
    private readonly HashSet<string> introducedNPCs = new();
    private readonly HashSet<string> newlyUnlockedNPCs = new();
    private readonly HashSet<string> triggeredConversationInterruptions = new();
    private readonly Dictionary<string, int> nextRepeatDialogueByNPC = new();
    private CinemachineCamera speakerCamera;
    private Transform originalCameraFollow;
    private GameObject speakerCameraTarget;
    private Transform activeSpeakerTransform;
    private SpeakerCameraState speakerCameraState;
    private Vector3 speakerCameraMoveStart;
    private float speakerCameraMoveElapsed;
    private bool speakerCameraUsesOrthographicLens;
    private float originalSpeakerCameraZoom;
    private float speakerCameraMoveStartZoom;

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
        RestoreSpeakerCameraImmediately();

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        UpdateSpeakerCameraFocus();
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
        FocusCameraOnCharacter(data);
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
        FocusCameraOnCharacter(speaker);
        StartCoroutine(TypeNPCLine());
        return true;
    }

    public bool ShowAmbientDialogue(
        AmbientNPCInfoSO speaker,
        string[] lines,
        Action onFinished = null,
        Transform speakerTransform = null,
        bool focusSpeaker = true)
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

        if (focusSpeaker)
            FocusCameraOnTransform(speakerTransform);

        StartCoroutine(TypeNPCLine());
        return true;
    }

    public bool AdvanceActiveDialogue()
    {
        if (!IsDialogueActive)
            return false;

        // Consume the world interaction input while the additive minigame owns input.
        if (isConversationInterrupted)
            return true;

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

        foreach (char letter in activeNPCDialogueLines[currentLineIndex])
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
        isConversationInterrupted = false;
        triggeredConversationInterruptions.Clear();
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
        if (isConversationInterrupted)
            return;

        if (isTyping)
        {
            StopAllCoroutines();
            isTyping = false;
            dialogueText.SetText(activeConversationLines[currentLineIndex].text);
            conversationSkipTracker.MarkTypewriterSkipped(Time.unscaledTime);
            ShowConversationCloseButton();
            return;
        }

        int completedLineIndex = currentLineIndex;
        conversationSkipTracker.CompleteLine(Time.unscaledTime);
        currentLineIndex++;

        if (TryStartConversationInterruption(completedLineIndex))
            return;

        if (currentLineIndex >= activeConversationLines.Count)
        {
            EndConversation();
            return;
        }

        StartCoroutine(TypeConversationLine());
    }

    private bool TryStartConversationInterruption(int completedLineIndex)
    {
        if (activeConversation == null ||
            !activeConversation.TryGetInterruptionAfterLine(
                completedLineIndex,
                out ConversationInterruption interruption) ||
            interruption == null)
        {
            return false;
        }

        string interruptionId = interruption.interruptionId.Trim();
        if (!triggeredConversationInterruptions.Add(interruptionId))
            return false;

        isConversationInterrupted = true;
        dialogueCloseButton.gameObject.SetActive(false);

        if (MinigameTransitionController.TryStart(
                interruption.minigameSceneName,
                HideDialogueForMinigame,
                ResumeConversationAfterMinigame))
        {
            return true;
        }

        isConversationInterrupted = false;
        dialoguePanel.SetActive(true);
        return false;
    }

    private void HideDialogueForMinigame()
    {
        if (isConversationInterrupted)
            dialoguePanel.SetActive(false);
    }

    private void ResumeConversationAfterMinigame(bool minigameCompleted)
    {
        if (!isConversationInterrupted || activeConversation == null)
            return;

        isConversationInterrupted = false;
        dialoguePanel.SetActive(true);

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
        FocusCameraOnCharacter(speaker);

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
        if (isConversationInterrupted)
            return;

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
        triggeredConversationInterruptions.Clear();
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

        if (conversation.interruptions != null)
        {
            HashSet<string> interruptionIds = new(StringComparer.Ordinal);
            HashSet<int> interruptionLineIndices = new();

            foreach (ConversationInterruption interruption in conversation.interruptions)
            {
                if (interruption == null ||
                    string.IsNullOrWhiteSpace(interruption.interruptionId) ||
                    string.IsNullOrWhiteSpace(interruption.minigameSceneName))
                {
                    reason = $"Conversation '{conversation.conversationId}' contains an incomplete minigame interruption.";
                    return false;
                }

                if (!interruptionIds.Add(interruption.interruptionId.Trim()))
                {
                    reason = $"Conversation '{conversation.conversationId}' contains duplicate interruption ID '{interruption.interruptionId}'.";
                    return false;
                }

                if (interruption.afterLineIndex < 0 ||
                    interruption.afterLineIndex >= resolvedLines.Count ||
                    !interruptionLineIndices.Add(interruption.afterLineIndex))
                {
                    reason = $"Conversation '{conversation.conversationId}' has an invalid or duplicate interruption line index {interruption.afterLineIndex}.";
                    return false;
                }
            }
        }

        reason = null;
        return true;
    }

    private void ResetDialogueUI()
    {
        currentLineIndex = 0;
        isTyping = false;
        isConversationInterrupted = false;
        IsDialogueActive = false;
        dialogueText.SetText(string.Empty);
        nameText.SetText(string.Empty);
        portraitImage.sprite = null;
        dialogueCloseButton.gameObject.SetActive(false);
        dialoguePanel.SetActive(false);
        ReturnSpeakerCameraToPlayer();
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
        RestoreSpeakerCameraImmediately();
    }

    private void HandleNPCUnlocked(NPCInfoSO data)
    {
        if (data != null && !string.IsNullOrWhiteSpace(data.NpcID))
            newlyUnlockedNPCs.Add(data.NpcID);
    }

    private void FocusCameraOnCharacter(NPCInfoSO speaker)
    {
        if (speaker == null || string.IsNullOrWhiteSpace(speaker.NpcID))
            return;

        bool isPlayerSpeaking = PlayerCharacter.Instance != null &&
            PlayerCharacter.Instance.CurrentCharacter != null &&
            string.Equals(
                PlayerCharacter.Instance.CurrentCharacter.NpcID,
                speaker.NpcID,
                StringComparison.OrdinalIgnoreCase);

        if (isPlayerSpeaking)
        {
            if (TryPrepareSpeakerCamera())
                FocusCameraOnTransform(originalCameraFollow);

            return;
        }

        if (NPC.TryGetById(speaker.NpcID, out NPC npc))
            FocusCameraOnTransform(npc.DialogueCameraFocus);
    }

    private void FocusCameraOnTransform(Transform speakerTransform)
    {
        if (!focusCameraOnSpeakers ||
            speakerTransform == null ||
            AmbientNPC.IsHintCameraPanning ||
            !TryPrepareSpeakerCamera())
        {
            return;
        }

        if (activeSpeakerTransform == speakerTransform &&
            speakerCameraState != SpeakerCameraState.ReturningToPlayer)
        {
            return;
        }

        activeSpeakerTransform = speakerTransform;
        BeginSpeakerCameraMove(SpeakerCameraState.FocusingSpeaker, speakerPanDuration);
    }

    private bool TryPrepareSpeakerCamera()
    {
        if (!focusCameraOnSpeakers || AmbientNPC.IsHintCameraPanning)
            return false;

        if (speakerCamera != null &&
            originalCameraFollow != null &&
            speakerCameraTarget != null)
        {
            return true;
        }

        CinemachineCamera activeCamera = GetActiveCinemachineCamera();
        if (activeCamera == null || activeCamera.Follow == null)
            return false;

        speakerCamera = activeCamera;
        originalCameraFollow = activeCamera.Follow;
        speakerCameraTarget = new GameObject("Dialogue Speaker Camera Target");
        speakerCameraTarget.hideFlags = HideFlags.HideAndDontSave;
        speakerCameraTarget.transform.position = originalCameraFollow.position;
        Camera mainCamera = Camera.main;
        speakerCameraUsesOrthographicLens = mainCamera != null && mainCamera.orthographic;
        originalSpeakerCameraZoom = GetCurrentSpeakerCameraZoom();
        speakerCamera.Follow = speakerCameraTarget.transform;
        return true;
    }

    private void BeginSpeakerCameraMove(SpeakerCameraState state, float duration)
    {
        if (speakerCameraTarget == null)
            return;

        speakerCameraState = state;
        speakerCameraMoveStart = speakerCameraTarget.transform.position;
        speakerCameraMoveElapsed = 0f;
        speakerCameraMoveStartZoom = GetCurrentSpeakerCameraZoom();

        if (duration > 0f)
            return;

        if (state == SpeakerCameraState.ReturningToPlayer)
        {
            RestoreSpeakerCameraImmediately();
            return;
        }

        if (activeSpeakerTransform != null)
            speakerCameraTarget.transform.position = activeSpeakerTransform.position;

        SetSpeakerCameraZoom(GetFocusedSpeakerCameraZoom());
        speakerCameraState = SpeakerCameraState.TrackingSpeaker;
    }

    private void UpdateSpeakerCameraFocus()
    {
        if (speakerCameraTarget == null)
            return;

        switch (speakerCameraState)
        {
            case SpeakerCameraState.FocusingSpeaker:
                if (activeSpeakerTransform == null)
                {
                    ReturnSpeakerCameraToPlayer();
                    return;
                }

                speakerCameraMoveElapsed += Time.unscaledDeltaTime;
                float focusProgress = Mathf.Clamp01(
                    speakerCameraMoveElapsed / Mathf.Max(speakerPanDuration, Mathf.Epsilon));
                float easedFocusProgress = Mathf.SmoothStep(0f, 1f, focusProgress);
                speakerCameraTarget.transform.position = Vector3.LerpUnclamped(
                    speakerCameraMoveStart,
                    activeSpeakerTransform.position,
                    easedFocusProgress);
                SetSpeakerCameraZoom(Mathf.LerpUnclamped(
                    speakerCameraMoveStartZoom,
                    GetFocusedSpeakerCameraZoom(),
                    easedFocusProgress));

                if (focusProgress >= 1f)
                    speakerCameraState = SpeakerCameraState.TrackingSpeaker;
                break;

            case SpeakerCameraState.TrackingSpeaker:
                if (activeSpeakerTransform != null)
                {
                    speakerCameraTarget.transform.position = activeSpeakerTransform.position;
                    SetSpeakerCameraZoom(GetFocusedSpeakerCameraZoom());
                }
                else
                    ReturnSpeakerCameraToPlayer();
                break;

            case SpeakerCameraState.ReturningToPlayer:
                if (originalCameraFollow == null)
                {
                    RestoreSpeakerCameraImmediately();
                    return;
                }

                speakerCameraMoveElapsed += Time.unscaledDeltaTime;
                float returnProgress = Mathf.Clamp01(
                    speakerCameraMoveElapsed / Mathf.Max(speakerReturnDuration, Mathf.Epsilon));
                float easedReturnProgress = Mathf.SmoothStep(0f, 1f, returnProgress);
                speakerCameraTarget.transform.position = Vector3.LerpUnclamped(
                    speakerCameraMoveStart,
                    originalCameraFollow.position,
                    easedReturnProgress);
                SetSpeakerCameraZoom(Mathf.LerpUnclamped(
                    speakerCameraMoveStartZoom,
                    originalSpeakerCameraZoom,
                    easedReturnProgress));

                if (returnProgress >= 1f)
                    RestoreSpeakerCameraImmediately();
                break;
        }
    }

    private void ReturnSpeakerCameraToPlayer()
    {
        if (speakerCameraTarget == null)
            return;

        activeSpeakerTransform = null;
        BeginSpeakerCameraMove(
            SpeakerCameraState.ReturningToPlayer,
            speakerReturnDuration);
    }

    public void ReleaseSpeakerCameraFocus()
    {
        RestoreSpeakerCameraImmediately();
    }

    private float GetCurrentSpeakerCameraZoom()
    {
        if (speakerCamera == null)
            return 0f;

        return speakerCameraUsesOrthographicLens
            ? speakerCamera.Lens.OrthographicSize
            : speakerCamera.Lens.FieldOfView;
    }

    private float GetFocusedSpeakerCameraZoom()
    {
        return zoomCameraOnSpeakers
            ? originalSpeakerCameraZoom * speakerZoomScale
            : originalSpeakerCameraZoom;
    }

    private void SetSpeakerCameraZoom(float zoom)
    {
        if (speakerCamera == null || zoom <= 0f)
            return;

        LensSettings lens = speakerCamera.Lens;
        if (speakerCameraUsesOrthographicLens)
            lens.OrthographicSize = zoom;
        else
            lens.FieldOfView = zoom;

        speakerCamera.Lens = lens;
    }

    private static CinemachineCamera GetActiveCinemachineCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null ||
            !mainCamera.TryGetComponent(out CinemachineBrain brain))
        {
            return null;
        }

        if (brain.ActiveVirtualCamera is CinemachineCameraManagerBase managerCamera)
            return managerCamera.LiveChild as CinemachineCamera;

        return brain.ActiveVirtualCamera as CinemachineCamera;
    }

    private void RestoreSpeakerCameraImmediately()
    {
        if (speakerCamera != null && originalSpeakerCameraZoom > 0f)
            SetSpeakerCameraZoom(originalSpeakerCameraZoom);

        if (speakerCamera != null &&
            originalCameraFollow != null &&
            speakerCameraTarget != null &&
            speakerCamera.Follow == speakerCameraTarget.transform)
        {
            speakerCamera.Follow = originalCameraFollow;
        }

        if (speakerCameraTarget != null)
            Destroy(speakerCameraTarget);

        speakerCamera = null;
        originalCameraFollow = null;
        speakerCameraTarget = null;
        activeSpeakerTransform = null;
        speakerCameraState = SpeakerCameraState.None;
        speakerCameraMoveStart = Vector3.zero;
        speakerCameraMoveElapsed = 0f;
        speakerCameraUsesOrthographicLens = false;
        originalSpeakerCameraZoom = 0f;
        speakerCameraMoveStartZoom = 0f;
    }
}
