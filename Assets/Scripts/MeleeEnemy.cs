using System.Collections;
using UnityEngine;

public class MeleeEnemy : EnemyBase
{
    [Header("Animation")]
    public Sprite idleFrame1;
    public Sprite idleFrame2;
    public Sprite walkFrame1;
    public Sprite walkFrame2;
    public float animationSwapSeconds = 0.12f;
    [Header("Melee")]
    public float stopDistance = 0.2f;
    public float attackPauseDuration = 0.85f;
    private bool isPaused = false;
    private Color defaultColor;

    [Header("Aggression Tuning")]
    [Min(0.5f)] public float aggressionMultiplier = 1.35f;
    [Min(0.5f)] public float attackRateMultiplier = 1.6f;
    [Min(0.05f)] public float minAttackPauseDuration = 0.25f;

    [Header("Smart Behavior")]
    public float strafeSpeed = 2.5f;
    public float lungeRange = 3f;
    public float lungeSpeedMultiplier = 1.8f;
    public float circleDistance = 4f;

    private enum MeleeState { Approach, Circle, Lunge }
    private MeleeState state = MeleeState.Approach;
    private float stateTimer = 0f;
    private float strafeDir = 1f; // 1 = clockwise, -1 = counter-clockwise
    private float nextStrafeSwitch = 0f;
    private Sprite defaultSprite;
    private float animationTimer;
    private bool isAnimationFrame1 = true;
    private bool isWalkingVisual;

    public override void Stun(float duration)
    {
        if (!isStunned)
            StartCoroutine(StunCoroutine(duration));
    }

    private IEnumerator StunCoroutine(float duration)
    {
        isStunned = true;
        sr.color = Color.yellow;
        yield return new WaitForSeconds(duration);
        isStunned = false;
        sr.color = defaultColor;
    }

    protected override void Awake()
    {
        base.Awake();
        detectionRange = 12f;
        float clampedAggression = Mathf.Clamp(aggressionMultiplier, 0.5f, 3f);
        float clampedAttackRate = Mathf.Clamp(attackRateMultiplier, 0.5f, 3f);

        moveSpeed = 4f * clampedAggression;
        defaultColor = Color.white;
        if (sr != null)
            sr.color = defaultColor;

        attackPauseDuration = Mathf.Max(minAttackPauseDuration, attackPauseDuration / clampedAttackRate);
        strafeSpeed *= clampedAggression;
        lungeSpeedMultiplier *= Mathf.Lerp(1f, clampedAggression, 0.75f);

        // Randomize initial behavior so enemies don't all act the same
        strafeDir = Random.value > 0.5f ? 1f : -1f;
        nextStrafeSwitch = Random.Range(1.2f, 3.2f) / clampedAttackRate;
        circleDistance = Random.Range(2.4f, 4.2f) / Mathf.Lerp(1f, 1.15f, clampedAggression - 1f);
    }

    protected override void Start()
    {
        base.Start();
        animationTimer = Mathf.Max(0.01f, animationSwapSeconds);
        ApplyCurrentAnimationFrame();
    }

    private void Update()
    {
        if (isStunned || isPaused || player == null || health == null || health.IsDead || IsPlayerDead())
            SetWalkingVisual(false);

        UpdateSpriteAnimation();
    }

    protected override void OnCollisionStay2D(Collision2D collision)
    {
        if (!TryResolvePlayerCollision(collision, out PlayerHealth targetHealth, out PlayerMovement targetMovement, out bool isFeetHitbox))
            return;

        if (isFeetHitbox)
            return;

        if (isPaused || isStunned || IsPlayerDead())
        {
            SetContactAttackSfxActive(false);
            return;
        }

        if (targetMovement != null && targetMovement.IsDashing)
        {
            SetContactAttackSfxActive(false);
            return;
        }

        if (targetHealth != null)
        {
            SetContactAttackSfxActive(true);
            Vector2 knockDir = (collision.transform.position - transform.position).normalized;
            targetHealth.TakeDamage(contactDamage, knockDir);
            StartCoroutine(AttackPause());
        }
    }

