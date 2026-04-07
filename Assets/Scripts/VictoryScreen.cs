using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Souls-like victory screen: fades to black, shows "GLUTTONY VANQUISHED" with subtitle,
/// then fades back to gameplay. Mirrors DeathScreen architecture.
/// </summary>
public class VictoryScreen : MonoBehaviour
{
    [Header("Timing")]
    public float fadeToBlackDuration = 2.0f;
    public float textFadeInDuration = 1.5f;
    public float textHoldDuration = 3.0f;
    public float textFadeOutDuration = 1.0f;
    public float blackHoldDuration = 0.5f;
    public float fadeFromBlackDuration = 1.5f;

    [Header("Title Style")]
    public string victoryText = "GLUTTONY VANQUISHED";
    public float titleFontSize = 68f;
    public Color titleColor = new Color(0.92f, 0.85f, 0.65f, 1f);
    public float titleLetterSpacing = 14f;

    [Header("Subtitle")]
    public string subtitleText = "The sin devoured by its own hunger";
    public float subtitleFontSize = 22f;
    public Color subtitleColor = new Color(0.7f, 0.6f, 0.5f, 1f);

    [Header("Decoration")]
    public Color lineColor = new Color(0.7f, 0.55f, 0.3f, 0.6f);
    public Color diamondColor = new Color(0.92f, 0.85f, 0.65f, 0.5f);

    // Runtime refs
    private Canvas canvas;
    private Image blackOverlay;
    private CanvasGroup textCanvasGroup;

    private static VictoryScreen _instance;

    public static VictoryScreen Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("VictoryScreen");
                _instance = go.AddComponent<VictoryScreen>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    /// <summary>
    /// Plays the full victory screen sequence. Returns total duration.
    /// onComplete is called when screen is fully black (before fade-back).
    /// </summary>
    public float Play(System.Action onComplete = null)
    {
        BuildUI();
        PlayVictoryScreenSfx();
        StartCoroutine(VictorySequence(onComplete));
        return fadeToBlackDuration + textFadeInDuration + textHoldDuration
             + textFadeOutDuration + blackHoldDuration + fadeFromBlackDuration;
    }

    private void PlayVictoryScreenSfx()
    {
        DeathVictoryScreenAudioController controller = DeathVictoryScreenAudioController.FindInstance();
        if (controller != null)
            controller.FadeOutBackgroundMusic();
    }

