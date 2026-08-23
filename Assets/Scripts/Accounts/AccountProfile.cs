using System;

[Serializable]
public sealed class AccountProfile
{
    public string accountId = string.Empty;
    public string username = string.Empty;
    public string inGameName = string.Empty;
    public string fullName = string.Empty;
    public string schoolEmail = string.Empty;
    public string emailConfirmationStatus = "NotApplicable";
    public string effectiveRole = "Player";
    public string createdAtUtc = string.Empty;
    public AccountRole role = AccountRole.Player;
    public TeacherVerificationStatus teacherVerificationStatus =
        TeacherVerificationStatus.NotApplicable;

    public AccountRole EffectiveRole =>
        role == AccountRole.Teacher &&
        teacherVerificationStatus != TeacherVerificationStatus.Verified
            ? AccountRole.Player
            : role;

    public AccountProfile Copy()
    {
        return new AccountProfile
        {
            accountId = accountId?.Trim() ?? string.Empty,
            username = username?.Trim() ?? string.Empty,
            inGameName = inGameName?.Trim() ?? string.Empty,
            fullName = fullName?.Trim() ?? string.Empty,
            schoolEmail = schoolEmail?.Trim() ?? string.Empty,
            emailConfirmationStatus = emailConfirmationStatus?.Trim() ?? string.Empty,
            effectiveRole = effectiveRole?.Trim() ?? string.Empty,
            createdAtUtc = createdAtUtc?.Trim() ?? string.Empty,
            role = role,
            teacherVerificationStatus = role == AccountRole.Teacher
                ? teacherVerificationStatus
                : TeacherVerificationStatus.NotApplicable
        };
    }
}
