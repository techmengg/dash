using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A physical door barrier that blocks passage until the player presses E.
/// Shows a "Press E" prompt when the player is nearby.
/// Opening plays a slide-open animation and removes the collider.
/// </summary>
public class DoorInteractable : MonoBehaviour
{
    [Header("Door Settings")]
    public float doorWidth = 5f;
    public Color doorColor = new Color(0.25f, 0.12f, 0.12f, 0.9f);
    public Color doorAccentColor = new Color(0.5f, 0.2f, 0.15f, 0.7f);
    public float interactRange = 2.5f;
    public float openSpeed = 4f;
    public bool startLocked = false;
    public bool autoClose = false;
    public float autoCloseDelay = 1.5f;

    [Header("Prompt Style")]
    public Color promptColor = new Color(0.9f, 0.85f, 0.7f, 1f);
    public Color promptBgColor = new Color(0.05f, 0.05f, 0.08f, 0.7f);

    private bool isOpen = false;
    private bool isOpening = false;
    private bool isLocked = false;
    private Transform player;
    private BoxCollider2D doorCollider;

    // Door panel visuals
    private GameObject leftPanel;
    private GameObject rightPanel;
    private GameObject centerLine;

    // UI prompt
    private GameObject promptCanvas;
    private RectTransform promptContainer;
    private CanvasGroup promptCanvasGroup;
    private float promptAlpha = 0f;

    private static Sprite _sq;
    private Camera _mainCam;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        _mainCam = Camera.main;

        BuildDoorPanels();
        BuildPromptUI();

