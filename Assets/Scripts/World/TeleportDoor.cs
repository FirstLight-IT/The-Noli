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
    [SerializeField] private NPCInfoSO lockedDialogueSpeaker;
    [SerializeField, TextArea] private string lockedDialogue =
        "I really shouldn't be snooping around right now.";
    [SerializeField, Min(0f)] private float lockedDialogueCooldown = 1f;

    private static float nextTeleportTime;
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

        Rigidbody2D playerBody = other.attachedRigidbody;

        if (playerBody == null ||
            !playerBody.CompareTag("Player") ||
            other.gameObject != playerBody.gameObject ||
            ScreenFade.IsTransitioning)
        {
            return;
        }

        if (Time.time < nextTeleportTime)
        {
            return;
        }

        if (IsLocked())
        {
            ShowLockedDialogue();
            return;
        }

        nextTeleportTime = Time.time + teleportCooldown;

        if (ScreenFade.Instance != null &&
            ScreenFade.Instance.BeginTransition(() => Teleport(playerBody)))
        {
            return;
        }

        Teleport(playerBody);
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
            : lockedDialogueSpeaker;

        if (dialogueController == null ||
            !dialogueController.ShowCharacterLine(speaker, lockedDialogue))
        {
            return;
        }

        nextLockedDialogueTime = Time.time + lockedDialogueCooldown;
    }

    private void Teleport(Rigidbody2D playerBody)
    {
        if (playerBody == null || destination == null)
        {
            return;
        }

        playerBody.linearVelocity = Vector2.zero;
        playerBody.position = destination.position;
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
