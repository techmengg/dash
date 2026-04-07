using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 3f;
    public float currentHealth = 3f;
    public Image healthBarFill;

    [Header("Passive Regen")]
    public bool enablePassiveRegen = true;
    public float passiveRegenPerSecond = 0.2f;

    [Header("References")]
    public SpriteRenderer spriteRenderer;
    public PlayerMovement playerMovement;

    [Header("Damage Feedback")]
    public float damageFlashDuration = 0.15f;
    public Color normalColor = Color.white;
    public Color damageColor = Color.red;
    public Color invincibleColor = Color.green;
    public Color deadColor = Color.black;

    [Header("Death Presentation")]
    public float deathRotationZ = 180f;

    [Header("Scene Routing")]
    public string menuSceneName = "Menu";

    [Header("Invincibility After Hit")]
    public float iFrameDuration = 1.2f;
    public float flickerInterval = 0.1f;

    public bool isInvincible = false;

    private Collider2D currentTrap = null;
    private bool isDead = false;
    private bool hasHitIFrames = false;
    private Coroutine flashCoroutine;
    private Coroutine iFrameCoroutine;
    private PlayerAudio playerAudio;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthUI();
        playerAudio = GetComponent<PlayerAudio>();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
        }
    }

    void Update()
    {
        if (!enablePassiveRegen || passiveRegenPerSecond <= 0f || isDead)
            return;

        if (currentHealth >= maxHealth)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + passiveRegenPerSecond * Time.deltaTime);
        UpdateHealthUI();
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

        bool isDashing = playerMovement != null && playerMovement.IsDashing;
        if (isDashing) return;

        isInvincible = false;

        // If player is still inside a trap when i-frames end, take damage
        if (currentTrap != null)
        {
            bool isStillInsideTrap = currentTrap.OverlapPoint(transform.position);
            if (isStillInsideTrap)
            {
                Vector2 knockbackDirection = ((Vector2)transform.position - (Vector2)currentTrap.transform.position).normalized;
                TakeDamage(1f, knockbackDirection);
            }
            else
            {
                currentTrap = null;
                SetColor(normalColor);
            }
        }
        else
        {
            SetColor(normalColor);
        }
    }

    /// <summary>
    /// Clears non-super temporary invincibility states (dash/combo/i-frames) for scripted encounter starts.
    /// </summary>
    public void ResetCombatInvincibility()
    {
        if (isDead)
            return;

        if (iFrameCoroutine != null)
        {
            StopCoroutine(iFrameCoroutine);
            iFrameCoroutine = null;
        }

        hasHitIFrames = false;
        isInvincible = false;
        currentTrap = null;
        SetColor(normalColor);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Trap"))
        {
            currentTrap = collision;

            bool isInSuper = playerMovement != null && playerMovement.isSuperActive;
            bool isDashing = playerMovement != null && playerMovement.IsDashing;

            if (!isInvincible && !isDead && !isInSuper && !isDashing)
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
            if (currentTrap == collision)
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
        bool isInSuper = playerMovement != null && playerMovement.isSuperActive;

        if (isDead || isInvincible || isInSuper) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        if (playerAudio == null)
            playerAudio = GetComponent<PlayerAudio>();

        if (playerAudio != null)
            playerAudio.PlayDamageSfx();

        if (playerMovement != null)
            playerMovement.CancelDashCharge();

        UpdateHealthUI();
        ShowDamageFlash();

        if (currentHealth <= 0)
        {
            StartCoroutine(DieCoroutine());
        }
        else
        {
            if (playerMovement != null && knockbackDirection.sqrMagnitude > 0.0001f)
            {
                playerMovement.ApplyKnockback(knockbackDirection);
            }

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
            bool useDeathAnimationColors = playerMovement != null && playerMovement.IsPlayingDeathAnimation;
            SetColor(useDeathAnimationColors ? normalColor : deadColor);
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

        if (playerAudio == null)
            playerAudio = GetComponent<PlayerAudio>();

        if (playerAudio != null)
            playerAudio.PlayDeathSfx();

        transform.rotation = Quaternion.Euler(0f, 0f, deathRotationZ);

        float deathAnimationTime = 0f;
        if (playerMovement != null)
        {
            deathAnimationTime = playerMovement.PlayDeathAnimation();
        }

        if (deathAnimationTime <= 0f)
        {
            SetColor(deadColor);
        }
        else
        {
            SetColor(normalColor);
        }

        if (playerMovement != null)
        {
            playerMovement.isStunned = true;
            playerMovement.rb.linearVelocity = Vector2.zero;
            playerMovement.rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        // Wait briefly for the death animation to play
        yield return new WaitForSeconds(Mathf.Max(0.5f, deathAnimationTime));

        // Launch the death screen — restart happens while screen is black
        bool restartTriggered = false;
        DeathScreen.Instance.Play(() =>
        {
            LoadMenuScene();
            restartTriggered = true;
        });

        // Wait until the restart callback has fired
        while (!restartTriggered)
            yield return null;

        // Wait a little extra for fade-from-black to start
        yield return new WaitForSeconds(0.3f);
    }

    private void LoadMenuScene()
    {
        if (!string.IsNullOrWhiteSpace(menuSceneName))
        {
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        SceneManager.LoadScene(0);
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

        // Flicker the sprite during i-frames so player knows they're invincible
        float elapsed = 0f;
        bool visible = true;
        while (elapsed < iFrameDuration)
        {
            elapsed += flickerInterval;
            visible = !visible;
            if (spriteRenderer != null)
                spriteRenderer.color = visible ? normalColor : new Color(normalColor.r, normalColor.g, normalColor.b, 0.3f);
            yield return new WaitForSeconds(flickerInterval);
        }

        // Restore full visibility
        if (spriteRenderer != null) SetColor(normalColor);

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