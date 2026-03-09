using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

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
    public GameObject damagePopupPrefab;

    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float dashStaminaCost = 25f;
    public float staminaRegenRate = 15f;
    public float staminaOnEnemyKill = 12f;
    public Image staminaBarFill;
    public Image staminaPreviewFill;
    public TextMeshProUGUI staminaCostText;
    private float projectedCost = 0f;

    [Header("Status Effects")]
    public float knockbackForce = 2f;
    public float knockbackDuration = 0.15f;
    public float stunDuration = 1f;

    [Header("Charge Dash System")]
    public float maxChargeTime = 1f;
    public float maxChargeMultiplier = 2.5f;
    public float maxChargeStaminaCost = 50f;
    public float chargeDelay = 0.1f;
    [Range(0f, 1f)] public float chargeMoveSpeedMultiplier = 0.45f;
    private bool isCharging = false;
    private float currentChargeTime = 0f;
    [HideInInspector] public float currentDashMultiplier = 1f;

    [Header("Combat Settings")]
    public float baseDashDamage = 1f;
    public float baseKnockbackForce = 3f;
    public float comboDamageBonusPerHit = 0.1f;
    public float maxComboDamageMultiplier = 2f;
    private List<EnemyHealth> hitEnemiesThisDash = new List<EnemyHealth>();

    [Header("Combo System")]
    public float comboWindowTime = 1.5f;
    public float comboStaminaMultiplier = 0.5f;
    public float minimumDashCost = 15f;
    public float comboWindowIncreasePerSuccess = 0.1f;
    public float maxComboWindowTime = 2f;

    [Header("Combo UI")]
    public GameObject comboUIContainer;
    public Image comboTimerCircle;
    public TextMeshProUGUI comboHitText;

    private bool isComboWindowActive = false;
    private float currentComboTimer = 0f;
    private float currentComboWindowDuration;
    private bool hitEnemyThisDash = false;
    private float currentDiscount = 1f;
    private int currentComboCount = 0;

    private Vector2 movement;
    private bool isDashing = false;
    public bool IsDashing => isDashing;
    private bool canDash = true;
    public bool isStunned = false;
    private Collider2D physicsCollider;

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

        if (comboUIContainer != null) comboUIContainer.SetActive(false);
        currentComboWindowDuration = comboWindowTime;

        // Get the non-trigger collider for dash-through
        foreach (var col in GetComponents<Collider2D>())
        {
            if (!col.isTrigger)
            {
                physicsCollider = col;
                break;
            }
        }
    }

    void Update()
    {
        // COMBO TIMER LOGIC
        if (isComboWindowActive && !isDashing)
        {
            currentComboTimer -= Time.deltaTime;

            if (comboTimerCircle != null)
            {
                float denominator = Mathf.Max(0.01f, currentComboWindowDuration);
                comboTimerCircle.fillAmount = currentComboTimer / denominator;
            }
            
            if (currentComboTimer <= 0f)
            {
                isComboWindowActive = false;
                currentDiscount = 1f;
                currentComboCount = 0;
                currentComboWindowDuration = comboWindowTime;

                if (comboUIContainer != null) comboUIContainer.SetActive(false);

                if (playerHealth != null) playerHealth.RemoveInvincibility();
            }
        }

        if (!isCharging && !isDashing)
        {
            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);
            }
        }

        if (!isCharging)
        {
            projectedCost = 0f;
        }

        if (staminaPreviewFill != null)
        {
            staminaPreviewFill.fillAmount = currentStamina / maxStamina;
        }
        
        if (staminaBarFill != null)
        {
            staminaBarFill.fillAmount = Mathf.Max(0, currentStamina - projectedCost) / maxStamina;
        }

        if (staminaCostText != null)
        {
            if (isCharging)
            {
                staminaCostText.text = "-" + Mathf.RoundToInt(projectedCost).ToString();
            }
            else
            {
                staminaCostText.text = "";
            }
        }

        if (isStunned || isDashing)
        {
            movement = Vector2.zero;
            return;
        }

        CheckMouseAimDash();
        UpdateAimIndicator();

        if (isDashing)
        {
            movement = Vector2.zero;
            return;
        }

        movement = moveAction.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        if (isStunned || isDashing)
        {
            movement = Vector2.zero;
            return;
        }

        float currentMoveSpeed = moveSpeed;
        if (isCharging)
            currentMoveSpeed *= chargeMoveSpeedMultiplier;

        rb.MovePosition(rb.position + movement * currentMoveSpeed * Time.fixedDeltaTime);
    }

    // DASH METHOD
    private void CheckMouseAimDash()
    {
        float effectiveDashCost = dashStaminaCost * currentDiscount;

        if (effectiveDashCost < minimumDashCost)
        {
            effectiveDashCost = minimumDashCost;
        }

        float ratio = effectiveDashCost / dashStaminaCost;
        float effectiveMaxCost = maxChargeStaminaCost * ratio;

        // START CHARGING
        if (dashAction.WasPressedThisFrame() && canDash && currentStamina >= effectiveDashCost && !isCharging)
        {
            isCharging = true;
            currentChargeTime = 0f;
            movement = Vector2.zero;
            rb.linearVelocity = Vector2.zero;

            projectedCost = effectiveDashCost;
            if (chargeFillImage != null) chargeFillImage.fillAmount = 0f;
        }

        // BUILD CHARGE
        if (isCharging && dashAction.IsPressed())
        {
            float maxPossibleCharge = Mathf.Min(currentStamina, effectiveMaxCost);
            float possibleChargePercent = Mathf.InverseLerp(effectiveDashCost, effectiveMaxCost, maxPossibleCharge);
            float dynamicMaxChargeTime = maxChargeTime * possibleChargePercent;

            currentChargeTime += Time.deltaTime;
            currentChargeTime = Mathf.Clamp(currentChargeTime, 0f, dynamicMaxChargeTime);

            if (currentChargeTime >= chargeDelay)
            {
                float chargePercent = currentChargeTime / maxChargeTime;
                projectedCost = Mathf.Lerp(effectiveDashCost, effectiveMaxCost, chargePercent);

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

            currentDashMultiplier = 1f;
            float actualStaminaCost = effectiveDashCost;
            
            if (currentChargeTime > chargeDelay)
            {
                float chargePercent = currentChargeTime / maxChargeTime;
                currentDashMultiplier = Mathf.Lerp(1f, maxChargeMultiplier, chargePercent);
                actualStaminaCost = Mathf.Lerp(effectiveDashCost, effectiveMaxCost, chargePercent);
            }

            currentStamina -= actualStaminaCost;

            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
            Vector2 dashDirection = (mouseWorldPosition - (Vector2)transform.position).normalized;

            StartCoroutine(PerformDash(dashDirection, currentDashMultiplier));
        }
    }

    // --- THE ACTUAL DASH EXECUTION ---
    private IEnumerator PerformDash(Vector2 dashDirection, float multiplier)
    {
        isDashing = true;
        canDash = false;
        isComboWindowActive = false;
        hitEnemyThisDash = false;
        hitEnemiesThisDash.Clear();

        if (playerHealth != null) playerHealth.BecomeInvincible();
        if (dashTrail != null) dashTrail.emitting = true;
        if (physicsCollider != null) physicsCollider.isTrigger = true;

        float startTime = Time.time;
        float finalDashSpeed = dashSpeed * multiplier;

        while (Time.time < startTime + dashDuration)
        {
            rb.MovePosition(rb.position + dashDirection * finalDashSpeed * Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        if (dashTrail != null) dashTrail.emitting = false;
        if (physicsCollider != null) physicsCollider.isTrigger = false;
        isDashing = false;

        if (hitEnemyThisDash)
        {
            isComboWindowActive = true;
            currentComboWindowDuration = Mathf.Min(currentComboWindowDuration + comboWindowIncreasePerSuccess, maxComboWindowTime);
            currentComboTimer = currentComboWindowDuration;
            canDash = true;
            currentDiscount *= comboStaminaMultiplier;
            
            if (comboTimerCircle != null) comboTimerCircle.fillAmount = 1f;
        }
        else
        {
            isComboWindowActive = false;
            currentDiscount = 1f;
            currentComboCount = 0;
            currentComboWindowDuration = comboWindowTime;

            if (comboUIContainer != null) comboUIContainer.SetActive(false);
            if (playerHealth != null) playerHealth.RemoveInvincibility();

            float cooldownTimer = 0f;

            while (cooldownTimer < dashCooldown)
            {
                cooldownTimer += Time.deltaTime;
                yield return null;
            }

            canDash = true;
        }
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
            float effectiveDashCost = dashStaminaCost * currentDiscount;
            if (effectiveDashCost < minimumDashCost) effectiveDashCost = minimumDashCost;
            
            float ratio = effectiveDashCost / dashStaminaCost;
            float effectiveMaxCost = maxChargeStaminaCost * ratio;

            float maxPossibleCharge = Mathf.Min(currentStamina, effectiveMaxCost);
            float possibleChargePercent = Mathf.InverseLerp(effectiveDashCost, effectiveMaxCost, maxPossibleCharge);
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

    // Handles different collider setups (trigger/non-trigger, child/root collider).
    private EnemyHealth ResolveEnemyHealth(Collider2D collision)
    {
        if (collision == null)
            return null;

        EnemyHealth enemy = collision.GetComponent<EnemyHealth>();

        if (enemy == null && collision.attachedRigidbody != null)
            enemy = collision.attachedRigidbody.GetComponent<EnemyHealth>();

        if (enemy == null)
            enemy = collision.GetComponentInParent<EnemyHealth>();

        return enemy;
    }

    private float GetComboDamageMultiplier()
    {
        // First combo hit stays at base damage; later hits scale up.
        float comboBonus = Mathf.Max(0, currentComboCount - 1) * comboDamageBonusPerHit;
        return Mathf.Min(1f + comboBonus, maxComboDamageMultiplier);
    }

    private void TryDamageEnemy(Collider2D collision)
    {
        if (!isDashing)
            return;

        EnemyHealth enemy = ResolveEnemyHealth(collision);

        if (enemy == null || enemy.IsDead)
            return;

        if (!hitEnemiesThisDash.Contains(enemy))
        {
            hitEnemiesThisDash.Add(enemy);
            hitEnemyThisDash = true;

            currentComboCount++;
            if (comboHitText != null) comboHitText.text = currentComboCount.ToString() + " HITS";
            if (comboUIContainer != null) comboUIContainer.SetActive(true);

            float comboDamageMultiplier = GetComboDamageMultiplier();
            float damageDealt = baseDashDamage * currentDashMultiplier * comboDamageMultiplier;
            float knockbackDealt = baseKnockbackForce * currentDashMultiplier;
            Vector2 knockbackDirection = (enemy.transform.position - transform.position).normalized;
            bool wasAliveBeforeHit = !enemy.IsDead;
            
            enemy.TakeDamage(damageDealt, knockbackDirection, knockbackDealt);

            if (wasAliveBeforeHit && enemy.IsDead && staminaOnEnemyKill > 0f)
            {
                currentStamina = Mathf.Min(maxStamina, currentStamina + staminaOnEnemyKill);
            }

            if (damagePopupPrefab != null)
            {
                Vector3 spawnPosition = transform.position + new Vector3(0, 1f, 0);
                GameObject popup = Instantiate(damagePopupPrefab, spawnPosition, Quaternion.identity);
                popup.GetComponent<DamagePopup>().Setup(damageDealt);
            }
        }
    }

    // --- DAMAGE DETECTION (hitlist method) ---
    private void OnTriggerStay2D(Collider2D collision)
    {
        TryDamageEnemy(collision);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryDamageEnemy(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDamageEnemy(collision.collider);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamageEnemy(collision.collider);
    }
}