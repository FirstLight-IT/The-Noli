using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TeacherClassroomDashboardController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const int StudentsPerPage = 12;
    private static readonly string[] DashboardChapterIds =
    {
        "chapter_1",
        "chapter_2",
        "chapter_3"
    };

    [Header("Dashboard Hierarchy")]
    [SerializeField] private GameObject dashboardRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Transform studentCardsRoot;
    [SerializeField] private TMP_Text studentPageText;
    [SerializeField] private Button previousStudentsButton;
    [SerializeField] private Button nextStudentsButton;
    [SerializeField] private TMP_Text analyticsOverviewText;
    [SerializeField] private TMP_Dropdown chapterDropdown;
    [SerializeField] private AnalyticsMetricBarView engagementMetric;
    [SerializeField] private AnalyticsMetricBarView quizMetric;
    [SerializeField] private AnalyticsMetricBarView dialogueMetric;
    [SerializeField] private AnalyticsMetricBarView artifactMetric;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button backButton;

    private ClassroomDashboardResponse dashboardData;
    private int studentPage;
    private bool isBusy;

    private void Awake()
    {
        refreshButton?.onClick.AddListener(RefreshDashboard);
        backButton?.onClick.AddListener(ReturnToMainMenu);
        previousStudentsButton?.onClick.AddListener(ShowPreviousStudents);
        nextStudentsButton?.onClick.AddListener(ShowNextStudents);
        chapterDropdown?.onValueChanged.AddListener(RenderChapter);
        dashboardRoot?.SetActive(true);
        SetInteractable(false);
    }

    private async void Start()
    {
        if (!PlayerSession.IsSignedIn)
            await AccountAuthenticationService.RestoreCachedSessionAsync();

        if (PlayerSession.EffectiveRole != AccountRole.Teacher ||
            !TeacherClassroomDashboardLaunchContext.HasRoom)
        {
            TeacherClassroomDashboardLaunchContext.Clear();
            SceneManager.LoadScene(MainMenuSceneName);
            return;
        }

        PopulateChapterDropdown();
        RenderDashboard();
        await LoadDashboardAsync();
    }

    private void OnDestroy()
    {
        refreshButton?.onClick.RemoveListener(RefreshDashboard);
        backButton?.onClick.RemoveListener(ReturnToMainMenu);
        previousStudentsButton?.onClick.RemoveListener(ShowPreviousStudents);
        nextStudentsButton?.onClick.RemoveListener(ShowNextStudents);
        chapterDropdown?.onValueChanged.RemoveListener(RenderChapter);
    }

    private async void RefreshDashboard()
    {
        if (!isBusy)
            await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        isBusy = true;
        SetInteractable(false);
        SetText(statusText, "Loading classroom dashboard...");

        ClassroomDashboardResponse response = await ClassroomService.GetDashboardAsync(
            TeacherClassroomDashboardLaunchContext.RoomId);

        isBusy = false;
        SetInteractable(true);
        if (response == null || !response.success)
        {
            SetText(statusText,
                response?.error ?? "The classroom dashboard could not be loaded.");
            return;
        }

        response.members ??= new List<ClassroomDashboardMember>();
        response.chapters ??= new List<ClassroomChapterAnalyticsAggregate>();
        dashboardData = response;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(
            response.members.Count / (float)StudentsPerPage));
        studentPage = Mathf.Clamp(studentPage, 0, pageCount - 1);
        SetText(statusText, string.Empty);
        RenderDashboard();
    }

    private void ReturnToMainMenu()
    {
        if (isBusy)
            return;

        TeacherClassroomDashboardLaunchContext.Clear();
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void RenderDashboard()
    {
        string roomName = dashboardData?.roomName;
        if (string.IsNullOrWhiteSpace(roomName))
            roomName = TeacherClassroomDashboardLaunchContext.RoomName;
        if (string.IsNullOrWhiteSpace(roomName))
            roomName = "Classroom";
        SetText(titleText, $"{roomName} Dashboard");

        int totalStudents = dashboardData?.members?.Count ?? 0;
        int uploadedStudents = dashboardData?.participantCount ?? 0;
        string joinCode = dashboardData?.joinCode;
        if (string.IsNullOrWhiteSpace(joinCode))
            joinCode = TeacherClassroomDashboardLaunchContext.JoinCode;
        string roomStatus = dashboardData?.status;
        if (string.IsNullOrWhiteSpace(roomStatus))
            roomStatus = TeacherClassroomDashboardLaunchContext.RoomStatus;
        SetText(summaryText,
            $"Join code: {joinCode}     Status: {roomStatus}     " +
            $"Students: {totalStudents}     Analytics uploaded: {uploadedStudents}");

        RenderChapter(chapterDropdown != null ? chapterDropdown.value : 0);
    }

    private void RenderStudentCards()
    {
        if (studentCardsRoot == null)
            return;

        List<ClassroomDashboardMember> members =
            dashboardData?.members ?? new List<ClassroomDashboardMember>();
        int firstMemberIndex = studentPage * StudentsPerPage;
        for (int index = 0; index < studentCardsRoot.childCount; index++)
        {
            GameObject card = studentCardsRoot.GetChild(index).gameObject;
            int memberIndex = firstMemberIndex + index;
            bool visible = index < StudentsPerPage && memberIndex < members.Count;
            card.SetActive(visible);
            if (!visible)
                continue;

            ClassroomDashboardMember member = members[memberIndex];
            bool uploadedSelectedChapter = HasUploadedSelectedChapter(member);
            TMP_Text label = card.GetComponentInChildren<TMP_Text>(true);
            label?.SetText($"{member.inGameName}\n" +
                (uploadedSelectedChapter ? "Analytics uploaded" : "Not uploaded"));

            Image background = card.GetComponent<Image>();
            if (background != null)
            {
                background.color = uploadedSelectedChapter
                    ? new Color(0.20f, 0.42f, 0.32f, 1f)
                    : new Color(0.18f, 0.32f, 0.42f, 1f);
            }

            Button button = card.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.enabled = false;
            }
        }

        int pageCount = Mathf.Max(1, Mathf.CeilToInt(
            members.Count / (float)StudentsPerPage));
        SetText(studentPageText, $"Page {studentPage + 1} of {pageCount}");
        if (previousStudentsButton != null)
            previousStudentsButton.interactable = !isBusy && studentPage > 0;
        if (nextStudentsButton != null)
            nextStudentsButton.interactable = !isBusy && studentPage + 1 < pageCount;
    }

    private void ShowPreviousStudents()
    {
        if (isBusy || studentPage <= 0)
            return;

        studentPage--;
        RenderStudentCards();
    }

    private void ShowNextStudents()
    {
        int memberCount = dashboardData?.members?.Count ?? 0;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(
            memberCount / (float)StudentsPerPage));
        if (isBusy || studentPage + 1 >= pageCount)
            return;

        studentPage++;
        RenderStudentCards();
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
    }

    private void RenderChapter(int chapterIndex)
    {
        RenderStudentCards();
        if (dashboardData == null)
        {
            SetText(analyticsOverviewText,
                "Load the dashboard to view classroom analytics.");
            SetMetricsVisible(false);
            return;
        }

        int safeChapterIndex = Mathf.Clamp(chapterIndex, 0, DashboardChapterIds.Length - 1);
        string chapterId = DashboardChapterIds[safeChapterIndex];
        ClassroomChapterAnalyticsAggregate chapter = dashboardData.chapters.Find(candidate =>
            string.Equals(candidate.chapterId, chapterId,
                System.StringComparison.OrdinalIgnoreCase));
        if (chapter == null)
        {
            SetText(analyticsOverviewText,
                $"UNIQUE UPLOADS\n{dashboardData.participantCount} of " +
                $"{dashboardData.members.Count} students\n\n" +
                $"{FormatChapterLabel(chapterId).ToUpperInvariant()} AVERAGE RESULTS\n" +
                "Chapter participants: 0\nAverage playtime: —\n\n" +
                "No completed chapter analytics have been uploaded yet.");
            SetMetricsVisible(false);
            return;
        }

        SetText(analyticsOverviewText,
            $"UNIQUE UPLOADS\n{dashboardData.participantCount} of " +
            $"{dashboardData.members.Count} students\n\n" +
            $"{FormatChapterLabel(chapter.chapterId).ToUpperInvariant()} AVERAGE RESULTS\n" +
            $"Chapter participants: {chapter.participantCount}\n" +
            $"Average playtime: {FormatPlaytime(chapter.averagePlayTimeSeconds)}");

        SetMetricsVisible(true);
        engagementMetric.SetValue("Engagement", chapter.averageEngagementRatePercent);
        quizMetric.SetValue("Quiz Score", chapter.averageQuizScoreRatePercent);
        dialogueMetric.SetValue(
            "Dialogue Attention",
            100d - chapter.averageDialogueSkipRatePercent,
            $"Skip rate: {chapter.averageDialogueSkipRatePercent:0.0}%");
        artifactMetric.SetValue(
            "Artifact Discovery", chapter.averageArtifactDiscoveryRatePercent);
    }

    private bool HasUploadedSelectedChapter(ClassroomDashboardMember member)
    {
        if (member?.uploadedChapterIds == null || chapterDropdown == null)
            return member?.hasUploadedAnalytics == true;

        int chapterIndex = Mathf.Clamp(
            chapterDropdown.value, 0, DashboardChapterIds.Length - 1);
        string chapterId = DashboardChapterIds[chapterIndex];
        return member.uploadedChapterIds.Exists(uploadedChapterId =>
            string.Equals(uploadedChapterId, chapterId,
                System.StringComparison.OrdinalIgnoreCase));
    }

    private void SetMetricsVisible(bool visible)
    {
        engagementMetric?.SetVisible(visible);
        quizMetric?.SetVisible(visible);
        dialogueMetric?.SetVisible(visible);
        artifactMetric?.SetVisible(visible);
    }

    private void SetInteractable(bool interactable)
    {
        if (refreshButton != null)
            refreshButton.interactable = interactable;
        if (backButton != null)
            backButton.interactable = interactable;
        if (chapterDropdown != null)
            chapterDropdown.interactable = interactable;
        if (previousStudentsButton != null)
            previousStudentsButton.interactable = interactable;
        if (nextStudentsButton != null)
            nextStudentsButton.interactable = interactable;
    }

    private static void SetText(TMP_Text target, string value)
    {
        target?.SetText(value ?? string.Empty);
    }

    private static string FormatChapterLabel(string chapterId)
    {
        return string.IsNullOrWhiteSpace(chapterId)
            ? "Unknown Chapter"
            : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                chapterId.Replace('_', ' '));
    }

    private static string FormatPlaytime(double seconds)
    {
        int totalSeconds = Mathf.Max(0, (int)System.Math.Round(seconds));
        return $"{totalSeconds / 60}m {totalSeconds % 60:00}s";
    }
}
