using System;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class SaveFileService
{
    public const string SaveFileName = "autosave.json";

    private readonly string savePath;
    private readonly string backupPath;
    private readonly string temporaryPath;

    public string SavePath => savePath;

    public SaveFileService(string saveDirectory)
    {
        if (string.IsNullOrWhiteSpace(saveDirectory))
            throw new ArgumentException("A save directory is required.", nameof(saveDirectory));

        savePath = Path.Combine(saveDirectory, SaveFileName);
        backupPath = savePath + ".bak";
        temporaryPath = savePath + ".tmp";
    }

    public bool HasValidSave()
    {
        return TryReadSave(savePath, out _, out _) ||
               TryReadSave(backupPath, out _, out _) ||
               TryReadSave(temporaryPath, out _, out _);
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
}
