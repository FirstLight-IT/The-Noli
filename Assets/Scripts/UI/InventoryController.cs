using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class InventoryController : MonoBehaviour
{

    [SerializeField] GameObject menuCanvas;
   
    void Start()
    {
        menuCanvas.SetActive(false);
    }

    //toggle for the journal using Tab or I key. 
    public void toggleJournal(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            menuCanvas.SetActive(!menuCanvas.activeSelf);
        }

    }

    public void exitJournal()
    {
        menuCanvas.SetActive(false);
    }


}
