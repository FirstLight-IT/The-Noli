using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LibrarianDashboardController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private static readonly string[] DashboardChapterIds =
    {
        "chapter_1",
        "chapter_2",
        "chapter_3"
    };

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button teacherVerificationTabButton;
    [SerializeField] private Button globalAnalyticsTabButton;
    [SerializeField] private GameObject teacherVerificationRoot;
    [SerializeField] private GameObject globalAnalyticsRoot;
    [SerializeField] private TMP_Text statusText;

    [Header("Global Analytics")]
    [SerializeField] private TMP_Dropdown chapterDropdown;
    [SerializeField] private TMP_Text globalParticipantsText;
    [SerializeField] private TMP_Text globalAveragePlaytimeText;
    [SerializeField] private TMP_Text globalResultsHeaderText;
    [SerializeField] private TMP_Text globalAnalyticsText;
    [SerializeField] private AnalyticsMetricBarView engagementMetric;
    [SerializeField] private AnalyticsMetricBarView quizScoreMetric;
    [SerializeField] private AnalyticsMetricBarView dialogueAttentionMetric;
    [SerializeField] private AnalyticsMetricBarView artifactDiscoveryMetric;

    [Header("Teacher Verification")]
    [SerializeField] private TMP_Text teacherRequestText;
    [SerializeField] private Button previousTeacherButton;
    [SerializeField] private Button nextTeacherButton;
    [SerializeField] private Button refreshTeachersButton;
    [SerializeField] private Button approveTeacherButton;
    [SerializeField] private Button rejectTeacherButton;

    private readonly List<TeacherReviewRequest> teacherRequests = new();
    private readonly List<GlobalChapterAnalyticsAggregate> analyticsChapters = new();
    private int analyticsParticipantCount;
    private int selectedTeacherIndex;
    private int pendingDecision;
    private bool isBusy;

    private void Awake()
    {
        backButton.onClick.AddListener(ReturnToMainMenu);
        teacherVerificationTabButton.onClick.AddListener(ShowTeacherVerification);
        globalAnalyticsTabButton.onClick.AddListener(ShowGlobalAnalytics);
        previousTeacherButton.onClick.AddListener(ShowPreviousTeacher);
        nextTeacherButton.onClick.AddListener(ShowNextTeacher);
        refreshTeachersButton.onClick.AddListener(RefreshPendingTeachers);
        approveTeacherButton.onClick.AddListener(ApproveSelectedTeacher);
        rejectTeacherButton.onClick.AddListener(RejectSelectedTeacher);
        if (chapterDropdown != null)
            chapterDropdown.onValueChanged.AddListener(ShowSelectedAnalyticsChapter);

        teacherVerificationRoot.SetActive(false);
        globalAnalyticsRoot.SetActive(false);
        SetBusy(true);
    }

    private async void Start()
    {
        if (!PlayerSession.IsSignedIn)
            await AccountAuthenticationService.RestoreCachedSessionAsync();

        if (PlayerSession.EffectiveRole != AccountRole.Librarian)
        {
            SceneManager.LoadScene(MainMenuSceneName);
            return;
        }

        SetBusy(false);
        await ShowTeacherVerificationAsync();
    }

    private void OnDestroy()
    {
        backButton.onClick.RemoveListener(ReturnToMainMenu);
        teacherVerificationTabButton.onClick.RemoveListener(ShowTeacherVerification);
        globalAnalyticsTabButton.onClick.RemoveListener(ShowGlobalAnalytics);
        previousTeacherButton.onClick.RemoveListener(ShowPreviousTeacher);
        nextTeacherButton.onClick.RemoveListener(ShowNextTeacher);
        refreshTeachersButton.onClick.RemoveListener(RefreshPendingTeachers);
        approveTeacherButton.onClick.RemoveListener(ApproveSelectedTeacher);
        rejectTeacherButton.onClick.RemoveListener(RejectSelectedTeacher);
        if (chapterDropdown != null)
            chapterDropdown.onValueChanged.RemoveListener(ShowSelectedAnalyticsChapter);
    }

    private void ReturnToMainMenu()
    {
        if (!isBusy)
            SceneManager.LoadScene(MainMenuSceneName);
    }

    private void ShowTeacherVerification()
    {
        _ = ShowTeacherVerificationAsync();
    }

    private async Task ShowTeacherVerificationAsync()
    {
        teacherVerificationRoot.SetActive(true);
        globalAnalyticsRoot.SetActive(false);
        await LoadPendingTeachers();
    }

    private void ShowGlobalAnalytics()
    {
        _ = ShowGlobalAnalyticsAsync();
    }

    private async Task ShowGlobalAnalyticsAsync()
    {
        teacherVerificationRoot.SetActive(false);
        globalAnalyticsRoot.SetActive(true);
        SetBusy(true);
        statusText.SetText("Loading Global Analytics...");
        globalAnalyticsText.SetText("Loading aggregated results...");
        if (chapterDropdown != null)
            chapterDropdown.gameObject.SetActive(false);

        GlobalAnalyticsDashboardResult result =
            await GlobalAnalyticsDashboardService.LoadAsync();

        SetBusy(false);

        if (!result.Success)
        {
            globalAnalyticsText.SetText("Global Analytics could not be loaded.");
            statusText.SetText(result.Error);
            return;
        }

        statusText.SetText(string.Empty);
        RenderGlobalAnalytics(result.Response);
    }

    private void RenderGlobalAnalytics(GlobalAnalyticsDashboardResponse response)
    {
        analyticsChapters.Clear();
        if (response?.chapters != null)
        {
            foreach (GlobalChapterAnalyticsAggregate chapter in response.chapters)
            {
                if (chapter != null)
                    analyticsChapters.Add(chapter);
            }
        }

        analyticsParticipantCount = response?.participantCount ?? 0;
        PopulateChapterDropdown();
        ShowSelectedAnalyticsChapter(0);
    }

    private void PopulateChapterDropdown()
    {
        if (chapterDropdown == null)
            return;

        List<string> options = new(DashboardChapterIds.Length);
        foreach (string chapterId in DashboardChapterIds)
            options.Add(FormatChapterLabel(chapterId));

        chapterDropdown.ClearOptions();
        chapterDropdown.AddOptions(options);
        chapterDropdown.SetValueWithoutNotify(0);
        chapterDropdown.RefreshShownValue();
        chapterDropdown.gameObject.SetActive(true);
    }

    private void ShowSelectedAnalyticsChapter(int chapterIndex)
    {
        int safeIndex = Mathf.Clamp(chapterIndex, 0, DashboardChapterIds.Length - 1);
        string selectedChapterId = DashboardChapterIds[safeIndex];
        GlobalChapterAnalyticsAggregate chapter = analyticsChapters.Find(candidate =>
            string.Equals(candidate.chapterId, selectedChapterId,
                System.StringComparison.OrdinalIgnoreCase));

        if (chapter == null)
        {
            SetText(globalParticipantsText,
                $"<b>UNIQUE PARTICIPANTS</b>\n{analyticsParticipantCount}");
            SetText(globalAveragePlaytimeText, "<b>AVERAGE PLAYTIME</b>\n—");
            SetText(globalResultsHeaderText,
                $"<b>{FormatChapterLabel(selectedChapterId)} AVERAGE RESULTS</b>\n" +
                "Chapter participants: 0");
            globalAnalyticsText.SetText(
                "No official submissions have been received for this chapter yet.");
            SetMetricsVisible(false);
            return;
        }

        SetText(globalParticipantsText,
            $"<b>UNIQUE PARTICIPANTS</b>\n{analyticsParticipantCount}");
        SetText(globalAveragePlaytimeText,
            $"<b>AVERAGE PLAYTIME</b>\n{FormatPlaytime(chapter.averagePlayTimeSeconds)}");
        SetText(globalResultsHeaderText,
            $"<b>{FormatChapterLabel(chapter.chapterId)} AVERAGE RESULTS</b>\n" +
            $"Chapter participants: {chapter.participantCount}");

        globalAnalyticsText.SetText(string.Empty);
        engagementMetric.SetValue("Engagement", chapter.averageEngagementRatePercent);
        quizScoreMetric.SetValue("Quiz Score", chapter.averageQuizScoreRatePercent);
        dialogueAttentionMetric.SetValue(
            "Dialogue Attention",
            100d - chapter.averageDialogueSkipRatePercent,
            $"Skip rate: {chapter.averageDialogueSkipRatePercent:0.0}%");
        artifactDiscoveryMetric.SetValue(
            "Artifact Discovery",
            chapter.averageArtifactDiscoveryRatePercent);
    }

    private void SetMetricsVisible(bool visible)
    {
        engagementMetric?.SetVisible(visible);
        quizScoreMetric?.SetVisible(visible);
        dialogueAttentionMetric?.SetVisible(visible);
        artifactDiscoveryMetric?.SetVisible(visible);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.SetText(value);
    }

    private static string FormatPlaytime(double seconds)
    {
        int totalSeconds = Mathf.Max(0, (int)System.Math.Round(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes}m {remainingSeconds:00}s";
    }

    private static string FormatMetricBar(string label, double percentage)
    {
        const int SegmentCount = 20;
        double safePercentage = System.Math.Clamp(percentage, 0d, 100d);
        int filledSegments = (int)System.Math.Round(
            safePercentage * SegmentCount / 100d,
            System.MidpointRounding.AwayFromZero);
        string filled = new('█', filledSegments);
        string empty = new('░', SegmentCount - filledSegments);
        return $"{label}  •  {safePercentage:0.0}%\n" +
               $"<color=#D6A84B>{filled}</color><color=#5B554D>{empty}</color>";
    }

    private static string FormatChapterLabel(string chapterId)
    {
        return string.IsNullOrWhiteSpace(chapterId)
            ? "UNKNOWN CHAPTER"
            : chapterId.Replace('_', ' ').ToUpperInvariant();
    }

    private async void RefreshPendingTeachers()
    {
        await LoadPendingTeachers();
    }

    private async Task LoadPendingTeachers()
    {
        SetBusy(true);
        statusText.SetText("Loading Teacher requests...");
        TeacherRequestListResult result =
            await LibrarianVerificationService.GetPendingTeachersAsync();

        teacherRequests.Clear();
        selectedTeacherIndex = 0;
        pendingDecision = 0;

        if (!result.Success)
        {
            SetBusy(false);
            teacherRequestText.SetText("Teacher requests could not be loaded.");
            statusText.SetText(result.Error);
            RenderTeacherRequest();
            return;
        }

        teacherRequests.AddRange(result.Requests);
        SetBusy(false);
        statusText.SetText(string.Empty);
        RenderTeacherRequest();
    }

    private void ShowPreviousTeacher()
    {
        if (selectedTeacherIndex > 0)
            selectedTeacherIndex--;

        ResetDecision();
        RenderTeacherRequest();
    }

    private void ShowNextTeacher()
    {
        if (selectedTeacherIndex + 1 < teacherRequests.Count)
            selectedTeacherIndex++;

        ResetDecision();
        RenderTeacherRequest();
    }

    private void ApproveSelectedTeacher()
    {
        ReviewSelectedTeacher(approve: true);
    }

    private void RejectSelectedTeacher()
    {
        ReviewSelectedTeacher(approve: false);
    }

    private async void ReviewSelectedTeacher(bool approve)
    {
        if (teacherRequests.Count == 0 || isBusy)
            return;

        int decision = approve ? 1 : -1;
        if (pendingDecision != decision)
        {
            pendingDecision = decision;
            statusText.SetText(approve
                ? "Press Approve again to confirm. Teacher access remains locked until email confirmation."
                : "Press Reject again to confirm this Teacher request rejection.");
            return;
        }

        TeacherReviewRequest request = teacherRequests[selectedTeacherIndex];
        SetBusy(true);
        statusText.SetText("Saving review...");
        AccountOperationResult result = await LibrarianVerificationService.ReviewAsync(
            request.accountId,
            approve);

        if (!result.Success)
        {
            SetBusy(false);
            ResetDecision();
            statusText.SetText(result.Error);
            return;
        }

        await LoadPendingTeachers();
        statusText.SetText(approve
            ? "Teacher approved and awaiting email confirmation."
            : "Teacher request rejected.");
    }

    private void ResetDecision()
    {
        pendingDecision = 0;
        statusText.SetText(string.Empty);
    }

    private void RenderTeacherRequest()
    {
        bool hasRequests = teacherRequests.Count > 0;
        previousTeacherButton.interactable = !isBusy && hasRequests && selectedTeacherIndex > 0;
        nextTeacherButton.interactable = !isBusy && hasRequests &&
                                         selectedTeacherIndex + 1 < teacherRequests.Count;
        approveTeacherButton.interactable = !isBusy && hasRequests;
        rejectTeacherButton.interactable = !isBusy && hasRequests;

        if (!hasRequests)
        {
            teacherRequestText.SetText("No pending Teacher requests.");
            return;
        }

        TeacherReviewRequest request = teacherRequests[selectedTeacherIndex];
        teacherRequestText.SetText(
            $"Request {selectedTeacherIndex + 1} of {teacherRequests.Count}\n\n" +
            $"Full name: {request.fullName}\n" +
            $"School email: {request.schoolEmail}\n" +
            $"Username: @{request.username}\n" +
            $"In-game name: {request.inGameName}");
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        backButton.interactable = !busy;
        teacherVerificationTabButton.interactable = !busy;
        globalAnalyticsTabButton.interactable = !busy;
        refreshTeachersButton.interactable = !busy;
        RenderTeacherRequest();
    }
}
