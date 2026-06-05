using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    
    [SerializeField]private float movementSpeed = 3f;

    private Rigidbody2D rb;
    private Vector2 inputMovement;
    

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    
    void Update()
    {
        rb.linearVelocity = inputMovement * movementSpeed;
    }

    public void playerMovement(InputAction.CallbackContext context)
    {
        inputMovement = context.ReadValue<Vector2>();
    }


}
