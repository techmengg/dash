using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Dark-themed power-up card selection screen shown between waves.
/// Pauses the game (Time.timeScale = 0) until the player picks a card or scraps.
/// Two cards + a "Scrap" button at the bottom.
/// </summary>
public class CardSelectionUI : MonoBehaviour
{
    // ── Card data (blank for now — just title + description placeholders) ──
    [System.Serializable]
    public class CardData
    {
        public string title = "???";
        public string description = "Power-up effect goes here.";
        public Color accentColor = new Color(0.85f, 0.25f, 0.2f, 1f);
    }

    // Callback: -1 = scrapped, 0 = left card, 1 = right card
    private Action<int> onSelection;
    private GameObject canvasObj;
    private CanvasGroup canvasGroup;
    private bool selectionMade = false;
    private bool cachedCursorVisible;
    private CursorLockMode cachedCursorLockMode;
    private bool cursorStateCached;

    // ── Theme colors ──
    private static readonly Color bgDark = new Color(0.02f, 0.02f, 0.03f, 0.92f);
    private static readonly Color cardBg = new Color(0.06f, 0.05f, 0.07f, 1f);
    private static readonly Color cardBorder = new Color(0.4f, 0.12f, 0.1f, 1f);
    private static readonly Color cardBorderHover = new Color(0.75f, 0.2f, 0.15f, 1f);
    private static readonly Color headerColor = new Color(0.92f, 0.85f, 0.65f, 1f);
    private static readonly Color titleColor = new Color(0.9f, 0.82f, 0.6f, 1f);
    private static readonly Color descColor = new Color(0.6f, 0.55f, 0.5f, 1f);
    private static readonly Color scrapBg = new Color(0.12f, 0.1f, 0.1f, 1f);
    private static readonly Color scrapBorder = new Color(0.35f, 0.3f, 0.25f, 0.6f);
    private static readonly Color scrapText = new Color(0.5f, 0.45f, 0.4f, 1f);
    private static readonly Color ornamentColor = new Color(0.7f, 0.55f, 0.3f, 0.35f);
    private static readonly Color dimLineColor = new Color(0.7f, 0.55f, 0.3f, 0.15f);

    /// <summary>
    /// Shows the card selection UI with two cards. Pauses the game.
    /// Call with a callback that receives the chosen index (-1=scrap, 0=left, 1=right).
    /// </summary>
    public void Show(CardData card1, CardData card2, Action<int> callback)
    {
        onSelection = callback;
        selectionMade = false;
        CacheAndShowCursor();
        BuildUI(card1, card2);
        StartCoroutine(FadeIn());
        Time.timeScale = 0f;
    }

