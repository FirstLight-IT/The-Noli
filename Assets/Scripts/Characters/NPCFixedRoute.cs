using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(NPCMover))]
public class NPCFixedRoute : MonoBehaviour
{
    public event Action RouteCompleted;

    [Header("Test Controls")]
    [SerializeField] private bool followRouteOnStart;

    [Header("Route")]
    [SerializeField] private Transform[] routePoints = new Transform[0];
    [SerializeField, Min(0f)] private float waitAtPoint;
    [SerializeField, Min(0f)] private float teleportArrivalTolerance = 0.25f;

    private NPCMover mover;
    private Coroutine waitRoutine;
    private int currentPointIndex;
    private bool isFollowingRoute;

    public bool IsFollowingRoute => isFollowingRoute;

    private void Awake()
    {
        mover = GetComponent<NPCMover>();
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
    }

    public void BeginRoute()
    {
        if (routePoints == null || routePoints.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} has no fixed NPC route points.", this);
            return;
        }

        if (waitRoutine != null)
        {
            StopCoroutine(waitRoutine);
            waitRoutine = null;
        }

        currentPointIndex = 0;
        isFollowingRoute = true;
        MoveToCurrentPoint();
    }

    public void CancelRoute()
    {
        if (waitRoutine != null)
            StopCoroutine(waitRoutine);

        waitRoutine = null;
        isFollowingRoute = false;
        mover.Stop();
    }

    private void HandleArrived()
    {
        if (!isFollowingRoute)
            return;

        Transform reachedPoint = currentPointIndex < routePoints.Length
            ? routePoints[currentPointIndex]
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
        if (currentPointIndex < routePoints.Length &&
            IsMatchingArrivalPoint(routePoints[currentPointIndex], arrivalPoint))
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
        if (currentPointIndex >= routePoints.Length)
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
        while (currentPointIndex < routePoints.Length && routePoints[currentPointIndex] == null)
            currentPointIndex++;

        if (currentPointIndex >= routePoints.Length)
        {
            CompleteRoute();
            return;
        }

        mover.MoveTo(routePoints[currentPointIndex]);
    }

    private void CompleteRoute()
    {
        mover.Stop();
        isFollowingRoute = false;
        RouteCompleted?.Invoke();
    }
}
