using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 3f;
    public float currentHealth = 3f;
    public float respawnDelay = 3f;

    [Header("References")]
    public SpriteRenderer spriteRenderer;
    public Rigidbody2D rb;

    [Header("Knockback Resistance")]
    [Range(0f, 1f)]
    public float knockbackResistance = 0.6f;
    public float maxKnockbackForce = 2f;

    [Header("Damage Feedback")]
    public float flashDuration = 0.12f;
    public Color damageFlashColor = Color.white;
    public Color normalColor = Color.white;

    [Header("UI")]
    public GameObject healthBarRoot;
    public Image healthBarFill;

    private Vector2 spawnPoint;
    private Collider2D[] colliders;
    private MonoBehaviour[] scripts;
    private Coroutine flashCoroutine;

    public bool IsDead { get; private set; }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        colliders = GetComponents<Collider2D>();
        scripts = GetComponents<MonoBehaviour>();

        spawnPoint = transform.position;
        currentHealth = maxHealth;

        if (healthBarRoot != null)
            healthBarRoot.SetActive(false);

        UpdateHealthBar();
    }

    public void SetSpawnPoint(Vector2 point)
    {
        spawnPoint = point;
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, Vector2.zero, 0f);
    }

    public void TakeDamage(float damage, Vector2 knockbackDirection, float knockbackForce)
    {
        if (IsDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        if (healthBarRoot != null)
            healthBarRoot.SetActive(true);

        UpdateHealthBar();
        ShowDamageFlash();

        if (rb != null && knockbackDirection != Vector2.zero && knockbackForce > 0f)
        {
            float reducedForce = knockbackForce * (1f - knockbackResistance);
            reducedForce = Mathf.Min(reducedForce, maxKnockbackForce);
            StartCoroutine(ApplyKnockback(knockbackDirection.normalized, reducedForce));
        }

        if (currentHealth <= 0f)
        {
            StartCoroutine(RespawnRoutine());
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBarFill != null)
            healthBarFill.fillAmount = currentHealth / maxHealth;
    }

    private void ShowDamageFlash()
    {
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(DamageFlashCoroutine());
    }

    private IEnumerator DamageFlashCoroutine()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = damageFlashColor;

        yield return new WaitForSeconds(flashDuration);

        if (!IsDead && spriteRenderer != null)
            spriteRenderer.color = normalColor;
    }

    [Header("Knockback Timing")]
    public float knockbackDuration = 0.15f;

    private IEnumerator ApplyKnockback(Vector2 direction, float force)
    {
        rb.linearVelocity = direction * force;
        yield return new WaitForSeconds(knockbackDuration);
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private IEnumerator RespawnRoutine()
    {
        IsDead = true;

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        foreach (var col in colliders)
            col.enabled = false;

        foreach (var script in scripts)
        {
            if (script != this)
                script.enabled = false;
        }

        if (healthBarRoot != null)
            healthBarRoot.SetActive(false);

        yield return new WaitForSeconds(respawnDelay);

        transform.position = spawnPoint;
        currentHealth = maxHealth;
        UpdateHealthBar();

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = normalColor;
        }

        foreach (var col in colliders)
            col.enabled = true;

        foreach (var script in scripts)
        {
            if (script != this)
                script.enabled = true;
        }

        IsDead = false;
    }
}