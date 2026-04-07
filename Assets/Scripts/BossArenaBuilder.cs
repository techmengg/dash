using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds pixel-art visual overlays to the Boss Arena scene.
/// Attach to an empty GameObject. Does NOT create walls/floor (those exist in the scene).
/// Adds: grid lines, wall edge highlights, corner accents, inner border glow.
/// </summary>
public class BossArenaBuilder : MonoBehaviour
{
    [Header("Arena Size (must match scene walls)")]
    public float arenaWidth = 25f;
    public float arenaHeight = 15f;

    [Header("Visual Style")]
    public Color gridColor = new Color(0.18f, 0.18f, 0.24f, 1f);
    public Color wallEdgeColor = new Color(0.45f, 0.35f, 0.6f, 1f);
    public Color borderGlowColor = new Color(0.5f, 0.3f, 0.8f, 0.25f);
    public Color cornerAccentColor = new Color(0.5f, 0.4f, 0.7f, 0.5f);

    [Header("Grid")]
    public bool showGrid = true;
    public float gridSpacing = 2f;
    public float gridLineWidth = 0.03f;

    [Header("Corner Accents")]
    public bool showCornerAccents = true;
    public float cornerSize = 2f;
    public float cornerThickness = 0.07f;

    [Header("Border Glow")]
    public bool showBorderGlow = true;
    public float glowWidth = 0.4f;

    [Header("Wall Edge Lines")]
    public bool showWallEdges = true;
    public float edgeWidth = 0.06f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != "Boss Arena")
            return;

        // Auto-add to scene if not already present
        if (FindFirstObjectByType<BossArenaBuilder>() != null)
            return;

        // Only add in scenes that have walls tagged "Walls"
        GameObject[] walls = GameObject.FindGameObjectsWithTag("Walls");
        if (walls.Length == 0)
            return;

        GameObject go = new GameObject("ArenaVisuals");
        go.AddComponent<BossArenaBuilder>();
    }

    private void Awake()
    {
        BuildOverlays();
    }

    private void BuildOverlays()
    {
        float hw = arenaWidth / 2f - 0.5f; // inset from wall center
        float hh = arenaHeight / 2f - 0.5f;

        if (showGrid) BuildGrid(hw, hh);
        if (showWallEdges) BuildWallEdges(hw, hh);
        if (showCornerAccents) BuildCorners(hw, hh);
        if (showBorderGlow) BuildGlow(hw, hh);
    }

    private void BuildGrid(float hw, float hh)
    {
        GameObject parent = new GameObject("Grid");
        parent.transform.SetParent(transform, false);

        for (float x = -hw + gridSpacing; x < hw; x += gridSpacing)
        {
            CreateSprite("GV", new Vector3(x, 0, 0),
                new Vector3(gridLineWidth, hh * 2f, 1f), gridColor, -1)
                .transform.SetParent(parent.transform, false);
        }

        for (float y = -hh + gridSpacing; y < hh; y += gridSpacing)
        {
            CreateSprite("GH", new Vector3(0, y, 0),
                new Vector3(hw * 2f, gridLineWidth, 1f), gridColor, -1)
                .transform.SetParent(parent.transform, false);
        }
    }

    private void BuildWallEdges(float hw, float hh)
    {
        // Bright thin lines along inner edges of each wall
        CreateSprite("EdgeL", new Vector3(-hw, 0, 0), new Vector3(edgeWidth, hh * 2f, 1f), wallEdgeColor, 1)
            .transform.SetParent(transform, false);
        CreateSprite("EdgeR", new Vector3(hw, 0, 0), new Vector3(edgeWidth, hh * 2f, 1f), wallEdgeColor, 1)
            .transform.SetParent(transform, false);
        CreateSprite("EdgeT", new Vector3(0, hh, 0), new Vector3(hw * 2f, edgeWidth, 1f), wallEdgeColor, 1)
            .transform.SetParent(transform, false);
        CreateSprite("EdgeB", new Vector3(0, -hh, 0), new Vector3(hw * 2f, edgeWidth, 1f), wallEdgeColor, 1)
            .transform.SetParent(transform, false);
    }

    private void BuildCorners(float hw, float hh)
    {
        float cs = cornerSize;
        float ct = cornerThickness;

        Vector2[] corners = {
            new Vector2(-hw, -hh),
            new Vector2(hw, -hh),
            new Vector2(-hw, hh),
            new Vector2(hw, hh)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 c = corners[i];
            float xDir = c.x > 0 ? -1f : 1f;
            float yDir = c.y > 0 ? -1f : 1f;

            // L-shaped accent at each corner
            CreateSprite("CH" + i, new Vector3(c.x + xDir * cs / 2f, c.y, 0),
                new Vector3(cs, ct, 1f), cornerAccentColor, 1)
                .transform.SetParent(transform, false);

            CreateSprite("CV" + i, new Vector3(c.x, c.y + yDir * cs / 2f, 0),
                new Vector3(ct, cs, 1f), cornerAccentColor, 1)
                .transform.SetParent(transform, false);
        }
    }

    private void BuildGlow(float hw, float hh)
    {
        float gw = glowWidth;

        CreateSprite("GL", new Vector3(-hw + gw / 2f, 0, 0), new Vector3(gw, hh * 2f, 1f), borderGlowColor, 0)
            .transform.SetParent(transform, false);
        CreateSprite("GR", new Vector3(hw - gw / 2f, 0, 0), new Vector3(gw, hh * 2f, 1f), borderGlowColor, 0)
            .transform.SetParent(transform, false);
        CreateSprite("GT", new Vector3(0, hh - gw / 2f, 0), new Vector3(hw * 2f, gw, 1f), borderGlowColor, 0)
            .transform.SetParent(transform, false);
        CreateSprite("GB", new Vector3(0, -hh + gw / 2f, 0), new Vector3(hw * 2f, gw, 1f), borderGlowColor, 0)
            .transform.SetParent(transform, false);
    }

    private GameObject CreateSprite(string name, Vector3 pos, Vector3 scale, Color color, int sortOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquare();
        sr.color = color;
        sr.sortingOrder = sortOrder;

        return go;
    }

    private static Sprite _sq;
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
