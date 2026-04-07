using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 3f;
    public float currentHealth = 3f;
    public float respawnDelay = 3f;
    public bool canRespawn = true;

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
    private EnemyAudio enemyAudio;
    private BossAudio bossAudio;
    private BossEnemy bossEnemy;

    public bool IsDead { get; set; }

    /// <summary>
    /// If set, this callback fires instead of RespawnRoutine when health reaches 0.
    /// Used by BossEnemy to intercept death for Phase 2 transition.
    /// </summary>
    public System.Action<EnemyHealth> onDeathOverride;

    public void SetDead(bool dead)
    {
        IsDead = dead;
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // Keep enemies color-neutral by default.
        normalColor = Color.white;
        if (spriteRenderer != null)
            spriteRenderer.color = normalColor;

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        enemyAudio = GetComponent<EnemyAudio>();
        if (enemyAudio == null)
            enemyAudio = gameObject.AddComponent<EnemyAudio>();

        bossAudio = GetComponent<BossAudio>();
        bossEnemy = GetComponent<BossEnemy>();

        colliders = GetComponents<Collider2D>();
        scripts = GetComponents<MonoBehaviour>();

        spawnPoint = transform.position;
        currentHealth = maxHealth;

        if (healthBarRoot != null)
            healthBarRoot.SetActive(false);

        UpdateHealthBar();

        if (enemyAudio != null)
            enemyAudio.PlaySpawnSfx();
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

        if (enemyAudio == null)
            enemyAudio = GetComponent<EnemyAudio>();

        if (bossAudio == null)
            bossAudio = GetComponent<BossAudio>();

        if (bossEnemy == null)
            bossEnemy = GetComponent<BossEnemy>();

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        bool playedBossDamageSfx = bossAudio != null && bossAudio.TryPlayBossDamageSfx();
        if (!playedBossDamageSfx && enemyAudio != null)
            enemyAudio.PlayDamageSfx();

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
            if (!IsDead)
            {
                IsDead = true;

                bool allowBossDeathSfx = bossEnemy == null || !bossEnemy.hasPhase2 || bossEnemy.CurrentPhase >= 2;
                bool playedBossDeathSfx = allowBossDeathSfx && bossAudio != null && bossAudio.TryPlayBossDeathSfx();

                if (!playedBossDeathSfx && enemyAudio != null)
                    enemyAudio.PlayDeathSfx();
            }

            if (onDeathOverride != null)
                onDeathOverride.Invoke(this);
            else if (!canRespawn)
                StartCoroutine(PermanentDeathRoutine());
            else
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

        if (enemyAudio == null)
            enemyAudio = GetComponent<EnemyAudio>();

        if (enemyAudio != null)
            enemyAudio.PlaySpawnSfx();

        IsDead = false;
    }

    private IEnumerator PermanentDeathRoutine()
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

        yield return null;
        Destroy(gameObject);
    }
}