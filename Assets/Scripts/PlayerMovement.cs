using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

[RequireComponent(typeof(PlayerAbilitySlots))]
[RequireComponent(typeof(PlayerAudio))]
public class PlayerMovement : MonoBehaviour
{
    private enum PlayerVisualState
    {
        Idle,
        Moving,
        Dashing,
        Dead
    }

    [Header("Dash Mode")]
    public static bool useWASDDash = false;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float dashSpeed = 18f;
    public float dashDuration = 0.25f;
    public float dashCooldown = 1f;
    [Min(0.1f)] public float dashRange = 4.5f;

    [Header("Dash Input Buffer")]
    [Min(0f)] public float dashInputBufferTime = 0.12f;

    [Header("Player Animation")]
    public SpriteRenderer playerSpriteRenderer;
    [Min(0)] public int playerSortingOrder = 20;
    public Sprite idleFrame1;
    public Sprite idleFrame2;
    public Sprite moveFrame1;
    public Sprite moveFrame2;
    public Sprite dashFrame1;
    public Sprite dashFrame2;
    public Sprite deathFrame1;
    public Sprite deathFrame2;
    public Sprite deathFrame3;
    public Sprite deathFrame4;
    public float animationSwapSeconds = 0.12f;
    public float deathFrameDuration = 0.12f;

    [Header("References")]
    public Rigidbody2D rb;
    public InputAction moveAction;
    public InputAction dashAction;
    public PlayerHealth playerHealth;

    [Header("Cursor")]
    public bool hideCursorWhilePlaying = true;

    [Header("Visual Effects")]
    public TrailRenderer dashTrail;
    public Image chargeFillImage;
    public GameObject aimIndicator;
    public GameObject damagePopupPrefab;

    [Header("Charge Fill On Player")]
    public bool usePlayerChargeFill = true;
    public Color playerChargeFillColor = new Color(1f, 0.92f, 0.2f, 0.85f);
    public Sprite chargeMaskSprite;
    public int chargeFillSortingOffset = 1;
    public SpriteMask chargeFillMask;
    public SpriteRenderer chargeFillOverlayRenderer;

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
    [FormerlySerializedAs("maxChargeMultiplier")]
    [Min(1f)] public float maxChargeRangeMultiplier = 2.5f;
    [Min(1f)] public float maxChargeDamageMultiplier = 2.5f;
    public float maxChargeStaminaCost = 50f;
    public float chargeDelay = 0.1f;
    [Range(0f, 1f)] public float chargeMoveSpeedMultiplier = 0.45f;
    private bool isCharging = false;
    private float currentChargeTime = 0f;
    [HideInInspector] public float currentDashMultiplier = 1f;

    [Header("Combat Settings")]
    public float baseDashDamage = 1f;
    public float baseKnockbackForce = 3f;
    [Tooltip("Flat damage added for each combo hit after the first hit.")]
    public float comboDamageBonusPerHit = 0.1f;
    [Min(0f)] public float maxComboBonusDamage = 2f;
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
    public ComboCircleSpriteProgression comboCircleSpriteProgression;

    [Header("Super Meter")]
    public float maxSuperMeter = 100f;
    public float currentSuperMeter = 0f;
    public float meterGainPerHit = 5f;
    public float superDuration = 5f;
    public Image superMeterFill;

    [Header("Super Meter Full VFX")]
    public bool enableSuperMeterFullVfx = true;
    [Range(0.8f, 1f)] public float superMeterFullThreshold = 0.995f;
    public Color superMeterFullGlowColor = new Color(1f, 0.9f, 0.35f, 1f);
    [Min(0.1f)] public float superMeterGlowPulseSpeed = 8f;

    private bool isComboWindowActive = false;
    private float currentComboTimer = 0f;
    [Min(0.1f)] public float superMeterFlickerSpeed = 18f;
    [Range(0f, 1f)] public float superMeterFlickerAmount = 0.25f;
    private float currentComboWindowDuration;
    [Range(0f, 0.2f)] public float superMeterScalePulseAmount = 0.05f;

    [Header("Meter Fill Image Settings")]
    public bool matchHealthFillImageSettings = true;

    [Header("Super Visuals")]
    public Image superOverlay;
    public float tintMaxAlpha = 0.2f;
    public float tintFadeSpeed = 0.5f;

    [HideInInspector] public bool isSuperActive = false;
    private bool hitEnemyThisDash = false;
    private float currentDiscount = 1f;
    private int currentComboCount = 0;

    private Vector2 movement;
    private bool isDashing = false;
    public bool IsDashing => isDashing;
    private bool canDash = true;
    public bool IsCharging => isCharging;
    public bool isStunned = false;
    public Collider2D PhysicsCollider => physicsCollider;
    public int CurrentComboCount => currentComboCount;
    private Collider2D physicsCollider;
    private float dashBufferedUntil = -1f;
    public LayerMask dashThroughLayers;
    private readonly List<RaycastHit2D> dashCastHits = new List<RaycastHit2D>(64);
    private readonly Collider2D[] dashOverlapHits = new Collider2D[8];
    private readonly RaycastHit2D[] chainLineOfSightHits = new RaycastHit2D[32];
    private readonly Dictionary<Collider2D, bool> dashIgnoredBossColliders = new Dictionary<Collider2D, bool>();
    private readonly Dictionary<Collider2D, bool> dashColliderTriggerStates = new Dictionary<Collider2D, bool>();
    private const float DashCollisionPadding = 0.01f;
    private PlayerVisualState currentVisualState = PlayerVisualState.Idle;
    private float animationTimer;
    private bool isAnimationFrame1 = true;
    private bool isPlayingDeathAnimation = false;
    private Coroutine deathAnimationCoroutine;
    private bool isPlayerChargeFillReady = false;
    private bool hasChargeAudioStarted = false;
    private PlayerAbilitySlots abilitySlots;
    private PlayerAudio playerAudio;
    private Color superMeterBaseColor = Color.white;
    private Vector3 superMeterBaseScale = Vector3.one;
    private bool hasSuperMeterBaseVisualState;
    private bool wasSuperMeterFullLastFrame;
    private SpriteRenderer aimIndicatorRenderer;
    private Vector2 lastAimDirection = Vector2.up;
    private static Canvas damagePopupCanvas;
    private static RectTransform damagePopupCanvasRect;
    public bool IsPlayingDeathAnimation => isPlayingDeathAnimation;

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

    private void AutoBindUiReferences()
    {
        if (staminaBarFill == null)
            staminaBarFill = FindUiComponentByName<Image>("StaminaFill", "StaminaBarFill");

        if (staminaPreviewFill == null)
            staminaPreviewFill = FindUiComponentByName<Image>("StaminaPreviewFill");

        if (staminaCostText == null)
            staminaCostText = FindUiComponentByName<TextMeshProUGUI>("StaminaCostText");

        if (comboUIContainer == null)
        {
            RectTransform comboRoot = FindUiComponentByName<RectTransform>("ComboTrackerUI", "ComboUIContainer");
            if (comboRoot != null)
                comboUIContainer = comboRoot.gameObject;
        }

        if (comboTimerCircle == null)
            comboTimerCircle = FindUiComponentByName<Image>("ComboCircle", "ComboTimerCircle");

        if (comboHitText == null)
            comboHitText = FindUiComponentByName<TextMeshProUGUI>("ComboHitText");

        if (superMeterFill == null)
            superMeterFill = FindUiComponentByName<Image>("SuperFill", "SuperMeterFill");

        if (superOverlay == null)
            superOverlay = FindUiComponentByName<Image>("SuperOverlay");

        if (playerHealth != null && playerHealth.healthBarFill == null)
            playerHealth.healthBarFill = FindUiComponentByName<Image>("HealthFill", "HealthBarFill");
    }

