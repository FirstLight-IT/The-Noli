using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionDetector : MonoBehaviour
{
    private Dictionary<Collider2D, IInteractable> interactablesInRange = new ();

    public void onInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            GetClosestInteractable()?.interact();
        }
    }

    private IInteractable GetClosestInteractable()
    {
        IInteractable closest = null;
        float closestSqrDist = float.MaxValue;
        Vector2 myPos = transform.position;

        foreach (var kvp in interactablesInRange)
        {
            float sqrDist = (myPos - (Vector2)kvp.Key.transform.position).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = kvp.Value;
            }
        }

        return closest;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out IInteractable interactable))
        {
            interactablesInRange.Add(collision, interactable);
            interactable.showIcon(true);
            interactable.incrementCounter();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out IInteractable interactable))
        {
            interactable.showIcon(false);
            interactablesInRange.Remove(collision);
        }
    }
}
