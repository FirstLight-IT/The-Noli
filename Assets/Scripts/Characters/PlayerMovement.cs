using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeed = 3f;
    [SerializeField, Range(1f, 89f)] private float diagonalAngle = IsometricGeometry.GroundAngle;

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
               (NarrationController.Instance != null && NarrationController.Instance.IsNarrationActive) ||
               (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive) ||
               (ArtifactDialogueController.Instance != null && ArtifactDialogueController.Instance.IsDialogueActive);
    }

    public void playerMovement(InputAction.CallbackContext context)
    {
        inputMovement = context.ReadValue<Vector2>();
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