    private void OnEnable()
    {
        try { moveAction?.Enable(); } catch (InvalidOperationException) { }
        try { dashAction?.Enable(); } catch (InvalidOperationException) { }
        ApplyGameplayCursorVisibility(true);
    }

    private void OnDisable()
    {
        RestoreDashColliderTriggerStates();
        RestoreBossDashCollisionIgnore();

        try { moveAction?.Disable(); } catch (InvalidOperationException) { }
        try { dashAction?.Disable(); } catch (InvalidOperationException) { }
        ClearBufferedDashInput();
        ApplyGameplayCursorVisibility(false);

        PlayerAbilitySlots slots = GetAbilitySlots();
        if (slots != null)
            slots.StopAndCleanup();

        if (deathAnimationCoroutine != null)
        {
            StopCoroutine(deathAnimationCoroutine);
            deathAnimationCoroutine = null;
        }

        if (chargeFillOverlayRenderer != null)
            chargeFillOverlayRenderer.enabled = false;

        ResetSuperMeterFullVfx();

        isPlayingDeathAnimation = false;

        if (playerAudio != null)
            playerAudio.StopAllLoopingSfx();
    }

    void Start()
    {
        EnsureDashTrailReady();
        if (dashTrail != null) dashTrail.emitting = false;

        if (playerAudio == null)
            playerAudio = GetComponent<PlayerAudio>();

        if (playerAudio == null)
            playerAudio = gameObject.AddComponent<PlayerAudio>();

        if (playerSpriteRenderer == null)
            playerSpriteRenderer = GetComponent<SpriteRenderer>();

        if (playerSpriteRenderer != null)
            playerSpriteRenderer.sortingOrder = Mathf.Max(playerSpriteRenderer.sortingOrder, playerSortingOrder);

        AutoBindUiReferences();

        GetAbilitySlots();

        SetupPlayerChargeFillVisual();

        animationTimer = Mathf.Max(0.01f, animationSwapSeconds);
        ApplyCurrentAnimationFrame();

        if (comboUIContainer != null) comboUIContainer.SetActive(true);
        currentComboWindowDuration = comboWindowTime;

        if (comboCircleSpriteProgression == null && comboTimerCircle != null)
            comboCircleSpriteProgression = comboTimerCircle.GetComponent<ComboCircleSpriteProgression>();

        if (comboCircleSpriteProgression != null)
            comboCircleSpriteProgression.alwaysUseFirstSprite = false;

        if (comboTimerCircle != null)
            comboTimerCircle.fillAmount = 1f;

        ApplyMeterFillImageSettings();
        CacheSuperMeterFullVfxBaseState();

        RefreshComboUIVisuals(false);

        CacheAimIndicatorVisual();
        ApplyGameplayCursorVisibility(true);

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

    private void OnApplicationFocus(bool hasFocus)
    {
        ApplyGameplayCursorVisibility(hasFocus);
    }

    private void OnApplicationPause(bool paused)
    {
        ApplyGameplayCursorVisibility(!paused);
    }

    private void ApplyGameplayCursorVisibility(bool gameplayVisible)
    {
        if (!hideCursorWhilePlaying)
            return;

        Cursor.visible = !gameplayVisible;
        Cursor.lockState = gameplayVisible ? CursorLockMode.Confined : CursorLockMode.None;
    }

    private void CacheAimIndicatorVisual()
    {
        if (aimIndicator == null)
        {
            Transform indicatorTransform = transform.Find("AimIndicator");
            if (indicatorTransform != null)
                aimIndicator = indicatorTransform.gameObject;
        }

        if (aimIndicator == null)
            return;

        if (aimIndicatorRenderer == null)
            aimIndicatorRenderer = aimIndicator.GetComponent<SpriteRenderer>();

        if (aimIndicatorRenderer != null && playerSpriteRenderer != null)
        {
            aimIndicatorRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            aimIndicatorRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + 2;
        }
    }

    private void EnsureDashTrailReady()
    {
        if (dashTrail == null)
            dashTrail = GetComponent<TrailRenderer>();

        if (playerSpriteRenderer == null)
            playerSpriteRenderer = GetComponent<SpriteRenderer>();

        if (dashTrail == null)
            return;

        if (!dashTrail.enabled)
            dashTrail.enabled = true;

        if (dashTrail.time < 0.05f)
            dashTrail.time = 0.2f;

        if (dashTrail.widthMultiplier <= 0.001f)
            dashTrail.widthMultiplier = 1f;

        if (playerSpriteRenderer != null)
        {
            dashTrail.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            dashTrail.sortingOrder = playerSpriteRenderer.sortingOrder - 1;
        }
    }

    private PlayerAbilitySlots GetAbilitySlots()
    {
        if (abilitySlots == null)
            abilitySlots = GetComponent<PlayerAbilitySlots>();

        if (abilitySlots == null)
            abilitySlots = gameObject.AddComponent<PlayerAbilitySlots>();

        return abilitySlots;
    }

    private void ApplyMeterFillImageSettings()
    {
        if (!matchHealthFillImageSettings)
            return;

        Image template = playerHealth != null ? playerHealth.healthBarFill : null;
        ApplyFilledImageSettings(staminaBarFill, template);
        ApplyFilledImageSettings(staminaPreviewFill, template);
        ApplyFilledImageSettings(superMeterFill, template);
    }

    private void ApplyFilledImageSettings(Image targetImage, Image template)
    {
        if (targetImage == null)
            return;

        if (template != null)
        {
            targetImage.type = template.type;
            targetImage.fillMethod = template.fillMethod;
            targetImage.fillOrigin = template.fillOrigin;
            targetImage.fillClockwise = template.fillClockwise;
            targetImage.preserveAspect = template.preserveAspect;
            targetImage.useSpriteMesh = template.useSpriteMesh;
            targetImage.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier;
            return;
        }

        targetImage.type = Image.Type.Filled;
        targetImage.fillMethod = Image.FillMethod.Horizontal;
        targetImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        targetImage.fillClockwise = true;
    }

    private float GetBaseDashRange()
    {
        float safeDuration = Mathf.Max(0.01f, dashDuration);
        float fallbackRange = Mathf.Max(0f, dashSpeed) * safeDuration;
        return dashRange > 0f ? dashRange : fallbackRange;
    }

    private void CacheSuperMeterFullVfxBaseState()
    {
        if (superMeterFill == null)
            return;

        superMeterBaseColor = superMeterFill.color;
        RectTransform meterRect = superMeterFill.rectTransform;
        superMeterBaseScale = meterRect != null ? meterRect.localScale : Vector3.one;
        hasSuperMeterBaseVisualState = true;
        wasSuperMeterFullLastFrame = false;
    }

    private void ResetSuperMeterFullVfx()
    {
        if (superMeterFill == null || !hasSuperMeterBaseVisualState)
            return;

        superMeterFill.color = superMeterBaseColor;

        RectTransform meterRect = superMeterFill.rectTransform;
        if (meterRect != null)
            meterRect.localScale = superMeterBaseScale;

        wasSuperMeterFullLastFrame = false;
    }

    private void UpdateSuperMeterFullVfx()
    {
        if (superMeterFill == null)
            return;

        if (!hasSuperMeterBaseVisualState)
            CacheSuperMeterFullVfxBaseState();

        if (!enableSuperMeterFullVfx)
        {
            if (wasSuperMeterFullLastFrame)
                ResetSuperMeterFullVfx();
            return;
        }

        float safeMaxSuperMeter = Mathf.Max(0.01f, maxSuperMeter);
        float threshold = Mathf.Clamp(superMeterFullThreshold, 0.8f, 1f);
        bool isSuperMeterFull = currentSuperMeter >= safeMaxSuperMeter * threshold;

        if (!isSuperMeterFull)
        {
            if (wasSuperMeterFullLastFrame)
                ResetSuperMeterFullVfx();
            return;
        }

        float time = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * Mathf.Max(0.1f, superMeterGlowPulseSpeed));
        float flickerNoise = Mathf.PerlinNoise(time * Mathf.Max(0.1f, superMeterFlickerSpeed), 0.37f);
        float flicker = Mathf.Lerp(1f - Mathf.Clamp01(superMeterFlickerAmount), 1f, flickerNoise);
        float glowAmount = Mathf.Clamp01(pulse * flicker);

        superMeterFill.color = Color.Lerp(superMeterBaseColor, superMeterFullGlowColor, glowAmount);

        RectTransform meterRect = superMeterFill.rectTransform;
        if (meterRect != null)
        {
            float scaleMultiplier = 1f + Mathf.Max(0f, superMeterScalePulseAmount) * glowAmount;
            meterRect.localScale = superMeterBaseScale * scaleMultiplier;
        }

        wasSuperMeterFullLastFrame = true;
    }

