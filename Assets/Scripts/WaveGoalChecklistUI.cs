using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays wave progression goal in the top-right corner and a boss-available banner.
/// Can use inspector-assigned TMP labels or auto-build a simple UI at runtime.
/// </summary>
[DisallowMultipleComponent]
public class WaveGoalChecklistUI : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI goalText;
    public TextMeshProUGUI bossAvailableText;

    [Header("Boss Available Effects")]
    public bool hideGoalWhenBossAvailable = true;
    public bool flashBossAvailableText = true;
    [Min(0.1f)] public float bossAvailableFlashSpeed = 6f;
    [Range(0f, 1f)] public float bossAvailableMinAlpha = 0.35f;
    [Range(0f, 1f)] public float bossAvailableMaxAlpha = 1f;

    [Header("Auto Build")]
    public bool autoBuildIfMissing = true;
    public int sortingOrder = 260;

    private GameObject autoCanvasRoot;
    private bool isBossAvailableActive;
    private Color bossAvailableBaseColor = Color.white;
    private bool hasBossAvailableBaseColor;

    public static WaveGoalChecklistUI FindInstance()
    {
        return Object.FindFirstObjectByType<WaveGoalChecklistUI>();
    }

    private void Awake()
    {
        EnsureUi();
        CacheBossAvailableBaseColor();
    }

    private void Update()
    {
        UpdateBossAvailableFlash();
    }

    public void Refresh(int clearedWaves, int requiredWaves, bool bossAvailable)
    {
        EnsureUi();
        CacheBossAvailableBaseColor();

        int safeCleared = Mathf.Max(0, clearedWaves);
        int safeRequired = Mathf.Max(1, requiredWaves);
        int clampedProgress = Mathf.Clamp(safeCleared, 0, safeRequired);

        if (goalText != null)
        {
            goalText.text = "GOAL: WAVES " + clampedProgress + " / " + safeRequired;
            goalText.gameObject.SetActive(!(hideGoalWhenBossAvailable && bossAvailable));
        }

        if (bossAvailableText != null)
        {
            bossAvailableText.gameObject.SetActive(bossAvailable);
            if (bossAvailable)
                bossAvailableText.text = "BOSS AVAILABLE";
        }

        isBossAvailableActive = bossAvailable;

        if (!isBossAvailableActive)
            ResetBossAvailableVisual();
    }

    private void CacheBossAvailableBaseColor()
    {
        if (bossAvailableText == null)
            return;

        if (hasBossAvailableBaseColor)
            return;

        bossAvailableBaseColor = bossAvailableText.color;
        hasBossAvailableBaseColor = true;
    }

    private void UpdateBossAvailableFlash()
    {
        if (bossAvailableText == null)
            return;

        if (!isBossAvailableActive || !flashBossAvailableText)
            return;

        CacheBossAvailableBaseColor();

        float minAlpha = Mathf.Clamp01(Mathf.Min(bossAvailableMinAlpha, bossAvailableMaxAlpha));
        float maxAlpha = Mathf.Clamp01(Mathf.Max(bossAvailableMinAlpha, bossAvailableMaxAlpha));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * bossAvailableFlashSpeed);

        Color flashed = hasBossAvailableBaseColor ? bossAvailableBaseColor : bossAvailableText.color;
        flashed.a = Mathf.Lerp(minAlpha, maxAlpha, pulse);
        bossAvailableText.color = flashed;
    }

    private void ResetBossAvailableVisual()
    {
        if (bossAvailableText == null)
            return;

        CacheBossAvailableBaseColor();

        Color reset = hasBossAvailableBaseColor ? bossAvailableBaseColor : bossAvailableText.color;
        bossAvailableText.color = reset;
    }

    private void EnsureUi()
    {
        if (goalText != null && bossAvailableText != null)
            return;

        if (!autoBuildIfMissing)
            return;

        BuildFallbackUi();
    }

    private void BuildFallbackUi()
    {
        if (autoCanvasRoot != null)
            return;

        autoCanvasRoot = new GameObject("WaveGoalCanvas");
        autoCanvasRoot.transform.SetParent(transform, false);

        Canvas canvas = autoCanvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = autoCanvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        autoCanvasRoot.AddComponent<GraphicRaycaster>();

        RectTransform panelRect = CreateRect("WaveGoalPanel", autoCanvasRoot.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-32f, -24f), new Vector2(340f, 90f));

        Image panelImage = panelRect.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.06f, 0.07f, 0.82f);

        Outline panelOutline = panelRect.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.45f, 0.2f, 0.1f, 0.9f);
        panelOutline.effectDistance = new Vector2(1f, -1f);

        goalText = CreateText("GoalText", panelRect.transform,
            "GOAL: WAVES 0 / 1", 28,
            new Color(0.92f, 0.85f, 0.65f, 1f),
            new Vector2(0f, 18f), new Vector2(320f, 34f));

        bossAvailableText = CreateText("BossAvailableText", panelRect.transform,
            "BOSS AVAILABLE", 22,
            new Color(0.95f, 0.45f, 0.2f, 1f),
            new Vector2(0f, -18f), new Vector2(320f, 30f));
        bossAvailableText.gameObject.SetActive(false);
    }

    private static RectTransform CreateRect(
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

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        Color color,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            anchoredPosition, size);

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        return text;
    }
}
