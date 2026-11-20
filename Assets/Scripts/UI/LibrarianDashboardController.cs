using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
    [SerializeField] private Button[] chapterTabButtons;
    [SerializeField] private Image[] chapterTabBackgrounds;
    [SerializeField] private TMP_Text[] chapterTabTexts;
    [SerializeField] private TMP_Text globalParticipantsText;
    [SerializeField] private TMP_Text globalAveragePlaytimeText;
    [SerializeField] private TMP_Text globalResultsHeaderText;
    [SerializeField] private TMP_Text globalAnalyticsText;
    [SerializeField] private TMP_Text engagementCardText;
    [SerializeField] private TMP_Text quizScoreCardText;
    [SerializeField] private TMP_Text dialogueSkipCardText;
    [SerializeField] private TMP_Text artifactDiscoveryAverageText;
    [SerializeField] private GameObject artifactDiscoveryTable;
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
    [SerializeField] private TMP_Text pendingRequestCountText;
    [SerializeField] private TMP_Text approvedAccountCountText;
    [SerializeField] private TMP_Text declinedRequestCountText;
    [SerializeField] private Button pendingRequestsTabButton;
    [SerializeField] private Button allRequestsTabButton;
    [SerializeField] private Image pendingRequestsTabBackground;
    [SerializeField] private Image allRequestsTabBackground;
    [SerializeField] private TMP_Text pendingRequestsTabText;
    [SerializeField] private TMP_Text allRequestsTabText;
    [SerializeField] private Transform teacherTableContent;
    [SerializeField] private TeacherRequestRowView teacherRequestRowTemplate;
    [SerializeField] private TMP_Text teacherTableEmptyText;

    private readonly List<TeacherReviewRequest> teacherRequests = new();
    private readonly List<GlobalChapterAnalyticsAggregate> analyticsChapters = new();
    private int selectedTeacherIndex;
    private int pendingDecision;
    private bool isBusy;
    private UnityAction[] chapterTabActions;
    private bool showAllTeacherRequests;
    private string pendingReviewAccountId = string.Empty;
    private int pendingReviewDecision;
    private string dashboardStatusMessage = string.Empty;
    private readonly List<TeacherRequestRowView> activeTeacherRows = new();

    private static readonly Color SelectedChapterColor =
        new(0.525f, 0.357f, 0.196f, 1f);
    private static readonly Color UnselectedChapterColor =
        new(0.953f, 0.898f, 0.784f, 1f);
    private static readonly Color SelectedChapterTextColor = Color.white;
    private static readonly Color UnselectedChapterTextColor =
        new(0.235f, 0.161f, 0.118f, 1f);

    private void Awake()
    {
        backButton.onClick.AddListener(ReturnToMainMenu);
        teacherVerificationTabButton.onClick.AddListener(ShowTeacherVerification);
        globalAnalyticsTabButton.onClick.AddListener(ShowGlobalAnalytics);
        previousTeacherButton?.onClick.AddListener(ShowPreviousTeacher);
        nextTeacherButton?.onClick.AddListener(ShowNextTeacher);
        refreshTeachersButton?.onClick.AddListener(RefreshPendingTeachers);
        approveTeacherButton?.onClick.AddListener(ApproveSelectedTeacher);
        rejectTeacherButton?.onClick.AddListener(RejectSelectedTeacher);
        pendingRequestsTabButton?.onClick.AddListener(ShowPendingTeacherRequests);
        allRequestsTabButton?.onClick.AddListener(ShowAllTeacherRequests);
        if (chapterDropdown != null)
            chapterDropdown.onValueChanged.AddListener(ShowSelectedAnalyticsChapter);
        BindChapterTabs();

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
        previousTeacherButton?.onClick.RemoveListener(ShowPreviousTeacher);
        nextTeacherButton?.onClick.RemoveListener(ShowNextTeacher);
        refreshTeachersButton?.onClick.RemoveListener(RefreshPendingTeachers);
        approveTeacherButton?.onClick.RemoveListener(ApproveSelectedTeacher);
        rejectTeacherButton?.onClick.RemoveListener(RejectSelectedTeacher);
        pendingRequestsTabButton?.onClick.RemoveListener(ShowPendingTeacherRequests);
        allRequestsTabButton?.onClick.RemoveListener(ShowAllTeacherRequests);
        if (chapterDropdown != null)
            chapterDropdown.onValueChanged.RemoveListener(ShowSelectedAnalyticsChapter);
        UnbindChapterTabs();
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
        SetStatusText("Loading Global Analytics...");
        globalAnalyticsText.SetText("Loading aggregated results...");
        if (chapterDropdown != null)
            chapterDropdown.gameObject.SetActive(false);

        GlobalAnalyticsDashboardResult result =
            await GlobalAnalyticsDashboardService.LoadAsync();

        SetBusy(false);

        if (!result.Success)
        {
            globalAnalyticsText.SetText("Global Analytics could not be loaded.");
            SetStatusText(result.Error);
            return;
        }

        SetStatusText(string.Empty);
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

        PopulateChapterDropdown();
        ShowSelectedAnalyticsChapter(0);
    }

    private void PopulateChapterDropdown()
    {
        bool hasChapterTabs = chapterTabButtons != null &&
                              chapterTabButtons.Length >= DashboardChapterIds.Length;

        if (hasChapterTabs)
        {
            if (chapterDropdown != null)
                chapterDropdown.gameObject.SetActive(false);

            UpdateChapterTabState(0);
            return;
        }

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
        UpdateChapterTabState(safeIndex);
        string selectedChapterId = DashboardChapterIds[safeIndex];
        GlobalChapterAnalyticsAggregate chapter = analyticsChapters.Find(candidate =>
            string.Equals(candidate.chapterId, selectedChapterId,
                System.StringComparison.OrdinalIgnoreCase));

        if (chapter == null)
        {
            SetText(globalParticipantsText,
                "<size=34><b>0</b></size>\n" +
                "<size=15>UNIQUE PARTICIPANTS</size>");
            SetText(globalAveragePlaytimeText,
                "<size=34><b>—</b></size>\n<size=15>AVERAGE PLAYTIME</size>");
            SetText(globalResultsHeaderText,
                $"<b>{FormatChapterLabel(selectedChapterId)} AVERAGE RESULTS</b>");
            globalAnalyticsText.SetText(
                "No official submissions have been received for this chapter yet.");
            SetMetricsVisible(false);
            return;
        }

        SetText(globalParticipantsText,
            $"<size=34><b>{chapter.participantCount}</b></size>\n" +
            "<size=15>UNIQUE PARTICIPANTS</size>");
        SetText(globalAveragePlaytimeText,
            $"<size=34><b>{FormatPlaytime(chapter.averagePlayTimeSeconds)}</b></size>\n" +
            "<size=15>AVERAGE PLAYTIME</size>");
        SetText(globalResultsHeaderText,
            $"<b>{FormatChapterLabel(chapter.chapterId)} AVERAGE RESULTS</b>");

        globalAnalyticsText.SetText(string.Empty);
        SetMetricsVisible(true);
        SetText(engagementCardText,
            $"<size=34><b>{chapter.averageEngagementRatePercent:0.#}%</b></size>\n" +
            "<size=14>PLAYER ENGAGEMENT</size>");
        SetText(quizScoreCardText,
            $"<size=34><b>{chapter.averageQuizScoreRatePercent:0.#}</b></size>\n" +
            "<size=14>AVERAGE QUIZ SCORE</size>");
        SetText(dialogueSkipCardText,
            $"<size=34><b>{chapter.averageDialogueSkipRatePercent:0.#}%</b></size>\n" +
            "<size=14>DIALOGUE SKIP RATE</size>");
        SetText(artifactDiscoveryAverageText,
            $"CHAPTER AVERAGE  {chapter.averageArtifactDiscoveryRatePercent:0.#}%");

        if (engagementCardText == null)
            engagementMetric?.SetValue("Engagement", chapter.averageEngagementRatePercent);
        if (quizScoreCardText == null)
            quizScoreMetric?.SetValue("Quiz Score", chapter.averageQuizScoreRatePercent);
        if (dialogueSkipCardText == null)
        {
            dialogueAttentionMetric?.SetValue(
                "Dialogue Attention",
                100d - chapter.averageDialogueSkipRatePercent,
                $"Skip rate: {chapter.averageDialogueSkipRatePercent:0.0}%");
        }
        if (artifactDiscoveryTable == null)
        {
            artifactDiscoveryMetric?.SetValue(
                "Artifact Discovery",
                chapter.averageArtifactDiscoveryRatePercent);
        }
    }

    private void SetMetricsVisible(bool visible)
    {
        if (engagementCardText != null)
            engagementCardText.gameObject.SetActive(visible);
        if (quizScoreCardText != null)
            quizScoreCardText.gameObject.SetActive(visible);
        if (dialogueSkipCardText != null)
            dialogueSkipCardText.gameObject.SetActive(visible);
        if (artifactDiscoveryTable != null)
            artifactDiscoveryTable.SetActive(visible);

        // Retain compatibility with the previous bar-based scene layout.
        if (engagementCardText == null)
            engagementMetric?.SetVisible(visible);
        if (quizScoreCardText == null)
            quizScoreMetric?.SetVisible(visible);
        if (dialogueSkipCardText == null)
            dialogueAttentionMetric?.SetVisible(visible);
        if (artifactDiscoveryTable == null)
            artifactDiscoveryMetric?.SetVisible(visible);
    }

    private void BindChapterTabs()
    {
        if (chapterTabButtons == null)
            return;

        int count = Mathf.Min(chapterTabButtons.Length, DashboardChapterIds.Length);
        chapterTabActions = new UnityAction[count];
        for (int index = 0; index < count; index++)
        {
            int tabIndex = index;
            chapterTabActions[index] = () => ShowSelectedAnalyticsChapter(tabIndex);
            chapterTabButtons[index]?.onClick.AddListener(chapterTabActions[index]);
        }
    }

    private void UnbindChapterTabs()
    {
        if (chapterTabButtons == null || chapterTabActions == null)
            return;

        int count = Mathf.Min(chapterTabButtons.Length, chapterTabActions.Length);
        for (int index = 0; index < count; index++)
            chapterTabButtons[index]?.onClick.RemoveListener(chapterTabActions[index]);
    }

    private void UpdateChapterTabState(int selectedIndex)
    {
        if (chapterTabButtons == null)
            return;

        for (int index = 0; index < chapterTabButtons.Length; index++)
        {
            bool selected = index == selectedIndex;
            if (chapterTabBackgrounds != null && index < chapterTabBackgrounds.Length &&
                chapterTabBackgrounds[index] != null)
            {
                chapterTabBackgrounds[index].color = selected
                    ? SelectedChapterColor
                    : UnselectedChapterColor;
            }

            if (chapterTabTexts != null && index < chapterTabTexts.Length &&
                chapterTabTexts[index] != null)
            {
                chapterTabTexts[index].color = selected
                    ? SelectedChapterTextColor
                    : UnselectedChapterTextColor;
            }
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.SetText(value);
    }

    private void SetStatusText(string value)
    {
        dashboardStatusMessage = value ?? string.Empty;
        SetText(statusText, dashboardStatusMessage);

        if (teacherVerificationRoot == null || !teacherVerificationRoot.activeSelf ||
            teacherTableEmptyText == null)
            return;

        if (string.IsNullOrWhiteSpace(dashboardStatusMessage))
        {
            teacherTableEmptyText.gameObject.SetActive(false);
            return;
        }

        teacherTableEmptyText.SetText(dashboardStatusMessage);
        teacherTableEmptyText.gameObject.SetActive(true);
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

    private void ShowPendingTeacherRequests()
    {
        showAllTeacherRequests = false;
        ResetDecision();
        RenderTeacherRequest();
    }

    private void ShowAllTeacherRequests()
    {
        showAllTeacherRequests = true;
        ResetDecision();
        RenderTeacherRequest();
    }

    private async Task LoadPendingTeachers()
    {
        SetBusy(true);
        SetStatusText("Loading Teacher requests...");
        TeacherRequestListResult result =
            await LibrarianVerificationService.GetPendingTeachersAsync();

        teacherRequests.Clear();
        selectedTeacherIndex = 0;
        pendingDecision = 0;
        pendingReviewAccountId = string.Empty;
        pendingReviewDecision = 0;

        if (!result.Success)
        {
            SetBusy(false);
            SetText(teacherRequestText, "Teacher requests could not be loaded.");
            SetText(teacherTableEmptyText, "Teacher requests could not be loaded.");
            SetStatusText(result.Error);
            RenderTeacherRequest();
            return;
        }

        teacherRequests.AddRange(result.Requests);
        SetBusy(false);
        SetStatusText(string.Empty);
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
            SetStatusText(approve
                ? "Press Approve again to confirm. Teacher access remains locked until email confirmation."
                : "Press Reject again to confirm this Teacher request rejection.");
            return;
        }

        TeacherReviewRequest request = teacherRequests[selectedTeacherIndex];
        SetBusy(true);
        SetStatusText("Saving review...");
        AccountOperationResult result = await LibrarianVerificationService.ReviewAsync(
            request.accountId,
            approve);

        if (!result.Success)
        {
            SetBusy(false);
            ResetDecision();
            SetStatusText(result.Error);
            return;
        }

        await LoadPendingTeachers();
        SetStatusText(approve
            ? "Teacher approved and awaiting email confirmation."
            : "Teacher request rejected.");
    }

    private async void ReviewTeacherRequest(TeacherReviewRequest request, bool approve)
    {
        if (request == null || isBusy)
            return;

        int decision = approve ? 1 : -1;
        if (!string.Equals(pendingReviewAccountId, request.accountId,
                StringComparison.Ordinal) || pendingReviewDecision != decision)
        {
            pendingReviewAccountId = request.accountId;
            pendingReviewDecision = decision;
            SetStatusText(approve
                ? $"Press Approve again to confirm {request.fullName}. Teacher access remains locked until email confirmation."
                : $"Press Decline again to confirm the rejection of {request.fullName}.");
            return;
        }

        SetBusy(true);
        SetStatusText("Saving review...");
        AccountOperationResult result = await LibrarianVerificationService.ReviewAsync(
            request.accountId,
            approve);

        if (!result.Success)
        {
            SetBusy(false);
            ResetDecision();
            SetStatusText(result.Error);
            return;
        }

        await LoadPendingTeachers();
        SetStatusText(approve
            ? "Teacher approved and awaiting email confirmation."
            : "Teacher request declined.");
    }

    private void ResetDecision()
    {
        pendingDecision = 0;
        pendingReviewAccountId = string.Empty;
        pendingReviewDecision = 0;
        SetStatusText(string.Empty);
    }

    private void RenderTeacherRequest()
    {
        if (teacherTableContent != null && teacherRequestRowTemplate != null)
        {
            RenderTeacherRequestTable();
            return;
        }

        bool hasRequests = teacherRequests.Count > 0;
        if (previousTeacherButton != null)
            previousTeacherButton.interactable = !isBusy && hasRequests && selectedTeacherIndex > 0;
        if (nextTeacherButton != null)
        {
            nextTeacherButton.interactable = !isBusy && hasRequests &&
                                             selectedTeacherIndex + 1 < teacherRequests.Count;
        }
        if (approveTeacherButton != null)
            approveTeacherButton.interactable = !isBusy && hasRequests;
        if (rejectTeacherButton != null)
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

    private void RenderTeacherRequestTable()
    {
        int pendingCount = 0;
        int approvedCount = 0;
        int declinedCount = 0;
        foreach (TeacherReviewRequest request in teacherRequests)
        {
            if (IsTeacherStatus(request, "Pending"))
                pendingCount++;
            else if (IsTeacherStatus(request, "Rejected"))
                declinedCount++;
            else
                approvedCount++;
        }

        SetText(pendingRequestCountText,
            $"<size=34><b>{pendingCount}</b></size>\n<size=14>PENDING REQUESTS</size>");
        SetText(approvedAccountCountText,
            $"<size=34><b>{approvedCount}</b></size>\n<size=14>APPROVED ACCOUNTS</size>");
        SetText(declinedRequestCountText,
            $"<size=34><b>{declinedCount}</b></size>\n<size=14>DECLINED REQUESTS</size>");

        Color selectedBackground = new(0.525f, 0.357f, 0.196f, 1f);
        Color unselectedBackground = new(0.953f, 0.898f, 0.784f, 1f);
        Color ink = new(0.235f, 0.161f, 0.118f, 1f);
        if (pendingRequestsTabBackground != null)
            pendingRequestsTabBackground.color = showAllTeacherRequests
                ? unselectedBackground
                : selectedBackground;
        if (allRequestsTabBackground != null)
            allRequestsTabBackground.color = showAllTeacherRequests
                ? selectedBackground
                : unselectedBackground;
        if (pendingRequestsTabText != null)
            pendingRequestsTabText.color = showAllTeacherRequests ? ink : Color.white;
        if (allRequestsTabText != null)
            allRequestsTabText.color = showAllTeacherRequests ? Color.white : ink;

        foreach (TeacherRequestRowView row in activeTeacherRows)
        {
            if (row != null)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
        }
        activeTeacherRows.Clear();

        int visibleCount = 0;
        foreach (TeacherReviewRequest request in teacherRequests)
        {
            if (!showAllTeacherRequests && !IsTeacherStatus(request, "Pending"))
                continue;

            TeacherRequestRowView row = Instantiate(teacherRequestRowTemplate, teacherTableContent);
            row.name = $"Teacher Request - {request.fullName}";
            row.gameObject.SetActive(true);
            row.Bind(request, ReviewTeacherRequest);
            row.SetInteractable(!isBusy);
            activeTeacherRows.Add(row);
            visibleCount++;
        }

        if (teacherTableEmptyText != null)
        {
            bool hasStatusMessage = !string.IsNullOrWhiteSpace(dashboardStatusMessage);
            teacherTableEmptyText.gameObject.SetActive(hasStatusMessage || visibleCount == 0);
            teacherTableEmptyText.SetText(hasStatusMessage
                ? dashboardStatusMessage
                : showAllTeacherRequests
                    ? "No Teacher account requests have been received."
                    : "No pending Teacher account requests.");
        }

        ResizeTeacherApplicationsPanel(visibleCount);
    }

    private void ResizeTeacherApplicationsPanel(int visibleRequestCount)
    {
        if (teacherTableContent?.parent?.parent is not RectTransform applicationsPanel)
            return;

        Vector2 anchorMin = applicationsPanel.anchorMin;
        anchorMin.y = showAllTeacherRequests
            ? 0.045f
            : Mathf.Clamp(0.40f - visibleRequestCount * 0.09f, 0.045f, 0.22f);
        applicationsPanel.anchorMin = anchorMin;
    }

    private static bool IsTeacherStatus(TeacherReviewRequest request, string status)
    {
        return request != null && string.Equals(
            request.teacherVerificationStatus,
            status,
            StringComparison.OrdinalIgnoreCase);
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        backButton.interactable = !busy;
        teacherVerificationTabButton.interactable = !busy;
        globalAnalyticsTabButton.interactable = !busy;
        if (refreshTeachersButton != null)
            refreshTeachersButton.interactable = !busy;
        if (pendingRequestsTabButton != null)
            pendingRequestsTabButton.interactable = !busy;
        if (allRequestsTabButton != null)
            allRequestsTabButton.interactable = !busy;
        RenderTeacherRequest();
    }
}
