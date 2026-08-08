using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NPCMover : MonoBehaviour
{
    public event Action Arrived;
    public event Action Blocked;
    public event Action<Transform> Teleported;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeed = 2f;
    [SerializeField, Min(0.01f)] private float arrivalDistance = 0.05f;

    [Header("Isometric Movement")]
    [SerializeField] private bool isometricMovementOnly;
    [Tooltip("0.5 gives classic 2:1 isometric diagonals. 1 gives 45-degree diagonals.")]
    [SerializeField, Range(0.1f, 1f)] private float isometricSlope = 0.5f;

    [Header("Physics")]
    [SerializeField, Min(1f)] private float bodyMass = 1000f;
    [SerializeField, Min(0f)] private float obstacleClearance = 0.05f;
    [SerializeField, Min(0f)] private float blockedNoticeDelay = 1f;

    private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[8];

    private Rigidbody2D body;
    private RigidbodyConstraints2D movementConstraints;
    private ContactFilter2D obstacleFilter;
    private Transform destination;
    private Vector2 isometricCorner;
    private bool hasIsometricCorner;
    private float blockedTime;
    private bool hasReportedBlocked;
    private bool isPhysicsPaused;

    public bool HasDestination => destination != null;
    public bool IsBlocked { get; private set; }
    public Transform Destination => destination;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.mass = bodyMass;
        body.gravityScale = 0f;

        movementConstraints = body.constraints | RigidbodyConstraints2D.FreezeRotation;
        body.constraints = movementConstraints;

        obstacleFilter = new ContactFilter2D { useTriggers = false };
        obstacleFilter.SetLayerMask(Physics2D.AllLayers);
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

        if (body.Cast(direction, obstacleFilter, obstacleHits, checkDistance) > 0)
        {
            StopBody();
            IsBlocked = true;
            blockedTime += Time.fixedDeltaTime;

            if (!hasReportedBlocked && blockedTime >= blockedNoticeDelay)
            {
                hasReportedBlocked = true;
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

    public void Stop()
    {
        destination = null;
        hasIsometricCorner = false;
        ResetBlockedState();
        StopBody();
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

        Vector2 start = body.position;
        Vector2 offset = (Vector2)destination.position - start;
        float slope = Mathf.Max(0.1f, isometricSlope);

        // Express the destination as movement along the two isometric ground axes:
        // axis A = (1, slope), axis B = (-1, slope).
        float axisAAmount = (offset.x + offset.y / slope) * 0.5f;
        float axisBAmount = (offset.y / slope - offset.x) * 0.5f;
        float axisLength = Mathf.Sqrt(1f + slope * slope);
        float axisADistance = Mathf.Abs(axisAAmount) * axisLength;
        float axisBDistance = Mathf.Abs(axisBAmount) * axisLength;

        // A destination already on one isometric axis needs no intermediate turn.
        if (axisADistance <= arrivalDistance || axisBDistance <= arrivalDistance)
            return;

        // Travel the longer leg first, then make one clean turn toward the destination.
        isometricCorner = axisADistance >= axisBDistance
            ? start + new Vector2(axisAAmount, axisAAmount * slope)
            : start + new Vector2(-axisBAmount, axisBAmount * slope);

        hasIsometricCorner = true;
    }

    private static bool IsMovementGloballyPaused()
    {
        return (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) ||
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
        hasReportedBlocked = false;
    }

    private void StopBody()
    {
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void OnDisable()
    {
        if (body == null)
            return;

        StopBody();
        body.constraints = movementConstraints;
        isPhysicsPaused = false;
    }

    private void Reset()
    {
        Rigidbody2D rigidbody = GetComponent<Rigidbody2D>();
        rigidbody.mass = bodyMass;
        rigidbody.gravityScale = 0f;
        rigidbody.freezeRotation = true;
    }
}
