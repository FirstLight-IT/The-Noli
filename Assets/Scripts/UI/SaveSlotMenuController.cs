using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaveSlotMenuController : MonoBehaviour
{
    private enum MenuMode
    {
        NewGame,
        LoadGame
    }

    [Header("Menu")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button closeButton;

    [Header("Connections")]
    [SerializeField] private MainMenuController mainMenuController;
    [SerializeField] private ChapterSelectionMenuController chapterSelectionMenuController;
    [Tooltip("Assign Save Slot 1, Save Slot 2, then Save Slot 3 in this exact order.")]
    [SerializeField] private SaveSlotView[] slotViews = new SaveSlotView[SaveGameManager.SaveSlotCount];

    private MenuMode mode;
    private int pendingDeleteSlot = -1;
    private int pendingAnalyticsSlot = -1;
    private bool isBusy;
    private GlobalAnalyticsSubmissionStatus analyticsStatus;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void ShowNewGameSlots()
    {
        Show(MenuMode.NewGame);
    }

    public void ShowLoadGameSlots()
    {
        Show(MenuMode.LoadGame);
    }

    public void Hide()
    {
        if (isBusy)
            return;

        pendingDeleteSlot = -1;
        SetMessage(string.Empty);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private async void Show(MenuMode requestedMode)
    {
        if (!TryValidate(out string error))
        {
            Debug.LogError(error, this);
            return;
        }

        mode = requestedMode;
        pendingDeleteSlot = -1;
        pendingAnalyticsSlot = -1;
        isBusy = false;
        analyticsStatus = null;
        closeButton.interactable = true;
        panelRoot.SetActive(true);
        titleText.SetText(mode == MenuMode.NewGame ? "Choose an Empty Slot" : "Load Game");
        SetMessage(string.Empty);
        RefreshSlots();

        if (PlayerSession.CanSubmitGlobalAnalytics &&
            PlayerSession.EffectiveRole != AccountRole.Librarian)
        {
            await LoadAnalyticsStatusAsync();
        }
    }

    private void RefreshSlots()
    {
        bool isNewGameMode = mode == MenuMode.NewGame;

        for (int index = 0; index < SaveGameManager.SaveSlotCount; index++)
        {
            int slotNumber = index + SaveFileService.MinimumSlotNumber;
            SaveSlotView slotView = slotViews[index];
            SaveSlotInfo slotInfo = SaveGameManager.GetSaveSlotInfo(slotNumber);
            slotView.Bind(
                slotInfo,
                isNewGameMode,
                () => HandlePrimaryAction(slotNumber, slotInfo),
                () => RequestDelete(slotNumber));

            if (pendingDeleteSlot == slotNumber)
            {
                slotView.BindDeleteConfirmation(
                    CancelDelete,
                    () => ConfirmDelete(slotNumber));
            }
            else
            {
                BindAnalyticsAction(slotView, slotInfo);
            }
        }
    }

    private async Task LoadAnalyticsStatusAsync()
    {
        GlobalAnalyticsStatusResult result =
            await GlobalAnalyticsSubmissionService.GetStatusAsync();

        if (panelRoot == null || !panelRoot.activeSelf)
            return;

        if (!result.Success)
        {
            SetMessage(result.Error);
            return;
        }

        analyticsStatus = result.Status;
        RefreshSlots();
    }

    private void BindAnalyticsAction(SaveSlotView slotView, SaveSlotInfo slotInfo)
    {
        if (analyticsStatus == null || !slotInfo.HasSave ||
            slotInfo.OfficialAnalyticsChapterIds.Count == 0)
        {
            slotView.BindAnalytics(false, false, string.Empty, null);
            return;
        }

        if (!analyticsStatus.hasOfficialPlaythrough)
        {
            bool confirming = pendingAnalyticsSlot == slotInfo.SlotNumber;
            slotView.BindAnalytics(
                true,
                true,
                confirming ? "Confirm Official Save" : "Use for Global Analytics",
                () => RequestAnalyticsSubmission(slotInfo.SlotNumber));
            return;
        }

        if (!string.Equals(
                analyticsStatus.officialPlaythroughId,
                slotInfo.PlaythroughId,
                StringComparison.Ordinal))
        {
            slotView.BindAnalytics(true, false, "Another Save Is Official", null);
            return;
        }

        HashSet<string> accepted = new(
            analyticsStatus.acceptedChapterIds,
            StringComparer.Ordinal);
        bool hasNewChapter = false;

        foreach (string chapterId in slotInfo.OfficialAnalyticsChapterIds)
        {
            if (!accepted.Contains(chapterId))
            {
                hasNewChapter = true;
                break;
            }
        }

        slotView.BindAnalytics(
            true,
            hasNewChapter,
            hasNewChapter ? "Upload New Chapter Results" : "Analytics Up to Date",
            hasNewChapter ? () => SubmitAnalytics(slotInfo.SlotNumber) : null);
    }

    private void RequestAnalyticsSubmission(int slotNumber)
    {
        if (isBusy)
            return;

        if (pendingAnalyticsSlot != slotNumber)
        {
            pendingAnalyticsSlot = slotNumber;
            SetMessage(
                $"Save Slot {slotNumber} will permanently become this account's official " +
                "Global Analytics playthrough. Press Confirm Official Save to continue.");
            RefreshSlots();
            return;
        }

        SubmitAnalytics(slotNumber);
    }

    private async void SubmitAnalytics(int slotNumber)
    {
        if (isBusy)
            return;

        if (!SaveGameManager.TryGetSaveSlotData(
                slotNumber,
                out GameSaveData saveData,
                out string error))
        {
            SetMessage(error);
            return;
        }

        BeginBusyState();
        SetMessage("Uploading official Global Analytics...");
        AccountOperationResult result =
            await GlobalAnalyticsSubmissionService.SubmitAsync(saveData);

        if (!result.Success)
        {
            SetMessage(result.Error);
            EndBusyState();
            return;
        }

        pendingAnalyticsSlot = -1;
        GlobalAnalyticsStatusResult statusResult =
            await GlobalAnalyticsSubmissionService.GetStatusAsync();
        analyticsStatus = statusResult.Success ? statusResult.Status : analyticsStatus;
        SetMessage(statusResult.Success
            ? "Global Analytics uploaded successfully."
            : "Analytics were uploaded, but their current status could not be refreshed.");
        EndBusyState();
    }

    private void HandlePrimaryAction(int slotNumber, SaveSlotInfo slotInfo)
    {
        if (isBusy)
            return;

        if (mode == MenuMode.NewGame)
        {
            if (slotInfo.HasSave)
                return;

            BeginBusyState();
            if (!mainMenuController.TryStartNewGameInSlot(slotNumber))
                EndBusyState();

            return;
        }

        if (!slotInfo.HasSave)
            return;

        BeginBusyState();
        if (!chapterSelectionMenuController.ShowForSlot(
                slotNumber,
                ReturnFromChapterSelection,
                out string error))
        {
            SetMessage(error);
            EndBusyState();
            return;
        }

        isBusy = false;
        panelRoot.SetActive(false);
    }

    private void ReturnFromChapterSelection()
    {
        isBusy = false;
        Show(MenuMode.LoadGame);
    }

    private void RequestDelete(int slotNumber)
    {
        if (isBusy)
            return;

        pendingDeleteSlot = slotNumber;
        pendingAnalyticsSlot = -1;
        SetMessage($"Confirm deletion of Save Slot {slotNumber}.");
        RefreshSlots();
    }

    private void CancelDelete()
    {
        pendingDeleteSlot = -1;
        SetMessage(string.Empty);
        RefreshSlots();
    }

    private void ConfirmDelete(int slotNumber)
    {
        if (isBusy || pendingDeleteSlot != slotNumber)
            return;

        if (!SaveGameManager.TryDeleteSlot(slotNumber, out string error))
        {
            SetMessage(error);
            pendingDeleteSlot = -1;
            RefreshSlots();
            return;
        }

        pendingDeleteSlot = -1;
        SetMessage($"Save Slot {slotNumber} was deleted.");
        mainMenuController.RefreshButtons();
        RefreshSlots();
    }

    private void BeginBusyState()
    {
        isBusy = true;
        pendingDeleteSlot = -1;
        closeButton.interactable = false;

        foreach (SaveSlotView slotView in slotViews)
            slotView.SetInteractable(false);
    }

    private void EndBusyState()
    {
        isBusy = false;
        closeButton.interactable = true;
        RefreshSlots();
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
            messageText.SetText(message ?? string.Empty);
    }

    private bool TryValidate(out string error)
    {
        if (panelRoot == null || titleText == null || closeButton == null ||
            mainMenuController == null || chapterSelectionMenuController == null ||
            slotViews == null ||
            slotViews.Length != SaveGameManager.SaveSlotCount)
        {
            error = "Save Slot Menu Controller has unassigned UI references.";
            return false;
        }

        for (int index = 0; index < slotViews.Length; index++)
        {
            if (slotViews[index] == null)
            {
                error = $"Save Slot View {index + 1} is not assigned.";
                return false;
            }

            if (!slotViews[index].TryValidate(out error))
                return false;
        }

        error = string.Empty;
        return true;
    }
}
