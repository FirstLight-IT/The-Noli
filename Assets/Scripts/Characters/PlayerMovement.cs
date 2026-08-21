using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeed = 3f;
    [SerializeField, Range(1f, 89f)] private float diagonalAngle = IsometricGeometry.GroundAngle;
    [SerializeField, Range(0f, 0.95f)] private float mobileDeadzone = 0.2f;

    private Rigidbody2D rb;
    private Vector2 inputMovement;
    private Vector2 movementDirection;
    private Vector2 filteredInput;
    private Animator animator;
    private StairsTrigger activeSlope;
    

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        movementDirection = activeSlope != null
            ? activeSlope.GetSlopeMovement(inputMovement, diagonalAngle)
            : ToIsometricDirection(inputMovement);

        Vector2 animationInput = IsMovementBlocked() ? Vector2.zero : movementDirection;

        if(animationInput != Vector2.zero)
        {
            animator.SetBool("isWalking", true);
            animator.SetFloat("inputX", animationInput.x);
            animator.SetFloat("inputY", animationInput.y);

            filteredInput = Vector2.MoveTowards(filteredInput, animationInput, Time.deltaTime * 12f);

            float snapX = filteredInput.x > 0.3f ? 1 : (filteredInput.x < -0.3f ? -1 : 0);
            float snapY = filteredInput.y > 0.3f ? 1 : (filteredInput.y < -0.3f ? -1 : 0);

            animator.SetFloat("lastInputX", snapX);
            animator.SetFloat("lastInputY", snapY);

        }
        else
        {
            animator.SetBool("isWalking", false);
            filteredInput = Vector2.zero;
        }

    }

    void FixedUpdate()
    {
        rb.linearVelocity = IsMovementBlocked()
            ? Vector2.zero
            : movementDirection * movementSpeed;
    }

    private static bool IsMovementBlocked()
    {
        return InventoryController.IsJournalOpen ||
               PauseMenuController.IsPaused ||
               ChapterController.IsChapterOpening ||
               ScreenFade.IsTransitioning ||
               AmbientNPC.IsHintCameraPanning ||
               (NarrationController.Instance != null && NarrationController.Instance.IsNarrationActive) ||
               (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) ||
               (ArtifactDialogueController.Instance != null && ArtifactDialogueController.Instance.IsDialogueActive);
    }

    public void playerMovement(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        inputMovement = ShouldUseDiscreteInput()
            ? QuantizeToEightWay(input, mobileDeadzone)
            : input;
    }

    private static bool ShouldUseDiscreteInput()
    {
#if UNITY_EDITOR
        // Allows the on-screen controls to be tested with a mouse in Play Mode.
        return true;
#else
        return Application.isMobilePlatform;
#endif
    }

    internal static Vector2 QuantizeToEightWay(Vector2 input, float deadzone)
    {
        if (input.magnitude <= deadzone)
        {
            return Vector2.zero;
        }

        const float DirectionStep = 45f;
        float inputAngle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        float snappedAngle = Mathf.Round(inputAngle / DirectionStep) * DirectionStep * Mathf.Deg2Rad;

        // A unit vector makes joystick distance irrelevant: movement is either
        // stopped or at the full configured speed.
        return new Vector2(Mathf.Cos(snappedAngle), Mathf.Sin(snappedAngle));
    }

    private Vector2 ToIsometricDirection(Vector2 input)
    {
        float inputMagnitude = Mathf.Clamp01(input.magnitude);
        if (inputMagnitude <= Mathf.Epsilon)
            return Vector2.zero;

        float angleInRadians = diagonalAngle * Mathf.Deg2Rad;
        Vector2 angledInput = new(
            input.x * Mathf.Cos(angleInRadians),
            input.y * Mathf.Sin(angleInRadians));

        return angledInput.normalized * inputMagnitude;
    }

    public void EnterSlope(StairsTrigger slope)
    {
        activeSlope = slope;
    }

    public void ExitSlope(StairsTrigger slope)
    {
        if (activeSlope == slope)
        {
            activeSlope = null;
        }
    }

}