    private void CacheAndShowCursor()
    {
        cachedCursorVisible = Cursor.visible;
        cachedCursorLockMode = Cursor.lockState;
        cursorStateCached = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void RestoreCursor()
    {
        if (!cursorStateCached)
            return;

        Cursor.visible = cachedCursorVisible;
        Cursor.lockState = cachedCursorLockMode;
        cursorStateCached = false;
    }

    private void BuildUI(CardData card1, CardData card2)
    {
        if (canvasObj != null) Destroy(canvasObj);

        // ── Root Canvas ──
        canvasObj = new GameObject("CardSelectionCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // Ensure an EventSystem exists so UI buttons can receive clicks
        if (EventSystem.current == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
        }

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        // ── Full-screen dark overlay ──
        GameObject overlay = MakeRect("Overlay", canvasObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        overlay.AddComponent<Image>().color = bgDark;

        // ── Main container ──
        GameObject container = MakeRect("Container", canvasObj.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero);
        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(900f, 620f);

        // ── Header: "CHOOSE YOUR POWER" ──
        BuildHeader(container.transform);

        // ── Top decorative line ──
        BuildDecorLine(container.transform, 200f);

        // ── Two cards side by side ──
        BuildCard(container.transform, card1, -175f, 0);
        BuildCard(container.transform, card2, 175f, 1);

        // ── "OR" text between cards ──
        GameObject orObj = MakeRect("OrText", container.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 10f));
        RectTransform orRect = orObj.GetComponent<RectTransform>();
        orRect.sizeDelta = new Vector2(50f, 40f);
        TextMeshProUGUI orText = orObj.AddComponent<TextMeshProUGUI>();
        orText.text = "OR";
        orText.fontSize = 18f;
        orText.color = new Color(0.45f, 0.4f, 0.35f, 0.6f);
        orText.alignment = TextAlignmentOptions.Center;
        orText.fontStyle = FontStyles.Italic;
        SetFont(orText);

        // ── Bottom decorative line ──
        BuildDecorLine(container.transform, -175f);

        // ── Scrap / Ignore button ──
        BuildScrapButton(container.transform);
    }

    // ══════════════════════════════════════════════════════════════
    // HEADER
    // ══════════════════════════════════════════════════════════════

    private void BuildHeader(Transform parent)
    {
        GameObject headerObj = MakeRect("Header", parent,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -15f));
        RectTransform hRect = headerObj.GetComponent<RectTransform>();
        hRect.sizeDelta = new Vector2(600f, 50f);

        TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = "CHOOSE YOUR POWER";
        headerText.fontSize = 32f;
        headerText.color = headerColor;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.characterSpacing = 8f;
        headerText.fontStyle = FontStyles.Normal;
        SetFont(headerText);

        // Small ornaments flanking the header
        BuildSmallDiamond(parent, -260f, -40f, headerColor);
        BuildSmallDiamond(parent, 260f, -40f, headerColor);
    }

    // ══════════════════════════════════════════════════════════════
    // CARD
    // ══════════════════════════════════════════════════════════════

    private void BuildCard(Transform parent, CardData data, float xOffset, int index)
    {
        float cardW = 300f;
        float cardH = 400f;

        // ── Card root ──
        GameObject cardRoot = MakeRect("Card" + index, parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(xOffset, 10f));
        RectTransform cardRootRect = cardRoot.GetComponent<RectTransform>();
        cardRootRect.sizeDelta = new Vector2(cardW, cardH);

        // ── Outer border (glowing frame) ──
        Image borderImg = cardRoot.AddComponent<Image>();
        borderImg.color = cardBorder;

        // ── Inner background ──
        GameObject inner = MakeRect("Inner", cardRoot.transform,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f), Vector2.zero);
        RectTransform innerRect = inner.GetComponent<RectTransform>();
        innerRect.offsetMin = new Vector2(3f, 3f);
        innerRect.offsetMax = new Vector2(-3f, -3f);
        inner.AddComponent<Image>().color = cardBg;

        // ── Top accent stripe ──
        GameObject stripe = MakeRect("Stripe", cardRoot.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero);
        RectTransform stripeRect = stripe.GetComponent<RectTransform>();
        stripeRect.sizeDelta = new Vector2(0f, 6f);
        stripe.AddComponent<Image>().color = data.accentColor;

        // ── Icon area (placeholder diamond) ──
        BuildCardIcon(cardRoot.transform, data.accentColor);

        // ── Horizontal separator under icon ──
        GameObject sep = MakeRect("Sep", cardRoot.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 30f));
        RectTransform sepRect = sep.GetComponent<RectTransform>();
        sepRect.sizeDelta = new Vector2(cardW * 0.6f, 1.5f);
        sep.AddComponent<Image>().color = new Color(data.accentColor.r, data.accentColor.g, data.accentColor.b, 0.3f);

