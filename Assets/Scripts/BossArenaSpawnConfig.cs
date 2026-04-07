using UnityEngine;

/// <summary>
/// Optional Boss Arena spawn overrides for player and boss.
/// Add this to a GameObject in the Boss Arena scene to configure spawn anchors in Inspector.
/// </summary>
public class BossArenaSpawnConfig : MonoBehaviour
{
    public enum SpawnAnchor
    {
        Center,
        Top,
        Bottom,
        Left,
        Right,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        CustomWorldPosition
    }

    [Header("Player Spawn")]
    public bool overridePlayerSpawn = true;
    public SpawnAnchor playerAnchor = SpawnAnchor.Bottom;
    public Vector2 playerOffset = new Vector2(0f, 2.5f);
    public Vector2 playerCustomWorldPosition = Vector2.zero;

    [Header("Boss Spawn")]
    public bool overrideBossSpawn = true;
    public SpawnAnchor bossAnchor = SpawnAnchor.Top;
    public Vector2 bossOffset = new Vector2(0f, -2.5f);
    public Vector2 bossCustomWorldPosition = Vector2.zero;

    [Header("Boss Arena Camera")]
    public bool overrideCameraSize = true;
    [Min(1f)] public float cameraSize = 16f;

    [Header("Fallback Intro Cutscene")]
    public bool enableFallbackIntroCutscene = true;
    [Min(0f)] public float fallbackPanInDuration = 0.8f;
    [Min(0f)] public float fallbackFocusHoldDuration = 0.65f;
    [Min(0f)] public float fallbackPostWakeHoldDuration = 0.55f;
    [Min(0f)] public float fallbackPanOutDuration = 0.8f;

    [Header("Phase 2 Transition Cutscene")]
    public bool enablePhase2TransitionCutscene = true;
    [Min(0f)] public float phase2PauseDuration = 0.2f;
    [Min(0f)] public float phase2PanToBossDuration = 0.65f;
    [Min(0f)] public float phase2BossFocusHoldDuration = 0.45f;
    [Min(0f)] public float phase2PanBackToPlayerDuration = 0.7f;
    [Range(0f, 90f)] public float phase2ZoomInPercent = 30f;
    [Min(0f)] public float phase2ZoomDuration = 0.45f;
}
