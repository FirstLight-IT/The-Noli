using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionDetector : MonoBehaviour
{
    private Dictionary<Collider2D, IInteractable> interactablesInRange = new ();
    private IInteractable highlightedInteractable;

    private void Update()
    {
        RefreshHighlight();
    }

    public void onInteract(InputAction.CallbackContext context)
    {
        if (!context.performed ||
            PauseMenuController.IsPaused ||
            ChapterController.IsChapterOpening ||
            InventoryController.IsJournalOpen ||
            MissionController.IsMissionCompletionVisible)
        {
            return;
        }

        if (NarrationController.Instance != null &&
            NarrationController.Instance.AdvanceActiveNarration())
        {
            return;
        }

        if (DialogueController.Instance != null &&
            DialogueController.Instance.AdvanceActiveDialogue())
        {
            return;
        }

        GetClosestInteractable()?.interact();
    }

    private IInteractable GetClosestInteractable()
    {
        IInteractable closest = null;
        float closestSqrDist = float.MaxValue;
        Vector2 myPos = transform.position;

        foreach (var kvp in interactablesInRange)
        {
            if (!kvp.Value.canInteract())
                continue;

            float sqrDist = (myPos - (Vector2)kvp.Key.transform.position).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = kvp.Value;
            }
        }

        return closest;
    }

    private void RefreshHighlight()
    {
        RemoveInvalidEntries();

        foreach (var kvp in interactablesInRange)
            kvp.Value.showIcon(kvp.Value.canInteract());

        IInteractable closest = GetClosestInteractable();
        if (ReferenceEquals(closest, highlightedInteractable))
            return;

        highlightedInteractable?.showHighlight(false);
        highlightedInteractable = closest;
        highlightedInteractable?.showHighlight(true);
    }

    private void RemoveInvalidEntries()
    {
        List<Collider2D> invalidColliders = null;

        foreach (var kvp in interactablesInRange)
        {
            if (kvp.Key != null && kvp.Key.enabled && kvp.Key.gameObject.activeInHierarchy)
                continue;

            invalidColliders ??= new List<Collider2D>();
            invalidColliders.Add(kvp.Key);
        }

        if (invalidColliders == null)
            return;

        foreach (Collider2D invalidCollider in invalidColliders)
            interactablesInRange.Remove(invalidCollider);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out IInteractable interactable))
        {
            interactablesInRange[collision] = interactable;
            interactable.showIcon(interactable.canInteract());
            interactable.incrementCounter();
            RefreshHighlight();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out IInteractable interactable))
        {
            interactable.showIcon(false);
            interactablesInRange.Remove(collision);
            RefreshHighlight();
        }
    }

    private void OnDisable()
    {
        highlightedInteractable?.showHighlight(false);
        highlightedInteractable = null;
        interactablesInRange.Clear();
    }
}
