using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class KeepingComposureMinigame : MonoBehaviour
{
    [Header("Composure")]
    [SerializeField, Range(0.05f, 0.95f)] private float startingComposure = 0.42f;
    [SerializeField, Min(0.01f)] private float composureGainPerSecond = 0.16f;
    [SerializeField, Min(0.01f)] private float composureLossPerSecond = 0.12f;

    [Header("Player Movement")]
    [SerializeField, Min(1f)] private float riseAcceleration = 1450f;
    [SerializeField, Min(1f)] private float fallAcceleration = 1200f;
    [SerializeField, Min(1f)] private float maximumSpeed = 620f;
    [SerializeField, Range(0f, 1f)] private float edgeBounce = 0.22f;
    [SerializeField, Min(0f)] private float trackPadding = 12f;

    [Header("Provocation Movement")]
    [SerializeField, Min(1f)] private float minimumProvocationSpeed = 135f;
    [SerializeField, Min(1f)] private float maximumProvocationSpeed = 390f;
    [SerializeField, Min(0.05f)] private float minimumDecisionSeconds = 0.28f;
    [SerializeField, Min(0.05f)] private float maximumDecisionSeconds = 0.85f;

    [Header("Playable Hierarchy UI")]
    [SerializeField] private RectTransform temperTrack;
    [SerializeField] private RectTransform provocationMarker;
    [SerializeField] private RectTransform composureZone;
    [SerializeField] private Image composureZoneImage;
    [SerializeField] private Image provocationMarkerImage;
    [SerializeField] private Slider composureMeter;
    [SerializeField] private TMP_Text composureValueText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text provocationText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultBodyText;

    [Header("Events")]
    [SerializeField] private UnityEvent onGameCompleted;
    [SerializeField] private UnityEvent onGameFailed;

    private static readonly string[] EnglishProvocations =
    {
        "And haven't you observed anything more than that?",
        "It wasn't worthwhile to squander your fortune to learn so trifling a thing.",
        "Any schoolboy knows that."
    };

    private static readonly string[] FilipinoProvocations =
    {
        "At wala ka na bang nakita kundi iyan lamang?",
        "Hindi karapat-dapat na iwaldas mo ang iyong kayamanan para sa napakaliit na bagay.",
        "Sinumang musmos sa paaralan ay nalalaman iyan."
    };

    private const float ProvocationLineSeconds = 4.6f;
    private const float ControlledFeedbackSeconds = 0.8f;

    private System.Random random;
    private float composure;
    private float composureVelocity;
    private float provocationDestination;
    private float provocationSpeed;
    private float decisionRemaining;
    private float lineRemaining;
    private float controlledRemaining;
    private int provocationLineIndex;
    private bool pointerHeld;
    private bool gameEnded;
    private bool isConfigured;
    private bool usesFilipino;

    public float Composure => composure;
    public bool IsComplete { get; private set; }

    private void Awake()
    {
        isConfigured = temperTrack != null && provocationMarker != null && composureZone != null &&
                       composureMeter != null;
        if (!isConfigured)
        {
            Debug.LogError(
                "KeepingComposureMinigame is missing its playable Hierarchy references. Exit Play Mode, " +
                "then run Tools > The Noli > Minigames > Rebuild Keeping Composure Minigame.",
                this);
            enabled = false;
            return;
        }

        random = new System.Random();
    }

    private void Start() => RestartGame();

    private void OnDisable() => pointerHeld = false;

    private void Update()
    {
        if (!isConfigured || gameEnded)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        MoveComposureZone(IsRiseInputHeld(), deltaTime);
        MoveProvocation(deltaTime);
        UpdateComposure(deltaTime);
        UpdateDialogue(deltaTime);
    }

    public void RestartGame()
    {
        if (!isConfigured)
            return;

        usesFilipino = string.Equals(GameLanguage.CurrentCode, "fil", System.StringComparison.OrdinalIgnoreCase);
        composure = startingComposure;
        composureVelocity = 0f;
        pointerHeld = false;
        gameEnded = false;
        IsComplete = false;
        provocationLineIndex = 0;
        lineRemaining = ProvocationLineSeconds;
        controlledRemaining = 0f;

        composureZone.anchoredPosition = Vector2.zero;
        provocationMarker.anchoredPosition = new Vector2(0f, GetVerticalRange(provocationMarker) * 0.45f);
        ChooseProvocationDestination();

        if (resultPanel != null)
            resultPanel.SetActive(false);
        if (instructionText != null)
            instructionText.text = usesFilipino
                ? "PIGILAN para UMAKYAT  •  BITAWAN para BUMABA"
                : "HOLD to RISE  •  RELEASE to FALL";
        if (feedbackText != null)
            feedbackText.text = usesFilipino ? "Panatilihin ang tukso sa loob ng kontrol." : "Keep the bait inside your control.";

        SetProvocationLine();
        RefreshPresentation(false);
    }

    public void SetPointerHeld(bool held)
    {
        if (!gameEnded)
            pointerHeld = held;
    }

    private bool IsRiseInputHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return pointerHeld || (keyboard != null &&
               (keyboard.spaceKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed));
    }

    private void MoveComposureZone(bool rise, float deltaTime)
    {
        float acceleration = rise ? riseAcceleration : -fallAcceleration;
        composureVelocity = Mathf.Clamp(
            composureVelocity + acceleration * deltaTime,
            -maximumSpeed,
            maximumSpeed);

        float range = GetVerticalRange(composureZone);
        float nextY = composureZone.anchoredPosition.y + composureVelocity * deltaTime;
        if (nextY > range)
        {
            nextY = range;
            if (composureVelocity > 0f)
                composureVelocity = -composureVelocity * edgeBounce;
        }
        else if (nextY < -range)
        {
            nextY = -range;
            if (composureVelocity < 0f)
                composureVelocity = -composureVelocity * edgeBounce;
        }

        composureZone.anchoredPosition = new Vector2(0f, nextY);
    }

    private void MoveProvocation(float deltaTime)
    {
        decisionRemaining -= deltaTime;
        float currentY = provocationMarker.anchoredPosition.y;
        float nextY = Mathf.MoveTowards(currentY, provocationDestination, provocationSpeed * deltaTime);
        provocationMarker.anchoredPosition = new Vector2(0f, nextY);

        if (decisionRemaining <= 0f || Mathf.Approximately(nextY, provocationDestination))
            ChooseProvocationDestination();
    }

    private void ChooseProvocationDestination()
    {
        float range = GetVerticalRange(provocationMarker);
        provocationDestination = Mathf.Lerp(-range, range, (float)random.NextDouble());
        float pressure = Mathf.Lerp(0.75f, 1.15f, composure);
        provocationSpeed = Mathf.Lerp(
            minimumProvocationSpeed,
            maximumProvocationSpeed,
            (float)random.NextDouble()) * pressure;
        decisionRemaining = Mathf.Lerp(
            minimumDecisionSeconds,
            maximumDecisionSeconds,
            (float)random.NextDouble());
    }

    private void UpdateComposure(float deltaTime)
    {
        bool contained = IsProvocationContained();
        composure += (contained ? composureGainPerSecond : -composureLossPerSecond) * deltaTime;
        composure = Mathf.Clamp01(composure);

        if (contained)
            controlledRemaining += deltaTime;
        else
            controlledRemaining = 0f;

        RefreshPresentation(contained);

        if (composure >= 1f)
            FinishGame(true);
        else if (composure <= 0f)
            FinishGame(false);
    }

    private bool IsProvocationContained()
    {
        float distance = Mathf.Abs(provocationMarker.anchoredPosition.y - composureZone.anchoredPosition.y);
        float overlapDistance = (provocationMarker.rect.height + composureZone.rect.height) * 0.5f;
        return distance <= overlapDistance;
    }

    private float GetVerticalRange(RectTransform item)
    {
        return Mathf.Max(0f, (temperTrack.rect.height - item.rect.height) * 0.5f - trackPadding);
    }

    private void UpdateDialogue(float deltaTime)
    {
        lineRemaining -= deltaTime;
        if (lineRemaining > 0f)
            return;

        provocationLineIndex++;
        lineRemaining = ProvocationLineSeconds;
        SetProvocationLine();
    }

    private void SetProvocationLine()
    {
        if (provocationText == null)
            return;

        string[] lines = usesFilipino ? FilipinoProvocations : EnglishProvocations;
        provocationText.text = $"“{lines[provocationLineIndex % lines.Length]}”";
    }

    private void RefreshPresentation(bool contained)
    {
        composureMeter.value = composure;
        if (composureValueText != null)
            composureValueText.text = $"{Mathf.RoundToInt(composure * 100f)}%";

        if (composureZoneImage != null)
            composureZoneImage.color = contained
                ? new Color(0.20f, 0.82f, 0.68f, 0.82f)
                : new Color(0.16f, 0.55f, 0.53f, 0.68f);
        if (provocationMarkerImage != null)
            provocationMarkerImage.color = contained
                ? new Color(0.92f, 0.58f, 0.22f)
                : new Color(0.88f, 0.22f, 0.18f);

        if (feedbackText != null && controlledRemaining >= ControlledFeedbackSeconds)
            feedbackText.text = usesFilipino ? "Mahinahon." : "Composed.";
        else if (feedbackText != null && !contained)
            feedbackText.text = usesFilipino ? "Huwag magpadala sa galit." : "Do not take the bait.";
    }

    private void FinishGame(bool succeeded)
    {
        gameEnded = true;
        IsComplete = succeeded;
        pointerHeld = false;

        if (resultPanel != null)
            resultPanel.SetActive(true);
        if (resultTitleText != null)
            resultTitleText.text = succeeded
                ? (usesFilipino ? "NANATILING MAHINAHON" : "COMPOSURE KEPT")
                : (usesFilipino ? "NANAIG ANG GALIT" : "COMPOSURE LOST");
        if (resultBodyText != null)
            resultBodyText.text = succeeded
                ? (usesFilipino ? "Hindi ka napadala sa pang-uudyok ni Padre Damaso." : "You refused to give Fray Damaso the reaction he wanted.")
                : (usesFilipino ? "Nakuha ni Padre Damaso ang reaksiyong nais niya." : "Fray Damaso's provocation broke through your restraint.");

        if (succeeded)
            onGameCompleted?.Invoke();
        else
            onGameFailed?.Invoke();
    }
}
