using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaveSlotView : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text slotTitleText;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private TMP_Text primaryButtonText;
    [SerializeField] private TMP_Text deleteButtonText;

    [Header("Buttons")]
    [SerializeField] private Button primaryButton;
    [SerializeField] private Button deleteButton;

    public bool TryValidate(out string error)
    {
        if (slotTitleText == null || detailsText == null ||
            primaryButtonText == null || deleteButtonText == null ||
            primaryButton == null || deleteButton == null)
        {
            error = $"{name} has unassigned save-slot UI references.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void Bind(
        SaveSlotInfo slot,
        bool isNewGameMode,
        Action primaryAction,
        Action deleteAction)
    {
        slotTitleText.SetText($"Save Slot {slot.SlotNumber}");
        detailsText.SetText(slot.HasSave ? FormatDetails(slot) : "Empty Slot");

        bool canUsePrimary = isNewGameMode ? !slot.HasSave : slot.HasSave;
        primaryButton.interactable = canUsePrimary;
        primaryButtonText.SetText(isNewGameMode
            ? slot.HasSave ? "Occupied" : "Start New Game"
            : slot.HasSave ? "Select Chapter" : "Empty");

        deleteButton.gameObject.SetActive(slot.HasSave);
        deleteButton.interactable = slot.HasSave;
        deleteButtonText.SetText("Delete");

        BindButton(primaryButton, primaryAction);
        BindButton(deleteButton, deleteAction);
    }

    public void BindDeleteConfirmation(Action cancelAction, Action confirmAction)
    {
        detailsText.SetText("Delete this save permanently?\nThis cannot be undone.");
        primaryButton.interactable = true;
        primaryButtonText.SetText("Cancel");
        deleteButton.gameObject.SetActive(true);
        deleteButton.interactable = true;
        deleteButtonText.SetText("Confirm Delete");
        BindButton(primaryButton, cancelAction);
        BindButton(deleteButton, confirmAction);
    }

    public void SetInteractable(bool interactable)
    {
        primaryButton.interactable = interactable && primaryButton.interactable;
        deleteButton.interactable = interactable && deleteButton.interactable;
    }

    private static void BindButton(Button button, Action action)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => action?.Invoke());
    }

    private static string FormatDetails(SaveSlotInfo slot)
    {
        string chapter = FormatChapterId(slot.ActiveChapterId);
        string state = slot.ActiveChapterCompleted
            ? "Completed"
            : string.IsNullOrWhiteSpace(slot.ActiveChapterState)
                ? "In Progress"
                : slot.ActiveChapterState;
        string playTime = FormatPlayTime(slot.TotalPlayTimeSeconds);
        string lastPlayed = FormatTimestamp(slot.LastSavedAtUtc);
        return $"{chapter} - {state}\nPlaytime: {playTime}\nLast played: {lastPlayed}";
    }

    private static string FormatChapterId(string chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId))
            return "Unknown Chapter";

        string display = chapterId.Replace('_', ' ').Trim();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(display);
    }

    private static string FormatPlayTime(double seconds)
    {
        TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0d, seconds));
        int totalHours = (int)duration.TotalHours;
        return $"{totalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static string FormatTimestamp(string timestamp)
    {
        return DateTime.TryParse(
            timestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTime parsed)
            ? parsed.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture)
            : "Unknown";
    }
}
