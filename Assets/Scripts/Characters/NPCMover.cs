using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NPCMover : MonoBehaviour
{
    private static readonly HashSet<NPCMover> ActiveMovers = new();

    public event Action Arrived;
    public event Action Blocked;
    public event Action<Transform> Teleported;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeed = 2f;
    [SerializeField, Min(0.01f)] private float arrivalDistance = 0.05f;

    [Header("Isometric Movement")]
    [SerializeField] private bool isometricMovementOnly;
    [SerializeField, Range(1f, 89f)] private float diagonalAngle = IsometricGeometry.GroundAngle;

    [Header("Physics")]
    [SerializeField] private bool ignoreOtherNPCs = true;
    [SerializeField, Min(1f)] private float bodyMass = 1000f;
    [SerializeField, Min(0f)] private float obstacleClearance = 0.05f;
    [SerializeField, Min(0f)] private float blockedNoticeDelay = 1f;

    private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[8];

    private Rigidbody2D body;
    private Collider2D[] bodyColliders;
    private RigidbodyConstraints2D movementConstraints;
    private ContactFilter2D obstacleFilter;
    private Transform destination;
    private Vector2 isometricCorner;
    private bool hasIsometricCorner;
    private float blockedTime;
    private bool isPhysicsPaused;

    public bool HasDestination => destination != null;
    public bool IsBlocked { get; private set; }
    public Transform Destination => destination;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyColliders = GetComponentsInChildren<Collider2D>(true);
        body.mass = bodyMass;
        body.gravityScale = 0f;

        movementConstraints = body.constraints | RigidbodyConstraints2D.FreezeRotation;
        body.constraints = movementConstraints;

        obstacleFilter = new ContactFilter2D { useTriggers = false };
        obstacleFilter.SetLayerMask(Physics2D.AllLayers);
    }

    private void OnEnable()
    {
        foreach (NPCMover otherMover in ActiveMovers)
            SetNPCPairCollision(otherMover, false);

        ActiveMovers.Add(this);
    }

    private void FixedUpdate()
    {
        if (IsMovementGloballyPaused())
        {
            PausePhysics();
            return;
        }

        ResumePhysics();

        if (destination == null)
        {
            StopBody();
            return;
        }

        Vector2 currentPosition = body.position;
        Vector2 destinationPosition = hasIsometricCorner
            ? isometricCorner
            : destination.position;

        if (Vector2.Distance(currentPosition, destinationPosition) <= arrivalDistance)
        {
            body.position = destinationPosition;
            ResetBlockedState();
            StopBody();

            if (hasIsometricCorner)
            {
                hasIsometricCorner = false;
                return;
            }

            destination = null;
            Arrived?.Invoke();
            return;
        }

        Vector2 direction = (destinationPosition - currentPosition).normalized;
        float checkDistance = movementSpeed * Time.fixedDeltaTime + obstacleClearance;

        if (HasBlockingHit(direction, checkDistance))
        {
            StopBody();
            IsBlocked = true;
            blockedTime += Time.fixedDeltaTime;

            if (blockedTime >= blockedNoticeDelay)
            {
                blockedTime = 0f;
                Blocked?.Invoke();
            }

            return;
        }

        ResetBlockedState();
        float remainingDistance = Vector2.Distance(currentPosition, destinationPosition);
        float allowedSpeed = Mathf.Min(
            movementSpeed,
            remainingDistance / Time.fixedDeltaTime);

        body.linearVelocity = direction * allowedSpeed;
    }

    public void MoveTo(Transform newDestination)
    {
        destination = newDestination;
        PrepareIsometricPath();
        ResetBlockedState();

        if (destination == null)
            StopBody();
    }

    public void MoveDirectlyTo(Transform newDestination)
    {
        destination = newDestination;
        hasIsometricCorner = false;
        ResetBlockedState();

        if (destination == null)
            StopBody();
    }

    public void Stop()
    {
        destination = null;
        hasIsometricCorner = false;
        ResetBlockedState();
        StopBody();
    }

    public bool IsPathImmediatelyBlocked(Transform target)
    {
        if (target == null || body == null)
            return true;

        // Network selection intentionally checks the direct connection, matching
        // the original waypoint-network behaviour. Isometric path splitting only
        // applies after a destination has been selected.
        Vector2 direction = ((Vector2)target.position - body.position).normalized;
        float checkDistance = obstacleClearance + 0.1f;

        return direction != Vector2.zero && HasBlockingHit(direction, checkDistance);
    }

    private bool HasBlockingHit(Vector2 direction, float distance)
    {
        int hitCount = body.Cast(direction, obstacleFilter, obstacleHits, distance);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = obstacleHits[i].collider;
            if (hitCollider == null)
                continue;

            NPCMover otherMover = hitCollider.GetComponentInParent<NPCMover>();
            bool ignoresThisNPC = ignoreOtherNPCs &&
                                  otherMover != null &&
                                  otherMover != this &&
                                  otherMover.ignoreOtherNPCs;

            if (!ignoresThisNPC)
                return true;
        }

        return false;
    }

    public void NotifyTeleported(Transform arrivalPoint)
    {
        destination = null;
        hasIsometricCorner = false;
        ResetBlockedState();
        StopBody();
        Teleported?.Invoke(arrivalPoint);
    }

    private void PrepareIsometricPath()
    {
        hasIsometricCorner = false;

        if (!isometricMovementOnly || destination == null)
            return;

        isometricCorner = GetFirstIsometricPathPoint(body.position, destination.position);
        hasIsometricCorner = Vector2.Distance(isometricCorner, destination.position) > arrivalDistance;
    }

    private Vector2 GetFirstIsometricPathPoint(Vector2 start, Vector2 end)
    {
        if (!isometricMovementOnly)
            return end;

        float radians = diagonalAngle * Mathf.Deg2Rad;
        float cosine = Mathf.Max(0.01f, Mathf.Cos(radians));
        float sine = Mathf.Max(0.01f, Mathf.Sin(radians));
        Vector2 offset = end - start;

        float rightAmount = (offset.x / cosine + offset.y / sine) * 0.5f;
        float leftAmount = (offset.y / sine - offset.x / cosine) * 0.5f;

        if (Mathf.Abs(rightAmount) <= arrivalDistance ||
            Mathf.Abs(leftAmount) <= arrivalDistance)
        {
            return end;
        }

        // Travel the longer leg first, then make one clean isometric turn.
        return Mathf.Abs(rightAmount) >= Mathf.Abs(leftAmount)
            ? start + IsometricGeometry.Axis(diagonalAngle, 1f) * rightAmount
            : start + IsometricGeometry.Axis(diagonalAngle, -1f) * leftAmount;
    }

    private static bool IsMovementGloballyPaused()
    {
        return ChapterController.IsChapterOpening ||
               AmbientNPC.IsHintCameraPanning ||
               (NarrationController.Instance != null && NarrationController.Instance.IsNarrationActive) ||
               (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) ||
               (ArtifactDialogueController.Instance != null && ArtifactDialogueController.Instance.IsDialogueActive);
    }

    private void PausePhysics()
    {
        if (isPhysicsPaused)
            return;

        StopBody();
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        isPhysicsPaused = true;
    }

    private void ResumePhysics()
    {
        if (!isPhysicsPaused)
            return;

        body.constraints = movementConstraints;
        isPhysicsPaused = false;
    }

    private void ResetBlockedState()
    {
        IsBlocked = false;
        blockedTime = 0f;
    }

    private void StopBody()
    {
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void OnDisable()
    {
        ActiveMovers.Remove(this);

        if (body == null)
            return;

        StopBody();
        body.constraints = movementConstraints;
        isPhysicsPaused = false;
    }

    private void SetNPCPairCollision(NPCMover otherMover, bool shouldCollide)
    {
        if (!ignoreOtherNPCs || otherMover == null || !otherMover.ignoreOtherNPCs)
            return;

        Collider2D[] otherColliders = otherMover.bodyColliders;
        if (bodyColliders == null || otherColliders == null)
            return;

        foreach (Collider2D ownCollider in bodyColliders)
        {
            if (ownCollider == null)
                continue;

            foreach (Collider2D otherCollider in otherColliders)
            {
                if (otherCollider != null)
                    Physics2D.IgnoreCollision(ownCollider, otherCollider, !shouldCollide);
            }
        }
    }

    private void Reset()
    {
        Rigidbody2D rigidbody = GetComponent<Rigidbody2D>();
        rigidbody.mass = bodyMass;
        rigidbody.gravityScale = 0f;
        rigidbody.freezeRotation = true;
    }
}
