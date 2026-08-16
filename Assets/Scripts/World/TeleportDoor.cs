using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TeleportDoor : MonoBehaviour
{
    [Header("Teleport Settings")]
    [SerializeField] private Transform destination;
    [SerializeField, Min(0f)] private float teleportCooldown = 0.35f;

    [Header("NPC Teleport Settings")]
    [Tooltip("Optional NPC-only landing point. This does not change the player's Destination.")]
    [SerializeField] private Transform npcDestination;

    [Header("Mission Lock")]
    [Tooltip("Leave empty for a door that is always unlocked.")]
    [SerializeField] private MissionInfoSO unlockWhenMissionStarts;
    [SerializeField, TextArea] private string lockedDialogue =
        "I really shouldn't be snooping around right now.";
    [SerializeField, Min(0f)] private float lockedDialogueCooldown = 1f;

    private static float nextPlayerTeleportTime;
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

        if (enteringBody == null ||
            !enteringBody.CompareTag("Player") ||
            other.isTrigger ||
            ScreenFade.IsTransitioning)
        {
            return;
        }

        if (Time.time < nextPlayerTeleportTime)
        {
            return;
        }

        if (IsLocked())
        {
            ShowLockedDialogue();
            return;
        }

        nextPlayerTeleportTime = Time.time + teleportCooldown;

        if (ScreenFade.Instance != null &&
            ScreenFade.Instance.BeginTransition(() => TeleportPlayer(enteringBody)))
        {
            return;
        }

        TeleportPlayer(enteringBody);
    }

    public bool TeleportNPC(NPCMover mover)
    {
        if (mover == null)
            return false;

        Rigidbody2D npcBody = mover.GetComponent<Rigidbody2D>();
        Transform npcLandingPoint = npcDestination != null ? npcDestination : destination;

        if (npcBody == null || npcLandingPoint == null)
            return false;

        Teleport(npcBody, npcLandingPoint);
        mover.NotifyTeleported(npcLandingPoint);
        return true;
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

    private static void Teleport(Rigidbody2D body, Transform landingPoint)
    {
        if (body == null || landingPoint == null)
        {
            return;
        }

        body.linearVelocity = Vector2.zero;
        body.position = landingPoint.position;
        Physics2D.SyncTransforms();
    }

    private void TeleportPlayer(Rigidbody2D playerBody)
    {
        Teleport(playerBody, destination);
        SaveGameManager.RecordPlayerDoorTransition();
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
