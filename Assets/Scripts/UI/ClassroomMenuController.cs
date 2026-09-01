using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ClassroomMenuController : MonoBehaviour
{
    private const int DashboardStudentsPerPage = 12;
    private static readonly string[] DashboardChapterIds =
    {
        "chapter_1",
        "chapter_2",
        "chapter_3"
    };

    [SerializeField] private MainMenuController mainMenuController;
    [SerializeField] private Button classroomButton;
    [SerializeField] private GameObject classroomPanel;
    [SerializeField] private GameObject playerOptionsRoot;
    [SerializeField] private GameObject teacherOptionsRoot;
    [SerializeField] private Button joinClassroomButton;
    [SerializeField] private Button myClassroomsButton;
    [SerializeField] private GameObject joinClassroomRoot;
    [SerializeField] private TMP_InputField classroomCodeInput;
    [SerializeField] private Button submitJoinClassroomButton;
    [SerializeField] private Button joinClassroomBackButton;
    [SerializeField] private GameObject myClassroomsRoot;
    [SerializeField] private Transform joinedClassroomCardsRoot;
    [SerializeField] private TMP_Text joinedClassroomText;
    [SerializeField] private Button previousJoinedClassroomButton;
    [SerializeField] private Button nextJoinedClassroomButton;
    [SerializeField] private Button refreshJoinedClassroomsButton;
    [SerializeField] private Button playJoinedClassroomButton;
    [SerializeField] private Button leaveJoinedClassroomButton;
    [SerializeField] private Button myClassroomsBackButton;
    [SerializeField] private Button createClassroomButton;
    [SerializeField] private Button manageClassroomsButton;
    [SerializeField] private GameObject createClassroomRoot;
    [SerializeField] private TMP_InputField classroomNameInput;
    [SerializeField] private Button submitCreateClassroomButton;
    [SerializeField] private Button createClassroomBackButton;
    [SerializeField] private GameObject manageClassroomsRoot;
    [SerializeField] private Transform managedClassroomCardsRoot;
    [SerializeField] private TMP_Text managedClassroomText;
    [SerializeField] private Button previousClassroomButton;
    [SerializeField] private Button nextClassroomButton;
    [SerializeField] private Button refreshClassroomsButton;
    [SerializeField] private Button toggleClassroomStatusButton;
    [SerializeField] private Button manageClassroomsBackButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Teacher Classroom Dashboard")]
    [SerializeField] private GameObject classroomDashboardRoot;
    [SerializeField] private TMP_Text dashboardTitleText;
    [SerializeField] private TMP_Text dashboardSummaryText;
    [SerializeField] private TMP_Text dashboardStatusText;
    [SerializeField] private Transform dashboardStudentCardsRoot;
    [SerializeField] private TMP_Text dashboardStudentPageText;
    [SerializeField] private Button dashboardPreviousStudentsButton;
    [SerializeField] private Button dashboardNextStudentsButton;
    [SerializeField] private TMP_Text dashboardAnalyticsOverviewText;
    [SerializeField] private TMP_Dropdown dashboardChapterDropdown;
    [SerializeField] private AnalyticsMetricBarView dashboardEngagementMetric;
    [SerializeField] private AnalyticsMetricBarView dashboardQuizMetric;
    [SerializeField] private AnalyticsMetricBarView dashboardDialogueMetric;
    [SerializeField] private AnalyticsMetricBarView dashboardArtifactMetric;
    [SerializeField] private Button dashboardRefreshButton;
    [SerializeField] private Button dashboardBackButton;

    private bool isBusy;
    private readonly List<ClassroomSummary> teacherClassrooms = new();
    private int selectedClassroomIndex;
    private readonly List<ClassroomMemberSummary> selectedClassroomMembers = new();
    private bool selectedClassroomDetailsLoaded;
    private readonly List<ClassroomMembership> playerClassrooms = new();
    private int selectedPlayerClassroomIndex;
    private string playerClassroomNotice = string.Empty;
    private bool leaveConfirmationPending;
    private string deleteConfirmationRoomId = string.Empty;
    private bool isReconciling;
    private bool wasInternetReachable;
    private float nextConnectivityCheckTime;
    private ClassroomDashboardResponse dashboardData;
    private int dashboardStudentPage;

    private void Awake()
    {
        classroomButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);
        joinClassroomButton.onClick.AddListener(ShowJoinPage);
        myClassroomsButton.onClick.AddListener(ShowMyClassroomsPage);
        submitJoinClassroomButton.onClick.AddListener(JoinClassroom);
        joinClassroomBackButton.onClick.AddListener(ShowPlayerOptions);
        previousJoinedClassroomButton.onClick.AddListener(ShowPreviousJoinedClassroom);
        nextJoinedClassroomButton.onClick.AddListener(ShowNextJoinedClassroom);
        refreshJoinedClassroomsButton.onClick.AddListener(RefreshPlayerClassrooms);
        playJoinedClassroomButton.onClick.AddListener(PlayJoinedClassroom);
        leaveJoinedClassroomButton.onClick.AddListener(LeaveJoinedClassroom);
        myClassroomsBackButton.onClick.AddListener(ShowPlayerOptions);
        createClassroomButton.onClick.AddListener(ShowCreatePage);
        manageClassroomsButton.onClick.AddListener(ShowManagePage);
        submitCreateClassroomButton.onClick.AddListener(CreateClassroom);
        createClassroomBackButton.onClick.AddListener(ShowTeacherOptions);
        previousClassroomButton.onClick.AddListener(ShowPreviousClassroom);
        nextClassroomButton.onClick.AddListener(ShowNextClassroom);
        refreshClassroomsButton.onClick.AddListener(RefreshTeacherClassrooms);
        toggleClassroomStatusButton.onClick.AddListener(ToggleClassroomStatus);
        manageClassroomsBackButton.onClick.AddListener(ShowTeacherOptions);
        if (dashboardRefreshButton != null)
            dashboardRefreshButton.onClick.AddListener(RefreshClassroomDashboard);
        if (dashboardBackButton != null)
            dashboardBackButton.onClick.AddListener(CloseClassroomDashboard);
        if (dashboardChapterDropdown != null)
            dashboardChapterDropdown.onValueChanged.AddListener(RenderDashboardChapter);
        if (dashboardPreviousStudentsButton != null)
            dashboardPreviousStudentsButton.onClick.AddListener(ShowPreviousDashboardStudents);
        if (dashboardNextStudentsButton != null)
            dashboardNextStudentsButton.onClick.AddListener(ShowNextDashboardStudents);
        if (classroomDashboardRoot != null)
            classroomDashboardRoot.SetActive(false);
        classroomPanel.SetActive(false);
    }

    private void OnEnable()
    {
        PlayerSession.Changed += Refresh;
        PlayerSession.ProfileChanged += Refresh;
        Refresh();
        wasInternetReachable = Application.internetReachability !=
                               NetworkReachability.NotReachable;
        if (wasInternetReachable && PlayerSession.EffectiveRole == AccountRole.Player)
            ReconcilePlayerClassrooms();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextConnectivityCheckTime)
            return;

        nextConnectivityCheckTime = Time.unscaledTime + 3f;
        bool isReachable = Application.internetReachability !=
                           NetworkReachability.NotReachable;
        if (isReachable && !wasInternetReachable &&
            PlayerSession.EffectiveRole == AccountRole.Player)
            ReconcilePlayerClassrooms();
        wasInternetReachable = isReachable;
    }

    private void OnDisable()
    {
        PlayerSession.Changed -= Refresh;
        PlayerSession.ProfileChanged -= Refresh;
    }

    private void OnDestroy()
    {
        classroomButton.onClick.RemoveListener(Open);
        closeButton.onClick.RemoveListener(Close);
        joinClassroomButton.onClick.RemoveListener(ShowJoinPage);
        myClassroomsButton.onClick.RemoveListener(ShowMyClassroomsPage);
        submitJoinClassroomButton.onClick.RemoveListener(JoinClassroom);
        joinClassroomBackButton.onClick.RemoveListener(ShowPlayerOptions);
        previousJoinedClassroomButton.onClick.RemoveListener(ShowPreviousJoinedClassroom);
        nextJoinedClassroomButton.onClick.RemoveListener(ShowNextJoinedClassroom);
        refreshJoinedClassroomsButton.onClick.RemoveListener(RefreshPlayerClassrooms);
        playJoinedClassroomButton.onClick.RemoveListener(PlayJoinedClassroom);
        leaveJoinedClassroomButton.onClick.RemoveListener(LeaveJoinedClassroom);
        myClassroomsBackButton.onClick.RemoveListener(ShowPlayerOptions);
        createClassroomButton.onClick.RemoveListener(ShowCreatePage);
        manageClassroomsButton.onClick.RemoveListener(ShowManagePage);
        submitCreateClassroomButton.onClick.RemoveListener(CreateClassroom);
        createClassroomBackButton.onClick.RemoveListener(ShowTeacherOptions);
        previousClassroomButton.onClick.RemoveListener(ShowPreviousClassroom);
        nextClassroomButton.onClick.RemoveListener(ShowNextClassroom);
        refreshClassroomsButton.onClick.RemoveListener(RefreshTeacherClassrooms);
        toggleClassroomStatusButton.onClick.RemoveListener(ToggleClassroomStatus);
        manageClassroomsBackButton.onClick.RemoveListener(ShowTeacherOptions);
        if (dashboardRefreshButton != null)
            dashboardRefreshButton.onClick.RemoveListener(RefreshClassroomDashboard);
        if (dashboardBackButton != null)
            dashboardBackButton.onClick.RemoveListener(CloseClassroomDashboard);
        if (dashboardChapterDropdown != null)
            dashboardChapterDropdown.onValueChanged.RemoveListener(RenderDashboardChapter);
        if (dashboardPreviousStudentsButton != null)
            dashboardPreviousStudentsButton.onClick.RemoveListener(ShowPreviousDashboardStudents);
        if (dashboardNextStudentsButton != null)
            dashboardNextStudentsButton.onClick.RemoveListener(ShowNextDashboardStudents);
    }

    private void Refresh()
    {
        bool canSeeClassrooms = PlayerSession.IsSignedIn &&
                                 PlayerSession.EffectiveRole != AccountRole.Librarian;
        classroomButton.gameObject.SetActive(canSeeClassrooms);

        if (!canSeeClassrooms)
        {
            classroomPanel.SetActive(false);
            return;
        }

        bool isTeacher = PlayerSession.EffectiveRole == AccountRole.Teacher;
        playerOptionsRoot.SetActive(!isTeacher);
        teacherOptionsRoot.SetActive(isTeacher);
        joinClassroomRoot.SetActive(false);
        myClassroomsRoot.SetActive(false);
        createClassroomRoot.SetActive(false);
        manageClassroomsRoot.SetActive(false);
        if (classroomDashboardRoot != null)
            classroomDashboardRoot.SetActive(false);
    }

    private void Open()
    {
        Refresh();
        statusText.SetText(PlayerSession.EffectiveRole == AccountRole.Teacher
            ? "Create or manage your classrooms."
            : "Join a classroom or continue a classroom save.");
        classroomPanel.SetActive(true);
    }

    private void Close()
    {
        classroomPanel.SetActive(false);
    }

    private void ShowJoinPage()
    {
        playerOptionsRoot.SetActive(false);
        joinClassroomRoot.SetActive(true);
        classroomCodeInput.text = string.Empty;
        statusText.SetText("Enter the six-character classroom code.");
        classroomCodeInput.ActivateInputField();
    }

    private void ShowPlayerOptions()
    {
        if (isBusy)
            return;

        ResetLeaveConfirmation();
        joinClassroomRoot.SetActive(false);
        myClassroomsRoot.SetActive(false);
        classroomPanel.SetActive(true);
        playerOptionsRoot.SetActive(true);
        statusText.SetText("Join a classroom or continue a classroom save.");
    }

    private async void JoinClassroom()
    {
        if (isBusy)
            return;

        isBusy = true;
        SetInteractable(false);
        statusText.SetText("Joining classroom...");

        try
        {
            ClassroomMembership membership = await ClassroomService.JoinAsync(
                classroomCodeInput.text);
            classroomCodeInput.text = string.Empty;
            statusText.SetText(
                $"Joined {membership.roomName}.\nTeacher: {membership.teacherInGameName}");
        }
        catch (System.Exception exception)
        {
            statusText.SetText(exception.Message);
        }
        finally
        {
            isBusy = false;
            SetInteractable(true);
        }
    }

    private void ShowMyClassroomsPage()
    {
        classroomPanel.SetActive(false);
        playerOptionsRoot.SetActive(false);
        joinClassroomRoot.SetActive(false);
        myClassroomsRoot.SetActive(true);
        ResetLeaveConfirmation();
        RefreshPlayerClassrooms();
    }

    private async void RefreshPlayerClassrooms()
    {
        if (isBusy)
            return;

        isBusy = true;
        SetInteractable(false);
        statusText.SetText("Loading joined classrooms...");

        try
        {
            IReadOnlyList<ClassroomMembership> memberships = await ClassroomService
                .GetPlayerClassroomsAsync();
            playerClassrooms.Clear();
            playerClassrooms.AddRange(memberships);
            selectedPlayerClassroomIndex = 0;
            statusText.SetText(playerClassrooms.Count == 0
                ? "You have not joined any classrooms yet."
                : $"{playerClassrooms.Count} joined classroom(s) found.");
        }
        catch (System.Exception exception)
        {
            playerClassrooms.Clear();
            selectedPlayerClassroomIndex = 0;
            statusText.SetText(exception.Message);
        }
        finally
        {
            isBusy = false;
            SetInteractable(true);
            RenderJoinedClassroomCards();
            RenderJoinedClassroom();
        }
    }

    private void ShowPreviousJoinedClassroom()
    {
        ResetLeaveConfirmation();
        if (selectedPlayerClassroomIndex > 0)
            selectedPlayerClassroomIndex--;
        RenderJoinedClassroom();
    }

    private void ShowNextJoinedClassroom()
    {
        ResetLeaveConfirmation();
        if (selectedPlayerClassroomIndex + 1 < playerClassrooms.Count)
            selectedPlayerClassroomIndex++;
        RenderJoinedClassroom();
    }

    private void RenderJoinedClassroom()
    {
        bool hasClassrooms = playerClassrooms.Count > 0;
        previousJoinedClassroomButton.interactable = !isBusy && hasClassrooms &&
            selectedPlayerClassroomIndex > 0;
        nextJoinedClassroomButton.interactable = !isBusy && hasClassrooms &&
            selectedPlayerClassroomIndex + 1 < playerClassrooms.Count;
        playJoinedClassroomButton.interactable = !isBusy && hasClassrooms;
        leaveJoinedClassroomButton.interactable = !isBusy && hasClassrooms;

        if (!hasClassrooms)
        {
            joinedClassroomText.SetText("No joined classrooms.");
            return;
        }

        ClassroomMembership membership = playerClassrooms[selectedPlayerClassroomIndex];
        joinedClassroomText.SetText(
            $"Classroom {selectedPlayerClassroomIndex + 1} of {playerClassrooms.Count}\n\n" +
            $"{membership.roomName}\n" +
            $"Teacher: {membership.teacherInGameName}\n" +
            $"Status: {membership.status}");
    }

    private void RenderJoinedClassroomCards()
    {
        RebuildCards(joinedClassroomCardsRoot, playerClassrooms.Count, index =>
        {
            ClassroomMembership room = playerClassrooms[index];
            string details =
                $"Teacher: {room.teacherInGameName}\nStatus: {room.status}";
            if (index == selectedPlayerClassroomIndex &&
                !string.IsNullOrWhiteSpace(playerClassroomNotice))
                details += $"\n\n{playerClassroomNotice}";
            return (room.roomName,
                details);
        }, index =>
        {
            ResetLeaveConfirmation();
            selectedPlayerClassroomIndex = index;
            RenderJoinedClassroomCards();
            RenderJoinedClassroom();
        }, selectedPlayerClassroomIndex,
        _ => "Play",
        index =>
        {
            selectedPlayerClassroomIndex = index;
            PlayJoinedClassroom();
        },
        index => leaveConfirmationPending && index == selectedPlayerClassroomIndex
            ? "Confirm Leave"
            : "Leave",
        index =>
        {
            selectedPlayerClassroomIndex = index;
            LeaveJoinedClassroom();
        });
    }

    private async void PlayJoinedClassroom()
    {
        if (isBusy || selectedPlayerClassroomIndex < 0 ||
            selectedPlayerClassroomIndex >= playerClassrooms.Count)
            return;

        ClassroomMembership membership = playerClassrooms[selectedPlayerClassroomIndex];
        playerClassroomNotice = "Checking classroom access...";
        RenderJoinedClassroomCards();
        isBusy = true;
        SetInteractable(false);
        ClassroomAccessResponse access;
        try
        {
            access = await ClassroomService.ValidateAccessAsync(membership.roomId);
        }
        catch (System.Exception exception)
        {
            playerClassroomNotice = exception.Message;
            statusText.SetText(exception.Message);
            Debug.LogError($"Classroom access validation failed: {exception.Message}", this);
            isBusy = false;
            SetInteractable(true);
            RenderJoinedClassroomCards();
            return;
        }

        isBusy = false;
        SetInteractable(true);
        if (!access.success)
        {
            if (string.Equals(access.status, "Deleted",
                    System.StringComparison.Ordinal))
            {
                ClassroomLocalCache.TryDeleteRoomSave(membership.roomId, out _);
                ClassroomLocalCache.Remove(membership.roomId);
                playerClassrooms.RemoveAt(selectedPlayerClassroomIndex);
                selectedPlayerClassroomIndex = 0;
            }
            statusText.SetText(access.error);
            playerClassroomNotice = access.error;
            RenderJoinedClassroomCards();
            RenderJoinedClassroom();
            return;
        }

        playerClassroomNotice = string.Empty;
        statusText.SetText($"Opening {membership.roomName}...");
        if (!mainMenuController.TryPlayClassroom(membership.roomId))
        {
            playerClassroomNotice = "The classroom save could not be opened. Check the Console.";
            statusText.SetText("The classroom save could not be opened.");
            RenderJoinedClassroomCards();
        }
    }

    private async void LeaveJoinedClassroom()
    {
        if (isBusy || selectedPlayerClassroomIndex < 0 ||
            selectedPlayerClassroomIndex >= playerClassrooms.Count)
            return;

        ClassroomMembership membership = playerClassrooms[selectedPlayerClassroomIndex];
        if (!leaveConfirmationPending)
        {
            leaveConfirmationPending = true;
            SetButtonLabel(leaveJoinedClassroomButton, "Confirm Leave");
            statusText.SetText(
                $"Leave {membership.roomName}? Its local classroom save will be deleted. " +
                "Press Confirm Leave to continue.");
            RenderJoinedClassroomCards();
            return;
        }

        isBusy = true;
        SetInteractable(false);
        statusText.SetText($"Leaving {membership.roomName}...");
        try
        {
            await ClassroomService.LeaveAsync(membership.roomId);
            playerClassrooms.RemoveAt(selectedPlayerClassroomIndex);
            selectedPlayerClassroomIndex = Mathf.Clamp(
                selectedPlayerClassroomIndex, 0, Mathf.Max(0, playerClassrooms.Count - 1));
            statusText.SetText($"Left {membership.roomName}. Its local save was removed.");
        }
        catch (System.Exception exception)
        {
            statusText.SetText(exception.Message);
        }
        finally
        {
            isBusy = false;
            ResetLeaveConfirmation();
            SetInteractable(true);
            RenderJoinedClassroomCards();
            RenderJoinedClassroom();
        }
    }

    private void ResetLeaveConfirmation()
    {
        leaveConfirmationPending = false;
        SetButtonLabel(leaveJoinedClassroomButton, "Leave Classroom");
    }

    private static void SetButtonLabel(Button button, string value)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null)
            label.SetText(value);
    }

    private void ShowCreatePage()
    {
        teacherOptionsRoot.SetActive(false);
        createClassroomRoot.SetActive(true);
        classroomNameInput.text = string.Empty;
        statusText.SetText("Enter a name for the new classroom.");
        classroomNameInput.ActivateInputField();
    }

    private void ShowTeacherOptions()
    {
        if (isBusy)
            return;

        createClassroomRoot.SetActive(false);
        manageClassroomsRoot.SetActive(false);
        classroomPanel.SetActive(true);
        joinClassroomRoot.SetActive(false);
        myClassroomsRoot.SetActive(false);
        teacherOptionsRoot.SetActive(true);
        statusText.SetText("Create or manage your classrooms.");
    }

    private async void CreateClassroom()
    {
        if (isBusy)
            return;

        isBusy = true;
        SetInteractable(false);
        statusText.SetText("Creating classroom...");

        ClassroomOperationResult result = await ClassroomService.CreateAsync(
            classroomNameInput.text);

        isBusy = false;
        SetInteractable(true);
        if (!result.Success)
        {
            statusText.SetText(result.Error);
            return;
        }

        classroomNameInput.text = string.Empty;
        statusText.SetText(
            $"{result.Classroom.roomName} created.\nJoin code: {result.Classroom.joinCode}");
    }

    private void ShowManagePage()
    {
        classroomPanel.SetActive(false);
        teacherOptionsRoot.SetActive(false);
        createClassroomRoot.SetActive(false);
        manageClassroomsRoot.SetActive(true);
        RefreshTeacherClassrooms();
    }

    private async void RefreshTeacherClassrooms()
    {
        if (isBusy)
            return;

        isBusy = true;
        SetInteractable(false);
        statusText.SetText("Loading classrooms...");

        TeacherClassroomListResult result = await ClassroomService
            .GetTeacherClassroomsAsync();

        isBusy = false;
        SetInteractable(true);
        teacherClassrooms.Clear();
        selectedClassroomIndex = 0;

        if (!result.Success)
        {
            managedClassroomText.SetText("Classrooms could not be loaded.");
            statusText.SetText(result.Error);
            RenderManagedClassroom();
            return;
        }

        teacherClassrooms.AddRange(result.Classrooms);
        statusText.SetText(teacherClassrooms.Count == 0
            ? "You have not created any classrooms yet."
            : $"{teacherClassrooms.Count} classroom(s) found.");
        RenderManagedClassroomCards();
        RefreshSelectedClassroomDetails();
    }

    private void ShowPreviousClassroom()
    {
        if (selectedClassroomIndex > 0)
            selectedClassroomIndex--;
        RefreshSelectedClassroomDetails();
    }

    private void ShowNextClassroom()
    {
        if (selectedClassroomIndex + 1 < teacherClassrooms.Count)
            selectedClassroomIndex++;
        RefreshSelectedClassroomDetails();
    }

    private async void RefreshSelectedClassroomDetails()
    {
        selectedClassroomMembers.Clear();
        selectedClassroomDetailsLoaded = false;
        RenderManagedClassroom();
        if (teacherClassrooms.Count == 0 || isBusy)
            return;

        isBusy = true;
        SetInteractable(false);
        try
        {
            IReadOnlyList<ClassroomMemberSummary> members = await ClassroomService
                .GetClassroomMembersAsync(teacherClassrooms[selectedClassroomIndex].roomId);
            selectedClassroomMembers.AddRange(members);
            selectedClassroomDetailsLoaded = true;
        }
        catch (System.Exception exception)
        {
            statusText.SetText(exception.Message);
        }
        finally
        {
            isBusy = false;
            SetInteractable(true);
            RenderManagedClassroom();
        }
    }

    private async void ToggleClassroomStatus()
    {
        if (isBusy || teacherClassrooms.Count == 0)
            return;

        ClassroomSummary room = teacherClassrooms[selectedClassroomIndex];
        if (!string.Equals(deleteConfirmationRoomId, room.roomId,
                System.StringComparison.Ordinal))
        {
            deleteConfirmationRoomId = room.roomId;
            statusText.SetText(
                $"Delete {room.roomName}? Players will lose access when they next connect. " +
                "Press Confirm Delete to continue.");
            RenderManagedClassroomCards();
            return;
        }

        isBusy = true;
        SetInteractable(false);
        statusText.SetText("Deleting classroom...");
        try
        {
            room.status = await ClassroomService.SetClassroomStatusAsync(
                room.roomId, "Deleted");
            teacherClassrooms.RemoveAt(selectedClassroomIndex);
            selectedClassroomIndex = Mathf.Clamp(selectedClassroomIndex, 0,
                Mathf.Max(0, teacherClassrooms.Count - 1));
            selectedClassroomMembers.Clear();
            selectedClassroomDetailsLoaded = false;
            statusText.SetText("Classroom deleted. Its historical data was preserved.");
        }
        catch (System.Exception exception)
        {
            statusText.SetText(exception.Message);
        }
        finally
        {
            isBusy = false;
            deleteConfirmationRoomId = string.Empty;
            SetInteractable(true);
            RenderManagedClassroom();
        }
    }

    private void RenderManagedClassroom()
    {
        bool hasClassrooms = teacherClassrooms.Count > 0;
        previousClassroomButton.interactable = !isBusy && hasClassrooms &&
                                               selectedClassroomIndex > 0;
        nextClassroomButton.interactable = !isBusy && hasClassrooms &&
                                           selectedClassroomIndex + 1 < teacherClassrooms.Count;

        if (!hasClassrooms)
        {
            managedClassroomText.SetText("No classrooms created.");
            toggleClassroomStatusButton.interactable = false;
            RenderManagedClassroomCards();
            return;
        }

        ClassroomSummary room = teacherClassrooms[selectedClassroomIndex];
        int activeMembers = selectedClassroomMembers.FindAll(
            member => member.status == "Active").Count;
        System.Text.StringBuilder roster = new();
        if (selectedClassroomMembers.Count == 0)
            roster.Append("No students yet.");
        else
        {
            foreach (ClassroomMemberSummary member in selectedClassroomMembers)
                roster.AppendLine($"• {member.inGameName} — {member.status}");
        }

        managedClassroomText.SetText(
            $"Classroom {selectedClassroomIndex + 1} of {teacherClassrooms.Count}\n\n" +
            $"{room.roomName}\n" +
            $"Join code: {room.joinCode}\n" +
            $"Status: {room.status}\n" +
            $"Active students: {activeMembers}\n\nSTUDENT ROSTER\n{roster}");
        SetButtonLabel(toggleClassroomStatusButton,
            "Delete Classroom");
        RenderManagedClassroomCards();
    }

    private void RenderManagedClassroomCards()
    {
        RebuildCards(managedClassroomCardsRoot, teacherClassrooms.Count, index =>
        {
            ClassroomSummary room = teacherClassrooms[index];
            string details = $"Join code: {room.joinCode}\n" +
                             $"Status: {room.status}\n" +
                             $"Active students: {room.memberCount}";

            if (index == selectedClassroomIndex && selectedClassroomDetailsLoaded)
            {
                details += "\n\nSTUDENT ROSTER";
                if (selectedClassroomMembers.Count == 0)
                {
                    details += "\nNo students yet.";
                }
                else
                {
                    int visibleCount = Mathf.Min(selectedClassroomMembers.Count, 6);
                    for (int memberIndex = 0; memberIndex < visibleCount; memberIndex++)
                    {
                        ClassroomMemberSummary member = selectedClassroomMembers[memberIndex];
                        details += $"\n• {member.inGameName} — {member.status}";
                    }
                    if (selectedClassroomMembers.Count > visibleCount)
                        details += $"\n+{selectedClassroomMembers.Count - visibleCount} more";
                }
            }

            return (room.roomName, details);
        }, index =>
        {
            selectedClassroomIndex = index;
            RenderManagedClassroomCards();
            RefreshSelectedClassroomDetails();
        }, selectedClassroomIndex,
        _ => "Dashboard",
        index =>
        {
            selectedClassroomIndex = index;
            RenderManagedClassroomCards();
            OpenClassroomDashboard();
        },
        index => string.Equals(deleteConfirmationRoomId,
                    teacherClassrooms[index].roomId, System.StringComparison.Ordinal)
            ? "Confirm Delete"
            : "Delete",
        index =>
        {
            selectedClassroomIndex = index;
            ToggleClassroomStatus();
        });
    }

    private void OpenClassroomDashboard()
    {
        if (isBusy || teacherClassrooms.Count == 0 ||
            selectedClassroomIndex < 0 ||
            selectedClassroomIndex >= teacherClassrooms.Count)
            return;

        TeacherClassroomDashboardLaunchContext.Set(
            teacherClassrooms[selectedClassroomIndex]);
        SceneManager.LoadScene(TeacherClassroomDashboardLaunchContext.SceneName);
    }

    private async void RefreshClassroomDashboard()
    {
        if (isBusy || classroomDashboardRoot == null ||
            !classroomDashboardRoot.activeSelf)
            return;

        await LoadClassroomDashboardAsync();
    }

    private async System.Threading.Tasks.Task LoadClassroomDashboardAsync()
    {
        if (teacherClassrooms.Count == 0)
            return;

        ClassroomSummary selectedRoom = teacherClassrooms[selectedClassroomIndex];
        isBusy = true;
        SetInteractable(false);
        dashboardStatusText.SetText("Loading classroom dashboard...");

        ClassroomDashboardResponse response = await ClassroomService
            .GetDashboardAsync(selectedRoom.roomId);

        isBusy = false;
        SetInteractable(true);
        if (response == null || !response.success)
        {
            dashboardStatusText.SetText(
                response?.error ?? "The classroom dashboard could not be loaded.");
            return;
        }

        dashboardData = response;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(
            dashboardData.members.Count / (float)DashboardStudentsPerPage));
        dashboardStudentPage = Mathf.Clamp(dashboardStudentPage, 0, pageCount - 1);
        dashboardStatusText.SetText(string.Empty);
        RenderClassroomDashboard();
    }

    private void CloseClassroomDashboard()
    {
        if (isBusy || classroomDashboardRoot == null)
            return;

        classroomDashboardRoot.SetActive(false);
        manageClassroomsRoot.SetActive(true);
        dashboardData = null;
        dashboardStudentPage = 0;
        RenderManagedClassroomCards();
    }

    private void RenderClassroomDashboard()
    {
        ClassroomSummary selectedRoom = teacherClassrooms.Count > 0
            ? teacherClassrooms[selectedClassroomIndex]
            : null;
        string roomName = dashboardData?.roomName ?? selectedRoom?.roomName ?? "Classroom";
        dashboardTitleText.SetText($"{roomName} Dashboard");

        int totalStudents = dashboardData?.members?.Count ?? 0;
        int uploadedStudents = dashboardData?.participantCount ?? 0;
        string joinCode = dashboardData?.joinCode ?? selectedRoom?.joinCode ?? string.Empty;
        string roomStatus = dashboardData?.status ?? selectedRoom?.status ?? "Unknown";
        dashboardSummaryText.SetText(
            $"Join code: {joinCode}     Status: {roomStatus}     " +
            $"Students: {totalStudents}     Analytics uploaded: {uploadedStudents}");

        PopulateDashboardChapterDropdown();
        RenderDashboardChapter(dashboardChapterDropdown != null
            ? dashboardChapterDropdown.value
            : 0);
    }

    private void RenderDashboardStudentCards()
    {
        if (dashboardStudentCardsRoot == null)
            return;

        List<ClassroomDashboardMember> members =
            dashboardData?.members ?? new List<ClassroomDashboardMember>();
        int firstMemberIndex = dashboardStudentPage * DashboardStudentsPerPage;
        for (int index = 0; index < dashboardStudentCardsRoot.childCount; index++)
        {
            GameObject card = dashboardStudentCardsRoot.GetChild(index).gameObject;
            int memberIndex = firstMemberIndex + index;
            bool visible = index < DashboardStudentsPerPage && memberIndex < members.Count;
            card.SetActive(visible);
            if (!visible)
                continue;

            ClassroomDashboardMember member = members[memberIndex];
            bool uploadedSelectedChapter = HasUploadedSelectedChapter(member);
            TMP_Text label = card.GetComponentInChildren<TMP_Text>(true);
            label?.SetText(
                $"{member.inGameName}\n" +
                (uploadedSelectedChapter
                    ? "Analytics uploaded"
                    : "Not uploaded"));
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
            members.Count / (float)DashboardStudentsPerPage));
        if (dashboardStudentPageText != null)
            dashboardStudentPageText.SetText($"Page {dashboardStudentPage + 1} of {pageCount}");
        if (dashboardPreviousStudentsButton != null)
            dashboardPreviousStudentsButton.interactable = !isBusy && dashboardStudentPage > 0;
        if (dashboardNextStudentsButton != null)
        {
            dashboardNextStudentsButton.interactable = !isBusy &&
                dashboardStudentPage + 1 < pageCount;
        }
    }

    private void ShowPreviousDashboardStudents()
    {
        if (isBusy || dashboardStudentPage <= 0)
            return;

        dashboardStudentPage--;
        RenderDashboardStudentCards();
    }

    private void ShowNextDashboardStudents()
    {
        int memberCount = dashboardData?.members?.Count ?? 0;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(
            memberCount / (float)DashboardStudentsPerPage));
        if (isBusy || dashboardStudentPage + 1 >= pageCount)
            return;

        dashboardStudentPage++;
        RenderDashboardStudentCards();
    }

    private void PopulateDashboardChapterDropdown()
    {
        if (dashboardChapterDropdown == null)
            return;

        List<string> options = new(DashboardChapterIds.Length);
        foreach (string chapterId in DashboardChapterIds)
            options.Add(FormatChapterLabel(chapterId));

        dashboardChapterDropdown.ClearOptions();
        dashboardChapterDropdown.AddOptions(options);
        dashboardChapterDropdown.SetValueWithoutNotify(0);
        dashboardChapterDropdown.RefreshShownValue();
        dashboardChapterDropdown.interactable = !isBusy;
    }

    private void RenderDashboardChapter(int chapterIndex)
    {
        RenderDashboardStudentCards();

        if (dashboardData == null)
        {
            dashboardAnalyticsOverviewText.SetText(
                "Load the dashboard to view classroom analytics.");
            SetDashboardMetricsVisible(false);
            return;
        }

        int safeChapterIndex = Mathf.Clamp(chapterIndex, 0, DashboardChapterIds.Length - 1);
        string chapterId = DashboardChapterIds[safeChapterIndex];
        ClassroomChapterAnalyticsAggregate chapter = dashboardData.chapters?.Find(candidate =>
            string.Equals(candidate.chapterId, chapterId,
                System.StringComparison.OrdinalIgnoreCase));
        if (chapter == null)
        {
            dashboardAnalyticsOverviewText.SetText(
                $"UNIQUE UPLOADS\n{dashboardData.participantCount} of " +
                $"{dashboardData.members.Count} students\n\n" +
                $"{FormatChapterLabel(chapterId).ToUpperInvariant()} AVERAGE RESULTS\n" +
                "Chapter participants: 0\nAverage playtime: —\n\n" +
                "No completed chapter analytics have been uploaded yet.");
            SetDashboardMetricsVisible(false);
            return;
        }

        dashboardAnalyticsOverviewText.SetText(
            $"UNIQUE UPLOADS\n{dashboardData.participantCount} of " +
            $"{dashboardData.members.Count} students\n\n" +
            $"{FormatChapterLabel(chapter.chapterId).ToUpperInvariant()} AVERAGE RESULTS\n" +
            $"Chapter participants: {chapter.participantCount}\n" +
            $"Average playtime: {FormatPlaytime(chapter.averagePlayTimeSeconds)}");

        SetDashboardMetricsVisible(true);
        dashboardEngagementMetric.SetValue(
            "Engagement", chapter.averageEngagementRatePercent);
        dashboardQuizMetric.SetValue(
            "Quiz Score", chapter.averageQuizScoreRatePercent);
        dashboardDialogueMetric.SetValue(
            "Dialogue Attention",
            100d - chapter.averageDialogueSkipRatePercent,
            $"Skip rate: {chapter.averageDialogueSkipRatePercent:0.0}%");
        dashboardArtifactMetric.SetValue(
            "Artifact Discovery", chapter.averageArtifactDiscoveryRatePercent);
    }

    private bool HasUploadedSelectedChapter(ClassroomDashboardMember member)
    {
        if (member?.uploadedChapterIds == null || dashboardChapterDropdown == null)
            return member?.hasUploadedAnalytics == true;

        int chapterIndex = Mathf.Clamp(
            dashboardChapterDropdown.value, 0, DashboardChapterIds.Length - 1);
        string chapterId = DashboardChapterIds[chapterIndex];
        return member.uploadedChapterIds.Exists(uploadedChapterId =>
            string.Equals(uploadedChapterId, chapterId,
                System.StringComparison.OrdinalIgnoreCase));
    }

    private void SetDashboardMetricsVisible(bool visible)
    {
        dashboardEngagementMetric?.SetVisible(visible);
        dashboardQuizMetric?.SetVisible(visible);
        dashboardDialogueMetric?.SetVisible(visible);
        dashboardArtifactMetric?.SetVisible(visible);
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

    private async void ReconcilePlayerClassrooms()
    {
        if (isReconciling || !PlayerSession.IsSignedIn ||
            PlayerSession.EffectiveRole != AccountRole.Player)
            return;

        isReconciling = true;
        try
        {
            IReadOnlyList<ClassroomMembership> memberships = await ClassroomService
                .GetPlayerClassroomsAsync();
            if (myClassroomsRoot != null && myClassroomsRoot.activeSelf)
            {
                playerClassrooms.Clear();
                playerClassrooms.AddRange(memberships);
                selectedPlayerClassroomIndex = 0;
                RenderJoinedClassroomCards();
                RenderJoinedClassroom();
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Classroom reconciliation failed: {exception.Message}");
        }
        finally
        {
            isReconciling = false;
        }
    }

    private static void RebuildCards(
        Transform root,
        int count,
        System.Func<int, (string title, string details)> content,
        System.Action<int> select,
        int selectedIndex,
        System.Func<int, string> primaryLabel,
        System.Action<int> primaryAction,
        System.Func<int, string> secondaryLabel,
        System.Action<int> secondaryAction)
    {
        if (root == null)
            return;

        for (int index = 0; index < root.childCount; index++)
        {
            GameObject card = root.GetChild(index).gameObject;
            bool visible = index < count;
            card.SetActive(visible);
            if (!visible)
                continue;

            int capturedIndex = index;
            (string title, string details) = content(index);
            Image background = card.GetComponent<Image>();
            if (background != null)
                background.color = index == selectedIndex
                    ? new Color(0.25f, 0.48f, 0.62f, 0.96f)
                    : new Color(0.12f, 0.18f, 0.24f, 0.92f);
            Button cardButton = card.GetComponent<Button>();
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => select(capturedIndex));
            TMP_Text[] texts = card.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0)
                texts[0].SetText(title);
            if (texts.Length > 1)
                texts[1].SetText(details);

            Button[] buttons = card.GetComponentsInChildren<Button>(true);
            if (buttons.Length > 1)
            {
                Button primary = buttons[1];
                primary.onClick.RemoveAllListeners();
                primary.onClick.AddListener(() => primaryAction(capturedIndex));
                SetButtonLabel(primary, primaryLabel(capturedIndex));
            }
            if (buttons.Length > 2)
            {
                Button secondary = buttons[2];
                secondary.onClick.RemoveAllListeners();
                secondary.onClick.AddListener(() => secondaryAction(capturedIndex));
                SetButtonLabel(secondary, secondaryLabel(capturedIndex));
            }
        }
    }

    private void SetInteractable(bool interactable)
    {
        classroomButton.interactable = interactable;
        joinClassroomButton.interactable = interactable;
        myClassroomsButton.interactable = interactable;
        createClassroomButton.interactable = interactable;
        manageClassroomsButton.interactable = interactable;
        submitCreateClassroomButton.interactable = interactable;
        createClassroomBackButton.interactable = interactable;
        previousClassroomButton.interactable = interactable;
        nextClassroomButton.interactable = interactable;
        refreshClassroomsButton.interactable = interactable;
        toggleClassroomStatusButton.interactable = interactable && teacherClassrooms.Count > 0;
        manageClassroomsBackButton.interactable = interactable;
        closeButton.interactable = interactable;
        classroomNameInput.interactable = interactable;
        classroomCodeInput.interactable = interactable;
        submitJoinClassroomButton.interactable = interactable;
        joinClassroomBackButton.interactable = interactable;
        previousJoinedClassroomButton.interactable = interactable;
        nextJoinedClassroomButton.interactable = interactable;
        refreshJoinedClassroomsButton.interactable = interactable;
        playJoinedClassroomButton.interactable = interactable && playerClassrooms.Count > 0;
        leaveJoinedClassroomButton.interactable = interactable && playerClassrooms.Count > 0;
        myClassroomsBackButton.interactable = interactable;
        if (dashboardRefreshButton != null)
            dashboardRefreshButton.interactable = interactable;
        if (dashboardBackButton != null)
            dashboardBackButton.interactable = interactable;
        if (dashboardChapterDropdown != null)
            dashboardChapterDropdown.interactable = interactable;
        if (dashboardPreviousStudentsButton != null)
            dashboardPreviousStudentsButton.interactable = interactable;
        if (dashboardNextStudentsButton != null)
            dashboardNextStudentsButton.interactable = interactable;

        if (dashboardStudentCardsRoot != null)
        {
            foreach (Button button in dashboardStudentCardsRoot
                         .GetComponentsInChildren<Button>(true))
            {
                button.interactable = interactable;
            }
        }
    }
}
