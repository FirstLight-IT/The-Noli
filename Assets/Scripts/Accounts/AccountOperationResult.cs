using System;

public sealed class AccountOperationResult
{
    public bool Success { get; }
    public string Error { get; }

    private AccountOperationResult(bool success, string error)
    {
        Success = success;
        Error = error ?? string.Empty;
    }

    public static AccountOperationResult Succeeded() =>
        new(true, string.Empty);

    public static AccountOperationResult Failed(string error) =>
        new(false, error);
}

[Serializable]
public sealed class TeacherEmailConfirmationResponse
{
    public bool success;
    public string error = string.Empty;
    public AccountProfile profile;
}

[Serializable]
public sealed class TeacherEmailResendResponse
{
    public bool success;
    public string error = string.Empty;
    public string expiresAtUtc = string.Empty;
    public int resendsRemaining;
}
