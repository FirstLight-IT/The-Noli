using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionDetector : MonoBehaviour
{
    private IInteractable interactableInRange = null;


    public void onInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            interactableInRange?.interact();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out IInteractable interactable))
        {
            interactableInRange = interactable;
            interactable.showIcon(true);
            interactable.incrementCounter();
            
            //Debug.Log("TRIGGERED!");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out IInteractable interactable))
        {
            interactable.showIcon(false);
            interactableInRange = null;
        }
    }



}

