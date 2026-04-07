using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds a two-room flow: Spawn Room -> Battle Arena.
/// Creates all physics walls + visual overlays at runtime.
/// Theme: dark reds, crimson, with green accents.
/// </summary>
public class BattleArenaBuilder : MonoBehaviour
{
    // ── Layout Constants ──────────────────────────────────────────
    // Battle arena remains in the lower part of the scene.

    [Header("Battle Arena")]
    public float battleWidth = 30f;
    public float battleHeight = 36f;
    // Battle arena: center (0, -27.5), walls at x=±15.5, top y=-18.5, bottom y=-36.5

    [Header("Spawn Hallway (Battle→Spawn)")]
    public float spawnHallwayWidth = 5f;
    public float spawnHallwayLength = 8f;

    [Header("Spawn Room (safe)")]
    public float spawnWidth = 16f;
    public float spawnHeight = 10f;

    [Header("Wall Thickness")]
    public float wallThick = 1f;

    [Header("Battle Arena Colors")]
    public Color floorColor = new Color(0.08f, 0.04f, 0.05f, 1f);
    public Color wallColor = new Color(0.22f, 0.08f, 0.10f, 1f);
    public Color gridColor = new Color(0.14f, 0.06f, 0.08f, 1f);
    public Color wallEdgeColor = new Color(0.65f, 0.12f, 0.12f, 1f);
    public Color borderGlowColor = new Color(0.55f, 0.08f, 0.08f, 0.22f);
    public Color cornerAccentColor = new Color(0.6f, 0.18f, 0.15f, 0.55f);

    [Header("Spawn Room Colors")]
    public Color spawnFloorColor = new Color(0.06f, 0.06f, 0.08f, 1f);
    public Color spawnWallColor = new Color(0.14f, 0.12f, 0.18f, 1f);
    public Color spawnEdgeColor = new Color(0.25f, 0.2f, 0.4f, 1f);
    public Color spawnGlowColor = new Color(0.15f, 0.25f, 0.5f, 0.2f);
    public Color spawnAccentColor = new Color(0.1f, 0.6f, 0.3f, 0.4f);

    [Header("Spawn Hallway Colors")]
    public Color spawnHallFloorColor = new Color(0.05f, 0.05f, 0.07f, 1f);
    public Color spawnHallWallColor = new Color(0.16f, 0.10f, 0.14f, 1f);
    public Color spawnHallEdgeColor = new Color(0.35f, 0.15f, 0.2f, 1f);

    [Header("Door / Green Accents")]
    public Color doorFrameColor = new Color(0.35f, 0.10f, 0.10f, 1f);
    public Color doorGlowColor = new Color(0.1f, 0.7f, 0.25f, 0.6f);
    public Color doorRuneColor = new Color(0.15f, 0.85f, 0.35f, 0.45f);
    public Color greenAccentColor = new Color(0.08f, 0.5f, 0.2f, 0.35f);
    public Color torchColor = new Color(0.7f, 0.25f, 0.08f, 0.5f);

    [Header("Visual Settings")]
    public float gridSpacing = 2.5f;
    public float gridLineWidth = 0.03f;
    public float edgeWidth = 0.06f;
    public float cornerSize = 2.5f;
    public float cornerThickness = 0.08f;
    public float glowWidth = 0.5f;

    [Header("Boss Intro Defaults")]
    [Min(0f)] public float bossIntroPanInDuration = 1.4f;
    [Min(0f)] public float bossIntroFocusHoldDuration = 1.2f;
    [Min(0f)] public float bossIntroPostWakeHoldDuration = 0.9f;
    [Min(0f)] public float bossIntroPanOutDuration = 1.2f;
    [Min(1f)] public float bossRoomCameraSize = 12f;

    // Computed positions
    private float battleCenterY;
    private float battleTopY;
    private float battleBottomY;
    private float battleHalfW;
    private float battleHalfH;

    // Spawn hallway + room positions
    private float spawnHallTopY;
    private float spawnHallBottomY;
    private float spawnHallHalfW;
    private float spawnCenterY;
    private float spawnTopY;
    private float spawnBottomY;
    private float spawnHalfW;
    private float spawnHalfH;

    private static Sprite _sq;

