using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    
    [SerializeField]private float movementSpeed = 3f;

    private Rigidbody2D rb;
    private Vector2 inputMovement;
    private Vector2 filteredInput;
    private Animator animator;
    

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        Vector2 animationInput = IsMovementBlocked() ? Vector2.zero : inputMovement;

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
            : inputMovement * movementSpeed;
    }

    private static bool IsMovementBlocked()
    {
        return InventoryController.IsJournalOpen ||
               ScreenFade.IsTransitioning ||
               (DialogueController.Instance != null && DialogueController.Instance.IsDialogueActive);
    }

    public void playerMovement(InputAction.CallbackContext context)
    {
        inputMovement = context.ReadValue<Vector2>();
    }


}
