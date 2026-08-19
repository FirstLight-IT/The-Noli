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
    private bool isBusy;

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

    private void Show(MenuMode requestedMode)
    {
        if (!TryValidate(out string error))
        {
            Debug.LogError(error, this);
            return;
        }

        mode = requestedMode;
        pendingDeleteSlot = -1;
        isBusy = false;
        closeButton.interactable = true;
        panelRoot.SetActive(true);
        titleText.SetText(mode == MenuMode.NewGame ? "Choose an Empty Slot" : "Load Game");
        SetMessage(string.Empty);
        RefreshSlots();
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
        }
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
