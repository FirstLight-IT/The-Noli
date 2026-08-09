using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(NPCMover))]
public class NPCFixedRoute : MonoBehaviour
{
    [Serializable]
    private class RouteDefinition
    {
        [SerializeField] private string routeId;
        [SerializeField] private Transform[] routePoints = new Transform[0];

        public string RouteId => routeId;
        public Transform[] RoutePoints => routePoints;
    }

    public event Action RouteCompleted;

    [Header("Test Controls")]
    [SerializeField] private bool followRouteOnStart;
    [SerializeField] private string routeToFollowOnStart;
    [Tooltip("Resume a sibling NPC Patrol after this scripted route finishes.")]
    [SerializeField] private bool resumePatrolAfterRoute = true;

    [Header("Routes")]
    [SerializeField] private RouteDefinition[] routes = new RouteDefinition[0];
    [SerializeField, Min(0f)] private float waitAtPoint;
    [SerializeField, Min(0f)] private float teleportArrivalTolerance = 0.25f;

    private NPCMover mover;
    private NPCPatrol patrol;
    private Transform[] activeRoutePoints;
    private Coroutine waitRoutine;
    private int currentPointIndex;
    private bool isFollowingRoute;
    private bool shouldResumePatrol;

    public bool IsFollowingRoute => isFollowingRoute;

    private void Awake()
    {
        mover = GetComponent<NPCMover>();
        TryGetComponent(out patrol);
    }

    private void OnEnable()
    {
        mover.Arrived += HandleArrived;
        mover.Teleported += HandleTeleported;
    }

    private void Start()
    {
        if (followRouteOnStart)
            BeginRoute();
    }

    private void OnDisable()
    {
        mover.Arrived -= HandleArrived;
        mover.Teleported -= HandleTeleported;

        if (waitRoutine != null)
            StopCoroutine(waitRoutine);

        waitRoutine = null;
        isFollowingRoute = false;
        shouldResumePatrol = false;
    }

    public void BeginRoute()
    {
        if (!string.IsNullOrWhiteSpace(routeToFollowOnStart))
        {
            TryBeginRoute(routeToFollowOnStart);
            return;
        }

        // Keep Route On Start convenient for NPCs with a single route. An ID is
        // only necessary when this component has multiple routes to choose from.
        if (routes != null && routes.Length == 1 && routes[0] != null)
        {
            TryBeginRoute(routes[0].RoutePoints, true);
            return;
        }

        Debug.LogWarning(
            $"{gameObject.name} needs a Route To Follow On Start because it does not have exactly one configured route.",
            this);
    }

    public bool HasConfiguredRoute(string routeId)
    {
        return TryGetRoute(routeId, out RouteDefinition route) &&
               route.RoutePoints != null &&
               route.RoutePoints.Length > 0;
    }

    public bool TryBeginRoute(string routeId)
    {
        if (!TryGetRoute(routeId, out RouteDefinition route))
        {
            Debug.LogWarning($"{gameObject.name} has no fixed NPC route with ID '{routeId}'.", this);
            return false;
        }

        return TryBeginRoute(route.RoutePoints, true);
    }

    public bool TryBeginRoute(Transform[] requestedRoutePoints, bool allowPatrolResume)
    {
        if (requestedRoutePoints == null || requestedRoutePoints.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} has no fixed NPC route points.", this);
            return false;
        }

        shouldResumePatrol = allowPatrolResume &&
                             resumePatrolAfterRoute &&
                             patrol != null &&
                             (patrol.IsPatrolling || patrol.IsSuspended || patrol.PatrolOnStart);

        if (patrol != null && patrol.IsPatrolling)
            patrol.SuspendPatrol();

        if (waitRoutine != null)
        {
            StopCoroutine(waitRoutine);
            waitRoutine = null;
        }