    private void BuildUI()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);

        // ── Canvas ──
        GameObject canvasObj = new GameObject("VictoryScreenCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        CanvasGroup cg = canvasObj.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // ── Full-screen black overlay ──
        GameObject overlayObj = new GameObject("BlackOverlay");
        overlayObj.transform.SetParent(canvasObj.transform, false);
        RectTransform overlayRect = overlayObj.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        blackOverlay = overlayObj.AddComponent<Image>();
        blackOverlay.color = new Color(0f, 0f, 0f, 0f);

        // ── Text container ──
        GameObject textContainer = new GameObject("VictoryTextContainer");
        textContainer.transform.SetParent(canvasObj.transform, false);
        RectTransform tcRect = textContainer.AddComponent<RectTransform>();
        tcRect.anchorMin = new Vector2(0.5f, 0.5f);
        tcRect.anchorMax = new Vector2(0.5f, 0.5f);
        tcRect.pivot = new Vector2(0.5f, 0.5f);
        tcRect.anchoredPosition = Vector2.zero;
        tcRect.sizeDelta = new Vector2(1200f, 300f);

        textCanvasGroup = textContainer.AddComponent<CanvasGroup>();
        textCanvasGroup.alpha = 0f;

        // ── Main victory text ──
        GameObject titleObj = new GameObject("VictoryTitle");
        titleObj.transform.SetParent(textContainer.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 15f);
        titleRect.sizeDelta = new Vector2(1100f, 80f);

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = victoryText;
        titleTMP.fontSize = titleFontSize;
        titleTMP.color = titleColor;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.characterSpacing = titleLetterSpacing;
        titleTMP.fontStyle = FontStyles.Normal;
        titleTMP.textWrappingMode = TextWrappingModes.NoWrap;
        SetFont(titleTMP);

        // ── Subtitle ──
        GameObject subObj = new GameObject("VictorySubtitle");
        subObj.transform.SetParent(textContainer.transform, false);
        RectTransform subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.5f);
        subRect.anchorMax = new Vector2(0.5f, 0.5f);
        subRect.pivot = new Vector2(0.5f, 0.5f);
        subRect.anchoredPosition = new Vector2(0f, -35f);
        subRect.sizeDelta = new Vector2(800f, 40f);

        TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
        subTMP.text = subtitleText;
        subTMP.fontSize = subtitleFontSize;
        subTMP.color = subtitleColor;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.characterSpacing = 6f;
        subTMP.fontStyle = FontStyles.Italic;
        SetFont(subTMP);

        // ── Decorative lines ──
        BuildVictoryLine(textContainer.transform, 60f);
        BuildVictoryLine(textContainer.transform, -60f);

        // ── Diamond ornaments flanking title ──
        BuildDiamond(textContainer.transform, -420f, 15f);
        BuildDiamond(textContainer.transform, 420f, 15f);
    }

    private void BuildVictoryLine(Transform parent, float yOffset)
    {
        // Main line
        GameObject lineObj = new GameObject("VictoryLine");
        lineObj.transform.SetParent(parent, false);
        RectTransform lineRect = lineObj.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = new Vector2(0f, yOffset);
        lineRect.sizeDelta = new Vector2(400f, 2f);
        lineObj.AddComponent<Image>().color = lineColor;

        // Faded extensions
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject ext = new GameObject("LineExt");
            ext.transform.SetParent(parent, false);
            RectTransform extRect = ext.AddComponent<RectTransform>();
            extRect.anchorMin = new Vector2(0.5f, 0.5f);
            extRect.anchorMax = new Vector2(0.5f, 0.5f);
            extRect.pivot = new Vector2(0.5f, 0.5f);
            extRect.anchoredPosition = new Vector2(side * 260f, yOffset);
            extRect.sizeDelta = new Vector2(120f, 1f);
            ext.AddComponent<Image>().color = new Color(lineColor.r, lineColor.g, lineColor.b, 0.15f);
        }

        // Center diamond on line
        BuildDiamond(parent, 0f, yOffset);
    }

    private void BuildDiamond(Transform parent, float x, float y)
    {
        GameObject diamond = new GameObject("Diamond");
        diamond.transform.SetParent(parent, false);
        RectTransform dRect = diamond.AddComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0.5f, 0.5f);
        dRect.anchorMax = new Vector2(0.5f, 0.5f);
        dRect.pivot = new Vector2(0.5f, 0.5f);
        dRect.anchoredPosition = new Vector2(x, y);
        dRect.sizeDelta = new Vector2(7f, 7f);
        dRect.localRotation = Quaternion.Euler(0, 0, 45f);
        diamond.AddComponent<Image>().color = diamondColor;
    }

    private IEnumerator VictorySequence(System.Action onComplete)
    {
        float elapsed;

        // ── Phase 1: Fade to black ──
        elapsed = 0f;
        while (elapsed < fadeToBlackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeToBlackDuration;
            float alpha = t * t; // ease-in
            blackOverlay.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
        blackOverlay.color = new Color(0f, 0f, 0f, 1f);

        // ── Phase 2: Fade in victory text ──
        elapsed = 0f;
        while (elapsed < textFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / textFadeInDuration;
            float alpha = t * t * (3f - 2f * t); // smoothstep
            textCanvasGroup.alpha = alpha;

            // Subtle scale-down from slightly larger
            float scale = 1f + 0.08f * (1f - alpha);
            textCanvasGroup.transform.localScale = Vector3.one * scale;

            yield return null;
        }
        textCanvasGroup.alpha = 1f;
        textCanvasGroup.transform.localScale = Vector3.one;

        // ── Phase 3: Hold ──
        yield return new WaitForSecondsRealtime(textHoldDuration);

        // ── Phase 4: Fade out text ──
        elapsed = 0f;
        while (elapsed < textFadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / textFadeOutDuration;
            textCanvasGroup.alpha = 1f - (t * t); // ease-out
            yield return null;
        }
        textCanvasGroup.alpha = 0f;

        // ── Phase 5: Callback while screen is black ──
        onComplete?.Invoke();

        yield return new WaitForSecondsRealtime(blackHoldDuration);

        // ── Phase 6: Fade from black ──
        elapsed = 0f;
        while (elapsed < fadeFromBlackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeFromBlackDuration;
            float alpha = 1f - t * t * (3f - 2f * t); // smoothstep inverse
            blackOverlay.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
        blackOverlay.color = new Color(0f, 0f, 0f, 0f);

        // Cleanup
        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    private void SetFont(TextMeshProUGUI tmp)
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
    }
}
