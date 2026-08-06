using System.Collections;
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

    [Header("Patrol")]
    [SerializeField] private bool patrolOnStart = true;
    [SerializeField] private bool pingPongPatrol;
    [SerializeField] private Transform[] waypoints = new Transform[0];

    private Rigidbody2D body;
    private RigidbodyConstraints2D movementConstraints;
    private ContactFilter2D obstacleFilter;
    private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[8];
    private int currentWaypointIndex;
    private int patrolDirection = 1;
    private bool isWaiting;
    private bool isPhysicsPaused;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.mass = bodyMass;
        movementConstraints = body.constraints | RigidbodyConstraints2D.FreezeRotation;
        body.constraints = movementConstraints;

        obstacleFilter = new ContactFilter2D
        {
            useTriggers = false
        };
        obstacleFilter.SetLayerMask(Physics2D.AllLayers);
    }

    private void OnEnable()
    {
        currentWaypointIndex = 0;
        patrolDirection = 1;
        isWaiting = false;
        isPhysicsPaused = false;
    }

    private void FixedUpdate()
    {
        if (IsMovementPaused())
        {
            PausePhysics();
            return;
        }

        ResumePhysics();

        if (!patrolOnStart || isWaiting || waypoints == null || waypoints.Length == 0)
        {
            StopMoving();
            return;
        }

        Transform target = waypoints[currentWaypointIndex];
        if (target == null)
        {
            StopMoving();
            AdvanceToNextWaypoint();
            return;
        }

        Vector2 currentPosition = body.position;
        Vector2 targetPosition = target.position;

        if (Vector2.Distance(currentPosition, targetPosition) <= arrivalDistance)
        {
            body.position = targetPosition;
            StopMoving();
            StartCoroutine(WaitThenAdvance());
            return;
        }

        Vector2 direction = (targetPosition - currentPosition).normalized;
        float movementDistance = movementSpeed * Time.fixedDeltaTime;

        if (HasObstacleAhead(direction, movementDistance + obstacleClearance))
        {
            StopMoving();
            return;
        }

        body.linearVelocity = direction * movementSpeed;
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

        if (waypoints == null || waypoints.Length == 0)
            return;

        AdvanceToNextWaypoint();
    }

    private void OnDisable()
    {
        if (body == null)
            return;

        StopMoving();
        body.constraints = movementConstraints;
        isPhysicsPaused = false;
    }

    private IEnumerator WaitThenAdvance()
    {
        isWaiting = true;

        if (waitAtWaypoint > 0f)
            yield return new WaitForSeconds(waitAtWaypoint);

        AdvanceToNextWaypoint();
        isWaiting = false;
    }

    private void AdvanceToNextWaypoint()
    {
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

    private void Reset()
    {
        Rigidbody2D rigidbody = GetComponent<Rigidbody2D>();
        rigidbody.mass = bodyMass;
        rigidbody.gravityScale = 0f;
        rigidbody.freezeRotation = true;
    }
}
