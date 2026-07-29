using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TeleportDoor : MonoBehaviour
{
    [Header("Teleport Settings")]
    [SerializeField] private Transform destination;
    [SerializeField, Min(0f)] private float teleportCooldown = 0.35f;

    private static float nextTeleportTime;

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

        nextTeleportTime = Time.time + teleportCooldown;

        if (ScreenFade.Instance != null &&
            ScreenFade.Instance.BeginTransition(() => Teleport(playerBody)))
        {
            return;
        }

        Teleport(playerBody);
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
