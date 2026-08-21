using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[RequireComponent(typeof(NPCMover))]
public class NPCPatrol : MonoBehaviour
{
    private const float BlockedRerouteRetryDelay = 0.25f;

    public event Action<NPCWaypoint> WaypointReached;

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
    private bool targetIsBlockedAlternative;
    private bool isReturningToReroute;
    private NPCWaypoint blockedDestinationToAvoid;
    private NPCWaypoint navigationDestination;

    public bool IsPatrolling => isPatrolling;
    public bool IsSuspended { get; private set; }
    public bool PatrolOnStart => patrolOnStart;
    public bool UsesWaypointNetwork => useWaypointNetwork;
    public NPCWaypoint CurrentNetworkWaypoint => lastReachedNetworkWaypoint;
    public bool LastWaypointWasBlockedAlternative { get; private set; }
    public NPCWaypoint NavigationDestination => navigationDestination;

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
        BeginPatrolAt(useWaypointNetwork ? startingWaypoint : null);
    }

    public void BeginPatrolAt(NPCWaypoint networkStartingWaypoint)
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
        targetIsBlockedAlternative = false;
        LastWaypointWasBlockedAlternative = false;
        isReturningToReroute = false;
        blockedDestinationToAvoid = null;
        navigationDestination = null;
        targetNetworkWaypoint = useWaypointNetwork
            ? networkStartingWaypoint != null ? networkStartingWaypoint : startingWaypoint
            : null;

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

    public bool TrySetNavigationDestination(NPCWaypoint destination)
    {
        if (!useWaypointNetwork || destination == null)
            return false;

        navigationDestination = destination;
        return true;
    }

    public void ClearNavigationDestination()
    {
        navigationDestination = null;
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

        if (useWaypointNetwork && isReturningToReroute)
        {
            isReturningToReroute = false;
            TryContinueBlockedReroute();
            return;
        }

        if (useWaypointNetwork)
        {
            previousNetworkWaypoint = lastReachedNetworkWaypoint;
            lastReachedNetworkWaypoint = targetNetworkWaypoint;
            LastWaypointWasBlockedAlternative = targetIsBlockedAlternative;
            WaypointReached?.Invoke(lastReachedNetworkWaypoint);

            if (!isPatrolling)
                return;
        }

        float waitDuration = GetCurrentWaypointWaitTime();
        if (waitDuration <= 0f)
        {
            AdvancePatrol();
            return;
        }

        waitRoutine = StartCoroutine(WaitThenAdvance(waitDuration));
    }

    private IEnumerator WaitThenAdvance(float waitDuration)
    {
        yield return new WaitForSeconds(waitDuration);
        waitRoutine = null;
        AdvancePatrol();
    }

    private float GetCurrentWaypointWaitTime()
    {
        if (useWaypointNetwork)
        {
            return lastReachedNetworkWaypoint != null
                ? lastReachedNetworkWaypoint.GetWaitTime(waitAtWaypoint)
                : waitAtWaypoint;
        }

        Transform reachedWaypoint = waypoints != null &&
                                    currentWaypointIndex >= 0 &&
                                    currentWaypointIndex < waypoints.Length
            ? waypoints[currentWaypointIndex]
            : null;

        return reachedWaypoint != null &&
               reachedWaypoint.TryGetComponent(out NPCWaypoint waypoint)
            ? waypoint.GetWaitTime(waitAtWaypoint)
            : waitAtWaypoint;
    }

    private void AdvancePatrol()
    {
        if (!isPatrolling)
            return;

        if (useWaypointNetwork)
        {
            NPCWaypoint nextWaypoint;

            if (navigationDestination == null)
            {
                nextWaypoint = ChooseNeighbour(null);
            }
            else if (lastReachedNetworkWaypoint == navigationDestination)
            {
                navigationDestination = null;
                nextWaypoint = lastReachedNetworkWaypoint;
            }
            else
            {
                TryFindNextStep(
                    lastReachedNetworkWaypoint,
                    navigationDestination,
                    null,
                    out nextWaypoint);
            }

            targetNetworkWaypoint = nextWaypoint != null
                ? nextWaypoint
                : lastReachedNetworkWaypoint;
            targetIsBlockedAlternative = false;
        }
        else
            AdvanceSimplePatrol();

        MoveToCurrentTarget();
    }

    private void HandleBlocked()
    {
        if (!isPatrolling ||
            !useWaypointNetwork ||
            lastReachedNetworkWaypoint == null ||
            isReturningToReroute)
        {
            return;
        }

        blockedDestinationToAvoid = targetNetworkWaypoint;
        isReturningToReroute = true;
        targetNetworkWaypoint = lastReachedNetworkWaypoint;
        MoveAlongWaypointEdge(lastReachedNetworkWaypoint.transform);
    }

    private void TryContinueBlockedReroute()
    {
        NPCWaypoint alternative = navigationDestination != null &&
                                  TryFindNextStep(
                                      lastReachedNetworkWaypoint,
                                      navigationDestination,
                                      blockedDestinationToAvoid,
                                      out NPCWaypoint directedAlternative)
            ? directedAlternative
            : navigationDestination == null
                ? ChooseNeighbour(blockedDestinationToAvoid)
                : null;
        if (alternative == null)
        {
            waitRoutine = StartCoroutine(RetryBlockedReroute());
            return;
        }

        targetNetworkWaypoint = alternative;
        targetIsBlockedAlternative = true;
        MoveAlongWaypointEdge(targetNetworkWaypoint.transform);
    }

    private bool TryFindNextStep(
        NPCWaypoint start,
        NPCWaypoint destination,
        NPCWaypoint excludedFirstStep,
        out NPCWaypoint nextStep)
    {
        nextStep = null;

        if (start == null || destination == null || start == destination)
            return false;

        Queue<NPCWaypoint> queue = new();
        Dictionary<NPCWaypoint, NPCWaypoint> previous = new();
        queue.Enqueue(start);
        previous.Add(start, null);

        while (queue.Count > 0)
        {
            NPCWaypoint current = queue.Dequeue();
            if (current.Neighbours == null)
                continue;

            foreach (NPCWaypoint neighbour in current.Neighbours)
            {
                if (neighbour == null || previous.ContainsKey(neighbour))
                    continue;

                if (current == start &&
                    (neighbour == excludedFirstStep || mover.IsPathImmediatelyBlocked(neighbour.transform)))
                {
                    continue;
                }

                previous.Add(neighbour, current);

                if (neighbour == destination)
                {
                    NPCWaypoint step = destination;
                    while (previous[step] != start)
                        step = previous[step];

                    nextStep = step;
                    return true;
                }

                queue.Enqueue(neighbour);
            }
        }

        return false;
    }

    private IEnumerator RetryBlockedReroute()
    {
        yield return new WaitForSeconds(BlockedRerouteRetryDelay);
        waitRoutine = null;

        if (isPatrolling)
            TryContinueBlockedReroute();
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

        return waypointChoices[UnityEngine.Random.Range(0, waypointChoices.Count)];
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

        if (useWaypointNetwork)
        {
            MoveAlongWaypointEdge(target);
            return;
        }

        mover.MoveTo(target);
    }

    private void MoveAlongWaypointEdge(Transform target)
    {
        // A waypoint-network destination must stay on its selected graph edge.
        // Free-form and fixed routes may still use isometric corner splitting.
        mover.MoveDirectlyTo(target);
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
