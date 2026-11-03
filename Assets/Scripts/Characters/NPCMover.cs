using System;
using System.Collections.Generic;
using UnityEngine;

public enum NPCFacingDirection
{
    North,
    NorthEast,
    East,
    SouthEast,
    South,
    SouthWest,
    West,
    NorthWest
}

[RequireComponent(typeof(Rigidbody2D))]
public class NPCMover : MonoBehaviour
{
    private const float DiagonalStepDegrees = 45f;
    private const float CardinalDominanceThreshold = 0.65f;
    private const float NearbyFacingHysteresisDegrees = 8f;
    private const float NearbyFacingMinimumDistanceSqr = 0.16f;
    private static readonly HashSet<NPCMover> ActiveMovers = new();
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int InputXHash = Animator.StringToHash("inputX");
    private static readonly int InputYHash = Animator.StringToHash("inputY");
    private static readonly int LastInputXHash = Animator.StringToHash("lastInputX");
    private static readonly int LastInputYHash = Animator.StringToHash("lastInputY");

    public event Action Arrived;
    public event Action Blocked;
    public event Action<Transform> Teleported;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeed = 2f;
    [SerializeField, Min(0.01f)] private float arrivalDistance = 0.05f;

    [Header("Isometric Movement")]
    [SerializeField] private bool isometricMovementOnly;
    [SerializeField, Range(1f, 89f)] private float diagonalAngle = IsometricGeometry.GroundAngle;

    [Header("Facing")]
    [Tooltip("Idle direction used when this NPC first appears. Prefab variants and scene instances can override it.")]
    [SerializeField] private NPCFacingDirection defaultFacing = NPCFacingDirection.South;
    [Tooltip("When enabled, an idle NPC faces the player while the player's interaction detector overlaps it.")]
    [SerializeField] private bool turnTowardNearbyPlayer = true;

    [Header("Physics")]
    [SerializeField] private bool ignoreOtherNPCs = true;
    [SerializeField, Min(1f)] private float bodyMass = 1000f;
    [SerializeField, Min(0f)] private float obstacleClearance = 0.05f;
    [SerializeField, Min(0f)] private float blockedNoticeDelay = 1f;

    private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[8];

    private Rigidbody2D body;
    private Animator animator;
    private Collider2D[] bodyColliders;
    private RigidbodyConstraints2D movementConstraints;
    private ContactFilter2D obstacleFilter;
    private Transform destination;
    private Vector2 isometricCorner;
    private bool hasIsometricCorner;
    private StairsTrigger activeSlope;
    private float blockedTime;
    private bool isPhysicsPaused;
    private Vector2 restingFacing;
    private Vector2 currentFacing;
    private Vector2 nearbyFacing;
    private bool hasNearbyFacing;
    private Transform nearbyPlayer;

    public bool HasDestination => destination != null;
    public bool IsBlocked { get; private set; }
    public bool IsOnSlope => activeSlope != null;
    public Transform Destination => destination;
    public Vector2 RestingFacing => restingFacing;
    public Vector2 CurrentFacing => currentFacing;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>(true);
        bodyColliders = GetComponentsInChildren<Collider2D>(true);
        body.mass = bodyMass;
        body.gravityScale = 0f;

        movementConstraints = body.constraints | RigidbodyConstraints2D.FreezeRotation;
        body.constraints = movementConstraints;

        obstacleFilter = new ContactFilter2D { useTriggers = false };
        obstacleFilter.SetLayerMask(Physics2D.AllLayers);

