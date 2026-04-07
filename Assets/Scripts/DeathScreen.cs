using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Elden Ring-style death screen: screen fades to black, "YOU DIED" text fades in/out,
/// then fades back to gameplay after respawn.
/// </summary>
public class DeathScreen : MonoBehaviour
{
    [Header("Timing")]
    public float fadeToBlackDuration = 1.5f;
    public float textFadeInDuration = 1.2f;
    public float textHoldDuration = 2.0f;
    public float textFadeOutDuration = 0.8f;
    public float blackHoldDuration = 0.5f;
    public float fadeFromBlackDuration = 1.5f;

    [Header("Text Style")]
    public string deathText = "YOU DIED";
    public float textFontSize = 80f;
    public Color textColor = new Color(0.7f, 0.15f, 0.12f, 1f); // deep blood red
    public float letterSpacing = 18f;

    [Header("Overlay")]
    public Color overlayColor = new Color(0f, 0f, 0f, 1f);

    // Runtime refs
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Image blackOverlay;
    private TextMeshProUGUI deathTextTMP;
    private CanvasGroup textCanvasGroup;

    private static DeathScreen _instance;

    /// <summary>
    /// Gets or creates the singleton DeathScreen instance.
    /// </summary>
    public static DeathScreen Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("DeathScreen");
                _instance = go.AddComponent<DeathScreen>();
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
    /// Plays the full death screen sequence. Returns total duration so caller can wait.
    /// onRespawn is called at the moment the screen is fully black (before fade-back).
    /// </summary>
    public float Play(System.Action onRespawn = null)
    {
        BuildUI();
        PlayDeathScreenSfx();
        StartCoroutine(DeathSequence(onRespawn));
        return fadeToBlackDuration + textFadeInDuration + textHoldDuration
             + textFadeOutDuration + blackHoldDuration + fadeFromBlackDuration;
    }

    private void PlayDeathScreenSfx()
    {
        DeathVictoryScreenAudioController controller = DeathVictoryScreenAudioController.FindInstance();
        if (controller != null)
        {
            controller.FadeOutBackgroundMusic();
            controller.PlayDeathScreenSfx();
        }
    }

    private void BuildUI()
    {
        // Destroy previous UI if any
        if (canvas != null)
            Destroy(canvas.gameObject);

        // ── Canvas ──
        GameObject canvasObj = new GameObject("DeathScreenCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // above everything

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // ── Full-screen black overlay ──
        GameObject overlayObj = new GameObject("BlackOverlay");
        overlayObj.transform.SetParent(canvasObj.transform, false);
        RectTransform overlayRect = overlayObj.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        blackOverlay = overlayObj.AddComponent<Image>();
        blackOverlay.color = new Color(0f, 0f, 0f, 0f); // start transparent

        // ── "YOU DIED" text container ──
        GameObject textContainer = new GameObject("DeathTextContainer");
        textContainer.transform.SetParent(canvasObj.transform, false);
        RectTransform textContainerRect = textContainer.AddComponent<RectTransform>();
        textContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
        textContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
        textContainerRect.pivot = new Vector2(0.5f, 0.5f);
        textContainerRect.anchoredPosition = Vector2.zero;
        textContainerRect.sizeDelta = new Vector2(1000f, 200f);

        textCanvasGroup = textContainer.AddComponent<CanvasGroup>();
        textCanvasGroup.alpha = 0f;

        // ── Main death text ──
        GameObject textObj = new GameObject("DeathText");
        textObj.transform.SetParent(textContainer.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        deathTextTMP = textObj.AddComponent<TextMeshProUGUI>();
        deathTextTMP.text = deathText;
        deathTextTMP.fontSize = textFontSize;
        deathTextTMP.color = textColor;
        deathTextTMP.alignment = TextAlignmentOptions.Center;
        deathTextTMP.characterSpacing = letterSpacing;
        deathTextTMP.fontStyle = FontStyles.Normal;
        deathTextTMP.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null)
            deathTextTMP.font = font;

        // ── Decorative lines above and below text ──
        BuildDeathLine(textContainer.transform, 50f);
        BuildDeathLine(textContainer.transform, -50f);
    }

    private void BuildDeathLine(Transform parent, float yOffset)
    {
        GameObject lineObj = new GameObject("DeathLine");
        lineObj.transform.SetParent(parent, false);
        RectTransform lineRect = lineObj.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = new Vector2(0f, yOffset);
        lineRect.sizeDelta = new Vector2(400f, 2f);

        Image lineImg = lineObj.AddComponent<Image>();
        lineImg.color = new Color(textColor.r, textColor.g, textColor.b, 0.4f);

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

            Image extImg = ext.AddComponent<Image>();
            extImg.color = new Color(textColor.r, textColor.g, textColor.b, 0.15f);
        }
    }

    private IEnumerator DeathSequence(System.Action onRespawn)
    {
        float elapsed;

        // ── Phase 1: Fade screen to black ──
        elapsed = 0f;
        while (elapsed < fadeToBlackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeToBlackDuration;
            // Ease-in: slow start, accelerating darkness
            float alpha = t * t;
            blackOverlay.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
        blackOverlay.color = new Color(0f, 0f, 0f, 1f);

        // ── Phase 2: Fade in "YOU DIED" text ──
        elapsed = 0f;
        while (elapsed < textFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / textFadeInDuration;
            // Smooth step for elegant entrance
            float alpha = t * t * (3f - 2f * t);
            textCanvasGroup.alpha = alpha;

            // Subtle scale-up effect (starts slightly larger, settles to 1)
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
            textCanvasGroup.alpha = 1f - (t * t);
            yield return null;
        }
        textCanvasGroup.alpha = 0f;

        // ── Phase 5: Respawn callback (while screen is still black) ──
        onRespawn?.Invoke();

        yield return new WaitForSecondsRealtime(blackHoldDuration);

        // ── Phase 6: Fade back from black ──
        elapsed = 0f;
        while (elapsed < fadeFromBlackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeFromBlackDuration;
            // Ease-out: fast start, gentle end
            float alpha = 1f - t * t * (3f - 2f * t);
            blackOverlay.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
        blackOverlay.color = new Color(0f, 0f, 0f, 0f);

        // Cleanup
        if (canvas != null)
            Destroy(canvas.gameObject);
    }
}
