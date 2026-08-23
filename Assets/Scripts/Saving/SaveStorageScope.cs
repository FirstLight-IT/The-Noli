using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class SaveStorageScope
{
    private const string SavesDirectoryName = "Saves";
    private const string GuestDirectoryName = "Guest";
    private const string AccountsDirectoryName = "Accounts";

    public static string GetGuestSaveDirectory(string persistentDataPath)
    {
        ValidatePersistentDataPath(persistentDataPath);

        return Path.Combine(
            persistentDataPath,
            SavesDirectoryName,
            GuestDirectoryName);
    }

    public static string GetAccountSaveDirectory(
        string persistentDataPath,
        string accountId)
    {
        ValidatePersistentDataPath(persistentDataPath);

        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("A permanent Account ID is required.", nameof(accountId));

        return Path.Combine(
            persistentDataPath,
            SavesDirectoryName,
            AccountsDirectoryName,
            GetAccountStorageKey(accountId));
    }

    public static string GetCurrentSaveDirectory(string persistentDataPath)
    {
        return PlayerSession.IsSignedIn
            ? GetAccountSaveDirectory(persistentDataPath, PlayerSession.AccountId)
            : GetGuestSaveDirectory(persistentDataPath);
    }

    public static string GetCurrentOwnerKey()
    {
        return PlayerSession.IsSignedIn
            ? GetAccountStorageKey(PlayerSession.AccountId)
            : GuestDirectoryName;
    }

    private static string GetAccountStorageKey(string accountId)
    {
        string normalizedAccountId = accountId.Trim();

        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedAccountId));
        StringBuilder result = new(hash.Length * 2);

        foreach (byte value in hash)
            result.Append(value.ToString("x2"));

        return result.ToString();
    }

    private static void ValidatePersistentDataPath(string persistentDataPath)
    {
        if (string.IsNullOrWhiteSpace(persistentDataPath))
        {
            throw new ArgumentException(
                "A persistent data path is required.",
                nameof(persistentDataPath));
        }
    }
}
