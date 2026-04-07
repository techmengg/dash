using System.Collections;
using UnityEngine;

/// <summary>
/// Boss slam visual effects inspired by Hollow Knight / Dead Cells.
/// Key techniques: hitpause, afterimages, screen flash, ground-hugging dust, upward debris.
/// Optimized: shared material, batched drivers, minimal GOs.
/// </summary>
public static class SlamEffects
{
    private static Material _sharedMat;
    private static Material SharedMat
    {
        get
        {
            if (_sharedMat == null)
                _sharedMat = new Material(Shader.Find("Sprites/Default"));
            return _sharedMat;
        }
    }

    private static Sprite _circle;
    private static Sprite Circle
    {
        get
        {
            if (_circle != null) return _circle;
            int s = 16;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float c = s / 2f, rSq = c * c;
            for (int x = 0; x < s; x++)
                for (int y = 0; y < s; y++)
                {
                    float dx = x - c + 0.5f, dy = y - c + 0.5f;
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - (dx * dx + dy * dy) / rSq)));
                }
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, s, s), Vector2.one * 0.5f, s);
            return _circle;
        }
    }

    // ==================== HITPAUSE (Hollow Knight style) ====================
    // Freezes the game briefly on impact. THE most important juice technique.

    public static IEnumerator HitPause(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    // ==================== SCREEN SHAKE ====================

    public static IEnumerator ScreenShake(float duration, float magnitude)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        Transform camT = cam.transform;
        Vector3 orig = camT.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // works during hitpause
            float t = 1f - (elapsed / duration);
            float x = Random.Range(-1f, 1f) * magnitude * t;
            float y = Random.Range(-1f, 1f) * magnitude * t;
            camT.localPosition = orig + new Vector3(x, y, 0f);
            yield return null;
        }

        camT.localPosition = orig;
    }

    // ==================== FULL SCREEN FLASH (Hollow Knight style) ====================
    // Brief white overlay covering entire screen. Sells massive impact.

    public static void SpawnScreenFlash(float duration, float startAlpha)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        GameObject go = new GameObject("ScreenFlash");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0, 0, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Circle;
        sr.sortingOrder = 100; // above everything
        sr.color = new Color(1f, 1f, 1f, startAlpha);

        // Scale to fill camera view
        float camH = cam.orthographicSize * 2f;
        float camW = camH * cam.aspect;
        float pixelSize = 1f / Circle.pixelsPerUnit;
        float scaleX = camW / (Circle.texture.width * pixelSize);
        float scaleY = camH / (Circle.texture.height * pixelSize);
        go.transform.localScale = new Vector3(scaleX * 1.5f, scaleY * 1.5f, 1f);

        ScreenFlashDriver d = go.AddComponent<ScreenFlashDriver>();
        d.Init(sr, duration, startAlpha);
    }

    // ==================== AFTERIMAGE (Dead Cells style) ====================
    // Ghost copy of the boss sprite that fades out. Call multiple times during movement.

    public static void SpawnAfterimage(Vector2 pos, Sprite sprite, Vector3 scale, Color tint, float duration, bool flipX)
    {
        GameObject go = new GameObject("Afterimage");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 1; // above ground, below boss
        sr.flipX = flipX;
        sr.color = new Color(tint.r, tint.g, tint.b, 0.5f);

        AfterimageDriver d = go.AddComponent<AfterimageDriver>();
        d.Init(sr, duration);
    }

    // ==================== SHOCKWAVE RING ====================

    public static void SpawnShockwave(Vector2 center, float maxRadius, float duration, Color color, float startWidth)
    {
        GameObject go = new GameObject("Shockwave");
        go.transform.position = new Vector3(center.x, center.y, 0f);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.sortingOrder = 1; // above ground, below boss
        lr.sharedMaterial = SharedMat;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = startWidth;
        lr.endWidth = startWidth;

        int seg = 20;
        lr.positionCount = seg;
        Vector3[] pts = new Vector3[seg];
        for (int i = 0; i < seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            pts[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        }
        lr.SetPositions(pts);

        ShockwaveDriver d = go.AddComponent<ShockwaveDriver>();
        d.Init(lr, pts, maxRadius, duration, color, startWidth);
    }

    // ==================== GROUND CRACKS (batched) ====================

    public static void SpawnGroundCracks(Vector2 center, int count, float maxLen, float duration)
    {
        GameObject go = new GameObject("Cracks");
        CrackBatchDriver d = go.AddComponent<CrackBatchDriver>();
        d.Init(center, count, maxLen, duration, SharedMat);
    }

    // ==================== GROUND DUST PUFFS (Hollow Knight style) ====================
    // Two symmetrical dust clouds expanding LEFT and RIGHT along the ground.
    // Much more realistic than random omnidirectional particles.

    public static void SpawnGroundDustPuffs(Vector2 center, float spreadDist, float duration)
    {
        GameObject go = new GameObject("DustPuffs");
        GroundPuffDriver d = go.AddComponent<GroundPuffDriver>();
        d.Init(center, spreadDist, duration, Circle);
    }

    // ==================== UPWARD DEBRIS (impact chunks flying up) ====================

    public static void SpawnUpwardDebris(Vector2 center, int count)
    {
        GameObject go = new GameObject("Debris");
        DebrisBatchDriver d = go.AddComponent<DebrisBatchDriver>();
        d.Init(center, count, Circle);
    }

    // ==================== LAUNCH DUST (small puff at takeoff point) ====================

    public static void SpawnLaunchDust(Vector2 center)
    {
        GameObject go = new GameObject("LaunchDust");
        go.transform.position = new Vector3(center.x, center.y, 0f);
        go.transform.localScale = Vector3.one * 0.6f;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Circle;
        sr.sortingOrder = 1; // above ground, below boss
        sr.color = new Color(0.7f, 0.65f, 0.5f, 0.5f);

        FlashDriver d = go.AddComponent<FlashDriver>();
        d.Init(sr, 0.3f);
    }

    // ==================== IMPACT FLASH (at slam point) ====================

    public static void SpawnImpactFlash(Vector2 center, float radius, float duration)
    {
        GameObject go = new GameObject("ImpactFlash");
        go.transform.position = new Vector3(center.x, center.y, 0f);
        go.transform.localScale = Vector3.one * radius * 2f;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Circle;
        sr.sortingOrder = 1; // above ground, below boss
        sr.color = new Color(1f, 0.95f, 0.8f, 0.7f);

        FlashDriver d = go.AddComponent<FlashDriver>();
        d.Init(sr, duration);
    }

    // ==================== TELEGRAPH RING (with pulsing fill) ====================

    public static GameObject SpawnTelegraphRing(Vector2 center, float radius, Color color)
    {
        GameObject go = new GameObject("Telegraph");
        go.transform.position = new Vector3(center.x, center.y, 0f);

        // Filled danger zone
        SpriteRenderer fill = go.AddComponent<SpriteRenderer>();
        fill.sprite = Circle;
        fill.sortingOrder = 0; // on ground, above background
        fill.color = new Color(color.r, color.g, color.b, 0.08f);
        go.transform.localScale = Vector3.one * radius * 2f / (Circle.texture.width / Circle.pixelsPerUnit);

        // Outline ring
        GameObject ring = new GameObject("Ring");
        ring.transform.SetParent(go.transform, false);
        ring.transform.localPosition = Vector3.zero;

        LineRenderer lr = ring.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.sortingOrder = 0; // on ground, above background
        lr.sharedMaterial = SharedMat;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = 0.06f;
        lr.endWidth = 0.06f;

        int seg = 20;
        lr.positionCount = seg;
        for (int i = 0; i < seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        return go;
    }

    // ==================== BOSS WHITE FLASH helper ====================
    // Temporarily turns the boss sprite white then restores color.

    public static IEnumerator SpriteWhiteFlash(SpriteRenderer sr, Color restoreColor, int frames)
    {
        if (sr == null) yield break;
        sr.color = Color.white;
        for (int i = 0; i < frames; i++)
            yield return null;
        sr.color = restoreColor;
    }
}

// ==================== DRIVERS ====================

public class ScreenFlashDriver : MonoBehaviour
{
    private SpriteRenderer sr;
    private float duration, elapsed, startAlpha;

    public void Init(SpriteRenderer sr, float duration, float startAlpha)
    {
        this.sr = sr;
        this.duration = duration;
        this.startAlpha = startAlpha;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        Color c = sr.color;
        c.a = Mathf.Lerp(startAlpha, 0f, t);
        sr.color = c;
        if (t >= 1f) Destroy(gameObject);
    }
}

public class AfterimageDriver : MonoBehaviour
{
    private SpriteRenderer sr;
    private float duration, elapsed;
    private float startAlpha;

    public void Init(SpriteRenderer sr, float duration)
    {
        this.sr = sr;
        this.duration = duration;
        this.startAlpha = sr.color.a;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        Color c = sr.color;
        c.a = Mathf.Lerp(startAlpha, 0f, t);
        sr.color = c;
        if (t >= 1f) Destroy(gameObject);
    }
}

public class ShockwaveDriver : MonoBehaviour
{
    private LineRenderer lr;
    private Vector3[] basePts;
    private float maxRadius, duration, elapsed, startWidth;
    private Color startColor;

    public void Init(LineRenderer lr, Vector3[] basePts, float maxRadius, float duration, Color color, float startWidth)
    {
        this.lr = lr;
        this.basePts = basePts;
        this.maxRadius = maxRadius;
        this.duration = duration;
        this.startColor = color;
        this.startWidth = startWidth;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = 1f - (1f - t) * (1f - t);
        float r = eased * maxRadius;

        for (int i = 0; i < basePts.Length; i++)
            lr.SetPosition(i, basePts[i] * r);

        float alpha = Mathf.Lerp(startColor.a, 0f, t);
        Color c = new Color(startColor.r, startColor.g, startColor.b, alpha);
        lr.startColor = c;
        lr.endColor = c;
        lr.startWidth = Mathf.Lerp(startWidth, 0.02f, t);
        lr.endWidth = lr.startWidth;

        if (t >= 1f) Destroy(gameObject);
    }
}

/// <summary>
/// Pixel-art style ground cracks with:
/// - Stepped/pixelated paths (axis-aligned segments like breaking a pixel grid)
/// - Glowing inner line (hot energy core) + dark outer line
/// - Branching sub-cracks that fork off at 90-degree angles
/// - Glow pulses on spawn then fades
/// </summary>
public class CrackBatchDriver : MonoBehaviour
{
    // Outer dark cracks
    private LineRenderer[] outerLrs;
    // Inner glow cracks (thinner, brighter, on top)
    private LineRenderer[] innerLrs;
    // Branch sub-cracks
    private LineRenderer[] branchLrs;
    private Vector3[][] outerTargetPts;
    private Vector3[][] innerTargetPts;
    private Vector3[][] branchTargetPts;
    private float duration, elapsed;
    private const float GROW_TIME = 0.1f;
    private const float GLOW_PEAK_TIME = 0.15f;
    private bool grown;
    private float pixelSize;

    public void Init(Vector2 center, int count, float maxLen, float duration, Material mat)
    {
        this.duration = duration;
        // Pixel snap size — adjust to match your game's pixel density
        pixelSize = 1f / 16f; // 16 pixels per unit

        var outerList = new System.Collections.Generic.List<LineRenderer>();
        var innerList = new System.Collections.Generic.List<LineRenderer>();
        var branchList = new System.Collections.Generic.List<LineRenderer>();
        var outerPtsList = new System.Collections.Generic.List<Vector3[]>();
        var innerPtsList = new System.Collections.Generic.List<Vector3[]>();
        var branchPtsList = new System.Collections.Generic.List<Vector3[]>();

        for (int i = 0; i < count; i++)
        {
            float baseAngle = (i / (float)count) * 360f + Random.Range(-10f, 10f);

            // Build pixel-stepped crack path
            Vector3[] pts = BuildPixelCrackPath(center, baseAngle, maxLen);
            outerPtsList.Add(pts);
            innerPtsList.Add(pts); // same path, different width/color

            // --- OUTER (dark edge) ---
            LineRenderer outer = CreateCrackLine(mat, 1);
            float dark = Random.Range(0.08f, 0.15f);
            Color darkCol = new Color(dark, dark * 0.5f, dark * 0.2f, 1f);
            outer.startColor = darkCol;
            outer.endColor = new Color(darkCol.r, darkCol.g, darkCol.b, 0.4f);
            outer.startWidth = Random.Range(0.16f, 0.22f);
            outer.endWidth = 0.05f;
            outer.positionCount = pts.Length;
            for (int j = 0; j < pts.Length; j++) outer.SetPosition(j, pts[0]);
            outerList.Add(outer);

            // --- INNER (glowing core) ---
            LineRenderer inner = CreateCrackLine(mat, 1);
            Color glowCol = new Color(1f, 0.7f, 0.3f, 0.9f); // hot orange/yellow
            inner.startColor = glowCol;
            inner.endColor = new Color(glowCol.r, glowCol.g, glowCol.b, 0.15f);
            inner.startWidth = Random.Range(0.07f, 0.1f);
            inner.endWidth = 0.02f;
            inner.positionCount = pts.Length;
            for (int j = 0; j < pts.Length; j++) inner.SetPosition(j, pts[0]);
            innerList.Add(inner);

            // --- BRANCHES (fork off at ~90 degrees, 40% chance per crack) ---
            if (pts.Length >= 3 && Random.value < 0.5f)
            {
                // Pick a point along the main crack to branch from
                int branchIdx = Random.Range(1, pts.Length - 1);
                Vector3 branchOrigin = pts[branchIdx];

                // Branch perpendicular to the main crack direction
                Vector3 mainDir = (pts[branchIdx] - pts[branchIdx - 1]).normalized;
                // 90 degree turn (randomly left or right)
                Vector3 perpDir = Random.value > 0.5f
                    ? new Vector3(-mainDir.y, mainDir.x, 0f)
                    : new Vector3(mainDir.y, -mainDir.x, 0f);

                Vector3[] branchPts = BuildPixelBranchPath(branchOrigin, perpDir, maxLen * Random.Range(0.35f, 0.55f));
                branchPtsList.Add(branchPts);

                // Branch outer
                LineRenderer brOuter = CreateCrackLine(mat, 1);
                float brDark = dark * 1.1f;
                Color brDarkCol = new Color(brDark, brDark * 0.5f, brDark * 0.2f, 0.8f);
                brOuter.startColor = brDarkCol;
                brOuter.endColor = new Color(brDarkCol.r, brDarkCol.g, brDarkCol.b, 0.15f);
                brOuter.startWidth = 0.1f;
                brOuter.endWidth = 0.03f;
                brOuter.positionCount = branchPts.Length;
                for (int j = 0; j < branchPts.Length; j++) brOuter.SetPosition(j, branchPts[0]);
                branchList.Add(brOuter);

                // Branch inner glow
                LineRenderer brInner = CreateCrackLine(mat, 1);
                Color brGlow = new Color(1f, 0.8f, 0.4f, 0.6f);
                brInner.startColor = brGlow;
                brInner.endColor = new Color(brGlow.r, brGlow.g, brGlow.b, 0.05f);
                brInner.startWidth = 0.045f;
                brInner.endWidth = 0.015f;
                brInner.positionCount = branchPts.Length;
                for (int j = 0; j < branchPts.Length; j++) brInner.SetPosition(j, branchPts[0]);
                branchList.Add(brInner);
            }
        }

        outerLrs = outerList.ToArray();
        innerLrs = innerList.ToArray();
        branchLrs = branchList.ToArray();
        outerTargetPts = outerPtsList.ToArray();
        innerTargetPts = innerPtsList.ToArray();
        branchTargetPts = branchPtsList.ToArray();
    }

    /// <summary>
    /// Builds a pixel-snapped crack path — moves in axis-aligned steps
    /// like cracking along a pixel grid. Gives that digital/retro feel.
    /// </summary>
    private Vector3[] BuildPixelCrackPath(Vector2 center, float angleDeg, float maxLen)
    {
        var points = new System.Collections.Generic.List<Vector3>();
        Vector3 current = new Vector3(PixSnap(center.x), PixSnap(center.y), 0f);
        points.Add(current);

        float angle = angleDeg * Mathf.Deg2Rad;
        float remaining = maxLen;
        int steps = Random.Range(6, 12); // more steps = more pixelated detail
        float avgStep = maxLen / steps;

        for (int s = 0; s < steps && remaining > 0.01f; s++)
        {
            // Alternate between horizontal and vertical steps (pixel grid movement)
            float stepLen = avgStep * Random.Range(0.5f, 1.5f);
            stepLen = Mathf.Min(stepLen, remaining);

            // Slight angle drift for organic feel
            angle += Random.Range(-0.4f, 0.4f);

            // Snap to axis-aligned movement: pick dominant axis
            float dx = Mathf.Cos(angle);
            float dy = Mathf.Sin(angle);

            Vector3 step;
            if (s % 2 == 0 || Mathf.Abs(dx) > Mathf.Abs(dy))
            {
                // Horizontal step
                step = new Vector3(Mathf.Sign(dx) * stepLen, 0f, 0f);
            }
            else
            {
                // Vertical step
                step = new Vector3(0f, Mathf.Sign(dy) * stepLen, 0f);
            }

            // Add the corner point (where it turns — the pixel step)
            Vector3 next = new Vector3(
                PixSnap(current.x + step.x),
                PixSnap(current.y + step.y),
                0f
            );

            // Occasionally add a small jog perpendicular (1-2 pixels) for jaggedness
            if (Random.value < 0.35f)
            {
                float jogSize = pixelSize * Random.Range(1, 3);
                Vector3 jog;
                if (step.x != 0)
                    jog = new Vector3(0f, (Random.value > 0.5f ? 1f : -1f) * jogSize, 0f);
                else
                    jog = new Vector3((Random.value > 0.5f ? 1f : -1f) * jogSize, 0f, 0f);

                Vector3 jogPt = new Vector3(PixSnap(current.x + jog.x), PixSnap(current.y + jog.y), 0f);
                points.Add(jogPt);
            }

            points.Add(next);
            current = next;
            remaining -= stepLen;
        }

        return points.ToArray();
    }

    /// <summary>
    /// Builds a short pixel-stepped branch crack.
    /// </summary>
    private Vector3[] BuildPixelBranchPath(Vector3 origin, Vector3 dir, float length)
    {
        var points = new System.Collections.Generic.List<Vector3>();
        Vector3 current = new Vector3(PixSnap(origin.x), PixSnap(origin.y), 0f);
        points.Add(current);

        int steps = Random.Range(3, 6);
        float avgStep = length / steps;

        for (int s = 0; s < steps; s++)
        {
            float stepLen = avgStep * Random.Range(0.6f, 1.4f);

            // Snap to axis
            Vector3 step;
            if (s % 2 == 0)
                step = new Vector3(Mathf.Sign(dir.x) * stepLen, 0f, 0f);
            else
                step = new Vector3(0f, Mathf.Sign(dir.y != 0 ? dir.y : 1f) * stepLen, 0f);

            Vector3 next = new Vector3(PixSnap(current.x + step.x), PixSnap(current.y + step.y), 0f);
            points.Add(next);
            current = next;
        }

        return points.ToArray();
    }

    private float PixSnap(float val)
    {
        return Mathf.Round(val / pixelSize) * pixelSize;
    }

    private LineRenderer CreateCrackLine(Material mat, int sortOrder)
    {
        GameObject child = new GameObject("CL");
        child.transform.SetParent(transform, false);
        LineRenderer lr = child.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.sortingOrder = sortOrder;
        lr.sharedMaterial = mat;
        return lr;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        // --- GROW PHASE: cracks shoot outward ---
        if (elapsed < GROW_TIME)
        {
            float t = elapsed / GROW_TIME;
            float eased = t * t;
            GrowLines(outerLrs, outerTargetPts, eased);
            GrowLines(innerLrs, innerTargetPts, eased);

            // Branches grow slightly delayed
            float branchT = Mathf.Clamp01((t - 0.3f) / 0.7f);
            float branchEased = branchT * branchT;
            GrowBranches(branchEased);
        }
        else if (!grown)
        {
            grown = true;
            FinishGrow(outerLrs, outerTargetPts);
            FinishGrow(innerLrs, innerTargetPts);
            FinishBranches();
        }

        // --- GLOW PHASE: inner lines pulse bright then dim ---
        float glowFactor;
        if (elapsed < GLOW_PEAK_TIME)
        {
            // Bright flash on impact
            glowFactor = 1f;
        }
        else if (elapsed < GLOW_PEAK_TIME + 0.4f)
        {
            // Rapid dim
            float gt = (elapsed - GLOW_PEAK_TIME) / 0.4f;
            glowFactor = Mathf.Lerp(1f, 0.2f, gt);
        }
        else
        {
            // Slow ember fade
            float gt = Mathf.Clamp01((elapsed - GLOW_PEAK_TIME - 0.4f) / (duration * 0.5f));
            glowFactor = Mathf.Lerp(0.2f, 0f, gt);
        }

        // Apply glow to inner lines
        for (int i = 0; i < innerLrs.Length; i++)
        {
            Color c = innerLrs[i].startColor;
            c.a = 0.9f * glowFactor;
            innerLrs[i].startColor = c;
            Color e = innerLrs[i].endColor;
            e.a = 0.15f * glowFactor;
            innerLrs[i].endColor = e;
            innerLrs[i].startWidth = Mathf.Lerp(0.03f, 0.1f, glowFactor);
        }

        // Apply glow to branch inner lines (every other is inner)
        for (int i = 1; i < branchLrs.Length; i += 2)
        {
            Color c = branchLrs[i].startColor;
            c.a = 0.6f * glowFactor;
            branchLrs[i].startColor = c;
        }

        // --- FADE PHASE: everything fades out ---
        float fadeStart = duration * 0.6f;
        if (elapsed > fadeStart)
        {
            float fadeT = Mathf.Clamp01((elapsed - fadeStart) / (duration - fadeStart));
            float a = 1f - fadeT;

            // Fade outer cracks
            for (int i = 0; i < outerLrs.Length; i++)
            {
                Color c = outerLrs[i].startColor;
                c.a = a;
                outerLrs[i].startColor = c;
                outerLrs[i].endColor = new Color(c.r, c.g, c.b, a * 0.4f);
            }

            // Fade branch outers
            for (int i = 0; i < branchLrs.Length; i += 2)
            {
                Color c = branchLrs[i].startColor;
                c.a = a * 0.8f;
                branchLrs[i].startColor = c;
                branchLrs[i].endColor = new Color(c.r, c.g, c.b, a * 0.15f);
            }
        }

        if (elapsed >= duration) Destroy(gameObject);
    }

    private void GrowLines(LineRenderer[] lines, Vector3[][] targets, float progress)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            var pts = targets[i];
            // Grow point by point — each point appears in sequence
            int totalPts = pts.Length;
            float ptsRevealed = 1f + progress * (totalPts - 1);
            int fullPts = Mathf.FloorToInt(ptsRevealed);

            for (int j = 1; j < totalPts; j++)
            {
                if (j < fullPts)
                    lines[i].SetPosition(j, pts[j]);
                else if (j == fullPts)
                {
                    float frac = ptsRevealed - fullPts;
                    lines[i].SetPosition(j, Vector3.Lerp(pts[j - 1], pts[j], frac));
                }
                else
                    lines[i].SetPosition(j, j > 0 ? pts[Mathf.Min(fullPts, totalPts - 1)] : pts[0]);
            }
        }
    }

    private void GrowBranches(float progress)
    {
        int branchIdx = 0;
        for (int i = 0; i < branchLrs.Length; i += 2)
        {
            if (branchIdx >= branchTargetPts.Length) break;
            var pts = branchTargetPts[branchIdx];
            int totalPts = pts.Length;
            float ptsRevealed = 1f + progress * (totalPts - 1);
            int fullPts = Mathf.FloorToInt(ptsRevealed);

            // Update both outer (i) and inner (i+1) branch lines
            for (int li = 0; li < 2 && i + li < branchLrs.Length; li++)
            {
                for (int j = 1; j < totalPts; j++)
                {
                    if (j < fullPts)
                        branchLrs[i + li].SetPosition(j, pts[j]);
                    else if (j == fullPts)
                    {
                        float frac = ptsRevealed - fullPts;
                        branchLrs[i + li].SetPosition(j, Vector3.Lerp(pts[j - 1], pts[j], frac));
                    }
                    else
                        branchLrs[i + li].SetPosition(j, pts[Mathf.Min(fullPts, totalPts - 1)]);
                }
            }
            branchIdx++;
        }
    }

    private void FinishGrow(LineRenderer[] lines, Vector3[][] targets)
    {
        for (int i = 0; i < lines.Length; i++)
            for (int j = 0; j < targets[i].Length; j++)
                lines[i].SetPosition(j, targets[i][j]);
    }

    private void FinishBranches()
    {
        int branchIdx = 0;
        for (int i = 0; i < branchLrs.Length; i += 2)
        {
            if (branchIdx >= branchTargetPts.Length) break;
            var pts = branchTargetPts[branchIdx];
            for (int li = 0; li < 2 && i + li < branchLrs.Length; li++)
                for (int j = 0; j < pts.Length; j++)
                    branchLrs[i + li].SetPosition(j, pts[j]);
            branchIdx++;
        }
    }
}

