using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class SocialInterruptionMinigame : MonoBehaviour
{
    [Header("Song Settings")]
    [SerializeField, Min(1)] private int chordsToWin = 8;
    [SerializeField, Min(50f)] private float chordSpeed = 230f;
    [SerializeField, Min(10f)] private float hitWindow = 62f;
    [SerializeField, Min(0.05f)] private float simultaneousGraceSeconds = 0.18f;
    [SerializeField, Min(0f)] private float nextChordDelay = 0.65f;

    [Header("Note Highway")]
    [SerializeField] private RectTransform noteContainer;
    [SerializeField] private RectTransform hitFrame;
    [SerializeField] private RectTransform chordTemplate;

    [Header("Hierarchy UI")]
    [SerializeField] private TMP_Text chordCountText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button playAgainButton;

    [Header("Events")]
    [SerializeField] private UnityEvent onGameCompleted;

    private readonly HashSet<RhythmInput> chordPresses = new HashSet<RhythmInput>();
    private System.Random random;
    private RectTransform activeChord;
    private TMP_Text[] activeLabels;
    private RhythmInput firstInput;
    private RhythmInput secondInput;
    private int completedChords;
    private int receivedPressCount;
    private float delayRemaining;
    private float graceRemaining;
    private bool chordActive;
    private bool gameComplete;
    private bool isConfigured;

    public int CompletedChords => completedChords;

    private void Awake()
    {
        isConfigured = noteContainer != null && hitFrame != null && chordTemplate != null;
        if (!isConfigured)
        {
            Debug.LogError(
                "SocialInterruptionMinigame is using an outdated scene layout. Exit Play Mode, then run " +
                "Tools > The Noli > Minigames > Rebuild Social Interruption Minigame.",
                this);
            enabled = false;
            return;
        }

        random = new System.Random();
        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(StartGame);
    }

    private void Start() => StartGame();

    private void Update()
    {
        if (!isConfigured || gameComplete)
            return;

        if (!chordActive)
        {
            delayRemaining -= Time.unscaledDeltaTime;
            if (delayRemaining <= 0f)
                BeginChord();
            return;
        }

        activeChord.anchoredPosition += Vector2.up * (chordSpeed * Time.unscaledDeltaTime);
        ReadKeyboardPresses();

        if (chordPresses.Count > 0)
        {
            graceRemaining -= Time.unscaledDeltaTime;
            if (graceRemaining <= 0f)
            {
                bool isExactChord = SocialInterruptionPromptSequence.IsExactChord(
                    firstInput,
                    secondInput,
                    chordPresses,
                    receivedPressCount);
                ResolveChord(isExactChord, isExactChord ? "Perfect chord!" : "Press only the two shown keys!");
            }
        }

        if (chordActive && activeChord.anchoredPosition.y > HitFrameY + hitWindow)
            ResolveChord(false, "Miss!");
    }

    public void StartGame()
    {
        if (!isConfigured)
            return;

        ClearActiveChord();
        completedChords = 0;
        delayRemaining = 0f;
        chordActive = false;
        gameComplete = false;
        chordPresses.Clear();
        if (resultPanel != null)
            resultPanel.SetActive(false);
        if (instructionText != null)
            instructionText.text = "Press both keys together — arrows or WASD — inside the frame";
        SetFeedback(string.Empty, Color.white);
        RefreshChordCount();
    }

    public void SubmitInput(RhythmInput input)
    {
        if (!chordActive)
            return;

        float distanceFromFrame = activeChord.anchoredPosition.y - HitFrameY;
        if (distanceFromFrame < -hitWindow)
        {
            ResolveChord(false, "Too early!");
            return;
        }
        if (distanceFromFrame > hitWindow)
        {
            ResolveChord(false, "Miss!");
            return;
        }

        if (input != firstInput && input != secondInput)
        {
            ResolveChord(false, "Wrong chord!");
            return;
        }

        if (chordPresses.Count == 0)
            graceRemaining = simultaneousGraceSeconds;
        receivedPressCount++;
        if (receivedPressCount > 2)
        {
            ResolveChord(false, "Too many keys!");
            return;
        }
        chordPresses.Add(input);
    }

    public void PressUp() => SubmitInput(RhythmInput.Up);
    public void PressDown() => SubmitInput(RhythmInput.Down);
    public void PressLeft() => SubmitInput(RhythmInput.Left);
    public void PressRight() => SubmitInput(RhythmInput.Right);
    public void PressQ() => SubmitInput(RhythmInput.Q);
    public void PressE() => SubmitInput(RhythmInput.E);

    private void BeginChord()
    {
        ClearActiveChord();
        List<RhythmInput> pair = SocialInterruptionPromptSequence.Create(random, 2, 2);
        firstInput = pair[0];
        secondInput = pair[1];
        chordPresses.Clear();
        receivedPressCount = 0;

        activeChord = Instantiate(chordTemplate, noteContainer);
        activeChord.name = $"Moving Chord ({firstInput} + {secondInput})";
        activeChord.gameObject.SetActive(true);
        activeChord.anchoredPosition = new Vector2(0f, -noteContainer.rect.height * 0.5f - 65f);
        activeLabels = activeChord.GetComponentsInChildren<TMP_Text>(true);
        if (activeLabels.Length >= 2)
        {
            activeLabels[0].text = GetLabel(firstInput);
            activeLabels[1].text = GetLabel(secondInput);
        }

        chordActive = true;
        SetFeedback(string.Empty, Color.white);
        RefreshChordCount();
    }

    private void ReadKeyboardPresses()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            SubmitInput(RhythmInput.Up);
        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            SubmitInput(RhythmInput.Down);
        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            SubmitInput(RhythmInput.Left);
        if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            SubmitInput(RhythmInput.Right);
        if (keyboard.qKey.wasPressedThisFrame) SubmitInput(RhythmInput.Q);
        if (keyboard.eKey.wasPressedThisFrame) SubmitInput(RhythmInput.E);
    }

    private void ResolveChord(bool succeeded, string message)
    {
        chordActive = false;
        chordPresses.Clear();
        receivedPressCount = 0;
        Color resultColor = succeeded
            ? new Color(0.12f, 0.48f, 0.25f)
            : new Color(0.78f, 0.08f, 0.10f);
        SetChordColor(resultColor, !succeeded);
        SetFeedback(message, succeeded
            ? new Color(0.42f, 0.86f, 0.55f)
            : new Color(0.95f, 0.38f, 0.36f));

        if (activeChord != null)
            Destroy(activeChord.gameObject, 0.4f);

        if (succeeded)
            completedChords++;

        if (completedChords >= chordsToWin)
        {
            gameComplete = true;
            resultPanel.SetActive(true);
            resultText.text = "Interruption complete!\nYou handled every prompt.";
            onGameCompleted?.Invoke();
        }
        else
        {
            delayRemaining = nextChordDelay;
            RefreshChordCount();
        }
    }

    private bool IsInsideHitWindow()
    {
        return activeChord != null &&
               Mathf.Abs(activeChord.anchoredPosition.y - HitFrameY) <= hitWindow;
    }

    private float HitFrameY => noteContainer.InverseTransformPoint(hitFrame.position).y;

    private void SetChordColor(Color color, bool failed)
    {
        if (activeChord == null)
            return;
        foreach (Image image in activeChord.GetComponentsInChildren<Image>(true))
            image.color = color;
        if (activeLabels == null)
            return;
        foreach (TMP_Text label in activeLabels)
            label.color = failed ? new Color(1f, 0.72f, 0.72f) : Color.white;
    }

    private void ClearActiveChord()
    {
        if (activeChord != null)
            Destroy(activeChord.gameObject);
        activeChord = null;
    }

    private void RefreshChordCount()
    {
        if (chordCountText != null)
            chordCountText.text = $"CHORD {Mathf.Min(completedChords + 1, chordsToWin)} / {chordsToWin}";
    }

    private void SetFeedback(string message, Color color)
    {
        if (feedbackText == null)
            return;
        feedbackText.text = message;
        feedbackText.color = color;
    }

    private static string GetLabel(RhythmInput input)
    {
        switch (input)
        {
            case RhythmInput.Up: return "↑";
            case RhythmInput.Down: return "↓";
            case RhythmInput.Left: return "←";
            case RhythmInput.Right: return "→";
            default: return input.ToString();
        }
    }
}
