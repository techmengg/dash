using UnityEngine;

public class RangedEnemy : EnemyBase
{
    [Header("Animation")]
    public Sprite idleFrame1;
    public Sprite idleFrame2;
    public Sprite walkFrame1;
    public Sprite walkFrame2;
    public float animationSwapSeconds = 0.12f;
    [Header("Ranged")]
    public float preferredDistance = 10f;
    public float retreatDistance = 3.5f;
    public float shootCooldown = 1.25f;
    public float projectileSpeed = 10f;
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Smart Behavior")]
    public float strafeSpeed = 3f;

    private float shootTimer;
    private float strafeDir = 1f;
    private float strafeSwitchTimer;
    private float repositionTimer;
    private bool repositioning = false;
    private Vector2 repositionTarget;

    protected override void Awake()
    {
        base.Awake();
        detectionRange = 20f;
        if (sr != null)
            sr.color = Color.white;

        // Randomize so ranged enemies don't all mirror each other
        strafeDir = Random.value > 0.5f ? 1f : -1f;
        strafeSwitchTimer = Random.Range(2f, 5f);
        preferredDistance = Random.Range(7f, 12f);
        repositionTimer = Random.Range(3f, 6f);
    }
    private Sprite defaultSprite;
    private float animationTimer;
    private bool isAnimationFrame1 = true;
    private bool isWalkingVisual;

    protected override void Start()
    {
        base.Start();
        shootTimer = shootCooldown;
        animationTimer = Mathf.Max(0.01f, animationSwapSeconds);
        ApplyCurrentAnimationFrame();
    }

    private void Update()
    {
        if (player == null || health == null || health.IsDead || IsPlayerDead()) return;

        shootTimer -= Time.deltaTime;
        strafeSwitchTimer -= Time.deltaTime;
        repositionTimer -= Time.deltaTime;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist <= detectionRange && dist <= preferredDistance + 3f && shootTimer <= 0f)
        {
            // Only shoot if we have a clear line of sight
            Vector2 dirToPlayer = ((Vector2)player.position - rb.position).normalized;
            RaycastHit2D hit = Physics2D.Raycast(rb.position, dirToPlayer, dist, wallLayer);

            if (hit.collider == null)
            {
                Shoot();
                shootTimer = shootCooldown + Random.Range(-0.3f, 0.3f); // Vary timing
            }
            else
            {
                // Wall in the way — reposition
                repositioning = true;
                repositionTarget = FindOpenPosition();
                repositionTimer = Random.Range(3f, 6f);
            }
        }

        // Switch strafe direction periodically
        if (strafeSwitchTimer <= 0f)
        {
            strafeDir *= -1f;
            strafeSwitchTimer = Random.Range(2f, 5f);
        }

        // Occasionally pick a new spot to move to
        if (repositionTimer <= 0f && !repositioning)
        {
            if (Random.value < 0.4f) // 40% chance to reposition
            {
                repositioning = true;
                repositionTarget = FindOpenPosition();
            }
            repositionTimer = Random.Range(3f, 6f);
        }
    }

    private void FixedUpdate()
    {
        if (isStunned || player == null || health == null || health.IsDead || IsPlayerDead())
        {
            SetWalkingVisual(false);
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > detectionRange)
        {
            rb.linearVelocity = Vector2.zero;
            SetWalkingVisual(false);
            return;
        }

        Vector2 dirToPlayer = GetDirectionToPlayer();

        // Move closer if too far
        if (dist > preferredDistance)
        {
            SetWalkingVisual(true);
            MoveWithAvoidance(dirToPlayer);
        }
        // Back away if too close
        else if (dist < retreatDistance)
        {
            SetWalkingVisual(true);
            MoveWithAvoidance(-dirToPlayer);
        }
        // Stay still if in good shooting range
        else
        {
            rb.linearVelocity = Vector2.zero;
            SetWalkingVisual(false);
        }
    }

    private Vector2 FindOpenPosition()
    {
        // Find a position that has line of sight to the player
        for (int i = 0; i < 10; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = Random.Range(preferredDistance * 0.7f, preferredDistance * 1.3f);
            Vector2 candidate = (Vector2)player.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

            // Check if position is clear of walls
            if (!Physics2D.OverlapCircle(candidate, 0.5f, wallLayer))
            {
                // Check line of sight to player
                Vector2 toPlayer = ((Vector2)player.position - candidate).normalized;
                float playerDist = Vector2.Distance(candidate, player.position);
                RaycastHit2D hit = Physics2D.Raycast(candidate, toPlayer, playerDist, wallLayer);

                if (hit.collider == null)
                    return candidate;
            }
        }

        // Fallback to current position
        return rb.position;
    }

    private void SetWalkingVisual(bool isWalking)
    {
        if (isWalkingVisual == isWalking)
            return;

        isWalkingVisual = isWalking;
        isAnimationFrame1 = true;
        animationTimer = Mathf.Max(0.01f, animationSwapSeconds);
        ApplyCurrentAnimationFrame();
    }

    private void UpdateSpriteAnimation()
    {
        if (sr == null)
            return;

        animationTimer -= Time.deltaTime;
        if (animationTimer > 0f)
            return;

        isAnimationFrame1 = !isAnimationFrame1;
        animationTimer = Mathf.Max(0.01f, animationSwapSeconds);
        ApplyCurrentAnimationFrame();
    }

    private void ApplyCurrentAnimationFrame()
    {
        if (sr == null)
            return;

        Sprite targetSprite = GetSpriteForState(isWalkingVisual, isAnimationFrame1);
        if (targetSprite != null)
            sr.sprite = targetSprite;
    }

    private Sprite GetSpriteForState(bool isWalking, bool frame1)
    {
        if (isWalking)
        {
            if (frame1)
                return walkFrame1 != null ? walkFrame1 : (walkFrame2 != null ? walkFrame2 : ResolveIdleSprite(true));

            return walkFrame2 != null ? walkFrame2 : (walkFrame1 != null ? walkFrame1 : ResolveIdleSprite(false));
        }

        return ResolveIdleSprite(frame1);
    }

    private Sprite ResolveIdleSprite(bool frame1)
    {
        if (frame1)
            return idleFrame1 != null ? idleFrame1 : (idleFrame2 != null ? idleFrame2 : defaultSprite);

        return idleFrame2 != null ? idleFrame2 : (idleFrame1 != null ? idleFrame1 : defaultSprite);
    }

    private void Shoot()
    {
        if (projectilePrefab == null || firePoint == null || player == null) return;

        PlayProjectileAttackSfx();

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        Vector2 dir = ((Vector2)player.position - (Vector2)firePoint.position).normalized;

        EnemyProjectile projectile = proj.GetComponent<EnemyProjectile>();
        if (projectile != null)
        {
            Collider2D myCollider = GetComponent<Collider2D>();
            projectile.Initialize(dir, projectileSpeed, myCollider);
        }
    }
}
