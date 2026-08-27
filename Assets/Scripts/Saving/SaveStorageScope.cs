using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class SaveStorageScope
{
    private const string SavesDirectoryName = "Saves";
    private const string GuestDirectoryName = "Guest";
    private const string AccountsDirectoryName = "Accounts";
    private const string ClassroomsDirectoryName = "Classrooms";

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

    public static string GetAccountClassroomsDirectory(
        string persistentDataPath,
        string accountId)
    {
        return Path.Combine(
            GetAccountSaveDirectory(persistentDataPath, accountId),
            ClassroomsDirectoryName);
    }

    public static string GetClassroomSaveDirectory(
        string persistentDataPath,
        string accountId,
        string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            throw new ArgumentException("A classroom ID is required.", nameof(roomId));

        return Path.Combine(
            GetAccountClassroomsDirectory(persistentDataPath, accountId),
            GetStorageKey(roomId));
    }

    private static string GetAccountStorageKey(string accountId)
    {
        string normalizedAccountId = accountId.Trim();

        return GetStorageKey(normalizedAccountId);
    }

    private static string GetStorageKey(string value)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value.Trim()));
        StringBuilder result = new(hash.Length * 2);

        foreach (byte byteValue in hash)
            result.Append(byteValue.ToString("x2"));

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
