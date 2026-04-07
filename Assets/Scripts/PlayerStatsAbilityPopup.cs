using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAbilitySlots))]
public class PlayerStatsAbilityPopup : MonoBehaviour
{
    [Header("Popup Settings")]
    public KeyCode holdPopupKey = KeyCode.Tab;
    public bool pauseGameplayWhileOpen = false;
    public bool forceCursorVisibleWhileOpen = true;
    public bool usePauseMenuTheme = true;
    [Range(0f, 1f)] public float backdropAlpha = 0.88f;
    public Vector2 panelSize = new Vector2(760f, 470f);

    [Header("Text Settings")]
    [Range(0.75f, 2f)] public float textScale = 1f;

    [Header("Menu UI SFX")]
    public AudioClip buttonSelectSfx;
    [Range(0f, 1f)] public float buttonSelectSfxVolume = 1f;
    public AudioClip buttonHighlightSfx;
    [Range(0f, 1f)] public float buttonHighlightSfxVolume = 0.85f;
    [Min(0f)] public float highlightSfxCooldown = 0.05f;

    [Header("Popup Colors")]
    public Color panelColor = new Color(0.06f, 0.05f, 0.07f, 1f);
    public Color panelBorderColor = new Color(0.4f, 0.12f, 0.1f, 1f);
    public Color textColor = new Color(0.75f, 0.68f, 0.58f, 1f);
    public Color accentTextColor = new Color(0.92f, 0.85f, 0.65f, 1f);
    public Color buttonColor = new Color(0.10f, 0.08f, 0.09f, 1f);
    public Color selectedButtonColor = new Color(0.74f, 0.34f, 0.12f, 1f);

    private static readonly Color backdropColor = new Color(0.02f, 0.02f, 0.03f, 0.92f);
    private static readonly Color subtitleColor = new Color(0.6f, 0.55f, 0.5f, 1f);
    private static readonly Color sectionLineColor = new Color(0.7f, 0.55f, 0.3f, 0.25f);
    private static readonly Color statsPanelColor = new Color(0.10f, 0.08f, 0.09f, 0.95f);
    private static readonly Color buttonHoverColor = new Color(0.18f, 0.12f, 0.10f, 1f);
    private static readonly Color buttonPressedColor = new Color(0.06f, 0.04f, 0.05f, 1f);
    private static readonly Color buttonTextColor = new Color(0.88f, 0.78f, 0.58f, 1f);
    private static readonly Color buttonBorderColor = new Color(0.4f, 0.12f, 0.1f, 0.7f);
    private static readonly Color selectedButtonTextColor = new Color(1f, 0.96f, 0.82f, 1f);
    private static readonly Color selectedButtonBorderColor = new Color(0.95f, 0.74f, 0.35f, 0.95f);

    [Header("Optional References")]
    public PlayerMovement playerMovement;
    public PlayerHealth playerHealth;
    public PlayerAbilitySlots abilitySlots;

    private readonly Dictionary<SuperEnhancerSlot, Button> superButtons = new Dictionary<SuperEnhancerSlot, Button>();
    private readonly Dictionary<DashEnhancerSlot, Button> dashButtons = new Dictionary<DashEnhancerSlot, Button>();
    private readonly HashSet<Button> hoveredButtons = new HashSet<Button>();
    private readonly StringBuilder statsBuilder = new StringBuilder(256);
    private readonly List<Text> popupTexts = new List<Text>();
    private readonly List<int> popupTextBaseSizes = new List<int>();

    private GameObject canvasRoot;
    private GameObject panelRoot;
    private RectTransform panelRectTransform;
    private Image backdropImage;
    private Text statsText;
    private Font uiFont;
    private Vector2 appliedPanelSize;
    private float appliedBackdropAlpha = -1f;
    private float appliedTextScale = -1f;