        currentPointIndex = 0;
        activeRoutePoints = requestedRoutePoints;
        isFollowingRoute = true;
        MoveToCurrentPoint();
        return true;
    }

    private bool TryGetRoute(string routeId, out RouteDefinition matchingRoute)
    {
        matchingRoute = null;

        if (string.IsNullOrWhiteSpace(routeId) || routes == null)
            return false;

        foreach (RouteDefinition route in routes)
        {
            if (route == null || !string.Equals(route.RouteId, routeId, StringComparison.Ordinal))
                continue;

            if (matchingRoute != null)
            {
                Debug.LogError($"{gameObject.name} contains duplicate fixed route ID '{routeId}'.", this);
                matchingRoute = null;
                return false;
            }

            matchingRoute = route;
        }

        return matchingRoute != null;
    }

    public void CancelRoute()
    {
        if (waitRoutine != null)
            StopCoroutine(waitRoutine);

        waitRoutine = null;
        isFollowingRoute = false;
        shouldResumePatrol = false;
        activeRoutePoints = null;
        mover.Stop();
    }

    private void HandleArrived()
    {
        if (!isFollowingRoute)
            return;

        Transform reachedPoint = currentPointIndex < activeRoutePoints.Length
            ? activeRoutePoints[currentPointIndex]
            : null;

        if (reachedPoint != null &&
            reachedPoint.TryGetComponent(out NPCDoorRoutePoint doorPoint))
        {
            if (doorPoint.Door == null)
            {
                Debug.LogError($"{reachedPoint.name} needs a Teleport Door reference.", doorPoint);
                CancelRoute();
                return;
            }

            if (!doorPoint.Door.TeleportNPC(mover))
            {
                Debug.LogError($"{gameObject.name} could not use {doorPoint.Door.name}.", this);
                CancelRoute();
            }

            return;
        }

        currentPointIndex++;
        ContinueAfterOptionalWait();
    }

    private void HandleTeleported(Transform arrivalPoint)
    {
        if (!isFollowingRoute)
            return;

        // Entering the trigger completes the route's door-entrance point.
        currentPointIndex++;

        // The teleport itself completes an explicitly listed arrival point.
        if (currentPointIndex < activeRoutePoints.Length &&
            IsMatchingArrivalPoint(activeRoutePoints[currentPointIndex], arrivalPoint))
        {
            currentPointIndex++;
        }

        MoveToCurrentPoint();
    }

    private bool IsMatchingArrivalPoint(Transform routePoint, Transform arrivalPoint)
    {
        if (routePoint == null || arrivalPoint == null)
            return false;

        return routePoint == arrivalPoint ||
               Vector2.Distance(routePoint.position, arrivalPoint.position) <= teleportArrivalTolerance;
    }

    private void ContinueAfterOptionalWait()
    {
        if (currentPointIndex >= activeRoutePoints.Length)
        {
            CompleteRoute();
            return;
        }

        if (waitAtPoint <= 0f)
        {
            MoveToCurrentPoint();
            return;
        }

        waitRoutine = StartCoroutine(WaitThenContinue());
    }

    private IEnumerator WaitThenContinue()
    {
        yield return new WaitForSeconds(waitAtPoint);
        waitRoutine = null;
        MoveToCurrentPoint();
    }

    private void MoveToCurrentPoint()
    {
        while (currentPointIndex < activeRoutePoints.Length && activeRoutePoints[currentPointIndex] == null)
            currentPointIndex++;

        if (currentPointIndex >= activeRoutePoints.Length)
        {
            CompleteRoute();
            return;
        }

        mover.MoveTo(activeRoutePoints[currentPointIndex]);
    }

    private void CompleteRoute()
    {
        mover.Stop();
        isFollowingRoute = false;
        activeRoutePoints = null;

        bool resumePatrol = shouldResumePatrol && patrol != null;
        shouldResumePatrol = false;

        if (resumePatrol)
            patrol.ResumePatrol();

        // External mission logic runs last, so it can override the resumed patrol.
        RouteCompleted?.Invoke();
    }
}
