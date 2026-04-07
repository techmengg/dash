using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(EnemyHealth))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("General")]
    public float moveSpeed = 2f;
    public float detectionRange = 8f;
    public int contactDamage = 1;

    [Header("Trap Avoidance")]
    public float trapCheckRadius = 0.8f;
    public float trapAvoidWeight = 0.8f;
    public LayerMask trapLayer;

    [Header("Wall Navigation")]
    public LayerMask wallLayer;
    public float wallCheckDist = 1.5f;
    private float stuckTimer = 0f;
    private Vector2 lastPosition;
    private Vector2 wallSlideDir = Vector2.zero;

    protected Transform player;
    protected Rigidbody2D rb;
    protected SpriteRenderer sr;
    protected EnemyHealth health;
    protected EnemyAudio enemyAudio;
    protected PlayerHealth playerHealth;
    protected Vector2 spawnPoint;
    protected bool isStunned = false;
    private Coroutine activeStunCoroutine;
    protected PlayerMovement playerMovement;

    // Trap avoidance throttling
    private Vector2 cachedTrapAvoidance;
    private int lastTrapCheckFrame = -1;
    private const int trapCheckInterval = 5; // check every 5 physics frames
    private Color stunOriginalColor = Color.white;
    private bool hasStunOriginalColor;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = Color.white;

        health = GetComponent<EnemyHealth>();
        enemyAudio = GetComponent<EnemyAudio>();
        if (enemyAudio == null)
            enemyAudio = gameObject.AddComponent<EnemyAudio>();
        spawnPoint = transform.position;
        lastPosition = transform.position;
    }

    protected void SetContactAttackSfxActive(bool isActive)
    {
        if (enemyAudio == null)
            return;

        enemyAudio.SetAttackLoopActive(isActive);
    }

    protected void PlayProjectileAttackSfx()
    {
        if (enemyAudio != null)
            enemyAudio.PlayProjectileFireSfx();
    }

    protected virtual void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
            playerMovement = playerObj.GetComponent<PlayerMovement>();
        }

        if (health != null)
            health.SetSpawnPoint(spawnPoint);
    }

    protected Vector2 GetDirectionToPlayer()
    {
        if (player == null) return Vector2.zero;
        return ((Vector2)player.position - rb.position).normalized;
    }

    protected Vector2 GetTrapAvoidance()
    {
        // Throttle: only run physics query every N fixed frames
        int frame = Time.frameCount;
        if (frame - lastTrapCheckFrame < trapCheckInterval)
            return cachedTrapAvoidance;
        lastTrapCheckFrame = frame;

        Collider2D[] nearbyTraps = Physics2D.OverlapCircleAll(transform.position, trapCheckRadius, trapLayer);
        if (nearbyTraps.Length == 0)
        {
            cachedTrapAvoidance = Vector2.zero;
            return cachedTrapAvoidance;
        }

        Vector2 avoid = Vector2.zero;

        foreach (Collider2D trap in nearbyTraps)
        {
            Vector2 away = (Vector2)transform.position - (Vector2)trap.transform.position;
            float dist = away.magnitude;

            if (dist < 0.001f) continue;

            float weight = Mathf.Clamp01((trapCheckRadius - dist) / trapCheckRadius);
            avoid += away.normalized * weight;
        }

        cachedTrapAvoidance = avoid.normalized * trapAvoidWeight;
        return cachedTrapAvoidance;
    }

    protected void MoveWithAvoidance(Vector2 desiredDirection)
    {
        Vector2 finalDir = (desiredDirection + GetTrapAvoidance()).normalized;

        // Detect if stuck (barely moving for too long)
        float movedDist = Vector2.Distance(rb.position, lastPosition);
        if (movedDist < 0.01f)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;
        lastPosition = rb.position;

        // Raycast ahead to check for walls
        RaycastHit2D hitCenter = Physics2D.Raycast(rb.position, finalDir, wallCheckDist, wallLayer);

        if (hitCenter.collider != null || stuckTimer > 0.15f)
        {
            // Wall ahead or stuck — find a way around
            Vector2 perpRight = new Vector2(finalDir.y, -finalDir.x);
            Vector2 perpLeft = new Vector2(-finalDir.y, finalDir.x);

            RaycastHit2D hitRight = Physics2D.Raycast(rb.position, perpRight, wallCheckDist, wallLayer);
            RaycastHit2D hitLeft = Physics2D.Raycast(rb.position, perpLeft, wallCheckDist, wallLayer);

            bool rightClear = hitRight.collider == null;
            bool leftClear = hitLeft.collider == null;

            if (rightClear && leftClear)
            {
                // Both clear — pick the one more aligned with the player
                if (player != null)
                {
                    Vector2 toPlayer = ((Vector2)player.position - rb.position).normalized;
                    finalDir = Vector2.Dot(toPlayer, perpRight) > 0 ? perpRight : perpLeft;
                }
                else
                {
                    finalDir = perpRight;
                }
            }
            else if (rightClear)
            {
                finalDir = perpRight;
            }
            else if (leftClear)
            {
                finalDir = perpLeft;
            }
            else
            {
                // Both sides blocked — try backing up slightly
                finalDir = -finalDir;
            }

            // Blend slide direction for smoother movement
            wallSlideDir = Vector2.Lerp(wallSlideDir, finalDir, 0.3f).normalized;
            finalDir = wallSlideDir;

            // Reset stuck timer when we pick a new direction
            if (stuckTimer > 0.15f) stuckTimer = 0f;
        }
        else
        {
            // No wall — decay the slide direction
            wallSlideDir = Vector2.zero;
        }

        Vector2 targetPos = rb.position + finalDir * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPos);
    }

    protected bool IsPlayerDead()
    {
        return playerHealth != null && playerHealth.IsDead();
    }

    public virtual void Stun(float duration)
    {
        if (!isActiveAndEnabled)
            return;

        if (health != null && health.IsDead)
            return;

        if (activeStunCoroutine != null)
            StopCoroutine(activeStunCoroutine);

        activeStunCoroutine = StartCoroutine(StunForDuration(duration));
    }

    protected virtual IEnumerator StunForDuration(float duration)
    {
        isStunned = true;

        SetContactAttackSfxActive(false);

        if (enemyAudio != null)
            enemyAudio.PlayStunSfx();

        if (sr != null)
        {
            if (!hasStunOriginalColor)
            {
                stunOriginalColor = sr.color;
                hasStunOriginalColor = true;
            }

            sr.color = Color.yellow;
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));

        isStunned = false;

        if (sr != null && hasStunOriginalColor)
            sr.color = stunOriginalColor;

        activeStunCoroutine = null;
    }

    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        if (!TryResolvePlayerCollision(collision, out PlayerHealth targetHealth, out PlayerMovement targetMovement, out bool isFeetHitbox))
            return;

        if (isFeetHitbox)
            return;

        if (isStunned || IsPlayerDead())
        {
            SetContactAttackSfxActive(false);
            return;
        }

        if (targetMovement != null && targetMovement.IsDashing)
        {
            SetContactAttackSfxActive(false);
            return;
        }

        if (targetHealth == null)
        {
            SetContactAttackSfxActive(false);
            return;
        }

        SetContactAttackSfxActive(true);
        Vector2 knockDir = (collision.transform.position - transform.position).normalized;
        targetHealth.TakeDamage(contactDamage, knockDir);
    }

    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        if (!TryResolvePlayerCollider(other, out PlayerHealth targetHealth, out PlayerMovement targetMovement, out bool isFeetHitbox))
            return;

        if (isFeetHitbox)
            return;

        if (isStunned || IsPlayerDead())
        {
            SetContactAttackSfxActive(false);
            return;
        }

        if (targetMovement != null && targetMovement.IsDashing)
        {
            SetContactAttackSfxActive(false);
            return;
        }

        SetContactAttackSfxActive(true);
        Vector2 knockDir = (other.transform.position - transform.position).normalized;
        targetHealth.TakeDamage(contactDamage, knockDir);
    }

    protected virtual void OnCollisionExit2D(Collision2D collision)
    {
        if (!TryResolvePlayerCollision(collision, out _, out _, out _))
            return;

        SetContactAttackSfxActive(false);
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (!TryResolvePlayerCollider(other, out _, out _, out _))
            return;

        SetContactAttackSfxActive(false);
    }

    private bool TryResolvePlayerCollision(
        Collision2D collision,
        out PlayerHealth targetHealth,
        out PlayerMovement targetMovement,
        out bool isFeetHitbox)
    {
        targetHealth = null;
        targetMovement = null;
        isFeetHitbox = false;

        if (collision == null)
            return false;

        Collider2D col = collision.otherCollider;
        if (col == null)
            col = collision.collider;

        if (col == null)
            return false;

        return TryResolvePlayerCollider(col, out targetHealth, out targetMovement, out isFeetHitbox);
    }

    private bool TryResolvePlayerCollider(
        Collider2D col,
        out PlayerHealth targetHealth,
        out PlayerMovement targetMovement,
        out bool isFeetHitbox)
    {
        targetHealth = null;
        targetMovement = null;
        isFeetHitbox = false;

        if (col == null)
            return false;

        PlayerHitbox2D hitbox = col.GetComponent<PlayerHitbox2D>();
        if (hitbox == null)
            hitbox = col.GetComponentInParent<PlayerHitbox2D>();

        if (hitbox != null)
        {
            targetHealth = hitbox.playerHealth != null ? hitbox.playerHealth : hitbox.GetComponentInParent<PlayerHealth>();
            targetMovement = hitbox.playerMovement != null ? hitbox.playerMovement : hitbox.GetComponentInParent<PlayerMovement>();
            isFeetHitbox = hitbox.IsFeetHitbox;
            return targetHealth != null;
        }

        targetHealth = col.GetComponent<PlayerHealth>();
        if (targetHealth == null)
            targetHealth = col.GetComponentInParent<PlayerHealth>();

        if (targetHealth == null && col.attachedRigidbody != null)
            targetHealth = col.attachedRigidbody.GetComponent<PlayerHealth>();

        targetMovement = col.GetComponent<PlayerMovement>();
        if (targetMovement == null)
            targetMovement = col.GetComponentInParent<PlayerMovement>();

        if (targetMovement == null && col.attachedRigidbody != null)
            targetMovement = col.attachedRigidbody.GetComponent<PlayerMovement>();

        return targetHealth != null;
    }

    protected virtual void OnDisable()
    {
        SetContactAttackSfxActive(false);

        if (enemyAudio != null)
            enemyAudio.StopAllLoopingSfx();
    }
}
