using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Elden Ring-style boss health bar. Attach to any GameObject in the scene.
/// Creates its own screen-space overlay Canvas so it always stays at the top center
/// of the screen regardless of camera or player position.
/// Supports Phase 2 transitions.
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("Boss (auto-finds if empty)")]
    public EnemyHealth bossHealth;

    [Header("Display")]
    public string bossName = "Boss";

    [Header("Bar Appearance")]
    public float barWidth = 600f;
    public float barHeight = 18f;
    public float borderThickness = 3f;
    public float topOffset = -60f;
    [Min(0.5f)] public float barSizeMultiplier = 1.35f;
    public Color barColor = new Color(0.75f, 0.15f, 0.15f, 1f);
    public Color backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    public Color borderColor = new Color(0.6f, 0.55f, 0.35f, 1f);

    [Header("Phase 2 Appearance")]
    public Color phase2BarColor = new Color(0.9f, 0.1f, 0.1f, 1f);
    public string phase2BossName = "Boss - Phase 2";

    [Header("Smooth Drain")]
    public float drainSpeed = 2f;

    [Header("Reveal")]
    public bool hideUntilBossArenaEntry = true;
    [Min(0f)] public float revealFadeDuration = 0.5f;

    private RectTransform fillRect;
    private Image fillImage;
    private GameObject barRoot;
    private GameObject canvasRoot;
    private CanvasGroup barRootCanvasGroup;
    private Text bossNameText;
    private float displayedFill = 1f;
    private bool bossFound = false;
    private bool isVisibleByTrigger;
    private Coroutine revealCoroutine;

    // Phase tracking
    private BossEnemy bossEnemy;
    private int lastKnownPhase = 1;
    private bool wasDeadLastFrame = false;

    private void Start()
    {
        DestroyStaleBossBarCanvases();

        isVisibleByTrigger = !hideUntilBossArenaEntry;
        BuildUI();
        TryFindBoss();

        if (isVisibleByTrigger)
            ShowBarImmediate();
        else
            HideBarImmediate();
    }

    private void Update()
    {
        if (!bossFound)
        {
            TryFindBoss();
            if (!bossFound) return;
        }

        if (bossHealth == null) return;

        if (!isVisibleByTrigger)
        {
            HideBarImmediate();
            return;
        }

        bool bossTransitioning = bossEnemy != null && bossEnemy.IsInPhaseTransition;

        if (bossEnemy != null && bossEnemy.CurrentPhase > lastKnownPhase)
        {
            lastKnownPhase = bossEnemy.CurrentPhase;
            if (lastKnownPhase > 1)
            {
                if (fillImage != null)
                    fillImage.color = phase2BarColor;
                if (bossNameText != null)
                    bossNameText.text = phase2BossName;
            }
        }

        // During phase transition, phase 1 fill should fully disappear before phase 2 rises.
        if (bossTransitioning && lastKnownPhase <= 1)
            displayedFill = 0f;

        // Detect Phase 2 transition: boss was dead, now alive again with new health
        bool isDead = bossHealth.IsDead;
        bool phaseChanged = false;

        if (wasDeadLastFrame && !isDead && bossHealth.currentHealth > 0f)
        {
            // Boss just came back to life — Phase 2 started
            displayedFill = 0f; // Start from empty, will fill up
            phaseChanged = true;

            if (barRoot != null) barRoot.SetActive(true);
        }
        wasDeadLastFrame = isDead;

        // Calculate target fill
        float targetFill = 0f;
        if (bossHealth.maxHealth > 0f)
            targetFill = Mathf.Clamp01(bossHealth.currentHealth / bossHealth.maxHealth);

        // Boss is dead or at 0 — drain quickly to zero
        if (isDead || bossHealth.currentHealth <= 0f || targetFill <= 0f)
        {
            displayedFill = Mathf.MoveTowards(displayedFill, 0f, drainSpeed * 4f * Time.deltaTime);
            if (displayedFill < 0.005f)
                displayedFill = 0f;
        }
        // Phase 2 just started — fill UP smoothly from empty
        else if (phaseChanged || (displayedFill < targetFill - 0.01f && lastKnownPhase > 1))
        {
            displayedFill = Mathf.MoveTowards(displayedFill, targetFill, drainSpeed * 1.5f * Time.deltaTime);
        }
        // Health went up (other reasons) — snap up
        else if (targetFill > displayedFill)
        {
            displayedFill = targetFill;
        }
        // Normal drain
        else
        {
            displayedFill = Mathf.MoveTowards(displayedFill, targetFill, drainSpeed * Time.deltaTime);
            if (displayedFill < 0.005f)
                displayedFill = 0f;
        }

        // Apply fill visually
        if (fillRect != null)
        {
            if (displayedFill <= 0f)
            {
                fillImage.enabled = false;
                fillRect.localScale = Vector3.zero;
            }
            else
            {
                fillImage.enabled = true;
                fillRect.localScale = new Vector3(displayedFill, 1f, 1f);
            }
        }

        // Bar visibility
        if (barRoot != null)
        {
            if (isDead && displayedFill <= 0f && !bossTransitioning)
                barRoot.SetActive(false);
            else if (!isDead || bossTransitioning)
                barRoot.SetActive(true);
        }
    }

    /// <summary>
    /// Resets the health bar to hidden, phase 1 state. Called on player respawn.
    /// </summary>
    public void ResetBar()
    {
        lastKnownPhase = 1;
        displayedFill = 1f;
        wasDeadLastFrame = false;
        isVisibleByTrigger = !hideUntilBossArenaEntry;

        if (fillImage != null) fillImage.color = barColor;
        if (bossNameText != null) bossNameText.text = bossName;

        if (isVisibleByTrigger)
            ShowBarImmediate();
        else
            HideBarImmediate();

        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }

        // Ensure visuals are hard-reset so stale phase 2 bar does not survive restart.
        if (fillRect != null)
            fillRect.localScale = Vector3.one;
        if (fillImage != null)
            fillImage.enabled = true;
    }

    /// <summary>
    /// Reveals the boss health bar with a fade-in. Called when player enters boss arena.
    /// </summary>
    public void RevealBar()
    {
        isVisibleByTrigger = true;

        if (barRoot == null)
            return;

        if (revealCoroutine != null)
            StopCoroutine(revealCoroutine);

        barRoot.SetActive(true);

        if (barRootCanvasGroup == null)
            return;

        if (revealFadeDuration <= 0f)
        {
            barRootCanvasGroup.alpha = 1f;
            revealCoroutine = null;
            return;
        }

        barRootCanvasGroup.alpha = 0f;
        revealCoroutine = StartCoroutine(FadeBarAlpha(1f, revealFadeDuration));
    }

    private void TryFindBoss()
    {
        if (bossHealth == null)
        {
            BossEnemy boss = FindAnyObjectByType<BossEnemy>();
            if (boss != null)
            {
                bossHealth = boss.GetComponent<EnemyHealth>();
                bossEnemy = boss;
            }
        }

        if (bossHealth != null)
        {
            bossFound = true;
            displayedFill = bossHealth.maxHealth > 0f ? bossHealth.currentHealth / bossHealth.maxHealth : 0f;

            if (bossEnemy != null && bossEnemy.CurrentPhase > 1)
            {
                lastKnownPhase = bossEnemy.CurrentPhase;
                if (fillImage != null)
                    fillImage.color = phase2BarColor;
                if (bossNameText != null)
                    bossNameText.text = phase2BossName;
            }
            else
            {
                lastKnownPhase = 1;
                if (fillImage != null)
                    fillImage.color = barColor;
                if (bossNameText != null)
                    bossNameText.text = bossName;
            }
        }
    }

    private void BuildUI()
    {
        float scale = Mathf.Max(0.5f, barSizeMultiplier);
        float scaledBarWidth = barWidth * scale;
        float scaledBarHeight = barHeight * scale;
        float scaledBorder = borderThickness * scale;
        int scaledNameFontSize = Mathf.RoundToInt(18f * scale);

        // Create a dedicated overlay canvas — NOT shared with player UI
        canvasRoot = new GameObject("BossHealthBarCanvas");
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasRoot.AddComponent<GraphicRaycaster>();

        // Root container anchored to top-center
        barRoot = new GameObject("BossBarRoot");
        barRoot.transform.SetParent(canvasRoot.transform, false);
        barRootCanvasGroup = barRoot.AddComponent<CanvasGroup>();
        barRootCanvasGroup.alpha = 0f;
        RectTransform rootRect = barRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, topOffset);
        rootRect.sizeDelta = new Vector2(scaledBarWidth + scaledBorder * 2, scaledBarHeight + scaledBorder * 2 + 30f * scale);

        // Boss name text
        GameObject nameObj = new GameObject("BossName");
        nameObj.transform.SetParent(barRoot.transform, false);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.5f, 1f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = Vector2.zero;
        nameRect.sizeDelta = new Vector2(scaledBarWidth, 25f * scale);
        bossNameText = nameObj.AddComponent<Text>();
        bossNameText.text = bossName;
        bossNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bossNameText.fontSize = scaledNameFontSize;
        bossNameText.alignment = TextAnchor.MiddleCenter;
        bossNameText.color = new Color(0.9f, 0.85f, 0.7f, 1f);

        // Gold border
        GameObject border = new GameObject("Border");
        border.transform.SetParent(barRoot.transform, false);
        RectTransform borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0.5f, 1f);
        borderRect.anchorMax = new Vector2(0.5f, 1f);
        borderRect.pivot = new Vector2(0.5f, 1f);
        borderRect.anchoredPosition = new Vector2(0f, -25f * scale);
        borderRect.sizeDelta = new Vector2(scaledBarWidth + scaledBorder * 2, scaledBarHeight + scaledBorder * 2);
        Image borderImg = border.AddComponent<Image>();
        borderImg.color = borderColor;

        // Dark background
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(border.transform, false);
        RectTransform bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = new Vector2(scaledBorder, scaledBorder);
        bgRect.offsetMax = new Vector2(-scaledBorder, -scaledBorder);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = backgroundColor;

        // Health fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(bg.transform, false);
        fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillImage = fill.AddComponent<Image>();
        fillImage.color = barColor;

        barRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }

        if (canvasRoot != null)
            Destroy(canvasRoot);
    }

    private static void DestroyStaleBossBarCanvases()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            if (canvas.gameObject.name == "BossHealthBarCanvas")
                Destroy(canvas.gameObject);
        }
    }

    private IEnumerator FadeBarAlpha(float targetAlpha, float duration)
    {
        float elapsed = 0f;
        float startAlpha = barRootCanvasGroup != null ? barRootCanvasGroup.alpha : 1f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            if (barRootCanvasGroup != null)
                barRootCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        if (barRootCanvasGroup != null)
            barRootCanvasGroup.alpha = targetAlpha;

        revealCoroutine = null;
    }

    private void ShowBarImmediate()
    {
        if (barRoot == null)
            return;

        barRoot.SetActive(true);

        if (barRootCanvasGroup != null)
            barRootCanvasGroup.alpha = 1f;
    }

    private void HideBarImmediate()
    {
        if (barRoot == null)
            return;

        if (barRootCanvasGroup != null)
            barRootCanvasGroup.alpha = 0f;

        barRoot.SetActive(false);
    }
}
