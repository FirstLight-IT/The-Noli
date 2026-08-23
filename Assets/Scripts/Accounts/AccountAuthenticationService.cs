using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;

public static class AccountAuthenticationService
{
    private const string UsernameKey = "profile_username";
    private const string InGameNameKey = "profile_in_game_name";
    private const string ProtectedProfileKey = "account_profile";
    private const string CreateProfileScriptName = "CreateAccountProfile";
    private const string UpdateInGameNameScriptName = "UpdateInGameName";
    private const string ConfirmTeacherEmailScriptName = "ConfirmTeacherEmail";
    private const string ResendTeacherEmailCodeScriptName = "ResendTeacherEmailCode";

    private static Task initializationTask;

    public static async Task<AccountOperationResult> RestoreCachedSessionAsync()
    {
        try
        {
            await EnsureInitializedAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                if (!AuthenticationService.Instance.SessionTokenExists)
                    return AccountOperationResult.Succeeded();

                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            if (PlayerSession.IsSignedIn &&
                string.Equals(
                    PlayerSession.AccountId,
                    AuthenticationService.Instance.PlayerId,
                    StringComparison.Ordinal))
            {
                return AccountOperationResult.Succeeded();
            }

            AccountProfile profile = await LoadProtectedPlayerProfileAsync();
            if (profile == null)
                profile = await TryMigrateLegacyPlayerProfileAsync();

            if (profile == null)
            {
                AuthenticationService.Instance.SignOut(clearCredentials: true);
                return AccountOperationResult.Failed(
                    "The saved session does not have a complete The Noli profile.");
            }

            if (!PlayerSession.TryBeginAccountSession(profile, out string error))
                return AccountOperationResult.Failed(error);

            return AccountOperationResult.Succeeded();
        }
        catch (Exception exception) when (IsExpectedServiceException(exception))
        {
            PlayerSession.ReturnToGuest();
            return AccountOperationResult.Failed(GetFriendlyError(exception));
        }
    }

    public static async Task<AccountOperationResult> RegisterPlayerAsync(
        string username,
        string password,
        string inGameName)
    {
        return await RegisterAsync(
            username,
            password,
            inGameName,
            AccountRole.Player,
            string.Empty,
            string.Empty);
    }

    public static bool TryValidateAccountDetails(
        string username,
        string password,
        string inGameName,
        out string error)
    {
        string trimmedUsername = username?.Trim() ?? string.Empty;
        string trimmedInGameName = inGameName?.Trim() ?? string.Empty;

        if (trimmedUsername.Length < 3 || trimmedUsername.Length > 20)
        {
            error = "Username must be 3 to 20 characters.";
            return false;
        }

        foreach (char character in trimmedUsername)
        {
            bool allowed = character >= 'A' && character <= 'Z' ||
                           character >= 'a' && character <= 'z' ||
                           character >= '0' && character <= '9' ||
                           character == '.' || character == '-' ||
                           character == '@' || character == '_';

            if (!allowed)
            {
                error = "Username may only use letters, numbers, ., -, @, and _.";
                return false;
            }
        }

        if (password == null || password.Length < 8 || password.Length > 30)
        {
            error = "Password must be 8 to 30 characters.";
            return false;
        }

        bool hasLowercase = false;
        bool hasUppercase = false;
        bool hasNumber = false;
        bool hasSymbol = false;
        bool hasWhitespace = false;

        foreach (char character in password)
        {
            hasLowercase |= char.IsLower(character);
            hasUppercase |= char.IsUpper(character);
            hasNumber |= char.IsDigit(character);
            hasSymbol |= !char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character);
            hasWhitespace |= char.IsWhiteSpace(character);
        }

        if (hasWhitespace || !hasLowercase || !hasUppercase || !hasNumber || !hasSymbol)
        {
            error = "Password needs an uppercase letter, lowercase letter, number, and symbol.";
            return false;
        }

