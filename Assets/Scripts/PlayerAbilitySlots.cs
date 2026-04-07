using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum SuperEnhancerSlot
{
    TemporalTrailDetonation,
    TemporalCloneAssault,
    InfiniteStamina
}

public enum DashEnhancerSlot
{
    ProjectileGhostDashes,
    ChainAutoAssist
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerAbilitySlots : MonoBehaviour
{
    [Header("Ability Slot - Super")]
    public SuperEnhancerSlot equippedSuperEnhancer = SuperEnhancerSlot.TemporalTrailDetonation;
    public KeyCode superActivationKey = KeyCode.LeftShift;
    [Range(0.05f, 1f)] public float superTimeScale = 0.2f;
    [Range(0f, 1f)] public float superComboTimerRate = 0f;
    [FormerlySerializedAs("temporalSuperDashStaminaCostMultiplier")]
    [Min(0f)] public float StaminaCostMultipler = 1f;

    [Header("Super Option A: Trail Detonation")]
    public float trailDetonationDamage = 18f;
    public float trailDetonationRadius = 0.6f;
    [Min(1)] public int trailDetonationSamplesPerUnit = 3;
    [Min(0.01f)] public float trailDetonationPreviewWidth = 0.2f;
    [Min(1f)] public float trailDetonationPreviewMaxWidthMultiplier = 1.8f;
    [Range(0f, 1f)] public float trailDetonationPreviewMaxBrighten = 0.6f;
    [Range(0f, 1f)] public float trailDetonationPreviewMaxAlphaBoost = 0.25f;
    public Color trailDetonationPreviewColor = new Color(1f, 0.7f, 0.35f, 0.7f);

    [Header("Super Option B: Clone Assault")]
    public float cloneDashDamage = 14f;
    public float cloneDashRadius = 0.55f;
    public float cloneDashDuration = 0.18f;
    [Range(0.1f, 1f)] public float cloneAlpha = 0.5f;
    [Min(0f)] public float cloneComboBonusPerHit = 0.12f;
    [Min(1f)] public float cloneMaxDamageMultiplier = 3f;
    [Min(1f)] public float cloneMaxRadiusMultiplier = 1.8f;
    [Min(1f)] public float cloneMaxVisualScaleMultiplier = 2f;

    [Header("Ability Slot - Dash Enhancer")]
    public DashEnhancerSlot equippedDashEnhancer = DashEnhancerSlot.ProjectileGhostDashes;
    public KeyCode dashEnhancerActivationKey = KeyCode.F;
    public float dashEnhancerCooldown = 10f;

    [Header("Dash Option A: Projectile Ghost Dashes")]
    public float projectileModeDuration = 8f;
    public float projectileGhostRange = 7f;
    public float projectileGhostRadius = 0.45f;
    public float projectileGhostDamageMultiplier = 1.35f;
    public float projectileGhostKnockbackMultiplier = 0.8f;
    public float projectileGhostFireRate = 0.18f;
    public float projectileGhostVisualSpeed = 24f;
    [Min(0.1f)] public float projectileGhostMoveSpeedMultiplier = 1.35f;
    public Color projectileGhostColor = new Color(0.6f, 0.95f, 1f, 0.65f);

    [Header("Dash Option B: Chain Auto-Assist")]
    public int minimumChainHits = 1;
    public int chainHitBonus = 0;
    public int maxChainHits = 12;
    public float chainSearchRadius = 14f;
    public float chainDashDuration = 0.08f;
    public float chainDashGapSeconds = 0.04f;
    public float chainArrivalOffset = 0.5f;
    public float chainDashDamageMultiplier = 1.75f;
    [Min(0f)] public float chainDamageBonusPerComboHit = 0.15f;
    [Range(0f, 1f)] public float chainDamageDissipationPerHit = 0.2f;
    [Min(0.1f)] public float chainMinimumDamageMultiplier = 0.35f;
    public float chainStunDuration = 0.35f;
    public float chainLightningTrailWidth = 0.2f;
    public Color chainLightningTrailColor = new Color(0.55f, 0.95f, 1f, 0.95f);

    [Header("UI - Super Ability")]
    public Image superChargeFillImage;
    public Image superActiveDurationFillImage;
    public GameObject superReadyIndicator;

    [Header("UI - Dash Enhancer")]
    public Image dashEnhancerCooldownFillImage;
    public Image dashEnhancerActiveFillImage;
    public GameObject dashEnhancerReadyIndicator;
    [FormerlySerializedAs("dashEnchancerDurationFill")]
    public Image dashEnhancerDurationFill;

    [Header("UI - Ability Ready Circle VFX")]
    public bool enableAbilityReadyCircleVfx = true;
    public Color abilityReadyGlowColor = new Color(1f, 0.92f, 0.45f, 1f);
    [Min(0.1f)] public float abilityReadyGlowPulseSpeed = 7f;
    [Min(0.1f)] public float abilityReadyFlickerSpeed = 16f;
    [Range(0f, 1f)] public float abilityReadyFlickerAmount = 0.3f;
    [Range(0f, 0.3f)] public float abilityReadyScalePulseAmount = 0.08f;

    private struct DashPathSegment
    {
        public Vector2 start;
        public Vector2 end;
        public float chargeMultiplier;
    }

    private class CloneDashRecord
    {
        public GameObject cloneObject;
        public Vector2 dashTarget;
        public Vector3 baseLocalScale = Vector3.one;
        public float damageMultiplier = 1f;
        public float radiusMultiplier = 1f;
        public float visualScaleMultiplier = 1f;
    }

    private PlayerMovement player;
    private Coroutine superCoroutine;
    private Coroutine dashEnhancerCoroutine;

    private bool isProjectileModeActive;
    private bool isChainAssistActive;
    private float nextGhostProjectileShotTime;
    private float nextDashEnhancerReadyTime;
    private float superEndUnscaledTime;
    private float projectileModeEndUnscaledTime;

    private bool isSuperTimeApplied;
    private float preSuperTimeScale = 1f;
    private float preSuperFixedDeltaTime;
    private float playerSuperSpeedCompensation = 1f;

    private readonly List<DashPathSegment> recordedSuperDashSegments = new List<DashPathSegment>();
    private readonly List<CloneDashRecord> recordedSuperCloneDashes = new List<CloneDashRecord>();
    private readonly List<LineRenderer> activeTemporalTrailPreviews = new List<LineRenderer>();
    private readonly Dictionary<Collider2D, bool> superIgnoredEnemyColliders = new Dictionary<Collider2D, bool>();
    private const float SuperCollisionRefreshSeconds = 0.1f;
    private LineRenderer activeChainLightningTrail;
    private bool isTemporalTrailTimingAdjusted;
    private float preTemporalTrailTime;

    private bool wasSuperReady;
    private bool wasDashEnhancerReady;
    private bool hasReadyStateInitialized;
    private Image dashEnhancerReadyIndicatorImage;
    private Color dashEnhancerReadyBaseColor = Color.white;
    private Vector3 dashEnhancerReadyBaseScale = Vector3.one;
    private bool hasDashEnhancerReadyBaseVisualState;

    public bool IsChainAssistActive => isChainAssistActive;
    public bool IsProjectileModeActive => isProjectileModeActive;

    public bool IsInfiniteStaminaSuperActive
    {
        get
        {
            PlayerMovement owner = GetPlayer();
            return owner != null
                && owner.isSuperActive
                && equippedSuperEnhancer == SuperEnhancerSlot.InfiniteStamina;
        }
    }

    public bool IsTemporalSuperActive
    {
        get
        {
            PlayerMovement owner = GetPlayer();
            return owner != null
                && owner.isSuperActive
                && IsTemporalSuperEquipped();
        }
    }

    public float GhostDashMoveSpeedMultiplier => isProjectileModeActive ? Mathf.Max(0.1f, projectileGhostMoveSpeedMultiplier) : 1f;
    public float PlayerTimeCompensation => isSuperTimeApplied ? playerSuperSpeedCompensation : 1f;
    public float SuperComboTimerRate => Mathf.Clamp01(superComboTimerRate);

    private static T FindUiComponentByName<T>(params string[] candidateNames) where T : Component
    {
        for (int i = 0; i < candidateNames.Length; i++)
        {
            string name = candidateNames[i];
            if (string.IsNullOrEmpty(name))
                continue;

            GameObject go = FindInActiveSceneByName(name);
            if (go == null)
                continue;

            T direct = go.GetComponent<T>();
            if (direct != null)
                return direct;

            T child = go.GetComponentInChildren<T>(true);
            if (child != null)
                return child;
        }

        return null;
    }

    private static GameObject FindInActiveSceneByName(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return null;

        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChildByNameRecursive(roots[i].transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindChildByNameRecursive(Transform parent, string objectName)
    {
        if (parent == null)
            return null;

        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildByNameRecursive(parent.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void EnsureImageUsesRadialFill(Image image)
    {
        if (image == null)
            return;

        if (image.type != Image.Type.Filled)
            image.type = Image.Type.Filled;

        if (image.fillMethod != Image.FillMethod.Radial360)
            image.fillMethod = Image.FillMethod.Radial360;
    }

    private void AutoBindUiReferences()
    {
        if (superChargeFillImage == null)
            superChargeFillImage = FindUiComponentByName<Image>("SuperFill", "SuperMeterFill");

        if (superActiveDurationFillImage == null)
            superActiveDurationFillImage = FindUiComponentByName<Image>("SuperOverlay", "SuperDuration", "SuperActiveDuration");

        if (superReadyIndicator == null)
        {
            RectTransform superReady = FindUiComponentByName<RectTransform>("SuperReady", "SuperReadyIndicator");
            if (superReady != null)
                superReadyIndicator = superReady.gameObject;
        }

        if (dashEnhancerCooldownFillImage == null)
            dashEnhancerCooldownFillImage = FindUiComponentByName<Image>("DashEnhancerCooldownFill", "AbilityCooldownFill", "AbilitySlot", "AbilitySlotFill");

        EnsureImageUsesRadialFill(dashEnhancerCooldownFillImage);

        if (dashEnhancerDurationFill == null)
            dashEnhancerDurationFill = FindUiComponentByName<Image>("AbilityDuration", "AbilityActiveDuration", "DashEnhancerDurationFill");

        if (dashEnhancerActiveFillImage == null)
            dashEnhancerActiveFillImage = dashEnhancerDurationFill;

        if (dashEnhancerReadyIndicator == null)
        {
            RectTransform ready = FindUiComponentByName<RectTransform>("AbilityReady", "AbilityReadyIndicator", "DashEnhancerReady");
            if (ready != null)
                dashEnhancerReadyIndicator = ready.gameObject;
        }

        if (dashEnhancerReadyIndicatorImage == null && dashEnhancerReadyIndicator != null)
            dashEnhancerReadyIndicatorImage = dashEnhancerReadyIndicator.GetComponent<Image>();
    }

    public float GetTemporalSuperDashStaminaCost(float defaultDashStaminaCost)
    {
        if (!IsTemporalSuperActive)
            return 0f;

        float safeDefaultCost = Mathf.Max(0f, defaultDashStaminaCost);
        float safeMultiplier = Mathf.Max(0f, StaminaCostMultipler);
        return safeDefaultCost * safeMultiplier;
    }

    private void Awake()
    {
        player = GetComponent<PlayerMovement>();
        AutoBindUiReferences();
        CacheAbilityReadyIndicatorBaseState();
    }

    private void Update()
    {
        UpdateCooldownUI();
    }

    private void OnDisable()
    {
        ResetAbilityReadyIndicatorVfx();
        StopAndCleanup();
    }

    private void CacheAbilityReadyIndicatorBaseState()
    {
        if (dashEnhancerReadyIndicator == null)
            return;

        if (dashEnhancerReadyIndicatorImage == null)
            dashEnhancerReadyIndicatorImage = dashEnhancerReadyIndicator.GetComponent<Image>();

        if (dashEnhancerReadyIndicatorImage != null)
            dashEnhancerReadyBaseColor = dashEnhancerReadyIndicatorImage.color;

        dashEnhancerReadyBaseScale = dashEnhancerReadyIndicator.transform.localScale;
        hasDashEnhancerReadyBaseVisualState = true;
    }

    private void ResetAbilityReadyIndicatorVfx()
    {
        if (dashEnhancerReadyIndicator == null || !hasDashEnhancerReadyBaseVisualState)
            return;

        dashEnhancerReadyIndicator.transform.localScale = dashEnhancerReadyBaseScale;

        if (dashEnhancerReadyIndicatorImage != null)
            dashEnhancerReadyIndicatorImage.color = dashEnhancerReadyBaseColor;
    }

    private void UpdateAbilityReadyIndicatorVfx(bool isDashEnhancerReady)
    {
        if (dashEnhancerReadyIndicator == null)
            return;

        if (!hasDashEnhancerReadyBaseVisualState)
            CacheAbilityReadyIndicatorBaseState();

        if (!enableAbilityReadyCircleVfx || !isDashEnhancerReady)
        {
            ResetAbilityReadyIndicatorVfx();
            return;
        }

        float time = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * Mathf.Max(0.1f, abilityReadyGlowPulseSpeed));
        float flickerNoise = Mathf.PerlinNoise(time * Mathf.Max(0.1f, abilityReadyFlickerSpeed), 0.73f);
        float flicker = Mathf.Lerp(1f - Mathf.Clamp01(abilityReadyFlickerAmount), 1f, flickerNoise);
        float glowAmount = Mathf.Clamp01(pulse * flicker);

        if (dashEnhancerReadyIndicatorImage != null)
            dashEnhancerReadyIndicatorImage.color = Color.Lerp(dashEnhancerReadyBaseColor, abilityReadyGlowColor, glowAmount);

        float scaleMultiplier = 1f + Mathf.Max(0f, abilityReadyScalePulseAmount) * glowAmount;
        dashEnhancerReadyIndicator.transform.localScale = dashEnhancerReadyBaseScale * scaleMultiplier;
    }

    private PlayerMovement GetPlayer()
    {
        if (player == null)
            player = GetComponent<PlayerMovement>();


        return player;
    }

    public void StopAndCleanup()
    {
        if (superCoroutine != null)
        {
            StopCoroutine(superCoroutine);
            superCoroutine = null;
        }

        if (dashEnhancerCoroutine != null)
        {
            StopCoroutine(dashEnhancerCoroutine);
            dashEnhancerCoroutine = null;
        }

        PlayerMovement owner = GetPlayer();
        PlayerAudio playerAudio = owner != null ? owner.GetComponent<PlayerAudio>() : null;

        if (owner != null)
        {
            owner.isSuperActive = false;
            owner.SetCanDash(true);

            if (owner.PhysicsCollider != null)
                owner.PhysicsCollider.isTrigger = false;

            if (owner.playerHealth != null)
                owner.playerHealth.RemoveInvincibility();
        }

            if (playerAudio != null)
            {
                playerAudio.SetSuperSfxActive(false);
                playerAudio.SetAbilitySfxActive(false);
                playerAudio.SetChainSfxActive(false);
            }

        RestoreSuperEnemyCollisionIgnore(owner);
        RestoreTemporalTrailRendererTiming(owner);
        DestroyChainLightningTrail(activeChainLightningTrail);
        activeChainLightningTrail = null;

        isProjectileModeActive = false;
        isChainAssistActive = false;
        nextGhostProjectileShotTime = 0f;
        superEndUnscaledTime = 0f;
        projectileModeEndUnscaledTime = 0f;
        wasSuperReady = false;
        wasDashEnhancerReady = false;
        hasReadyStateInitialized = false;

        ClearRecordedSuperData();
        RestoreSuperTimeScale();
        UpdateCooldownUI();
    }

    public void HandleAbilitySlotTriggers()
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null)
            return;

        PlayerAudio playerAudio = owner.GetComponent<PlayerAudio>();

        KeyCode superKey = GameKeybinds.Super;
        KeyCode abilityKey = GameKeybinds.Ability;

        if (WasKeyPressedThisFrame(superKey))
        {
            bool isSuperReady = !owner.isSuperActive && owner.currentSuperMeter >= owner.maxSuperMeter;

            if (isSuperReady)
            {
                if (superCoroutine != null)
                    StopCoroutine(superCoroutine);

                superCoroutine = StartCoroutine(ActivateSuper());
            }
            else if (!owner.isSuperActive && playerAudio != null)
            {
                playerAudio.PlaySuperErrorSfx();
            }
        }

        if (WasKeyPressedThisFrame(abilityKey))
        {
            bool isDashEnhancerActive = isProjectileModeActive || isChainAssistActive;
            bool isDashEnhancerOnCooldown = Time.unscaledTime < nextDashEnhancerReadyTime;

            if (isDashEnhancerOnCooldown)
            {
                if (playerAudio != null)
                    playerAudio.PlayAbilityErrorSfx();
            }
            else if (!owner.isSuperActive && !isDashEnhancerActive)
            {
                TryActivateDashEnhancer();
            }
        }
    }

    private bool WasKeyPressedThisFrame(KeyCode key)
    {
        // Try new Input System first
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            KeyControl keyControl = null;
            switch (key)
            {
                case KeyCode.F: keyControl = keyboard.fKey; break;
                case KeyCode.LeftShift: keyControl = keyboard.leftShiftKey; break;
                case KeyCode.RightShift: keyControl = keyboard.rightShiftKey; break;
                case KeyCode.E: keyControl = keyboard.eKey; break;
                case KeyCode.Q: keyControl = keyboard.qKey; break;
                case KeyCode.Space: keyControl = keyboard.spaceKey; break;
                case KeyCode.Tab: keyControl = keyboard.tabKey; break;
            }

            if (keyControl != null)
                return keyControl.wasPressedThisFrame;
        }

        // Fallback to old Input Manager
        try { return Input.GetKeyDown(key); }
        catch (System.InvalidOperationException) { return false; }
    }

    public bool TryHandleDashInputOverride()
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null || owner.dashAction == null)
            return false;