    protected override void OnTriggerStay2D(Collider2D other)
    {
        if (!TryResolvePlayerCollider(other, out PlayerHealth targetHealth, out PlayerMovement targetMovement, out bool isFeetHitbox))
            return;

        if (isFeetHitbox)
            return;

        if (isPaused || isStunned || IsPlayerDead())
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
        StartCoroutine(AttackPause());
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

        targetMovement = col.GetComponent<PlayerMovement>();
        if (targetMovement == null)
            targetMovement = col.GetComponentInParent<PlayerMovement>();

        if (targetMovement == null && col.attachedRigidbody != null)
            targetMovement = col.attachedRigidbody.GetComponent<PlayerMovement>();

        return targetHealth != null;
    }

    private IEnumerator AttackPause()
    {
        isPaused = true;
        SetContactAttackSfxActive(false);
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(attackPauseDuration);
        isPaused = false;
        // After attacking, back off and circle again
        state = MeleeState.Circle;
        stateTimer = GetPostLungeCircleDuration();
    }

    private void FixedUpdate()
    {
        if (isStunned || isPaused || IsPlayerDead()) return;
        if (player == null || health == null || health.IsDead) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > detectionRange) return;

        stateTimer -= Time.fixedDeltaTime;

        // Decide state transitions
        switch (state)
        {
            case MeleeState.Approach:
                if (dist <= circleDistance)
                {
                    state = MeleeState.Circle;
                    stateTimer = GetCircleDuration();
                }
                break;

            case MeleeState.Circle:
                if (stateTimer <= 0f)
                {
                    state = MeleeState.Lunge;
                    stateTimer = GetLungeDuration();
                }
                // If player gets too far, go back to approaching
                if (dist > circleDistance * 1.5f)
                {
                    state = MeleeState.Approach;
                }
                break;

            case MeleeState.Lunge:
                if (stateTimer <= 0f || dist <= stopDistance)
                {
                    state = MeleeState.Circle;
                    stateTimer = GetPostLungeCircleDuration();
                }
                break;
        }

        // Execute behavior
        switch (state)
        {
            case MeleeState.Approach:
                // Walk toward player but not in a straight line — slight strafe
                Vector2 approachDir = GetDirectionToPlayer();
                Vector2 approachPerp = new Vector2(-approachDir.y, approachDir.x) * 0.3f * strafeDir;
                MoveWithAvoidance((approachDir + approachPerp).normalized);
                break;

            case MeleeState.Circle:
                // Strafe around the player at circleDistance
                Vector2 toPlayer = GetDirectionToPlayer();
                Vector2 perpendicular = new Vector2(-toPlayer.y, toPlayer.x) * strafeDir;

                // Maintain distance — drift in/out
                float distDiff = dist - circleDistance;
                Vector2 circleDir = (perpendicular + toPlayer * distDiff * 0.75f).normalized;
                MoveWithAvoidance(circleDir * (strafeSpeed / moveSpeed));

                // Occasionally switch strafe direction
                nextStrafeSwitch -= Time.fixedDeltaTime;
                if (nextStrafeSwitch <= 0f)
                {
                    strafeDir *= -1f;
                    nextStrafeSwitch = Random.Range(1.2f, 3.2f) / Mathf.Clamp(attackRateMultiplier, 0.5f, 3f);
                }
                break;

            case MeleeState.Lunge:
                // Rush at the player fast
                Vector2 lungeDir = GetDirectionToPlayer();
                Vector2 lungeTarget = lungeDir * lungeSpeedMultiplier;
                MoveWithAvoidance(lungeTarget);
                break;
        }
    }

    private float GetCircleDuration()
    {
        return Random.Range(0.55f, 1.35f) / Mathf.Clamp(attackRateMultiplier, 0.5f, 3f);
    }

    private float GetPostLungeCircleDuration()
    {
        return Random.Range(0.45f, 1.0f) / Mathf.Clamp(attackRateMultiplier, 0.5f, 3f);
    }

    private float GetLungeDuration()
    {
        float clampedAggression = Mathf.Clamp(aggressionMultiplier, 0.5f, 3f);
        return Mathf.Max(0.35f, 0.45f * clampedAggression);
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
}
