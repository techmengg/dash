using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerMovement : MonoBehaviour
{
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

    [Header("Visual Effects")]
    public TrailRenderer dashTrail;
    public Image chargeFillImage;
    public GameObject aimIndicator;

    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float dashStaminaCost = 25f;
    public float staminaRegenRate = 15f;
    public Image staminaBarFill;
    public Image staminaPreviewFill;
    private float projectedCost = 0f;

    [Header("Status Effects")]
    public float knockbackForce = 10f;
    public float knockbackDuration = 0.15f;
    public float stunDuration = 1f;

    [Header("Charge Dash System")]
    public float maxChargeTime = 1f;
    public float maxChargeMultiplier = 2.5f;
    public float maxChargeStaminaCost = 50f;
    public float chargeDelay = 0.1f;

    private bool isCharging = false;
    private float currentChargeTime = 0f;
    [HideInInspector] public float currentDashMultiplier = 1f;

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
        if (!isCharging && !isDashing)
        {
            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);
            }
        }

        if (staminaPreviewFill != null)
        {
            staminaPreviewFill.fillAmount = currentStamina / maxStamina;
        }
        
        if (staminaBarFill != null)
        {
            staminaBarFill.fillAmount = Mathf.Max(0, currentStamina - projectedCost) / maxStamina;
        }

        CheckMouseAimDash();
        UpdateAimIndicator();

        if (isStunned || isDashing || isCharging) return;

        movement = moveAction.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        if (isStunned || isDashing || isCharging) return;
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }

    // DASH METHOD
    private void CheckMouseAimDash()
    {
        // START CHARGING
        if (dashAction.WasPressedThisFrame() && canDash && currentStamina >= dashStaminaCost && !isCharging)
        {
            isCharging = true;
            currentChargeTime = 0f;

            projectedCost = dashStaminaCost;

            if (chargeFillImage != null) chargeFillImage.fillAmount = 0f;
        }

        // BUILD CHARGE
        if (isCharging && dashAction.IsPressed())
        {
            float maxPossibleCharge = Mathf.Min(currentStamina, maxChargeStaminaCost);
            float possibleChargePercent = Mathf.InverseLerp(dashStaminaCost, maxChargeStaminaCost, maxPossibleCharge);
            float dynamicMaxChargeTime = maxChargeTime * possibleChargePercent;

            currentChargeTime += Time.deltaTime;
            currentChargeTime = Mathf.Clamp(currentChargeTime, 0f, dynamicMaxChargeTime);

            if (currentChargeTime >= chargeDelay)
            {
                float chargePercent = currentChargeTime / maxChargeTime;

                projectedCost = Mathf.Lerp(dashStaminaCost, maxChargeStaminaCost, chargePercent);

                if (chargeFillImage != null)
                {
                    chargeFillImage.fillAmount = currentChargeTime / maxChargeTime;
                }
            }
            
        }

        // EXECUTE DASH
        if (isCharging && dashAction.WasReleasedThisFrame())
        {
            isCharging = false;
            projectedCost = 0f;

            if (chargeFillImage != null) chargeFillImage.fillAmount = 0f;

            float finalMultiplier = 1f;
            float actualStaminaCost = dashStaminaCost;
            
            if (currentChargeTime > chargeDelay)
            {
                float chargePercent = currentChargeTime / maxChargeTime;
                finalMultiplier = Mathf.Lerp(1f, maxChargeMultiplier, chargePercent);
                actualStaminaCost = Mathf.Lerp(dashStaminaCost, maxChargeStaminaCost, chargePercent);
            }

            currentStamina -= actualStaminaCost;

            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
            Vector2 dashDirection = (mouseWorldPosition - (Vector2)transform.position).normalized;

            StartCoroutine(PerformDash(dashDirection, finalMultiplier));
        }
    }

    // --- THE ACTUAL DASH EXECUTION ---
    private IEnumerator PerformDash(Vector2 dashDirection, float multiplier)
    {
        isDashing = true;
        canDash = false;

        if (playerHealth != null) playerHealth.BecomeInvincible();
        if (dashTrail != null) dashTrail.emitting = true;

        float startTime = Time.time;
        float finalDashSpeed = dashSpeed * multiplier;

        while (Time.time < startTime + dashDuration)
        {
            rb.MovePosition(rb.position + dashDirection * finalDashSpeed * Time.fixedDeltaTime);
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

    private void UpdateAimIndicator()
    {
        if (aimIndicator ==  null) return;

        if (isDashing || isStunned)
        {
            aimIndicator.SetActive(false);
            return;
        }

        aimIndicator.SetActive(true);

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        Vector2 dashDirection = (mouseWorldPosition - (Vector2)transform.position).normalized;

        float currentMultiplier = 1f;

        if (isCharging)
        {
            float maxPossibleCharge = Mathf.Min(currentStamina, maxChargeStaminaCost);
            float possibleChargePercent = Mathf.InverseLerp(dashStaminaCost, maxChargeStaminaCost, maxPossibleCharge);
            float dynamicMaxChargeTime = maxChargeTime * possibleChargePercent;

            float clampedChargeTime = Mathf.Clamp(currentChargeTime, 0f, dynamicMaxChargeTime);

            if (clampedChargeTime > chargeDelay)
            {
                float chargePercent = clampedChargeTime / maxChargeTime;
                currentMultiplier = Mathf.Lerp(1f, maxChargeMultiplier, chargePercent);
            }
        }

        float expectedDistance = dashSpeed * currentMultiplier * dashDuration;
        Vector2 expectedEndPosition = (Vector2)transform.position + dashDirection * expectedDistance;

        aimIndicator.transform.position = expectedEndPosition;

        float angle = Mathf.Atan2(dashDirection.y, dashDirection.x) * Mathf.Rad2Deg;
        aimIndicator.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }
    
}