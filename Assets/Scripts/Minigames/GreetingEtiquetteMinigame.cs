using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class GreetingEtiquetteMinigame : MonoBehaviour
{
    private enum GamePhase
    {
        PreparingPlayback,
        HighlightingKey,
        BetweenHighlights,
        AwaitingInput,
        ShowingRoundFeedback,
        Complete
    }

    [Header("Challenge")]
    [SerializeField, Min(1)] private int combinationsToComplete = 5;
    [SerializeField, Min(1)] private int minimumSequenceLength = 3;
    [SerializeField, Min(1)] private int maximumSequenceLength = 6;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float preparationSeconds = 0.8f;
    [SerializeField, Min(0.05f)] private float highlightSeconds = 0.85f;
    [SerializeField, Min(0f)] private float highlightGapSeconds = 0.18f;
    [SerializeField, Min(0f)] private float correctInputFlashSeconds = 0.18f;
    [SerializeField, Min(0f)] private float roundFeedbackSeconds = 0.9f;

    [Header("Playable Hierarchy Keys")]
    [SerializeField] private Image upKey;
    [SerializeField] private Image downKey;
    [SerializeField] private Image leftKey;
    [SerializeField] private Image rightKey;
    [SerializeField] private Image qKey;
    [SerializeField] private Image eKey;

    [Header("Hierarchy UI")]
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text sequenceLengthText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultBodyText;

    [Header("Events")]
    [SerializeField] private UnityEvent onGameCompleted;
    [SerializeField, Min(0f)] private float transitionReturnDelaySeconds = 1.25f;

    private readonly List<GreetingEtiquetteInput> currentSequence = new List<GreetingEtiquetteInput>();
    private readonly Color idleColor = new Color(0.20f, 0.16f, 0.27f);
    private readonly Color playbackColor = new Color(0.94f, 0.68f, 0.24f);
    private readonly Color correctColor = new Color(0.22f, 0.72f, 0.48f);
    private readonly Color incorrectColor = new Color(0.82f, 0.18f, 0.18f);

    private System.Random random;
    private GamePhase phase;
    private GreetingEtiquetteInput activeVisualInput;
    private float phaseRemaining;
    private float inputFlashRemaining;
    private int currentRoundIndex;
    private int playbackIndex;
    private int playerInputIndex;
    private bool retryCurrentRound;
    private bool hasActiveVisual;
    private bool isConfigured;
    private Coroutine pendingTransitionReturn;

    public int CompletedCombinations => currentRoundIndex;
    public int CurrentSequenceLength => currentSequence.Count;
    public bool IsComplete => phase == GamePhase.Complete;

    private void Awake()
    {
        isConfigured = upKey != null && downKey != null && leftKey != null && rightKey != null &&
                       qKey != null && eKey != null && roundText != null && instructionText != null;
        if (!isConfigured)
        {
            Debug.LogError(
                "GreetingEtiquetteMinigame is missing its playable Hierarchy references. Exit Play Mode, " +
                "then run Tools > The Noli > Minigames > Rebuild Greeting Etiquette Minigame.",
                this);
            enabled = false;
            return;
        }

        random = new System.Random();
    }

    private void Start() => RestartGame();

    private void OnValidate()
    {
        maximumSequenceLength = Mathf.Max(minimumSequenceLength, maximumSequenceLength);
    }

    private void Update()
    {
        if (!isConfigured || phase == GamePhase.Complete)
            return;

        UpdateInputFlash(Time.unscaledDeltaTime);

        if (phase == GamePhase.AwaitingInput)
        {
            ReadKeyboardPresses();
            return;
        }

        phaseRemaining -= Time.unscaledDeltaTime;
        if (phaseRemaining > 0f)
            return;

        switch (phase)
        {
            case GamePhase.PreparingPlayback:
                BeginNextHighlight();
                break;
            case GamePhase.HighlightingKey:
                SetKeyColor(activeVisualInput, idleColor);
                hasActiveVisual = false;
                phase = GamePhase.BetweenHighlights;
                phaseRemaining = highlightGapSeconds;
                break;
            case GamePhase.BetweenHighlights:
                if (playbackIndex < currentSequence.Count)
                    BeginNextHighlight();
                else
                    BeginPlayerInput();
                break;
            case GamePhase.ShowingRoundFeedback:
                if (retryCurrentRound)
                    SchedulePlayback(true);
                else
                    BeginRound();
                break;
        }
    }

    public void RestartGame()
    {
        if (!isConfigured)
            return;

        if (pendingTransitionReturn != null)
        {
            StopCoroutine(pendingTransitionReturn);
            pendingTransitionReturn = null;
        }

        currentRoundIndex = 0;
        playerInputIndex = 0;
        inputFlashRemaining = 0f;
        hasActiveVisual = false;
        SetAllKeyColors(idleColor);
        if (resultPanel != null)
            resultPanel.SetActive(false);
        BeginRound();
    }

    public void SubmitInput(GreetingEtiquetteInput input)
    {
        if (phase != GamePhase.AwaitingInput || playerInputIndex >= currentSequence.Count)
            return;

        GreetingEtiquetteInput expected = currentSequence[playerInputIndex];
        if (input != expected)
        {
            FlashInput(input, incorrectColor);
            retryCurrentRound = true;
            phase = GamePhase.ShowingRoundFeedback;
            phaseRemaining = roundFeedbackSeconds;
            SetFeedback("Not quite. Watch the same greeting again.", incorrectColor);
            instructionText.text = "Sequence interrupted";
            return;
        }

        FlashInput(input, correctColor);
        playerInputIndex++;
        SetFeedback($"{playerInputIndex} / {currentSequence.Count}", correctColor);

        if (playerInputIndex < currentSequence.Count)
            return;

        currentRoundIndex++;
        if (currentRoundIndex >= combinationsToComplete)
        {
            FinishGame();
            return;
        }

        retryCurrentRound = false;
        phase = GamePhase.ShowingRoundFeedback;
        phaseRemaining = roundFeedbackSeconds;
        instructionText.text = "Greeting returned correctly";
        SetFeedback("Correct! Prepare for the next greeting.", correctColor);
        RefreshProgress();
    }

    public void PressUp() => SubmitInput(GreetingEtiquetteInput.Up);
    public void PressDown() => SubmitInput(GreetingEtiquetteInput.Down);
    public void PressLeft() => SubmitInput(GreetingEtiquetteInput.Left);
    public void PressRight() => SubmitInput(GreetingEtiquetteInput.Right);
    public void PressQ() => SubmitInput(GreetingEtiquetteInput.Q);
    public void PressE() => SubmitInput(GreetingEtiquetteInput.E);

    private void BeginRound()
    {
        int sequenceLength = GreetingEtiquetteSequence.GetLengthForRound(
            currentRoundIndex,
            combinationsToComplete,
            minimumSequenceLength,
            maximumSequenceLength);
        currentSequence.Clear();
        currentSequence.AddRange(GreetingEtiquetteSequence.Create(random, sequenceLength));
        RefreshProgress();
        SchedulePlayback(false);
    }

    private void SchedulePlayback(bool isRetry)
    {
        SetAllKeyColors(idleColor);
        playbackIndex = 0;
        playerInputIndex = 0;
        inputFlashRemaining = 0f;
        hasActiveVisual = false;
        phase = GamePhase.PreparingPlayback;
        phaseRemaining = preparationSeconds;
        instructionText.text = "Watch the greeting carefully";
        SetFeedback(isRetry ? "Same sequence - take your time." : "Memorize each highlighted key.", Color.white);
    }

    private void BeginNextHighlight()
    {
        activeVisualInput = currentSequence[playbackIndex++];
        SetKeyColor(activeVisualInput, playbackColor);
        hasActiveVisual = true;
        phase = GamePhase.HighlightingKey;
        phaseRemaining = highlightSeconds;
    }

    private void BeginPlayerInput()
    {
        phase = GamePhase.AwaitingInput;
        playerInputIndex = 0;
        instructionText.text = "Repeat the greeting in the same order";
        SetFeedback($"0 / {currentSequence.Count}", Color.white);
    }

    private void FinishGame()
    {
        phase = GamePhase.Complete;
        SetAllKeyColors(idleColor);
        instructionText.text = "All greetings completed";
        SetFeedback("Perfect recall.", correctColor);
        RefreshProgress();

        if (resultPanel != null)
            resultPanel.SetActive(true);
        if (resultTitleText != null)
            resultTitleText.text = "ETIQUETTE MASTERED";
        if (resultBodyText != null)
            resultBodyText.text = "You remembered and returned all five greeting patterns.";
        onGameCompleted?.Invoke();
        pendingTransitionReturn = StartCoroutine(ReturnToMissionAfterDelay());
    }

    private System.Collections.IEnumerator ReturnToMissionAfterDelay()
    {
        if (transitionReturnDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(transitionReturnDelaySeconds);

        MinigameTransitionController.TryComplete(gameObject.scene.name);
        pendingTransitionReturn = null;
    }

    private void ReadKeyboardPresses()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.upArrowKey.wasPressedThisFrame) SubmitInput(GreetingEtiquetteInput.Up);
        if (keyboard.downArrowKey.wasPressedThisFrame) SubmitInput(GreetingEtiquetteInput.Down);
        if (keyboard.leftArrowKey.wasPressedThisFrame) SubmitInput(GreetingEtiquetteInput.Left);
        if (keyboard.rightArrowKey.wasPressedThisFrame) SubmitInput(GreetingEtiquetteInput.Right);
        if (keyboard.qKey.wasPressedThisFrame) SubmitInput(GreetingEtiquetteInput.Q);
        if (keyboard.eKey.wasPressedThisFrame) SubmitInput(GreetingEtiquetteInput.E);
    }

    private void FlashInput(GreetingEtiquetteInput input, Color color)
    {
        if (hasActiveVisual)
            SetKeyColor(activeVisualInput, idleColor);
        activeVisualInput = input;
        hasActiveVisual = true;
        inputFlashRemaining = correctInputFlashSeconds;
        SetKeyColor(input, color);
    }

    private void UpdateInputFlash(float deltaTime)
    {
        if (!hasActiveVisual || inputFlashRemaining <= 0f)
            return;

        inputFlashRemaining -= deltaTime;
        if (inputFlashRemaining > 0f)
            return;

        SetKeyColor(activeVisualInput, idleColor);
        hasActiveVisual = false;
    }

    private void RefreshProgress()
    {
        roundText.text = $"GREETING {Mathf.Min(currentRoundIndex + 1, combinationsToComplete)} / {combinationsToComplete}";
        if (sequenceLengthText != null)
        {
            int displayedLength = currentRoundIndex >= combinationsToComplete
                ? maximumSequenceLength
                : GreetingEtiquetteSequence.GetLengthForRound(
                    currentRoundIndex,
                    combinationsToComplete,
                    minimumSequenceLength,
                    maximumSequenceLength);
            sequenceLengthText.text = $"{displayedLength} KEY PATTERN";
        }
    }

    private void SetFeedback(string message, Color color)
    {
        if (feedbackText == null)
            return;
        feedbackText.text = message;
        feedbackText.color = color;
    }

    private void SetAllKeyColors(Color color)
    {
        upKey.color = color;
        downKey.color = color;
        leftKey.color = color;
        rightKey.color = color;
        qKey.color = color;
        eKey.color = color;
    }

    private void SetKeyColor(GreetingEtiquetteInput input, Color color)
    {
        GetKeyImage(input).color = color;
    }

    private Image GetKeyImage(GreetingEtiquetteInput input)
    {
        switch (input)
        {
            case GreetingEtiquetteInput.Up: return upKey;
            case GreetingEtiquetteInput.Down: return downKey;
            case GreetingEtiquetteInput.Left: return leftKey;
            case GreetingEtiquetteInput.Right: return rightKey;
            case GreetingEtiquetteInput.Q: return qKey;
            default: return eKey;
        }
    }
}
