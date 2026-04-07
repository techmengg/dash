using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Souls-like area title popup. Fades in a large stylized title + subtitle,
/// holds, then fades out. Triggered by an external call (e.g., BattleArenaTrigger).
/// </summary>
public class AreaTitlePopup : MonoBehaviour
{
    [Header("Display")]
    public string areaName = "Gluttony's Hideout";
    public string subtitle = "";
    public float fadeInDuration = 0.6f;
    public float holdDuration = 1.2f;
    public float fadeOutDuration = 0.6f;

    [Header("Title Style")]
    public float titleFontSize = 62f;
    public Color titleColor = new Color(0.92f, 0.85f, 0.65f, 1f); // warm gold
    public float titleLetterSpacing = 12f;

    [Header("Subtitle Style")]
    public float subtitleFontSize = 24f;
    public Color subtitleColor = new Color(0.7f, 0.6f, 0.5f, 1f);

    [Header("Separator Line")]
    public Color lineColor = new Color(0.7f, 0.55f, 0.3f, 0.6f);
    public float lineWidth = 300f;
    public float lineHeight = 2f;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private bool hasTriggered = false;

    [Header("Audio")]
    public bool playShowSfx = true;

    /// <summary>
    /// Called when the full fade sequence (in + hold + out) completes.
    /// </summary>
    public System.Action onComplete;

    /// <summary>
    /// Call this to show the area title popup.
    /// </summary>
    public void Show()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        if (playShowSfx)
        {
            BossArenaAudioController bossArenaAudio = BossArenaAudioController.FindInstance();
            if (bossArenaAudio != null)
                bossArenaAudio.PlayTitlePopupSfx();
            else
            {
                RoomAudioController roomAudio = RoomAudioController.FindInstance();
                if (roomAudio != null)
                    roomAudio.PlayTitlePopupSfx();
            }
        }

        BuildUI();
        StartCoroutine(FadeSequence());
    }

    /// <summary>
    /// Convenience: show with a custom area name.
    /// </summary>
    public void Show(string name)
    {
        areaName = name;
        Show();
    }

    private void BuildUI()
    {
        // ── Canvas ──
        GameObject canvasObj = new GameObject("AreaTitleCanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // above everything

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // ── Container (centered on screen, slightly above middle) ──
        GameObject container = new GameObject("TitleContainer");
        container.transform.SetParent(canvasObj.transform, false);
        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = new Vector2(0f, 40f); // slightly above center
        containerRect.sizeDelta = new Vector2(1000f, 200f);

        // ── Top decorative line ──
        BuildLine(container.transform, 35f);

        // ── Main title ──
        GameObject titleObj = new GameObject("AreaTitle");
        titleObj.transform.SetParent(container.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 0f);
        titleRect.sizeDelta = new Vector2(900f, 80f);

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = areaName;
        titleText.fontSize = titleFontSize;
        titleText.color = titleColor;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.characterSpacing = titleLetterSpacing;
        titleText.fontStyle = FontStyles.Normal;
        titleText.textWrappingMode = TextWrappingModes.NoWrap;

        // Load TMP font from Resources
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null)
            titleText.font = font;

        // ── Bottom decorative line ──
        BuildLine(container.transform, -35f);

        // ── Subtitle (if provided) ──
        if (!string.IsNullOrEmpty(subtitle))
        {
            GameObject subObj = new GameObject("AreaSubtitle");
            subObj.transform.SetParent(container.transform, false);
            RectTransform subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.5f);
            subRect.anchorMax = new Vector2(0.5f, 0.5f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.anchoredPosition = new Vector2(0f, -55f);
            subRect.sizeDelta = new Vector2(900f, 40f);

            TextMeshProUGUI subText = subObj.AddComponent<TextMeshProUGUI>();
            subText.text = subtitle;
            subText.fontSize = subtitleFontSize;
            subText.color = subtitleColor;
            subText.alignment = TextAlignmentOptions.Center;
            subText.characterSpacing = 6f;
            if (font != null) subText.font = font;
        }

        // ── Side ornaments — small diamond shapes flanking the title ──
        BuildOrnament(container.transform, -lineWidth / 2f - 20f, 0f);
        BuildOrnament(container.transform, lineWidth / 2f + 20f, 0f);
    }

    private void BuildLine(Transform parent, float yOffset)
    {
        // Main line
        GameObject lineObj = new GameObject("DecorLine");
        lineObj.transform.SetParent(parent, false);
        RectTransform lineRect = lineObj.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = new Vector2(0f, yOffset);
        lineRect.sizeDelta = new Vector2(lineWidth, lineHeight);

        Image lineImg = lineObj.AddComponent<Image>();
        lineImg.color = lineColor;

        // Fade-out extensions on each side (gradient feel via narrower, dimmer bars)
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject ext = new GameObject("LineExt");
            ext.transform.SetParent(parent, false);
            RectTransform extRect = ext.AddComponent<RectTransform>();
            extRect.anchorMin = new Vector2(0.5f, 0.5f);
            extRect.anchorMax = new Vector2(0.5f, 0.5f);
            extRect.pivot = new Vector2(0.5f, 0.5f);
            extRect.anchoredPosition = new Vector2(side * (lineWidth / 2f + 40f), yOffset);
            extRect.sizeDelta = new Vector2(80f, lineHeight * 0.6f);

            Image extImg = ext.AddComponent<Image>();
            extImg.color = new Color(lineColor.r, lineColor.g, lineColor.b, lineColor.a * 0.3f);
        }

        // Center dot on the line
        GameObject dot = new GameObject("LineDot");
        dot.transform.SetParent(parent, false);
        RectTransform dotRect = dot.AddComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = new Vector2(0f, yOffset);
        dotRect.sizeDelta = new Vector2(6f, 6f);

        Image dotImg = dot.AddComponent<Image>();
        dotImg.color = new Color(lineColor.r * 1.3f, lineColor.g * 1.2f, lineColor.b * 1.1f, lineColor.a);
    }

    private void BuildOrnament(Transform parent, float xOffset, float yOffset)
    {
        GameObject orn = new GameObject("Ornament");
        orn.transform.SetParent(parent, false);
        RectTransform ornRect = orn.AddComponent<RectTransform>();
        ornRect.anchorMin = new Vector2(0.5f, 0.5f);
        ornRect.anchorMax = new Vector2(0.5f, 0.5f);
        ornRect.pivot = new Vector2(0.5f, 0.5f);
        ornRect.anchoredPosition = new Vector2(xOffset, yOffset);
        ornRect.sizeDelta = new Vector2(8f, 8f);
        ornRect.localRotation = Quaternion.Euler(0, 0, 45f);

        Image ornImg = orn.AddComponent<Image>();
        ornImg.color = new Color(titleColor.r, titleColor.g, titleColor.b, 0.5f);
    }

    private IEnumerator FadeSequence()
    {
        // ── Fade in ──
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;
            // Ease-out curve for smooth entrance
            float alpha = t * t * (3f - 2f * t);
            canvasGroup.alpha = alpha;
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // ── Hold ──
        yield return new WaitForSeconds(holdDuration);

        // ── Fade out ──
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;
            // Ease-in curve for gentle exit
            canvasGroup.alpha = 1f - (t * t);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        // Cleanup
        if (canvas != null)
            Destroy(canvas.gameObject);

        onComplete?.Invoke();
    }
}