/// <summary>
/// Two dust clouds that expand horizontally along the ground — Hollow Knight style.
/// Much more realistic than random particles.
/// </summary>
public class GroundPuffDriver : MonoBehaviour
{
    private Transform leftPuff, rightPuff;
    private SpriteRenderer leftSr, rightSr;
    private float duration, elapsed;
    private Vector2 center;
    private float spreadDist;

    public void Init(Vector2 center, float spreadDist, float duration, Sprite sprite)
    {
        this.center = center;
        this.spreadDist = spreadDist;
        this.duration = duration;

        // Left puff
        GameObject l = new GameObject("L");
        l.transform.SetParent(transform, false);
        l.transform.position = new Vector3(center.x, center.y, 0f);
        leftSr = l.AddComponent<SpriteRenderer>();
        leftSr.sprite = sprite;
        leftSr.sortingOrder = 1; // above ground, below boss
        leftSr.color = new Color(0.65f, 0.6f, 0.5f, 0.6f);
        l.transform.localScale = new Vector3(0.3f, 0.25f, 1f);
        leftPuff = l.transform;

        // Right puff
        GameObject r = new GameObject("R");
        r.transform.SetParent(transform, false);
        r.transform.position = new Vector3(center.x, center.y, 0f);
        rightSr = r.AddComponent<SpriteRenderer>();
        rightSr.sprite = sprite;
        rightSr.sortingOrder = 1; // above ground, below boss
        rightSr.color = new Color(0.65f, 0.6f, 0.5f, 0.6f);
        r.transform.localScale = new Vector3(0.3f, 0.25f, 1f);
        rightPuff = r.transform;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // Ease out for deceleration
        float eased = 1f - (1f - t) * (1f - t);

        // Spread horizontally
        float dist = eased * spreadDist;
        leftPuff.position = new Vector3(center.x - dist, center.y, 0f);
        rightPuff.position = new Vector3(center.x + dist, center.y, 0f);

        // Scale up then slightly shrink (like real dust expanding then dissipating)
        float scaleX, scaleY;
        if (t < 0.3f)
        {
            float st = t / 0.3f;
            scaleX = Mathf.Lerp(0.3f, 1.2f, st);
            scaleY = Mathf.Lerp(0.25f, 0.7f, st);
        }
        else
        {
            float st = (t - 0.3f) / 0.7f;
            scaleX = Mathf.Lerp(1.2f, 0.8f, st);
            scaleY = Mathf.Lerp(0.7f, 0.3f, st);
        }
        leftPuff.localScale = new Vector3(scaleX, scaleY, 1f);
        rightPuff.localScale = new Vector3(scaleX, scaleY, 1f);

        // Rise slightly (dust floats up)
        float rise = t * 0.3f;
        Vector3 lp = leftPuff.position; lp.y += rise; leftPuff.position = lp;
        Vector3 rp = rightPuff.position; rp.y += rise; rightPuff.position = rp;

        // Fade
        float alpha = Mathf.Lerp(0.6f, 0f, t);
        leftSr.color = new Color(leftSr.color.r, leftSr.color.g, leftSr.color.b, alpha);
        rightSr.color = new Color(rightSr.color.r, rightSr.color.g, rightSr.color.b, alpha);

        if (t >= 1f) Destroy(gameObject);
    }
}

