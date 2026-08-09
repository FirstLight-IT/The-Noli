using System.Collections;
using TMPro;
using UnityEngine;

public class NarrationController : MonoBehaviour
{
    public static NarrationController Instance { get; private set; }
    public bool IsNarrationActive { get; private set; }

    [Header("Narration UI")]
    [SerializeField] private GameObject narrationPanel;
    [SerializeField] private TMP_Text narrationText;
    [SerializeField] private GameObject closeButton;

    [Header("Opening Sequence")]
    [SerializeField] private bool playOpeningOnStart = true;
    [SerializeField] private NarrationSequenceSO openingSequence;
    [SerializeField] private string missionToStartAfterOpening;

    [Header("Text")]
    [SerializeField, Min(0f)] private float secondsPerCharacter = 0.025f;

    private NarrationSequenceSO activeSequence;
    private Coroutine typingRoutine;
    private int passageIndex;
    private bool isTyping;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one Narration Controller can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (narrationPanel != null)
            narrationPanel.SetActive(false);

        if (narrationText != null)
            narrationText.SetText(string.Empty);

        if (closeButton != null)
            closeButton.SetActive(false);
    }

    private IEnumerator Start()
    {
        // Let the mission and other scene controllers finish their Start methods first.
        yield return null;

        if (playOpeningOnStart)
            Play(openingSequence);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool Play(NarrationSequenceSO sequence)
    {
        if (IsNarrationActive || !HasPassages(sequence))
            return false;

        if (narrationPanel == null || narrationText == null)
        {
            Debug.LogError("Narration Controller is missing its UI references.", this);
            return false;
        }

        activeSequence = sequence;
        passageIndex = 0;
        IsNarrationActive = true;
        narrationPanel.SetActive(true);
        SetCloseButtonVisible(false);
        ShowCurrentPassage();
        return true;
    }

    public bool AdvanceActiveNarration()
    {
        if (!IsNarrationActive)
            return false;

        if (isTyping)
        {
            FinishTypingImmediately();
            return true;
        }

        passageIndex++;
        if (passageIndex >= activeSequence.Passages.Length)
            EndNarration();
        else
            ShowCurrentPassage();

        return true;
    }

    public void CloseNarration()
    {
        if (IsNarrationActive)
            EndNarration();
    }

    private void ShowCurrentPassage()
    {
        if (typingRoutine != null)
            StopCoroutine(typingRoutine);

        SetCloseButtonVisible(false);
        string passage = activeSequence.Passages[passageIndex];
        narrationText.SetText(passage);
        narrationText.maxVisibleCharacters = 0;
        typingRoutine = StartCoroutine(RevealPassage(passage.Length));
    }

    private IEnumerator RevealPassage(int characterCount)
    {
        isTyping = true;

        if (secondsPerCharacter <= 0f)
        {
            narrationText.maxVisibleCharacters = int.MaxValue;
        }
        else
        {
            for (int visibleCharacters = 1; visibleCharacters <= characterCount; visibleCharacters++)
            {
                narrationText.maxVisibleCharacters = visibleCharacters;
                yield return new WaitForSecondsRealtime(secondsPerCharacter);
            }
        }

        isTyping = false;
        typingRoutine = null;
        ShowCloseButtonIfFinalPassage();
    }

    private void FinishTypingImmediately()
    {
        if (typingRoutine != null)
            StopCoroutine(typingRoutine);

        typingRoutine = null;
        isTyping = false;
        narrationText.maxVisibleCharacters = int.MaxValue;
        ShowCloseButtonIfFinalPassage();
    }

    private void EndNarration()
    {
        FinishTypingImmediately();
        activeSequence = null;
        IsNarrationActive = false;
        narrationText.SetText(string.Empty);
        SetCloseButtonVisible(false);
        narrationPanel.SetActive(false);

        if (!string.IsNullOrWhiteSpace(missionToStartAfterOpening) && MissionController.Instance != null)
        {
            string missionId = missionToStartAfterOpening;
            missionToStartAfterOpening = string.Empty;
            MissionController.Instance.StartMission(missionId);
        }
    }

    private static bool HasPassages(NarrationSequenceSO sequence)
    {
        if (sequence == null || sequence.Passages == null || sequence.Passages.Length == 0)
            return false;

        foreach (string passage in sequence.Passages)
        {
            if (string.IsNullOrWhiteSpace(passage))
                return false;
        }

        return true;
    }

    private void ShowCloseButtonIfFinalPassage()
    {
        bool isFinalPassage = IsNarrationActive &&
                              activeSequence != null &&
                              passageIndex == activeSequence.Passages.Length - 1;
        SetCloseButtonVisible(isFinalPassage);
    }

    private void SetCloseButtonVisible(bool visible)
    {
        if (closeButton != null)
            closeButton.SetActive(visible);
    }
}
