using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerMovement : MonoBehaviour
{
    public enum DashControlScheme 
    { 
        SpacebarDirection, 
        MouseAim
    }

    [Header("Testing Controls")]
    public DashControlScheme currentControlScheme = DashControlScheme.SpacebarDirection;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    [Header("References")]
    public Rigidbody2D rb;
    public InputAction moveAction;
    public InputAction dashAction;
    public PlayerHealth playerHealth;
    public Animator anim;

    [Header("Visual Effects")]
    public TrailRenderer dashTrail;

    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float dashStaminaCost = 25f;
    public float staminaRegenRate = 15f;
    public Image staminaBarFill;

    [Header("Status Effects")]
    public float knockbackForce = 10f;
    public float knockbackDuration = 0.15f;
    public float stunDuration = 1f;

    private Vector2 movement;
    private bool isDashing = false;
    private bool canDash = true;
    public bool isStunned = false;

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
        if (dashTrail == null) dashTrail = GetComponent<TrailRenderer>();
        if (dashTrail != null) dashTrail.emitting = false;
    }

    void Update()
    {
        if (isStunned || isDashing) return;

        movement = moveAction.ReadValue<Vector2>();

        switch (currentControlScheme)
        {
            case DashControlScheme.SpacebarDirection:
                CheckSpacebarDash();
                break;
            case DashControlScheme.MouseAim:
                CheckMouseAimDash();
                break;
        }

        if (currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
        }
        
        if (staminaBarFill != null)
        {
            staminaBarFill.fillAmount = currentStamina / maxStamina;
        }

        if (anim != null)
        {
            anim.SetBool("isMoving", movement != Vector2.zero);
        }
    }

    void FixedUpdate()
    {
        if (isStunned || isDashing) return;
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }

    // --- DASH  METHODS ---
    private void CheckSpacebarDash()
    {
        if (dashAction.WasPressedThisFrame() && canDash && movement != Vector2.zero && currentStamina >= dashStaminaCost)
        {
            currentStamina -= dashStaminaCost;
            StartCoroutine(PerformDash(movement.normalized));
        }
    }

    private void CheckMouseAimDash()
    {
        // Triggers when you press your Dash button (Space/Shift)
        if (dashAction.WasPressedThisFrame() && canDash && currentStamina >= dashStaminaCost)
        {
            currentStamina -= dashStaminaCost;
            
            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
            Vector2 dashDirection = (mouseWorldPosition - (Vector2)transform.position).normalized;

            StartCoroutine(PerformDash(dashDirection));
        }
    }

    // --- THE ACTUAL DASH EXECUTION ---
    private IEnumerator PerformDash(Vector2 dashDirection)
    {
        isDashing = true;
        canDash = false;

        if (playerHealth != null) playerHealth.BecomeInvincible();
        if (dashTrail != null) dashTrail.emitting = true;

        float startTime = Time.time;

        while (Time.time < startTime + dashDuration)
        {
            rb.MovePosition(rb.position + dashDirection * dashSpeed * Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        if (playerHealth != null) playerHealth.RemoveInvincibility();
        if (dashTrail != null) dashTrail.emitting = false;

        isDashing = false;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    public void ApplyKnockback(Vector2 direction)
    {
        if (!isStunned)
        {
            StartCoroutine(KnockbackCoroutine(direction));
        }
    }

    // --- KNOCKBACK AND STUN EXECUTION ---
    private IEnumerator KnockbackCoroutine(Vector2 knockbackDirection)
    {
        isStunned = true;
        movement = Vector2.zero;

        float angle = Mathf.Atan2(knockbackDirection.y, knockbackDirection.x) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0, 0, angle);

        float startTime = Time.time;

        while (Time.time < startTime + knockbackDuration)
        {
            rb.MovePosition(rb.position + knockbackDirection * knockbackForce * Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        yield return new WaitForSeconds(stunDuration - knockbackDuration);

        transform.rotation = Quaternion.identity;

        isStunned = false;
    }

    
}