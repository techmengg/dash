using UnityEngine;
using UnityEngine.InputSystem; // This is required for the new system!

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public Rigidbody2D rb;
    
    // This creates a customizable input slot in the Unity Inspector
    public InputAction moveAction;
    
    private Vector2 movement;

    // You must enable the action when the object is active
    private void OnEnable()
    {
        moveAction.Enable();
    }

    // You must disable it when the object is inactive to prevent memory leaks
    private void OnDisable()
    {
        moveAction.Disable();
    }

    void Update()
    {
        // This automatically reads the X and Y values from our keys as a Vector2
        movement = moveAction.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        // Physics movement stays exactly the same!
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }
}