using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NPCMover), typeof(NPCPatrol), typeof(NPCFixedRoute))]
public class NPCMultiRoomPatrol : MonoBehaviour
{
    [Serializable]
    private class RoomStage
    {
        [Tooltip("Used only to make the schedule easier to read in the Inspector.")]
        public string roomName = string.Empty;

        [Tooltip("The first waypoint used when this room becomes active.")]
        public NPCWaypoint startingWaypoint = null;

        [Tooltip("The NPC leaves only after the timer expires and it next reaches this waypoint.")]
        public NPCWaypoint exitWaypoint = null;

        [Min(0f)] public float minimumPatrolSeconds = 20f;
        [Min(0f)] public float maximumPatrolSeconds = 40f;

        [Tooltip("Ordered fixed-route points leading from this room to the next room. Door points are supported.")]
        public Transform[] routeToNextRoom = Array.Empty<Transform>();

        public float GetPatrolDuration()
        {
            float minimum = Mathf.Max(0f, minimumPatrolSeconds);
            float maximum = Mathf.Max(minimum, maximumPatrolSeconds);
            return UnityEngine.Random.Range(minimum, maximum);
        }
    }

    [Header("Schedule")]
    [SerializeField] private bool beginOnStart = true;
    [SerializeField] private bool loopSchedule = true;
    [SerializeField] private RoomStage[] rooms = Array.Empty<RoomStage>();

    private NPCPatrol patrol;
    private NPCFixedRoute fixedRoute;
    private Coroutine roomTimerRoutine;
    private int currentRoomIndex;
    private bool isRunning;
    private bool mayLeaveCurrentRoom;

    public bool IsRunning => isRunning;
    public int CurrentRoomIndex => currentRoomIndex;

    private void Awake()
    {
        patrol = GetComponent<NPCPatrol>();
        fixedRoute = GetComponent<NPCFixedRoute>();
    }

    private void OnEnable()
    {
        patrol.WaypointReached += HandleWaypointReached;
        fixedRoute.RouteCompleted += HandleTransitionCompleted;
    }

    private void Start()
    {
        if (beginOnStart)
            BeginSchedule();
    }

    private void OnDisable()
    {
        patrol.WaypointReached -= HandleWaypointReached;
        fixedRoute.RouteCompleted -= HandleTransitionCompleted;

        if (roomTimerRoutine != null)
            StopCoroutine(roomTimerRoutine);

        roomTimerRoutine = null;
        isRunning = false;
        mayLeaveCurrentRoom = false;
    }

    public void BeginSchedule()
    {
        if (!ValidateSchedule())
            return;

        if (roomTimerRoutine != null)
            StopCoroutine(roomTimerRoutine);

        currentRoomIndex = 0;
        isRunning = true;
        BeginCurrentRoom();
    }

    public void StopSchedule()
    {
        if (roomTimerRoutine != null)
            StopCoroutine(roomTimerRoutine);

        roomTimerRoutine = null;
        isRunning = false;
        mayLeaveCurrentRoom = false;
    }

    private void BeginCurrentRoom()
    {
        RoomStage room = rooms[currentRoomIndex];
        mayLeaveCurrentRoom = false;
        patrol.BeginPatrolAt(room.startingWaypoint);

        if (!HasNextRoom())
            return;

        roomTimerRoutine = StartCoroutine(ArmRoomExitAfterDelay(room.GetPatrolDuration()));
    }

    private IEnumerator ArmRoomExitAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        roomTimerRoutine = null;
        mayLeaveCurrentRoom = true;

        RoomStage room = rooms[currentRoomIndex];
        if (patrol.CurrentNetworkWaypoint == room.exitWaypoint &&
            !patrol.LastWaypointWasBlockedAlternative)
        {
            BeginTransition();
        }
    }

    private void HandleWaypointReached(NPCWaypoint waypoint)
    {
        if (!isRunning || !mayLeaveCurrentRoom)
            return;

        if (waypoint == rooms[currentRoomIndex].exitWaypoint &&
            !patrol.LastWaypointWasBlockedAlternative)
        {
            BeginTransition();
        }
    }

    private void BeginTransition()
    {
        RoomStage room = rooms[currentRoomIndex];
        mayLeaveCurrentRoom = false;
        patrol.SuspendPatrol();

        if (fixedRoute.TryBeginRoute(room.routeToNextRoom, false))
            return;

        Debug.LogError(
            $"{gameObject.name} cannot leave {room.roomName}: its route to the next room is empty.",
            this);
        patrol.ResumePatrol();
        isRunning = false;
    }

    private void HandleTransitionCompleted()
    {
        if (!isRunning)
            return;

        currentRoomIndex++;
        if (currentRoomIndex >= rooms.Length)
        {
            if (!loopSchedule)
            {
                isRunning = false;
                return;
            }

            currentRoomIndex = 0;
        }

        BeginCurrentRoom();
    }

    private bool HasNextRoom()
    {
        return loopSchedule || currentRoomIndex < rooms.Length - 1;
    }

    private bool ValidateSchedule()
    {
        if (!patrol.UsesWaypointNetwork)
        {
            Debug.LogError($"{gameObject.name} must enable Use Waypoint Network on NPC Patrol.", this);
            return false;
        }

        if (rooms == null || rooms.Length == 0)
        {
            Debug.LogError($"{gameObject.name} has no multi-room patrol stages.", this);
            return false;
        }

        for (int index = 0; index < rooms.Length; index++)
        {
            RoomStage room = rooms[index];
            if (room == null || room.startingWaypoint == null)
            {
                Debug.LogError($"{gameObject.name} room stage {index + 1} needs a starting waypoint.", this);
                return false;
            }

            if (HasTransitionAfter(index) && room.exitWaypoint == null)
            {
                Debug.LogError($"{gameObject.name} room stage {index + 1} needs an exit waypoint.", this);
                return false;
            }

            if (HasTransitionAfter(index) &&
                (room.routeToNextRoom == null || room.routeToNextRoom.Length == 0))
            {
                Debug.LogError($"{gameObject.name} room stage {index + 1} needs a route to the next room.", this);
                return false;
            }
        }

        return true;
    }

    private bool HasTransitionAfter(int roomIndex)
    {
        return loopSchedule || roomIndex < rooms.Length - 1;
    }
}
