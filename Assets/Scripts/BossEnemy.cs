using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossEnemy : EnemyBase
{
    public enum BossState { Sleeping, Chasing, Attacking, Rolling, PhaseTransition }

    [Header("Boss Settings")]
    public float stopDistance = 1.5f;
    public float attackCooldown = 4f;
    private float attackTimer;
    private BossState currentState = BossState.Sleeping;
    private bool hasAwoken = false;

    [Header("Grab & Eat Attack")]
    public float grabRange = 2.5f;
    public float grabTelegraphTime = 0.6f;
    public float chewDuration = 1.5f;
    public int eatDamage = 50;
    public float spitOutForce = 10f;

    [Header("Leap Slam Attack (5 Frames)")]
    public float leapMinRange = 3f;
    public float leapMaxRange = 7f;
    public float leapTravelTime = 0.45f;
    public float leapLandPause = 0.25f;
    public float leapAoeRadius = 2.2f;
    public int leapDamage = 20;
    public float leapKnockbackForce = 12f;
    public float leapSlamCooldown = 6f;
    public LayerMask playerLayer;
    public Sprite leapFrame1;
    public Sprite leapFrame2;
    public Sprite leapFrame3;
    public Sprite leapFrame4;
    public Sprite leapFrame5;

    [Header("Slam Visual Effects")]
    public float slamTelegraphDuration = 0.55f;
    public float slamScreenShakeMagnitude = 0.3f;
    public float slamScreenShakeDuration = 0.4f;
    public float slamShockwaveRadius = 3f;
    public int slamCrackCount = 6;
    public float slamCrackLength = 3f;
    public float slamCrackDuration = 2f;
    public float slamArcHeight = 2.5f;
    [Tooltip("Hitpause duration in seconds (0.04-0.07 feels best)")]
    public float slamHitPauseDuration = 0.055f;
    [Tooltip("How many afterimages to spawn during the leap")]
    public int slamAfterimageCount = 3;

    [Header("Attack Sprites")]
    public Sprite grabSprite;
    public Sprite eatSprite;

    [Header("Idle & Walk Sprites (2 Frames Each)")]
    public Sprite idleFrame1;
    public Sprite idleFrame2;
    public Sprite walkFrame1;
    public Sprite walkFrame2;
    public float animationSpeed = 0.5f;

    [Header("Summon Attack (Phase 1)")]
    public GameObject meleePrefab;             // Assign MeleeEnemy prefab
    public GameObject rangedPrefab;            // Assign RangedEnemy prefab
    [Range(0f, 1f)]
    public float meleeSpawnChance = 0.6f;      // 60% melee, 40% ranged
    public int summonCount = 3;                // Max summons alive at once
    public float summonCooldown = 12f;
    public float summonSpawnRadius = 2.5f;
    public float summonAnimDuration = 1.2f;
    [Range(0f, 1f)]
    public float throwChance = 0.4f;           // % chance to throw a nearby summon at the player
    public float throwCheckRange = 4f;         // How close an enemy must be to get thrown
    public float throwSpeed = 15f;
    public int throwDamage = 10;
    public float throwKnockbackForce = 8f;
    public float throwCooldown = 5f;           // How often the boss can throw during chasing

    [Header("Roll Attack (Consecutive Hits)")]
    public int consecutiveHitsToRoll = 3;
    public float consecutiveHitWindow = 2f;
    public float rollDuration = 6f;
    public float rollSpeed = 8f;
    public float rollRotationSpeed = 720f;
    public int rollDamage = 15;
    public float rollKnockbackForce = 10f;
    [Min(0f)] public float rollCooldown = 7f;

    [Header("Roll Bounce")]
    [Min(0f)] public float rollBouncePadding = 0.2f;

    [Header("Phase 2 Settings")]
    public bool hasPhase2 = true;
    public float phase2MaxHealth = 150f;
    [Min(1f)] public float phase2ScaleMultiplier = 1.2f;
    public float phase2MoveSpeedMultiplier = 1.4f;
    public float phase2AttackCooldownMultiplier = 0.6f;
    public float phase2EatDamageMultiplier = 1.5f;
    public float phase2LeapDamageMultiplier = 1.5f;
    public float phase2LeapAoeRadiusMultiplier = 1.3f;
    public float phase2RollSpeedMultiplier = 1.3f;
    public float transitionDuration = 3f;
    public Color phase2TintColor = new Color(0.8f, 0.2f, 0.2f, 1f);

    [Header("Phase 2 Sprites (optional)")]
    public Sprite phase2IdleFrame1;
    public Sprite phase2IdleFrame2;
    public Sprite phase2WalkFrame1;
    public Sprite phase2WalkFrame2;

    [Header("Phase 2 Attack Sprites (optional)")]
    public Sprite phase2GrabSprite;
    public Sprite phase2EatSprite;
    public Sprite phase2LeapFrame1;
    public Sprite phase2LeapFrame2;
    public Sprite phase2LeapFrame3;
    public Sprite phase2LeapFrame4;
    public Sprite phase2LeapFrame5;

    [Header("Environment")]
    public LayerMask obstacleLayer;

    [Header("Scene Routing")]
    public string menuSceneName = "Menu";

    // Active arena bounds used for clamping movement.
    // Defaults to the legacy boss room bounds and can be overridden at runtime.
    private float bossArenaMinX = -11.5f;
    private float bossArenaMaxX = 11.5f;
    private float bossArenaMinY = -6.5f;
    private float bossArenaMaxY = 6.5f;

    private float animTimer;
    private bool isFrame1 = true;
    private Color defaultColor = Color.white;
    private RigidbodyConstraints2D cachedConstraints;
    private bool hasCachedConstraints = false;

    // Consecutive hit tracking
    private int consecutiveHitCount = 0;
    private float lastHitTime = -999f;
    private bool isRolling = false;
    private float lastRollHitTime = 0f;
    private float nextRollAllowedTime = 0f;

    // Summon tracking
    private float summonTimer = 0f;
    private float throwTimer = 0f;
    private bool isThrowing = false;
    private float leapSlamTimer = 0f;
    private System.Collections.Generic.List<GameObject> activeSummons = new System.Collections.Generic.List<GameObject>();

    // Phase tracking
    private float previousHealth;
    private int currentPhase = 1;
    private bool isInPhaseTransition = false;
    private Vector3 baseLocalScale = Vector3.one;
    private PhysicsMaterial2D activeBounceMat;
    private PhysicsMaterial2D originalColliderMat;
    private BossAudio bossAudio;

    public int CurrentPhase => currentPhase;
    public bool IsInPhaseTransition => isInPhaseTransition;
    public bool HasAwoken => hasAwoken;

    private void EnsureBossDeathRouting()
    {
        if (health == null)
            return;

        health.canRespawn = false;

        if (hasPhase2 && currentPhase <= 1 && !isInPhaseTransition)
            health.onDeathOverride = OnPhase1Death;
        else
            health.onDeathOverride = OnPhase2Death;
    }

    // Sleep visual colors
    private Color sleepTintColor = new Color(0.3f, 0.3f, 0.35f, 1f);

    protected override void Awake()
    {
        base.Awake();
        baseLocalScale = transform.localScale;
        bossAudio = GetComponent<BossAudio>();
        if (bossAudio == null)
            bossAudio = gameObject.AddComponent<BossAudio>();

        attackTimer = attackCooldown;
        animTimer = animationSpeed;

        if (sr != null)
        {
            defaultColor = sr.color;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = Mathf.Max(sr.sortingOrder, 29);
            if (idleFrame1 != null) sr.sprite = idleFrame1;

            // Start sleeping — darken the sprite
            sr.color = sleepTintColor;
        }

        EnsureBossDeathRouting();
    }

    protected override void Start()
    {
        base.Start();
        if (health != null)
        {
            previousHealth = health.currentHealth;
            EnsureBossDeathRouting();
        }
        summonTimer = summonCooldown * 0.5f; // First summon comes a bit earlier
    }

    protected override void OnCollisionStay2D(Collision2D collision)
    {
        return;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnlockPositionAfterAttack();
        StopBossActionSfx();

        if (enemyAudio != null)
            enemyAudio.StopAllLoopingSfx();

        if (bossAudio != null)
            bossAudio.StopAllLoopingSfx();
    }

    private void OnDestroy()
    {
        UnlockPositionAfterAttack();
        StopBossActionSfx();

        if (enemyAudio != null)
            enemyAudio.StopAllLoopingSfx();

        if (bossAudio != null)
            bossAudio.StopAllLoopingSfx();

        if (rollCircle != null) Destroy(rollCircle);
        CleanupBounceMaterial();
    }

    private void FixedUpdate()
    {
        EnsureBossDeathRouting();

        if (currentState == BossState.Sleeping)
        {
            if (bossAudio != null)
                bossAudio.SetBossWalkLoopActive(false);
            return;
        }

        if (isInPhaseTransition)
        {
            if (bossAudio != null)
                bossAudio.SetBossWalkLoopActive(false);
            return;
        }

        if (isStunned || IsPlayerDead() || player == null || health == null || health.IsDead)
        {
            if (bossAudio != null)
                bossAudio.SetBossWalkLoopActive(false);
            return;
        }

        DetectConsecutiveHits();

        if (currentState == BossState.Chasing)
        {
            ChasePlayer();
            HandleAttackCooldown();
            HandleMovementAnimations();
            HandleThrowCheck();
        }
        else if (bossAudio != null)
        {
            bossAudio.SetBossWalkLoopActive(false);
        }

        // Always enforce boss stays in its arena
        EnforceBossArenaBounds();
    }

    /// <summary>
    /// Hard clamp: boss can never leave the boss arena rectangle.
    /// </summary>
    private void EnforceBossArenaBounds()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, bossArenaMinX, bossArenaMaxX);
        pos.y = Mathf.Clamp(pos.y, bossArenaMinY, bossArenaMaxY);
        transform.position = pos;
    }

    /// <summary>
    /// Clamps a Vector2 position to the boss arena bounds.
    /// Use this to sanitize landing positions before leaping.
    /// </summary>
    private Vector2 ClampToBossArena(Vector2 pos, float margin = 1f)
    {
        pos.x = Mathf.Clamp(pos.x, bossArenaMinX + margin, bossArenaMaxX - margin);
        pos.y = Mathf.Clamp(pos.y, bossArenaMinY + margin, bossArenaMaxY - margin);
        return pos;
    }

    // ==================== SLEEP / WAKE SYSTEM ====================

    /// <summary>
    /// Called when the encounter starts.
    /// Plays a dramatic wake-up sequence then starts fighting.
    /// </summary>
    public void WakeUp()
    {
        if (hasAwoken) return;
        hasAwoken = true;
        StartCoroutine(WakeUpSequence());
    }

    private IEnumerator WakeUpSequence()
    {
        rb.linearVelocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;

        // --- Flash white ---
        if (sr != null) sr.color = Color.white;
        yield return new WaitForSeconds(0.1f);

        // --- Shake and pulse ---
        Vector3 origPos = transform.position;
        float shakeDur = 0.8f;
        float elapsed = 0f;
        while (elapsed < shakeDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shakeDur;

            // Shake intensity increases then decreases
            float intensity = Mathf.Sin(t * Mathf.PI) * 0.15f;
            float offsetX = Mathf.Sin(elapsed * 45f) * intensity;
            float offsetY = Mathf.Cos(elapsed * 37f) * intensity * 0.5f;
            transform.position = origPos + new Vector3(offsetX, offsetY, 0);

            // Color pulse from dark to bright red
            if (sr != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 12f);
                sr.color = Color.Lerp(sleepTintColor, new Color(1f, 0.4f, 0.3f, 1f), pulse * t);
            }

            yield return null;
        }
        transform.position = origPos;

        // --- Scale pop ---
        Vector3 origScale = transform.localScale;
        transform.localScale = origScale * 0.6f;

        float popDur = 0.35f;
        elapsed = 0f;
        while (elapsed < popDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDur;
            // Overshoot then settle
            float scale;
            if (t < 0.6f)
                scale = Mathf.Lerp(0.6f, 1.2f, t / 0.6f);
            else
                scale = Mathf.Lerp(1.2f, 1f, (t - 0.6f) / 0.4f);
            transform.localScale = origScale * scale;
            yield return null;
        }
        transform.localScale = origScale;

        // --- Restore color ---
        if (sr != null) sr.color = defaultColor;

        // --- Screen shake on wake ---
        StartCoroutine(SlamEffects.ScreenShake(0.3f, 0.2f));

        // Unlock and start fighting
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        currentState = BossState.Chasing;
        attackTimer = attackCooldown * 0.5f; // Attack soon after waking
    }

    public void ConfigureArenaBounds(float minX, float maxX, float minY, float maxY)
    {
        bossArenaMinX = Mathf.Min(minX, maxX);
        bossArenaMaxX = Mathf.Max(minX, maxX);
        bossArenaMinY = Mathf.Min(minY, maxY);
        bossArenaMaxY = Mathf.Max(minY, maxY);
    }

    // ==================== RESET (RESPAWN) ====================

    /// <summary>
    /// Resets the boss to its initial sleeping state. Called on player death/respawn.
    /// </summary>
    public void ResetBoss()
    {
        StopAllCoroutines();
        UnlockPositionAfterAttack();

        if (enemyAudio != null)
            enemyAudio.StopAllLoopingSfx();

        if (bossAudio != null)
            bossAudio.StopAllLoopingSfx();

        // Clean up roll state
        if (isRolling)
        {
            isRolling = false;
            if (enemyAudio != null)
                enemyAudio.SetRollLoopActive(false);

            transform.rotation = Quaternion.identity;
            if (rollCircle != null)
            {
                Destroy(rollCircle);
                rollCircle = null;
            }
            CleanupBounceMaterial();
        }

        // Reset state
        currentState = BossState.Sleeping;
        hasAwoken = false;
        isInPhaseTransition = false;
        currentPhase = 1;
        attackTimer = attackCooldown;
        consecutiveHitCount = 0;
        lastHitTime = -999f;
        nextRollAllowedTime = 0f;
        summonTimer = summonCooldown * 0.5f;
        throwTimer = 0f;
        isThrowing = false;
        leapSlamTimer = 0f;

        // Reset health
        if (health != null)
        {
            health.currentHealth = health.maxHealth;
            health.IsDead = false;
            EnsureBossDeathRouting();
        }

        // Reset visuals to sleeping
        if (sr != null)
        {
            sr.color = sleepTintColor;
            if (idleFrame1 != null) sr.sprite = idleFrame1;
        }

        // Reset physics
        rb.linearVelocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        transform.localScale = baseLocalScale;

        // Destroy any active summons
        foreach (GameObject summon in activeSummons)
        {
            if (summon != null) Destroy(summon);
        }
        activeSummons.Clear();
    }

    // ==================== PHASE 2 SYSTEM ====================

    private void OnPhase1Death(EnemyHealth enemyHealth)
    {
        // Stop ALL ongoing coroutines (attacks, roll, etc.)
        StopAllCoroutines();
        UnlockPositionAfterAttack();

        if (enemyAudio != null)
            enemyAudio.StopAllLoopingSfx();

        if (bossAudio != null)
            bossAudio.StopAllLoopingSfx();

        // Clean up roll state if interrupted mid-roll
        if (isRolling)
        {
            isRolling = false;
            if (enemyAudio != null)
                enemyAudio.SetRollLoopActive(false);

            transform.rotation = Quaternion.identity;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

            // Remove roll circle collider and restore polygon
            if (rollCircle != null)
            {
                Collider2D playerCol = player != null ? player.GetComponent<Collider2D>() : null;
                if (playerCol != null)
                    Physics2D.IgnoreCollision(rollCircle, playerCol, false);
                Destroy(rollCircle);
                rollCircle = null;
            }
            CleanupBounceMaterial();

            // Re-enable original polygon collider
            PolygonCollider2D poly = GetComponent<PolygonCollider2D>();
            if (poly != null) poly.enabled = true;
        }

        // Safety: if boss was mid-eat, re-enable the player
        if (player != null)
        {
            PlayerMovement pm = player.GetComponent<PlayerMovement>();
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (pm != null && !pm.enabled)
                pm.enabled = true;
            if (ph != null)
                ph.isInvincible = false;
        }

        // Mark dead to block FixedUpdate and further damage
        health.SetDead(true);
        health.canRespawn = false;
        health.onDeathOverride = OnPhase2Death;

        // Start Phase 2 transition (runs on BossEnemy, not EnemyHealth, so it won't be disabled)
        StartCoroutine(PhaseTransitionRoutine());
    }

    private IEnumerator PhaseTransitionRoutine()
    {
        currentState = BossState.PhaseTransition;
        isInPhaseTransition = true;
        rb.linearVelocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;

        yield return StartCoroutine(PlayPhase2TransitionCutsceneIfEnabled());

        Vector3 originalScale = transform.localScale;
        float targetScaleMultiplier = Mathf.Max(1f, phase2ScaleMultiplier);
        float overshootScaleMultiplier = Mathf.Max(targetScaleMultiplier + 0.1f, targetScaleMultiplier * 1.15f);
        Color startColor = sr != null ? sr.color : Color.white;

        // --- COLLAPSE: flash white then fade out ---
        if (sr != null)
        {
            sr.sprite = idleFrame1;
            sr.color = Color.white;
        }

        // Quick fade to near-invisible
        float fadeTime = 0.5f;
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;
            if (sr != null)
                sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.15f, t));
            yield return null;
        }

        // Pause while "dead"
        yield return new WaitForSeconds(1f);

        // --- RISE UP: scale up + color shift to Phase 2 ---
        transform.localScale = originalScale * 0.3f;
        if (sr != null) sr.color = new Color(phase2TintColor.r, phase2TintColor.g, phase2TintColor.b, 0.15f);

        // Shake + grow over transitionDuration
        elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float eased = t * t * (3f - 2f * t); // smoothstep

            // Scale: 0.3 -> overshoot -> settle at configured Phase 2 size
            float scaleT;
            if (t < 0.7f)
                scaleT = Mathf.Lerp(0.3f, overshootScaleMultiplier, eased / 0.7f);
            else
                scaleT = Mathf.Lerp(overshootScaleMultiplier, targetScaleMultiplier, (t - 0.7f) / 0.3f);

            transform.localScale = originalScale * scaleT;

            // Fade alpha back in
            float alpha = Mathf.Lerp(0.15f, 1f, eased);
            if (sr != null)
                sr.color = new Color(phase2TintColor.r, phase2TintColor.g, phase2TintColor.b, alpha);

            // Screen shake effect: slight random offset that decreases over time
            float shakeStrength = Mathf.Lerp(0.08f, 0f, t);
            Vector3 shakeOffset = new Vector3(
                Random.Range(-shakeStrength, shakeStrength),
                Random.Range(-shakeStrength, shakeStrength),
                0f
            );
            transform.localPosition = (Vector3)(Vector2)transform.position + shakeOffset;

            yield return null;
        }

        // Ensure clean final state
        transform.localScale = originalScale * targetScaleMultiplier;
        if (sr != null) sr.color = phase2TintColor;

        // --- APPLY PHASE 2 ---
        ApplyPhase2Stats();

        health.maxHealth = phase2MaxHealth;
        health.currentHealth = phase2MaxHealth;
        health.SetDead(false);

        // Phase 2 death triggers the boss death animation + victory screen
        health.onDeathOverride = OnPhase2Death;

        // Unfreeze
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Update colors for damage flash system
        defaultColor = phase2TintColor;
        health.normalColor = phase2TintColor;

        // Reset tracking
        consecutiveHitCount = 0;
        previousHealth = health.currentHealth;

        isInPhaseTransition = false;
        currentPhase = 2;
        attackTimer = attackCooldown;
        currentState = BossState.Chasing;
    }

    private IEnumerator PlayPhase2TransitionCutsceneIfEnabled()
    {
        BossArenaSpawnConfig spawnConfig = FindFirstObjectByType<BossArenaSpawnConfig>();

        bool enableCutscene = spawnConfig == null || spawnConfig.enablePhase2TransitionCutscene;
        if (!enableCutscene)
            yield break;

        float pauseDuration = spawnConfig != null ? Mathf.Max(0f, spawnConfig.phase2PauseDuration) : 0.2f;
        float panToBossDuration = spawnConfig != null ? Mathf.Max(0f, spawnConfig.phase2PanToBossDuration) : 0.65f;
        float bossFocusHoldDuration = spawnConfig != null ? Mathf.Max(0f, spawnConfig.phase2BossFocusHoldDuration) : 0.45f;
        float panBackDuration = spawnConfig != null ? Mathf.Max(0f, spawnConfig.phase2PanBackToPlayerDuration) : 0.7f;
        float zoomPercent = spawnConfig != null ? Mathf.Clamp(spawnConfig.phase2ZoomInPercent, 0f, 90f) : 30f;
        float zoomDuration = spawnConfig != null ? Mathf.Max(0f, spawnConfig.phase2ZoomDuration) : 0.45f;

        Camera cam = Camera.main;
        if (cam == null)
            yield break;

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        bool followWasEnabled = follow != null && follow.enabled;
        float previousTimeScale = Time.timeScale;

        Time.timeScale = 0f;

        if (followWasEnabled)
            follow.enabled = false;

        Vector3 camStart = cam.transform.position;
        Vector3 camBoss = new Vector3(transform.position.x, transform.position.y, camStart.z);

        if (pauseDuration > 0f)
            yield return new WaitForSecondsRealtime(pauseDuration);

        yield return PanCameraUnscaled(cam.transform, camStart, camBoss, panToBossDuration);

        if (bossFocusHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(bossFocusHoldDuration);

        Vector3 camReturn = GetPlayerCameraPosition(cam, follow, camStart);
        yield return PanCameraUnscaled(cam.transform, cam.transform.position, camReturn, panBackDuration);

        float startingZoom = cam.orthographicSize;
        float zoomFactor = 1f - (zoomPercent / 100f);
        float targetZoom = Mathf.Max(1f, startingZoom * zoomFactor);
        yield return ZoomCameraUnscaled(cam, follow, startingZoom, targetZoom, zoomDuration);

        if (follow != null)
        {
            follow.orthographicSize = cam.orthographicSize;
            follow.enabled = followWasEnabled;
            if (followWasEnabled)
                follow.SnapToTarget();
        }

        Time.timeScale = previousTimeScale;
    }

    private static Vector3 GetPlayerCameraPosition(Camera cam, CameraFollow follow, Vector3 fallback)
    {
        if (cam == null)
            return fallback;

        Transform target = null;
        if (follow != null)
            target = follow.target;

        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
            else
            {
                PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
                if (pm != null)
                    target = pm.transform;
            }
        }

        if (target == null)
            return fallback;

        return new Vector3(target.position.x, target.position.y, fallback.z);
    }

    private static IEnumerator PanCameraUnscaled(Transform camTransform, Vector3 from, Vector3 to, float duration)
    {
        if (camTransform == null)
            yield break;

        if (duration <= 0f)
        {
            camTransform.position = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            camTransform.position = Vector3.Lerp(from, to, eased);
            yield return null;
        }

        camTransform.position = to;
    }

    private static IEnumerator ZoomCameraUnscaled(Camera cam, CameraFollow follow, float fromSize, float toSize, float duration)
    {
        if (cam == null || !cam.orthographic)
            yield break;

        if (duration <= 0f)
        {
            cam.orthographicSize = toSize;
            if (follow != null)
                follow.orthographicSize = toSize;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            float size = Mathf.Lerp(fromSize, toSize, eased);
            cam.orthographicSize = size;
            if (follow != null)
                follow.orthographicSize = size;
            yield return null;
        }

        cam.orthographicSize = toSize;
        if (follow != null)
            follow.orthographicSize = toSize;
    }

    // ══════════════════════════════════════════════════════════════
    // PHASE 2 DEATH — "GLUTTONY DEVOURS ITSELF"
    // ══════════════════════════════════════════════════════════════

    private void OnPhase2Death(EnemyHealth enemyHealth)
    {
        // Same cleanup as OnPhase1Death
        StopAllCoroutines();
        UnlockPositionAfterAttack();

        if (enemyAudio != null)
            enemyAudio.StopAllLoopingSfx();

        if (bossAudio != null)
            bossAudio.StopAllLoopingSfx();

        // Clean up roll state if interrupted mid-roll
        if (isRolling)
        {
            isRolling = false;
            if (enemyAudio != null)
                enemyAudio.SetRollLoopActive(false);

            transform.rotation = Quaternion.identity;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

            if (rollCircle != null)
            {
                Collider2D playerCol = player != null ? player.GetComponent<Collider2D>() : null;
                if (playerCol != null)
                    Physics2D.IgnoreCollision(rollCircle, playerCol, false);
                Destroy(rollCircle);
                rollCircle = null;
            }
            CleanupBounceMaterial();

            PolygonCollider2D poly = GetComponent<PolygonCollider2D>();
            if (poly != null) poly.enabled = true;
        }

        // Safety: free the player if mid-eat
        if (player != null)
        {
            PlayerMovement pm = player.GetComponent<PlayerMovement>();
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (pm != null && !pm.enabled)
                pm.enabled = true;
            if (ph != null)
                ph.isInvincible = false;
        }

        // Block further damage and freeze
        health.SetDead(true);
        rb.linearVelocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;

        // Disable colliders so player can move freely during death anim
        foreach (Collider2D col in GetComponents<Collider2D>())
            col.enabled = false;

        StartCoroutine(BossDeathSequence());
    }

    private IEnumerator BossDeathSequence()
    {
        Vector2 bossPos = transform.position;
        Vector3 originalScale = transform.localScale;
        Sprite deathSprite = sr != null ? sr.sprite : null;
        bool flipX = sr != null && sr.flipX;

        // ── Beat 0: Hit Freeze (0.12s) ──
        if (sr != null)
            StartCoroutine(SlamEffects.SpriteWhiteFlash(sr, phase2TintColor, 4));
        yield return StartCoroutine(SlamEffects.HitPause(0.12f));

        // ── Beat 1: Bloat — Gluttony swells (1.5s) ──
        StartCoroutine(SlamEffects.ScreenShake(1.5f, 0.1f));

        float bloatDuration = 1.5f;
        float bloatElapsed = 0f;
        Vector3 basePos = transform.position;
        bool leakedFirst = false;
        bool leakedSecond = false;

        while (bloatElapsed < bloatDuration)
        {
            bloatElapsed += Time.deltaTime;
            float t = bloatElapsed / bloatDuration;

            // Swell from 1.0 to 1.6
            float scale = Mathf.Lerp(1f, 1.6f, t * t * (3f - 2f * t));
            transform.localScale = originalScale * scale;

            // Color pulse: red ↔ orange, frequency increases over time
            float pulseFreq = Mathf.Lerp(4f, 20f, t);
            float pulse = Mathf.Sin(bloatElapsed * pulseFreq) * 0.5f + 0.5f;
            Color sicklyOrange = new Color(1f, 0.4f, 0.15f, 1f);
            if (sr != null)
                sr.color = Color.Lerp(phase2TintColor, sicklyOrange, pulse);

            // Position tremble with increasing intensity
            float trembleMag = Mathf.Lerp(0.02f, 0.15f, t);
            transform.position = basePos + new Vector3(
                Mathf.Sin(bloatElapsed * 45f) * trembleMag,
                Mathf.Cos(bloatElapsed * 37f) * trembleMag,
                0f);

            // Leak afterimages at 0.5s and 1.0s
            if (!leakedFirst && bloatElapsed >= 0.5f && deathSprite != null)
            {
                leakedFirst = true;
                SlamEffects.SpawnAfterimage(bossPos + new Vector2(1.5f, 0.5f), deathSprite,
                    originalScale, new Color(0.6f, 0.1f, 0.1f, 1f), 0.8f, flipX);
            }
            if (!leakedSecond && bloatElapsed >= 1.0f && deathSprite != null)
            {
                leakedSecond = true;
                SlamEffects.SpawnAfterimage(bossPos + new Vector2(-1f, -0.8f), deathSprite,
                    originalScale, new Color(0.6f, 0.1f, 0.1f, 1f), 0.8f, flipX);
            }

            yield return null;
        }

        // ── Beat 2: Rupture — violent snap inward (0.3s) ──
        bossPos = transform.position;
        yield return StartCoroutine(SlamEffects.HitPause(0.06f));

        SlamEffects.SpawnScreenFlash(0.2f, 0.5f);
        StartCoroutine(SlamEffects.ScreenShake(0.5f, 0.4f));
        SlamEffects.SpawnShockwave(bossPos, 5f, 0.5f, new Color(0.8f, 0.15f, 0.05f, 0.9f), 0.3f);
        SlamEffects.SpawnGroundCracks(bossPos, 8, 4f, 3f);
        SlamEffects.SpawnUpwardDebris(bossPos, 8);

        // 8 afterimages expelled in a ring
        if (deathSprite != null)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * (360f / 8f) * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2f;
                SlamEffects.SpawnAfterimage(bossPos + offset, deathSprite,
                    originalScale * 0.8f, new Color(1f, 0.3f, 0.1f, 1f), 0.6f, flipX);
            }
        }

        // Snap scale from 1.6 to 0.7
        float ruptureDuration = 0.15f;
        float ruptureElapsed = 0f;
        while (ruptureElapsed < ruptureDuration)
        {
            ruptureElapsed += Time.deltaTime;
            float t = ruptureElapsed / ruptureDuration;
            float s = Mathf.Lerp(1.6f, 0.7f, t * t * t); // cubic ease-in for violent snap
            transform.localScale = originalScale * s;
            yield return null;
        }
        transform.localScale = originalScale * 0.7f;

        yield return new WaitForSeconds(0.15f);

        // ── Beat 3: Implosion — power sucked inward (1.2s) ──
        SlamEffects.SpawnGroundDustPuffs(bossPos, 3f, 1.0f);

        float implodeDuration = 1.2f;
        float implodeElapsed = 0f;

        // Spawn afterimages at decreasing radii to create convergence illusion
        float[] ringTimes = { 0f, 0.24f, 0.48f, 0.72f, 0.96f };
        float[] ringRadii = { 4f, 3f, 2f, 1f, 0.5f };
        int nextRing = 0;

        while (implodeElapsed < implodeDuration)
        {
            implodeElapsed += Time.deltaTime;
            float t = implodeElapsed / implodeDuration;

            // Scale pulses down: 0.7 → 0.5
            float pulse = Mathf.Sin(implodeElapsed * 8f) * 0.05f;
            float s = Mathf.Lerp(0.7f, 0.5f, t) + pulse;
            transform.localScale = originalScale * s;

            // Alpha fading
            if (sr != null)
            {
                float alpha = Mathf.Lerp(1f, 0.6f, t);
                sr.color = new Color(phase2TintColor.r, phase2TintColor.g, phase2TintColor.b, alpha);
            }

            // Spawn converging afterimage rings
            if (nextRing < ringTimes.Length && implodeElapsed >= ringTimes[nextRing] && deathSprite != null)
            {
                float radius = ringRadii[nextRing];
                for (int i = 0; i < 4; i++)
                {
                    float angle = (i * 90f + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                    Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    SlamEffects.SpawnAfterimage(bossPos + offset, deathSprite,
                        originalScale * (0.3f + radius * 0.1f),
                        new Color(0.9f, 0.2f, 0.1f, 1f), 0.6f, flipX);
                }
                nextRing++;
            }

            yield return null;
        }

        // ── Beat 4: Final Collapse (0.5s) ──
        yield return StartCoroutine(SlamEffects.HitPause(0.1f));

        if (bossAudio != null)
            bossAudio.TryPlayBossPhase2ExplodeSfx();

        SlamEffects.SpawnScreenFlash(0.3f, 0.7f);
        StartCoroutine(SlamEffects.ScreenShake(0.6f, 0.5f));

        // Dual shockwaves: gold outer + red inner
        SlamEffects.SpawnShockwave(bossPos, 8f, 0.6f, new Color(1f, 0.5f, 0.2f, 1f), 0.4f);
        SlamEffects.SpawnShockwave(bossPos, 4f, 0.4f, new Color(0.7f, 0.15f, 0.1f, 0.8f), 0.25f);
        SlamEffects.SpawnGroundCracks(bossPos, 12, 6f, 4f);
        SlamEffects.SpawnUpwardDebris(bossPos, 12);
        SlamEffects.SpawnImpactFlash(bossPos, 3f, 0.4f);

        // Snap to tiny then disable sprite
        float collapseDuration = 0.1f;
        float collapseElapsed = 0f;
        while (collapseElapsed < collapseDuration)
        {
            collapseElapsed += Time.deltaTime;
            float t = collapseElapsed / collapseDuration;
            transform.localScale = originalScale * Mathf.Lerp(0.5f, 0.1f, t);
            yield return null;
        }

        if (sr != null) sr.enabled = false;

        // ── Beat 5: Silence — let effects settle (1.5s) ──
        yield return new WaitForSeconds(1.5f);

        // ── Beat 6: Victory Screen ──
        if (bossAudio != null)
            bossAudio.TryPlayBossVictoryScreenSfx();

        bool victoryDone = false;
        VictoryScreen.Instance.Play(() =>
        {
            LoadMenuScene();
            victoryDone = true;
        });

        while (!victoryDone)
            yield return null;

        // Boss is truly gone
        Destroy(gameObject);
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

    private void ApplyPhase2Stats()
    {
        moveSpeed *= phase2MoveSpeedMultiplier;
        attackCooldown *= phase2AttackCooldownMultiplier;
        eatDamage = Mathf.RoundToInt(eatDamage * phase2EatDamageMultiplier);
        leapDamage = Mathf.RoundToInt(leapDamage * phase2LeapDamageMultiplier);
        rollSpeed *= phase2RollSpeedMultiplier;
        rollDamage = Mathf.RoundToInt(rollDamage * 1.5f);

        // Swap sprites if Phase 2 overrides are provided
        if (phase2IdleFrame1 != null) idleFrame1 = phase2IdleFrame1;
        if (phase2IdleFrame2 != null) idleFrame2 = phase2IdleFrame2;
        if (phase2WalkFrame1 != null) walkFrame1 = phase2WalkFrame1;
        if (phase2WalkFrame2 != null) walkFrame2 = phase2WalkFrame2;
        if (phase2GrabSprite != null) grabSprite = phase2GrabSprite;
        if (phase2EatSprite != null) eatSprite = phase2EatSprite;
        if (phase2LeapFrame1 != null) leapFrame1 = phase2LeapFrame1;
        if (phase2LeapFrame2 != null) leapFrame2 = phase2LeapFrame2;
        if (phase2LeapFrame3 != null) leapFrame3 = phase2LeapFrame3;
        if (phase2LeapFrame4 != null) leapFrame4 = phase2LeapFrame4;
        if (phase2LeapFrame5 != null) leapFrame5 = phase2LeapFrame5;
    }

    // ==================== MOVEMENT ====================

    private void ChasePlayer()
    {
        float dist = Vector2.Distance(transform.position, player.position);
        float chaseStopDistance = Mathf.Max(stopDistance, grabRange);

        if (dist <= detectionRange && dist > chaseStopDistance)
        {
            Vector2 dir = GetDirectionToPlayer();
            MoveDirectly(dir);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void MoveDirectly(Vector2 desiredDirection)
    {
        Vector2 targetPos = rb.position + desiredDirection.normalized * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPos);
    }

    // ==================== ATTACK SELECTION ====================

    private void HandleAttackCooldown()
    {
        if (attackTimer > 0)
        {
            attackTimer -= Time.fixedDeltaTime;
        }

        // Tick summon cooldown independently (Phase 1 only)
        if (currentPhase == 1 && (meleePrefab != null || rangedPrefab != null))
        {
            summonTimer -= Time.fixedDeltaTime;
        }

        if (leapSlamTimer > 0f)
        {
            leapSlamTimer -= Time.fixedDeltaTime;
        }

        float distToPlayer = Vector2.Distance(transform.position, player.position);

        if (attackTimer > 0) return;

        // Summon takes priority when ready (Phase 1 only, max summonCount alive at once)
        CleanupDeadSummons();
        if (currentPhase == 1 && summonTimer <= 0f && (meleePrefab != null || rangedPrefab != null)
            && activeSummons.Count < summonCount)
        {
            StartCoroutine(SummonAttack());
            return;
        }

        if (distToPlayer <= grabRange)
        {
            StartCoroutine(GrabAndEatAttack());
            return;
        }

        if (distToPlayer >= leapMinRange && distToPlayer <= leapMaxRange && leapSlamTimer <= 0f)
        {
            StartCoroutine(LeapSlamAttack());
        }
    }

    // ==================== THROW CHECK (while chasing) ====================

    private void HandleThrowCheck()
    {
        if (isThrowing || activeSummons.Count == 0) return;

        throwTimer -= Time.fixedDeltaTime;
        if (throwTimer > 0f) return;

        CleanupDeadSummons();
        if (activeSummons.Count == 0) return;

        if (Random.value <= throwChance)
        {
            StartCoroutine(ChaseThrowEnemy());
        }

        throwTimer = throwCooldown;
    }

    private IEnumerator ChaseThrowEnemy()
    {
        // Find closest alive summon
        GameObject throwTarget = null;
        float closestDist = float.MaxValue;

        foreach (GameObject enemy in activeSummons)
        {
            if (enemy == null) continue;
            EnemyHealth eh = enemy.GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead) continue;

            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                throwTarget = enemy;
            }
        }

        if (throwTarget == null) yield break;

        isThrowing = true;
        currentState = BossState.Attacking;
        rb.linearVelocity = Vector2.zero;
        LockPositionForAttack();

        // Pull enemy to boss
        Transform enemyTransform = throwTarget.transform;
        Rigidbody2D enemyRb = throwTarget.GetComponent<Rigidbody2D>();
        EnemyBase enemyBase = throwTarget.GetComponent<EnemyBase>();

        if (enemyBase != null) enemyBase.enabled = false;
        if (enemyRb != null) enemyRb.linearVelocity = Vector2.zero;

        float pullTime = 0.3f;
        float elapsed = 0f;
        Vector2 pullStart = enemyTransform.position;
        while (elapsed < pullTime)
        {
            if (throwTarget == null) break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pullTime);
            enemyTransform.position = Vector2.Lerp(pullStart, transform.position, t);
            yield return null;
        }

        // Hold and throw
        if (sr != null) sr.sprite = grabSprite;
        yield return new WaitForSeconds(0.2f);

        if (throwTarget != null)
        {
            Vector2 throwDir = player != null
                ? ((Vector2)player.position - (Vector2)transform.position).normalized
                : Vector2.right;

            if (bossAudio != null)
                bossAudio.PlayBossTossSfx();
            else if (enemyAudio != null)
                enemyAudio.PlayAttackSfx();

            if (sr != null) sr.sprite = idleFrame1;

            Collider2D bossCol = GetComponent<Collider2D>();
            Collider2D enemyCol = throwTarget.GetComponent<Collider2D>();
            if (bossCol != null && enemyCol != null)
                Physics2D.IgnoreCollision(bossCol, enemyCol, true);

            StartCoroutine(ThrownEnemyFlight(throwTarget, throwDir, bossCol, enemyCol, enemyBase));
        }
        else
        {
            if (sr != null) sr.sprite = idleFrame1;
        }

        UnlockPositionAfterAttack();
        isThrowing = false;
        attackTimer = attackCooldown * 0.5f; // Short cooldown after throw
        StopBossActionSfx();
        currentState = BossState.Chasing;
    }

    // ==================== CONSECUTIVE HIT DETECTION ====================

    private void DetectConsecutiveHits()
    {
        if (isInPhaseTransition || currentPhase < 2) return;

        float currentHP = health.currentHealth;
        if (currentHP < previousHealth)
        {
            float now = Time.time;
            if (now - lastHitTime <= consecutiveHitWindow)
                consecutiveHitCount++;
            else
                consecutiveHitCount = 1;

            lastHitTime = now;

            if (consecutiveHitCount >= consecutiveHitsToRoll
                && !isRolling
                && currentState != BossState.Rolling
                && now >= nextRollAllowedTime)
            {
                consecutiveHitCount = 0;
                nextRollAllowedTime = now + Mathf.Max(0f, rollCooldown);
                StopBossActionSfx();
                StopAllCoroutines();
                UnlockPositionAfterAttack();
                StartCoroutine(RollAttack());
            }
        }
        previousHealth = currentHP;
    }

    // ==================== ANIMATIONS ====================

    private void HandleMovementAnimations()
    {
        animTimer -= Time.fixedDeltaTime;
        if (animTimer <= 0)
        {
            isFrame1 = !isFrame1;
            animTimer = animationSpeed;
        }

        float dist = Vector2.Distance(transform.position, player.position);
        float chaseStopDistance = Mathf.Max(stopDistance, grabRange);
        bool isMoving = (dist <= detectionRange && dist > chaseStopDistance);

        if (bossAudio != null)
            bossAudio.SetBossWalkLoopActive(isMoving);

        if (sr != null)
        {
            if (isMoving)
            {
                sr.sprite = isFrame1 ? walkFrame1 : walkFrame2;
            }
            else
            {
                sr.sprite = isFrame1 ? idleFrame1 : idleFrame2;
            }
        }
    }

    // ==================== GRAB & EAT ATTACK ====================

    private IEnumerator GrabAndEatAttack()
    {
        currentState = BossState.Attacking;
        rb.linearVelocity = Vector2.zero;
        LockPositionForAttack();
        StopBossActionSfx();

        if (bossAudio != null)
            bossAudio.PlayBossGrabSfx();
        else if (enemyAudio != null)
            enemyAudio.PlayAttackSfx();

        if (sr != null)
        {
            sr.sprite = grabSprite;
            sr.color = Color.cyan;
        }

        yield return new WaitForSeconds(grabTelegraphTime);

        if (sr != null) sr.color = defaultColor;

        float distToPlayer = Vector2.Distance(transform.position, player.position);
        PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();

        if (distToPlayer <= grabRange && (playerMovement == null || !playerMovement.IsDashing))
        {
            yield return StartCoroutine(EatPlayer(playerMovement));
            UnlockPositionAfterAttack();
            attackTimer = attackCooldown;
        }
        else
        {
            // Missed — no cooldown, boss can immediately act again
            UnlockPositionAfterAttack();
            attackTimer = 0f;
        }

        StopBossActionSfx();
        currentState = BossState.Chasing;
    }

    private void LockPositionForAttack()
    {
        if (rb == null || hasCachedConstraints) return;

        cachedConstraints = rb.constraints;
        hasCachedConstraints = true;
        rb.constraints = cachedConstraints | RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY;
    }

    private void UnlockPositionAfterAttack()
    {
        if (rb == null || !hasCachedConstraints) return;

        rb.constraints = cachedConstraints;
        hasCachedConstraints = false;
    }

    private IEnumerator EatPlayer(PlayerMovement playerMovement)
    {
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerHealth != null) playerHealth.BecomeInvincible();
        if (playerRb != null) playerRb.linearVelocity = Vector2.zero;

        if (sr != null) sr.sprite = eatSprite;

        if (bossAudio != null)
            bossAudio.SetBossChewLoopActive(true);

        float timer = 0f;
        while (timer < chewDuration)
        {
            player.position = transform.position;
            timer += Time.deltaTime;
            yield return null;
        }

        if (bossAudio != null)
            bossAudio.SetBossChewLoopActive(false);

        Vector2 spitDirection = Random.insideUnitCircle.normalized;
        player.position = (Vector2)transform.position + (spitDirection * 1.5f);

        if (playerMovement != null) playerMovement.enabled = true;

        if (bossAudio != null)
            bossAudio.PlayBossTossSfx();
        else if (enemyAudio != null)
            enemyAudio.PlayAttackSfx();

        // End grab-protection so the bite can deal damage normally.
        if (playerHealth != null)
        {
            playerHealth.isInvincible = false;
            playerHealth.TakeDamage(eatDamage, spitDirection * spitOutForce);
        }

        if (sr != null) sr.sprite = idleFrame1;

        yield return new WaitForSeconds(1f);
    }

    // ==================== LEAP SLAM ATTACK ====================
    // Inspired by Hollow Knight boss slams: telegraph → launch → hang → SLAM → hitpause + effects

    private IEnumerator LeapSlamAttack()
    {
        currentState = BossState.Attacking;
        rb.linearVelocity = Vector2.zero;
        LockPositionForAttack();
        leapSlamTimer = leapSlamCooldown;

        float effectiveLeapAoeRadius = GetEffectiveLeapAoeRadius();
        float aoeScale = effectiveLeapAoeRadius / Mathf.Max(0.01f, leapAoeRadius);

        if (enemyAudio != null)
            enemyAudio.PlayAttackSfx();

        Vector2 startPos = rb.position;
        Vector2 playerPosAtJump = player != null ? (Vector2)player.position : startPos;
        Vector2 toPlayer = playerPosAtJump - startPos;
        Vector2 jumpDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;
        float jumpDistance = Mathf.Clamp(toPlayer.magnitude, leapMinRange, leapMaxRange);
        Vector2 landingPos = ClampToBossArena(startPos + jumpDir * jumpDistance);

        Vector3 originalScale = transform.localScale;
        bool faceRight = jumpDir.x >= 0;

        // ============================
        // PHASE 1: TELEGRAPH (coiling)
        // ============================
        SetAttackSprite(leapFrame1);
        Color telegraphColor = new Color(1f, 0.3f, 0.1f, 1f);

        // Danger zone indicator with fill
        Color ringColor = new Color(1f, 0.15f, 0.1f, 0.5f);
        GameObject telegraphRing = SlamEffects.SpawnTelegraphRing(landingPos, effectiveLeapAoeRadius, ringColor);
        SpriteRenderer telegraphFill = telegraphRing != null ? telegraphRing.GetComponent<SpriteRenderer>() : null;

        float telegraphElapsed = 0f;
        while (telegraphElapsed < slamTelegraphDuration)
        {
            telegraphElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(telegraphElapsed / slamTelegraphDuration);

            // EXTREME squash — coiling like a spring (Hollow Knight style)
            float squashX = Mathf.Lerp(1f, 1.4f, t * t);
            float squashY = Mathf.Lerp(1f, 0.55f, t * t);
            transform.localScale = new Vector3(originalScale.x * squashX, originalScale.y * squashY, originalScale.z);

            // Shake builds in intensity
            float shakeIntensity = t * t * 0.06f;
            float shake = Mathf.Sin(telegraphElapsed * 55f) * shakeIntensity;
            transform.position = (Vector3)startPos + new Vector3(shake, 0f, 0f);

            // Color pulse — gets faster as it charges
            if (sr != null)
            {
                float pulseSpeed = Mathf.Lerp(8f, 25f, t);
                float pulse = Mathf.Sin(telegraphElapsed * pulseSpeed) * 0.5f + 0.5f;
                sr.color = Color.Lerp(defaultColor, telegraphColor, pulse * t);
            }

            // Telegraph ring fill pulses and intensifies
            if (telegraphFill != null)
            {
                float fillAlpha = Mathf.Lerp(0.03f, 0.15f, t) * (Mathf.Sin(telegraphElapsed * 12f) * 0.4f + 0.6f);
                Color fc = telegraphFill.color;
                fc.a = fillAlpha;
                telegraphFill.color = fc;
            }

            yield return null;
        }

        if (telegraphRing != null) Object.Destroy(telegraphRing);

        // ============================
        // PHASE 2: LAUNCH (explosive)
        // ============================

        // Snap to stretch pose — like a spring releasing
        SetAttackSprite(leapFrame2);
        if (sr != null) sr.color = defaultColor;
        transform.localScale = new Vector3(originalScale.x * 0.65f, originalScale.y * 1.5f, originalScale.z);
        transform.position = (Vector3)startPos; // reset shake offset

        // Small dust puff at launch point
        SlamEffects.SpawnLaunchDust(startPos);

        UnlockPositionAfterAttack();
        yield return new WaitForSeconds(0.04f);

        // ============================
        // PHASE 3: AIRBORNE (asymmetric arc — slow rise, hang, fast descent)
        // ============================

        float riseTime = leapTravelTime * 0.45f;    // slow rise
        float hangTime = leapTravelTime * 0.12f;    // brief hang at peak
        float fallTime = leapTravelTime * 0.43f;    // fast descent
        float totalTime = riseTime + hangTime + fallTime;

        float elapsed = 0f;
        int afterimageCounter = 0;
        float afterimageInterval = totalTime / Mathf.Max(1, slamAfterimageCount + 1);
        float nextAfterimageTime = afterimageInterval;

        // Afterimage tint — bluish ghost for phase 1, reddish for phase 2
        Color afterimageTint = currentPhase == 2
            ? new Color(1f, 0.4f, 0.3f, 1f)
            : new Color(0.5f, 0.6f, 1f, 1f);

        while (elapsed < totalTime)
        {
            elapsed += Time.deltaTime;

            // Calculate progress through each sub-phase
            float t; // 0-1 overall horizontal progress
            float arcHeight; // 0-1 height factor

            if (elapsed < riseTime)
            {
                // RISE: ease-out (fast start, slow at top)
                float rt = Mathf.Clamp01(elapsed / riseTime);
                float easedRise = 1f - (1f - rt) * (1f - rt); // ease out
                t = easedRise * 0.5f; // first half of horizontal distance
                arcHeight = easedRise; // rising to peak
            }
            else if (elapsed < riseTime + hangTime)
            {
                // HANG: barely moves — suspended at peak
                float ht = Mathf.Clamp01((elapsed - riseTime) / hangTime);
                t = 0.5f + ht * 0.02f; // almost no horizontal movement
                arcHeight = 1f; // at peak
            }
            else
            {
                // FALL: ease-in (accelerating — gravity!)
                float ft = Mathf.Clamp01((elapsed - riseTime - hangTime) / fallTime);
                float easedFall = ft * ft * ft; // cubic ease-in = heavy slam feel
                t = 0.52f + easedFall * 0.48f; // remaining horizontal distance
                arcHeight = 1f - easedFall; // plummeting down
            }

            // Position
            Vector2 flatPos = Vector2.Lerp(startPos, landingPos, t);
            Vector2 arcOffset = Vector2.up * arcHeight * slamArcHeight;
            rb.MovePosition(flatPos + arcOffset);

            // Scale: bigger at peak (closer to "camera"), compress on descent
            float scaleMod;
            if (elapsed < riseTime + hangTime)
                scaleMod = 1f + arcHeight * 0.25f;
            else
            {
                float ft = Mathf.Clamp01((elapsed - riseTime - hangTime) / fallTime);
                scaleMod = Mathf.Lerp(1.25f, 0.9f, ft); // shrink as it comes down fast
            }
            transform.localScale = originalScale * scaleMod;

            // Sprite: rising vs falling
            bool falling = elapsed > riseTime + hangTime;
            SetAttackSprite(falling ? leapFrame4 : leapFrame3);

            // Rotation — slight tilt, more aggressive on descent
            float rotAmount = falling ? 20f : 10f;
            float rotT = falling
                ? Mathf.Clamp01((elapsed - riseTime - hangTime) / fallTime)
                : Mathf.Clamp01(elapsed / riseTime);
            float rot = Mathf.Sin(rotT * Mathf.PI * 0.5f) * rotAmount * (faceRight ? -1f : 1f);
            transform.rotation = Quaternion.Euler(0f, 0f, rot);

            // Afterimages (Dead Cells style ghost trail)
            if (elapsed > nextAfterimageTime && afterimageCounter < slamAfterimageCount && sr != null)
            {
                SlamEffects.SpawnAfterimage(
                    rb.position, sr.sprite, transform.localScale,
                    afterimageTint, 0.2f, sr.flipX
                );
                afterimageCounter++;
                nextAfterimageTime += afterimageInterval;
            }

            yield return null;
        }

        // ============================
        // PHASE 4: IMPACT (the big moment)
        // ============================

        rb.MovePosition(landingPos);
        transform.rotation = Quaternion.identity;

        // --- HITPAUSE: freeze the game briefly (Hollow Knight's secret sauce) ---
        SetAttackSprite(leapFrame5);
        if (sr != null) sr.color = Color.white; // white flash on boss sprite
        StartCoroutine(SlamEffects.HitPause(slamHitPauseDuration));
        yield return new WaitForSecondsRealtime(slamHitPauseDuration);
        if (sr != null) sr.color = defaultColor;

        // --- EXTREME SQUASH on landing (exaggerated like Hollow Knight) ---
        float squashDuration = 0.1f;
        float squashElapsed = 0f;
        while (squashElapsed < squashDuration)
        {
            squashElapsed += Time.deltaTime;
            float st = Mathf.Clamp01(squashElapsed / squashDuration);

            // Overshoot recovery: squash → overshoot tall → settle
            float sx, sy;
            if (st < 0.4f)
            {
                // Squash phase
                float p = st / 0.4f;
                sx = Mathf.Lerp(1.6f, 0.85f, p);
                sy = Mathf.Lerp(0.4f, 1.1f, p);
            }
            else
            {
                // Settle phase (overshoot → normal)
                float p = (st - 0.4f) / 0.6f;
                sx = Mathf.Lerp(0.85f, 1f, p);
                sy = Mathf.Lerp(1.1f, 1f, p);
            }
            transform.localScale = new Vector3(originalScale.x * sx, originalScale.y * sy, originalScale.z);
            yield return null;
        }
        transform.localScale = originalScale;

        // --- ALL IMPACT EFFECTS (fire simultaneously) ---

        // Screen shake
        StartCoroutine(SlamEffects.ScreenShake(slamScreenShakeDuration, slamScreenShakeMagnitude));

        // Full-screen white flash (Hollow Knight style)
        float flashAlpha = currentPhase == 2 ? 0.35f : 0.25f;
        SlamEffects.SpawnScreenFlash(0.15f, flashAlpha);

        // Impact point flash
        SlamEffects.SpawnImpactFlash(landingPos, effectiveLeapAoeRadius * 0.4f, 0.15f);

        // Thick shockwave ring
        Color shockColor = currentPhase == 2
            ? new Color(1f, 0.3f, 0.1f, 0.9f)
            : new Color(0.95f, 0.85f, 0.5f, 0.8f);
        float ringWidth = currentPhase == 2 ? 0.25f : 0.18f;
        float effectiveShockwaveRadius = slamShockwaveRadius * aoeScale;
        SlamEffects.SpawnShockwave(landingPos, effectiveShockwaveRadius, 0.4f, shockColor, ringWidth);

        // Ground cracks
        int cracks = currentPhase == 2 ? slamCrackCount + 2 : slamCrackCount;
        float crackLen = (currentPhase == 2 ? slamCrackLength * 1.3f : slamCrackLength) * aoeScale;
        SlamEffects.SpawnGroundCracks(landingPos, cracks, crackLen, slamCrackDuration);

        // Ground dust puffs (Hollow Knight — two horizontal clouds)
        float puffSpread = (currentPhase == 2 ? 2.5f : 2f) * aoeScale;
        SlamEffects.SpawnGroundDustPuffs(landingPos, puffSpread, 0.5f);

        // Upward debris chunks (rocks flying up from impact)
        int debrisCount = currentPhase == 2 ? 6 : 4;
        SlamEffects.SpawnUpwardDebris(landingPos, debrisCount);

        if (bossAudio != null)
            bossAudio.PlayBossGroundSlamSfx();
        else if (enemyAudio != null)
            enemyAudio.PlayAttackSfx();

        // Deal damage
        DoLeapExplosion();

        // Boss white flash recovery (2 more flickers)
        StartCoroutine(SlamEffects.SpriteWhiteFlash(sr, defaultColor, 2));

        // Dramatic pause
        yield return new WaitForSeconds(leapLandPause);

        if (sr != null) sr.sprite = idleFrame1;

        attackTimer = attackCooldown;
        StopBossActionSfx();
        currentState = BossState.Chasing;
    }

    // ==================== SUMMON ATTACK (Phase 1) ====================

    private IEnumerator SummonAttack()
    {
        currentState = BossState.Attacking;
        rb.linearVelocity = Vector2.zero;
        LockPositionForAttack();

        bool playedSummonSfx = false;
        if (bossAudio != null)
            playedSummonSfx = bossAudio.TryPlayBossSummonSfx();

        if (!playedSummonSfx && enemyAudio != null)
            enemyAudio.PlayAttackSfx();

        // --- TELEGRAPH: Boss glows and shakes ---
        Color originalColor = sr != null ? sr.color : defaultColor;
        Vector3 originalPos = transform.position;

        if (sr != null) sr.color = new Color(1f, 0.5f, 0f, 1f); // Orange glow

        float telegraphTime = 0.6f;
        float elapsed = 0f;
        while (elapsed < telegraphTime)
        {
            elapsed += Time.deltaTime;
            // Shake effect
            float shake = Mathf.Sin(elapsed * 40f) * 0.05f;
            transform.position = originalPos + new Vector3(shake, 0f, 0f);
            yield return null;
        }
        transform.position = originalPos;

        // --- SUMMON ANIMATION: Spawn only enough to fill up to max ---
        CleanupDeadSummons();
        int toSpawn = summonCount - activeSummons.Count;
        GameObject[] spawnedEnemies = new GameObject[toSpawn];

        for (int i = 0; i < toSpawn; i++)
        {
            // Pick melee or ranged based on meleeSpawnChance
            GameObject prefab = null;
            if (meleePrefab != null && rangedPrefab != null)
                prefab = Random.value <= meleeSpawnChance ? meleePrefab : rangedPrefab;
            else if (meleePrefab != null)
                prefab = meleePrefab;
            else if (rangedPrefab != null)
                prefab = rangedPrefab;

            if (prefab == null) continue;

            // Spawn position: evenly spread in a circle around the boss
            float angle = (360f / toSpawn) * i + Random.Range(-20f, 20f);
            Vector2 offset = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            ) * summonSpawnRadius;

            Vector2 spawnPos = (Vector2)transform.position + offset;

            // Ensure spawn position is inside arena (not in a wall)
            foreach (Vector2 dir in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
            {
                RaycastHit2D hit = Physics2D.Raycast(spawnPos, dir, 1f, obstacleLayer);
                if (hit.collider != null && hit.distance < 0.8f)
                    spawnPos += hit.normal * (0.8f - hit.distance);
            }

            // Instantiate and track
            GameObject spawned = Instantiate(prefab, spawnPos, Quaternion.identity);
            EnemyHealth spawnedHealth = spawned.GetComponent<EnemyHealth>();
            if (spawnedHealth != null)
                spawnedHealth.canRespawn = false;
            spawnedEnemies[i] = spawned;
            activeSummons.Add(spawned);

            // Pop-in effect: start small, scale up
            StartCoroutine(SpawnPopEffect(spawned.transform));

            // Flash boss sprite for each summon
            if (sr != null) sr.color = Color.white;
            yield return new WaitForSeconds(summonAnimDuration / Mathf.Max(1, toSpawn));
            if (sr != null) sr.color = new Color(1f, 0.5f, 0f, 1f);
        }

        // --- BOSS COLOR RESET ---
        if (sr != null) sr.color = originalColor;

        // --- THROW CHECK: chance to grab a nearby summoned enemy and throw it ---
        if (Random.value <= throwChance)
        {
            yield return StartCoroutine(TryThrowEnemy(spawnedEnemies));
        }

        // Done
        UnlockPositionAfterAttack();
        summonTimer = summonCooldown;
        attackTimer = attackCooldown;
        StopBossActionSfx();
        currentState = BossState.Chasing;
    }

    private IEnumerator SpawnPopEffect(Transform target)
    {
        if (target == null) yield break;

        Vector3 fullScale = target.localScale;
        target.localScale = Vector3.zero;

        float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Overshoot then settle
            float scale = t < 0.7f
                ? Mathf.Lerp(0f, 1.3f, t / 0.7f)
                : Mathf.Lerp(1.3f, 1f, (t - 0.7f) / 0.3f);
            target.localScale = fullScale * scale;
            yield return null;
        }

        if (target != null)
            target.localScale = fullScale;
    }

    private IEnumerator TryThrowEnemy(GameObject[] spawnedEnemies)
    {
        // Find the closest spawned enemy that's still alive
        GameObject throwTarget = null;
        float closestDist = float.MaxValue;

        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy == null) continue;
            EnemyHealth eh = enemy.GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead) continue;

            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                throwTarget = enemy;
            }
        }

        if (throwTarget == null) yield break;

        // --- GRAB THE ENEMY: pull it toward the boss ---
        Transform enemyTransform = throwTarget.transform;
        Rigidbody2D enemyRb = throwTarget.GetComponent<Rigidbody2D>();
        EnemyBase enemyBase = throwTarget.GetComponent<EnemyBase>();

        // Disable the enemy's AI while being thrown
        if (enemyBase != null) enemyBase.enabled = false;
        if (enemyRb != null) enemyRb.linearVelocity = Vector2.zero;

        // Pull enemy to boss over 0.3s
        float pullTime = 0.3f;
        float elapsed = 0f;
        Vector2 pullStart = enemyTransform.position;
        while (elapsed < pullTime)
        {
            if (throwTarget == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pullTime);
            enemyTransform.position = Vector2.Lerp(pullStart, transform.position, t);
            yield return null;
        }

        // Brief hold
        if (sr != null) sr.sprite = grabSprite;
        yield return new WaitForSeconds(0.2f);

        if (throwTarget == null) yield break;

        // --- THROW: launch the enemy at the player ---
        Vector2 throwDir = player != null
            ? ((Vector2)player.position - (Vector2)transform.position).normalized
            : Vector2.right;

        if (bossAudio != null)
            bossAudio.PlayBossTossSfx();
        else if (enemyAudio != null)
            enemyAudio.PlayAttackSfx();

        if (sr != null) sr.sprite = idleFrame1;

        // Disable enemy collider with boss so it doesn't bounce off
        Collider2D bossCol = GetComponent<Collider2D>();
        Collider2D enemyCol = throwTarget.GetComponent<Collider2D>();
        if (bossCol != null && enemyCol != null)
            Physics2D.IgnoreCollision(bossCol, enemyCol, true);

        // Launch as a projectile — fly in a straight line
        StartCoroutine(ThrownEnemyFlight(throwTarget, throwDir, bossCol, enemyCol, enemyBase));
    }

    private IEnumerator ThrownEnemyFlight(GameObject enemy, Vector2 direction, Collider2D bossCol, Collider2D enemyCol, EnemyBase enemyBase)
    {
        float flightTime = 0f;
        float maxFlightTime = 2f;
        Rigidbody2D enemyRb = enemy != null ? enemy.GetComponent<Rigidbody2D>() : null;

        while (flightTime < maxFlightTime)
        {
            if (enemy == null) yield break;

            flightTime += Time.deltaTime;

            // Check for wall BEFORE moving — prevents phasing through
            RaycastHit2D wallHit = Physics2D.Raycast(enemy.transform.position, direction, throwSpeed * Time.deltaTime + 0.5f, obstacleLayer);
            if (wallHit.collider != null)
            {
                // Snap to wall hit point (with small offset so not inside wall)
                enemy.transform.position = (Vector3)(wallHit.point + wallHit.normal * 0.5f);
                break;
            }

            // Move via Rigidbody if available (respects colliders), else transform
            if (enemyRb != null)
                enemyRb.MovePosition((Vector2)enemy.transform.position + direction * throwSpeed * Time.deltaTime);
            else
                enemy.transform.position += (Vector3)(direction * throwSpeed * Time.deltaTime);

            // Spin
            enemy.transform.Rotate(0f, 0f, 900f * Time.deltaTime);

            // Player hit check
            if (player != null)
            {
                float dist = Vector2.Distance(enemy.transform.position, player.position);
                if (dist < 1.2f)
                {
                    PlayerMovement pm = player.GetComponent<PlayerMovement>();
                    if (pm == null || !pm.IsDashing)
                    {
                        PlayerHealth ph = player.GetComponent<PlayerHealth>();
                        if (ph != null)
                            ph.TakeDamage(throwDamage, direction * throwKnockbackForce);
                    }
                    break;
                }
            }

            yield return null;
        }

        // Landing: restore the enemy to normal
        if (enemy != null)
        {
            enemy.transform.rotation = Quaternion.identity;

            // Clamp position inside arena bounds to prevent out-of-bounds state
            ClampToArenaBounds(enemy.transform);

            if (bossCol != null && enemyCol != null)
                Physics2D.IgnoreCollision(bossCol, enemyCol, false);

            if (enemyBase != null) enemyBase.enabled = true;
            if (enemyRb != null) enemyRb.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// Safety clamp: ensures a transform is within the arena walls.
    /// Uses raycasts to find the nearest wall in each direction and clamps.
    /// </summary>
    private void ClampToArenaBounds(Transform target, float margin = 1f)
    {
        Vector2 pos = target.position;

        // Cast from the target outward in all 4 directions to find nearby walls
        Vector2[] dirs = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        foreach (Vector2 dir in dirs)
        {
            RaycastHit2D hit = Physics2D.Raycast(pos, dir, margin + 0.5f, obstacleLayer);
            if (hit.collider != null && hit.distance < margin)
            {
                pos += hit.normal * (margin - hit.distance + 0.05f);
            }
        }

        target.position = new Vector3(pos.x, pos.y, target.position.z);
    }

    // ==================== ROLL ATTACK ====================
    // Uses a temporary CircleCollider2D with a bouncy material for the roll.
    // Circles bounce perfectly off walls — no wedging, no corner catching.
    // The PolygonCollider2D is disabled during the roll and restored after.

    private CircleCollider2D rollCircle;

    private IEnumerator RollAttack()
    {
        currentState = BossState.Rolling;
        isRolling = true;
        rb.linearVelocity = Vector2.zero;

        bool playedRollStartSfx = false;
        bool usingBossRollLoop = false;

        if (bossAudio != null)
        {
            playedRollStartSfx = bossAudio.TryPlayBossRollStartSfx();
            usingBossRollLoop = bossAudio.TrySetBossRollLoopActive(true);
        }

        if (enemyAudio != null)
        {
            if (!playedRollStartSfx)
                enemyAudio.PlayAttackSfx();

            enemyAudio.SetRollLoopActive(!usingBossRollLoop);
        }

        RigidbodyConstraints2D preRollConstraints = rb.constraints;
        rb.constraints = RigidbodyConstraints2D.None; // allow physics rotation too

        // --- SWAP COLLIDER: Polygon → Circle ---
        Collider2D polyCollider = GetComponent<PolygonCollider2D>();
        if (polyCollider == null) polyCollider = GetComponent<Collider2D>();
        if (polyCollider != null) polyCollider.enabled = false;

        // Create a circle collider sized to fit the boss roughly
        rollCircle = gameObject.AddComponent<CircleCollider2D>();
        rollCircle.radius = 0.7f; // fits inside the boss sprite
        rollCircle.offset = Vector2.zero;

        // Bouncy material: perfect elastic bounce, no friction
        activeBounceMat = new PhysicsMaterial2D("RollBounce");
        activeBounceMat.bounciness = 1f;
        activeBounceMat.friction = 0f;
        rollCircle.sharedMaterial = activeBounceMat;

        // Continuous collision detection prevents tunneling through walls
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Ignore player collision
        Collider2D playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
        if (playerCollider != null)
            Physics2D.IgnoreCollision(rollCircle, playerCollider, true);

        // --- TELEGRAPH ---
        if (sr != null) sr.color = Color.yellow;
        yield return new WaitForSeconds(0.3f);
        if (sr != null) sr.color = defaultColor;

        // --- LAUNCH ---
        // Pick a diagonal direction (avoids pure axis-aligned which can cause repetitive bouncing)
        float randomAngle = Random.Range(20f, 70f) + 90f * Random.Range(0, 4);
        Vector2 rollDir = new Vector2(Mathf.Cos(randomAngle * Mathf.Deg2Rad), Mathf.Sin(randomAngle * Mathf.Deg2Rad)).normalized;
        rb.linearVelocity = rollDir * rollSpeed;

        float elapsed = 0f;

        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;

            // Maintain constant speed — physics handles the direction/bouncing
            if (rb.linearVelocity.sqrMagnitude > 0.1f)
                rb.linearVelocity = rb.linearVelocity.normalized * rollSpeed;
            else
                rb.linearVelocity = rollDir * rollSpeed; // nudge if somehow stopped

            // Update rollDir from actual physics velocity (so we track bounces)
            if (rb.linearVelocity.sqrMagnitude > 0.1f)
                rollDir = rb.linearVelocity.normalized;

            // Bounce off arena edges instead of clamping, which can wedge against walls.
            HandleRollArenaBounce(ref rollDir);

            // Spin visual
            transform.Rotate(0f, 0f, rollRotationSpeed * Time.deltaTime);

            // Player damage check (proximity-based, not collision-based since we ignore player)
            if (player != null && Time.time - lastRollHitTime > 0.5f)
            {
                float dist = Vector2.Distance(transform.position, player.position);
                if (dist < 1.5f)
                {
                    PlayerMovement pm = player.GetComponent<PlayerMovement>();
                    if (pm == null || !pm.IsDashing)
                    {
                        PlayerHealth ph = player.GetComponent<PlayerHealth>();
                        if (ph != null)
                        {
                            Vector2 knockDir = ((Vector2)player.position - (Vector2)transform.position).normalized;
                            if (knockDir.sqrMagnitude < 0.0001f) knockDir = Vector2.up;
                            ph.TakeDamage(rollDamage, knockDir * rollKnockbackForce);
                            lastRollHitTime = Time.time;
                        }
                    }
                }
            }

            yield return null;
        }

        // --- CLEANUP: Circle → Polygon ---
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.rotation = Quaternion.identity;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

        // Restore player collision before destroying circle
        if (playerCollider != null && rollCircle != null)
            Physics2D.IgnoreCollision(rollCircle, playerCollider, false);

        // Remove the circle collider
        if (rollCircle != null)
        {
            Destroy(rollCircle);
            rollCircle = null;
        }

        // Clean up bounce material
        CleanupBounceMaterial();

        // Re-enable the original collider
        if (polyCollider != null) polyCollider.enabled = true;

        rb.constraints = preRollConstraints;
        isRolling = false;

        if (enemyAudio != null)
            enemyAudio.SetRollLoopActive(false);

        if (bossAudio != null)
            bossAudio.TrySetBossRollLoopActive(false);

        if (sr != null) sr.sprite = idleFrame1;

        attackTimer = attackCooldown;
        StopBossActionSfx();
        currentState = BossState.Chasing;
    }

    private void HandleRollArenaBounce(ref Vector2 rollDir)
    {
        if (rb == null)
            return;

        Vector2 pos = rb.position;
        Vector2 vel = rb.linearVelocity;
        bool bounced = false;

        float minX = bossArenaMinX + rollBouncePadding;
        float maxX = bossArenaMaxX - rollBouncePadding;
        float minY = bossArenaMinY + rollBouncePadding;
        float maxY = bossArenaMaxY - rollBouncePadding;

        if (pos.x <= minX && vel.x < 0f)
        {
            pos.x = minX;
            vel.x = Mathf.Abs(vel.x);
            bounced = true;
        }
        else if (pos.x >= maxX && vel.x > 0f)
        {
            pos.x = maxX;
            vel.x = -Mathf.Abs(vel.x);
            bounced = true;
        }

        if (pos.y <= minY && vel.y < 0f)
        {
            pos.y = minY;
            vel.y = Mathf.Abs(vel.y);
            bounced = true;
        }
        else if (pos.y >= maxY && vel.y > 0f)
        {
            pos.y = maxY;
            vel.y = -Mathf.Abs(vel.y);
            bounced = true;
        }

        if (!bounced)
            return;

        if (vel.sqrMagnitude < 0.01f)
            vel = rollDir.sqrMagnitude > 0.0001f ? rollDir : FindMostOpenDirection();

        rb.position = pos;
        rb.linearVelocity = vel.normalized * rollSpeed;
        rollDir = rb.linearVelocity.normalized;
    }

    private void CleanupBounceMaterial()
    {
        if (activeBounceMat != null)
        {
            Destroy(activeBounceMat);
            activeBounceMat = null;
        }
    }

    private void StopBossActionSfx()
    {
        if (bossAudio != null)
            bossAudio.StopBossActionSfx();
    }

    // ==================== HELPERS ====================

    private void CleanupDeadSummons()
    {
        for (int i = activeSummons.Count - 1; i >= 0; i--)
        {
            if (activeSummons[i] == null)
            {
                activeSummons.RemoveAt(i);
                continue;
            }
            EnemyHealth eh = activeSummons[i].GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead)
            {
                activeSummons.RemoveAt(i);
            }
        }
    }

    private Vector2 FindMostOpenDirection()
    {
        Vector2 bestDir = Vector2.right;
        float bestDist = 0f;

        for (int i = 0; i < 16; i++)
        {
            float angle = i * 22.5f * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, 30f, obstacleLayer);
            float clearDist = hit.collider != null ? hit.distance : 30f;

            if (clearDist > bestDist)
            {
                bestDist = clearDist;
                bestDir = dir;
            }
        }

        return bestDir;
    }

    private void DoLeapExplosion()
    {
        if (player == null) return;

        // Phase 2 can scale slam AoE radius with a configurable multiplier.
        float effectiveRadius = GetEffectiveLeapAoeRadius();

        Collider2D playerHit = Physics2D.OverlapCircle(transform.position, effectiveRadius, playerLayer);

        if (playerHit == null)
        {
            float dist = Vector2.Distance(transform.position, player.position);
            if (dist > effectiveRadius) return;
            playerHit = player.GetComponent<Collider2D>();
        }

        if (!TryResolvePlayerFromCollider(playerHit, out PlayerHealth ph, out PlayerMovement pm, out bool isFeetHitbox))
        {
            // Fallback to known player references for backward compatibility.
            ph = player != null ? player.GetComponent<PlayerHealth>() : null;
            pm = player != null ? player.GetComponent<PlayerMovement>() : null;
            isFeetHitbox = false;
        }

        if (ph == null)
            return;

        if (isFeetHitbox)
            return;

        if (pm != null && pm.IsDashing) return;

        Vector2 knockDir = ((Vector2)player.position - (Vector2)transform.position).normalized;
        if (knockDir.sqrMagnitude < 0.0001f) knockDir = Vector2.up;

        // Stronger knockback in Phase 2
        float knockForce = currentPhase == 2 ? leapKnockbackForce * 1.4f : leapKnockbackForce;
        ph.TakeDamage(leapDamage, knockDir * knockForce);
    }

    private float GetEffectiveLeapAoeRadius()
    {
        return currentPhase == 2
            ? leapAoeRadius * Mathf.Max(0.1f, phase2LeapAoeRadiusMultiplier)
            : leapAoeRadius;
    }

    private static bool TryResolvePlayerFromCollider(
        Collider2D collider,
        out PlayerHealth playerHealth,
        out PlayerMovement playerMovement,
        out bool isFeetHitbox)
    {
        playerHealth = null;
        playerMovement = null;
        isFeetHitbox = false;

        if (collider == null)
            return false;

        PlayerHitbox2D hitbox = collider.GetComponent<PlayerHitbox2D>();
        if (hitbox == null)
            hitbox = collider.GetComponentInParent<PlayerHitbox2D>();

        if (hitbox != null)
        {
            playerHealth = hitbox.playerHealth != null ? hitbox.playerHealth : hitbox.GetComponentInParent<PlayerHealth>();
            playerMovement = hitbox.playerMovement != null ? hitbox.playerMovement : hitbox.GetComponentInParent<PlayerMovement>();
            isFeetHitbox = hitbox.IsFeetHitbox;
            return playerHealth != null;
        }

        playerHealth = collider.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = collider.GetComponentInParent<PlayerHealth>();

        playerMovement = collider.GetComponent<PlayerMovement>();
        if (playerMovement == null)
            playerMovement = collider.GetComponentInParent<PlayerMovement>();

        return playerHealth != null;
    }

    private void SetAttackSprite(Sprite attackSprite)
    {
        if (sr != null && attackSprite != null)
        {
            sr.sprite = attackSprite;
        }
    }

    private Vector2 GetBestSpitDirection()
    {
        Vector2[] directions = {
            Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(1, 1).normalized,
            new Vector2(1, -1).normalized,
            new Vector2(-1, 1).normalized,
            new Vector2(-1, -1).normalized
        };

        Vector2 bestDirection = Vector2.zero;
        float maxDistance = -1f;

        foreach (Vector2 dir in directions)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, 20f, obstacleLayer);
            float clearDistance = (hit.collider != null) ? hit.distance : 20f;

            if (clearDistance > maxDistance)
            {
                maxDistance = clearDistance;
                bestDirection = dir;
            }
        }

        return bestDirection;
    }
}