        if (startLocked)
            isLocked = true;
    }

    private void Update()
    {
        if (isOpen || isLocked || player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);
        bool inRange = dist < interactRange && !isOpening;

        // Fade prompt in/out
        float targetAlpha = inRange ? 1f : 0f;
        promptAlpha = Mathf.MoveTowards(promptAlpha, targetAlpha, Time.deltaTime * 5f);
        if (promptCanvasGroup != null)
            promptCanvasGroup.alpha = promptAlpha;

        // Position prompt above the door in world space
        if (promptContainer != null && promptAlpha > 0.01f)
        {
            Vector3 screenPos = _mainCam.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
            // Convert screen position to canvas local position for CanvasScaler compatibility
            Vector2 viewportPos = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);
            promptContainer.anchorMin = viewportPos;
            promptContainer.anchorMax = viewportPos;
            promptContainer.anchoredPosition = Vector2.zero;
        }

        // Check for E key
        if (inRange && Input.GetKeyDown(KeyCode.E))
        {
            StartCoroutine(OpenDoor());
        }
    }

    private void BuildDoorPanels()
    {
        // Physical barrier collider
        doorCollider = gameObject.AddComponent<BoxCollider2D>();
        doorCollider.size = new Vector2(doorWidth, 0.8f);
        doorCollider.offset = Vector2.zero;

        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        gameObject.layer = LayerMask.NameToLayer("Wall");
        gameObject.tag = "Walls";

        float panelWidth = doorWidth / 2f - 0.1f;

        // Left door panel
        leftPanel = new GameObject("DoorPanelL");
        leftPanel.transform.SetParent(transform, false);
        leftPanel.transform.localPosition = new Vector3(-doorWidth / 4f, 0, 0);
        leftPanel.transform.localScale = new Vector3(panelWidth, 0.6f, 1f);
        SpriteRenderer srL = leftPanel.AddComponent<SpriteRenderer>();
        srL.sprite = GetSquare();
        srL.color = doorColor;
        srL.sortingOrder = 5;

        // Right door panel
        rightPanel = new GameObject("DoorPanelR");
        rightPanel.transform.SetParent(transform, false);
        rightPanel.transform.localPosition = new Vector3(doorWidth / 4f, 0, 0);
        rightPanel.transform.localScale = new Vector3(panelWidth, 0.6f, 1f);
        SpriteRenderer srR = rightPanel.AddComponent<SpriteRenderer>();
        srR.sprite = GetSquare();
        srR.color = doorColor;
        srR.sortingOrder = 5;

        // Center seam line
        centerLine = new GameObject("DoorSeam");
        centerLine.transform.SetParent(transform, false);
        centerLine.transform.localPosition = Vector3.zero;
        centerLine.transform.localScale = new Vector3(0.06f, 0.7f, 1f);
        SpriteRenderer srC = centerLine.AddComponent<SpriteRenderer>();
        srC.sprite = GetSquare();
        srC.color = doorAccentColor;
        srC.sortingOrder = 6;

        // Accent lines on panels
        BuildPanelAccents(leftPanel.transform, panelWidth);
        BuildPanelAccents(rightPanel.transform, panelWidth);

        // Top and bottom edge highlights
        CreateAccent("EdgeTop", new Vector3(0, 0.35f, 0), new Vector3(doorWidth + 0.2f, 0.04f, 1f), doorAccentColor);
        CreateAccent("EdgeBot", new Vector3(0, -0.35f, 0), new Vector3(doorWidth + 0.2f, 0.04f, 1f), doorAccentColor);
    }

    private void BuildPanelAccents(Transform panel, float panelWidth)
    {
        // Horizontal bar across the panel
        GameObject bar = new GameObject("Accent");
        bar.transform.SetParent(panel, false);
        bar.transform.localPosition = Vector3.zero;
        // Scale relative to parent (parent is already scaled)
        bar.transform.localScale = new Vector3(0.8f, 0.08f / 0.6f, 1f);
        SpriteRenderer sr = bar.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquare();
        sr.color = doorAccentColor;
        sr.sortingOrder = 6;

        // Small diamond at center of panel
        GameObject diamond = new GameObject("Diamond");
        diamond.transform.SetParent(panel, false);
        diamond.transform.localPosition = Vector3.zero;
        diamond.transform.localScale = new Vector3(0.15f / panelWidth, 0.15f / 0.6f, 1f);
        diamond.transform.localRotation = Quaternion.Euler(0, 0, 45f);
        SpriteRenderer dsr = diamond.AddComponent<SpriteRenderer>();
        dsr.sprite = GetSquare();
        dsr.color = new Color(doorAccentColor.r * 1.3f, doorAccentColor.g * 1.2f, doorAccentColor.b, 0.8f);
        dsr.sortingOrder = 7;
    }

    private void CreateAccent(string name, Vector3 localPos, Vector3 scale, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquare();
        sr.color = color;
        sr.sortingOrder = 6;
    }

    private void BuildPromptUI()
    {
        // Screen-space canvas for the "Press E" prompt
        promptCanvas = new GameObject("DoorPromptCanvas");
        Canvas canvas = promptCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;

        CanvasScaler scaler = promptCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        promptCanvasGroup = promptCanvas.AddComponent<CanvasGroup>();
        promptCanvasGroup.alpha = 0f;
        promptCanvasGroup.interactable = false;
        promptCanvasGroup.blocksRaycasts = false;

        // Container — anchors set dynamically in Update to track door position
        GameObject container = new GameObject("PromptContainer");
        container.transform.SetParent(promptCanvas.transform, false);
        promptContainer = container.AddComponent<RectTransform>();
        promptContainer.anchorMin = new Vector2(0.5f, 0.5f);
        promptContainer.anchorMax = new Vector2(0.5f, 0.5f);
        promptContainer.pivot = new Vector2(0.5f, 0.5f);
        promptContainer.sizeDelta = new Vector2(200f, 50f);

        // Background
        Image bg = container.AddComponent<Image>();
        bg.color = promptBgColor;

        // "Press E" text
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(container.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Press <b>E</b>";
        text.fontSize = 22f;
        text.color = promptColor;
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = 3f;

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null)
            text.font = font;

        // Key icon — bordered "E" box
        GameObject keyBox = new GameObject("KeyBox");
        keyBox.transform.SetParent(container.transform, false);
        RectTransform keyRect = keyBox.AddComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0.5f, 0f);
        keyRect.anchorMax = new Vector2(0.5f, 0f);
        keyRect.pivot = new Vector2(0.5f, 0.5f);
        keyRect.anchoredPosition = new Vector2(0f, -18f);
        keyRect.sizeDelta = new Vector2(30f, 4f);

        Image keyBorder = keyBox.AddComponent<Image>();
        keyBorder.color = new Color(promptColor.r, promptColor.g, promptColor.b, 0.3f);
    }

    private IEnumerator OpenDoor()
    {
        isOpening = true;

        BossArenaAudioController bossArenaAudio = BossArenaAudioController.FindInstance();
        if (bossArenaAudio != null)
            bossArenaAudio.PlayDoorOpenSfx();

        // Fade out prompt immediately
        if (promptCanvasGroup != null)
        {
            float fadeTime = 0.2f;
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                promptCanvasGroup.alpha = 1f - (elapsed / fadeTime);
                yield return null;
            }
            promptCanvasGroup.alpha = 0f;
        }

        // Disable collider so player can walk through
        if (doorCollider != null)
            doorCollider.enabled = false;

        // Slide panels apart
        float leftTarget = -(doorWidth / 2f + 0.5f);
        float rightTarget = doorWidth / 2f + 0.5f;
        Vector3 leftStart = leftPanel.transform.localPosition;
        Vector3 rightStart = rightPanel.transform.localPosition;

        // Also fade the center seam
        SpriteRenderer seamSr = centerLine != null ? centerLine.GetComponent<SpriteRenderer>() : null;

        float slideTime = 0.5f;
        float elapsed2 = 0f;

        while (elapsed2 < slideTime)
        {
            elapsed2 += Time.deltaTime;
            float t = elapsed2 / slideTime;
            // Ease-out for snappy feel
            float eased = 1f - (1f - t) * (1f - t);

            leftPanel.transform.localPosition = Vector3.Lerp(leftStart, new Vector3(leftTarget, 0, 0), eased);
            rightPanel.transform.localPosition = Vector3.Lerp(rightStart, new Vector3(rightTarget, 0, 0), eased);

            // Fade all visuals
            float alpha = 1f - eased;
            SetChildAlpha(leftPanel, alpha);
            SetChildAlpha(rightPanel, alpha);
            if (seamSr != null)
            {
                Color c = seamSr.color;
                c.a = doorAccentColor.a * alpha;
                seamSr.color = c;
            }

            yield return null;
        }

        // Fade out edge highlights
        foreach (Transform child in transform)
        {
            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = 0f;
                sr.color = c;
            }
        }

        isOpen = true;
        isOpening = false;

        // Cleanup prompt UI
        if (promptCanvas != null)
            Destroy(promptCanvas);

        // Auto-close: wait for player to walk through, then close and lock
        if (autoClose && player != null)
        {
            // Wait until the player has moved past the door
            yield return new WaitUntil(() =>
                player == null || Mathf.Abs(player.position.y - transform.position.y) > 2.5f);

            // Extra grace period
            yield return new WaitForSeconds(0.3f);

            CloseDoor();
        }
    }

    /// <summary>
    /// Closes and locks the door with a slide-shut animation.
    /// </summary>
    private void CloseDoor()
    {
        isOpen = false;
        isLocked = true;

        // Re-enable collider
        if (doorCollider != null)
            doorCollider.enabled = true;

        // Snap panels back to closed position
        if (leftPanel != null)
            leftPanel.transform.localPosition = new Vector3(-doorWidth / 4f, 0, 0);
        if (rightPanel != null)
            rightPanel.transform.localPosition = new Vector3(doorWidth / 4f, 0, 0);

        // Restore all visual alpha
        RestoreAlpha(leftPanel, doorColor.a);
        RestoreAlpha(rightPanel, doorColor.a);

        if (centerLine != null)
        {
            SpriteRenderer sr = centerLine.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = doorAccentColor.a;
                sr.color = c;
            }
        }

        // Restore edge highlights
        foreach (Transform child in transform)
        {
            if (child.gameObject.name.StartsWith("Edge"))
            {
                SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = doorAccentColor.a;
                    sr.color = c;
                }
            }
        }
    }

    /// <summary>
    /// Locks the door shut — slides panels back together, re-enables collider.
    /// Called externally (e.g., boss arena locks behind player).
    /// </summary>
    public void LockDoor()
    {
        if (!isOpen && !isOpening) return;

        StopAllCoroutines();
        isOpen = false;
        isOpening = false;
        isLocked = true;

        // Re-enable collider
        if (doorCollider != null)
            doorCollider.enabled = true;

        // Snap panels back to closed position
        if (leftPanel != null)
            leftPanel.transform.localPosition = new Vector3(-doorWidth / 4f, 0, 0);
        if (rightPanel != null)
            rightPanel.transform.localPosition = new Vector3(doorWidth / 4f, 0, 0);

        // Restore all visual alpha
        RestoreAlpha(leftPanel, doorColor.a);
        RestoreAlpha(rightPanel, doorColor.a);

        if (centerLine != null)
        {
            SpriteRenderer sr = centerLine.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = doorAccentColor.a;
                sr.color = c;
            }
        }

        // Restore edge highlights
        foreach (Transform child in transform)
        {
            if (child.gameObject.name.StartsWith("Edge"))
            {
                SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = doorAccentColor.a;
                    sr.color = c;
                }
            }
        }

        // Kill prompt if it exists
        if (promptCanvas != null)
            Destroy(promptCanvas);
    }

    /// <summary>
    /// Unlocks a locked door so the player can interact with it again.
    /// </summary>
    public void UnlockDoor()
    {
        isLocked = false;
    }

    /// <summary>
    /// Fully resets the door to its initial closed, interactive state.
    /// Called on player respawn to restore all doors.
    /// </summary>
    public void ResetDoor()
    {
        StopAllCoroutines();
        isOpen = false;
        isOpening = false;
        isLocked = startLocked; // respect initial lock state

        // Re-enable collider
        if (doorCollider != null)
            doorCollider.enabled = true;

        // Snap panels back to closed position
        if (leftPanel != null)
            leftPanel.transform.localPosition = new Vector3(-doorWidth / 4f, 0, 0);
        if (rightPanel != null)
            rightPanel.transform.localPosition = new Vector3(doorWidth / 4f, 0, 0);

        // Restore all visual alpha
        RestoreAlpha(leftPanel, doorColor.a);
        RestoreAlpha(rightPanel, doorColor.a);

        if (centerLine != null)
        {
            SpriteRenderer sr = centerLine.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = doorAccentColor.a;
                sr.color = c;
            }
        }

        // Restore edge highlights
        foreach (Transform child in transform)
        {
            if (child.gameObject.name.StartsWith("Edge"))
            {
                SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = doorAccentColor.a;
                    sr.color = c;
                }
            }
        }

        // Rebuild prompt UI if it was destroyed
        if (promptCanvas == null)
        {
            BuildPromptUI();
        }
    }

    private void RestoreAlpha(GameObject panel, float alpha)
    {
        if (panel == null) return;

        SpriteRenderer sr = panel.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }

        foreach (Transform child in panel.transform)
        {
            SpriteRenderer csr = child.GetComponent<SpriteRenderer>();
            if (csr != null)
            {
                Color c = csr.color;
                c.a = doorAccentColor.a;
                csr.color = c;
            }
        }
    }

    private void SetChildAlpha(GameObject panel, float alpha)
    {
        SpriteRenderer sr = panel.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a = doorColor.a * alpha;
            sr.color = c;
        }

        foreach (Transform child in panel.transform)
        {
            SpriteRenderer csr = child.GetComponent<SpriteRenderer>();
            if (csr != null)
            {
                Color c = csr.color;
                c.a = Mathf.Min(c.a, alpha);
                csr.color = c;
            }
        }
    }

    private void OnDestroy()
    {
        if (promptCanvas != null)
            Destroy(promptCanvas);
    }

    private Sprite GetSquare()
    {
        if (_sq != null) return _sq;
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        _sq = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4);
        return _sq;
    }
}
