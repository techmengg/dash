using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
public class PauseMenuUI : MonoBehaviour
{
    [Header("Pause")]
    public KeyCode pauseKey = KeyCode.Escape;
    public int sortingOrder = 900;
    [Range(0f, 1f)] public float backdropAlpha = 0.88f;
    public bool forceCursorVisibleWhilePaused = true;

    [Header("Settings Defaults")]
    [Range(0f, 1f)] public float defaultMasterVolume = 1f;

    [Header("Scene Routing")]
    public string mainMenuSceneName = "Menu";

    [Header("Menu UI SFX")]
    public AudioClip buttonSelectSfx;
    [Range(0f, 1f)] public float buttonSelectSfxVolume = 1f;
    public AudioClip buttonHighlightSfx;
    [Range(0f, 1f)] public float buttonHighlightSfxVolume = 0.85f;
    [Min(0f)] public float highlightSfxCooldown = 0.05f;

    // ── Theme (matches CardSelectionUI / dungeon aesthetic) ──
    private static readonly Color bgOverlay       = new Color(0.02f, 0.02f, 0.03f, 0.92f);
    private static readonly Color panelBg         = new Color(0.06f, 0.05f, 0.07f, 1f);
    private static readonly Color panelBorder     = new Color(0.4f, 0.12f, 0.1f, 1f);
    private static readonly Color titleGold       = new Color(0.92f, 0.85f, 0.65f, 1f);
    private static readonly Color subtitleColor   = new Color(0.6f, 0.55f, 0.5f, 1f);
    private static readonly Color labelColor      = new Color(0.75f, 0.68f, 0.58f, 1f);
    private static readonly Color valueColor      = new Color(0.9f, 0.82f, 0.6f, 1f);
    private static readonly Color btnNormal       = new Color(0.10f, 0.08f, 0.09f, 1f);
    private static readonly Color btnHover        = new Color(0.18f, 0.12f, 0.10f, 1f);
    private static readonly Color btnPressed      = new Color(0.06f, 0.04f, 0.05f, 1f);
    private static readonly Color btnText         = new Color(0.88f, 0.78f, 0.58f, 1f);
    private static readonly Color btnBorder       = new Color(0.4f, 0.12f, 0.1f, 0.7f);
    private static readonly Color accentLine      = new Color(0.7f, 0.55f, 0.3f, 0.25f);
    private static readonly Color sliderTrack     = new Color(0.10f, 0.08f, 0.09f, 1f);
    private static readonly Color sliderFill      = new Color(0.65f, 0.35f, 0.15f, 1f);
    private static readonly Color sliderHandle    = new Color(0.92f, 0.82f, 0.6f, 1f);
    private static readonly Color toggleActiveBg  = new Color(0.55f, 0.25f, 0.12f, 1f);

    private GameObject canvasRoot;
    private GameObject mainPanel;
    private GameObject settingsPanel;
    private RectTransform settingsPanelRect;
    private Image backdropImage;
    private Slider volumeSlider;
    private Text volumeValueLabel;
    private Button dashModeButton;
    private Image dashModeButtonImage;
    private Text dashModeButtonLabel;
    private Text keybindHintLabel;

    private class KeybindRow
    {
        public string actionName;
        public Func<KeyCode> getter;
        public Action<KeyCode> setter;
        public Button button;
        public Text keyLabel;
    }

    private readonly List<KeybindRow> keybindRows = new List<KeybindRow>();
    private bool isAwaitingRebind;
    private KeybindRow activeRebindRow;
    private int rebindStartFrame = -1;

