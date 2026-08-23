using System;
using System.Collections.Generic;
using UnityEngine;

public static class GuestSaveTransferService
{
    public static IReadOnlyList<int> GetGuestSlots()
    {
        return GetSlotsWithSaves(
            SaveStorageScope.GetGuestSaveDirectory(Application.persistentDataPath));
    }

    public static IReadOnlyList<int> GetEmptyAccountSlots()
    {
        List<int> slots = new();

        if (!PlayerSession.IsSignedIn)
            return slots;

        string directory = SaveStorageScope.GetAccountSaveDirectory(
            Application.persistentDataPath,
            PlayerSession.AccountId);

        for (int slot = SaveFileService.MinimumSlotNumber;
             slot <= SaveFileService.MaximumSlotNumber;
             slot++)
        {
            if (!new SaveFileService(directory, slot).HasAnySaveFiles())
                slots.Add(slot);
        }

        return slots;
    }

    public static bool CanTransferAny()
    {
        return PlayerSession.IsSignedIn &&
               GetGuestSlots().Count > 0 &&
               GetEmptyAccountSlots().Count > 0;
    }

    public static bool TryTransfer(
        int guestSlot,
        int accountSlot,
        out string error)
    {
        if (!PlayerSession.IsSignedIn)
        {
            error = "Sign in before transferring a Guest save.";
            return false;
        }

        try
        {
            string guestDirectory = SaveStorageScope.GetGuestSaveDirectory(
                Application.persistentDataPath);
            string accountDirectory = SaveStorageScope.GetAccountSaveDirectory(
                Application.persistentDataPath,
                PlayerSession.AccountId);

            SaveFileService source = new(guestDirectory, guestSlot);
            SaveFileService destination = new(accountDirectory, accountSlot);
            return source.TryMoveToEmptySlot(destination, out error);
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static IReadOnlyList<int> GetSlotsWithSaves(string directory)
    {
        List<int> slots = new();

        for (int slot = SaveFileService.MinimumSlotNumber;
             slot <= SaveFileService.MaximumSlotNumber;
             slot++)
        {
            if (new SaveFileService(directory, slot).HasValidSave())
                slots.Add(slot);
        }

        return slots;
    }
}
