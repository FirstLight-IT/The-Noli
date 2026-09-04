using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class StairsTrigger : MonoBehaviour
{
    [SerializeField] private Transform bottom;
    [SerializeField] private Transform top;

    private readonly HashSet<NPCMover> npcMoversInside = new();

    private void Awake()
    {
        FindMarkers();
    }

    public Vector2 GetSlopeMovement(Vector2 input, float diagonalAngle)
    {
        float inputMagnitude = Mathf.Clamp01(input.magnitude);
        if (inputMagnitude <= Mathf.Epsilon || bottom == null || top == null)
        {
            return Vector2.zero;
        }

        Vector2 slope = (Vector2)(top.position - bottom.position);
        if (slope.sqrMagnitude <= Mathf.Epsilon)
        {
            return Vector2.zero;
        }

        Vector2 slopeDirection = slope.normalized;

        // A staircase in an isometric world occupies one diagonal ground axis.
        // Choose the ascending input diagonal from the side on which Top lies.
        float ascentX = Mathf.Sign(slope.x);
        Vector2 ascentInputAxis = new Vector2(ascentX, 1f).normalized;
        Vector2 sideInputAxis = new Vector2(-ascentX, 1f).normalized;

        float climbInput = Vector2.Dot(input, ascentInputAxis);
        float sideInput = Vector2.Dot(input, sideInputAxis);

        float angle = diagonalAngle * Mathf.Deg2Rad;
        Vector2 sideDirection = new(
            sideInputAxis.x * Mathf.Cos(angle),
            sideInputAxis.y * Mathf.Sin(angle));
        sideDirection.Normalize();

        Vector2 movement = slopeDirection * climbInput + sideDirection * sideInput;
        return movement.normalized * inputMagnitude;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (TryGetPlayerBody(other, out PlayerMovement player))
            player.EnterSlope(this);

        if (TryGetNPCBody(other, out NPCMover npcMover))
        {
            npcMoversInside.Add(npcMover);
            npcMover.EnterSlope(this);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (TryGetPlayerBody(other, out PlayerMovement player))
            player.EnterSlope(this);

        if (TryGetNPCBody(other, out NPCMover npcMover))
        {
            npcMoversInside.Add(npcMover);
            npcMover.EnterSlope(this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (TryGetPlayerBody(other, out PlayerMovement player))
            player.ExitSlope(this);

        if (TryGetNPCBody(other, out NPCMover npcMover))
        {
            npcMoversInside.Remove(npcMover);
            npcMover.ExitSlope(this);
        }
    }

    private void OnDisable()
    {
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        if (player != null)
            player.ExitSlope(this);

        foreach (NPCMover npcMover in npcMoversInside)
        {
            if (npcMover != null)
                npcMover.ExitSlope(this);
        }

        npcMoversInside.Clear();
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
        FindMarkers();
    }

    private void OnValidate()
    {
        Collider2D slopeTrigger = GetComponent<Collider2D>();
        if (slopeTrigger != null)
        {
            slopeTrigger.isTrigger = true;
        }

        FindMarkers();
    }

    private void FindMarkers()
    {
        if (bottom == null)
        {
            bottom = transform.Find("Bottom");
        }

        if (top == null)
        {
            top = transform.Find("Top");
        }
    }

    private static bool TryGetPlayerBody(Collider2D other, out PlayerMovement player)
    {
        player = null;
        if (other == null || other.isTrigger || other.attachedRigidbody == null)
        {
            return false;
        }

        player = other.attachedRigidbody.GetComponent<PlayerMovement>();
        Collider2D bodyCollider = other.attachedRigidbody.GetComponent<Collider2D>();
        return player != null && other == bodyCollider;
    }

    private static bool TryGetNPCBody(Collider2D other, out NPCMover npcMover)
    {
        npcMover = null;
        if (other == null || other.isTrigger || other.attachedRigidbody == null)
            return false;

        npcMover = other.attachedRigidbody.GetComponent<NPCMover>();
        Collider2D bodyCollider = other.attachedRigidbody.GetComponent<Collider2D>();
        return npcMover != null && other == bodyCollider;
    }

    private void OnDrawGizmosSelected()
    {
        FindMarkers();
        if (bottom == null || top == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(bottom.position, top.position);
        Gizmos.DrawSphere(bottom.position, 0.08f);
        Gizmos.DrawSphere(top.position, 0.08f);

    }
}
