using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionDetector : MonoBehaviour
{
    [Tooltip("Point NPCs should face, usually the player's sprite feet. Uses the player's Animator child when unassigned.")]
    [SerializeField] private Transform facingTarget;

    private Dictionary<Collider2D, IInteractable> interactablesInRange = new ();
    private readonly Dictionary<Collider2D, NPCMover> facingNPCsInRange = new();
    private IInteractable highlightedInteractable;

    private void Awake()
    {
        if (facingTarget != null)
            return;

        Rigidbody2D playerBody = GetComponentInParent<Rigidbody2D>();
        Animator playerAnimator = playerBody != null
            ? playerBody.GetComponentInChildren<Animator>(true)
            : null;
        facingTarget = playerAnimator != null
            ? playerAnimator.transform
            : playerBody != null
                ? playerBody.transform
                : transform.parent != null ? transform.parent : transform;
    }

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

        // A hint pan blocks new world interactions, but active dialogue must
        // still receive this button so the player can advance and close it.
        if (AmbientNPC.IsHintCameraPanning)
            return;

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

        if (invalidColliders != null)
        {
            foreach (Collider2D invalidCollider in invalidColliders)
                interactablesInRange.Remove(invalidCollider);
        }

        invalidColliders = null;
        foreach (var kvp in facingNPCsInRange)
        {
            if (kvp.Key != null && kvp.Key.enabled && kvp.Key.gameObject.activeInHierarchy &&
                kvp.Value != null && kvp.Value.isActiveAndEnabled)
            {
                continue;
            }

            invalidColliders ??= new List<Collider2D>();
            invalidColliders.Add(kvp.Key);
        }

        if (invalidColliders != null)
        {
            foreach (Collider2D invalidCollider in invalidColliders)
                RemoveFacingNPC(invalidCollider);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        NPCMover mover = collision.GetComponentInParent<NPCMover>();
        if (mover != null)
        {
            facingNPCsInRange[collision] = mover;
            mover.SetNearbyPlayer(facingTarget);
        }

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
        RemoveFacingNPC(collision);

        if(collision.TryGetComponent(out IInteractable interactable))
        {
            interactable.showIcon(false);
            interactablesInRange.Remove(collision);
            RefreshHighlight();
        }
    }

    private void RemoveFacingNPC(Collider2D collision)
    {
        if (!facingNPCsInRange.Remove(collision, out NPCMover mover) || mover == null)
            return;

        foreach (NPCMover remainingMover in facingNPCsInRange.Values)
        {
            if (remainingMover == mover)
                return;
        }

        mover.ClearNearbyPlayer(facingTarget);
    }

    private void OnDisable()
    {
        highlightedInteractable?.showHighlight(false);
        highlightedInteractable = null;
        interactablesInRange.Clear();

        foreach (NPCMover mover in facingNPCsInRange.Values)
        {
            if (mover != null)
                mover.ClearNearbyPlayer(facingTarget);
        }

        facingNPCsInRange.Clear();
    }
}
