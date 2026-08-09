using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryController : MonoBehaviour
{
    public static bool IsJournalOpen { get; private set; }

    [SerializeField] GameObject menuCanvas;
   
    void Start()
    {
        menuCanvas.SetActive(false);
        IsJournalOpen = false;
    }

    //toggle for the journal using Tab or I key. 
    public void toggleJournal(InputAction.CallbackContext context)
    {
        if (!context.performed || IsAnyDialogueActive())
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

    void OnDisable()
    {
        IsJournalOpen = false;
    }


}