/// <summary>
/// Small chunks that fly UPWARD then fall with gravity — like rubble from a ground impact.
/// All driven from a single Update.
/// </summary>
public class DebrisBatchDriver : MonoBehaviour
{
    private Transform[] chunks;
    private SpriteRenderer[] renderers;
    private Vector2[] velocities;
    private Vector2[] positions;
    private float elapsed;
    private const float GRAVITY = 12f;
    private const float LIFETIME = 0.7f;

    public void Init(Vector2 center, int count, Sprite sprite)
    {
        chunks = new Transform[count];
        renderers = new SpriteRenderer[count];
        velocities = new Vector2[count];
        positions = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            // Upward with slight horizontal spread
            float xSpread = Random.Range(-3f, 3f);
            float yForce = Random.Range(5f, 9f);
            velocities[i] = new Vector2(xSpread, yForce);
            positions[i] = center + new Vector2(Random.Range(-0.3f, 0.3f), 0f);

            GameObject go = new GameObject("D");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(positions[i].x, positions[i].y, 0f);

            float size = Random.Range(0.06f, 0.14f);
            go.transform.localScale = Vector3.one * size;

            // Vary between brown/grey for rock debris look
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 1; // above ground, below boss
            float r = Random.Range(0.25f, 0.5f);
            float g = r * Random.Range(0.7f, 0.9f);
            float b = g * Random.Range(0.5f, 0.8f);
            sr.color = new Color(r, g, b, 1f);

            // Random rotation
            go.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            chunks[i] = go.transform;
            renderers[i] = sr;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float dt = Time.deltaTime;

        if (elapsed >= LIFETIME)
        {
            Destroy(gameObject);
            return;
        }

        float t = elapsed / LIFETIME;

        for (int i = 0; i < chunks.Length; i++)
        {
            // Apply gravity
            velocities[i].y -= GRAVITY * dt;
            positions[i] += velocities[i] * dt;
            chunks[i].position = new Vector3(positions[i].x, positions[i].y, 0f);

            // Spin
            chunks[i].Rotate(0, 0, velocities[i].x * 50f * dt);

            // Fade in last 30%
            if (t > 0.7f)
            {
                float fadeT = (t - 0.7f) / 0.3f;
                Color c = renderers[i].color;
                c.a = 1f - fadeT;
                renderers[i].color = c;
            }
        }
    }
}

public class FlashDriver : MonoBehaviour
{
    private SpriteRenderer sr;
    private float duration, elapsed, baseScale;

    public void Init(SpriteRenderer sr, float duration)
    {
        this.sr = sr;
        this.duration = duration;
        this.baseScale = transform.localScale.x;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.5f, t) * baseScale;

        Color c = sr.color;
        c.a = Mathf.Lerp(sr.color.a, 0f, t * t);
        sr.color = c;

        if (t >= 1f) Destroy(gameObject);
    }
}
