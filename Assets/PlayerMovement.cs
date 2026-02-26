using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public enum DashControlScheme 
    { 
        SpacebarDirection, 
        MouseAim, 
        DoubleTapWASD 
    }

    [Header("Testing Controls")]
    public DashControlScheme currentControlScheme = DashControlScheme.SpacebarDirection;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public float doubleTapTimeWindow = 0.3f; // Max time between taps

    [Header("References")]
    public Rigidbody2D rb;
    public InputAction moveAction;
    public InputAction dashAction; 

    private Vector2 movement;
    private Vector2 previousMovement; // Tracks if we just started moving
    private float lastTapTime;        // Tracks when the last key was pressed
    private Vector2 lastTapDirection; // Tracks which key was pressed last
    
    private bool isDashing = false;
    private bool canDash = true;

    [Header("Visual Effects")]
    public TrailRenderer dashTrail; // 1. Add a slot to hold the trail component

    private void OnEnable()
    {
        moveAction.Enable();
        dashAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        dashAction.Disable();
    }

    void Start()
    {
        // If we forgot to link it in the inspector, try to find it automatically
        if (dashTrail == null) dashTrail = GetComponent<TrailRenderer>();

        // Ensure the trail is OFF when the game starts
        if (dashTrail != null) dashTrail.emitting = false;
    }

    void Update()
    {
        if (isDashing) return;

        movement = moveAction.ReadValue<Vector2>();

        switch (currentControlScheme)
        {
            case DashControlScheme.SpacebarDirection:
                CheckSpacebarDash();
                break;
            case DashControlScheme.MouseAim:
                CheckMouseAimDash();
                break;
            case DashControlScheme.DoubleTapWASD:
                CheckDoubleTapDash();
                break;
        }

        // Save this frame's movement to compare against the next frame
        previousMovement = movement; 
    }

    void FixedUpdate()
    {
        if (isDashing) return;
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }

    // --- DASH DETECTION METHODS ---

    private void CheckSpacebarDash()
    {
        if (dashAction.WasPressedThisFrame() && canDash && movement != Vector2.zero)
        {
            StartCoroutine(PerformDash(movement.normalized));
        }
    }

    private void CheckMouseAimDash()
    {
        // Triggers when you press your Dash button (Space/Shift)
        if (dashAction.WasPressedThisFrame() && canDash)
        {
            // 1. Get the mouse's pixel position on your monitor
            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            
            // 2. Convert those screen pixels into game world coordinates
            Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
            
            // 3. Calculate the direction from the Player to the Mouse
            Vector2 dashDirection = (mouseWorldPosition - (Vector2)transform.position).normalized;

            StartCoroutine(PerformDash(dashDirection));
        }
    }

    private void CheckDoubleTapDash()
    {
        // Check if the player JUST pressed a movement key this exact frame
        if (movement != Vector2.zero && previousMovement == Vector2.zero)
        {
            // Did they press the same direction within 0.3 seconds?
            if (movement == lastTapDirection && Time.time - lastTapTime <= doubleTapTimeWindow && canDash)
            {
                StartCoroutine(PerformDash(movement.normalized));
                
                // Reset the timer so they can't triple-tap to bypass the cooldown
                lastTapTime = 0f; 
            }
            else
            {
                // If not a double tap, log this as the first tap
                lastTapDirection = movement;
                lastTapTime = Time.time;
            }
        }
    }

    // --- THE ACTUAL DASH EXECUTION ---

    private IEnumerator PerformDash(Vector2 dashDirection)
    {
        isDashing = true;
        canDash = false;

        // 2. Turn the trail ON right as the dash starts
        if (dashTrail != null) dashTrail.emitting = true;

        float startTime = Time.time;

        while (Time.time < startTime + dashDuration)
        {
            rb.MovePosition(rb.position + dashDirection * dashSpeed * Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        // 3. Turn the trail OFF as soon as the movement part ends
        if (dashTrail != null) dashTrail.emitting = false;

        isDashing = false;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }
}