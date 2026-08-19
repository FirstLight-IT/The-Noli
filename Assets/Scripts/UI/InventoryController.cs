using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance { get; private set; }
    public static bool IsJournalOpen { get; private set; }

    [SerializeField] GameObject menuCanvas;

    private void Awake()
    {
        Instance = this;
    }
   
    void Start()
    {
        menuCanvas.SetActive(false);
        IsJournalOpen = false;
    }

    //toggle for the journal using Tab or I key. 
    public void toggleJournal(InputAction.CallbackContext context)
    {
        if (!context.performed || PauseMenuController.IsPaused ||
            ChapterController.IsChapterOpening || IsAnyDialogueActive())
            return;

        IsJournalOpen = !IsJournalOpen;
        menuCanvas.SetActive(IsJournalOpen);
    }

    private static bool IsAnyDialogueActive()
    {
        return (NarrationController.Instance != null && NarrationController.Instance.IsNarrationActive) ||
               (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) ||
               (ArtifactDialogueController.Instance != null && ArtifactDialogueController.Instance.IsDialogueActive);
    }

    public void exitJournal()
    {
        IsJournalOpen = false;
        menuCanvas.SetActive(false);
    }

    public static bool CloseIfOpen()
    {
        if (!IsJournalOpen || Instance == null)
            return false;

        Instance.exitJournal();
        return true;
    }

    void OnDisable()
    {
        IsJournalOpen = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


}
