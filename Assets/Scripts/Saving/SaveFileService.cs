using System;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class SaveFileService
{
    public const int MinimumSlotNumber = 1;
    public const int MaximumSlotNumber = 3;
    public const string SlotSaveFileNameFormat = "save_slot_{0}.json";
    private const string SlotMigrationMarkerFileName = "normal_save_slots.initialized";
    private const string GuestScopeMigrationMarkerFileName = "unscoped_save_slots.migrated";

    // Kept only so saves made before the slot system can be migrated safely.
    public const string SaveFileName = "autosave.json";

    private readonly int slotNumber;
    private readonly string savePath;
    private readonly string backupPath;
    private readonly string temporaryPath;

    public int SlotNumber => slotNumber;
    public string SavePath => savePath;

    public SaveFileService(string saveDirectory)
        : this(saveDirectory, MinimumSlotNumber)
    {
    }

    public SaveFileService(string saveDirectory, int slotNumber)
    {
        if (string.IsNullOrWhiteSpace(saveDirectory))
            throw new ArgumentException("A save directory is required.", nameof(saveDirectory));

        ValidateSlotNumber(slotNumber);
        this.slotNumber = slotNumber;
        savePath = GetSlotSavePath(saveDirectory, slotNumber);
        backupPath = savePath + ".bak";
        temporaryPath = savePath + ".tmp";
    }

    public static string GetSlotSavePath(string saveDirectory, int slotNumber)
    {
        if (string.IsNullOrWhiteSpace(saveDirectory))
            throw new ArgumentException("A save directory is required.", nameof(saveDirectory));

        ValidateSlotNumber(slotNumber);
        return Path.Combine(
            saveDirectory,
            string.Format(SlotSaveFileNameFormat, slotNumber));
    }

    public static bool TryMigrateLegacySaveToSlotOne(
        string saveDirectory,
        out bool migrated,
        out string error)
    {
        migrated = false;

        if (string.IsNullOrWhiteSpace(saveDirectory))
        {
            error = "A save directory is required.";
            return false;
        }

        string migrationMarkerPath = Path.Combine(
            saveDirectory,
            SlotMigrationMarkerFileName);

        if (File.Exists(migrationMarkerPath))
        {
            error = string.Empty;
            return true;
        }

        SaveFileService slotOne = new(saveDirectory, MinimumSlotNumber);

        if (slotOne.HasValidSave())
        {
            return TryWriteMigrationMarker(migrationMarkerPath, out error);
        }

        string legacyPath = Path.Combine(saveDirectory, SaveFileName);
        string legacyBackupPath = legacyPath + ".bak";
        string legacyTemporaryPath = legacyPath + ".tmp";
        GameSaveData legacyData;
        string legacyError;

        if (!TryReadSave(legacyPath, out legacyData, out string primaryError) &&
            !TryReadSave(legacyBackupPath, out legacyData, out string backupError) &&
            !TryReadSave(legacyTemporaryPath, out legacyData, out string temporaryError))
        {
            bool legacyFilesExist = File.Exists(legacyPath) ||
                                    File.Exists(legacyBackupPath) ||
                                    File.Exists(legacyTemporaryPath);

            if (!legacyFilesExist)
                return TryWriteMigrationMarker(migrationMarkerPath, out error);

            legacyError =
                $"Primary legacy save: {primaryError} " +
                $"Backup legacy save: {backupError} " +
                $"Temporary legacy save: {temporaryError}";
            error = $"The previous autosave could not be migrated. {legacyError}";
            return false;
        }

        if (!slotOne.TrySaveFresh(legacyData, out error))
        {
            error = $"The previous autosave was found but could not be copied into Slot 1. {error}";
            return false;
        }

        migrated = true;
        return TryWriteMigrationMarker(migrationMarkerPath, out error);
    }

    public static bool TryMigrateUnscopedSlots(
        string sourceDirectory,
        string guestSaveDirectory,
        out bool migrated,
        out string error)
    {
        migrated = false;

        if (string.IsNullOrWhiteSpace(sourceDirectory) ||
            string.IsNullOrWhiteSpace(guestSaveDirectory))
        {
            error = "Source and Guest save directories are required.";
            return false;
        }

        string migrationMarkerPath = Path.Combine(
            guestSaveDirectory,
            GuestScopeMigrationMarkerFileName);

        if (File.Exists(migrationMarkerPath))
        {
            error = string.Empty;
            return true;
        }

        for (int slotNumber = MinimumSlotNumber;
             slotNumber <= MaximumSlotNumber;
             slotNumber++)
        {
            SaveFileService destination = new(guestSaveDirectory, slotNumber);

            // Never replace a save that already exists in the Guest namespace.
            if (destination.HasValidSave())
                continue;

            SaveFileService source = new(sourceDirectory, slotNumber);

            if (!source.TryLoad(out GameSaveData saveData, out _))
                continue;

            if (!destination.TrySaveFresh(saveData, out error))
            {
                error = $"Save Slot {slotNumber} could not be migrated to Guest storage. {error}";
                return false;
            }

            migrated = true;
        }

        return TryWriteMigrationMarker(migrationMarkerPath, out error);
    }

    public bool HasValidSave()
    {
        return TryReadSave(savePath, out _, out _) ||
               TryReadSave(backupPath, out _, out _) ||
               TryReadSave(temporaryPath, out _, out _);
    }

    public bool HasAnySaveFiles()
    {
        return File.Exists(savePath) ||
               File.Exists(backupPath) ||
               File.Exists(temporaryPath);
    }

    public bool TryMoveToEmptySlot(SaveFileService destination, out string error)
    {
        if (destination == null)
        {
            error = "A destination save slot is required.";
            return false;
        }

        if (string.Equals(savePath, destination.savePath, StringComparison.OrdinalIgnoreCase))
        {
            error = "The source and destination save slots must be different.";
            return false;
        }

        if (destination.HasAnySaveFiles())
        {
            error = "The receiving account slot is not empty.";
            return false;
        }

        if (!TryLoad(out GameSaveData saveData, out string loadError))
        {
            error = $"The Guest save could not be loaded. {loadError}";
            return false;
        }

        if (!destination.TrySaveFresh(saveData, out string saveError))
        {
            error = $"The Guest save could not be written to the account slot. {saveError}";
            return false;
        }

        if (TryDeleteAll(out string deleteError))
        {
            error = string.Empty;
            return true;
        }

        if (!destination.TryDeleteAll(out string rollbackError))
        {
            error =
                $"The Guest save could not be removed after transfer. {deleteError} " +
                $"The account copy also could not be rolled back. {rollbackError}";
            return false;
        }

        error = $"The Guest save could not be removed, so the transfer was cancelled. {deleteError}";
        return false;
    }

    public bool TryLoad(out GameSaveData saveData, out string error)
    {
        if (TryReadSave(savePath, out saveData, out string primaryError))
        {
            error = string.Empty;
            return true;
        }

        if (TryReadSave(backupPath, out saveData, out string backupError))
        {
            error = string.Empty;
            return true;
        }

        if (TryReadSave(temporaryPath, out saveData, out string temporaryError))
        {
            error = string.Empty;
            return true;
        }

        saveData = null;
        error =
            $"Primary save: {primaryError} " +
            $"Backup save: {backupError} " +
            $"Temporary save: {temporaryError}";
        return false;
    }

    public bool TrySave(GameSaveData saveData, out string error)
    {
        return TrySaveInternal(saveData, preservePreviousSave: true, out error);
    }

    public bool TrySaveFresh(GameSaveData saveData, out string error)
    {
        return TrySaveInternal(saveData, preservePreviousSave: false, out error);
    }

    private bool TrySaveInternal(
        GameSaveData saveData,
        bool preservePreviousSave,
        out string error)
    {
        if (!TryValidate(saveData, out error))
            return false;

        saveData.Normalize();

        try
        {
            string directory = Path.GetDirectoryName(savePath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(saveData, true);
            WriteAllTextAndFlush(temporaryPath, json);

            if (!TryReadSave(temporaryPath, out _, out string temporaryError))
            {
                error = $"The temporary save failed validation: {temporaryError}";
                return false;
            }

            if (preservePreviousSave && TryReadSave(savePath, out _, out _))
                File.Copy(savePath, backupPath, true);

            File.Copy(temporaryPath, savePath, true);

            if (!preservePreviousSave)
                DeleteIfPresent(backupPath);

            File.Delete(temporaryPath);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not write the autosave: {exception.Message}";
            return false;
        }
        finally
        {
            TryDeleteTemporaryFile();
        }
    }

    public bool TryDeleteAll(out string error)
    {
        try
        {
            DeleteIfPresent(savePath);
            DeleteIfPresent(backupPath);
            DeleteIfPresent(temporaryPath);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not clear the autosave files: {exception.Message}";
            return false;
        }
    }

    private static bool TryReadSave(string path, out GameSaveData saveData, out string error)
    {
        saveData = null;

        if (!File.Exists(path))
        {
            error = "File does not exist.";
            return false;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "File is empty.";
                return false;
            }

            saveData = JsonUtility.FromJson<GameSaveData>(json);

            if (!TryValidate(saveData, out error))
            {
                saveData = null;
                return false;
            }

            saveData.Normalize();
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not read the file: {exception.Message}";
            saveData = null;
            return false;
        }
    }

    private static bool TryValidate(GameSaveData saveData, out string error)
    {
        if (saveData == null)
        {
            error = "Save data is missing.";
            return false;
        }

        if (saveData.schemaVersion != GameSaveData.CurrentSchemaVersion)
        {
            error = $"Unsupported schema version {saveData.schemaVersion}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void WriteAllTextAndFlush(string path, string contents)
    {
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using StreamWriter writer = new(stream, new UTF8Encoding(false));
        writer.Write(contents);
        writer.Flush();
        stream.Flush(true);
    }

    private static bool TryWriteMigrationMarker(string path, out string error)
    {
        try
        {
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            WriteAllTextAndFlush(path, DateTime.UtcNow.ToString("O"));
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"The save-slot migration marker could not be written: {exception.Message}";
            return false;
        }
    }

    private void TryDeleteTemporaryFile()
    {
        try
        {
            DeleteIfPresent(temporaryPath);
        }
        catch
        {
            // The next save will overwrite a stale temporary file.
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void ValidateSlotNumber(int slotNumber)
    {
        if (slotNumber < MinimumSlotNumber || slotNumber > MaximumSlotNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotNumber),
                slotNumber,
                $"Save slot must be between {MinimumSlotNumber} and {MaximumSlotNumber}.");
        }
    }
}
