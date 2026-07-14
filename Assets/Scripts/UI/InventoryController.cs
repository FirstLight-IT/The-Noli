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
        if (context.performed)
        {
            IsJournalOpen = !IsJournalOpen;
            menuCanvas.SetActive(IsJournalOpen);
        }

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