        // Ghost mode now uses PlayerMovement's charge/release flow.
        return false;
    }

    public bool TryHandleChargedGhostDashRelease(float chargeMultiplier, out bool firedGhostShot)
    {
        firedGhostShot = false;

        if (!isProjectileModeActive)
            return false;

        firedGhostShot = FireGhostDashProjectile(Mathf.Max(1f, chargeMultiplier));
        return true;
    }

    public void RecordDashForActiveSuper(Vector2 start, Vector2 end, float chargeMultiplier)
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null || !owner.isSuperActive)
            return;

        if ((end - start).sqrMagnitude < 0.01f)
            return;

        if (equippedSuperEnhancer == SuperEnhancerSlot.TemporalTrailDetonation)
        {
            float clampedChargeMultiplier = Mathf.Max(1f, chargeMultiplier);
            recordedSuperDashSegments.Add(new DashPathSegment
            {
                start = start,
                end = end,
                chargeMultiplier = clampedChargeMultiplier
            });
            CreateTemporalTrailPreviewSegment(start, end, clampedChargeMultiplier);
            return;
        }

        if (equippedSuperEnhancer == SuperEnhancerSlot.TemporalCloneAssault)
            CreateSuperCloneRecord(start, end);
    }

    private bool IsTemporalSuperEquipped()
    {
        return equippedSuperEnhancer == SuperEnhancerSlot.TemporalTrailDetonation
            || equippedSuperEnhancer == SuperEnhancerSlot.TemporalCloneAssault;
    }

    private void ApplySuperTimeScale()
    {
        if (isSuperTimeApplied)
            return;

        preSuperTimeScale = Mathf.Max(0.0001f, Time.timeScale);
        preSuperFixedDeltaTime = Mathf.Max(0.0001f, Time.fixedDeltaTime);

        float targetScale = Mathf.Clamp(superTimeScale, 0.05f, 1f);
        Time.timeScale = targetScale;
        Time.fixedDeltaTime = preSuperFixedDeltaTime * (targetScale / preSuperTimeScale);

        playerSuperSpeedCompensation = preSuperTimeScale / Mathf.Max(0.0001f, targetScale);
        isSuperTimeApplied = true;
    }

    private void RestoreSuperTimeScale()
    {
        if (!isSuperTimeApplied)
            return;

        Time.timeScale = preSuperTimeScale;
        Time.fixedDeltaTime = preSuperFixedDeltaTime;
        playerSuperSpeedCompensation = 1f;
        isSuperTimeApplied = false;
    }

    private void ApplyTemporalTrailRendererTiming(PlayerMovement owner)
    {
        if (isTemporalTrailTimingAdjusted)
            return;

        if (equippedSuperEnhancer != SuperEnhancerSlot.TemporalTrailDetonation)
            return;

        if (owner == null || owner.dashTrail == null)
            return;

        preTemporalTrailTime = Mathf.Max(0f, owner.dashTrail.time);
        float targetScale = Mathf.Clamp(superTimeScale, 0.05f, 1f);
        owner.dashTrail.time = preTemporalTrailTime * targetScale;
        isTemporalTrailTimingAdjusted = true;
    }

    private void RestoreTemporalTrailRendererTiming(PlayerMovement owner)
    {
        if (!isTemporalTrailTimingAdjusted)
            return;

        if (owner != null && owner.dashTrail != null)
            owner.dashTrail.time = preTemporalTrailTime;

        preTemporalTrailTime = 0f;
        isTemporalTrailTimingAdjusted = false;
    }

    private void ApplySuperEnemyCollisionIgnore(PlayerMovement owner)
    {
        if (owner == null || owner.PhysicsCollider == null)
            return;

        Collider2D playerCollider = owner.PhysicsCollider;
        EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            Collider2D[] enemyColliders = enemy.GetComponentsInChildren<Collider2D>(true);
            for (int c = 0; c < enemyColliders.Length; c++)
            {
                Collider2D enemyCollider = enemyColliders[c];
                if (enemyCollider == null || enemyCollider == playerCollider)
                    continue;

                if (!superIgnoredEnemyColliders.ContainsKey(enemyCollider))
                {
                    bool wasIgnored = Physics2D.GetIgnoreCollision(playerCollider, enemyCollider);
                    superIgnoredEnemyColliders.Add(enemyCollider, wasIgnored);
                }

                Physics2D.IgnoreCollision(playerCollider, enemyCollider, true);
            }
        }
    }

    private void RestoreSuperEnemyCollisionIgnore(PlayerMovement owner)
    {
        if (superIgnoredEnemyColliders.Count == 0)
            return;

        Collider2D playerCollider = owner != null ? owner.PhysicsCollider : null;
        if (playerCollider != null)
        {
            foreach (KeyValuePair<Collider2D, bool> pair in superIgnoredEnemyColliders)
            {
                if (pair.Key != null)
                    Physics2D.IgnoreCollision(playerCollider, pair.Key, pair.Value);
            }
        }

        superIgnoredEnemyColliders.Clear();
    }

    private IEnumerator ActivateSuper()
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null)
        {
            superCoroutine = null;
            yield break;
        }

        CancelActiveDashEnhancer(owner);

        PlayerAudio playerAudio = owner.GetComponent<PlayerAudio>();
        if (playerAudio != null)
        {
            playerAudio.PlaySuperActivateSfx();
            playerAudio.SetSuperSfxActive(true);
        }

        owner.isSuperActive = true;
        owner.currentSuperMeter = 0f;
        owner.currentStamina = Mathf.Max(0f, owner.maxStamina);
        superEndUnscaledTime = Time.unscaledTime + Mathf.Max(0.01f, owner.superDuration);
        ClearRecordedSuperData();

        bool isTemporalSuper = IsTemporalSuperEquipped();
        if (isTemporalSuper)
        {
            ApplySuperTimeScale();
            ApplyTemporalTrailRendererTiming(owner);
            ApplySuperEnemyCollisionIgnore(owner);
        }

        if (owner.superOverlay != null)
            StartCoroutine(FadeTint(owner.tintMaxAlpha));

        float elapsed = 0f;
        float collisionRefreshTimer = SuperCollisionRefreshSeconds;
        while (elapsed < owner.superDuration)
        {
            if (isTemporalSuper)
            {
                collisionRefreshTimer -= Time.unscaledDeltaTime;
                if (collisionRefreshTimer <= 0f)
                {
                    ApplySuperEnemyCollisionIgnore(owner);
                    collisionRefreshTimer = SuperCollisionRefreshSeconds;
                }
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (equippedSuperEnhancer == SuperEnhancerSlot.TemporalTrailDetonation)
        {
            ResolveTrailDetonationAtSuperEnd();
        }
        else if (equippedSuperEnhancer == SuperEnhancerSlot.TemporalCloneAssault)
        {
            yield return StartCoroutine(ResolveCloneAssaultAtSuperEnd());
        }

        ClearRecordedSuperData();

        if (isTemporalSuper)
        {
            RestoreSuperEnemyCollisionIgnore(owner);
            RestoreTemporalTrailRendererTiming(owner);
        }

        owner.isSuperActive = false;
        superCoroutine = null;
        superEndUnscaledTime = 0f;

        if (isTemporalSuper)
            RestoreSuperTimeScale();

        if (playerAudio != null)
        {
            playerAudio.SetSuperSfxActive(false);
            playerAudio.PlaySuperEndSfx();
        }

        if (owner.superOverlay != null)
            StartCoroutine(FadeTint(0f));
    }

    private void CreateSuperCloneRecord(Vector2 spawnPosition, Vector2 dashTarget)
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null || owner.playerSpriteRenderer == null)
            return;

        GameObject cloneObject = new GameObject("SuperClone");
        cloneObject.transform.position = spawnPosition;
        cloneObject.transform.rotation = transform.rotation;
        cloneObject.transform.localScale = owner.transform.localScale;

        SpriteRenderer cloneRenderer = cloneObject.AddComponent<SpriteRenderer>();
        cloneRenderer.sprite = owner.playerSpriteRenderer.sprite;
        cloneRenderer.sortingLayerID = owner.playerSpriteRenderer.sortingLayerID;
        cloneRenderer.sortingOrder = owner.playerSpriteRenderer.sortingOrder - 1;

        Color color = owner.playerSpriteRenderer.color;
        color.a = cloneAlpha;
        cloneRenderer.color = color;

        recordedSuperCloneDashes.Add(new CloneDashRecord
        {
            cloneObject = cloneObject,
            dashTarget = dashTarget,
            baseLocalScale = cloneObject.transform.localScale
        });
    }

    private void ResolveTrailDetonationAtSuperEnd()
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null)
            return;

        PlayerAudio playerAudio = owner.GetComponent<PlayerAudio>();
        if (playerAudio != null && recordedSuperDashSegments.Count > 0)
            playerAudio.PlayTrailDetonationSfx();

        Dictionary<int, float> totalDamageByEnemyId = new Dictionary<int, float>();
        Dictionary<int, Vector3> popupPositionsByEnemyId = new Dictionary<int, Vector3>();

        for (int i = 0; i < recordedSuperDashSegments.Count; i++)
        {
            DashPathSegment segment = recordedSuperDashSegments[i];
            float segmentDamage = trailDetonationDamage * Mathf.Max(1f, segment.chargeMultiplier);
            owner.DamageEnemiesAlongPath(
                segment.start,
                segment.end,
                trailDetonationRadius,
                segmentDamage,
                false,
                trailDetonationSamplesPerUnit,
                0f,
                null,
                false,
                false,
                totalDamageByEnemyId,
                popupPositionsByEnemyId);
        }

        foreach (KeyValuePair<int, float> pair in totalDamageByEnemyId)
        {
            if (pair.Value <= 0f)
                continue;

            Vector3 popupPosition;
            if (!popupPositionsByEnemyId.TryGetValue(pair.Key, out popupPosition))
                popupPosition = owner.transform.position + new Vector3(0f, 1f, 0f);

            owner.SpawnDamagePopup(popupPosition, pair.Value);
        }
    }

    private void CreateTemporalTrailPreviewSegment(Vector2 start, Vector2 end, float chargeMultiplier)
    {
        if ((end - start).sqrMagnitude < 0.0001f)
            return;

        PlayerMovement owner = GetPlayer();
        float normalizedCharge = 0f;
        if (owner != null && owner.maxChargeDamageMultiplier > 1.0001f)
            normalizedCharge = Mathf.Clamp01((Mathf.Max(1f, chargeMultiplier) - 1f) / (owner.maxChargeDamageMultiplier - 1f));

        GameObject previewObject = new GameObject("TemporalTrailPreview");
        LineRenderer previewLine = previewObject.AddComponent<LineRenderer>();
        previewLine.useWorldSpace = true;
        previewLine.alignment = LineAlignment.View;
        previewLine.textureMode = LineTextureMode.Stretch;
        previewLine.numCapVertices = 2;
        previewLine.numCornerVertices = 2;
        previewLine.positionCount = 2;
        previewLine.SetPosition(0, start);
        previewLine.SetPosition(1, end);

        float maxWidthMultiplier = Mathf.Max(1f, trailDetonationPreviewMaxWidthMultiplier);
        float widthMultiplier = Mathf.Lerp(1f, maxWidthMultiplier, normalizedCharge);
        float width = Mathf.Max(0.01f, trailDetonationPreviewWidth * widthMultiplier);
        previewLine.startWidth = width;
        previewLine.endWidth = width;

        Color startColor = Color.Lerp(trailDetonationPreviewColor, Color.white, normalizedCharge * trailDetonationPreviewMaxBrighten);
        startColor.a = Mathf.Clamp01(trailDetonationPreviewColor.a + (normalizedCharge * trailDetonationPreviewMaxAlphaBoost));

        Color endColor = startColor;
        endColor.a *= 0.55f;
        previewLine.startColor = startColor;
        previewLine.endColor = endColor;

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
            previewLine.material = new Material(spriteShader);

        activeTemporalTrailPreviews.Add(previewLine);
    }

    private void DestroyTemporalTrailPreviews()
    {
        for (int i = 0; i < activeTemporalTrailPreviews.Count; i++)
        {
            LineRenderer preview = activeTemporalTrailPreviews[i];
            if (preview == null)
                continue;

            if (preview.material != null)
                Destroy(preview.material);

            Destroy(preview.gameObject);
        }

        activeTemporalTrailPreviews.Clear();
    }

    private IEnumerator ResolveCloneAssaultAtSuperEnd()
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null || recordedSuperCloneDashes.Count == 0)
            yield break;

        PlayerAudio playerAudio = owner.GetComponent<PlayerAudio>();
        if (playerAudio != null)
            playerAudio.PlayCloneSummonSfx();

        Vector2 recallTarget = owner.rb != null ? owner.rb.position : (Vector2)owner.transform.position;
        int comboCount = Mathf.Max(0, owner.CurrentComboCount);
        float comboScale = 1f + (comboCount * Mathf.Max(0f, cloneComboBonusPerHit));
        float damageMultiplier = Mathf.Clamp(comboScale, 1f, Mathf.Max(1f, cloneMaxDamageMultiplier));
        float radiusMultiplier = Mathf.Clamp(comboScale, 1f, Mathf.Max(1f, cloneMaxRadiusMultiplier));
        float visualScaleMultiplier = Mathf.Clamp(comboScale, 1f, Mathf.Max(1f, cloneMaxVisualScaleMultiplier));

        for (int i = 0; i < recordedSuperCloneDashes.Count; i++)
        {
            CloneDashRecord cloneRecord = recordedSuperCloneDashes[i];
            if (cloneRecord == null || cloneRecord.cloneObject == null)
                continue;

            cloneRecord.dashTarget = recallTarget;
            cloneRecord.damageMultiplier = damageMultiplier;
            cloneRecord.radiusMultiplier = radiusMultiplier;
            cloneRecord.visualScaleMultiplier = visualScaleMultiplier;

            StartCoroutine(ExecuteSingleCloneDash(cloneRecord));
        }

        float waitDuration = Mathf.Max(0.01f, cloneDashDuration) + 0.05f;
        yield return new WaitForSecondsRealtime(waitDuration);
    }

    private IEnumerator ExecuteSingleCloneDash(CloneDashRecord cloneRecord)
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null || cloneRecord == null || cloneRecord.cloneObject == null)
            yield break;

        PlayerAudio playerAudio = owner.GetComponent<PlayerAudio>();
        if (playerAudio != null)
            playerAudio.PlayCloneDashSfx();

        Transform cloneTransform = cloneRecord.cloneObject.transform;
        Vector2 start = cloneTransform.position;
        Vector2 end = cloneRecord.dashTarget;
        float duration = Mathf.Max(0.01f, cloneDashDuration);
        float elapsed = 0f;
        float safeVisualScale = Mathf.Max(0.1f, cloneRecord.visualScaleMultiplier);
        cloneTransform.localScale = cloneRecord.baseLocalScale * safeVisualScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cloneTransform.position = Vector2.Lerp(start, end, t);
            yield return null;
        }

        cloneTransform.position = end;
        float scaledRadius = cloneDashRadius * Mathf.Max(0.1f, cloneRecord.radiusMultiplier);
        float scaledDamage = cloneDashDamage * Mathf.Max(0.1f, cloneRecord.damageMultiplier);
        owner.DamageEnemiesAlongPath(start, end, scaledRadius, scaledDamage, false, 3, 0f, null, false);

        if (cloneRecord.cloneObject != null)
            Destroy(cloneRecord.cloneObject);

        cloneRecord.cloneObject = null;
    }

    private void ClearRecordedSuperData()
    {
        recordedSuperDashSegments.Clear();
        DestroyTemporalTrailPreviews();

        for (int i = 0; i < recordedSuperCloneDashes.Count; i++)
        {
            CloneDashRecord cloneRecord = recordedSuperCloneDashes[i];
            if (cloneRecord != null && cloneRecord.cloneObject != null)
                Destroy(cloneRecord.cloneObject);
        }

        recordedSuperCloneDashes.Clear();
    }

    private void CancelActiveDashEnhancer(PlayerMovement owner)
    {
        bool hasActiveEnhancer = isProjectileModeActive || isChainAssistActive || dashEnhancerCoroutine != null;
        if (!hasActiveEnhancer)
            return;

        if (dashEnhancerCoroutine != null)
        {
            StopCoroutine(dashEnhancerCoroutine);
            dashEnhancerCoroutine = null;
        }

        isProjectileModeActive = false;
        isChainAssistActive = false;
        nextGhostProjectileShotTime = 0f;
        projectileModeEndUnscaledTime = 0f;

        if (owner != null)
        {
            owner.SetCanDash(true);

            if (owner.playerHealth != null)
                owner.playerHealth.RemoveInvincibility();
        }

        DestroyChainLightningTrail(activeChainLightningTrail);
        activeChainLightningTrail = null;

        PlayerAudio playerAudio = owner != null ? owner.GetComponent<PlayerAudio>() : null;
        if (playerAudio != null)
        {
            playerAudio.SetAbilitySfxActive(false);
            playerAudio.SetChainSfxActive(false);
        }
    }

    private void TryActivateDashEnhancer()
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null)
            return;

        if (Time.unscaledTime < nextDashEnhancerReadyTime)
            return;

        if (owner.IsDashing || owner.IsCharging || owner.isStunned)
            return;

        if (equippedDashEnhancer == DashEnhancerSlot.ChainAutoAssist && owner.CurrentComboCount < 1)
            return;

        nextDashEnhancerReadyTime = Time.unscaledTime + dashEnhancerCooldown;

        PlayerAudio playerAudio = owner.GetComponent<PlayerAudio>();
        if (playerAudio != null)
            playerAudio.PlayAbilityActivateSfx();

        if (dashEnhancerCoroutine != null)
            StopCoroutine(dashEnhancerCoroutine);

        if (equippedDashEnhancer == DashEnhancerSlot.ProjectileGhostDashes)
            dashEnhancerCoroutine = StartCoroutine(ActivateProjectileMode());
        else
            dashEnhancerCoroutine = StartCoroutine(ExecuteChainAutoAssist());
    }

    private IEnumerator ActivateProjectileMode()
    {
        PlayerMovement owner = GetPlayer();

        isProjectileModeActive = true;

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, projectileModeDuration);
        projectileModeEndUnscaledTime = Time.unscaledTime + duration;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        isProjectileModeActive = false;
        projectileModeEndUnscaledTime = 0f;

        dashEnhancerCoroutine = null;
    }

    private bool FireGhostDashProjectile(float chargeMultiplier)
    {
        PlayerMovement owner = GetPlayer();
        Camera activeCamera = Camera.main;
        if (owner == null || activeCamera == null)
            return false;

        if (Time.unscaledTime < nextGhostProjectileShotTime)
            return false;

        Vector2 start = owner.transform.position;
        Vector2 mouseScreenPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
        Vector2 mouseWorldPosition = activeCamera.ScreenToWorldPoint(mouseScreenPosition);
        Vector2 direction = (mouseWorldPosition - start).normalized;

        if (direction.sqrMagnitude < 0.0001f)
            return false;

        nextGhostProjectileShotTime = Time.unscaledTime + Mathf.Max(0.05f, projectileGhostFireRate);

        PlayerAudio playerAudio = owner.GetComponent<PlayerAudio>();
        if (playerAudio != null)
            playerAudio.PlayGhostProjectileFireSfx();

        float effectiveChargeMultiplier = Mathf.Max(1f, chargeMultiplier);
        Vector2 end = start + direction * (projectileGhostRange * effectiveChargeMultiplier);

        float damage = owner.baseDashDamage * projectileGhostDamageMultiplier * effectiveChargeMultiplier;
        float knockbackForce = owner.baseKnockbackForce * projectileGhostKnockbackMultiplier;

        StartCoroutine(PlayGhostProjectileVisual(start, end, direction, damage, knockbackForce));
        return true;
    }

    private void DestroyEnemyProjectilesAlongPath(
        Vector2 start,
        Vector2 end,
        float radius,
        int samplesPerUnit,
        HashSet<EnemyProjectile> destroyedProjectiles = null)
    {
        float safeRadius = Mathf.Max(0.05f, radius);
        float distance = Vector2.Distance(start, end);
        int samples = Mathf.Max(1, Mathf.CeilToInt(distance * Mathf.Max(1, samplesPerUnit)));
        HashSet<EnemyProjectile> processedProjectiles = destroyedProjectiles ?? new HashSet<EnemyProjectile>();

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector2 samplePosition = Vector2.Lerp(start, end, t);
            Collider2D[] hits = Physics2D.OverlapCircleAll(samplePosition, safeRadius);

            for (int h = 0; h < hits.Length; h++)
            {
                Collider2D hit = hits[h];
                if (hit == null)
                    continue;

                EnemyProjectile projectile = hit.GetComponent<EnemyProjectile>();
                if (projectile == null)
                    projectile = hit.GetComponentInParent<EnemyProjectile>();

                if (projectile == null)
                    continue;

                if (processedProjectiles.Add(projectile))
                    Destroy(projectile.gameObject);
            }
        }
    }

    private void DamageGhostDashEnemiesAlongPath(
        PlayerMovement owner,
        Vector2 start,
        Vector2 end,
        float radius,
        float damage,
        int samplesPerUnit,
        float knockbackForce,
        Vector2 knockbackDirection,
        HashSet<EnemyHealth> hitEnemies)
    {
        if (owner == null)
            return;

        PlayerAudio playerAudio = owner.GetComponent<PlayerAudio>();

        float safeRadius = Mathf.Max(0.05f, radius);
        float distance = Vector2.Distance(start, end);
        int samples = Mathf.Max(1, Mathf.CeilToInt(distance * Mathf.Max(1, samplesPerUnit)));

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector2 samplePosition = Vector2.Lerp(start, end, t);
            Collider2D[] hits = Physics2D.OverlapCircleAll(samplePosition, safeRadius);

            for (int h = 0; h < hits.Length; h++)
            {
                EnemyHealth enemy = ResolveEnemyHealth(hits[h]);
                if (enemy == null || enemy.IsDead)
                    continue;

                if (!hitEnemies.Add(enemy))
                    continue;

                bool wasAliveBeforeHit = !enemy.IsDead;
                enemy.TakeDamage(damage, knockbackDirection, knockbackForce);

                if (playerAudio != null)
                    playerAudio.PlayImpactSfx();

                owner.currentSuperMeter = Mathf.Min(owner.currentSuperMeter + owner.meterGainPerHit, owner.maxSuperMeter);
                owner.RegisterSingleComboHitAndStartTimer();

                if (wasAliveBeforeHit && enemy.IsDead && owner.staminaOnEnemyKill > 0f)
                    owner.currentStamina = Mathf.Min(owner.maxStamina, owner.currentStamina + owner.staminaOnEnemyKill);

                owner.SpawnDamagePopup(enemy.transform.position + new Vector3(0f, 1f, 0f), damage);
            }
        }
    }

    private EnemyHealth ResolveEnemyHealth(Collider2D collision)
    {
        if (collision == null)
            return null;

        EnemyHealth enemy = collision.GetComponent<EnemyHealth>();
        if (enemy != null)
            return enemy;

        if (collision.attachedRigidbody != null)
        {
            enemy = collision.attachedRigidbody.GetComponent<EnemyHealth>();
            if (enemy != null)
                return enemy;
        }

        return collision.GetComponentInParent<EnemyHealth>();
    }

    private IEnumerator PlayGhostProjectileVisual(
        Vector2 start,
        Vector2 end,
        Vector2 direction,
        float damage,
        float knockbackForce)
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null || owner.playerSpriteRenderer == null)
            yield break;

        GameObject ghostObject = new GameObject("GhostDashProjectile");
        ghostObject.transform.position = start;
        ghostObject.transform.rotation = owner.transform.rotation;

        SpriteRenderer ghostRenderer = ghostObject.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = owner.playerSpriteRenderer.sprite;
        ghostRenderer.sortingLayerID = owner.playerSpriteRenderer.sortingLayerID;
        ghostRenderer.sortingOrder = owner.playerSpriteRenderer.sortingOrder - 1;
        ghostRenderer.color = projectileGhostColor;

        float distance = Vector2.Distance(start, end);
        float duration = distance / Mathf.Max(0.01f, projectileGhostVisualSpeed);
        float elapsed = 0f;
        Vector2 previousPosition = start;
        HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();
        HashSet<EnemyProjectile> destroyedProjectiles = new HashSet<EnemyProjectile>();

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector2 currentPosition = Vector2.Lerp(start, end, t);

            DamageGhostDashEnemiesAlongPath(
                owner,
                previousPosition,
                currentPosition,
                projectileGhostRadius,
                damage,
                3,
                knockbackForce,
                direction,
                hitEnemies);

            DestroyEnemyProjectilesAlongPath(
                previousPosition,
                currentPosition,
                projectileGhostRadius,
                3,
                destroyedProjectiles);

            ghostObject.transform.position = currentPosition;
            previousPosition = currentPosition;
            yield return null;
        }

        if ((previousPosition - end).sqrMagnitude > 0.0001f)
        {
            DamageGhostDashEnemiesAlongPath(
                owner,
                previousPosition,
                end,
                projectileGhostRadius,
                damage,
                3,
                knockbackForce,
                direction,
                hitEnemies);

            DestroyEnemyProjectilesAlongPath(
                previousPosition,
                end,
                projectileGhostRadius,
                3,
                destroyedProjectiles);
        }

        ghostObject.transform.position = end;

        if (ghostObject != null)
            Destroy(ghostObject);
    }

    private IEnumerator ExecuteChainAutoAssist()
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null)
        {
            dashEnhancerCoroutine = null;
            yield break;
        }

        PlayerAudio playerAudio = owner.GetComponent<PlayerAudio>();
        if (playerAudio != null)
        {
            playerAudio.PlayChainStartSfx();
            playerAudio.SetChainSfxActive(true);
        }

        isChainAssistActive = true;
        isProjectileModeActive = false;
        owner.SetCanDash(false);

        if (owner.aimIndicator != null)
            owner.aimIndicator.SetActive(false);

        if (owner.playerHealth != null)
            owner.playerHealth.BecomeInvincible();

        int chainHitCount = GetChainHitCount();
        bool landedAbilityHit = false;
        HashSet<EnemyHealth> hitChainEnemies = new HashSet<EnemyHealth>();
        EnemyHealth previousTarget = null;
        activeChainLightningTrail = CreateChainLightningTrail();
        float currentChainDamageMultiplier = chainDashDamageMultiplier + (owner.CurrentComboCount * chainDamageBonusPerComboHit);
        currentChainDamageMultiplier = Mathf.Max(chainMinimumDamageMultiplier, currentChainDamageMultiplier);

        for (int i = 0; i < chainHitCount; i++)
        {
            HashSet<EnemyHealth> firstPassExclusions = new HashSet<EnemyHealth>(hitChainEnemies);
            if (previousTarget != null)
                firstPassExclusions.Add(previousTarget);

            EnemyHealth target = owner.FindClosestEnemy(chainSearchRadius, firstPassExclusions, true);
            if (target == null)
            {
                HashSet<EnemyHealth> fallbackExclusions = null;
                if (previousTarget != null)
                {
                    fallbackExclusions = new HashSet<EnemyHealth>();
                    fallbackExclusions.Add(previousTarget);
                }

                target = owner.FindClosestEnemy(chainSearchRadius, fallbackExclusions, true);
            }

            if (target == null)
                break;

            Vector2 segmentStart = owner.rb != null ? owner.rb.position : (Vector2)owner.transform.position;

            yield return StartCoroutine(PerformChainDashToTarget(target));

            Vector2 segmentEnd = owner.rb != null ? owner.rb.position : (Vector2)owner.transform.position;
            AddChainLightningSegment(activeChainLightningTrail, segmentStart, segmentEnd);

            if (target != null && !target.IsDead)
            {
                float damage = owner.baseDashDamage * currentChainDamageMultiplier;
                float knockback = owner.baseKnockbackForce;
                if (owner.ApplyAbilityHitWithoutCombo(target, damage, knockback))
                {
                    landedAbilityHit = true;
                    hitChainEnemies.Add(target);
                    ApplyChainStun(target);

                    if (playerAudio != null)
                        playerAudio.PlayChainLightningHitSfx();

                    float dissipationFactor = Mathf.Clamp01(1f - chainDamageDissipationPerHit);
                    currentChainDamageMultiplier = Mathf.Max(chainMinimumDamageMultiplier, currentChainDamageMultiplier * dissipationFactor);
                }
            }

            previousTarget = target;

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, chainDashGapSeconds));
        }

        owner.SetCanDash(true);
        isChainAssistActive = false;

        if (landedAbilityHit)
            owner.RegisterSingleComboHitAndStartTimer();

        if (owner.playerHealth != null)
            owner.playerHealth.RemoveInvincibility();

        DestroyChainLightningTrail(activeChainLightningTrail);
        activeChainLightningTrail = null;

        if (playerAudio != null)
            playerAudio.SetChainSfxActive(false);

        dashEnhancerCoroutine = null;
    }

    private LineRenderer CreateChainLightningTrail()
    {
        GameObject trailObject = new GameObject("ChainLightningTrail");
        LineRenderer line = trailObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.positionCount = 0;
        line.startWidth = Mathf.Max(0.01f, chainLightningTrailWidth);
        line.endWidth = Mathf.Max(0.01f, chainLightningTrailWidth * 0.8f);

        Color endColor = chainLightningTrailColor;
        endColor.a *= 0.35f;
        line.startColor = chainLightningTrailColor;
        line.endColor = endColor;

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
            line.material = new Material(spriteShader);

        return line;
    }

    private void AddChainLightningSegment(LineRenderer line, Vector2 start, Vector2 end)
    {
        if (line == null)
            return;

        int count = line.positionCount;
        if (count == 0)
        {
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            return;
        }

        Vector3 last = line.GetPosition(count - 1);
        if ((last - (Vector3)start).sqrMagnitude > 0.0001f)
        {
            line.positionCount = count + 2;
            line.SetPosition(count, start);
            line.SetPosition(count + 1, end);
        }
        else
        {
            line.positionCount = count + 1;
            line.SetPosition(count, end);
        }
    }

    private void DestroyChainLightningTrail(LineRenderer line)
    {
        if (line == null)
            return;

        if (line.material != null)
            Destroy(line.material);

        Destroy(line.gameObject);
    }

    private void ApplyChainStun(EnemyHealth target)
    {
        if (target == null || target.IsDead)
            return;

        EnemyBase enemyBase = target.GetComponent<EnemyBase>();
        if (enemyBase == null)
            enemyBase = target.GetComponentInParent<EnemyBase>();

        if (enemyBase != null)
            enemyBase.Stun(chainStunDuration);
    }

    private void UpdateCooldownUI()
    {
        if (dashEnhancerCooldownFillImage == null || dashEnhancerDurationFill == null || dashEnhancerReadyIndicator == null)
            AutoBindUiReferences();

        PlayerMovement owner = GetPlayer();
        if (owner == null)
            return;

        float safeSuperMeter = Mathf.Max(0.01f, owner.maxSuperMeter);
        float superCharge = Mathf.Clamp01(owner.currentSuperMeter / safeSuperMeter);
        if (superChargeFillImage != null)
            superChargeFillImage.fillAmount = superCharge;

        float superActiveFill = 0f;
        if (owner.isSuperActive)
        {
            float superDuration = Mathf.Max(0.01f, owner.superDuration);
            float superRemaining = Mathf.Max(0f, superEndUnscaledTime - Time.unscaledTime);
            superActiveFill = Mathf.Clamp01(superRemaining / superDuration);
        }

        if (superActiveDurationFillImage != null)
            superActiveDurationFillImage.fillAmount = superActiveFill;

        bool isSuperReady = !owner.isSuperActive && owner.currentSuperMeter >= owner.maxSuperMeter;

        if (superReadyIndicator != null)
            superReadyIndicator.SetActive(isSuperReady);

        float safeCooldown = Mathf.Max(0.01f, dashEnhancerCooldown);
        float cooldownRemaining = Mathf.Max(0f, nextDashEnhancerReadyTime - Time.unscaledTime);
        float cooldownFill = Mathf.Clamp01(cooldownRemaining / safeCooldown);
        if (dashEnhancerCooldownFillImage != null)
            dashEnhancerCooldownFillImage.fillAmount = cooldownFill;

        float activeFill = 0f;
        if (isProjectileModeActive)
        {
            float modeDuration = Mathf.Max(0.1f, projectileModeDuration);
            float modeRemaining = Mathf.Max(0f, projectileModeEndUnscaledTime - Time.unscaledTime);
            activeFill = Mathf.Clamp01(modeRemaining / modeDuration);
        }
        else if (isChainAssistActive)
        {
            activeFill = 1f;
        }

        if (dashEnhancerDurationFill != null)
            dashEnhancerDurationFill.fillAmount = activeFill;

        // Keep older scene references functional if they still use the legacy field.
        if (dashEnhancerActiveFillImage != null)
            dashEnhancerActiveFillImage.fillAmount = activeFill;

        bool isDashEnhancerReady = cooldownRemaining <= 0f && !isProjectileModeActive && !isChainAssistActive;

        if (dashEnhancerReadyIndicator != null)
            dashEnhancerReadyIndicator.SetActive(isDashEnhancerReady);

        UpdateAbilityReadyIndicatorVfx(isDashEnhancerReady);

        if (!hasReadyStateInitialized)
        {
            wasSuperReady = isSuperReady;
            wasDashEnhancerReady = isDashEnhancerReady;
            hasReadyStateInitialized = true;
            return;
        }

        if (wasSuperReady == isSuperReady && wasDashEnhancerReady == isDashEnhancerReady)
            return;

        PlayerAudio playerAudio = owner.GetComponent<PlayerAudio>();

        if (!wasSuperReady && isSuperReady && playerAudio != null)
            playerAudio.PlaySuperReadySfx();

        if (!wasDashEnhancerReady && isDashEnhancerReady && playerAudio != null)
            playerAudio.PlayAbilityReadySfx();

        wasSuperReady = isSuperReady;
        wasDashEnhancerReady = isDashEnhancerReady;
    }

    private IEnumerator PerformChainDashToTarget(EnemyHealth target)
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null || target == null || target.IsDead)
            yield break;

        PlayerAudio playerAudio = owner.GetComponent<PlayerAudio>();
        if (playerAudio != null)
            playerAudio.PlayChainDashSfx();

        Vector2 start = owner.rb.position;
        Vector2 targetPosition = target.transform.position;
        Vector2 toTarget = targetPosition - start;

        if (toTarget.sqrMagnitude < 0.001f)
            yield break;

        Vector2 destination = targetPosition - toTarget.normalized * chainArrivalOffset;
        float duration = Mathf.Max(0.01f, chainDashDuration);
        float elapsed = 0f;

        if (owner.PhysicsCollider != null)
            owner.PhysicsCollider.isTrigger = true;

        while (elapsed < duration)
        {
            elapsed += Time.fixedUnscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            owner.rb.MovePosition(Vector2.Lerp(start, destination, t));
            yield return new WaitForFixedUpdate();
        }

        owner.rb.MovePosition(destination);

        owner.ResolvePostDashPenetration(start - destination);

        if (owner.PhysicsCollider != null)
            owner.PhysicsCollider.isTrigger = false;
    }

    private int GetChainHitCount()
    {
        PlayerMovement owner = GetPlayer();
        int currentCombo = owner != null ? owner.CurrentComboCount : 0;

        int comboBased = Mathf.Max(minimumChainHits, currentCombo);
        return Mathf.Clamp(comboBased + chainHitBonus, minimumChainHits, maxChainHits);
    }

    private IEnumerator FadeTint(float targetAlpha)
    {
        PlayerMovement owner = GetPlayer();
        if (owner == null || owner.superOverlay == null)
            yield break;

        Color currentColor = owner.superOverlay.color;

        while (!Mathf.Approximately(currentColor.a, targetAlpha))
        {
            currentColor.a = Mathf.MoveTowards(currentColor.a, targetAlpha, owner.tintFadeSpeed * Time.deltaTime);
            owner.superOverlay.color = currentColor;
            yield return null;
        }
    }
}