    private void UpdateWalkingSfx(bool isWalking)
    {
        if (playerAudio == null)
            playerAudio = GetComponent<PlayerAudio>();

        if (playerAudio == null)
            return;

        playerAudio.SetWalkingSfxActive(isWalking);
    }

    private void PlayImpactSfx()
    {
        if (playerAudio == null)
            playerAudio = GetComponent<PlayerAudio>();

        if (playerAudio != null)
            playerAudio.PlayImpactSfx();
    }

    void Update()
    {
        PlayerAbilitySlots slots = GetAbilitySlots();
        bool isChainAssistActive = slots.IsChainAssistActive;

        if (DashWasPressedThisFrame())
            BufferDashInput();

        // COMBO TIMER LOGIC
        if (isComboWindowActive && !isDashing && !isChainAssistActive)
        {
            float comboTimerDelta = Time.deltaTime;
            if (slots.IsTemporalSuperActive)
            {
                float superComboRate = slots.SuperComboTimerRate;
                comboTimerDelta = Time.unscaledDeltaTime * superComboRate;
            }

            currentComboTimer -= comboTimerDelta;

            if (comboTimerCircle != null)
            {
                float denominator = Mathf.Max(0.01f, currentComboWindowDuration);
                comboTimerCircle.fillAmount = currentComboTimer / denominator;
            }

            if (currentComboTimer <= 0f)
            {
                isComboWindowActive = false;
                currentDiscount = 1f;
                currentComboWindowDuration = comboWindowTime;
                ResetComboCounter();

                if (playerHealth != null) playerHealth.RemoveInvincibility();
            }
        }
        else if (comboTimerCircle != null)
        {
            // Keep the combo circle visible even when no combo timer is active.
            comboTimerCircle.fillAmount = 1f;
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
            if (playerAudio != null)
                playerAudio.SetChargeSfxActive(false);

            projectedCost = 0f;
            UpdateChargeFillVisual(0f);
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

        if (superMeterFill != null)
        {
            superMeterFill.fillAmount = currentSuperMeter / maxSuperMeter;
        }

        UpdateSuperMeterFullVfx();

        slots.HandleAbilitySlotTriggers();

        isChainAssistActive = slots.IsChainAssistActive;

        if (isStunned || isDashing || isChainAssistActive)
        {
            if (isCharging)
                CancelDashCharge();

            movement = Vector2.zero;
            UpdateWalkingSfx(false);
            UpdateSpriteAnimation();
            return;
        }

        CheckMouseAimDash();
        UpdateAimIndicator();

        if (isDashing)
        {
            movement = Vector2.zero;
            UpdateWalkingSfx(false);
            UpdateSpriteAnimation();
            return;
        }

        movement = ReadMoveInput();
        UpdateWalkingSfx(movement.sqrMagnitude > 0.0001f && !isPlayingDeathAnimation);
        UpdateSpriteAnimation();
    }

    void FixedUpdate()
    {
        PlayerAbilitySlots slots = GetAbilitySlots();
        bool isChainAssistActive = slots.IsChainAssistActive;

        if (isStunned || isDashing || isChainAssistActive)
        {
            movement = Vector2.zero;
            return;
        }

        float currentMoveSpeed = moveSpeed;
        currentMoveSpeed *= slots.GhostDashMoveSpeedMultiplier;

        if (isCharging)
            currentMoveSpeed *= chargeMoveSpeedMultiplier;

        currentMoveSpeed *= slots.PlayerTimeCompensation;

        rb.MovePosition(rb.position + movement * currentMoveSpeed * Time.fixedDeltaTime);
    }

    // DASH METHOD
    private void CheckMouseAimDash()
    {
        PlayerAbilitySlots slots = GetAbilitySlots();
        if (slots.TryHandleDashInputOverride())
            return;

        bool isGhostProjectileMode = slots.IsProjectileModeActive;
        bool isTemporalSuperStaminaMode = slots.IsTemporalSuperActive;
        bool hasInfiniteSuperStamina = slots.IsInfiniteStaminaSuperActive;
        bool hasUnlimitedCharge = hasInfiniteSuperStamina;
        float temporalSuperDashCost = isTemporalSuperStaminaMode
            ? slots.GetTemporalSuperDashStaminaCost(dashStaminaCost)
            : 0f;

        float effectiveDashCost = dashStaminaCost * currentDiscount;

        if (effectiveDashCost < minimumDashCost)
        {
            effectiveDashCost = minimumDashCost;
        }

        float ratio = effectiveDashCost / dashStaminaCost;
        float effectiveMaxCost = maxChargeStaminaCost * ratio;

        float requiredDashCost = isTemporalSuperStaminaMode ? temporalSuperDashCost : effectiveDashCost;

        bool canStartCharge = hasUnlimitedCharge || currentStamina >= requiredDashCost;
        bool wantsDashStart = HasBufferedDashInput();

        // START CHARGING
        if (wantsDashStart && (isGhostProjectileMode || canDash) && canStartCharge && !isCharging)
        {
            ClearBufferedDashInput();
            isCharging = true;
            currentChargeTime = 0f;
            hasChargeAudioStarted = false;
            movement = Vector2.zero;
            rb.linearVelocity = Vector2.zero;

            projectedCost = hasUnlimitedCharge ? 0f : requiredDashCost;
            UpdateChargeFillVisual(0f);

            if (chargeFillImage != null && !isPlayerChargeFillReady)
                chargeFillImage.fillAmount = 0f;
        }

        // BUILD CHARGE
        if (isCharging && DashIsPressed())
        {
            float maxPossibleCharge = hasUnlimitedCharge ? effectiveMaxCost : Mathf.Min(currentStamina, effectiveMaxCost);
            float possibleChargePercent = Mathf.InverseLerp(effectiveDashCost, effectiveMaxCost, maxPossibleCharge);
            float dynamicMaxChargeTime = maxChargeTime * possibleChargePercent;

            currentChargeTime += Time.deltaTime * GetAbilitySlots().PlayerTimeCompensation;
            currentChargeTime = Mathf.Clamp(currentChargeTime, 0f, dynamicMaxChargeTime);

            if (currentChargeTime >= chargeDelay)
            {
                if (!hasChargeAudioStarted && playerAudio != null)
                {
                    playerAudio.SetChargeSfxActive(true);
                    hasChargeAudioStarted = true;
                }

                float chargePercent = currentChargeTime / maxChargeTime;

                if (hasUnlimitedCharge)
                {
                    projectedCost = 0f;
                }
                else if (isTemporalSuperStaminaMode)
                {
                    projectedCost = requiredDashCost;
                }
                else
                {
                    projectedCost = Mathf.Lerp(effectiveDashCost, effectiveMaxCost, chargePercent);
                }

                if (chargeFillImage != null)
                {
                    if (!isPlayerChargeFillReady)
                        chargeFillImage.fillAmount = currentChargeTime / maxChargeTime;
                }

                UpdateChargeFillVisual(currentChargeTime / Mathf.Max(0.01f, maxChargeTime));
            }
        }

        // EXECUTE DASH
        if (isCharging && (DashWasReleasedThisFrame() || !DashIsPressed()))
        {
            isCharging = false;
            projectedCost = 0f;

            if (playerAudio != null)
            {
                playerAudio.SetChargeSfxActive(false);
            }

            hasChargeAudioStarted = false;

            UpdateChargeFillVisual(0f);

            if (chargeFillImage != null && !isPlayerChargeFillReady)
                chargeFillImage.fillAmount = 0f;

            currentDashMultiplier = 1f;
            float dashRangeMultiplier = 1f;
            float actualStaminaCost = effectiveDashCost;

            if (currentChargeTime > chargeDelay)
            {
                float chargePercent = currentChargeTime / maxChargeTime;
                dashRangeMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, maxChargeRangeMultiplier), chargePercent);
                currentDashMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, maxChargeDamageMultiplier), chargePercent);
                actualStaminaCost = Mathf.Lerp(effectiveDashCost, effectiveMaxCost, chargePercent);
            }

            if (isTemporalSuperStaminaMode)
                actualStaminaCost = requiredDashCost;

            if (slots.TryHandleChargedGhostDashRelease(currentDashMultiplier, out bool firedGhostShot))
            {
                if (firedGhostShot && !hasInfiniteSuperStamina)
                    currentStamina = Mathf.Max(0f, currentStamina - actualStaminaCost);

                return;
            }

            if (!hasInfiniteSuperStamina)
                currentStamina = Mathf.Max(0f, currentStamina - actualStaminaCost);

            Vector2 dashDirection = GetSafeDashDirection();

            StartCoroutine(PerformDash(dashDirection, dashRangeMultiplier, currentDashMultiplier));
        }
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

        if (moveAction != null)
        {
            try { input = moveAction.ReadValue<Vector2>(); }
            catch (InvalidOperationException) { }
        }

        if (input.sqrMagnitude > 0.0001f)
            return Vector2.ClampMagnitude(input, 1f);

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float x = 0f;
            float y = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;

            Vector2 keyboardInput = new Vector2(x, y);
            if (keyboardInput.sqrMagnitude > 0.0001f)
                return keyboardInput.normalized;
        }

        try
        {
            float x = 0f;
            float y = 0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;

            Vector2 legacyInput = new Vector2(x, y);
            if (legacyInput.sqrMagnitude > 0.0001f)
                return legacyInput.normalized;
        }
        catch (InvalidOperationException)
        {
            // Old Input Manager can be disabled in project settings.
        }

        return Vector2.zero;
    }

    private bool DashWasPressedThisFrame()
    {
        KeyCode dashKey = GameKeybinds.Dash;

        if (dashAction != null)
        {
            try
            {
                if (dashAction.WasPressedThisFrame())
                    return true;
            }
            catch (InvalidOperationException) { }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && TryGetKeyboardKeyControl(keyboard, dashKey, out KeyControl keyControl) && keyControl != null)
            return keyControl.wasPressedThisFrame;

        try { return Input.GetKeyDown(dashKey); }
        catch (InvalidOperationException) { return false; }
    }

    private bool DashWasReleasedThisFrame()
    {
        KeyCode dashKey = GameKeybinds.Dash;

        if (dashAction != null)
        {
            try
            {
                if (dashAction.WasReleasedThisFrame())
                    return true;
            }
            catch (InvalidOperationException) { }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && TryGetKeyboardKeyControl(keyboard, dashKey, out KeyControl keyControl) && keyControl != null)
            return keyControl.wasReleasedThisFrame;

        try { return Input.GetKeyUp(dashKey); }
        catch (InvalidOperationException) { return false; }
    }

    private bool DashIsPressed()
    {
        KeyCode dashKey = GameKeybinds.Dash;

        if (dashAction != null)
        {
            try
            {
                if (dashAction.IsPressed())
                    return true;
            }
            catch (InvalidOperationException) { }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && TryGetKeyboardKeyControl(keyboard, dashKey, out KeyControl keyControl) && keyControl != null)
            return keyControl.isPressed;

        try { return Input.GetKey(dashKey); }
        catch (InvalidOperationException) { return false; }
    }

    private bool TryGetKeyboardKeyControl(Keyboard keyboard, KeyCode keyCode, out KeyControl keyControl)
    {
        keyControl = null;
        if (keyboard == null)
            return false;

        switch (keyCode)
        {
            case KeyCode.Space: keyControl = keyboard.spaceKey; return true;
            case KeyCode.Tab: keyControl = keyboard.tabKey; return true;
            case KeyCode.Escape: keyControl = keyboard.escapeKey; return true;
            case KeyCode.LeftShift: keyControl = keyboard.leftShiftKey; return true;
            case KeyCode.RightShift: keyControl = keyboard.rightShiftKey; return true;
            case KeyCode.LeftControl: keyControl = keyboard.leftCtrlKey; return true;
            case KeyCode.RightControl: keyControl = keyboard.rightCtrlKey; return true;
            case KeyCode.E: keyControl = keyboard.eKey; return true;
            case KeyCode.F: keyControl = keyboard.fKey; return true;
            case KeyCode.M: keyControl = keyboard.mKey; return true;
            case KeyCode.Q: keyControl = keyboard.qKey; return true;
            case KeyCode.R: keyControl = keyboard.rKey; return true;
            default: return false;
        }
    }

    // --- THE ACTUAL DASH EXECUTION ---
    private IEnumerator PerformDash(Vector2 dashDirection, float rangeMultiplier, float damageMultiplier)
    {
        PlayerAbilitySlots slots = GetAbilitySlots();
        bool startedDuringTemporalSuper = slots.IsTemporalSuperActive;
        isDashing = true;
        canDash = false;
        if (playerAudio != null)
        {
            playerAudio.PlayDashSfx();
            playerAudio.SetDashSfxActive(true);
        }
        if (!startedDuringTemporalSuper)
            isComboWindowActive = false;
        hitEnemyThisDash = false;
        hitEnemiesThisDash.Clear();
        Vector2 dashStartPosition = rb.position;

        if (playerHealth != null) playerHealth.BecomeInvincible();
        EnsureDashTrailReady();
        if (dashTrail != null)
        {
            dashTrail.Clear();
            dashTrail.emitting = true;
        }
        CacheAndSetDashColliderTriggerStates();
        ApplyBossDashCollisionIgnore();

        float elapsed = 0f;
        float safeDashDuration = Mathf.Max(0.01f, dashDuration);
        float finalDashDistance = GetBaseDashRange() * Mathf.Max(0f, rangeMultiplier);
        float finalDashSpeed = (finalDashDistance / safeDashDuration) * slots.PlayerTimeCompensation;
        bool hitBlockingCollider = false;

        while (elapsed < safeDashDuration)
        {
            float stepDistance = finalDashSpeed * Time.fixedDeltaTime;
            float allowedDistance = GetAllowedDashDistance(dashDirection, stepDistance);

            if (allowedDistance > DashCollisionPadding)
            {
                rb.MovePosition(rb.position + dashDirection * allowedDistance);
            }
            else
            {
                // Hit a blocking collider; stop dash before entering geometry.
                hitBlockingCollider = true;
                break;
            }

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedUnscaledDeltaTime;
        }

        if (hitBlockingCollider)
            PlayImpactSfx();

        Vector2 dashEndPosition = rb.position;
        slots.RecordDashForActiveSuper(dashStartPosition, dashEndPosition, damageMultiplier);

        if (dashTrail != null) dashTrail.emitting = false;

        // Resolve any tiny overlap before restoring solid collider collisions.
        ResolveDashEndPenetration(dashDirection);

        RestoreDashColliderTriggerStates();
        RestoreBossDashCollisionIgnore();
        isDashing = false;

        if (playerAudio != null)
            playerAudio.SetDashSfxActive(false);

        if (startedDuringTemporalSuper)
        {
            canDash = true;

            yield break;
        }

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
            currentComboWindowDuration = comboWindowTime;
            ResetComboCounter();
            if (playerHealth != null) playerHealth.RemoveInvincibility();
            canDash = true;
        }
    }

    private float GetAllowedDashDistance(Vector2 dashDirection, float desiredDistance)
    {
        if (physicsCollider == null || desiredDistance <= 0f)
            return desiredDistance;

        Vector2 direction = dashDirection.normalized;
        if (direction.sqrMagnitude < 0.0001f)
            return desiredDistance;

        ContactFilter2D castFilter = new ContactFilter2D();
        castFilter.useLayerMask = true;
        castFilter.layerMask = Physics2D.AllLayers;
        castFilter.useTriggers = false;

        dashCastHits.Clear();
        int hitCount = physicsCollider.Cast(direction, castFilter, dashCastHits, desiredDistance + DashCollisionPadding);
        float allowedDistance = desiredDistance;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = dashCastHits[i].collider;
            if (!IsDashBlockingCollider(hitCollider))
                continue;

            float safeDistance = Mathf.Max(0f, dashCastHits[i].distance - DashCollisionPadding);
            allowedDistance = Mathf.Min(allowedDistance, safeDistance);
        }

        return allowedDistance;
    }

    private bool IsDashBlockingCollider(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return false;

        // Ignore self and enemy colliders so dash combat behavior remains intact.
        if (hitCollider.attachedRigidbody == rb)
            return false;

        // Boss should never physically block dash movement.
        if (hitCollider.GetComponentInParent<BossEnemy>() != null)
            return false;

        // Support custom enemy hitboxes that may not carry EnemyHealth directly.
        if (hitCollider.CompareTag("Enemy"))
            return false;

        int enemiesLayer = LayerMask.NameToLayer("Enemies");
        if (enemiesLayer >= 0)
        {
            if (hitCollider.gameObject.layer == enemiesLayer)
                return false;

            Rigidbody2D attachedBody = hitCollider.attachedRigidbody;
            if (attachedBody != null && attachedBody.gameObject.layer == enemiesLayer)
                return false;
        }

        if (ResolveEnemyHealth(hitCollider) != null)
            return false;

        if (hitCollider.CompareTag("Trap") || hitCollider.GetComponentInParent<SpikeTrap>() != null)
            return false;

        if (((1 << hitCollider.gameObject.layer) & dashThroughLayers.value) != 0)
            return false;

        return true;
    }

    private void ResolveDashEndPenetration(Vector2 dashDirection)
    {
        if (physicsCollider == null || rb == null)
            return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = Physics2D.AllLayers;
        filter.useTriggers = false;

        Vector2 fallbackDirection = dashDirection.sqrMagnitude > 0.0001f
            ? -dashDirection.normalized
            : Vector2.zero;

        for (int iteration = 0; iteration < 4; iteration++)
        {
            int overlapCount = physicsCollider.Overlap(filter, dashOverlapHits);
            float maxPenetration = 0f;
            Vector2 separationDirection = Vector2.zero;

            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D hitCollider = dashOverlapHits[i];
                if (!IsDashBlockingCollider(hitCollider))
                    continue;

                ColliderDistance2D distance = physicsCollider.Distance(hitCollider);
                if (!distance.isOverlapped)
                    continue;

                float penetration = Mathf.Abs(distance.distance);
                if (penetration > maxPenetration)
                {
                    maxPenetration = penetration;
                    separationDirection = distance.normal;
                }
            }

            if (maxPenetration <= 0f)
                break;

            if (separationDirection.sqrMagnitude < 0.0001f)
            {
                if (fallbackDirection.sqrMagnitude > 0.0001f)
                    separationDirection = fallbackDirection;
                else
                    separationDirection = Vector2.up;
            }

            rb.position += separationDirection.normalized * (maxPenetration + DashCollisionPadding);
        }
    }

    public void ResolvePostDashPenetration(Vector2 fallbackDirection)
    {
        ResolveDashEndPenetration(fallbackDirection);
    }

    private void CacheAndSetDashColliderTriggerStates()
    {
        dashColliderTriggerStates.Clear();

        Collider2D[] allPlayerColliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < allPlayerColliders.Length; i++)
        {
            Collider2D col = allPlayerColliders[i];
            if (col == null)
                continue;

            if (!dashColliderTriggerStates.ContainsKey(col))
                dashColliderTriggerStates.Add(col, col.isTrigger);

            col.isTrigger = true;
        }
    }

    private void RestoreDashColliderTriggerStates()
    {
        if (dashColliderTriggerStates.Count == 0)
            return;

        foreach (KeyValuePair<Collider2D, bool> pair in dashColliderTriggerStates)
        {
            if (pair.Key != null)
                pair.Key.isTrigger = pair.Value;
        }

        dashColliderTriggerStates.Clear();
    }

    private void ApplyBossDashCollisionIgnore()
    {
        if (physicsCollider == null)
            return;

        BossEnemy boss = FindFirstObjectByType<BossEnemy>();
        if (boss == null)
            return;

        Collider2D[] bossColliders = boss.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < bossColliders.Length; i++)
        {
            Collider2D bossCollider = bossColliders[i];
            if (bossCollider == null || bossCollider == physicsCollider)
                continue;

            if (!dashIgnoredBossColliders.ContainsKey(bossCollider))
            {
                bool wasIgnored = Physics2D.GetIgnoreCollision(physicsCollider, bossCollider);
                dashIgnoredBossColliders.Add(bossCollider, wasIgnored);
            }

            Physics2D.IgnoreCollision(physicsCollider, bossCollider, true);
        }
    }

    private void RestoreBossDashCollisionIgnore()
    {
        if (dashIgnoredBossColliders.Count == 0)
            return;

        if (physicsCollider != null)
        {
            foreach (KeyValuePair<Collider2D, bool> pair in dashIgnoredBossColliders)
            {
                if (pair.Key != null)
                    Physics2D.IgnoreCollision(physicsCollider, pair.Key, pair.Value);
            }
        }

        dashIgnoredBossColliders.Clear();
    }

    public void CancelDashCharge()
    {
        if (!isCharging)
            return;

        isCharging = false;
        currentChargeTime = 0f;
        projectedCost = 0f;
        currentDashMultiplier = 1f;
        hasChargeAudioStarted = false;

        if (playerAudio != null)
            playerAudio.SetChargeSfxActive(false);

        UpdateChargeFillVisual(0f);

        if (chargeFillImage != null && !isPlayerChargeFillReady)
            chargeFillImage.fillAmount = 0f;
    }

    private void BufferDashInput()
    {
        dashBufferedUntil = Time.unscaledTime + dashInputBufferTime;
    }

    private bool HasBufferedDashInput()
    {
        return Time.unscaledTime <= dashBufferedUntil;
    }

    private void ClearBufferedDashInput()
    {
        dashBufferedUntil = -1f;
    }

    public void ApplyKnockback(Vector2 direction)
    {
        CancelDashCharge();

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
        if (aimIndicator == null)
            CacheAimIndicatorVisual();

        if (aimIndicator == null)
            return;

        if (aimIndicatorRenderer == null)
            aimIndicatorRenderer = aimIndicator.GetComponent<SpriteRenderer>();

        PlayerAbilitySlots slots = GetAbilitySlots();
        bool isChainAssistActive = slots.IsChainAssistActive;

        if (isDashing || isStunned || isChainAssistActive || useWASDDash)
        {
            aimIndicator.SetActive(false);
            return;
        }

        aimIndicator.SetActive(true);

        if (aimIndicatorRenderer != null)
        {
            aimIndicatorRenderer.enabled = true;

            if (playerSpriteRenderer != null)
            {
                aimIndicatorRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
                aimIndicatorRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + 2;
            }
        }

        if (Mouse.current == null || Camera.main == null)
            return;

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        Vector2 toMouse = mouseWorldPosition - (Vector2)transform.position;
        float mouseDistance = toMouse.magnitude;

        Vector2 dashDirection = mouseDistance > 0.0001f ? toMouse / mouseDistance : Vector2.up;
        if (dashDirection.sqrMagnitude > 0.0001f)
            lastAimDirection = dashDirection;

        float currentMultiplier = 1f;

        if (isCharging)
        {
            float effectiveDashCost = dashStaminaCost * currentDiscount;
            if (effectiveDashCost < minimumDashCost) effectiveDashCost = minimumDashCost;

            float ratio = effectiveDashCost / dashStaminaCost;
            float effectiveMaxCost = maxChargeStaminaCost * ratio;

            bool hasUnlimitedCharge = slots.IsInfiniteStaminaSuperActive;
            float maxPossibleCharge = hasUnlimitedCharge ? effectiveMaxCost : Mathf.Min(currentStamina, effectiveMaxCost);
            float possibleChargePercent = Mathf.InverseLerp(effectiveDashCost, effectiveMaxCost, maxPossibleCharge);
            float dynamicMaxChargeTime = maxChargeTime * possibleChargePercent;

            float clampedChargeTime = Mathf.Clamp(currentChargeTime, 0f, dynamicMaxChargeTime);

            if (clampedChargeTime > chargeDelay)
            {
                float chargePercent = clampedChargeTime / maxChargeTime;
                currentMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, maxChargeRangeMultiplier), chargePercent);
            }
        }

        float maxAimDistance = GetBaseDashRange() * currentMultiplier;
        float indicatorDistance = isCharging
            ? maxAimDistance
            : Mathf.Clamp(mouseDistance, 0f, maxAimDistance);
        Vector2 expectedEndPosition = (Vector2)transform.position + dashDirection * indicatorDistance;

        aimIndicator.transform.position = expectedEndPosition;

        float angle = Mathf.Atan2(dashDirection.y, dashDirection.x) * Mathf.Rad2Deg;
        aimIndicator.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }

    private Vector2 GetSafeDashDirection()
    {
        if (useWASDDash)
        {
            // WASD mode: use movement input direction, fall back to last aim
            if (movement.sqrMagnitude > 0.0001f)
                return movement.normalized;

            if (lastAimDirection.sqrMagnitude > 0.0001f)
                return lastAimDirection.normalized;

            return Vector2.up;
        }

        // Mouse mode: aim toward cursor
        if (Mouse.current != null && Camera.main != null)
        {
            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
            Vector2 toMouse = mouseWorldPosition - (Vector2)transform.position;
            if (toMouse.sqrMagnitude > 0.0001f)
                return toMouse.normalized;
        }

        if (movement.sqrMagnitude > 0.0001f)
            return movement.normalized;

        if (lastAimDirection.sqrMagnitude > 0.0001f)
            return lastAimDirection.normalized;

        return Vector2.up;
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

    private float GetComboFlatDamageBonus()
    {
        // First combo hit stays at base damage; later hits add flat bonus.
        float comboBonus = Mathf.Max(0, currentComboCount - 1) * comboDamageBonusPerHit;
        return Mathf.Min(comboBonus, Mathf.Max(0f, maxComboBonusDamage));
    }

    private void TryDamageEnemy(Collider2D collision)
    {
        if (!isDashing)
            return;

        PlayerAbilitySlots slots = GetAbilitySlots();

        EnemyHealth enemy = ResolveEnemyHealth(collision);

        if (enemy == null || enemy.IsDead)
            return;

        if (!hitEnemiesThisDash.Contains(enemy))
        {
            if (slots.IsTemporalSuperActive)
            {
                // Temporal supers defer all damage until the super ends.
                hitEnemiesThisDash.Add(enemy);
                hitEnemyThisDash = true;
                return;
            }

            currentSuperMeter = Mathf.Min(currentSuperMeter + meterGainPerHit, maxSuperMeter);

            bool isFirstEnemyHitThisDash = !hitEnemyThisDash;
            hitEnemiesThisDash.Add(enemy);
            hitEnemyThisDash = true;

            if (isFirstEnemyHitThisDash)
            {
                currentComboCount++;
                RefreshComboUIVisuals(true);
            }

            float comboDamageBonus = GetComboFlatDamageBonus();
            float damageDealt = (baseDashDamage + comboDamageBonus) * currentDashMultiplier;
            float knockbackDealt = baseKnockbackForce * currentDashMultiplier;
            Vector2 knockbackDirection = (enemy.transform.position - transform.position).normalized;
            bool wasAliveBeforeHit = !enemy.IsDead;

            enemy.TakeDamage(damageDealt, knockbackDirection, knockbackDealt);
            PlayImpactSfx();

            if (wasAliveBeforeHit && enemy.IsDead && staminaOnEnemyKill > 0f)
            {
                currentStamina = Mathf.Min(maxStamina, currentStamina + staminaOnEnemyKill);
            }

            SpawnDamagePopup(enemy.transform.position + new Vector3(0f, 1f, 0f), damageDealt);
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

    private void UpdateSpriteAnimation()
    {
        if (playerSpriteRenderer == null)
            return;

        if (isPlayingDeathAnimation)
            return;

        PlayerVisualState targetState = GetTargetVisualState();

        if (targetState != currentVisualState)
        {
            currentVisualState = targetState;
            isAnimationFrame1 = true;
            animationTimer = Mathf.Max(0.01f, animationSwapSeconds);
            ApplyCurrentAnimationFrame();
            return;
        }

        animationTimer -= Time.deltaTime;
        if (animationTimer <= 0f)
        {
            isAnimationFrame1 = !isAnimationFrame1;
            animationTimer = Mathf.Max(0.01f, animationSwapSeconds);
            ApplyCurrentAnimationFrame();
        }
    }

    private PlayerVisualState GetTargetVisualState()
    {
        bool isChainAssistActive = GetAbilitySlots().IsChainAssistActive;

        if (isPlayingDeathAnimation)
            return PlayerVisualState.Dead;

        if (isDashing || isChainAssistActive)
            return PlayerVisualState.Dashing;

        if (movement.sqrMagnitude > 0.0001f)
            return PlayerVisualState.Moving;

        return PlayerVisualState.Idle;
    }

    private void ApplyCurrentAnimationFrame()
    {
        Sprite targetSprite = GetSpriteForState(currentVisualState, isAnimationFrame1);
        if (targetSprite != null)
        {
            playerSpriteRenderer.sprite = targetSprite;
            SyncChargeFillSprite();
        }
    }

    private Sprite GetSpriteForState(PlayerVisualState state, bool frame1)
    {
        switch (state)
        {
            case PlayerVisualState.Dead:
                return frame1 ? (deathFrame1 != null ? deathFrame1 : deathFrame2) : (deathFrame2 != null ? deathFrame2 : deathFrame1);

            case PlayerVisualState.Dashing:
                return frame1 ? (dashFrame1 != null ? dashFrame1 : dashFrame2) : (dashFrame2 != null ? dashFrame2 : dashFrame1);

            case PlayerVisualState.Moving:
                return frame1 ? (moveFrame1 != null ? moveFrame1 : moveFrame2) : (moveFrame2 != null ? moveFrame2 : moveFrame1);

            default:
                return frame1 ? (idleFrame1 != null ? idleFrame1 : idleFrame2) : (idleFrame2 != null ? idleFrame2 : idleFrame1);
        }
    }

    public float PlayDeathAnimation()
    {
        if (playerSpriteRenderer == null)
            return 0f;

        Sprite[] frames = GetDeathFrames();
        if (frames.Length == 0)
            return 0f;

        if (deathAnimationCoroutine != null)
            StopCoroutine(deathAnimationCoroutine);

        deathAnimationCoroutine = StartCoroutine(PlayDeathAnimationCoroutine(frames));
        UpdateChargeFillVisual(0f);
        return frames.Length * Mathf.Max(0.01f, deathFrameDuration);
    }

    public void ResetAfterDeathAnimation()
    {
        if (deathAnimationCoroutine != null)
        {
            StopCoroutine(deathAnimationCoroutine);
            deathAnimationCoroutine = null;
        }

        isPlayingDeathAnimation = false;
        currentVisualState = PlayerVisualState.Idle;
        isAnimationFrame1 = true;
        animationTimer = Mathf.Max(0.01f, animationSwapSeconds);
        ApplyCurrentAnimationFrame();
    }

    private IEnumerator PlayDeathAnimationCoroutine(Sprite[] frames)
    {
        isPlayingDeathAnimation = true;
        currentVisualState = PlayerVisualState.Dead;

        float frameDelay = Mathf.Max(0.01f, deathFrameDuration);

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
                playerSpriteRenderer.sprite = frames[i];

            if (i < frames.Length - 1)
                yield return new WaitForSeconds(frameDelay);
        }
    }

    private Sprite[] GetDeathFrames()
    {
        List<Sprite> frames = new List<Sprite>(4);

        if (deathFrame1 != null) frames.Add(deathFrame1);
        if (deathFrame2 != null) frames.Add(deathFrame2);
        if (deathFrame3 != null) frames.Add(deathFrame3);
        if (deathFrame4 != null) frames.Add(deathFrame4);

        return frames.ToArray();
    }

    private void SetupPlayerChargeFillVisual()
    {
        if (!usePlayerChargeFill || playerSpriteRenderer == null)
            return;

        if (chargeFillOverlayRenderer == null)
        {
            GameObject overlayObj = new GameObject("ChargeFillOverlay");
            overlayObj.transform.SetParent(transform, false);
            chargeFillOverlayRenderer = overlayObj.AddComponent<SpriteRenderer>();
        }

        if (chargeFillMask == null)
        {
            GameObject maskObj = new GameObject("ChargeFillMask");
            maskObj.transform.SetParent(transform, false);
            chargeFillMask = maskObj.AddComponent<SpriteMask>();
        }

        chargeFillOverlayRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        chargeFillOverlayRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
        chargeFillOverlayRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + chargeFillSortingOffset;
        chargeFillOverlayRenderer.color = playerChargeFillColor;
        chargeFillOverlayRenderer.enabled = false;

        chargeFillMask.sprite = chargeMaskSprite != null ? chargeMaskSprite : playerSpriteRenderer.sprite;
        chargeFillMask.transform.localRotation = Quaternion.identity;

        isPlayerChargeFillReady = chargeFillOverlayRenderer != null && chargeFillMask != null;

        if (isPlayerChargeFillReady && chargeFillImage != null)
            chargeFillImage.gameObject.SetActive(false);

        SyncChargeFillSprite();
        UpdateChargeFillVisual(0f);
    }

    private void SyncChargeFillSprite()
    {
        if (!isPlayerChargeFillReady || playerSpriteRenderer == null)
            return;

        chargeFillOverlayRenderer.sprite = playerSpriteRenderer.sprite;

        if (chargeMaskSprite == null)
            chargeFillMask.sprite = playerSpriteRenderer.sprite;
    }

    private void UpdateChargeFillVisual(float normalizedCharge)
    {
        if (!isPlayerChargeFillReady || playerSpriteRenderer == null || chargeFillOverlayRenderer == null || chargeFillMask == null)
            return;

        float clamped = Mathf.Clamp01(normalizedCharge);
        SyncChargeFillSprite();

        if (clamped <= 0f || chargeFillOverlayRenderer.sprite == null || chargeFillMask.sprite == null)
        {
            chargeFillOverlayRenderer.enabled = false;
            return;
        }

        chargeFillOverlayRenderer.enabled = true;
        chargeFillOverlayRenderer.color = playerChargeFillColor;
        chargeFillOverlayRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
        chargeFillOverlayRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + chargeFillSortingOffset;

        Vector2 playerSize = chargeFillOverlayRenderer.sprite.bounds.size;
        Vector2 maskSize = chargeFillMask.sprite.bounds.size;

        float safeMaskWidth = Mathf.Max(0.0001f, maskSize.x);
        float safeMaskHeight = Mathf.Max(0.0001f, maskSize.y);

        float fillHeight = playerSize.y * clamped;
        float scaleX = playerSize.x / safeMaskWidth;
        float scaleY = Mathf.Max(0.0001f, fillHeight / safeMaskHeight);

        chargeFillMask.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        chargeFillMask.transform.localPosition = new Vector3(0f, (-playerSize.y * 0.5f) + (fillHeight * 0.5f), 0f);
    }

    public void SetCanDash(bool value)
    {
        canDash = value;
    }

    public void RegisterSingleComboHitAndStartTimer()
    {
        currentComboCount++;
        RefreshComboUIVisuals(true);

        isComboWindowActive = true;
        currentComboTimer = currentComboWindowDuration;
        if (comboTimerCircle != null) comboTimerCircle.fillAmount = 1f;
    }

    private void ResetComboCounter()
    {
        currentComboCount = 0;

        RefreshComboUIVisuals(false);
    }

    private void RefreshComboUIVisuals(bool showComboContainer)
    {
        if (comboHitText != null)
            comboHitText.text = Mathf.Max(0, currentComboCount).ToString();

        if (comboUIContainer != null)
            comboUIContainer.SetActive(true);

        if (comboCircleSpriteProgression != null)
            comboCircleSpriteProgression.SetComboHits(currentComboCount);
    }

    public void SpawnDamagePopup(Vector3 worldPosition, float damageAmount)
    {
        GameObject popup = null;
        TMP_Text sourceText = null;

        if (damagePopupPrefab != null)
        {
            popup = Instantiate(damagePopupPrefab, worldPosition, Quaternion.identity);
            sourceText = FindTmpText(popup);
        }

        if (popup != null)
            Destroy(popup);

        popup = CreateOverlayDamagePopup(worldPosition, sourceText);
        if (popup == null)
        {
            popup = CreateFallbackDamagePopup(worldPosition);
        }

        if (popup == null)
            return;

        DamagePopup popupComponent = popup != null ? popup.GetComponent<DamagePopup>() : null;
        if (popupComponent == null)
            popupComponent = popup.AddComponent<DamagePopup>();

        if (popupComponent != null)
            popupComponent.Setup(damageAmount, true);
    }

    private TMP_Text FindTmpText(GameObject popup)
    {
        if (popup == null)
            return null;

        TMP_Text text = popup.GetComponent<TMP_Text>();
        if (text != null)
            return text;

        return popup.GetComponentInChildren<TMP_Text>(true);
    }

    private RectTransform EnsureDamagePopupCanvas()
    {
        if (damagePopupCanvas != null && damagePopupCanvasRect != null)
            return damagePopupCanvasRect;

        GameObject root = new GameObject("DamagePopupCanvas");
        DontDestroyOnLoad(root);

        damagePopupCanvas = root.AddComponent<Canvas>();
        damagePopupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        damagePopupCanvas.sortingOrder = 5000;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        root.AddComponent<GraphicRaycaster>();

        damagePopupCanvasRect = root.GetComponent<RectTransform>();
        return damagePopupCanvasRect;
    }

    private GameObject CreateOverlayDamagePopup(Vector3 worldPosition, TMP_Text styleSource)
    {
        RectTransform canvasRect = EnsureDamagePopupCanvas();
        if (canvasRect == null)
            return null;

        GameObject popup = new GameObject("DamagePopup_Overlay");
        popup.transform.SetParent(canvasRect, false);

        RectTransform rect = popup.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(220f, 80f);

        Vector3 screenPosition = worldPosition;
        if (Camera.main != null)
            screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

        rect.position = new Vector3(screenPosition.x, screenPosition.y, 0f);

        TextMeshProUGUI text = popup.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;

        if (styleSource != null)
        {
            text.font = styleSource.font;
            text.fontSize = styleSource.fontSize;
            text.color = styleSource.color;
            text.fontStyle = styleSource.fontStyle;
            text.characterSpacing = styleSource.characterSpacing;
            text.outlineWidth = styleSource.outlineWidth;
            text.outlineColor = styleSource.outlineColor;
        }
        else
        {
            text.fontSize = 52f;
            text.color = new Color(1f, 0.92f, 0.55f, 1f);
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color(0f, 0f, 0f, 0.8f);

            if (TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;
        }

        return popup;
    }

    private GameObject CreateFallbackDamagePopup(Vector3 worldPosition)
    {
        GameObject popup = new GameObject("DamagePopup_Fallback");
        popup.transform.position = worldPosition;
        popup.transform.localScale = Vector3.one * 0.12f;

        TextMeshPro text = popup.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 36f;
        text.color = new Color(1f, 0.92f, 0.55f, 1f);
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.8f);

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        MeshRenderer meshRenderer = text.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            if (playerSpriteRenderer != null)
                meshRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;

            meshRenderer.sortingOrder = playerSortingOrder + 30;
        }

        return popup;
    }

    public bool ApplyAbilityHitWithoutCombo(EnemyHealth target, float damage, float knockback)
    {
        if (target == null || target.IsDead)
            return false;

        Vector2 knockbackDirection = (target.transform.position - transform.position).normalized;
        bool wasAliveBeforeHit = !target.IsDead;

        target.TakeDamage(damage, knockbackDirection, knockback);
        PlayImpactSfx();
        currentSuperMeter = Mathf.Min(currentSuperMeter + meterGainPerHit, maxSuperMeter);

        if (wasAliveBeforeHit && target.IsDead && staminaOnEnemyKill > 0f)
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaOnEnemyKill);

        SpawnDamagePopup(target.transform.position + new Vector3(0f, 1f, 0f), damage);

        return true;
    }

    public void ApplyAbilityHit(EnemyHealth target, float damage, float knockback)
    {
        if (!ApplyAbilityHitWithoutCombo(target, damage, knockback))
            return;

        RegisterSingleComboHitAndStartTimer();
    }

    public EnemyHealth FindClosestEnemy(float maxDistance)
    {
        return FindClosestEnemy(maxDistance, null);
    }

    public EnemyHealth FindClosestEnemy(float maxDistance, HashSet<EnemyHealth> excludedEnemies)
    {
        return FindClosestEnemy(maxDistance, excludedEnemies, false);
    }

    public EnemyHealth FindClosestEnemy(float maxDistance, HashSet<EnemyHealth> excludedEnemies, bool requireLineOfSight)
    {
        EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        EnemyHealth closest = null;
        float bestDistanceSquared = maxDistance * maxDistance;
        Vector2 currentPosition = transform.position;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            if (excludedEnemies != null && excludedEnemies.Contains(enemy))
                continue;

            if (requireLineOfSight && !HasLineOfSightToEnemy(enemy))
                continue;

            float sqrDistance = ((Vector2)enemy.transform.position - currentPosition).sqrMagnitude;
            if (sqrDistance <= bestDistanceSquared)
            {
                bestDistanceSquared = sqrDistance;
                closest = enemy;
            }
        }

        return closest;
    }

    public bool HasLineOfSightToEnemy(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsDead)
            return false;

        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 target = enemy.transform.position;
        Vector2 delta = target - origin;
        float distance = delta.magnitude;

        if (distance <= 0.0001f)
            return true;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = Physics2D.AllLayers;
        filter.useTriggers = false;

        int hitCount = Physics2D.Raycast(origin, delta / distance, filter, chainLineOfSightHits, distance);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = chainLineOfSightHits[i].collider;
            if (hitCollider == null)
                continue;

            if (hitCollider.attachedRigidbody == rb)
                continue;

            EnemyHealth hitEnemy = ResolveEnemyHealth(hitCollider);
            if (hitEnemy != null)
                continue;

            // Any non-enemy solid collider between player and target blocks chain targeting.
            return false;
        }

        return true;
    }

    public void DamageEnemiesAlongPath(
        Vector2 start,
        Vector2 end,
        float radius,
        float damage,
        bool allowMultiHitsPerEnemyAcrossSamples,
        int samplesPerUnit,
        float knockbackForce,
        Vector2? knockbackDirectionOverride,
        bool grantComboAndMeter,
        bool showDamagePopup = true,
        Dictionary<int, float> accumulatedDamageByEnemy = null,
        Dictionary<int, Vector3> accumulatedDamagePopupPositions = null)
    {
        float safeRadius = Mathf.Max(0.05f, radius);
        float distance = Vector2.Distance(start, end);
        int samples = Mathf.Max(1, Mathf.CeilToInt(distance * Mathf.Max(1, samplesPerUnit)));
        HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>();

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

                if (!allowMultiHitsPerEnemyAcrossSamples && damagedEnemies.Contains(enemy))
                    continue;

                Vector2 knockbackDirection = knockbackDirectionOverride ?? ((Vector2)enemy.transform.position - samplePosition).normalized;
                bool wasAliveBeforeHit = !enemy.IsDead;
                enemy.TakeDamage(damage, knockbackDirection, knockbackForce);
                PlayImpactSfx();
                damagedEnemies.Add(enemy);

                if (accumulatedDamageByEnemy != null)
                {
                    int enemyId = enemy.GetInstanceID();

                    if (accumulatedDamageByEnemy.ContainsKey(enemyId))
                        accumulatedDamageByEnemy[enemyId] += damage;
                    else
                        accumulatedDamageByEnemy.Add(enemyId, damage);

                    if (accumulatedDamagePopupPositions != null)
                        accumulatedDamagePopupPositions[enemyId] = enemy.transform.position + new Vector3(0f, 1f, 0f);
                }

                if (showDamagePopup)
                    SpawnDamagePopup(enemy.transform.position + new Vector3(0f, 1f, 0f), damage);

                if (grantComboAndMeter)
                {
                    currentSuperMeter = Mathf.Min(currentSuperMeter + meterGainPerHit, maxSuperMeter);
                    currentComboCount++;
                    RefreshComboUIVisuals(true);
                    isComboWindowActive = true;
                    currentComboTimer = currentComboWindowDuration;
                    if (comboTimerCircle != null) comboTimerCircle.fillAmount = 1f;

                    if (wasAliveBeforeHit && enemy.IsDead && staminaOnEnemyKill > 0f)
                        currentStamina = Mathf.Min(maxStamina, currentStamina + staminaOnEnemyKill);
                }
            }
        }
    }
}