    private Font uiFont;
    private bool isPaused;
    private bool initializedVolume;
    private float prePauseTimeScale = 1f;
    private bool changedCursorState;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private AudioSource uiSfxSource;
    private float lastHighlightSfxTime = -10f;
    private GameObject lastHighlightedButton;
    private bool showingFullscreenSettingsFromMainMenu;
    private bool hasCachedSettingsPanelLayout;
    private Vector2 settingsPanelAnchorMin;
    private Vector2 settingsPanelAnchorMax;
    private Vector2 settingsPanelPivot;
    private Vector2 settingsPanelAnchoredPosition;
    private Vector2 settingsPanelSizeDelta;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<PauseMenuUI>() != null)
            return;

        GameObject root = new GameObject("PauseMenuUI");
        root.AddComponent<PauseMenuUI>();
    }

    private void Awake()
    {
        GameKeybinds.EnsureInitialized(pauseKey);
        EnsureUiSfxSource();
        BuildUi();
        EnsureEventSystemExists();
        ApplyInitialVolume();
        SetMenuVisible(false);
    }

    private void Update()
    {
        if (isAwaitingRebind)
        {
            UpdateRebindCapture();
            return;
        }

        if (!WasPausePressedThisFrame())
            return;

        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    private void OnDisable()
    {
        if (isPaused)
            ResumeGame();
    }

    private void OnDestroy()
    {
        CancelRebind(false);

        if (isPaused)
            ResumeGame();
    }

    private bool WasPausePressedThisFrame()
    {
        KeyCode activePauseKey = GameKeybinds.Pause;

        try
        {
            if (Input.GetKeyDown(activePauseKey))
                return true;
        }
        catch (InvalidOperationException) { }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        if (activePauseKey == KeyCode.Escape) return keyboard.escapeKey.wasPressedThisFrame;
        if (activePauseKey == KeyCode.P) return keyboard.pKey.wasPressedThisFrame;
        if (activePauseKey == KeyCode.Tab) return keyboard.tabKey.wasPressedThisFrame;
#endif

        return false;
    }

    // ─────────────────────── Game State ───────────────────────

    private void PauseGame()
    {
        if (isPaused)
            return;

        if (Time.timeScale <= 0.0001f)
            return;

        prePauseTimeScale = Mathf.Max(0.0001f, Time.timeScale);
        Time.timeScale = 0f;
        isPaused = true;

        ShowMainPanel();
        SetMenuVisible(true);
        ApplyPausedCursorState();
    }

    private void ResumeGame()
    {
        if (!isPaused)
            return;

        Time.timeScale = Mathf.Max(0.0001f, prePauseTimeScale);
        isPaused = false;
        SetMenuVisible(false);
        RestoreCursorState();
    }

    private void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        isPaused = false;
        RestoreCursorState();

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
            SceneManager.LoadScene(activeScene.buildIndex);
        else
            SceneManager.LoadScene(activeScene.name);
    }

    private void ExitGame()
    {
        Time.timeScale = 1f;
        isPaused = false;
        RestoreCursorState();

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        SceneManager.LoadScene(0);
    }

    private void OpenSettingsPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        RefreshKeybindLabels();
    }

    private void ShowMainPanel()
    {
        CancelRebind(true);

        if (showingFullscreenSettingsFromMainMenu)
        {
            showingFullscreenSettingsFromMainMenu = false;
            ApplySettingsPanelLayout(false);
            SetMenuVisible(false);
            RestoreCursorState();
            return;
        }

        if (mainPanel != null)
            mainPanel.SetActive(true);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void SetMenuVisible(bool visible)
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(visible);
    }

    private void ApplyPausedCursorState()
    {
        if (!forceCursorVisibleWhilePaused || changedCursorState)
            return;

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        changedCursorState = true;
    }

    private void RestoreCursorState()
    {
        if (!changedCursorState)
            return;

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        changedCursorState = false;
    }

    // ─────────────────────── Settings Callbacks ───────────────────────

    private void ApplyInitialVolume()
    {
        if (initializedVolume)
            return;

        float initialVolume = Mathf.Clamp01(defaultMasterVolume);
        AudioListener.volume = initialVolume;

        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(initialVolume);

        UpdateVolumeLabel(initialVolume);
        initializedVolume = true;
    }

    private void OnVolumeChanged(float value)
    {
        float safeValue = Mathf.Clamp01(value);
        AudioListener.volume = safeValue;
        UpdateVolumeLabel(safeValue);
    }

    private void UpdateVolumeLabel(float value)
    {
        if (volumeValueLabel != null)
            volumeValueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void OnDashModeToggle()
    {
        PlayerMovement.useWASDDash = !PlayerMovement.useWASDDash;
        UpdateDashModeVisual();
    }

    private void UpdateDashModeVisual()
    {
        bool wasd = PlayerMovement.useWASDDash;
        string label = wasd ? "WASD + Space" : "Mouse Aim";

        if (dashModeButtonLabel != null)
            dashModeButtonLabel.text = label;

        if (dashModeButtonImage != null)
            dashModeButtonImage.color = wasd ? toggleActiveBg : btnNormal;
    }

    // ─────────────────────── Build UI ───────────────────────

    private void BuildUi()
    {
        if (canvasRoot != null)
            return;

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        canvasRoot = new GameObject("PauseMenuCanvas");
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasRoot.AddComponent<GraphicRaycaster>();

        // Full-screen backdrop
        RectTransform overlayRect = CreateRect("PauseOverlay", canvasRoot.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        backdropImage = overlayRect.gameObject.AddComponent<Image>();
        backdropImage.color = bgOverlay;

        mainPanel = BuildMainPanel(canvasRoot.transform);
        settingsPanel = BuildSettingsPanel(canvasRoot.transform);
        settingsPanel.SetActive(false);
    }

    // ─────────────────────── Main Panel ───────────────────────

    private GameObject BuildMainPanel(Transform parent)
    {
        Vector2 panelSize = new Vector2(420f, 440f);
        RectTransform panelRect = CreateRect("MainPanel", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, panelSize);
        GameObject panel = panelRect.gameObject;

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelBg;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = panelBorder;
        outline.effectDistance = new Vector2(2f, -2f);

        // Top ornament line
        AddHorizontalLine(panel.transform, new Vector2(0f, 185f), 320f, accentLine);

        // Title
        CreateText("PauseTitle", panel.transform,
            "PAUSED", 42, FontStyle.Bold,
            TextAnchor.MiddleCenter, titleGold,
            new Vector2(0f, 155f), new Vector2(300f, 60f));

        // Subtitle
        CreateText("PauseHint", panel.transform,
            "- press esc to resume -", 15, FontStyle.Italic,
            TextAnchor.MiddleCenter, subtitleColor,
            new Vector2(0f, 118f), new Vector2(320f, 24f));

        // Separator
        AddHorizontalLine(panel.transform, new Vector2(0f, 98f), 280f, accentLine);

        // Buttons
        float btnY = 56f;
        float btnSpacing = 58f;
        Vector2 btnSize = new Vector2(280f, 48f);

        CreateStyledButton("ResumeButton", panel.transform, "Resume", new Vector2(0f, btnY), btnSize, ResumeGame);
        CreateStyledButton("RestartButton", panel.transform, "Restart", new Vector2(0f, btnY - btnSpacing), btnSize, RestartCurrentScene);
        CreateStyledButton("SettingsButton", panel.transform, "Settings", new Vector2(0f, btnY - btnSpacing * 2), btnSize, OpenSettingsPanel);
        CreateStyledButton("ExitButton", panel.transform, "Exit To Menu", new Vector2(0f, btnY - btnSpacing * 3), btnSize, ExitGame);

        // Bottom ornament line
        AddHorizontalLine(panel.transform, new Vector2(0f, -195f), 320f, accentLine);

        return panel;
    }

    // ─────────────────────── Settings Panel ───────────────────────

    private GameObject BuildSettingsPanel(Transform parent)
    {
        Vector2 panelSize = new Vector2(520f, 820f);
        RectTransform panelRect = CreateRect("SettingsPanel", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, panelSize);
        settingsPanelRect = panelRect;
        GameObject panel = panelRect.gameObject;

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelBg;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = panelBorder;
        outline.effectDistance = new Vector2(2f, -2f);

        float y = 378f; // start from top

        // ── Header ──
        AddHorizontalLine(panel.transform, new Vector2(0f, y), 400f, accentLine);
        y -= 30f;

        CreateText("SettingsTitle", panel.transform,
            "SETTINGS", 42, FontStyle.Bold,
            TextAnchor.MiddleCenter, titleGold,
            new Vector2(0f, y), new Vector2(400f, 54f));
        y -= 38f;

        AddHorizontalLine(panel.transform, new Vector2(0f, y), 400f, accentLine);
        y -= 28f;

        // ── Section: Audio ──
        CreateText("AudioSection", panel.transform,
            "AUDIO", 16, FontStyle.Bold,
            TextAnchor.MiddleCenter, subtitleColor,
            new Vector2(0f, y), new Vector2(400f, 24f));
        y -= 34f;

        CreateText("VolumeLabel", panel.transform,
            "Master Volume", 22, FontStyle.Normal,
            TextAnchor.MiddleLeft, labelColor,
            new Vector2(-110f, y), new Vector2(220f, 30f));

        volumeValueLabel = CreateText("VolumeValue", panel.transform,
            "100%", 22, FontStyle.Bold,
            TextAnchor.MiddleRight, valueColor,
            new Vector2(110f, y), new Vector2(100f, 30f));
        y -= 38f;

        RectTransform sliderRoot = CreateRect("VolumeSliderRoot", panel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(380f, 24f));
        volumeSlider = BuildSlider(sliderRoot);
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        y -= 38f;

        // ── Separator ──
        AddHorizontalLine(panel.transform, new Vector2(0f, y), 360f, accentLine);
        y -= 28f;

        // ── Section: Controls ──
        CreateText("ControlsSection", panel.transform,
            "CONTROLS", 16, FontStyle.Bold,
            TextAnchor.MiddleCenter, subtitleColor,
            new Vector2(0f, y), new Vector2(400f, 24f));
        y -= 34f;

        CreateText("DashModeLabel", panel.transform,
            "Dash Direction", 22, FontStyle.Normal,
            TextAnchor.MiddleLeft, labelColor,
            new Vector2(-110f, y), new Vector2(220f, 30f));

        dashModeButton = CreateStyledButton("DashModeButton", panel.transform,
            PlayerMovement.useWASDDash ? "WASD + Space" : "Mouse Aim",
            new Vector2(100f, y), new Vector2(180f, 36f), OnDashModeToggle);

        dashModeButtonImage = dashModeButton.GetComponent<Image>();
        dashModeButtonLabel = dashModeButton.GetComponentInChildren<Text>();
        UpdateDashModeVisual();
        y -= 42f;

        // ── Separator ──
        AddHorizontalLine(panel.transform, new Vector2(0f, y), 360f, accentLine);
        y -= 28f;

        // ── Section: Keybinds ──
        CreateText("KeybindsSection", panel.transform,
            "KEYBINDS", 16, FontStyle.Bold,
            TextAnchor.MiddleCenter, subtitleColor,
            new Vector2(0f, y), new Vector2(400f, 24f));
        y -= 28f;

        string[][] binds = new string[][]
        {
            new string[] { "WASD", "Move (fixed)" },
            new string[] { "Esc", "Pause (fixed)" },
        };

        float rowH = 30f;
        float keyBoxW = 80f;
        float rowTotalW = 380f;
        float keyBoxX = -rowTotalW / 2f + keyBoxW / 2f + 10f;  // left side
        float descX = keyBoxX + keyBoxW / 2f + 16f;             // right of key box
        float descW = rowTotalW - keyBoxW - 36f;

        for (int i = 0; i < binds.Length; i++)
        {
            // Alternating row background for readability
            if (i % 2 == 0)
            {
                RectTransform rowBg = CreateRect("KeyRowBg_" + i, panel.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(rowTotalW, rowH));
                Image rowBgImg = rowBg.gameObject.AddComponent<Image>();
                rowBgImg.color = new Color(0.08f, 0.06f, 0.07f, 0.6f);
            }

            // Key badge
            RectTransform keyBadge = CreateRect("KeyBadge_" + i, panel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(keyBoxX, y), new Vector2(keyBoxW, rowH - 4f));
            Image badgeImg = keyBadge.gameObject.AddComponent<Image>();
            badgeImg.color = new Color(0.12f, 0.09f, 0.08f, 1f);

            Outline badgeOutline = keyBadge.gameObject.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(0.4f, 0.25f, 0.15f, 0.5f);
            badgeOutline.effectDistance = new Vector2(1f, -1f);

            // Key text inside badge
            CreateText("Key_" + binds[i][0], keyBadge.transform,
                binds[i][0], 16, FontStyle.Bold,
                TextAnchor.MiddleCenter, valueColor,
                Vector2.zero, new Vector2(keyBoxW - 8f, rowH - 4f));

            // Description text
            CreateText("Desc_" + binds[i][0], panel.transform,
                binds[i][1], 17, FontStyle.Normal,
                TextAnchor.MiddleLeft, labelColor,
                new Vector2(descX + descW / 2f, y), new Vector2(descW, rowH));

            y -= rowH;
        }

        KeybindRow[] editableRows = new KeybindRow[]
        {
            CreateKeybindRow("Dash", () => GameKeybinds.Dash, key => GameKeybinds.Dash = key),
            CreateKeybindRow("Ability", () => GameKeybinds.Ability, key => GameKeybinds.Ability = key),
            CreateKeybindRow("Super", () => GameKeybinds.Super, key => GameKeybinds.Super = key),
            CreateKeybindRow("Interact", () => GameKeybinds.Interact, key => GameKeybinds.Interact = key),
            CreateKeybindRow("View Stats", () => GameKeybinds.Stats, key => GameKeybinds.Stats = key),
            CreateKeybindRow("Minimap", () => GameKeybinds.Minimap, key => GameKeybinds.Minimap = key),
        };

        for (int i = 0; i < editableRows.Length; i++)
        {
            if (i % 2 == 0)
            {
                RectTransform rowBg = CreateRect("EditableKeyRowBg_" + i, panel.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(rowTotalW, rowH));
                Image rowBgImg = rowBg.gameObject.AddComponent<Image>();
                rowBgImg.color = new Color(0.08f, 0.06f, 0.07f, 0.6f);
            }

            KeybindRow row = editableRows[i];

            // Clickable key badge
            row.button = CreateStyledButton("Rebind_" + row.actionName, panel.transform,
                "", new Vector2(keyBoxX, y), new Vector2(keyBoxW, rowH - 4f),
                () => BeginRebind(row));
            row.keyLabel = row.button.GetComponentInChildren<Text>();

            if (row.keyLabel != null)
            {
                row.keyLabel.fontSize = 16;
                row.keyLabel.fontStyle = FontStyle.Bold;
            }

            // Description text
            CreateText("DescRebind_" + row.actionName, panel.transform,
                row.actionName, 17, FontStyle.Normal,
                TextAnchor.MiddleLeft, labelColor,
                new Vector2(descX + descW / 2f, y), new Vector2(descW, rowH));

            keybindRows.Add(row);
            y -= rowH;
        }

        y -= 8f;
        keybindHintLabel = CreateText("KeybindHint", panel.transform,
            "Click a key badge to rebind. Press Esc or click outside to cancel.",
            14, FontStyle.Italic, TextAnchor.MiddleCenter, subtitleColor,
            new Vector2(0f, y), new Vector2(420f, 26f));

        RefreshKeybindLabels();

        y -= 14f;

        // ── Separator ──
        AddHorizontalLine(panel.transform, new Vector2(0f, y), 360f, accentLine);
        y -= 14f;

        // ── Back Button ──
        CreateStyledButton("BackButton", panel.transform, "Back",
            new Vector2(0f, y - 16f), new Vector2(220f, 48f), ShowMainPanel);

        // Bottom ornament
        AddHorizontalLine(panel.transform, new Vector2(0f, y - 56f), 400f, accentLine);

        return panel;
    }

    public void OpenSettingsFullscreenFromMainMenu()
    {
        BuildUi();
        CancelRebind(true);

        Time.timeScale = 1f;
        isPaused = false;
        ApplyPausedCursorState();

        showingFullscreenSettingsFromMainMenu = true;
        SetMenuVisible(true);
        ApplySettingsPanelLayout(true);

        if (mainPanel != null)
            mainPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        RefreshKeybindLabels();
    }

    private void ApplySettingsPanelLayout(bool fullscreen)
    {
        if (settingsPanelRect == null)
            return;

        if (!hasCachedSettingsPanelLayout)
        {
            settingsPanelAnchorMin = settingsPanelRect.anchorMin;
            settingsPanelAnchorMax = settingsPanelRect.anchorMax;
            settingsPanelPivot = settingsPanelRect.pivot;
            settingsPanelAnchoredPosition = settingsPanelRect.anchoredPosition;
            settingsPanelSizeDelta = settingsPanelRect.sizeDelta;
            hasCachedSettingsPanelLayout = true;
        }

        if (fullscreen)
        {
            settingsPanelRect.anchorMin = Vector2.zero;
            settingsPanelRect.anchorMax = Vector2.one;
            settingsPanelRect.pivot = new Vector2(0.5f, 0.5f);
            settingsPanelRect.anchoredPosition = Vector2.zero;
            settingsPanelRect.sizeDelta = Vector2.zero;

            if (backdropImage != null)
                backdropImage.color = new Color(bgOverlay.r, bgOverlay.g, bgOverlay.b, 1f);
        }
        else
        {
            settingsPanelRect.anchorMin = settingsPanelAnchorMin;
            settingsPanelRect.anchorMax = settingsPanelAnchorMax;
            settingsPanelRect.pivot = settingsPanelPivot;
            settingsPanelRect.anchoredPosition = settingsPanelAnchoredPosition;
            settingsPanelRect.sizeDelta = settingsPanelSizeDelta;

            if (backdropImage != null)
                backdropImage.color = bgOverlay;
        }
    }

    private KeybindRow CreateKeybindRow(string actionName, Func<KeyCode> getter, Action<KeyCode> setter)
    {
        return new KeybindRow
        {
            actionName = actionName,
            getter = getter,
            setter = setter
        };
    }

    private void BeginRebind(KeybindRow row)
    {
        if (row == null)
            return;

        activeRebindRow = row;
        isAwaitingRebind = true;
        rebindStartFrame = Time.frameCount;

        if (row.keyLabel != null)
            row.keyLabel.text = "...";

        if (keybindHintLabel != null)
            keybindHintLabel.text = "Press any key for " + row.actionName + " (Esc/click outside to cancel)";
    }

    private void UpdateRebindCapture()
    {
        if (!isAwaitingRebind)
            return;

        // Allow immediate cancel while rebinding.
        if (IsKeyDown(KeyCode.Escape))
        {
            CancelRebind(true);
            return;
        }

        // Clicking elsewhere cancels (after the frame that started rebinding).
        if (Time.frameCount > rebindStartFrame && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)))
        {
            CancelRebind(true);
            return;
        }

        if (TryGetPressedKeyboardKey(out KeyCode pressed))
        {
            ApplyRebind(pressed);
        }
    }

    private void ApplyRebind(KeyCode newKey)
    {
        if (activeRebindRow == null)
            return;

        if (newKey == KeyCode.None)
            return;

        // Keep pause cancellation behavior predictable.
        if (activeRebindRow.actionName != "Pause" && newKey == KeyCode.Escape)
            return;

        activeRebindRow.setter?.Invoke(newKey);
        GameKeybinds.ApplyToLiveObjects();

        CancelRebind(false);
        RefreshKeybindLabels();
    }

    private void CancelRebind(bool cancelled)
    {
        if (!isAwaitingRebind)
            return;

        isAwaitingRebind = false;
        activeRebindRow = null;
        rebindStartFrame = -1;

        RefreshKeybindLabels();

        if (keybindHintLabel != null)
        {
            keybindHintLabel.text = cancelled
                ? "Rebind cancelled. Click a key badge to try again."
                : "Click a key badge to rebind. Press Esc or click outside to cancel.";
        }
    }

    private void RefreshKeybindLabels()
    {
        for (int i = 0; i < keybindRows.Count; i++)
        {
            KeybindRow row = keybindRows[i];
            if (row == null || row.keyLabel == null || row.getter == null)
                continue;

            row.keyLabel.text = GameKeybinds.ToDisplayString(row.getter());
        }
    }

    private bool TryGetPressedKeyboardKey(out KeyCode pressed)
    {
        pressed = KeyCode.None;

        Array values = Enum.GetValues(typeof(KeyCode));
        for (int i = 0; i < values.Length; i++)
        {
            KeyCode key = (KeyCode)values.GetValue(i);
            if (key == KeyCode.None)
                continue;

            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
                continue;

            if (!IsKeyDown(key))
                continue;

            pressed = key;
            return true;
        }

        return false;
    }

    private bool IsKeyDown(KeyCode key)
    {
        try
        {
            return Input.GetKeyDown(key);
        }
        catch (InvalidOperationException)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            switch (key)
            {
                case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.Tab: return keyboard.tabKey.wasPressedThisFrame;
                case KeyCode.Escape: return keyboard.escapeKey.wasPressedThisFrame;
                case KeyCode.LeftShift: return keyboard.leftShiftKey.wasPressedThisFrame;
                case KeyCode.RightShift: return keyboard.rightShiftKey.wasPressedThisFrame;
                case KeyCode.LeftControl: return keyboard.leftCtrlKey.wasPressedThisFrame;
                case KeyCode.RightControl: return keyboard.rightCtrlKey.wasPressedThisFrame;
                case KeyCode.E: return keyboard.eKey.wasPressedThisFrame;
                case KeyCode.F: return keyboard.fKey.wasPressedThisFrame;
                case KeyCode.M: return keyboard.mKey.wasPressedThisFrame;
                case KeyCode.P: return keyboard.pKey.wasPressedThisFrame;
                case KeyCode.Q: return keyboard.qKey.wasPressedThisFrame;
                case KeyCode.R: return keyboard.rKey.wasPressedThisFrame;
                case KeyCode.Alpha1: return keyboard.digit1Key.wasPressedThisFrame;
                case KeyCode.Alpha2: return keyboard.digit2Key.wasPressedThisFrame;
                case KeyCode.Alpha3: return keyboard.digit3Key.wasPressedThisFrame;
                case KeyCode.Alpha4: return keyboard.digit4Key.wasPressedThisFrame;
                default: return false;
            }
#else
            return false;
#endif
        }
    }

    // ─────────────────────── Slider ───────────────────────

    private Slider BuildSlider(RectTransform root)
    {
        Image bg = root.gameObject.AddComponent<Image>();
        bg.color = sliderTrack;

        // Rounded look via outline
        Outline trackOutline = root.gameObject.AddComponent<Outline>();
        trackOutline.effectColor = new Color(0.25f, 0.15f, 0.1f, 0.4f);
        trackOutline.effectDistance = new Vector2(1f, -1f);

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = Mathf.Clamp01(defaultMasterVolume);

        RectTransform fillArea = CreateRect("FillArea", root,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        fillArea.offsetMin = new Vector2(6f, 5f);
        fillArea.offsetMax = new Vector2(-6f, -5f);

        RectTransform fill = CreateRect("Fill", fillArea,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = sliderFill;

        RectTransform handleArea = CreateRect("HandleArea", root,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        handleArea.offsetMin = new Vector2(6f, 0f);
        handleArea.offsetMax = new Vector2(-6f, 0f);

        RectTransform handle = CreateRect("Handle", handleArea,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 28f));
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = sliderHandle;

        Outline handleOutline = handle.gameObject.AddComponent<Outline>();
        handleOutline.effectColor = new Color(0.3f, 0.15f, 0.08f, 0.6f);
        handleOutline.effectDistance = new Vector2(1f, -1f);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;

        return slider;
    }

    // ─────────────────────── UI Primitives ───────────────────────

    private Button CreateStyledButton(string name, Transform parent, string label, Vector2 position, Vector2 size, Action onClick)
    {
        RectTransform rect = CreateRect(name, parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), position, size);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = btnNormal;

        Outline btnOutline = rect.gameObject.AddComponent<Outline>();
        btnOutline.effectColor = btnBorder;
        btnOutline.effectDistance = new Vector2(1f, -1f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(
            btnHover.r / Mathf.Max(0.01f, btnNormal.r),
            btnHover.g / Mathf.Max(0.01f, btnNormal.g),
            btnHover.b / Mathf.Max(0.01f, btnNormal.b), 1f);
        colors.pressedColor = new Color(
            btnPressed.r / Mathf.Max(0.01f, btnNormal.r),
            btnPressed.g / Mathf.Max(0.01f, btnNormal.g),
            btnPressed.b / Mathf.Max(0.01f, btnNormal.b), 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        button.onClick.AddListener(PlayButtonSelectSfx);
        AddButtonHoverSfx(button);

        CreateText(name + "Label", rect.transform,
            label, 20, FontStyle.Bold,
            TextAnchor.MiddleCenter, btnText,
            Vector2.zero, size - new Vector2(12f, 4f));

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

        AddEventTriggerListener(trigger, EventTriggerType.PointerEnter, _ => PlayButtonHighlightSfx(button.gameObject));
        AddEventTriggerListener(trigger, EventTriggerType.Select, _ => PlayButtonHighlightSfx(button.gameObject));
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

    private void AddHorizontalLine(Transform parent, Vector2 position, float width, Color color)
    {
        RectTransform lineRect = CreateRect("Line", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), position, new Vector2(width, 2f));
        Image lineImage = lineRect.gameObject.AddComponent<Image>();
        lineImage.color = color;
    }

    private Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), position, size);

        Text text = rect.gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = value;

        return text;
    }

    private RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        return rect;
    }

    private static void EnsureEventSystemExists()
    {
        if (EventSystem.current != null)
            return;

        GameObject system = new GameObject("EventSystem");
        system.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        system.AddComponent<InputSystemUIInputModule>();
#else
        system.AddComponent<StandaloneInputModule>();
#endif
    }
}