        InitializeAnimation();
    }

    private void OnEnable()
    {
        foreach (NPCMover otherMover in ActiveMovers)
            SetNPCPairCollision(otherMover, false);

        ActiveMovers.Add(this);
    }

    private void Update()
    {
        if (nearbyPlayer != null && destination == null)
            RefreshIdleFacing();
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
        UpdateAnimation(direction);
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

    public void EnterSlope(StairsTrigger slope)
    {
        if (slope == null || activeSlope == slope)
            return;

        activeSlope = slope;
        PrepareIsometricPath();
    }

    public void ExitSlope(StairsTrigger slope)
    {
        if (activeSlope != slope)
            return;

        activeSlope = null;
        PrepareIsometricPath();
    }

    public void Stop()
    {
        destination = null;
        hasIsometricCorner = false;
        ResetBlockedState();
        StopBody();
    }

    public void SetRestingFacing(NPCFacingDirection facing)
    {
        restingFacing = GetFacingVector(facing);
        RefreshIdleFacing();
    }

    public void SetNearbyPlayer(Transform player)
    {
        if (!turnTowardNearbyPlayer || player == null)
            return;

        if (nearbyPlayer != player)
            hasNearbyFacing = false;

        nearbyPlayer = player;
        RefreshIdleFacing();
    }

    public void ClearNearbyPlayer(Transform player)
    {
        if (nearbyPlayer == null || (player != null && nearbyPlayer != player))
            return;

        nearbyPlayer = null;
        hasNearbyFacing = false;
        RefreshIdleFacing();
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

        if (!isometricMovementOnly || destination == null || activeSlope != null)
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
        UpdateAnimation(Vector2.zero);
    }

    private void InitializeAnimation()
    {
        restingFacing = GetFacingVector(defaultFacing);
        currentFacing = restingFacing;

        if (HasAnimationController())
        {
            // Eight-direction clips already have distinct left/right frames. A mirrored
            // renderer makes east/west (and diagonals) look opposite to the facing data.
            SpriteRenderer directionalRenderer = animator.GetComponent<SpriteRenderer>();
            if (directionalRenderer != null)
                directionalRenderer.flipX = false;

            animator.SetBool(IsWalkingHash, false);
            ApplyAnimatorFacing(currentFacing);
        }
    }

    private void UpdateAnimation(Vector2 movementDirection)
    {
        bool isWalking = movementDirection.sqrMagnitude > 0.0001f;
        if (isWalking)
        {
            restingFacing = SnapFacing(movementDirection);
            currentFacing = restingFacing;
        }
        else
        {
            currentFacing = ResolveIdleFacing();
        }

        if (HasAnimationController())
        {
            ApplyAnimatorFacing(currentFacing);
            animator.SetBool(IsWalkingHash, isWalking);
        }
    }

    private void RefreshIdleFacing()
    {
        if (destination != null)
            return;

        currentFacing = ResolveIdleFacing();

        if (HasAnimationController())
        {
            ApplyAnimatorFacing(currentFacing);
            animator.SetBool(IsWalkingHash, false);
        }
    }

    private Vector2 ResolveIdleFacing()
    {
        if (turnTowardNearbyPlayer && nearbyPlayer != null && destination == null)
        {
            Vector2 playerDirection = (Vector2)nearbyPlayer.position - body.position;
            if (playerDirection.sqrMagnitude >= NearbyFacingMinimumDistanceSqr)
            {
                Vector2 isometricDirection = ToIsometricFacingSpace(playerDirection);
                if (hasNearbyFacing &&
                    Vector2.Angle(nearbyFacing, isometricDirection) <=
                    GetSectorHalfWidth(nearbyFacing) + NearbyFacingHysteresisDegrees)
                {
                    return nearbyFacing;
                }

                nearbyFacing = SnapFacingFromIsometricSpace(isometricDirection);
                hasNearbyFacing = true;
                return nearbyFacing;
            }

            if (hasNearbyFacing)
                return nearbyFacing;
        }

        return restingFacing.sqrMagnitude > 0.0001f
            ? restingFacing
            : GetFacingVector(defaultFacing);
    }

    private void ApplyAnimatorFacing(Vector2 facing)
    {
        // Both blend trees must point at the same discrete clip. Supplying the
        // raw movement vector mixes neighbouring sprite animations while the
        // walking/idle states transition, which appears as random turning.
        animator.SetFloat(InputXHash, facing.x);
        animator.SetFloat(InputYHash, facing.y);
        animator.SetFloat(LastInputXHash, facing.x);
        animator.SetFloat(LastInputYHash, facing.y);
    }

    private bool HasAnimationController()
    {
        return animator != null && animator.runtimeAnimatorController != null;
    }

    private Vector2 SnapFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector2.zero;

        return SnapFacingFromIsometricSpace(ToIsometricFacingSpace(direction));
    }

    private Vector2 ToIsometricFacingSpace(Vector2 direction)
    {
        float radians = diagonalAngle * Mathf.Deg2Rad;
        return new Vector2(
            direction.x / Mathf.Max(0.01f, Mathf.Cos(radians)),
            direction.y / Mathf.Max(0.01f, Mathf.Sin(radians)));
    }

    private static Vector2 SnapFacingFromIsometricSpace(Vector2 direction)
    {
        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);

        // Give front/back/side sprites a wider sector than diagonals. With
        // equal 45-degree sectors, a player visibly in front but a little to
        // the side is classified as diagonal immediately on trigger entry.
        if (absX <= absY * CardinalDominanceThreshold)
            return direction.y >= 0f ? Vector2.up : Vector2.down;

        if (absY <= absX * CardinalDominanceThreshold)
            return direction.x >= 0f ? Vector2.right : Vector2.left;

        return new Vector2(Mathf.Sign(direction.x), Mathf.Sign(direction.y));
    }

    private static float GetSectorHalfWidth(Vector2 facing)
    {
        float cardinalHalfWidth = Mathf.Atan(CardinalDominanceThreshold) * Mathf.Rad2Deg;
        return facing.x != 0f && facing.y != 0f
            ? DiagonalStepDegrees - cardinalHalfWidth
            : cardinalHalfWidth;
    }

    private static Vector2 GetFacingVector(NPCFacingDirection facing)
    {
        return facing switch
        {
            NPCFacingDirection.North => Vector2.up,
            NPCFacingDirection.NorthEast => Vector2.one,
            NPCFacingDirection.East => Vector2.right,
            NPCFacingDirection.SouthEast => new Vector2(1f, -1f),
            NPCFacingDirection.South => Vector2.down,
            NPCFacingDirection.SouthWest => -Vector2.one,
            NPCFacingDirection.West => Vector2.left,
            NPCFacingDirection.NorthWest => new Vector2(-1f, 1f),
            _ => Vector2.down
        };
    }

    private void OnDisable()
    {
        ActiveMovers.Remove(this);

        nearbyPlayer = null;
        hasNearbyFacing = false;

        if (body == null)
            return;

        StopBody();
        body.constraints = movementConstraints;
        isPhysicsPaused = false;
        activeSlope = null;
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
