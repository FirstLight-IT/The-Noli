using System.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AccountMenuController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text sessionStatusText;

    [Header("Signed Out")]
    [SerializeField] private GameObject signedOutRoot;
    [SerializeField] private GameObject accountDetailsRoot;
    [SerializeField] private GameObject teacherDetailsRoot;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField inGameNameInput;
    [SerializeField] private TMP_Dropdown roleDropdown;
    [SerializeField] private Button primaryButton;
    [SerializeField] private TMP_Text primaryButtonText;
    [SerializeField] private TMP_InputField fullNameInput;
    [SerializeField] private TMP_InputField schoolEmailInput;
    [SerializeField] private Button teacherBackButton;
    [SerializeField] private Button createTeacherAccountButton;
    [SerializeField] private Button switchModeButton;
    [SerializeField] private TMP_Text switchModeButtonText;

    [Header("Signed In")]
    [SerializeField] private GameObject signedInRoot;
    [SerializeField] private GameObject accountSummaryRoot;
    [SerializeField] private TMP_Text accountText;
    [SerializeField] private TMP_Text accountDetailsText;
    [SerializeField] private Button openAccountSettingsButton;
    [SerializeField] private Button changeInGameNameButton;
    [SerializeField] private Button changePasswordButton;
    [SerializeField] private Button transferGuestSaveButton;
    [SerializeField] private Button confirmTeacherEmailButton;
    [SerializeField] private Button openTeacherRequestsButton;
    [SerializeField] private Button signOutButton;

    [Header("Account Settings")]
    [SerializeField] private GameObject accountSettingsRoot;
    [SerializeField] private Button accountSettingsBackButton;

    [Header("Change In-Game Name")]
    [SerializeField] private GameObject changeIgnRoot;
    [SerializeField] private TMP_InputField newIgnInput;
    [SerializeField] private Button saveIgnButton;
    [SerializeField] private Button ignBackButton;

    [Header("Change Password")]
    [SerializeField] private GameObject changePasswordRoot;
    [SerializeField] private TMP_InputField currentPasswordInput;
    [SerializeField] private TMP_InputField newPasswordInput;
    [SerializeField] private TMP_InputField confirmPasswordInput;
    [SerializeField] private Button savePasswordButton;
    [SerializeField] private Button passwordBackButton;

    [Header("Transfer Guest Save")]
    [SerializeField] private GameObject transferGuestSaveRoot;
    [SerializeField] private TMP_Dropdown guestSaveDropdown;
    [SerializeField] private TMP_Dropdown accountSlotDropdown;
    [SerializeField] private Button confirmTransferButton;
    [SerializeField] private TMP_Text confirmTransferButtonText;
    [SerializeField] private Button transferBackButton;

    [Header("Confirm Teacher Email")]
    [SerializeField] private GameObject confirmTeacherEmailRoot;
    [SerializeField] private TMP_InputField teacherEmailCodeInput;
    [SerializeField] private Button submitTeacherEmailCodeButton;
    [SerializeField] private Button resendTeacherEmailCodeButton;
    [SerializeField] private Button teacherEmailBackButton;

    private bool isRegisterMode;
    private bool isBusy;
    private bool transferConfirmationPending;
    private readonly List<int> guestTransferSlots = new();
    private readonly List<int> accountTransferSlots = new();

    private void Awake()
    {
        if (!TryValidate())
        {
            enabled = false;
            return;
        }

        openButton.onClick.AddListener(Show);
        closeButton.onClick.AddListener(Hide);
        primaryButton.onClick.AddListener(Submit);
        roleDropdown.onValueChanged.AddListener(RoleChanged);
        teacherBackButton.onClick.AddListener(ShowAccountDetailsPage);
        createTeacherAccountButton.onClick.AddListener(CreateTeacherAccount);
        switchModeButton.onClick.AddListener(SwitchMode);
        openAccountSettingsButton.onClick.AddListener(ShowAccountSettingsPage);
        accountSettingsBackButton.onClick.AddListener(ShowAccountSummaryPage);
        changeInGameNameButton.onClick.AddListener(ShowChangeIgnPage);
        changePasswordButton.onClick.AddListener(ShowChangePasswordPage);
        saveIgnButton.onClick.AddListener(SaveInGameName);
        ignBackButton.onClick.AddListener(ShowAccountSettingsPage);
        savePasswordButton.onClick.AddListener(SavePassword);
        passwordBackButton.onClick.AddListener(ShowAccountSettingsPage);
        transferGuestSaveButton.onClick.AddListener(ShowTransferGuestSavePage);
        guestSaveDropdown.onValueChanged.AddListener(TransferSelectionChanged);
        accountSlotDropdown.onValueChanged.AddListener(TransferSelectionChanged);
        confirmTransferButton.onClick.AddListener(TransferGuestSave);
        transferBackButton.onClick.AddListener(ShowAccountSummaryPage);
        confirmTeacherEmailButton.onClick.AddListener(ShowConfirmTeacherEmailPage);
        submitTeacherEmailCodeButton.onClick.AddListener(SubmitTeacherEmailCode);
        resendTeacherEmailCodeButton.onClick.AddListener(ResendTeacherEmailCode);
        teacherEmailBackButton.onClick.AddListener(ShowAccountSummaryPage);
        if (openTeacherRequestsButton != null)
            openTeacherRequestsButton.onClick.AddListener(OpenLibrarianDashboard);
        signOutButton.onClick.AddListener(SignOut);
        panelRoot.SetActive(false);
        SetRegisterMode(false);
        RefreshState();
    }

    private void OnEnable()
    {
        PlayerSession.Changed += RefreshState;
        PlayerSession.ProfileChanged += RefreshState;
    }

    private async void Start()
    {
        SetBusy(true);
        AccountOperationResult result =
            await AccountAuthenticationService.RestoreCachedSessionAsync();
        SetBusy(false);

        if (!result.Success)
            Debug.LogWarning($"Account session was not restored: {result.Error}", this);

        RefreshState();
    }

    private void OnDisable()
    {
        PlayerSession.Changed -= RefreshState;
        PlayerSession.ProfileChanged -= RefreshState;
    }

    private void OnDestroy()
    {
        if (openButton != null)
            openButton.onClick.RemoveListener(Show);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);

        if (primaryButton != null)
            primaryButton.onClick.RemoveListener(Submit);

        if (roleDropdown != null)
            roleDropdown.onValueChanged.RemoveListener(RoleChanged);

        if (teacherBackButton != null)
            teacherBackButton.onClick.RemoveListener(ShowAccountDetailsPage);

        if (createTeacherAccountButton != null)
            createTeacherAccountButton.onClick.RemoveListener(CreateTeacherAccount);

        if (switchModeButton != null)
            switchModeButton.onClick.RemoveListener(SwitchMode);

        if (changeInGameNameButton != null)
            changeInGameNameButton.onClick.RemoveListener(ShowChangeIgnPage);

        if (openAccountSettingsButton != null)
            openAccountSettingsButton.onClick.RemoveListener(ShowAccountSettingsPage);

        if (accountSettingsBackButton != null)
            accountSettingsBackButton.onClick.RemoveListener(ShowAccountSummaryPage);

        if (changePasswordButton != null)
            changePasswordButton.onClick.RemoveListener(ShowChangePasswordPage);

        if (saveIgnButton != null)
            saveIgnButton.onClick.RemoveListener(SaveInGameName);

        if (ignBackButton != null)
            ignBackButton.onClick.RemoveListener(ShowAccountSettingsPage);

        if (savePasswordButton != null)
            savePasswordButton.onClick.RemoveListener(SavePassword);

        if (passwordBackButton != null)
            passwordBackButton.onClick.RemoveListener(ShowAccountSettingsPage);

        if (transferGuestSaveButton != null)
            transferGuestSaveButton.onClick.RemoveListener(ShowTransferGuestSavePage);

        if (guestSaveDropdown != null)
            guestSaveDropdown.onValueChanged.RemoveListener(TransferSelectionChanged);

        if (accountSlotDropdown != null)
            accountSlotDropdown.onValueChanged.RemoveListener(TransferSelectionChanged);

        if (confirmTransferButton != null)
            confirmTransferButton.onClick.RemoveListener(TransferGuestSave);

        if (transferBackButton != null)
            transferBackButton.onClick.RemoveListener(ShowAccountSummaryPage);

        if (confirmTeacherEmailButton != null)
            confirmTeacherEmailButton.onClick.RemoveListener(ShowConfirmTeacherEmailPage);

        if (submitTeacherEmailCodeButton != null)
            submitTeacherEmailCodeButton.onClick.RemoveListener(SubmitTeacherEmailCode);

        if (resendTeacherEmailCodeButton != null)
            resendTeacherEmailCodeButton.onClick.RemoveListener(ResendTeacherEmailCode);

        if (teacherEmailBackButton != null)
            teacherEmailBackButton.onClick.RemoveListener(ShowAccountSummaryPage);

        if (openTeacherRequestsButton != null)
            openTeacherRequestsButton.onClick.RemoveListener(OpenLibrarianDashboard);

        if (signOutButton != null)
            signOutButton.onClick.RemoveListener(SignOut);
    }

    public void Show()
    {
        statusText.SetText(string.Empty);
        panelRoot.SetActive(true);
        RefreshState();
    }

    public void Hide()
    {
        if (!isBusy)
            panelRoot.SetActive(false);
    }

    private void SwitchMode()
    {
        if (!isBusy)
            SetRegisterMode(!isRegisterMode);
    }

    private void SetRegisterMode(bool registerMode)
    {
        isRegisterMode = registerMode;
        titleText.SetText(isRegisterMode ? "Create Account" : "Sign In");
        switchModeButtonText.SetText(isRegisterMode
            ? "Already have an account? Sign In"
            : "Need an account? Register");
        inGameNameInput.gameObject.SetActive(isRegisterMode);
        roleDropdown.gameObject.SetActive(isRegisterMode);
        ShowAccountDetailsPage();
        statusText.SetText(string.Empty);
    }

    private async void Submit()
    {
        if (isRegisterMode && SelectedRole == AccountRole.Teacher)
        {
            if (!AccountAuthenticationService.TryValidateAccountDetails(
                    usernameInput.text,
                    passwordInput.text,
                    inGameNameInput.text,
                    out string error))
            {
                statusText.SetText(error);
                return;
            }

            ShowTeacherDetailsPage();
            return;
        }

        Task<AccountOperationResult> operation = isRegisterMode
            ? AccountAuthenticationService.RegisterAsync(
                usernameInput.text,
                passwordInput.text,
                inGameNameInput.text,
                AccountRole.Player,
                string.Empty,
                string.Empty)
            : AccountAuthenticationService.SignInAsync(usernameInput.text, passwordInput.text);

        await RunAccountOperation(operation);
    }

    private async void CreateTeacherAccount()
    {
        await RunAccountOperation(AccountAuthenticationService.RegisterAsync(
            usernameInput.text,
            passwordInput.text,
            inGameNameInput.text,
            AccountRole.Teacher,
            fullNameInput.text,
            schoolEmailInput.text));
    }

    private AccountRole SelectedRole => roleDropdown.value == 1
        ? AccountRole.Teacher
        : AccountRole.Player;

    private void RoleChanged(int _)
    {
        RefreshPrimaryButtonText();
    }

    private void ShowAccountDetailsPage()
    {
        accountDetailsRoot.SetActive(true);
        teacherDetailsRoot.SetActive(false);
        switchModeButton.gameObject.SetActive(true);
        titleText.SetText(isRegisterMode ? "Create Account" : "Sign In");
        RefreshPrimaryButtonText();
        statusText.SetText(string.Empty);
    }

    private void ShowTeacherDetailsPage()
    {
        accountDetailsRoot.SetActive(false);
        teacherDetailsRoot.SetActive(true);
        switchModeButton.gameObject.SetActive(false);
        titleText.SetText("Teacher Information");
        statusText.SetText(string.Empty);
    }

    private void RefreshPrimaryButtonText()
    {
        string label = !isRegisterMode
            ? "Sign In"
            : SelectedRole == AccountRole.Teacher ? "Next" : "Create Account";
        primaryButtonText.SetText(label);
    }

    private void SignOut()
    {
        AccountAuthenticationService.SignOut();
        passwordInput.text = string.Empty;
        statusText.SetText("Signed out. You are playing as Guest.");
    }

    private void ShowAccountSummaryPage()
    {
        accountSummaryRoot.SetActive(true);
        accountSettingsRoot.SetActive(false);
        changeIgnRoot.SetActive(false);
        changePasswordRoot.SetActive(false);
        transferGuestSaveRoot.SetActive(false);
        confirmTeacherEmailRoot.SetActive(false);
        titleText.SetText("Account");
        transferGuestSaveButton.gameObject.SetActive(
            PlayerSession.IsSignedIn && GuestSaveTransferService.GetGuestSlots().Count > 0);
        statusText.SetText(string.Empty);
    }

    private void ShowAccountSettingsPage()
    {
        accountSummaryRoot.SetActive(false);
        accountSettingsRoot.SetActive(true);
        changeIgnRoot.SetActive(false);
        changePasswordRoot.SetActive(false);
        transferGuestSaveRoot.SetActive(false);
        confirmTeacherEmailRoot.SetActive(false);
        titleText.SetText("Account Settings");
        statusText.SetText(string.Empty);
    }

    private void ShowChangeIgnPage()
    {
        accountSummaryRoot.SetActive(false);
        accountSettingsRoot.SetActive(false);
        changeIgnRoot.SetActive(true);
        changePasswordRoot.SetActive(false);
        transferGuestSaveRoot.SetActive(false);
        confirmTeacherEmailRoot.SetActive(false);
        newIgnInput.text = PlayerSession.CurrentAccount?.inGameName ?? string.Empty;
        titleText.SetText("Change In-Game Name");
        statusText.SetText(string.Empty);
    }

    private void ShowChangePasswordPage()
    {
        accountSummaryRoot.SetActive(false);
        accountSettingsRoot.SetActive(false);
        changeIgnRoot.SetActive(false);
        changePasswordRoot.SetActive(true);
        transferGuestSaveRoot.SetActive(false);
        confirmTeacherEmailRoot.SetActive(false);
        currentPasswordInput.text = string.Empty;
        newPasswordInput.text = string.Empty;
        confirmPasswordInput.text = string.Empty;
        titleText.SetText("Change Password");
        statusText.SetText(string.Empty);
    }

    private async void SaveInGameName()
    {
        SetBusy(true);
        statusText.SetText("Saving...");
        AccountOperationResult result =
            await AccountAuthenticationService.UpdateInGameNameAsync(newIgnInput.text);
        SetBusy(false);

        if (!result.Success)
        {
            statusText.SetText(result.Error);
            return;
        }

        ShowAccountSettingsPage();
        statusText.SetText("In-game name updated.");
    }

    private async void SavePassword()
    {
        if (newPasswordInput.text != confirmPasswordInput.text)
        {
            statusText.SetText("The new passwords do not match.");
            return;
        }

        SetBusy(true);
        statusText.SetText("Saving...");
        AccountOperationResult result = await AccountAuthenticationService.UpdatePasswordAsync(
            currentPasswordInput.text,
            newPasswordInput.text);
        SetBusy(false);

        if (!result.Success)
        {
            statusText.SetText(result.Error);
            return;
        }

        currentPasswordInput.text = string.Empty;
        newPasswordInput.text = string.Empty;
        confirmPasswordInput.text = string.Empty;
        ShowAccountSettingsPage();
        statusText.SetText("Password updated.");
    }

    private void ShowTransferGuestSavePage()
    {
        accountSummaryRoot.SetActive(false);
        accountSettingsRoot.SetActive(false);
        changeIgnRoot.SetActive(false);
        changePasswordRoot.SetActive(false);
        transferGuestSaveRoot.SetActive(true);
        confirmTeacherEmailRoot.SetActive(false);
        titleText.SetText("Transfer Guest Save");
        statusText.SetText(string.Empty);
        RefreshTransferOptions();
    }

    private void ShowConfirmTeacherEmailPage()
    {
        accountSummaryRoot.SetActive(false);
        accountSettingsRoot.SetActive(false);
        changeIgnRoot.SetActive(false);
        changePasswordRoot.SetActive(false);
        transferGuestSaveRoot.SetActive(false);
        confirmTeacherEmailRoot.SetActive(true);
        teacherEmailCodeInput.text = string.Empty;
        titleText.SetText("Confirm Teacher Email");
        statusText.SetText("Enter the six-digit code sent to your email.");
    }

    private async void SubmitTeacherEmailCode()
    {
        SetBusy(true);
        statusText.SetText("Confirming...");
        AccountOperationResult result =
            await AccountAuthenticationService.ConfirmTeacherEmailAsync(
                teacherEmailCodeInput.text);
        SetBusy(false);

        if (!result.Success)
        {
            statusText.SetText(result.Error);
            return;
        }

        teacherEmailCodeInput.text = string.Empty;
        ShowAccountSummaryPage();
        statusText.SetText("Email confirmed. Teacher access is now active.");
    }

    private async void ResendTeacherEmailCode()
    {
        SetBusy(true);
        statusText.SetText("Sending a new code...");
        AccountOperationResult result =
            await AccountAuthenticationService.ResendTeacherEmailCodeAsync();
        SetBusy(false);

        statusText.SetText(result.Success
            ? "A new confirmation code was sent. The previous code no longer works."
            : result.Error);
    }

    private void RefreshTransferOptions()
    {
        guestTransferSlots.Clear();
        guestTransferSlots.AddRange(GuestSaveTransferService.GetGuestSlots());
        accountTransferSlots.Clear();
        accountTransferSlots.AddRange(GuestSaveTransferService.GetEmptyAccountSlots());

        guestSaveDropdown.ClearOptions();
        accountSlotDropdown.ClearOptions();

        List<string> guestLabels = new();
        foreach (int slot in guestTransferSlots)
            guestLabels.Add($"Guest Slot {slot}");

        List<string> accountLabels = new();
        foreach (int slot in accountTransferSlots)
            accountLabels.Add($"Empty Account Slot {slot}");

        guestSaveDropdown.AddOptions(guestLabels.Count > 0
            ? guestLabels
            : new List<string> { "No Guest saves" });
        accountSlotDropdown.AddOptions(accountLabels.Count > 0
            ? accountLabels
            : new List<string> { "No empty account slots" });
        guestSaveDropdown.SetValueWithoutNotify(0);
        accountSlotDropdown.SetValueWithoutNotify(0);
        guestSaveDropdown.RefreshShownValue();
        accountSlotDropdown.RefreshShownValue();
        ResetTransferConfirmation();
    }

    private void TransferSelectionChanged(int _)
    {
        ResetTransferConfirmation();
        statusText.SetText(string.Empty);
    }

    private void ResetTransferConfirmation()
    {
        transferConfirmationPending = false;
        confirmTransferButtonText.SetText("Transfer Save");
        confirmTransferButton.interactable = !isBusy &&
                                             guestTransferSlots.Count > 0 &&
                                             accountTransferSlots.Count > 0;
    }

    private void TransferGuestSave()
    {
        if (guestTransferSlots.Count == 0 || accountTransferSlots.Count == 0)
            return;

        if (!transferConfirmationPending)
        {
            transferConfirmationPending = true;
            confirmTransferButtonText.SetText("Confirm Transfer");
            statusText.SetText(
                "This permanently removes the Guest save. Press Confirm Transfer to continue.");
            return;
        }

        int guestSlot = guestTransferSlots[guestSaveDropdown.value];
        int accountSlot = accountTransferSlots[accountSlotDropdown.value];
        SetBusy(true);
        bool success = GuestSaveTransferService.TryTransfer(
            guestSlot,
            accountSlot,
            out string error);
        SetBusy(false);

        if (!success)
        {
            statusText.SetText(error);
            RefreshTransferOptions();
            return;
        }

        Object.FindAnyObjectByType<MainMenuController>()?.RefreshButtons();
        RefreshTransferOptions();
        statusText.SetText(
            $"Guest Slot {guestSlot} moved to Account Slot {accountSlot}.");
    }

    private void OpenLibrarianDashboard()
    {
        if (!isBusy && PlayerSession.EffectiveRole == AccountRole.Librarian)
            SceneManager.LoadScene("LibrarianDashboard");
    }

    private async Task RunAccountOperation(Task<AccountOperationResult> operation)
    {
        SetBusy(true);
        statusText.SetText("Connecting...");
        AccountOperationResult result = await operation;
        SetBusy(false);

        if (!result.Success)
        {
            statusText.SetText(result.Error);
            return;
        }

        passwordInput.text = string.Empty;
        statusText.SetText("Account connected successfully.");
        RefreshState();
    }

    private void RefreshState()
    {
        if (signedOutRoot == null || signedInRoot == null)
            return;

        bool signedIn = PlayerSession.IsSignedIn;
        signedOutRoot.SetActive(!signedIn);
        signedInRoot.SetActive(signedIn);
        titleText.SetText(signedIn
            ? "Account"
            : isRegisterMode ? "Create Account" : "Sign In");
        sessionStatusText.SetText(signedIn
            ? $"Playing as {PlayerSession.CurrentAccount.inGameName}"
            : "Playing as Guest");

        if (signedIn)
        {
            AccountProfile profile = PlayerSession.CurrentAccount;
            accountText.SetText($"@{profile.username}");

            accountDetailsText.SetText(profile.role switch
            {
                AccountRole.Teacher =>
                    $"Requested role: Teacher\n" +
                    $"Verification: {FormatStatus(profile.teacherVerificationStatus)}\n" +
                    $"Current access: {profile.EffectiveRole}",
                AccountRole.Librarian => "Role: Librarian\nCurrent access: Librarian",
                _ => "Role: Player"
            });

            transferGuestSaveButton.gameObject.SetActive(
                GuestSaveTransferService.GetGuestSlots().Count > 0);
            confirmTeacherEmailButton.gameObject.SetActive(
                profile.role == AccountRole.Teacher &&
                profile.teacherVerificationStatus ==
                    TeacherVerificationStatus.AwaitingEmailConfirmation);
            if (openTeacherRequestsButton != null)
            {
                openTeacherRequestsButton.gameObject.SetActive(
                    profile.EffectiveRole == AccountRole.Librarian);
                if (profile.EffectiveRole == AccountRole.Librarian)
                {
                    openTeacherRequestsButton.GetComponentInChildren<TMP_Text>()
                        ?.SetText("Librarian Dashboard");
                }
            }
        }
    }

    private static string FormatStatus(TeacherVerificationStatus status)
    {
        return status switch
        {
            TeacherVerificationStatus.Pending => "Pending librarian approval",
            TeacherVerificationStatus.AwaitingEmailConfirmation =>
                "Awaiting email confirmation",
            TeacherVerificationStatus.Verified => "Verified",
            TeacherVerificationStatus.Rejected => "Rejected",
            _ => "Not applicable"
        };
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        primaryButton.interactable = !busy;
        teacherBackButton.interactable = !busy;
        createTeacherAccountButton.interactable = !busy;
        switchModeButton.interactable = !busy;
        openAccountSettingsButton.interactable = !busy;
        accountSettingsBackButton.interactable = !busy;
        changeInGameNameButton.interactable = !busy;
        changePasswordButton.interactable = !busy;
        saveIgnButton.interactable = !busy;
        ignBackButton.interactable = !busy;
        savePasswordButton.interactable = !busy;
        passwordBackButton.interactable = !busy;
        transferGuestSaveButton.interactable = !busy;
        guestSaveDropdown.interactable = !busy;
        accountSlotDropdown.interactable = !busy;
        confirmTransferButton.interactable = !busy &&
                                             guestTransferSlots.Count > 0 &&
                                             accountTransferSlots.Count > 0;
        transferBackButton.interactable = !busy;
        confirmTeacherEmailButton.interactable = !busy;
        teacherEmailCodeInput.interactable = !busy;
        submitTeacherEmailCodeButton.interactable = !busy;
        resendTeacherEmailCodeButton.interactable = !busy;
        teacherEmailBackButton.interactable = !busy;
        if (openTeacherRequestsButton != null)
            openTeacherRequestsButton.interactable = !busy;
        signOutButton.interactable = !busy;
        closeButton.interactable = !busy;
    }

    private bool TryValidate()
    {
        bool valid = panelRoot != null && openButton != null && closeButton != null &&
                     titleText != null && statusText != null && sessionStatusText != null &&
                     signedOutRoot != null && accountDetailsRoot != null &&
                     teacherDetailsRoot != null &&
                     usernameInput != null && passwordInput != null &&
                     inGameNameInput != null && roleDropdown != null &&
                     primaryButton != null &&
                     primaryButtonText != null && switchModeButton != null &&
                     fullNameInput != null && schoolEmailInput != null &&
                     teacherBackButton != null && createTeacherAccountButton != null &&
                     switchModeButtonText != null && signedInRoot != null &&
                     accountSummaryRoot != null && accountText != null &&
                     accountDetailsText != null && openAccountSettingsButton != null &&
                     accountSettingsRoot != null && accountSettingsBackButton != null &&
                     changeInGameNameButton != null &&
                     changePasswordButton != null && transferGuestSaveButton != null &&
                     changeIgnRoot != null &&
                     newIgnInput != null && saveIgnButton != null &&
                     ignBackButton != null && changePasswordRoot != null &&
                     currentPasswordInput != null && newPasswordInput != null &&
                     confirmPasswordInput != null && savePasswordButton != null &&
                     passwordBackButton != null && transferGuestSaveRoot != null &&
                     guestSaveDropdown != null && accountSlotDropdown != null &&
                     confirmTransferButton != null && confirmTransferButtonText != null &&
                     transferBackButton != null && confirmTeacherEmailButton != null &&
                     confirmTeacherEmailRoot != null && teacherEmailCodeInput != null &&
                     submitTeacherEmailCodeButton != null &&
                     resendTeacherEmailCodeButton != null && teacherEmailBackButton != null &&
                     signOutButton != null;

        if (!valid)
            Debug.LogError("Account Menu Controller has unassigned UI references.", this);

        return valid;
    }
}
