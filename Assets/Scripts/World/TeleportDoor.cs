using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TeleportDoor : MonoBehaviour
{
    [Header("Teleport Settings")]
    [SerializeField] private Transform destination;
    [SerializeField, Min(0f)] private float teleportCooldown = 0.35f;

    [Header("Mission Lock")]
    [Tooltip("Leave empty for a door that is always unlocked.")]
    [SerializeField] private MissionInfoSO unlockWhenMissionStarts;
    [SerializeField, TextArea] private string lockedDialogue =
        "I really shouldn't be snooping around right now.";
    [SerializeField, Min(0f)] private float lockedDialogueCooldown = 1f;

    private static readonly Dictionary<EntityId, float> NextTeleportTimeByBody = new();
    private float nextLockedDialogueTime;

    private void Reset()
    {
        Collider2D doorCollider = GetComponent<Collider2D>();
        doorCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        Collider2D doorCollider = GetComponent<Collider2D>();

        if (doorCollider != null)
        {
            doorCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (destination == null)
        {
            return;
        }

        Rigidbody2D enteringBody = other.attachedRigidbody;

        if (enteringBody == null || other.gameObject != enteringBody.gameObject)
        {
            return;
        }

        bool isPlayer = enteringBody.CompareTag("Player");
        NPCMovement npcMovement = enteringBody.GetComponent<NPCMovement>();

        if (!isPlayer && npcMovement == null)
        {
            return;
        }

        if (isPlayer && ScreenFade.IsTransitioning)
        {
            return;
        }

        EntityId bodyID = enteringBody.GetEntityId();
        if (NextTeleportTimeByBody.TryGetValue(bodyID, out float nextTeleportTime) &&
            Time.time < nextTeleportTime)
        {
            return;
        }

        if (isPlayer && IsLocked())
        {
            ShowLockedDialogue();
            return;
        }

        NextTeleportTimeByBody[bodyID] = Time.time + teleportCooldown;

        if (npcMovement != null)
        {
            Teleport(enteringBody);
            npcMovement.HandleDoorTeleport();
            return;
        }

        if (ScreenFade.Instance != null &&
            ScreenFade.Instance.BeginTransition(() => Teleport(enteringBody)))
        {
            return;
        }

        Teleport(enteringBody);
    }

    private bool IsLocked()
    {
        if (unlockWhenMissionStarts == null)
            return false;

        MissionController missionController = MissionController.Instance;
        return missionController == null || !missionController.HasMissionStarted(unlockWhenMissionStarts);
    }

    private void ShowLockedDialogue()
    {
        if (Time.time < nextLockedDialogueTime)
            return;

        DialogueController dialogueController = DialogueController.Instance;
        NPCInfoSO speaker = PlayerCharacter.Instance != null
            ? PlayerCharacter.Instance.CurrentCharacter
            : null;

        if (dialogueController == null ||
            !dialogueController.ShowCharacterLine(speaker, lockedDialogue))
        {
            return;
        }

        nextLockedDialogueTime = Time.time + lockedDialogueCooldown;
    }

    private void Teleport(Rigidbody2D body)
    {
        if (body == null || destination == null)
        {
            return;
        }

        body.linearVelocity = Vector2.zero;
        body.position = destination.position;
        Physics2D.SyncTransforms();
    }

    private void OnDrawGizmosSelected()
    {
        if (destination == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawWireSphere(destination.position, 0.2f);
    }
}
