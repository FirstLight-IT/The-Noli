using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NPCMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeed = 2f;
    [SerializeField, Min(0f)] private float waitAtWaypoint = 1f;
    [SerializeField, Min(0.01f)] private float arrivalDistance = 0.05f;
    [SerializeField, Min(1f)] private float bodyMass = 1000f;
    [SerializeField, Min(0f)] private float obstacleClearance = 0.05f;
    [SerializeField, Min(0f)] private float blockedWaitTime = 1f;

    [Header("Waypoint Network")]
    [SerializeField] private bool useWaypointNetwork;
    [SerializeField] private NPCWaypoint startingWaypoint;

    [Header("Simple Patrol (Legacy)")]
    [SerializeField] private bool patrolOnStart = true;
    [SerializeField] private bool pingPongPatrol;
    [SerializeField] private Transform[] waypoints = new Transform[0];

    private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[8];
    private readonly List<NPCWaypoint> waypointChoices = new();

    private Rigidbody2D body;
    private RigidbodyConstraints2D movementConstraints;
    private ContactFilter2D obstacleFilter;
    private int currentWaypointIndex;
    private int patrolDirection = 1;
    private bool isWaiting;
    private bool isPhysicsPaused;
    private float blockedTime;

    private NPCWaypoint targetNetworkWaypoint;
    private NPCWaypoint lastReachedNetworkWaypoint;
    private NPCWaypoint previousNetworkWaypoint;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.mass = bodyMass;
        movementConstraints = body.constraints | RigidbodyConstraints2D.FreezeRotation;
        body.constraints = movementConstraints;

        obstacleFilter = new ContactFilter2D { useTriggers = false };
        obstacleFilter.SetLayerMask(Physics2D.AllLayers);
    }

    private void OnEnable()
    {
        currentWaypointIndex = 0;
        patrolDirection = 1;
        isWaiting = false;
        isPhysicsPaused = false;
        blockedTime = 0f;

        previousNetworkWaypoint = null;
        lastReachedNetworkWaypoint = null;
        targetNetworkWaypoint = useWaypointNetwork ? startingWaypoint : null;
    }

    private void FixedUpdate()
    {
        if (IsMovementPaused())
        {
            PausePhysics();
            return;
        }

        ResumePhysics();

        if (!patrolOnStart || isWaiting)
        {
            StopMoving();
            return;
        }

        Transform target = GetCurrentTarget();
        if (target == null)
        {
            StopMoving();
            return;
        }

        Vector2 currentPosition = body.position;
        Vector2 targetPosition = target.position;

        if (Vector2.Distance(currentPosition, targetPosition) <= arrivalDistance)
        {
            body.position = targetPosition;
            StopMoving();
            blockedTime = 0f;
            StartCoroutine(WaitThenAdvance());
            return;
        }

        Vector2 direction = (targetPosition - currentPosition).normalized;
        float checkDistance = movementSpeed * Time.fixedDeltaTime + obstacleClearance;

        if (HasObstacleAhead(direction, checkDistance))
        {
            StopMoving();
            blockedTime += Time.fixedDeltaTime;

            if (blockedTime >= blockedWaitTime)
            {
                if (useWaypointNetwork)
                    TrySwitchBlockedDestination();

                blockedTime = 0f;
            }

            return;
        }

        blockedTime = 0f;
        body.linearVelocity = direction * movementSpeed;
    }

    private Transform GetCurrentTarget()
    {
        if (useWaypointNetwork)
            return targetNetworkWaypoint != null ? targetNetworkWaypoint.transform : null;

        if (waypoints == null || waypoints.Length == 0)
            return null;

        Transform target = waypoints[currentWaypointIndex];
        if (target == null)
            AdvanceSimplePatrol();

        return target;
    }

    private IEnumerator WaitThenAdvance()
    {
        isWaiting = true;

        if (useWaypointNetwork)
            RecordNetworkWaypointArrival();

        if (waitAtWaypoint > 0f)
            yield return new WaitForSeconds(waitAtWaypoint);

        if (useWaypointNetwork)
            ChooseNextNetworkWaypoint();
        else
            AdvanceSimplePatrol();

        isWaiting = false;
    }

    private void RecordNetworkWaypointArrival()
    {
        previousNetworkWaypoint = lastReachedNetworkWaypoint;
        lastReachedNetworkWaypoint = targetNetworkWaypoint;
    }

    private void ChooseNextNetworkWaypoint()
    {
        NPCWaypoint next = ChooseNeighbour(
            lastReachedNetworkWaypoint,
            previousNetworkWaypoint,
            null);

        targetNetworkWaypoint = next != null ? next : lastReachedNetworkWaypoint;
    }

    private void TrySwitchBlockedDestination()
    {
        if (lastReachedNetworkWaypoint == null)
            return;

        NPCWaypoint alternative = ChooseNeighbour(
            lastReachedNetworkWaypoint,
            previousNetworkWaypoint,
            targetNetworkWaypoint);

        if (alternative != null)
            targetNetworkWaypoint = alternative;
    }

    private NPCWaypoint ChooseNeighbour(
        NPCWaypoint origin,
        NPCWaypoint avoidBacktrackingTo,
        NPCWaypoint excludedDestination)
    {
        waypointChoices.Clear();

        if (origin == null || origin.Neighbours == null)
            return null;

        AddOpenNeighbours(origin, avoidBacktrackingTo, excludedDestination);

        // A dead end is allowed to return to the previous node.
        if (waypointChoices.Count == 0 && avoidBacktrackingTo != null)
            AddOpenNeighbours(origin, null, excludedDestination);

        if (waypointChoices.Count == 0)
            return null;

        return waypointChoices[Random.Range(0, waypointChoices.Count)];
    }

    private void AddOpenNeighbours(
        NPCWaypoint origin,
        NPCWaypoint avoidBacktrackingTo,
        NPCWaypoint excludedDestination)
    {
        foreach (NPCWaypoint neighbour in origin.Neighbours)
        {
            if (neighbour == null ||
                neighbour == avoidBacktrackingTo ||
                neighbour == excludedDestination)
            {
                continue;
            }

            Vector2 direction = ((Vector2)neighbour.transform.position - body.position).normalized;
            if (!HasObstacleAhead(direction, obstacleClearance + 0.1f))
                waypointChoices.Add(neighbour);
        }
    }

    private static bool IsMovementPaused()
    {
        return (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) ||
               (ArtifactDialogueController.Instance != null && ArtifactDialogueController.Instance.IsDialogueActive);
    }

    private void PausePhysics()
    {
        if (isPhysicsPaused)
            return;

        StopMoving();
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

    private void StopMoving()
    {
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private bool HasObstacleAhead(Vector2 direction, float distance)
    {
        if (direction == Vector2.zero || distance <= 0f)
            return false;

        return body.Cast(direction, obstacleFilter, obstacleHits, distance) > 0;
    }

    public void HandleDoorTeleport()
    {
        StopMoving();
        blockedTime = 0f;

        if (useWaypointNetwork)
        {
            RecordNetworkWaypointArrival();
            ChooseNextNetworkWaypoint();
            return;
        }

        AdvanceSimplePatrol();
    }

    private void AdvanceSimplePatrol()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        if (!pingPongPatrol || waypoints.Length <= 1)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            return;
        }

        int nextWaypointIndex = currentWaypointIndex + patrolDirection;
        if (nextWaypointIndex < 0 || nextWaypointIndex >= waypoints.Length)
        {
            patrolDirection *= -1;
            nextWaypointIndex = currentWaypointIndex + patrolDirection;
        }

        currentWaypointIndex = nextWaypointIndex;
    }

    private void OnDisable()
    {
        if (body == null)
            return;

        StopMoving();
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
