using System;
using UnityEngine;

public static class PlayerSession
{
    public static event Action Changed;
    public static event Action ProfileChanged;

    public static bool IsSignedIn => CurrentAccount != null;
    public static bool IsGuest => !IsSignedIn;
    public static AccountProfile CurrentAccount { get; private set; }
    public static string AccountId => CurrentAccount?.accountId ?? string.Empty;
    public static AccountRole EffectiveRole =>
        CurrentAccount?.EffectiveRole ?? AccountRole.Player;
    public static bool CanUseOnlineAccountFeatures => IsSignedIn;
    public static bool CanSubmitGlobalAnalytics => IsSignedIn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        CurrentAccount = null;
        Changed = null;
        ProfileChanged = null;
    }

    public static bool TryBeginAccountSession(AccountProfile profile, out string error)
    {
        if (!TryValidate(profile, out error))
            return false;

        CurrentAccount = profile.Copy();
#if UNITY_EDITOR
        Debug.Log($"[Developer Only] Signed-in Account ID: {CurrentAccount.accountId}");
#endif
        Changed?.Invoke();
        return true;
    }

    public static void ReturnToGuest()
    {
        if (CurrentAccount == null)
            return;

        CurrentAccount = null;
        Changed?.Invoke();
    }

    public static bool TryUpdateAccountProfile(AccountProfile profile, out string error)
    {
        if (!TryValidate(profile, out error))
            return false;

        if (!IsSignedIn || !string.Equals(
                CurrentAccount.accountId,
                profile.accountId,
                StringComparison.Ordinal))
        {
            error = "The updated profile does not belong to the signed-in account.";
            return false;
        }

        CurrentAccount = profile.Copy();
        ProfileChanged?.Invoke();
        return true;
    }

    private static bool TryValidate(AccountProfile profile, out string error)
    {
        if (profile == null)
        {
            error = "An account profile is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(profile.accountId))
        {
            error = "A permanent Account ID is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(profile.username))
        {
            error = "An account username is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(profile.inGameName))
        {
            error = "An in-game name is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