    /// <summary>
    /// Returns the spawn room center position for player respawn.
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        return new Vector3(0, spawnCenterY, 0);
    }

    public Vector2 GetBattleCenterPosition() => new Vector2(0f, battleCenterY);

    public void GetBattleArenaBounds(float margin, out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = -battleHalfW + margin;
        maxX = battleHalfW - margin;
        minY = battleBottomY + margin;
        maxY = battleTopY - margin;
    }

    public float GetBattleCenterY() => battleCenterY;
    public float GetBattleHalfW() => battleHalfW;
    public float GetBattleHalfH() => battleHalfH;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != "Boss Arena")
            return;

        if (FindFirstObjectByType<BattleArenaBuilder>() != null)
            return;

        GameObject[] walls = GameObject.FindGameObjectsWithTag("Walls");
        if (walls.Length == 0)
            return;

        GameObject go = new GameObject("BattleArena");
        go.AddComponent<BattleArenaBuilder>();
    }

    private void Awake()
    {
        ComputePositions();
        CleanupLegacyFormerBossSection();
        CleanupGlobalLegacyObjects();

        // If this scene already has a generated arena layout, avoid rebuilding and
        // stacking colliders. Also clear legacy blocker walls that can seal openings.
        bool hasAnyDoorBarrier = transform.Find("DoorTransitionBarrier") != null
            || transform.Find("DoorSpawnBarrier") != null;

        if (hasAnyDoorBarrier)
        {
            bool hasSplitBottom = transform.Find("BattleBottomL") != null || transform.Find("BattleBottomR") != null;
            if (hasSplitBottom)
                DestroyChildrenByName("BattleWallB");

            EnsureTwoRoomBattleTopWall();

            MovePlayerToSpawnRoom();
            return;
        }

        BuildBattleArenaWalls();
        BuildFloors();
        // Intentionally no decorative visual overlays in the simplified layout.

        // Spawn room + hallway
        SplitBattleBottomWall();
        BuildSpawnHallwayWalls();
        BuildSpawnRoomWalls();
        BuildSpawnFloors();
        // Intentionally no decorative visual overlays in the simplified layout.

        // All doors — clear separators at every arena entrance/exit
        BuildAllDoors();

        // Area title trigger
        BuildBattleArenaTrigger();

        MovePlayerToSpawnRoom();
    }

    private void ComputePositions()
    {
        battleTopY = -18.5f;
        battleBottomY = battleTopY - battleHeight;    // -36.5
        battleCenterY = (battleTopY + battleBottomY) / 2f; // -27.5
        battleHalfW = battleWidth / 2f;               // 15
        battleHalfH = battleHeight / 2f;              // 9

        // Spawn hallway: from battle arena bottom downward
        spawnHallHalfW = spawnHallwayWidth / 2f;
        spawnHallTopY = battleBottomY - wallThick / 2f;   // -37
        spawnHallBottomY = spawnHallTopY - spawnHallwayLength; // -45

        // Spawn room
        spawnTopY = spawnHallBottomY;                      // -45
        spawnBottomY = spawnTopY - spawnHeight;            // -55
        spawnCenterY = (spawnTopY + spawnBottomY) / 2f;   // -50
        spawnHalfW = spawnWidth / 2f;                      // 8
        spawnHalfH = spawnHeight / 2f;                     // 5
    }

    // ── PHYSICS WALLS ─────────────────────────────────────────────

    private void BuildBattleArenaWalls()
    {
        float bw = battleWidth / 2f + wallThick / 2f; // wall center offset

        // Left wall
        CreateWall("BattleWallL", new Vector3(-bw, battleCenterY, 0),
            new Vector3(wallThick, battleHeight + wallThick, 1f), wallColor);

        // Right wall
        CreateWall("BattleWallR", new Vector3(bw, battleCenterY, 0),
            new Vector3(wallThick, battleHeight + wallThick, 1f), wallColor);

        // Bottom wall
        CreateWall("BattleWallB", new Vector3(0, battleBottomY - wallThick / 2f, 0),
            new Vector3(battleWidth + wallThick * 2f, wallThick, 1f), wallColor);

        // Two-room layout: seal the top edge; no former-boss connector.
        CreateWall("BattleWallT", new Vector3(0, battleTopY + wallThick / 2f, 0),
            new Vector3(battleWidth + wallThick * 2f, wallThick, 1f), wallColor);
    }

    private GameObject CreateWall(string name, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.layer = LayerMask.NameToLayer("Wall");
        go.tag = "Walls";

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquare();
        sr.color = color;
        sr.sortingOrder = 0;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        // size (1,1) works because collider scales with transform
        col.size = Vector2.one;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        return go;
    }

    // ── FLOORS ────────────────────────────────────────────────────

    private void BuildFloors()
    {
        // Battle arena floor
        CreateFloor("BattleFloor", new Vector3(0, battleCenterY, 0),
            new Vector3(battleWidth, battleHeight, 1f), floorColor);
    }

    private GameObject CreateFloor(string name, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquare();
        sr.color = color;
        sr.sortingOrder = -2;

        return go;
    }

    // ── BATTLE ARENA VISUALS ──────────────────────────────────────

    private void BuildBattleArenaVisuals()
    {
        float hw = battleHalfW - 0.5f;  // inset from wall center
        float hh = battleHalfH - 0.5f;
        float cy = battleCenterY;

        GameObject parent = new GameObject("BattleVisuals");
        parent.transform.SetParent(transform, false);

        // Grid
        BuildGrid(parent.transform, hw, hh, cy);

        // Wall edge highlights
        BuildWallEdges(parent.transform, hw, hh, cy);

        // Corner accents
        BuildCorners(parent.transform, hw, hh, cy);

        // Border glow
        BuildBorderGlow(parent.transform, hw, hh, cy);

        // Danger stripe patterns along walls
        BuildDangerStripes(parent.transform, hw, hh, cy);

        // Green accent runes scattered on floor
        BuildFloorRunes(parent.transform, hw, hh, cy);

        // Torch glows at corners
        BuildTorchGlows(parent.transform, hw, hh, cy);
    }

    private void BuildGrid(Transform parent, float hw, float hh, float cy)
    {
        GameObject gridParent = new GameObject("Grid");
        gridParent.transform.SetParent(parent, false);

        // Vertical lines
        for (float x = -hw + gridSpacing; x < hw; x += gridSpacing)
        {
            MakeSprite("GV", new Vector3(x, cy, 0),
                new Vector3(gridLineWidth, hh * 2f, 1f), gridColor, -1)
                .transform.SetParent(gridParent.transform, false);
        }

        // Horizontal lines
        for (float y = -hh + gridSpacing; y < hh; y += gridSpacing)
        {
            MakeSprite("GH", new Vector3(0, cy + y, 0),
                new Vector3(hw * 2f, gridLineWidth, 1f), gridColor, -1)
                .transform.SetParent(gridParent.transform, false);
        }

        // Cross-hatch diagonal accents (subtle)
        Color diagColor = new Color(gridColor.r * 0.7f, gridColor.g * 0.7f, gridColor.b * 0.7f, 0.3f);
        for (float x = -hw + gridSpacing * 2f; x < hw; x += gridSpacing * 2f)
        {
            for (float y = -hh + gridSpacing * 2f; y < hh; y += gridSpacing * 2f)
            {
                // Small + marks at grid intersections
                MakeSprite("GX", new Vector3(x, cy + y, 0),
                    new Vector3(0.15f, 0.02f, 1f), diagColor, -1)
                    .transform.SetParent(gridParent.transform, false);
                MakeSprite("GY", new Vector3(x, cy + y, 0),
                    new Vector3(0.02f, 0.15f, 1f), diagColor, -1)
                    .transform.SetParent(gridParent.transform, false);
            }
        }
    }

    private void BuildWallEdges(Transform parent, float hw, float hh, float cy)
    {
        // Bright crimson edges along inner walls
        MakeSprite("EdgeL", new Vector3(-hw, cy, 0), new Vector3(edgeWidth, hh * 2f, 1f), wallEdgeColor, 1)
            .transform.SetParent(parent, false);
        MakeSprite("EdgeR", new Vector3(hw, cy, 0), new Vector3(edgeWidth, hh * 2f, 1f), wallEdgeColor, 1)
            .transform.SetParent(parent, false);
        MakeSprite("EdgeT", new Vector3(0, cy + hh, 0), new Vector3(hw * 2f, edgeWidth, 1f), wallEdgeColor, 1)
            .transform.SetParent(parent, false);
        MakeSprite("EdgeB", new Vector3(0, cy - hh, 0), new Vector3(hw * 2f, edgeWidth, 1f), wallEdgeColor, 1)
            .transform.SetParent(parent, false);

        // Secondary inner edge (slightly inset, darker)
        Color innerEdge = new Color(wallEdgeColor.r * 0.5f, wallEdgeColor.g * 0.3f, wallEdgeColor.b * 0.3f, 0.4f);
        float inset = 0.15f;
        MakeSprite("InnerL", new Vector3(-hw + inset, cy, 0), new Vector3(edgeWidth * 0.5f, hh * 2f, 1f), innerEdge, 1)
            .transform.SetParent(parent, false);
        MakeSprite("InnerR", new Vector3(hw - inset, cy, 0), new Vector3(edgeWidth * 0.5f, hh * 2f, 1f), innerEdge, 1)
            .transform.SetParent(parent, false);
        MakeSprite("InnerT", new Vector3(0, cy + hh - inset, 0), new Vector3(hw * 2f, edgeWidth * 0.5f, 1f), innerEdge, 1)
            .transform.SetParent(parent, false);
        MakeSprite("InnerB", new Vector3(0, cy - hh + inset, 0), new Vector3(hw * 2f, edgeWidth * 0.5f, 1f), innerEdge, 1)
            .transform.SetParent(parent, false);
    }

    private void BuildCorners(Transform parent, float hw, float hh, float cy)
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

            // L-shaped accent
            MakeSprite("CH" + i, new Vector3(c.x + xDir * cs / 2f, cy + c.y, 0),
                new Vector3(cs, ct, 1f), cornerAccentColor, 1)
                .transform.SetParent(parent, false);
            MakeSprite("CV" + i, new Vector3(c.x, cy + c.y + yDir * cs / 2f, 0),
                new Vector3(ct, cs, 1f), cornerAccentColor, 1)
                .transform.SetParent(parent, false);

            // Inner diagonal accent at corner
            Color diagCorner = new Color(cornerAccentColor.r, cornerAccentColor.g * 1.2f, cornerAccentColor.b, 0.3f);
            MakeSprite("CD" + i, new Vector3(c.x + xDir * 0.5f, cy + c.y + yDir * 0.5f, 0),
                new Vector3(0.12f, 0.12f, 1f), diagCorner, 1)
                .transform.SetParent(parent, false);
        }
    }

    private void BuildBorderGlow(Transform parent, float hw, float hh, float cy)
    {
        float gw = glowWidth;

        MakeSprite("GL", new Vector3(-hw + gw / 2f, cy, 0), new Vector3(gw, hh * 2f, 1f), borderGlowColor, 0)
            .transform.SetParent(parent, false);
        MakeSprite("GR", new Vector3(hw - gw / 2f, cy, 0), new Vector3(gw, hh * 2f, 1f), borderGlowColor, 0)
            .transform.SetParent(parent, false);
        MakeSprite("GT", new Vector3(0, cy + hh - gw / 2f, 0), new Vector3(hw * 2f, gw, 1f), borderGlowColor, 0)
            .transform.SetParent(parent, false);
        MakeSprite("GB", new Vector3(0, cy - hh + gw / 2f, 0), new Vector3(hw * 2f, gw, 1f), borderGlowColor, 0)
            .transform.SetParent(parent, false);
    }

    private void BuildDangerStripes(Transform parent, float hw, float hh, float cy)
    {
        // Warning stripes along bottom and top walls (alternating dark red / darker)
        Color stripe1 = new Color(0.3f, 0.06f, 0.06f, 0.35f);
        Color stripe2 = new Color(0.12f, 0.04f, 0.04f, 0.35f);
        float stripeW = 1f;
        float stripeH = 0.3f;

        // Bottom wall stripes
        for (float x = -hw; x < hw; x += stripeW * 2f)
        {
            MakeSprite("SB", new Vector3(x + stripeW / 2f, cy - hh + stripeH / 2f + 0.1f, 0),
                new Vector3(stripeW, stripeH, 1f), stripe1, 0)
                .transform.SetParent(parent, false);
            if (x + stripeW * 1.5f < hw)
            {
                MakeSprite("SB2", new Vector3(x + stripeW * 1.5f, cy - hh + stripeH / 2f + 0.1f, 0),
                    new Vector3(stripeW, stripeH, 1f), stripe2, 0)
                    .transform.SetParent(parent, false);
            }
        }

        // Top wall stripes
        for (float x = -hw; x < hw; x += stripeW * 2f)
        {
            MakeSprite("ST", new Vector3(x + stripeW / 2f, cy + hh - stripeH / 2f - 0.1f, 0),
                new Vector3(stripeW, stripeH, 1f), stripe1, 0)
                .transform.SetParent(parent, false);
        }
    }

    private void BuildFloorRunes(Transform parent, float hw, float hh, float cy)
    {
        // Scattered green pixel-art rune marks on the floor
        // Creates a subtle mystical feel
        System.Random rng = new System.Random(42); // deterministic

        for (int i = 0; i < 12; i++)
        {
            float rx = (float)(rng.NextDouble() * 2 - 1) * (hw - 2f);
            float ry = (float)(rng.NextDouble() * 2 - 1) * (hh - 2f);

            // Small cross rune
            float runeSize = 0.3f + (float)rng.NextDouble() * 0.2f;
            float runeAlpha = 0.15f + (float)rng.NextDouble() * 0.15f;
            Color runeCol = new Color(greenAccentColor.r, greenAccentColor.g, greenAccentColor.b, runeAlpha);

            MakeSprite("RH" + i, new Vector3(rx, cy + ry, 0),
                new Vector3(runeSize, 0.04f, 1f), runeCol, -1)
                .transform.SetParent(parent, false);
            MakeSprite("RV" + i, new Vector3(rx, cy + ry, 0),
                new Vector3(0.04f, runeSize, 1f), runeCol, -1)
                .transform.SetParent(parent, false);

            // Some get extra dots
            if (rng.NextDouble() > 0.5)
            {
                float dotOff = runeSize * 0.6f;
                MakeSprite("RD" + i, new Vector3(rx + dotOff, cy + ry + dotOff, 0),
                    new Vector3(0.06f, 0.06f, 1f), runeCol, -1)
                    .transform.SetParent(parent, false);
                MakeSprite("RD2" + i, new Vector3(rx - dotOff, cy + ry - dotOff, 0),
                    new Vector3(0.06f, 0.06f, 1f), runeCol, -1)
                    .transform.SetParent(parent, false);
            }
        }
    }

    private void BuildTorchGlows(Transform parent, float hw, float hh, float cy)
    {
        // Warm glow spots at each corner
        Vector2[] corners = {
            new Vector2(-hw + 0.5f, hh - 0.5f),
            new Vector2(hw - 0.5f, hh - 0.5f),
            new Vector2(-hw + 0.5f, -hh + 0.5f),
            new Vector2(hw - 0.5f, -hh + 0.5f)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            // Large soft glow
            MakeSprite("Torch" + i, new Vector3(corners[i].x, cy + corners[i].y, 0),
                new Vector3(2.5f, 2.5f, 1f), new Color(torchColor.r, torchColor.g, torchColor.b, 0.12f), -1)
                .transform.SetParent(parent, false);
            // Bright center
            MakeSprite("TorchC" + i, new Vector3(corners[i].x, cy + corners[i].y, 0),
                new Vector3(0.8f, 0.8f, 1f), new Color(torchColor.r, torchColor.g, torchColor.b, 0.25f), -1)
                .transform.SetParent(parent, false);
            // Tiny green ember
            MakeSprite("TorchG" + i, new Vector3(corners[i].x, cy + corners[i].y, 0),
                new Vector3(0.2f, 0.2f, 1f), new Color(0.1f, 0.8f, 0.3f, 0.2f), -1)
                .transform.SetParent(parent, false);
        }
    }

    // ── DOOR ──────────────────────────────────────────────────────

    // ── DOOR SYSTEM ──────────────────────────────────────────────
    // Builds clear visual door separators at every arena entrance/exit.

    private void BuildAllDoors()
    {
        // Door 1: Battle Arena ↔ Spawn Hallway (y = battleBottomY)
        Color transitionFrame = new Color(0.35f, 0.12f, 0.18f, 1f);
        Color transitionGlow = new Color(0.5f, 0.15f, 0.35f, 0.4f);
        Color transitionRune = new Color(0.6f, 0.2f, 0.4f, 0.4f);
        Color transitionKeystone = new Color(0.55f, 0.2f, 0.45f, 0.65f);
        BuildDoorFrame("DoorTransition", battleBottomY, spawnHallHalfW, spawnHallwayWidth,
            transitionFrame, transitionGlow, transitionRune,
            transitionKeystone, true);
        SpawnDoorBarrier("DoorTransitionBarrier", battleBottomY, spawnHallwayWidth,
            new Color(0.28f, 0.1f, 0.15f, 0.9f),
            new Color(0.45f, 0.18f, 0.25f, 0.7f),
            false, true); // autoClose: shuts behind player entering arena

        // Door 2: Spawn Hallway ↔ Spawn Room (y = spawnTopY)
        Color spawnDoorFrame = new Color(0.15f, 0.18f, 0.3f, 1f);
        Color spawnDoorGlow = new Color(0.1f, 0.35f, 0.5f, 0.45f);
        Color spawnDoorRune = new Color(0.15f, 0.5f, 0.6f, 0.4f);
        Color spawnKeystone = new Color(0.1f, 0.6f, 0.5f, 0.65f);
        BuildDoorFrame("DoorSpawn", spawnTopY, spawnHallHalfW, spawnHallwayWidth,
            spawnDoorFrame, spawnDoorGlow, spawnDoorRune,
            spawnKeystone, false);
        SpawnDoorBarrier("DoorSpawnBarrier", spawnTopY, spawnHallwayWidth,
            new Color(0.12f, 0.14f, 0.22f, 0.9f),
            new Color(0.2f, 0.25f, 0.4f, 0.7f));
    }

    private void SpawnDoorBarrier(string name, float doorY, float width, Color panelColor, Color accentColor, bool startLocked = false, bool autoClose = false)
    {
        GameObject door = new GameObject(name);
        door.transform.SetParent(transform, false);
        door.transform.position = new Vector3(0, doorY, 0);

        DoorInteractable interactable = door.AddComponent<DoorInteractable>();
        interactable.doorWidth = width;
        interactable.doorColor = panelColor;
        interactable.doorAccentColor = accentColor;
        interactable.startLocked = startLocked;
        interactable.autoClose = autoClose;
    }

    /// <summary>
    /// Reusable door frame builder. Creates pillars, lintel, threshold, glow, runes, keystone.
    /// </summary>
    private void BuildDoorFrame(string name, float doorY, float halfW, float width,
        Color frameColor, Color glowColor, Color runeColor, Color keystoneColor,
        bool addPulse)
    {
        GameObject doorParent = new GameObject(name);
        doorParent.transform.SetParent(transform, false);

        float frameThick = 0.25f;
        float pillarH = 2f;
        float lintelY = doorY + pillarH / 2f + frameThick / 2f;
        float threshY = doorY - pillarH / 2f - frameThick / 4f;
        Color darkFrame = new Color(frameColor.r * 0.6f, frameColor.g * 0.6f, frameColor.b * 0.6f, 1f);

        // ── Pillars ──
        MakeSprite(name + "PilL", new Vector3(-halfW - frameThick / 2f, doorY, 0),
            new Vector3(frameThick, pillarH, 1f), frameColor, 3)
            .transform.SetParent(doorParent.transform, false);
        MakeSprite(name + "PilR", new Vector3(halfW + frameThick / 2f, doorY, 0),
            new Vector3(frameThick, pillarH, 1f), frameColor, 3)
            .transform.SetParent(doorParent.transform, false);

        // Pillar inner accent (thin bright line on inner face)
        Color pillarAccent = new Color(frameColor.r * 1.3f, frameColor.g * 1.1f, frameColor.b * 1.1f,
            Mathf.Min(1f, frameColor.a * 1.2f));
        MakeSprite(name + "PAL", new Vector3(-halfW + 0.02f, doorY, 0),
            new Vector3(0.04f, pillarH - 0.2f, 1f), pillarAccent, 4)
            .transform.SetParent(doorParent.transform, false);
        MakeSprite(name + "PAR", new Vector3(halfW - 0.02f, doorY, 0),
            new Vector3(0.04f, pillarH - 0.2f, 1f), pillarAccent, 4)
            .transform.SetParent(doorParent.transform, false);

        // ── Lintel (top bar) ──
        MakeSprite(name + "Lint", new Vector3(0, lintelY, 0),
            new Vector3(width + frameThick * 2.5f, frameThick, 1f), frameColor, 3)
            .transform.SetParent(doorParent.transform, false);

        // Lintel decoration — thin line above
        MakeSprite(name + "LintD", new Vector3(0, lintelY + frameThick * 0.7f, 0),
            new Vector3(width + frameThick * 3f, 0.04f, 1f),
            new Color(frameColor.r * 0.8f, frameColor.g * 0.8f, frameColor.b * 0.8f, 0.5f), 4)
            .transform.SetParent(doorParent.transform, false);

        // ── Threshold (bottom bar) ──
        MakeSprite(name + "Thresh", new Vector3(0, threshY, 0),
            new Vector3(width + frameThick * 2.5f, frameThick * 0.6f, 1f), darkFrame, 3)
            .transform.SetParent(doorParent.transform, false);

        // Threshold ground line
        MakeSprite(name + "ThreshL", new Vector3(0, threshY - frameThick * 0.4f, 0),
            new Vector3(width + frameThick * 3f, 0.03f, 1f),
            new Color(frameColor.r * 0.5f, frameColor.g * 0.5f, frameColor.b * 0.5f, 0.4f), 3)
            .transform.SetParent(doorParent.transform, false);

        // ─��� Glow ──
        MakeSprite(name + "GlowL", new Vector3(-halfW - 0.1f, doorY, 0),
            new Vector3(0.7f, pillarH + 0.5f, 1f), glowColor, 2)
            .transform.SetParent(doorParent.transform, false);
        MakeSprite(name + "GlowR", new Vector3(halfW + 0.1f, doorY, 0),
            new Vector3(0.7f, pillarH + 0.5f, 1f), glowColor, 2)
            .transform.SetParent(doorParent.transform, false);
        MakeSprite(name + "GlowT", new Vector3(0, lintelY + 0.15f, 0),
            new Vector3(width + 1.2f, 0.5f, 1f), glowColor, 2)
            .transform.SetParent(doorParent.transform, false);

        // ── Runes on pillars ──
        float[] pillarX = { -halfW - frameThick / 2f, halfW + frameThick / 2f };
        for (int p = 0; p < pillarX.Length; p++)
        {
            for (int i = 0; i < 3; i++)
            {
                float ry = doorY + (i - 1) * 0.45f;

                MakeSprite(name + "RH" + p + i, new Vector3(pillarX[p], ry, 0),
                    new Vector3(0.12f, 0.03f, 1f), runeColor, 4)
                    .transform.SetParent(doorParent.transform, false);
                MakeSprite(name + "RV" + p + i, new Vector3(pillarX[p], ry, 0),
                    new Vector3(0.03f, 0.12f, 1f), runeColor, 4)
                    .transform.SetParent(doorParent.transform, false);

                if (i == 1)
                {
                    MakeSprite(name + "RD" + p, new Vector3(pillarX[p], ry + 0.08f, 0),
                        new Vector3(0.04f, 0.04f, 1f), runeColor, 4)
                        .transform.SetParent(doorParent.transform, false);
                    MakeSprite(name + "RD2" + p, new Vector3(pillarX[p], ry - 0.08f, 0),
                        new Vector3(0.04f, 0.04f, 1f), runeColor, 4)
                        .transform.SetParent(doorParent.transform, false);
                }
            }
        }

        // ── Keystone diamond at top center ──
        GameObject keystone = MakeSprite(name + "Key", new Vector3(0, lintelY + 0.3f, 0),
            new Vector3(0.3f, 0.3f, 1f), keystoneColor, 5);
        keystone.transform.rotation = Quaternion.Euler(0, 0, 45f);
        keystone.transform.SetParent(doorParent.transform, false);

        // Keystone glow halo
        MakeSprite(name + "KeyGlow", new Vector3(0, lintelY + 0.3f, 0),
            new Vector3(0.8f, 0.8f, 1f),
            new Color(keystoneColor.r, keystoneColor.g, keystoneColor.b, 0.15f), 4)
            .transform.SetParent(doorParent.transform, false);

        // ── Side ornaments — small squares flanking the lintel ──
        Color ornamentCol = new Color(frameColor.r * 1.1f, frameColor.g * 0.9f, frameColor.b * 0.9f, 0.6f);
        MakeSprite(name + "OrnL", new Vector3(-halfW - frameThick * 1.2f, lintelY, 0),
            new Vector3(0.15f, 0.15f, 1f), ornamentCol, 4)
            .transform.SetParent(doorParent.transform, false);
        MakeSprite(name + "OrnR", new Vector3(halfW + frameThick * 1.2f, lintelY, 0),
            new Vector3(0.15f, 0.15f, 1f), ornamentCol, 4)
            .transform.SetParent(doorParent.transform, false);

        // ── Pulsing glow effect ──
        if (addPulse)
        {
            DoorPulse pulse = doorParent.AddComponent<DoorPulse>();
            pulse.glowColor = glowColor;
        }
    }

    // ── EXTRA DECORATIONS ─────────────────────────────────────────

    private void BuildDecorations()
    {
        GameObject parent = new GameObject("Decorations");
        parent.transform.SetParent(transform, false);

        // Skull warning marks near the active battle entrance from spawn hallway (bottom side)
        float entryY = battleBottomY + 0.5f;
        BuildSkullMark(parent.transform, new Vector3(-spawnHallHalfW - 2f, entryY, 0));
        BuildSkullMark(parent.transform, new Vector3(spawnHallHalfW + 2f, entryY, 0));

        // Red vignette strips at far edges of battle arena
        Color vignetteColor = new Color(0.15f, 0.02f, 0.02f, 0.3f);
        MakeSprite("VigL", new Vector3(-battleHalfW + 1f, battleCenterY, 0),
            new Vector3(2f, battleHeight, 1f), vignetteColor, 0)
            .transform.SetParent(parent.transform, false);
        MakeSprite("VigR", new Vector3(battleHalfW - 1f, battleCenterY, 0),
            new Vector3(2f, battleHeight, 1f), vignetteColor, 0)
            .transform.SetParent(parent.transform, false);

        // Center arena marker (subtle circle/diamond at center of battle arena)
        Color centerMark = new Color(0.3f, 0.08f, 0.08f, 0.2f);
        MakeSprite("CenterH", new Vector3(0, battleCenterY, 0),
            new Vector3(3f, 0.04f, 1f), centerMark, -1)
            .transform.SetParent(parent.transform, false);
        MakeSprite("CenterV", new Vector3(0, battleCenterY, 0),
            new Vector3(0.04f, 3f, 1f), centerMark, -1)
            .transform.SetParent(parent.transform, false);

        // Diamond at center
        GameObject diamond = MakeSprite("CenterD", new Vector3(0, battleCenterY, 0),
            new Vector3(1.5f, 1.5f, 1f), new Color(0.25f, 0.06f, 0.06f, 0.15f), -1);
        diamond.transform.rotation = Quaternion.Euler(0, 0, 45f);
        diamond.transform.SetParent(parent.transform, false);

        // Green accent line at active battle entrance from spawn hallway
        MakeSprite("EntryLine", new Vector3(0, battleBottomY + 0.1f, 0),
            new Vector3(spawnHallwayWidth - 0.5f, 0.05f, 1f),
            new Color(0.08f, 0.6f, 0.2f, 0.4f), 1)
            .transform.SetParent(parent.transform, false);
    }

    private void BuildSkullMark(Transform parent, Vector3 pos)
    {
        // Pixel-art skull shape (simplified: rectangle head + two eye holes)
        Color skullCol = new Color(0.4f, 0.1f, 0.1f, 0.35f);
        Color eyeCol = new Color(0.08f, 0.55f, 0.2f, 0.4f);

        // Head
        MakeSprite("SkullH", pos, new Vector3(0.6f, 0.5f, 1f), skullCol, 1)
            .transform.SetParent(parent, false);

        // Jaw
        MakeSprite("SkullJ", pos + new Vector3(0, -0.2f, 0), new Vector3(0.4f, 0.15f, 1f), skullCol, 1)
            .transform.SetParent(parent, false);

        // Left eye (green)
        MakeSprite("SkullEL", pos + new Vector3(-0.12f, 0.05f, 0), new Vector3(0.1f, 0.1f, 1f), eyeCol, 2)
            .transform.SetParent(parent, false);

        // Right eye (green)
        MakeSprite("SkullER", pos + new Vector3(0.12f, 0.05f, 0), new Vector3(0.1f, 0.1f, 1f), eyeCol, 2)
            .transform.SetParent(parent, false);
    }

    // ── SPRITE UTILITY ────────────────────────────────────────────

    private GameObject MakeSprite(string name, Vector3 pos, Vector3 scale, Color color, int sortOrder)
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

    // ── SPAWN ROOM: WALLS & FLOORS ──────────────────────────────

    private void SplitBattleBottomWall()
    {
        // Find the bottom wall we created for the battle arena (BattleWallB)
        // and replace it with two segments with a gap for the spawn hallway
        DestroyChildrenByName("BattleWallB");

        float segW = (battleWidth + wallThick * 2f - spawnHallwayWidth) / 2f;
        float segCenterX = spawnHallHalfW + segW / 2f;

        CreateWall("BattleBottomL",
            new Vector3(-segCenterX, battleBottomY - wallThick / 2f, 0),
            new Vector3(segW, wallThick, 1f), wallColor);

        CreateWall("BattleBottomR",
            new Vector3(segCenterX, battleBottomY - wallThick / 2f, 0),
            new Vector3(segW, wallThick, 1f), wallColor);
    }

    private void DestroyChildrenByName(string childName)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == childName)
                Destroy(child.gameObject);
        }
    }

    private void CleanupLegacyFormerBossSection()
    {
        // Remove legacy objects from the old boss connector layout.
        string[] legacyNames =
        {
            "DoorBoss",
            "DoorBossBarrier",
            "DoorBattle",
            "DoorBattleBarrier",
            "BossArenaTrigger",
            "HallwayVisuals",
            "HallFloor",
            "HallWallL",
            "HallWallR",
            "BossBottomL",
            "BossBottomR",
            "BattleTopL",
            "BattleTopR"
        };

        for (int i = 0; i < legacyNames.Length; i++)
            DestroyChildrenByName(legacyNames[i]);
    }

    private void CleanupGlobalLegacyObjects()
    {
        // Some older scene versions created these as top-level objects/siblings.
        string[] globalLegacyNames =
        {
            "ArenaVisuals",
            "DoorBoss",
            "DoorBossBarrier",
            "DoorBattle",
            "DoorBattleBarrier",
            "BossArenaTrigger"
        };

        for (int i = 0; i < globalLegacyNames.Length; i++)
        {
            GameObject legacy = GameObject.Find(globalLegacyNames[i]);
            if (legacy != null)
                Destroy(legacy);
        }
    }

    private void EnsureTwoRoomBattleTopWall()
    {
        if (transform.Find("BattleWallT") != null)
            return;

        CreateWall("BattleWallT", new Vector3(0, battleTopY + wallThick / 2f, 0),
            new Vector3(battleWidth + wallThick * 2f, wallThick, 1f), wallColor);
    }

    private void BuildSpawnHallwayWalls()
    {
        float hallCenterY = (spawnHallTopY + spawnHallBottomY) / 2f;

        CreateWall("SpawnHallWallL",
            new Vector3(-spawnHallHalfW, hallCenterY, 0),
            new Vector3(wallThick, spawnHallwayLength, 1f), spawnHallWallColor);

        CreateWall("SpawnHallWallR",
            new Vector3(spawnHallHalfW, hallCenterY, 0),
            new Vector3(wallThick, spawnHallwayLength, 1f), spawnHallWallColor);
    }

    private void BuildSpawnRoomWalls()
    {
        float sw = spawnHalfW + wallThick / 2f;

        // Left wall
        CreateWall("SpawnWallL",
            new Vector3(-sw, spawnCenterY, 0),
            new Vector3(wallThick, spawnHeight + wallThick, 1f), spawnWallColor);

        // Right wall
        CreateWall("SpawnWallR",
            new Vector3(sw, spawnCenterY, 0),
            new Vector3(wallThick, spawnHeight + wallThick, 1f), spawnWallColor);

        // Bottom wall (solid, no gap)
        CreateWall("SpawnWallB",
            new Vector3(0, spawnBottomY - wallThick / 2f, 0),
            new Vector3(spawnWidth + wallThick * 2f, wallThick, 1f), spawnWallColor);

        // Top wall — split for hallway opening
        float topSegW = (spawnWidth - spawnHallwayWidth) / 2f;
        float topSegCenterX = spawnHallHalfW + topSegW / 2f;

        CreateWall("SpawnTopL",
            new Vector3(-topSegCenterX, spawnTopY + wallThick / 2f, 0),
            new Vector3(topSegW, wallThick, 1f), spawnWallColor);

        CreateWall("SpawnTopR",
            new Vector3(topSegCenterX, spawnTopY + wallThick / 2f, 0),
            new Vector3(topSegW, wallThick, 1f), spawnWallColor);
    }

    private void BuildSpawnFloors()
    {
        // Spawn hallway floor
        float hallCenterY = (spawnHallTopY + spawnHallBottomY) / 2f;
        CreateFloor("SpawnHallFloor",
            new Vector3(0, hallCenterY, 0),
            new Vector3(spawnHallwayWidth, spawnHallwayLength, 1f), spawnHallFloorColor);

        // Spawn room floor
        CreateFloor("SpawnRoomFloor",
            new Vector3(0, spawnCenterY, 0),
            new Vector3(spawnWidth, spawnHeight, 1f), spawnFloorColor);
    }

    // ── SPAWN HALLWAY VISUALS ────────────────────────��────────────

    private void BuildSpawnHallwayVisuals()
    {
        float hallCenterY = (spawnHallTopY + spawnHallBottomY) / 2f;
        float hh = spawnHallwayLength / 2f - 0.5f;
        float hw = spawnHallHalfW - 0.5f;

        GameObject parent = new GameObject("SpawnHallwayVisuals");
        parent.transform.SetParent(transform, false);

        // Wall edge highlights
        MakeSprite("SHEdgeL", new Vector3(-hw, hallCenterY, 0),
            new Vector3(edgeWidth, hh * 2f, 1f), spawnHallEdgeColor, 1)
            .transform.SetParent(parent.transform, false);
        MakeSprite("SHEdgeR", new Vector3(hw, hallCenterY, 0),
            new Vector3(edgeWidth, hh * 2f, 1f), spawnHallEdgeColor, 1)
            .transform.SetParent(parent.transform, false);

        // Glow strips
        Color shGlow = new Color(spawnHallEdgeColor.r, spawnHallEdgeColor.g, spawnHallEdgeColor.b, 0.12f);
        MakeSprite("SHGlowL", new Vector3(-hw + 0.2f, hallCenterY, 0),
            new Vector3(0.35f, hh * 2f, 1f), shGlow, 0)
            .transform.SetParent(parent.transform, false);
        MakeSprite("SHGlowR", new Vector3(hw - 0.2f, hallCenterY, 0),
            new Vector3(0.35f, hh * 2f, 1f), shGlow, 0)
            .transform.SetParent(parent.transform, false);

        // Center runner
        Color runnerCol = new Color(0.12f, 0.06f, 0.08f, 0.4f);
        MakeSprite("SHRunner", new Vector3(0, hallCenterY, 0),
            new Vector3(1.2f, spawnHallwayLength - 1f, 1f), runnerCol, -1)
            .transform.SetParent(parent.transform, false);

        // Guide lights along walls — transitioning from calm blue-green to battle red
        for (float y = spawnHallTopY + 1f; y > spawnHallBottomY - 0.5f; y -= 1.8f)
        {
            float t = Mathf.InverseLerp(spawnHallTopY, spawnHallBottomY, y);
            Color lightCol = Color.Lerp(
                new Color(0.6f, 0.15f, 0.15f, 0.3f),  // top = red (near battle)
                new Color(0.1f, 0.45f, 0.5f, 0.35f),   // bottom = calm teal (near spawn)
                t);

            MakeSprite("SHLightL", new Vector3(-hw + 0.12f, y, 0),
                new Vector3(0.09f, 0.09f, 1f), lightCol, 1)
                .transform.SetParent(parent.transform, false);
            MakeSprite("SHLightR", new Vector3(hw - 0.12f, y, 0),
                new Vector3(0.09f, 0.09f, 1f), lightCol, 1)
                .transform.SetParent(parent.transform, false);
        }

        // Arrow markers pointing up (toward battle)
        Color arrowCol = new Color(0.4f, 0.12f, 0.12f, 0.2f);
        for (float y = spawnHallBottomY + 2f; y < spawnHallTopY - 1f; y += 2.5f)
        {
            // Simple chevron: two angled bars forming a "^"
            MakeSprite("ArrL", new Vector3(-0.15f, y, 0),
                new Vector3(0.5f, 0.06f, 1f), arrowCol, -1)
                .transform.SetParent(parent.transform, false);
            // Rotate the sprite objects
            parent.transform.Find("ArrL"); // already parented
            MakeSprite("ArrR", new Vector3(0.15f, y, 0),
                new Vector3(0.5f, 0.06f, 1f), arrowCol, -1)
                .transform.SetParent(parent.transform, false);
        }
    }

    // ── SPAWN ROOM VISUALS ────────────────────────────────────────

    private void BuildSpawnRoomVisuals()
    {
        float hw = spawnHalfW - 0.5f;
        float hh = spawnHalfH - 0.5f;
        float cy = spawnCenterY;

        GameObject parent = new GameObject("SpawnRoomVisuals");
        parent.transform.SetParent(transform, false);

        // Subtle grid — calmer spacing
        BuildSpawnGrid(parent.transform, hw, hh, cy);

        // Wall edges — cool blue-purple tones
        BuildSpawnWallEdges(parent.transform, hw, hh, cy);

        // Corner accents
        BuildSpawnCorners(parent.transform, hw, hh, cy);

        // Border glow
        BuildSpawnBorderGlow(parent.transform, hw, hh, cy);

        // Safe zone indicator at center
        BuildSafeZoneMarker(parent.transform, cy);

        // Ambient light pools — calm, welcoming
        BuildSpawnAmbientLights(parent.transform, hw, hh, cy);

        // Exit sign above hallway entrance
        BuildExitSign(parent.transform, hw, hh, cy);
    }

    private void BuildSpawnGrid(Transform parent, float hw, float hh, float cy)
    {
        Color sGridColor = new Color(0.10f, 0.10f, 0.14f, 0.6f);
        float spacing = 2f;

        for (float x = -hw + spacing; x < hw; x += spacing)
        {
            MakeSprite("SGV", new Vector3(x, cy, 0),
                new Vector3(0.02f, hh * 2f, 1f), sGridColor, -1)
                .transform.SetParent(parent, false);
        }
        for (float y = -hh + spacing; y < hh; y += spacing)
        {
            MakeSprite("SGH", new Vector3(0, cy + y, 0),
                new Vector3(hw * 2f, 0.02f, 1f), sGridColor, -1)
                .transform.SetParent(parent, false);
        }
    }

    private void BuildSpawnWallEdges(Transform parent, float hw, float hh, float cy)
    {
        MakeSprite("SEL", new Vector3(-hw, cy, 0), new Vector3(edgeWidth, hh * 2f, 1f), spawnEdgeColor, 1)
            .transform.SetParent(parent, false);
        MakeSprite("SER", new Vector3(hw, cy, 0), new Vector3(edgeWidth, hh * 2f, 1f), spawnEdgeColor, 1)
            .transform.SetParent(parent, false);
        MakeSprite("SET", new Vector3(0, cy + hh, 0), new Vector3(hw * 2f, edgeWidth, 1f), spawnEdgeColor, 1)
            .transform.SetParent(parent, false);
        MakeSprite("SEB", new Vector3(0, cy - hh, 0), new Vector3(hw * 2f, edgeWidth, 1f), spawnEdgeColor, 1)
            .transform.SetParent(parent, false);

        // Softer inner edge
        Color inner = new Color(spawnEdgeColor.r * 0.5f, spawnEdgeColor.g * 0.5f, spawnEdgeColor.b * 0.5f, 0.3f);
        float inset = 0.12f;
        MakeSprite("SiEL", new Vector3(-hw + inset, cy, 0), new Vector3(edgeWidth * 0.4f, hh * 2f, 1f), inner, 1)
            .transform.SetParent(parent, false);
        MakeSprite("SiER", new Vector3(hw - inset, cy, 0), new Vector3(edgeWidth * 0.4f, hh * 2f, 1f), inner, 1)
            .transform.SetParent(parent, false);
        MakeSprite("SiET", new Vector3(0, cy + hh - inset, 0), new Vector3(hw * 2f, edgeWidth * 0.4f, 1f), inner, 1)
            .transform.SetParent(parent, false);
        MakeSprite("SiEB", new Vector3(0, cy - hh + inset, 0), new Vector3(hw * 2f, edgeWidth * 0.4f, 1f), inner, 1)
            .transform.SetParent(parent, false);
    }

    private void BuildSpawnCorners(Transform parent, float hw, float hh, float cy)
    {
        float cs = 1.8f;
        float ct = 0.06f;
        Color cc = new Color(0.2f, 0.18f, 0.35f, 0.5f);

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

            MakeSprite("SCH" + i, new Vector3(c.x + xDir * cs / 2f, cy + c.y, 0),
                new Vector3(cs, ct, 1f), cc, 1)
                .transform.SetParent(parent, false);
            MakeSprite("SCV" + i, new Vector3(c.x, cy + c.y + yDir * cs / 2f, 0),
                new Vector3(ct, cs, 1f), cc, 1)
                .transform.SetParent(parent, false);
        }
    }

    private void BuildSpawnBorderGlow(Transform parent, float hw, float hh, float cy)
    {
        float gw = 0.4f;

        MakeSprite("SGL", new Vector3(-hw + gw / 2f, cy, 0), new Vector3(gw, hh * 2f, 1f), spawnGlowColor, 0)
            .transform.SetParent(parent, false);
        MakeSprite("SGR", new Vector3(hw - gw / 2f, cy, 0), new Vector3(gw, hh * 2f, 1f), spawnGlowColor, 0)
            .transform.SetParent(parent, false);
        MakeSprite("SGT", new Vector3(0, cy + hh - gw / 2f, 0), new Vector3(hw * 2f, gw, 1f), spawnGlowColor, 0)
            .transform.SetParent(parent, false);
        MakeSprite("SGB", new Vector3(0, cy - hh + gw / 2f, 0), new Vector3(hw * 2f, gw, 1f), spawnGlowColor, 0)
            .transform.SetParent(parent, false);
    }

    private void BuildSafeZoneMarker(Transform parent, float cy)
    {
        // Central diamond — calm green/teal
        Color safeCol = new Color(0.08f, 0.45f, 0.35f, 0.2f);

        GameObject diamond = MakeSprite("SafeD", new Vector3(0, cy, 0),
            new Vector3(2f, 2f, 1f), safeCol, -1);
        diamond.transform.rotation = Quaternion.Euler(0, 0, 45f);
        diamond.transform.SetParent(parent, false);

        // Inner bright diamond
        GameObject innerD = MakeSprite("SafeD2", new Vector3(0, cy, 0),
            new Vector3(0.8f, 0.8f, 1f), new Color(0.1f, 0.55f, 0.4f, 0.3f), -1);
        innerD.transform.rotation = Quaternion.Euler(0, 0, 45f);
        innerD.transform.SetParent(parent, false);

        // Cross lines
        Color crossCol = new Color(0.1f, 0.4f, 0.3f, 0.15f);
        MakeSprite("SafeH", new Vector3(0, cy, 0), new Vector3(3f, 0.03f, 1f), crossCol, -1)
            .transform.SetParent(parent, false);
        MakeSprite("SafeV", new Vector3(0, cy, 0), new Vector3(0.03f, 3f, 1f), crossCol, -1)
            .transform.SetParent(parent, false);

        // "SAFE" text dots — four pixel clusters in a row
        Color dotCol = new Color(0.1f, 0.5f, 0.35f, 0.25f);
        float dotY = cy - 1.8f;
        for (int i = 0; i < 4; i++)
        {
            float dx = -0.45f + i * 0.3f;
            MakeSprite("SDot" + i, new Vector3(dx, dotY, 0),
                new Vector3(0.12f, 0.12f, 1f), dotCol, -1)
                .transform.SetParent(parent, false);
        }
    }

    private void BuildSpawnAmbientLights(Transform parent, float hw, float hh, float cy)
    {
        // Soft teal light pools at corners — cozy safe room feel
        Color lightCol = new Color(0.08f, 0.35f, 0.45f, 0.08f);
        Color brightCol = new Color(0.1f, 0.4f, 0.5f, 0.15f);

        Vector2[] positions = {
            new Vector2(-hw + 1f, hh - 1f),
            new Vector2(hw - 1f, hh - 1f),
            new Vector2(-hw + 1f, -hh + 1f),
            new Vector2(hw - 1f, -hh + 1f),
            new Vector2(0, 0) // center
        };

        for (int i = 0; i < positions.Length; i++)
        {
            float size = i == 4 ? 3.5f : 2.2f;
            MakeSprite("SAL" + i, new Vector3(positions[i].x, cy + positions[i].y, 0),
                new Vector3(size, size, 1f), lightCol, -1)
                .transform.SetParent(parent, false);
            MakeSprite("SALC" + i, new Vector3(positions[i].x, cy + positions[i].y, 0),
                new Vector3(size * 0.35f, size * 0.35f, 1f), brightCol, -1)
                .transform.SetParent(parent, false);

            // Tiny green spark
            if (i < 4)
            {
                MakeSprite("SSpark" + i, new Vector3(positions[i].x, cy + positions[i].y, 0),
                    new Vector3(0.15f, 0.15f, 1f), spawnAccentColor, 1)
                    .transform.SetParent(parent, false);
            }
        }
    }

    private void BuildExitSign(Transform parent, float hw, float hh, float cy)
    {
        // Visual indicator above the hallway exit (top of spawn room)
        float signY = cy + hh - 0.2f;

        // Arrow pointing up
        Color arrowCol = new Color(0.6f, 0.2f, 0.15f, 0.5f);
        MakeSprite("ExitArrowV", new Vector3(0, signY, 0),
            new Vector3(0.08f, 0.6f, 1f), arrowCol, 1)
            .transform.SetParent(parent, false);
        // Arrowhead — two small angled bars
        MakeSprite("ExitArrowHL", new Vector3(-0.15f, signY + 0.2f, 0),
            new Vector3(0.35f, 0.06f, 1f), arrowCol, 1)
            .transform.SetParent(parent, false);
        MakeSprite("ExitArrowHR", new Vector3(0.15f, signY + 0.2f, 0),
            new Vector3(0.35f, 0.06f, 1f), arrowCol, 1)
            .transform.SetParent(parent, false);

        // Red glow behind arrow
        MakeSprite("ExitGlow", new Vector3(0, signY, 0),
            new Vector3(1.5f, 1.2f, 1f), new Color(0.4f, 0.08f, 0.08f, 0.12f), 0)
            .transform.SetParent(parent, false);

        // Warning text dots — "DANGER AHEAD" hint
        Color warnDot = new Color(0.5f, 0.12f, 0.1f, 0.3f);
        for (int i = 0; i < 6; i++)
        {
            float dx = -0.75f + i * 0.3f;
            MakeSprite("WDot" + i, new Vector3(dx, signY - 0.5f, 0),
                new Vector3(0.08f, 0.08f, 1f), warnDot, 1)
                .transform.SetParent(parent, false);
        }
    }

    // ── BATTLE ARENA TRIGGER (area title popup) ─────────────────

    private void BuildBattleArenaTrigger()
    {
        // Trigger at the battle arena entrance (where spawn hallway meets battle arena top)
        float triggerY = battleBottomY + 1f; // just inside the battle arena from below

        GameObject trigger = new GameObject("BattleArenaTrigger");
        trigger.transform.SetParent(transform, false);
        trigger.transform.position = new Vector3(0, triggerY, 0);

        BoxCollider2D col = trigger.AddComponent<BoxCollider2D>();
        col.size = new Vector2(spawnHallwayWidth + 2f, 2f);
        col.isTrigger = true;

        Rigidbody2D rb = trigger.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        BattleArenaTrigger triggerComponent = trigger.AddComponent<BattleArenaTrigger>();
        triggerComponent.introPanInDuration = bossIntroPanInDuration;
        triggerComponent.introFocusHoldDuration = bossIntroFocusHoldDuration;
        triggerComponent.introPostWakeHoldDuration = bossIntroPostWakeHoldDuration;
        triggerComponent.introPanOutDuration = bossIntroPanOutDuration;
        triggerComponent.bossRoomCameraSize = bossRoomCameraSize;
    }

    // ── PLAYER SPAWN ──────────────────────────────────────────────

    private void MovePlayerToSpawnRoom()
    {
        // Move the player to the center of the spawn room.
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null)
                playerObj = pm.gameObject;
        }

        if (playerObj == null)
            return;

        // Ensure downstream systems that rely on Player tag (camera/triggers) can find the player.
        if (!playerObj.CompareTag("Player"))
            playerObj.tag = "Player";

        playerObj.transform.position = new Vector3(0, spawnCenterY, 0);
    }
}

/// <summary>
/// Pulses the door glow sprites to create a breathing/alive effect.
/// </summary>
public class DoorPulse : MonoBehaviour
{
    public Color glowColor;
    private SpriteRenderer[] glows;

    private void Start()
    {
        // Find glow sprites in children
        glows = GetComponentsInChildren<SpriteRenderer>();
        StartCoroutine(Pulse());
    }

    private IEnumerator Pulse()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 1.5f;
            float pulse = 0.7f + 0.3f * Mathf.Sin(t);

            foreach (SpriteRenderer sr in glows)
            {
                string n = sr.gameObject.name;
                if (n.Contains("Glow") || n.Contains("Key"))
                {
                    Color c = sr.color;
                    c.a = n.Contains("Key") && !n.Contains("KeyGlow")
                        ? 0.5f + 0.2f * Mathf.Sin(t * 1.3f)
                        : glowColor.a * pulse;
                    sr.color = c;
                }
            }

            yield return null;
        }
    }
}
