using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 3f;
    public float currentHealth = 3f;
    public Image healthBarFill;

    [Header("References")]
    public SpriteRenderer spriteRenderer;
    public PlayerMovement playerMovement;

    [Header("Damage Feedback")]
    public float damageFlashDuration = 0.15f;
    public Color normalColor = Color.white;
    public Color damageColor = Color.red;
    public Color invincibleColor = Color.green;
    public Color deadColor = Color.black;

    [Header("Invincibility After Hit")]
    public float iFrameDuration = 0.75f;

    public bool isInvincible = false;

    private Transform currentTrap = null;
    private bool isDead = false;
    private bool hasHitIFrames = false;
    private Coroutine flashCoroutine;
    private Coroutine iFrameCoroutine;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthUI();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
        }
    }

    public void BecomeInvincible()
    {
        if (isDead) return;

        isInvincible = true;
        SetColor(invincibleColor);
    }

    public void RemoveInvincibility()
    {
        if (isDead) return;

        // Don't remove invincibility if hit i-frames are still active
        if (hasHitIFrames) return;

        isInvincible = false;

        // If player is still inside a trap when i-frames end, take damage
        if (currentTrap != null)
        {
            Vector2 knockbackDirection = ((Vector2)transform.position - (Vector2)currentTrap.position).normalized;
            TakeDamage(1f, knockbackDirection);
        }
        else
        {
            SetColor(normalColor);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Trap"))
        {
            currentTrap = collision.transform;

            if (!isInvincible && !isDead)
            {
                Vector2 knockbackDirection = ((Vector2)transform.position - (Vector2)collision.transform.position).normalized;
                TakeDamage(1f, knockbackDirection);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Trap"))
        {
            if (currentTrap == collision.transform)
            {
                currentTrap = null;
            }

            if (!isInvincible && !isDead)
            {
                SetColor(normalColor);
            }
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, Vector2.zero);
    }

    public void TakeDamage(float amount, Vector2 knockbackDirection)
    {
        if (isDead || isInvincible) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        UpdateHealthUI();
        ShowDamageFlash();

        if (currentHealth <= 0)
        {
            StartCoroutine(DieCoroutine());
        }
        else
        {
            if (iFrameCoroutine != null) StopCoroutine(iFrameCoroutine);
            iFrameCoroutine = StartCoroutine(HitIFrameCoroutine());
        }
    }

    private void UpdateHealthUI()
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = currentHealth / maxHealth;
        }
    }

    private void ShowDamageFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(DamageFlashCoroutine());
    }

    private IEnumerator DamageFlashCoroutine()
    {
        SetColor(damageColor);
        yield return new WaitForSeconds(damageFlashDuration);

        if (isDead)
        {
            SetColor(deadColor);
        }
        else if (isInvincible)
        {
            SetColor(invincibleColor);
        }
        else
        {
            SetColor(normalColor);
        }
    }

    private IEnumerator DieCoroutine()
    {
        isDead = true;
        SetColor(deadColor);

        if (playerMovement != null)
        {
            playerMovement.isStunned = true;
            playerMovement.rb.linearVelocity = Vector2.zero;
            playerMovement.rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        yield return new WaitForSeconds(2f);

        currentHealth = maxHealth;
        UpdateHealthUI();

        isDead = false;
        isInvincible = false;
        currentTrap = null;

        SetColor(normalColor);

        if (playerMovement != null)
        {
            playerMovement.rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            playerMovement.isStunned = false;
        }
    }

    private void SetColor(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    private IEnumerator HitIFrameCoroutine()
    {
        hasHitIFrames = true;
        isInvincible = true;

        yield return new WaitForSeconds(iFrameDuration);

        hasHitIFrames = false;

        // Only remove invincibility if dash isn't also granting it
        if (playerMovement == null || !playerMovement.IsDashing)
        {
            isInvincible = false;
            if (!isDead) SetColor(normalColor);
        }
    }

    public bool IsDead()
    {
        return isDead;
    }
}