        if (trimmedInGameName.Length == 0 || trimmedInGameName.Length > 30)
        {
            error = "In-game name must be 1 to 30 characters.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidatePassword(string password, out string error)
    {
        if (password == null || password.Length < 8 || password.Length > 30)
        {
            error = "Password must be 8 to 30 characters.";
            return false;
        }

        bool hasLowercase = false;
        bool hasUppercase = false;
        bool hasNumber = false;
        bool hasSymbol = false;
        bool hasWhitespace = false;

        foreach (char character in password)
        {
            hasLowercase |= char.IsLower(character);
            hasUppercase |= char.IsUpper(character);
            hasNumber |= char.IsDigit(character);
            hasSymbol |= !char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character);
            hasWhitespace |= char.IsWhiteSpace(character);
        }

        if (hasWhitespace || !hasLowercase || !hasUppercase || !hasNumber || !hasSymbol)
        {
            error = "Password needs an uppercase letter, lowercase letter, number, and symbol.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static async Task<AccountOperationResult> UpdateInGameNameAsync(string inGameName)
    {
        string trimmedName = inGameName?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0 || trimmedName.Length > 30)
            return AccountOperationResult.Failed("In-game name must be 1 to 30 characters.");

        if (!PlayerSession.IsSignedIn)
            return AccountOperationResult.Failed("Sign in before changing your in-game name.");

        try
        {
            Dictionary<string, object> arguments = new()
            {
                ["inGameName"] = trimmedName
            };

            AccountProfile profile = await CloudCodeService.Instance
                .CallEndpointAsync<AccountProfile>(UpdateInGameNameScriptName, arguments);

            return PlayerSession.TryUpdateAccountProfile(profile, out string error)
                ? AccountOperationResult.Succeeded()
                : AccountOperationResult.Failed(error);
        }
        catch (Exception exception)
        {
            return AccountOperationResult.Failed(GetFriendlyError(exception));
        }
    }

    public static async Task<AccountOperationResult> UpdatePasswordAsync(
        string currentPassword,
        string newPassword)
    {
        if (string.IsNullOrEmpty(currentPassword))
            return AccountOperationResult.Failed("Enter your current password.");

        if (!TryValidatePassword(newPassword, out string error))
            return AccountOperationResult.Failed(error);

        if (!PlayerSession.IsSignedIn)
            return AccountOperationResult.Failed("Sign in before changing your password.");

        try
        {
            await AuthenticationService.Instance.UpdatePasswordAsync(currentPassword, newPassword);
            return AccountOperationResult.Succeeded();
        }
        catch (Exception exception)
        {
            return AccountOperationResult.Failed(GetFriendlyError(exception));
        }
    }

    public static async Task<AccountOperationResult> ConfirmTeacherEmailAsync(string code)
    {
        string trimmedCode = code?.Trim() ?? string.Empty;
        if (trimmedCode.Length != 6)
            return AccountOperationResult.Failed("Enter the six-digit confirmation code.");

        foreach (char character in trimmedCode)
        {
            if (!char.IsDigit(character))
                return AccountOperationResult.Failed("The confirmation code must use six numbers.");
        }

        if (!PlayerSession.IsSignedIn ||
            PlayerSession.CurrentAccount.role != AccountRole.Teacher)
        {
            return AccountOperationResult.Failed("Sign in to the Teacher account first.");
        }

        try
        {
            Dictionary<string, object> arguments = new()
            {
                ["code"] = trimmedCode
            };

            TeacherEmailConfirmationResponse response = await CloudCodeService.Instance
                .CallEndpointAsync<TeacherEmailConfirmationResponse>(
                    ConfirmTeacherEmailScriptName,
                    arguments);

            if (response == null)
                return AccountOperationResult.Failed("The confirmation service returned no result.");

            if (!response.success)
                return AccountOperationResult.Failed(response.error);

            return PlayerSession.TryUpdateAccountProfile(response.profile, out string error)
                ? AccountOperationResult.Succeeded()
                : AccountOperationResult.Failed(error);
        }
        catch (Exception exception)
        {
            return AccountOperationResult.Failed(GetFriendlyError(exception));
        }
    }

    public static async Task<AccountOperationResult> ResendTeacherEmailCodeAsync()
    {
        if (!PlayerSession.IsSignedIn ||
            PlayerSession.CurrentAccount.role != AccountRole.Teacher ||
            PlayerSession.CurrentAccount.teacherVerificationStatus !=
                TeacherVerificationStatus.AwaitingEmailConfirmation)
        {
            return AccountOperationResult.Failed(
                "This Teacher account is not awaiting email confirmation.");
        }

        try
        {
            TeacherEmailResendResponse response = await CloudCodeService.Instance
                .CallEndpointAsync<TeacherEmailResendResponse>(
                    ResendTeacherEmailCodeScriptName,
                    new Dictionary<string, object>());

            if (response == null)
                return AccountOperationResult.Failed("The resend service returned no result.");

            return response.success
                ? AccountOperationResult.Succeeded()
                : AccountOperationResult.Failed(response.error);
        }
        catch (Exception exception)
        {
            return AccountOperationResult.Failed(GetFriendlyError(exception));
        }
    }

    public static async Task<AccountOperationResult> RegisterAsync(
        string username,
        string password,
        string inGameName,
        AccountRole requestedRole,
        string fullName,
        string schoolEmail)
    {
        if (!TryValidateRegistration(
                username,
                password,
                inGameName,
                requestedRole,
                fullName,
                schoolEmail,
                out string error))
            return AccountOperationResult.Failed(error);

        try
        {
            await EnsureInitializedAsync();
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(
                username.Trim(),
                password);

            AccountProfile profile = await CreateProtectedProfileAsync(
                username,
                inGameName,
                requestedRole,
                fullName,
                schoolEmail);

            if (!PlayerSession.TryBeginAccountSession(profile, out error))
                return AccountOperationResult.Failed(error);

            return AccountOperationResult.Succeeded();
        }
        catch (Exception exception) when (IsExpectedServiceException(exception))
        {
            return AccountOperationResult.Failed(GetFriendlyError(exception));
        }
    }

    public static async Task<AccountOperationResult> SignInAsync(
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return AccountOperationResult.Failed("Enter your username and password.");

        try
        {
            await EnsureInitializedAsync();
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(
                username.Trim(),
                password);

            AccountProfile profile = await LoadProtectedPlayerProfileAsync();

            if (profile == null)
                profile = await TryMigrateLegacyPlayerProfileAsync();

            if (profile == null)
            {
                throw new InvalidOperationException(
                    "This account does not have a complete The Noli profile.");
            }

            if (!PlayerSession.TryBeginAccountSession(profile, out string error))
                return AccountOperationResult.Failed(error);

            return AccountOperationResult.Succeeded();
        }
        catch (Exception exception) when (IsExpectedServiceException(exception))
        {
            return AccountOperationResult.Failed(GetFriendlyError(exception));
        }
    }

    public static void SignOut()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized &&
            AuthenticationService.Instance.IsSignedIn)
        {
            AuthenticationService.Instance.SignOut(clearCredentials: true);
        }