        // ── Card title ──
        GameObject titleObj = MakeRect("Title", cardRoot.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -5f));
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(cardW - 30f, 40f);
        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = data.title;
        titleTMP.fontSize = 24f;
        titleTMP.color = titleColor;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.characterSpacing = 3f;
        titleTMP.fontStyle = FontStyles.Bold;
        SetFont(titleTMP);

        // ── Card description ──
        GameObject descObj = MakeRect("Desc", cardRoot.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -55f));
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.sizeDelta = new Vector2(cardW - 40f, 70f);
        TextMeshProUGUI descTMP = descObj.AddComponent<TextMeshProUGUI>();
        descTMP.text = data.description;
        descTMP.fontSize = 16f;
        descTMP.color = descColor;
        descTMP.alignment = TextAlignmentOptions.Center;
        descTMP.textWrappingMode = TextWrappingModes.Normal;
        SetFont(descTMP);

        // ── Corner ornaments ──
        BuildCornerOrnament(cardRoot.transform, cardW, cardH, true, true);   // top-left
        BuildCornerOrnament(cardRoot.transform, cardW, cardH, false, true);  // top-right
        BuildCornerOrnament(cardRoot.transform, cardW, cardH, true, false);  // bot-left
        BuildCornerOrnament(cardRoot.transform, cardW, cardH, false, false); // bot-right

        // ── Bottom accent line ──
        GameObject botLine = MakeRect("BotAccent", cardRoot.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), Vector2.zero);
        RectTransform botLineRect = botLine.GetComponent<RectTransform>();
        botLineRect.sizeDelta = new Vector2(0f, 3f);
        botLine.AddComponent<Image>().color = new Color(data.accentColor.r, data.accentColor.g, data.accentColor.b, 0.5f);

        // ── Click button (invisible, covers the whole card) ──
        Button btn = cardRoot.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = cb;
        btn.targetGraphic = borderImg;

        // Hover effect: brighten border
        int capturedIndex = index;
        CardHoverEffect hover = cardRoot.AddComponent<CardHoverEffect>();
        hover.Init(borderImg, cardBorder, cardBorderHover, data.accentColor);

        btn.onClick.AddListener(() => OnCardPicked(capturedIndex));
    }

    private void BuildCardIcon(Transform parent, Color accent)
    {
        // Large diamond icon placeholder
        GameObject iconContainer = MakeRect("IconArea", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 95f));
        RectTransform iconRect = iconContainer.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(70f, 70f);

        // Outer diamond
        GameObject outerDiamond = MakeRect("DiamondOuter", iconContainer.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero);
        RectTransform outerRect = outerDiamond.GetComponent<RectTransform>();
        outerRect.sizeDelta = new Vector2(50f, 50f);
        outerRect.localRotation = Quaternion.Euler(0, 0, 45f);
        Image outerImg = outerDiamond.AddComponent<Image>();
        outerImg.color = new Color(accent.r, accent.g, accent.b, 0.25f);

        // Inner diamond
        GameObject innerDiamond = MakeRect("DiamondInner", iconContainer.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero);
        RectTransform innerRect = innerDiamond.GetComponent<RectTransform>();
        innerRect.sizeDelta = new Vector2(35f, 35f);
        innerRect.localRotation = Quaternion.Euler(0, 0, 45f);
        Image innerImg = innerDiamond.AddComponent<Image>();
        innerImg.color = new Color(accent.r * 1.2f, accent.g * 1.1f, accent.b, 0.5f);

        // "?" text
        GameObject qMark = MakeRect("QMark", iconContainer.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero);
        RectTransform qRect = qMark.GetComponent<RectTransform>();
        qRect.sizeDelta = new Vector2(50f, 50f);
        TextMeshProUGUI qText = qMark.AddComponent<TextMeshProUGUI>();
        qText.text = "?";
        qText.fontSize = 36f;
        qText.color = new Color(accent.r * 1.3f, accent.g * 1.2f, accent.b * 1.1f, 0.8f);
        qText.alignment = TextAlignmentOptions.Center;
        qText.fontStyle = FontStyles.Bold;
        SetFont(qText);
    }

    private void BuildCornerOrnament(Transform parent, float w, float h, bool left, bool top)
    {
        float x = left ? -w / 2f + 12f : w / 2f - 12f;
        float y = top ? h / 2f - 12f : -h / 2f + 12f;
        // L-shaped corner bracket
        float lineLen = 14f;
        float lineThick = 1.5f;
        Color col = ornamentColor;

        // Horizontal line
        float hx = left ? x + lineLen / 2f : x - lineLen / 2f;
        GameObject hLine = MakeRect("CornerH", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(hx, y));
        hLine.GetComponent<RectTransform>().sizeDelta = new Vector2(lineLen, lineThick);
        hLine.AddComponent<Image>().color = col;

        // Vertical line
        float vy = top ? y - lineLen / 2f : y + lineLen / 2f;
        GameObject vLine = MakeRect("CornerV", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(x, vy));
        vLine.GetComponent<RectTransform>().sizeDelta = new Vector2(lineThick, lineLen);
        vLine.AddComponent<Image>().color = col;
    }

    // ══════════════════════════════════════════════════════════════
    // SCRAP BUTTON
    // ══════════════════════════════════════════════════════════════

    private void BuildScrapButton(Transform parent)
    {
        float btnW = 220f;
        float btnH = 44f;

        GameObject btnObj = MakeRect("ScrapBtn", parent,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 25f));
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(btnW, btnH);

        // Border
        Image btnBorder = btnObj.AddComponent<Image>();
        btnBorder.color = scrapBorder;

        // Inner bg
        GameObject btnInner = MakeRect("ScrapInner", btnObj.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        RectTransform innerRect = btnInner.GetComponent<RectTransform>();
        innerRect.offsetMin = new Vector2(2f, 2f);
        innerRect.offsetMax = new Vector2(-2f, -2f);
        btnInner.AddComponent<Image>().color = scrapBg;

        // Text
        GameObject textObj = MakeRect("ScrapText", btnObj.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        textObj.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        textObj.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        TextMeshProUGUI scrapTMP = textObj.AddComponent<TextMeshProUGUI>();
        scrapTMP.text = "SCRAP";
        scrapTMP.fontSize = 20f;
        scrapTMP.color = scrapText;
        scrapTMP.alignment = TextAlignmentOptions.Center;
        scrapTMP.characterSpacing = 6f;
        SetFont(scrapTMP);

        // Small dashes flanking text
        BuildScrapDash(btnObj.transform, -85f);
        BuildScrapDash(btnObj.transform, 85f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnBorder;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.2f, 1.1f, 1f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = cb;

        // Hover effect for scrap
        CardHoverEffect hover = btnObj.AddComponent<CardHoverEffect>();
        hover.Init(btnBorder, scrapBorder, new Color(0.55f, 0.45f, 0.35f, 0.8f), scrapText);

        btn.onClick.AddListener(() => OnCardPicked(-1));
    }

    private void BuildScrapDash(Transform parent, float xOffset)
    {
        GameObject dash = MakeRect("ScrapDash", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(xOffset, 0f));
        dash.GetComponent<RectTransform>().sizeDelta = new Vector2(20f, 1.5f);
        dash.AddComponent<Image>().color = new Color(scrapText.r, scrapText.g, scrapText.b, 0.3f);
    }

    // ══════════════════════════════════════════════════════════════
    // DECORATIVE ELEMENTS
    // ══════════════════════════════════════════════════════════════

    private void BuildDecorLine(Transform parent, float yOffset)
    {
        // Main line
        GameObject line = MakeRect("DecorLine", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, yOffset));
        line.GetComponent<RectTransform>().sizeDelta = new Vector2(350f, 1.5f);
        line.AddComponent<Image>().color = ornamentColor;

        // Faded extensions
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject ext = MakeRect("LineExt", parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(side * 230f, yOffset));
            ext.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 1f);
            ext.AddComponent<Image>().color = dimLineColor;
        }

        // Center diamond
        BuildSmallDiamond(parent, 0f, yOffset, ornamentColor);
    }

    private void BuildSmallDiamond(Transform parent, float x, float y, Color color)
    {
        GameObject diamond = MakeRect("Diamond", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(x, y));
        RectTransform dRect = diamond.GetComponent<RectTransform>();
        dRect.sizeDelta = new Vector2(7f, 7f);
        dRect.localRotation = Quaternion.Euler(0, 0, 45f);
        diamond.AddComponent<Image>().color = new Color(color.r, color.g, color.b, 0.7f);
    }

    // ══════════════════════════════════════════════════════════════
    // SELECTION & CLEANUP
    // ══════════════════════════════════════════════════════════════

    private void OnCardPicked(int index)
    {
        if (selectionMade) return;
        selectionMade = true;
        StartCoroutine(FadeOutAndFinish(index));
    }

    private IEnumerator FadeIn()
    {
        float dur = 0.4f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            canvasGroup.alpha = t * t * (3f - 2f * t);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutAndFinish(int index)
    {
        float dur = 0.3f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            canvasGroup.alpha = 1f - t;
            yield return null;
        }
        canvasGroup.alpha = 0f;

        // Unpause
        Time.timeScale = 1f;
        RestoreCursor();

        // Cleanup
        if (canvasObj != null) Destroy(canvasObj);

        onSelection?.Invoke(index);
    }

    private void OnDestroy()
    {
        // Safety: ensure timeScale is restored if this gets destroyed unexpectedly
        Time.timeScale = 1f;
        RestoreCursor();
        if (canvasObj != null) Destroy(canvasObj);
    }

    // ══════════════════════════════════════════════════════════════
    // UTILITY
    // ══════════════════════════════════════════════════════════════

    private GameObject MakeRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        return go;
    }

    private void SetFont(TextMeshProUGUI tmp)
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
    }
}

/// <summary>
/// Simple hover color effect for card/button borders using IPointerEnter/Exit.
/// </summary>
public class CardHoverEffect : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    private Image targetImage;
    private Color normalColor;
    private Color hoverColor;
    private Color accentColor;

    public void Init(Image img, Color normal, Color hover, Color accent)
    {
        targetImage = img;
        normalColor = normal;
        hoverColor = hover;
        accentColor = accent;
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (targetImage != null) targetImage.color = hoverColor;
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (targetImage != null) targetImage.color = normalColor;
    }
}
