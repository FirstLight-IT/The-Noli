using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NPCMover))]
public class NPCPatrol : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private bool patrolOnStart = true;
    [SerializeField, Min(0f)] private float waitAtWaypoint = 1f;

    [Header("Waypoint Network")]
    [SerializeField] private bool useWaypointNetwork;
    [SerializeField] private NPCWaypoint startingWaypoint;

    [Header("Simple Patrol")]
    [SerializeField] private bool pingPongPatrol;
    [SerializeField] private Transform[] waypoints = new Transform[0];

    private readonly List<NPCWaypoint> waypointChoices = new();

    private NPCMover mover;
    private Coroutine waitRoutine;
    private int currentWaypointIndex;
    private int patrolDirection = 1;
    private bool isPatrolling;
    private NPCWaypoint targetNetworkWaypoint;
    private NPCWaypoint lastReachedNetworkWaypoint;
    private NPCWaypoint previousNetworkWaypoint;

    public bool IsPatrolling => isPatrolling;
    public bool IsSuspended { get; private set; }
    public bool PatrolOnStart => patrolOnStart;

    private void Awake()
    {
        mover = GetComponent<NPCMover>();
    }

    private void OnEnable()
    {
        mover.Arrived += HandleArrived;
        mover.Blocked += HandleBlocked;
    }

    private void Start()
    {
        if (patrolOnStart &&
            (!TryGetComponent(out NPCFixedRoute fixedRoute) || !fixedRoute.IsFollowingRoute))
        {
            BeginPatrol();
        }
    }

    private void OnDisable()
    {
        mover.Arrived -= HandleArrived;
        mover.Blocked -= HandleBlocked;

        if (waitRoutine != null)
            StopCoroutine(waitRoutine);

        waitRoutine = null;
        isPatrolling = false;
        IsSuspended = false;
    }

    public void BeginPatrol()
    {
        if (TryGetComponent(out NPCFixedRoute fixedRoute) && fixedRoute.IsFollowingRoute)
            fixedRoute.CancelRoute();

        if (waitRoutine != null)
        {
            StopCoroutine(waitRoutine);
            waitRoutine = null;
        }

        currentWaypointIndex = 0;
        patrolDirection = 1;
        previousNetworkWaypoint = null;
        lastReachedNetworkWaypoint = null;
        targetNetworkWaypoint = useWaypointNetwork ? startingWaypoint : null;

        if (GetCurrentTarget() == null)
        {
            Debug.LogWarning($"{gameObject.name} has no valid NPC patrol starting point.", this);
            isPatrolling = false;
            return;
        }

        isPatrolling = true;
        IsSuspended = false;
        MoveToCurrentTarget();
    }

    public void CancelPatrol()
    {
        if (waitRoutine != null)
            StopCoroutine(waitRoutine);

        waitRoutine = null;
        isPatrolling = false;
        IsSuspended = false;
        mover.Stop();
    }

    public void SuspendPatrol()
    {
        if (!isPatrolling)
            return;

        if (waitRoutine != null)
            StopCoroutine(waitRoutine);

        waitRoutine = null;
        isPatrolling = false;
        IsSuspended = true;
        mover.Stop();
    }

    public void ResumePatrol()
    {
        if (!IsSuspended)
        {
            BeginPatrol();
            return;
        }

        IsSuspended = false;
        isPatrolling = true;
        MoveToCurrentTarget();
    }

    private void HandleArrived()
    {
        if (!isPatrolling)
            return;

        if (useWaypointNetwork)
        {
            previousNetworkWaypoint = lastReachedNetworkWaypoint;
            lastReachedNetworkWaypoint = targetNetworkWaypoint;
        }

        if (waitAtWaypoint <= 0f)
        {
            AdvancePatrol();
            return;
        }

        waitRoutine = StartCoroutine(WaitThenAdvance());
    }

    private IEnumerator WaitThenAdvance()
    {
        yield return new WaitForSeconds(waitAtWaypoint);
        waitRoutine = null;
        AdvancePatrol();
    }

    private void AdvancePatrol()
    {
        if (!isPatrolling)
            return;

        if (useWaypointNetwork)
        {
            NPCWaypoint nextWaypoint = ChooseNeighbour(null);
            targetNetworkWaypoint = nextWaypoint != null
                ? nextWaypoint
                : lastReachedNetworkWaypoint;
        }
        else
            AdvanceSimplePatrol();

        MoveToCurrentTarget();
    }

    private void HandleBlocked()
    {
        if (!isPatrolling || !useWaypointNetwork || lastReachedNetworkWaypoint == null)
            return;

        NPCWaypoint alternative = ChooseNeighbour(targetNetworkWaypoint);
        if (alternative == null)
            return;

        targetNetworkWaypoint = alternative;
        mover.MoveTo(targetNetworkWaypoint.transform);
    }

    private NPCWaypoint ChooseNeighbour(NPCWaypoint excludedDestination)
    {
        waypointChoices.Clear();

        if (lastReachedNetworkWaypoint == null || lastReachedNetworkWaypoint.Neighbours == null)
            return null;

        AddNeighbourChoices(previousNetworkWaypoint, excludedDestination);

        // Dead ends may return to the waypoint they came from.
        if (waypointChoices.Count == 0 &&
            previousNetworkWaypoint != null)
        {
            AddNeighbourChoices(null, excludedDestination);
        }

        if (waypointChoices.Count == 0)
            return null;

        return waypointChoices[Random.Range(0, waypointChoices.Count)];
    }

    private void AddNeighbourChoices(
        NPCWaypoint avoidBacktrackingTo,
        NPCWaypoint excludedDestination)
    {
        foreach (NPCWaypoint neighbour in lastReachedNetworkWaypoint.Neighbours)
        {
            if (neighbour != null &&
                neighbour != avoidBacktrackingTo &&
                neighbour != excludedDestination &&
                !mover.IsPathImmediatelyBlocked(neighbour.transform))
            {
                waypointChoices.Add(neighbour);
            }
        }
    }

    private Transform GetCurrentTarget()
    {
        if (useWaypointNetwork)
            return targetNetworkWaypoint != null ? targetNetworkWaypoint.transform : null;

        SkipNullSimpleWaypoints();
        return waypoints != null && waypoints.Length > 0
            ? waypoints[currentWaypointIndex]
            : null;
    }

    private void MoveToCurrentTarget()
    {
        Transform target = GetCurrentTarget();
        if (target == null)
        {
            CancelPatrol();
            return;
        }

        mover.MoveTo(target);
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

    private void SkipNullSimpleWaypoints()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        int checkedWaypoints = 0;
        while (waypoints[currentWaypointIndex] == null && checkedWaypoints < waypoints.Length)
        {
            AdvanceSimplePatrol();
            checkedWaypoints++;
        }
    }
}