        PlayerSession.ReturnToGuest();
    }

    private static async Task EnsureInitializedAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized)
            return;

        initializationTask ??= UnityServices.InitializeAsync();

        try
        {
            await initializationTask;
        }
        catch
        {
            initializationTask = null;
            throw;
        }
    }

    private static async Task<AccountProfile> CreateProtectedPlayerProfileAsync(
        string username,
        string inGameName)
    {
        return await CreateProtectedProfileAsync(
            username,
            inGameName,
            AccountRole.Player,
            string.Empty,
            string.Empty);
    }

    private static async Task<AccountProfile> CreateProtectedProfileAsync(
        string username,
        string inGameName,
        AccountRole requestedRole,
        string fullName,
        string schoolEmail)
    {
        Dictionary<string, object> arguments = new()
        {
            ["username"] = username.Trim(),
            ["inGameName"] = inGameName.Trim(),
            ["requestedRole"] = requestedRole.ToString().ToLowerInvariant(),
            ["fullName"] = fullName?.Trim() ?? string.Empty,
            ["schoolEmail"] = schoolEmail?.Trim() ?? string.Empty
        };

        return await CloudCodeService.Instance.CallEndpointAsync<AccountProfile>(
            CreateProfileScriptName,
            arguments);
    }

    private static async Task<AccountProfile> LoadProtectedPlayerProfileAsync()
    {
        HashSet<string> keys = new() { ProtectedProfileKey };
        LoadOptions options = new(new ProtectedReadAccessClassOptions());
        var profileData = await CloudSaveService.Instance.Data.Player.LoadAsync(keys, options);

        return profileData.TryGetValue(ProtectedProfileKey, out var profileItem)
            ? profileItem.Value.GetAs<AccountProfile>()
            : null;
    }

    private static async Task<AccountProfile> TryMigrateLegacyPlayerProfileAsync()
    {
        HashSet<string> keys = new() { UsernameKey, InGameNameKey };
        var profileData = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

        if (!profileData.TryGetValue(UsernameKey, out var usernameItem) ||
            !profileData.TryGetValue(InGameNameKey, out var inGameNameItem))
        {
            return null;
        }

        return await CreateProtectedPlayerProfileAsync(
            usernameItem.Value.GetAs<string>(),
            inGameNameItem.Value.GetAs<string>());
    }

    private static bool TryValidateRegistration(
        string username,
        string password,
        string inGameName,
        AccountRole requestedRole,
        string fullName,
        string schoolEmail,
        out string error)
    {
        if (!TryValidateAccountDetails(username, password, inGameName, out error))
            return false;

        if (requestedRole != AccountRole.Player && requestedRole != AccountRole.Teacher)
        {
            error = "Only Player or Teacher accounts can be registered.";
            return false;
        }

        if (requestedRole == AccountRole.Teacher)
        {
            string trimmedFullName = fullName?.Trim() ?? string.Empty;
            string trimmedEmail = schoolEmail?.Trim() ?? string.Empty;

            if (trimmedFullName.Length == 0 || trimmedEmail.Length == 0)
            {
                error = "Enter your full name and school email.";
                return false;
            }

            int atIndex = trimmedEmail.IndexOf('@');
            int lastDotIndex = trimmedEmail.LastIndexOf('.');
            if (atIndex <= 0 || atIndex != trimmedEmail.LastIndexOf('@') ||
                lastDotIndex <= atIndex + 1 || lastDotIndex == trimmedEmail.Length - 1 ||
                trimmedEmail.Contains(' '))
            {
                error = "Enter a valid email address.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool IsExpectedServiceException(Exception exception)
    {
        return exception is AuthenticationException ||
               exception is RequestFailedException ||
               exception is InvalidOperationException;
    }

    private static string GetFriendlyError(Exception exception)
    {
        string message = exception.Message ?? string.Empty;
        string normalizedMessage = message.ToLowerInvariant();

        if (normalizedMessage.Contains("already exists") ||
            normalizedMessage.Contains("conflict"))
        {
            return "That username is already taken.";
        }

        if (normalizedMessage.Contains("invalid username or password") ||
            normalizedMessage.Contains("invalid credentials") ||
            normalizedMessage.Contains("unauthorized"))
        {
            return "The username or password is incorrect.";
        }

        if (normalizedMessage.Contains("network") ||
            normalizedMessage.Contains("connection") ||
            normalizedMessage.Contains("timed out") ||
            normalizedMessage.Contains("timeout"))
        {
            return "Could not connect. Check your internet connection and try again.";
        }

        if (exception is RequestFailedException requestFailure &&
            !string.IsNullOrWhiteSpace(requestFailure.Message))
        {
            return requestFailure.Message;
        }

        return string.IsNullOrWhiteSpace(message)
            ? "The account request could not be completed."
            : message;
    }
}
