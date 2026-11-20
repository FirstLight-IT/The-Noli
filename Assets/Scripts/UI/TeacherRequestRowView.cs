using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TeacherRequestRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text teacherNameText;
    [SerializeField] private TMP_Text emailText;
    [SerializeField] private TMP_Text dateSubmittedText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button approveButton;
    [SerializeField] private Button declineButton;
    [SerializeField] private TMP_Text completedActionText;

    private TeacherReviewRequest request;
    private Action<TeacherReviewRequest, bool> reviewRequested;

    public void Bind(
        TeacherReviewRequest teacherRequest,
        Action<TeacherReviewRequest, bool> onReviewRequested)
    {
        request = teacherRequest;
        reviewRequested = onReviewRequested;

        teacherNameText.SetText(request.fullName);
        emailText.SetText(request.schoolEmail);
        dateSubmittedText.SetText(FormatDate(request.submittedAtUtc));

        string status = request.teacherVerificationStatus ?? string.Empty;
        bool pending = status.Equals("Pending", StringComparison.OrdinalIgnoreCase);
        bool approved = status.Equals("Verified", StringComparison.OrdinalIgnoreCase) ||
                        status.Equals("AwaitingEmailConfirmation", StringComparison.OrdinalIgnoreCase);

        statusText.SetText(pending ? "PENDING" : approved ? "APPROVED" : "DECLINED");
        statusText.color = pending
            ? new Color(0.60f, 0.33f, 0.04f, 1f)
            : approved
                ? new Color(0.12f, 0.46f, 0.20f, 1f)
                : new Color(0.67f, 0.16f, 0.09f, 1f);

        approveButton.gameObject.SetActive(pending);
        declineButton.gameObject.SetActive(pending);
        completedActionText.gameObject.SetActive(!pending);
        if (!pending)
        {
            completedActionText.SetText(approved
                ? status.Equals("Verified", StringComparison.OrdinalIgnoreCase)
                    ? "Account Activated"
                    : "Awaiting Confirmation"
                : "Request Declined");
        }

        approveButton.onClick.RemoveAllListeners();
        declineButton.onClick.RemoveAllListeners();
        approveButton.onClick.AddListener(() => reviewRequested?.Invoke(request, true));
        declineButton.onClick.AddListener(() => reviewRequested?.Invoke(request, false));
    }

    public void SetInteractable(bool interactable)
    {
        approveButton.interactable = interactable;
        declineButton.interactable = interactable;
    }

    private static string FormatDate(string utcValue)
    {
        return DateTime.TryParse(
            utcValue,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out DateTime date)
            ? date.ToLocalTime().ToString("MMM d, yyyy")
            : "—";
    }
}
