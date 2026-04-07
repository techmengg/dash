using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerAbilitySlots))]
public class PlayerUpgradeDeck : MonoBehaviour
{
    public enum UpgradeEffectType
    {
        MoveSpeedPercent,
        DashRangePercent,
        DashDurationReductionPercent,
        DashCooldownReductionPercent,
        MaxHealthFlat,
        PassiveRegenFlat,
        MaxStaminaFlat,
        StaminaRegenPercent,
        DashCostReductionPercent,
        StaminaOnKillFlat,
        DashDamagePercent,
        DashKnockbackPercent,
        ComboWindowPercent,
        ComboDamagePerHitFlat,
        SuperMeterRequirementReductionPercent,
        SuperMeterGainPercent,
        SuperDurationPercent,
        MaxChargeTimePercent,
        ChargeRangePercent,
        ChargeDamagePercent,
        ChargeCostReductionPercent,
        ChargeDelayReductionPercent,
        DashEnhancerCooldownReductionPercent,
        ProjectileModeDurationPercent,
        ProjectileDamagePercent,
        ProjectileRangePercent,
        ChainDamagePercent,
        ChainRadiusPercent,
        TrailDetonationDamagePercent,
        HitIFramePercent
    }

    [Serializable]
    public class UpgradeDefinition
    {
        public string id;
        public string title;
        [TextArea(2, 5)] public string description;
        public Color accentColor = new Color(0.85f, 0.25f, 0.2f, 1f);
        public UpgradeEffectType effectType;
        public float value = 0.1f;
        [Min(1)] public int maxStacks = 3;
        public bool enabled = true;
    }

    [Serializable]
    public class UpgradeStackSnapshot
    {
        public string upgradeId;
        public int stackCount;
    }

    [Header("Card Pool")]
    public bool autoPopulateDefaults = true;
    public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();

    [Header("Debug")]
    public bool logSelections = true;

    private readonly Dictionary<string, int> runtimeStacks = new Dictionary<string, int>();
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private PlayerAbilitySlots abilitySlots;

    private void Awake()
    {
        CacheReferences();
        EnsureDefaultsLoaded();
    }

    private void OnValidate()
    {
        EnsureDefaultsLoaded();
    }