    private bool isPopupVisible;
    private bool changedTimeScale;
    private float previousTimeScale = 1f;
    private bool changedCursorState;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private AudioSource uiSfxSource;
    private float lastHighlightSfxTime = -10f;
    private GameObject lastHighlightedButton;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        ApplyPauseMenuThemeIfEnabled();
        CacheReferences();
        EnsureUiSfxSource();
        BuildPopupUi();
        ApplyLayoutSettings(true);
        SetPopupVisible(false, true);
    }

    private void Update()
    {
        ApplyLayoutSettings(false);

        bool shouldShowPopup = IsHoldKeyPressed();
        if (shouldShowPopup != isPopupVisible)
            SetPopupVisible(shouldShowPopup, false);

        if (isPopupVisible)
            RefreshPopupData();
    }

    private void OnDisable()
    {
        SetPopupVisible(false, true);
    }

    private void OnDestroy()
    {
        RestoreRuntimeState();
    }

    private void OnValidate()
    {
        ApplyPauseMenuThemeIfEnabled();
        panelSize.x = Mathf.Max(300f, panelSize.x);
        panelSize.y = Mathf.Max(220f, panelSize.y);
        backdropAlpha = Mathf.Clamp01(backdropAlpha);
        textScale = Mathf.Clamp(textScale, 0.75f, 2f);
        ApplyLayoutSettings(false);
    }

    private void ApplyPauseMenuThemeIfEnabled()
    {
        if (!usePauseMenuTheme)
            return;

        panelColor = new Color(0.06f, 0.05f, 0.07f, 1f);
        panelBorderColor = new Color(0.4f, 0.12f, 0.1f, 1f);
        textColor = new Color(0.75f, 0.68f, 0.58f, 1f);
        accentTextColor = new Color(0.92f, 0.85f, 0.65f, 1f);
        buttonColor = new Color(0.10f, 0.08f, 0.09f, 1f);
        selectedButtonColor = new Color(0.74f, 0.34f, 0.12f, 1f);
    }

    private void CacheReferences()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (abilitySlots == null)
            abilitySlots = GetComponent<PlayerAbilitySlots>();

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
    }

    private bool IsHoldKeyPressed()
    {
        KeyCode activeHoldKey = GameKeybinds.Stats;

        try
        {
            return Input.GetKey(activeHoldKey);
        }
        catch (InvalidOperationException)
        {
            // If old Input is disabled, fall back to Input System keys below.
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        if (activeHoldKey == KeyCode.Tab) return keyboard.tabKey.isPressed;
        if (activeHoldKey == KeyCode.Space) return keyboard.spaceKey.isPressed;
        if (activeHoldKey == KeyCode.LeftShift) return keyboard.leftShiftKey.isPressed;
        if (activeHoldKey == KeyCode.RightShift) return keyboard.rightShiftKey.isPressed;
        if (activeHoldKey == KeyCode.LeftControl) return keyboard.leftCtrlKey.isPressed;
        if (activeHoldKey == KeyCode.RightControl) return keyboard.rightCtrlKey.isPressed;
        if (activeHoldKey == KeyCode.E) return keyboard.eKey.isPressed;
        if (activeHoldKey == KeyCode.Q) return keyboard.qKey.isPressed;
#endif

        return false;
    }

    private void BuildPopupUi()
    {
        if (canvasRoot != null)
            return;

        popupTexts.Clear();
        popupTextBaseSizes.Clear();

        EnsureEventSystemExists();

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        canvasRoot = new GameObject("PlayerStatsAbilityPopupCanvas");
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 420;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasRoot.AddComponent<GraphicRaycaster>();

        RectTransform overlayRect = CreateRect("Overlay", canvasRoot.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        backdropImage = overlayRect.gameObject.AddComponent<Image>();
        Color overlay = backdropColor;
        overlay.a = Mathf.Clamp01(backdropAlpha);
        backdropImage.color = overlay;

        RectTransform panelRect = CreateRect("Panel", canvasRoot.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, panelSize);
        panelRectTransform = panelRect;
        panelRoot = panelRect.gameObject;

        Image panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = panelColor;

        Outline panelOutline = panelRoot.AddComponent<Outline>();
        panelOutline.effectColor = panelBorderColor;
        panelOutline.effectDistance = new Vector2(2f, -2f);

        AddHorizontalLine(panelRoot.transform, new Vector2(0f, 236f), panelSize.x - 120f, sectionLineColor);

        CreateText("Title", panelRoot.transform,
            "PLAYER STATUS / ABILITIES", 38, FontStyle.Bold,
            TextAnchor.MiddleCenter, accentTextColor,
            new Vector2(0f, 206f), new Vector2(panelSize.x - 40f, 54f));

        CreateText("Hint", panelRoot.transform,
            "- hold " + holdPopupKey + " to keep this open -", 15, FontStyle.Italic,
            TextAnchor.MiddleCenter, subtitleColor,
            new Vector2(0f, 170f), new Vector2(panelSize.x - 40f, 28f));

        AddHorizontalLine(panelRoot.transform, new Vector2(0f, 152f), panelSize.x - 150f, sectionLineColor);

        RectTransform statsPanelRect = CreateRect("StatsPanel", panelRoot.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-185f, -40f), new Vector2(330f, 300f));
        Image statsPanelImage = statsPanelRect.gameObject.AddComponent<Image>();
        statsPanelImage.color = statsPanelColor;

        Outline statsPanelOutline = statsPanelRect.gameObject.AddComponent<Outline>();
        statsPanelOutline.effectColor = new Color(panelBorderColor.r, panelBorderColor.g, panelBorderColor.b, 0.55f);
        statsPanelOutline.effectDistance = new Vector2(1f, -1f);

        statsText = CreateText("StatsText", statsPanelRect.transform,
            string.Empty, 16, FontStyle.Normal,
            TextAnchor.UpperLeft, textColor,
            Vector2.zero, new Vector2(292f, 258f));
        statsText.verticalOverflow = VerticalWrapMode.Truncate;

        CreateText("SuperHeader", panelRoot.transform,
            "SUPER ABILITY", 20, FontStyle.Bold,
            TextAnchor.MiddleLeft, subtitleColor,
            new Vector2(178f, 112f), new Vector2(300f, 30f));

        AddHorizontalLine(panelRoot.transform, new Vector2(180f, 90f), 320f, sectionLineColor);

        float superStartY = 62f;
        int superIndex = 0;
        foreach (SuperEnhancerSlot slot in Enum.GetValues(typeof(SuperEnhancerSlot)))
        {
            SuperEnhancerSlot capturedSlot = slot;
            float y = superStartY - (superIndex * 42f);
            Button button = CreateButton(
                "Super_" + slot,
                panelRoot.transform,
                PrettyName(slot.ToString()),
                new Vector2(182f, y),
                new Vector2(300f, 34f),
                () => EquipSuper(capturedSlot));
            superButtons[capturedSlot] = button;
            superIndex++;
        }

        CreateText("DashHeader", panelRoot.transform,
            "DASH ENHANCER", 20, FontStyle.Bold,
            TextAnchor.MiddleLeft, subtitleColor,
            new Vector2(178f, -108f), new Vector2(300f, 30f));

        AddHorizontalLine(panelRoot.transform, new Vector2(180f, -130f), 320f, sectionLineColor);

        float dashStartY = -162f;
        int dashIndex = 0;
        foreach (DashEnhancerSlot slot in Enum.GetValues(typeof(DashEnhancerSlot)))
        {
            DashEnhancerSlot capturedSlot = slot;
            float y = dashStartY - (dashIndex * 42f);
            Button button = CreateButton(
                "Dash_" + slot,
                panelRoot.transform,
                PrettyName(slot.ToString()),
                new Vector2(182f, y),
                new Vector2(300f, 34f),
                () => EquipDash(capturedSlot));
            dashButtons[capturedSlot] = button;
            dashIndex++;
        }

        CreateText("FooterHint", panelRoot.transform,
            "Click an ability to equip it immediately", 14, FontStyle.Italic,
            TextAnchor.MiddleCenter, subtitleColor,
            new Vector2(0f, -250f), new Vector2(panelSize.x - 40f, 24f));

        AddHorizontalLine(panelRoot.transform, new Vector2(0f, -272f), panelSize.x - 120f, sectionLineColor);

        ApplyLayoutSettings(true);
    }

    private void ApplyLayoutSettings(bool force)
    {
        Vector2 clampedPanelSize = new Vector2(
            Mathf.Max(300f, panelSize.x),
            Mathf.Max(220f, panelSize.y));
        float clampedBackdropAlpha = Mathf.Clamp01(backdropAlpha);
        float clampedTextScale = Mathf.Clamp(textScale, 0.75f, 2f);

        if (!force
            && clampedPanelSize == appliedPanelSize
            && Mathf.Approximately(clampedBackdropAlpha, appliedBackdropAlpha)
            && Mathf.Approximately(clampedTextScale, appliedTextScale))
        {
            return;
        }

        appliedPanelSize = clampedPanelSize;
        appliedBackdropAlpha = clampedBackdropAlpha;
        appliedTextScale = clampedTextScale;

        if (panelRectTransform != null)
            panelRectTransform.sizeDelta = clampedPanelSize;

        if (backdropImage != null)
        {
            Color overlay = backdropColor;
            overlay.a = clampedBackdropAlpha;
            backdropImage.color = overlay;
        }

        for (int i = popupTexts.Count - 1; i >= 0; i--)
        {
            Text popupText = popupTexts[i];
            if (popupText == null)
            {
                popupTexts.RemoveAt(i);
                popupTextBaseSizes.RemoveAt(i);
                continue;
            }

            int scaledSize = Mathf.Max(1, Mathf.RoundToInt(popupTextBaseSizes[i] * clampedTextScale));
            popupText.fontSize = scaledSize;
        }
    }

    private void EquipSuper(SuperEnhancerSlot slot)
    {
        if (abilitySlots == null)
            return;

        abilitySlots.equippedSuperEnhancer = slot;
        RefreshPopupData();
    }

    private void EquipDash(DashEnhancerSlot slot)
    {
        if (abilitySlots == null)
            return;

        abilitySlots.equippedDashEnhancer = slot;
        RefreshPopupData();
    }

    private void SetPopupVisible(bool visible, bool force)
    {
        if (!force && isPopupVisible == visible)
            return;

        isPopupVisible = visible;

        if (canvasRoot != null)
            canvasRoot.SetActive(visible);

        if (visible)
        {
            if (pauseGameplayWhileOpen && !changedTimeScale && Time.timeScale > 0f)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                changedTimeScale = true;
            }

            if (forceCursorVisibleWhileOpen && !changedCursorState)
            {
                previousCursorVisible = Cursor.visible;
                previousCursorLockMode = Cursor.lockState;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                changedCursorState = true;
            }

            RefreshPopupData();
            return;
        }

        RestoreRuntimeState();
    }

    private void RestoreRuntimeState()
    {
        if (changedTimeScale)
        {
            Time.timeScale = previousTimeScale;
            changedTimeScale = false;
        }

        if (changedCursorState)
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
            changedCursorState = false;
        }
    }

    private void RefreshPopupData()
    {
        CacheReferences();
        UpdateStatsText();
        UpdateAbilityButtonStates();
    }

    private void UpdateStatsText()
    {
        if (statsText == null)
            return;

        if (playerMovement == null || abilitySlots == null)
        {
            statsText.text = "Missing required player references.\nAttach this to the Player object.";
            return;
        }

        float healthCurrent = playerHealth != null ? playerHealth.currentHealth : 0f;
        float healthMax = playerHealth != null ? playerHealth.maxHealth : 0f;

        float baseDamage = playerMovement.baseDashDamage;
        float maxChargeDamage = baseDamage * Mathf.Max(1f, playerMovement.maxChargeDamageMultiplier);

        statsBuilder.Length = 0;
        statsBuilder.AppendLine("HEALTH");
        statsBuilder.Append("  ").Append(healthCurrent.ToString("0.0")).Append(" / ").Append(healthMax.ToString("0.0")).AppendLine();

        statsBuilder.AppendLine("STAMINA");
        statsBuilder.Append("  ").Append(playerMovement.currentStamina.ToString("0.0")).Append(" / ").Append(playerMovement.maxStamina.ToString("0.0")).AppendLine();
        statsBuilder.Append("  Regen: ").Append(playerMovement.staminaRegenRate.ToString("0.0")).Append("/s").AppendLine();

        statsBuilder.AppendLine("DAMAGE");
        statsBuilder.Append("  Base Dash: ").Append(baseDamage.ToString("0.0")).AppendLine();
        statsBuilder.Append("  Max Charged Dash: ").Append(maxChargeDamage.ToString("0.0")).AppendLine();
        statsBuilder.Append("  Current Dash Damage Multiplier: x").Append(playerMovement.currentDashMultiplier.ToString("0.00")).AppendLine();

        statsBuilder.AppendLine("COMBAT");
        statsBuilder.Append("  Combo: ").Append(playerMovement.CurrentComboCount).AppendLine();
        statsBuilder.Append("  Super Meter: ").Append(playerMovement.currentSuperMeter.ToString("0.0")).Append(" / ").Append(playerMovement.maxSuperMeter.ToString("0.0")).AppendLine();

        statsBuilder.AppendLine("EQUIPPED");
        statsBuilder.Append("  Super: ").Append(PrettyName(abilitySlots.equippedSuperEnhancer.ToString())).AppendLine();
        statsBuilder.Append("  Dash: ").Append(PrettyName(abilitySlots.equippedDashEnhancer.ToString())).AppendLine();

        statsText.text = statsBuilder.ToString();
    }

    private void UpdateAbilityButtonStates()
    {
        if (abilitySlots == null)
            return;

        foreach (KeyValuePair<SuperEnhancerSlot, Button> pair in superButtons)
        {
            if (pair.Value == null || pair.Value.image == null)
                continue;

            bool isSelected = pair.Key == abilitySlots.equippedSuperEnhancer;
            bool isHovered = hoveredButtons.Contains(pair.Value);
            bool useActiveHighlight = isSelected || isHovered;
            pair.Value.image.color = useActiveHighlight ? selectedButtonColor : buttonColor;

            Outline outline = pair.Value.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = useActiveHighlight ? selectedButtonBorderColor : buttonBorderColor;

            Text label = pair.Value.GetComponentInChildren<Text>(true);
            if (label != null)
                label.color = useActiveHighlight ? selectedButtonTextColor : buttonTextColor;
        }

        foreach (KeyValuePair<DashEnhancerSlot, Button> pair in dashButtons)
        {
            if (pair.Value == null || pair.Value.image == null)
                continue;

            bool isSelected = pair.Key == abilitySlots.equippedDashEnhancer;
            bool isHovered = hoveredButtons.Contains(pair.Value);
            bool useActiveHighlight = isSelected || isHovered;
            pair.Value.image.color = useActiveHighlight ? selectedButtonColor : buttonColor;

            Outline outline = pair.Value.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = useActiveHighlight ? selectedButtonBorderColor : buttonBorderColor;

            Text label = pair.Value.GetComponentInChildren<Text>(true);
            if (label != null)
                label.color = useActiveHighlight ? selectedButtonTextColor : buttonTextColor;
        }
    }

    private static string PrettyName(string enumName)
    {
        if (string.IsNullOrEmpty(enumName))
            return "Unknown";

        StringBuilder builder = new StringBuilder(enumName.Length + 6);
        for (int i = 0; i < enumName.Length; i++)
        {
            char current = enumName[i];
            if (current == '_')
            {
                builder.Append(' ');
                continue;
            }

            if (i > 0 && char.IsUpper(current) && !char.IsUpper(enumName[i - 1]))
                builder.Append(' ');

            builder.Append(current);
        }

        return builder.ToString();
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, Action onClick)
    {
        RectTransform rect = CreateRect(name, parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), position, size);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = buttonColor;

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = buttonBorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonHoverColor;
        colors.pressedColor = buttonPressedColor;
        colors.selectedColor = selectedButtonColor;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        button.onClick.AddListener(PlayButtonSelectSfx);
        AddButtonHoverSfx(button);

        Text buttonText = CreateText(name + "_Text", rect.transform,
            label, 15, FontStyle.Bold, TextAnchor.MiddleCenter, buttonTextColor,
            Vector2.zero, size - new Vector2(16f, 8f));
        buttonText.resizeTextForBestFit = false;

        return button;
    }

    private void EnsureUiSfxSource()
    {
        if (uiSfxSource != null)
            return;

        uiSfxSource = GetComponent<AudioSource>();
        if (uiSfxSource == null)
            uiSfxSource = gameObject.AddComponent<AudioSource>();

        uiSfxSource.playOnAwake = false;
        uiSfxSource.loop = false;
        uiSfxSource.spatialBlend = 0f;
    }

    private void AddButtonHoverSfx(Button button)
    {
        if (button == null)
            return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        AddEventTriggerListener(trigger, EventTriggerType.PointerEnter, _ =>
        {
            SetButtonHoverState(button, true);
            PlayButtonHighlightSfx(button.gameObject);
        });
        AddEventTriggerListener(trigger, EventTriggerType.Select, _ =>
        {
            SetButtonHoverState(button, true);
            PlayButtonHighlightSfx(button.gameObject);
        });
        AddEventTriggerListener(trigger, EventTriggerType.PointerExit, _ => SetButtonHoverState(button, false));
        AddEventTriggerListener(trigger, EventTriggerType.Deselect, _ => SetButtonHoverState(button, false));
    }

    private void SetButtonHoverState(Button button, bool isHovered)
    {
        if (button == null)
            return;

        if (isHovered)
            hoveredButtons.Add(button);
        else
            hoveredButtons.Remove(button);
    }

    private static void AddEventTriggerListener(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> action)
    {
        if (trigger == null || action == null)
            return;

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(evt => action(evt));
        trigger.triggers.Add(entry);
    }

    private void PlayButtonSelectSfx()
    {
        if (buttonSelectSfx == null)
            return;

        EnsureUiSfxSource();
        if (uiSfxSource == null)
            return;

        uiSfxSource.PlayOneShot(buttonSelectSfx, Mathf.Clamp01(buttonSelectSfxVolume));
    }

    private void PlayButtonHighlightSfx(GameObject highlighted)
    {
        if (buttonHighlightSfx == null)
            return;

        float now = Time.unscaledTime;
        if (highlighted == lastHighlightedButton && now - lastHighlightSfxTime < highlightSfxCooldown)
            return;

        EnsureUiSfxSource();
        if (uiSfxSource == null)
            return;

        uiSfxSource.PlayOneShot(buttonHighlightSfx, Mathf.Clamp01(buttonHighlightSfxVolume));
        lastHighlightedButton = highlighted;
        lastHighlightSfxTime = now;
    }

    private Text CreateText(
        string name,
        Transform parent,
        string message,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            position,
            size);

        Text text = rect.gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.text = message;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RegisterText(text, fontSize);

        return text;
    }

    private void RegisterText(Text text, int baseFontSize)
    {
        if (text == null)
            return;

        popupTexts.Add(text);
        popupTextBaseSizes.Add(Mathf.Max(1, baseFontSize));
    }

    private RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        return rect;
    }

    private void AddHorizontalLine(Transform parent, Vector2 position, float width, Color color)
    {
        RectTransform lineRect = CreateRect(
            "Line",
            parent,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            position,
            new Vector2(width, 2f));

        Image image = lineRect.gameObject.AddComponent<Image>();
        image.color = color;
    }

    private static void EnsureEventSystemExists()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }
}