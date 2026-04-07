using System.Collections;
using UnityEngine;

/// <summary>
/// Invisible trigger zone placed at the boss arena entrance.
/// When the player walks through, it wakes the boss and locks the door behind them.
/// </summary>
public class BossArenaTrigger : MonoBehaviour
{
    private const float BossArenaMargin = 1.5f;
    private const float BossBoundsPositionPadding = 0.5f;
    private const float MinimumBossRoomCameraSize = 16f;

    [Header("Trigger Gate")]
    [Tooltip("Player center must be this far above the trigger center before the intro can start.")]
    public float requiredEntryDepth = 0.35f;

    [Header("Boss Intro Cutscene")]
    [Min(0f)] public float introPanInDuration = 1.4f;
    [Min(0f)] public float introFocusHoldDuration = 1.2f;
    [Min(0f)] public float introPostWakeHoldDuration = 0.9f;
    [Min(0f)] public float introPanOutDuration = 1.2f;

    [Header("Boss Room Camera")]
    [Min(1f)] public float bossRoomCameraSize = 16f;

    private bool triggered = false;
    private BossEnemy boss;
    private BossArenaAudioController bossArenaAudio;
    private BossHealthBar bossHealthBar;

    private static bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        return other.GetComponent<PlayerMovement>() != null
            || other.GetComponentInParent<PlayerMovement>() != null;
    }

    private void Start()
    {
        boss = FindFirstObjectByType<BossEnemy>();
        bossArenaAudio = BossArenaAudioController.FindInstance();
        bossHealthBar = FindFirstObjectByType<BossHealthBar>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartCutscene(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartCutscene(other);
    }

    private void TryStartCutscene(Collider2D other)
    {
        if (triggered) return;
        if (!IsPlayerCollider(other)) return;
        if (!HasFullyEnteredArena(other)) return;

        triggered = true;

        // Prevent duplicate intro flow if both trigger types exist in scene.
        BattleArenaTrigger primaryTrigger = FindFirstObjectByType<BattleArenaTrigger>();
        if (primaryTrigger != null)
            primaryTrigger.gameObject.SetActive(false);

        if (bossArenaAudio == null)
            bossArenaAudio = BossArenaAudioController.FindInstance();

        if (bossHealthBar == null)
            bossHealthBar = FindFirstObjectByType<BossHealthBar>();

        // Configure arena bounds, but keep the boss at its authored scene position.
        ConfigureBossArenaBoundsAtCurrentPosition();
        SyncBossRigidbodyToCurrentPosition();

        ApplyBossRoomZoom();

        StartCoroutine(PlayBossIntroCutscene());
    }

    private bool HasFullyEnteredArena(Collider2D playerCollider)
    {
        if (playerCollider == null)
            return false;

        float playerCenterY = playerCollider.bounds.center.y;
        return playerCenterY >= transform.position.y + requiredEntryDepth;
    }

    private void ConfigureBossArenaBoundsAtCurrentPosition()
    {
        if (boss == null)
            return;

        float minX;
        float maxX;
        float minY;
        float maxY;

        BattleArenaBuilder builder = FindFirstObjectByType<BattleArenaBuilder>();
        if (builder != null)
        {
            builder.GetBattleArenaBounds(BossArenaMargin, out minX, out maxX, out minY, out maxY);
        }
        else
        {
            Vector3 p = boss.transform.position;
            minX = p.x - 18f;
            maxX = p.x + 18f;
            minY = p.y - 12f;
            maxY = p.y + 12f;
        }

        Vector3 bossPos = boss.transform.position;
        minX = Mathf.Min(minX, bossPos.x - BossBoundsPositionPadding);
        maxX = Mathf.Max(maxX, bossPos.x + BossBoundsPositionPadding);
        minY = Mathf.Min(minY, bossPos.y - BossBoundsPositionPadding);
        maxY = Mathf.Max(maxY, bossPos.y + BossBoundsPositionPadding);

        boss.ConfigureArenaBounds(minX, maxX, minY, maxY);
    }

    private void SyncBossRigidbodyToCurrentPosition()
    {
        if (boss == null)
            return;

        Rigidbody2D bossRb = boss.GetComponent<Rigidbody2D>();
        if (bossRb == null)
            return;

        Vector2 bossPos = boss.transform.position;
        bossRb.position = bossPos;
        bossRb.linearVelocity = Vector2.zero;
        bossRb.angularVelocity = 0f;
    }

    private IEnumerator PlayBossIntroCutscene()
    {
        Camera cam = Camera.main;
        CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        bool followWasEnabled = follow != null && follow.enabled;

        LockBossDoor();

        Time.timeScale = 0f;

        if (followWasEnabled)
            follow.enabled = false;

        Vector3 camStart = cam != null ? cam.transform.position : Vector3.zero;
        Vector3 camBoss = camStart;
        if (cam != null && boss != null)
            camBoss = new Vector3(boss.transform.position.x, boss.transform.position.y, camStart.z);

        if (cam != null)
        {
            yield return PanCameraUnscaled(cam.transform, camStart, camBoss, introPanInDuration);
            yield return new WaitForSecondsRealtime(introFocusHoldDuration);
        }

        Time.timeScale = 1f;

        if (bossArenaAudio != null)
            bossArenaAudio.OnBossArenaEntered();

        if (bossHealthBar != null)
            bossHealthBar.RevealBar();

        ClearPlayerFightStartInvincibility();

        if (boss != null)
            boss.WakeUp();

        yield return new WaitForSecondsRealtime(introPostWakeHoldDuration);

        if (cam != null)
        {
            Vector3 camReturn = GetPlayerCameraPosition(cam, follow, camStart);
            yield return PanCameraUnscaled(cam.transform, cam.transform.position, camReturn, introPanOutDuration);
        }

        if (follow != null)
        {
            follow.enabled = followWasEnabled;
            if (followWasEnabled)
                follow.SnapToTarget();
        }

        // Disable this trigger after first use.
        gameObject.SetActive(false);
    }

    private static Vector3 GetPlayerCameraPosition(Camera cam, CameraFollow follow, Vector3 fallback)
    {
        if (cam == null)
            return fallback;

        Transform target = null;
        if (follow != null)
            target = follow.target;

        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
            else
            {
                PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
                if (pm != null)
                    target = pm.transform;
            }
        }

        if (target == null)
            return fallback;

        return new Vector3(target.position.x, target.position.y, fallback.z);
    }

    private static void ClearPlayerFightStartInvincibility()
    {
        PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null && pm.isSuperActive)
            return;

        PlayerHealth ph = FindFirstObjectByType<PlayerHealth>();
        if (ph != null)
            ph.ResetCombatInvincibility();
    }

    private void LockBossDoor()
    {
        // Lock the boss arena door behind the player so they can't retreat
        // Find the boss door barrier (DoorBossBarrier) and re-lock it
        DoorInteractable[] doors = FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
        foreach (DoorInteractable door in doors)
        {
            if (door.gameObject.name == "DoorBossBarrier")
            {
                door.LockDoor();
                break;
            }
        }
    }

    private static IEnumerator PanCameraUnscaled(Transform camTransform, Vector3 from, Vector3 to, float duration)
    {
        if (camTransform == null)
            yield break;

        if (duration <= 0f)
        {
            camTransform.position = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            camTransform.position = Vector3.Lerp(from, to, eased);
            yield return null;
        }

        camTransform.position = to;
    }

    /// <summary>
    /// Re-arms this trigger so it can fire again after player respawn.
    /// </summary>
    public void ResetTrigger()
    {
        triggered = false;

        if (bossArenaAudio == null)
            bossArenaAudio = BossArenaAudioController.FindInstance();

        if (bossArenaAudio != null)
            bossArenaAudio.ResetForRespawn();

        gameObject.SetActive(true);
    }

    private void ApplyBossRoomZoom()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
            return;

        float zoomSize = Mathf.Max(bossRoomCameraSize, MinimumBossRoomCameraSize);
        cam.orthographicSize = zoomSize;

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow != null)
            follow.orthographicSize = zoomSize;
    }
}