    private void CacheReferences()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (abilitySlots == null)
            abilitySlots = GetComponent<PlayerAbilitySlots>();
    }

    public bool TryGetCardPair(out UpgradeDefinition left, out UpgradeDefinition right)
    {
        left = null;
        right = null;

        List<UpgradeDefinition> available = GetAvailableUpgrades();
        if (available.Count == 0)
            return false;

        int first = UnityEngine.Random.Range(0, available.Count);
        left = available[first];

        if (available.Count == 1)
        {
            right = available[first];
            return true;
        }

        int second = first;
        while (second == first)
            second = UnityEngine.Random.Range(0, available.Count);

        right = available[second];
        return true;
    }

    public bool ApplyUpgrade(UpgradeDefinition upgrade)
    {
        if (upgrade == null)
            return false;

        CacheReferences();
        if (playerMovement == null || playerHealth == null || abilitySlots == null)
            return false;

        string upgradeId = string.IsNullOrWhiteSpace(upgrade.id) ? upgrade.title : upgrade.id;
        if (string.IsNullOrWhiteSpace(upgradeId))
            upgradeId = Guid.NewGuid().ToString("N");

        int stackCount = GetStackCount(upgradeId);
        if (stackCount >= Mathf.Max(1, upgrade.maxStacks))
            return false;

        ApplyEffect(upgrade.effectType, upgrade.value);
        runtimeStacks[upgradeId] = stackCount + 1;

        if (logSelections)
            Debug.Log($"Upgrade applied: {upgrade.title} (Stack {runtimeStacks[upgradeId]}/{Mathf.Max(1, upgrade.maxStacks)})");

        return true;
    }

    public int GetStackCount(string upgradeId)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
            return 0;

        int count;
        return runtimeStacks.TryGetValue(upgradeId, out count) ? count : 0;
    }

    public List<UpgradeDefinition> GetAvailableUpgrades()
    {
        EnsureDefaultsLoaded();

        List<UpgradeDefinition> available = new List<UpgradeDefinition>();
        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeDefinition candidate = upgrades[i];
            if (candidate == null || !candidate.enabled)
                continue;

            string id = string.IsNullOrWhiteSpace(candidate.id) ? candidate.title : candidate.id;
            int stacks = GetStackCount(id);
            if (stacks < Mathf.Max(1, candidate.maxStacks))
                available.Add(candidate);
        }

        return available;
    }

    public List<UpgradeStackSnapshot> CreateRuntimeSnapshot()
    {
        List<UpgradeStackSnapshot> snapshot = new List<UpgradeStackSnapshot>();
        foreach (KeyValuePair<string, int> pair in runtimeStacks)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
                continue;

            snapshot.Add(new UpgradeStackSnapshot
            {
                upgradeId = pair.Key,
                stackCount = pair.Value
            });
        }

        return snapshot;
    }

    public void ApplyRuntimeSnapshot(List<UpgradeStackSnapshot> snapshot)
    {
        if (snapshot == null || snapshot.Count == 0)
            return;

        CacheReferences();
        EnsureDefaultsLoaded();

        if (playerMovement == null || playerHealth == null || abilitySlots == null)
            return;

        bool previousLogState = logSelections;
        logSelections = false;

        for (int i = 0; i < snapshot.Count; i++)
        {
            UpgradeStackSnapshot entry = snapshot[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.upgradeId) || entry.stackCount <= 0)
                continue;

            UpgradeDefinition definition = FindUpgradeById(entry.upgradeId);
            if (definition == null)
                continue;

            int current = GetStackCount(entry.upgradeId);
            int target = Mathf.Clamp(entry.stackCount, 0, Mathf.Max(1, definition.maxStacks));
            for (int stack = current; stack < target; stack++)
                ApplyUpgrade(definition);
        }

        logSelections = previousLogState;
    }

    private UpgradeDefinition FindUpgradeById(string upgradeId)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
            return null;

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeDefinition candidate = upgrades[i];
            if (candidate == null)
                continue;

            string candidateId = string.IsNullOrWhiteSpace(candidate.id) ? candidate.title : candidate.id;
            if (string.Equals(candidateId, upgradeId, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    private void ApplyEffect(UpgradeEffectType effectType, float value)
    {
        switch (effectType)
        {
            case UpgradeEffectType.MoveSpeedPercent:
                playerMovement.moveSpeed = Mathf.Max(0.1f, playerMovement.moveSpeed * (1f + value));
                break;
            case UpgradeEffectType.DashRangePercent:
                playerMovement.dashRange = Mathf.Max(0.1f, playerMovement.dashRange * (1f + value));
                break;
            case UpgradeEffectType.DashDurationReductionPercent:
                playerMovement.dashDuration = Mathf.Max(0.05f, playerMovement.dashDuration * (1f - value));
                break;
            case UpgradeEffectType.DashCooldownReductionPercent:
                playerMovement.dashCooldown = Mathf.Max(0.05f, playerMovement.dashCooldown * (1f - value));
                break;
            case UpgradeEffectType.MaxHealthFlat:
                playerHealth.maxHealth = Mathf.Max(1f, playerHealth.maxHealth + value);
                playerHealth.currentHealth = Mathf.Min(playerHealth.maxHealth, playerHealth.currentHealth + value);
                if (playerHealth.healthBarFill != null)
                    playerHealth.healthBarFill.fillAmount = playerHealth.currentHealth / Mathf.Max(0.01f, playerHealth.maxHealth);
                break;
            case UpgradeEffectType.PassiveRegenFlat:
                playerHealth.enablePassiveRegen = true;
                playerHealth.passiveRegenPerSecond = Mathf.Max(0f, playerHealth.passiveRegenPerSecond + value);
                break;
            case UpgradeEffectType.MaxStaminaFlat:
                playerMovement.maxStamina = Mathf.Max(1f, playerMovement.maxStamina + value);
                playerMovement.currentStamina = Mathf.Min(playerMovement.maxStamina, playerMovement.currentStamina + value);
                break;
            case UpgradeEffectType.StaminaRegenPercent:
                playerMovement.staminaRegenRate = Mathf.Max(0f, playerMovement.staminaRegenRate * (1f + value));
                break;
            case UpgradeEffectType.DashCostReductionPercent:
                playerMovement.dashStaminaCost = Mathf.Max(1f, playerMovement.dashStaminaCost * (1f - value));
                playerMovement.minimumDashCost = Mathf.Max(1f, playerMovement.minimumDashCost * (1f - value));
                break;
            case UpgradeEffectType.StaminaOnKillFlat:
                playerMovement.staminaOnEnemyKill = Mathf.Max(0f, playerMovement.staminaOnEnemyKill + value);
                break;
            case UpgradeEffectType.DashDamagePercent:
                playerMovement.baseDashDamage = Mathf.Max(0.1f, playerMovement.baseDashDamage * (1f + value));
                break;
            case UpgradeEffectType.DashKnockbackPercent:
                playerMovement.baseKnockbackForce = Mathf.Max(0.1f, playerMovement.baseKnockbackForce * (1f + value));
                break;
            case UpgradeEffectType.ComboWindowPercent:
                playerMovement.comboWindowTime = Mathf.Max(0.2f, playerMovement.comboWindowTime * (1f + value));
                playerMovement.maxComboWindowTime = Mathf.Max(playerMovement.comboWindowTime, playerMovement.maxComboWindowTime * (1f + value));
                break;
            case UpgradeEffectType.ComboDamagePerHitFlat:
                playerMovement.comboDamageBonusPerHit = Mathf.Max(0f, playerMovement.comboDamageBonusPerHit + value);
                playerMovement.maxComboBonusDamage = Mathf.Max(playerMovement.comboDamageBonusPerHit, playerMovement.maxComboBonusDamage + value * 2f);
                break;
            case UpgradeEffectType.SuperMeterRequirementReductionPercent:
                playerMovement.maxSuperMeter = Mathf.Max(15f, playerMovement.maxSuperMeter * (1f - value));
                playerMovement.currentSuperMeter = Mathf.Min(playerMovement.currentSuperMeter, playerMovement.maxSuperMeter);
                break;
            case UpgradeEffectType.SuperMeterGainPercent:
                playerMovement.meterGainPerHit = Mathf.Max(0.1f, playerMovement.meterGainPerHit * (1f + value));
                break;
            case UpgradeEffectType.SuperDurationPercent:
                playerMovement.superDuration = Mathf.Max(0.25f, playerMovement.superDuration * (1f + value));
                break;
            case UpgradeEffectType.MaxChargeTimePercent:
                playerMovement.maxChargeTime = Mathf.Max(0.1f, playerMovement.maxChargeTime * (1f + value));
                break;
            case UpgradeEffectType.ChargeRangePercent:
                playerMovement.maxChargeRangeMultiplier = Mathf.Max(1f, playerMovement.maxChargeRangeMultiplier * (1f + value));
                break;
            case UpgradeEffectType.ChargeDamagePercent:
                playerMovement.maxChargeDamageMultiplier = Mathf.Max(1f, playerMovement.maxChargeDamageMultiplier * (1f + value));
                break;
            case UpgradeEffectType.ChargeCostReductionPercent:
                playerMovement.maxChargeStaminaCost = Mathf.Max(1f, playerMovement.maxChargeStaminaCost * (1f - value));
                break;
            case UpgradeEffectType.ChargeDelayReductionPercent:
                playerMovement.chargeDelay = Mathf.Max(0f, playerMovement.chargeDelay * (1f - value));
                break;
            case UpgradeEffectType.DashEnhancerCooldownReductionPercent:
                abilitySlots.dashEnhancerCooldown = Mathf.Max(1f, abilitySlots.dashEnhancerCooldown * (1f - value));
                break;
            case UpgradeEffectType.ProjectileModeDurationPercent:
                abilitySlots.projectileModeDuration = Mathf.Max(0.5f, abilitySlots.projectileModeDuration * (1f + value));
                break;
            case UpgradeEffectType.ProjectileDamagePercent:
                abilitySlots.projectileGhostDamageMultiplier = Mathf.Max(0.1f, abilitySlots.projectileGhostDamageMultiplier * (1f + value));
                break;
            case UpgradeEffectType.ProjectileRangePercent:
                abilitySlots.projectileGhostRange = Mathf.Max(0.1f, abilitySlots.projectileGhostRange * (1f + value));
                break;
            case UpgradeEffectType.ChainDamagePercent:
                abilitySlots.chainDashDamageMultiplier = Mathf.Max(0.1f, abilitySlots.chainDashDamageMultiplier * (1f + value));
                break;
            case UpgradeEffectType.ChainRadiusPercent:
                abilitySlots.chainSearchRadius = Mathf.Max(0.5f, abilitySlots.chainSearchRadius * (1f + value));
                break;
            case UpgradeEffectType.TrailDetonationDamagePercent:
                abilitySlots.trailDetonationDamage = Mathf.Max(0.1f, abilitySlots.trailDetonationDamage * (1f + value));
                break;
            case UpgradeEffectType.HitIFramePercent:
                playerHealth.iFrameDuration = Mathf.Max(0.1f, playerHealth.iFrameDuration * (1f + value));
                break;
        }
    }

    private void EnsureDefaultsLoaded()
    {
        if (!autoPopulateDefaults)
            return;

        if (upgrades != null && upgrades.Count > 0)
            return;

        upgrades = CreateDefaultUpgradeLibrary();
    }

    private List<UpgradeDefinition> CreateDefaultUpgradeLibrary()
    {
        return new List<UpgradeDefinition>
        {
            Make("swift-footwork", "Swift Footwork", "+10% move speed.", new Color(0.96f, 0.62f, 0.24f, 1f), UpgradeEffectType.MoveSpeedPercent, 0.10f, 4),
            Make("phantom-reach", "Phantom Reach", "+15% dash range.", new Color(0.34f, 0.76f, 0.95f, 1f), UpgradeEffectType.DashRangePercent, 0.15f, 4),
            Make("blink-compression", "Blink Compression", "-12% dash duration (faster burst).", new Color(0.7f, 0.88f, 1f, 1f), UpgradeEffectType.DashDurationReductionPercent, 0.12f, 4),
            Make("coolant-jets", "Coolant Jets", "-10% dash cooldown.", new Color(0.24f, 0.84f, 0.76f, 1f), UpgradeEffectType.DashCooldownReductionPercent, 0.10f, 4),
            Make("reinforced-core", "Reinforced Core", "+1 max health and heal 1.", new Color(0.92f, 0.34f, 0.28f, 1f), UpgradeEffectType.MaxHealthFlat, 1f, 5),
            Make("adrenal-recovery", "Adrenal Recovery", "+0.15 passive regen/sec.", new Color(0.4f, 0.88f, 0.45f, 1f), UpgradeEffectType.PassiveRegenFlat, 0.15f, 4),
            Make("ether-reservoir", "Ether Reservoir", "+20 max stamina.", new Color(0.27f, 0.64f, 0.98f, 1f), UpgradeEffectType.MaxStaminaFlat, 20f, 4),
            Make("rapid-recovery", "Rapid Recovery", "+20% stamina regen.", new Color(0.2f, 0.78f, 0.96f, 1f), UpgradeEffectType.StaminaRegenPercent, 0.20f, 4),
            Make("efficient-burst", "Efficient Burst", "-15% dash stamina cost.", new Color(0.28f, 0.82f, 0.7f, 1f), UpgradeEffectType.DashCostReductionPercent, 0.15f, 4),
            Make("hunters-dividend", "Hunter's Dividend", "+4 stamina on enemy kill.", new Color(0.84f, 0.7f, 0.24f, 1f), UpgradeEffectType.StaminaOnKillFlat, 4f, 4),
            Make("jagged-trail", "Jagged Trail", "+18% base dash damage.", new Color(1f, 0.48f, 0.38f, 1f), UpgradeEffectType.DashDamagePercent, 0.18f, 4),
            Make("crushing-momentum", "Crushing Momentum", "+20% dash knockback.", new Color(0.95f, 0.58f, 0.22f, 1f), UpgradeEffectType.DashKnockbackPercent, 0.20f, 4),
            Make("combo-tempo", "Combo Tempo", "+15% combo window duration.", new Color(0.96f, 0.77f, 0.3f, 1f), UpgradeEffectType.ComboWindowPercent, 0.15f, 4),
            Make("rising-punish", "Rising Punish", "+0.06 combo damage per hit.", new Color(1f, 0.63f, 0.36f, 1f), UpgradeEffectType.ComboDamagePerHitFlat, 0.06f, 5),
            Make("super-compression", "Super Compression", "-12% super meter required.", new Color(0.76f, 0.92f, 0.34f, 1f), UpgradeEffectType.SuperMeterRequirementReductionPercent, 0.12f, 4),
            Make("arc-siphon", "Arc Siphon", "+18% super meter gain per hit.", new Color(0.54f, 0.9f, 0.52f, 1f), UpgradeEffectType.SuperMeterGainPercent, 0.18f, 4),
            Make("overclocked-super", "Overclocked Super", "+18% super duration.", new Color(0.45f, 0.93f, 0.74f, 1f), UpgradeEffectType.SuperDurationPercent, 0.18f, 4),
            Make("perfect-focus", "Perfect Focus", "+20% max charge time.", new Color(0.58f, 0.84f, 1f, 1f), UpgradeEffectType.MaxChargeTimePercent, 0.20f, 3),
            Make("elastic-windup", "Elastic Windup", "+15% max charge range multiplier.", new Color(0.34f, 0.75f, 0.98f, 1f), UpgradeEffectType.ChargeRangePercent, 0.15f, 4),
            Make("voltaic-windup", "Voltaic Windup", "+15% max charge damage multiplier.", new Color(0.25f, 0.66f, 0.98f, 1f), UpgradeEffectType.ChargeDamagePercent, 0.15f, 4),
            Make("lean-charge", "Lean Charge", "-12% max charge stamina cost.", new Color(0.24f, 0.89f, 0.85f, 1f), UpgradeEffectType.ChargeCostReductionPercent, 0.12f, 4),
            Make("flash-start", "Flash Start", "-20% charge delay.", new Color(0.84f, 0.94f, 1f, 1f), UpgradeEffectType.ChargeDelayReductionPercent, 0.20f, 4),
            Make("ghost-capacitor", "Ghost Capacitor", "-15% dash enhancer cooldown.", new Color(0.72f, 0.95f, 1f, 1f), UpgradeEffectType.DashEnhancerCooldownReductionPercent, 0.15f, 4),
            Make("specter-drift", "Specter Drift", "+20% projectile mode duration.", new Color(0.52f, 0.91f, 1f, 1f), UpgradeEffectType.ProjectileModeDurationPercent, 0.20f, 4),
            Make("specter-payload", "Specter Payload", "+20% ghost projectile damage.", new Color(0.43f, 0.82f, 1f, 1f), UpgradeEffectType.ProjectileDamagePercent, 0.20f, 4),
            Make("specter-reach", "Specter Reach", "+15% ghost projectile range.", new Color(0.38f, 0.74f, 1f, 1f), UpgradeEffectType.ProjectileRangePercent, 0.15f, 4),
            Make("chain-voltage", "Chain Voltage", "+18% chain assist damage.", new Color(0.88f, 0.95f, 0.36f, 1f), UpgradeEffectType.ChainDamagePercent, 0.18f, 4),
            Make("chain-radius", "Chain Radius", "+20% chain search radius.", new Color(0.76f, 0.9f, 0.32f, 1f), UpgradeEffectType.ChainRadiusPercent, 0.20f, 4),
            Make("temporal-payload", "Temporal Payload", "+18% trail detonation damage.", new Color(1f, 0.78f, 0.34f, 1f), UpgradeEffectType.TrailDetonationDamagePercent, 0.18f, 4),
            Make("iron-nerves", "Iron Nerves", "+15% hit i-frame duration.", new Color(0.94f, 0.85f, 0.62f, 1f), UpgradeEffectType.HitIFramePercent, 0.15f, 4)
        };
    }

    private UpgradeDefinition Make(
        string id,
        string title,
        string description,
        Color accentColor,
        UpgradeEffectType effectType,
        float value,
        int maxStacks)
    {
        return new UpgradeDefinition
        {
            id = id,
            title = title,
            description = description,
            accentColor = accentColor,
            effectType = effectType,
            value = value,
            maxStacks = Mathf.Max(1, maxStacks),
            enabled = true
        };
    }
}
