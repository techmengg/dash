using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Sets target frame rate to 240 and disables vsync.
/// Auto-runs before any scene loads.
/// </summary>
public static class GameBootstrap
{
    private enum BossCorridor
    {
        North,
        South,
        East,
        West
    }

    private static bool subscribedToSceneLoad;

    private const float DefaultOrthoSize = 6f;
    private const float DefaultNearClip = 0.1f;
    private const float DefaultFarClip = 1000f;
    private const float CorridorSpawnInset = 1.5f;
    private const float BossArenaBoundsMargin = 1.5f;
    private const float BossArenaCameraSize = 16f;
    private const float BossArenaBossTopInset = 2.5f;
    private const float BossArenaPlayerBottomInset = 2.5f;
    private const int BossScenePlayerSortingOrder = 30;
    private const int BossSceneBossSortingOrder = 29;
    private const float BossArenaSetupRetryDuration = 3f;
    private const float BossRoomAnchorOffsetY = 1.25f;
    private const float BossRoomMaxDistanceFromAnchor = 12f;
    private const float BossAutoWakeDistance = 9f;
    private const float BossAutoWakeDelay = 1.25f;
    private const float BossAutoWakePanInDuration = 0.8f;
    private const float BossAutoWakeFocusHoldDuration = 0.65f;
    private const float BossAutoWakePostWakeHoldDuration = 0.55f;
    private const float BossAutoWakePanOutDuration = 0.8f;
    private static readonly Vector3 LeftStairFallbackOffset = new Vector3(-3f, 3.5f, 0f);
    private static readonly Vector3 RightStairFallbackOffset = new Vector3(3f, 3.5f, 0f);

    private static readonly string[] LeftStairMarkerNames =
    {
        "BossSpawnLeft",
        "SpawnStairLeft",
        "LeftStairSpawn",
        "StairLeft"
    };

    private static readonly string[] RightStairMarkerNames =
    {
        "BossSpawnRight",
        "SpawnStairRight",
        "RightStairSpawn",
        "StairRight"
    };

    private static readonly string[] NorthCorridorMarkerNames =
    {
        "BossSpawnNorth",
        "SpawnNorth",
        "NorthCorridorSpawn",
        "NorthDoorSpawn"
    };

    private static readonly string[] SouthCorridorMarkerNames =
    {
        "BossSpawnSouth",
        "SpawnSouth",
        "SouthCorridorSpawn",
        "SouthDoorSpawn"
    };

    private static readonly string[] EastCorridorMarkerNames =
    {
        "BossSpawnEast",
        "SpawnEast",
        "EastCorridorSpawn",
        "EastDoorSpawn"
    };

    private static readonly string[] WestCorridorMarkerNames =
    {
        "BossSpawnWest",
        "SpawnWest",
        "WestCorridorSpawn",
        "WestDoorSpawn"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 240;

        if (!subscribedToSceneLoad)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            subscribedToSceneLoad = true;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || scene.name != "Boss Arena")
            return;

        if (!TryFinalizeBossArenaSetup())
            SpawnBossArenaLateBinder();
    }

    private static bool TryFinalizeBossArenaSetup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != "Boss Arena")
            return true;

        EnsureMainCameraExists();

        Time.timeScale = 1f;
        NormalizeBossRoomRenderOrder();
        ElevateBossRoomActorsRenderOrder();

        PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
        if (player == null)
            return false;

        if (!player.CompareTag("Player"))
            player.tag = "Player";

        ApplyPendingPlayerUpgradeSnapshot(player);

        BattleArenaBuilder arena = Object.FindFirstObjectByType<BattleArenaBuilder>();
        BossArenaSpawnConfig spawnConfig = Object.FindFirstObjectByType<BossArenaSpawnConfig>();
        player.transform.position = ResolveBossArenaSpawn(arena, spawnConfig);

        Vector3 playerPos = player.transform.position;
        if (!Mathf.Approximately(playerPos.z, 0f))
        {
            playerPos.z = 0f;
            player.transform.position = playerPos;
        }

        PositionBossesInArena(arena, spawnConfig);
        ConfigureBossArenaBoundsForCurrentLayout(arena);
        EnsureBossEncounterFallback(player, spawnConfig);

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();

        follow.target = player.transform;
        ApplyBossArenaZoom(cam, follow, spawnConfig);

        follow.SnapToTarget();
        return true;
    }

    private static void SpawnBossArenaLateBinder()
    {
        if (Object.FindFirstObjectByType<BossArenaLateBinder>() != null)
            return;

        GameObject binderObject = new GameObject("BossArenaLateBinder");
        binderObject.AddComponent<BossArenaLateBinder>();
    }

    private static void ApplyPendingPlayerUpgradeSnapshot(PlayerMovement player)
    {
        if (player == null)
            return;

        List<PlayerUpgradeDeck.UpgradeStackSnapshot> snapshot;
        if (!PlayerUpgradeTransitionState.TryConsume(out snapshot))
            return;

        PlayerUpgradeDeck deck = player.GetComponent<PlayerUpgradeDeck>();
        if (deck == null)
            deck = player.gameObject.AddComponent<PlayerUpgradeDeck>();

        if (deck != null)
            deck.ApplyRuntimeSnapshot(snapshot);
    }

    private static Vector3 ResolveBossArenaSpawn(BattleArenaBuilder arena, BossArenaSpawnConfig spawnConfig)
    {
        // Consume one-shot transition data even though boss arena now uses fixed top/bottom staging.
        BossArenaTransitionState.TransitionData consumedTransitionData;
        BossArenaTransitionState.TryConsumeEntry(out consumedTransitionData);

        float minX;
        float maxX;
        float minY;
        float maxY;
        if (TryGetBossArenaBounds(arena, out minX, out maxX, out minY, out maxY))
        {
            Vector3 defaultSpawn = new Vector3(
                (minX + maxX) * 0.5f,
                Mathf.Clamp(minY + BossArenaPlayerBottomInset, minY, maxY),
                0f);

            if (spawnConfig != null && spawnConfig.overridePlayerSpawn)
            {
                return ResolveSpawnPointFromConfig(
                    minX,
                    maxX,
                    minY,
                    maxY,
                    spawnConfig.playerAnchor,
                    spawnConfig.playerOffset,
                    spawnConfig.playerCustomWorldPosition,
                    defaultSpawn);
            }

            return defaultSpawn;
        }

        Vector3 fallbackSpawn = GetDefaultBossSpawn(arena);
        return new Vector3(fallbackSpawn.x, fallbackSpawn.y, 0f);
    }

    private static Vector3 GetDefaultBossSpawn(BattleArenaBuilder arena)
    {
        if (arena != null)
            return arena.GetSpawnPosition();

        Vector3 roomCenter;
        if (TryGetBossRoomCenter(out roomCenter))
            return roomCenter;

        return Vector3.zero;
    }

    private static bool TryGetBossRoomCenter(out Vector3 center)
    {
        GameObject bossRoom = FindInActiveSceneByName("Boss_Room");
        if (bossRoom == null)
        {
            center = default;
            return false;
        }

        TilemapRenderer tilemapRenderer = bossRoom.GetComponentInChildren<TilemapRenderer>();
        if (tilemapRenderer != null)
        {
            center = tilemapRenderer.bounds.center;
            center.z = 0f;
            return true;
        }

        center = bossRoom.transform.position;
        center.z = 0f;
        return true;
    }

    private static void NormalizeBossRoomRenderOrder()
    {
        GameObject bossRoom = FindInActiveSceneByName("Boss_Room");
        if (bossRoom == null)
            return;

        TilemapRenderer[] tilemapRenderers = bossRoom.GetComponentsInChildren<TilemapRenderer>(true);
        for (int i = 0; i < tilemapRenderers.Length; i++)
        {
            TilemapRenderer tilemapRenderer = tilemapRenderers[i];
            if (tilemapRenderer == null)
                continue;

            string objectName = tilemapRenderer.gameObject.name.ToLowerInvariant();

            // Force map to sit at the very bottom so actors/summons always render above it.
            int sortingOrder = -120;
            if (objectName.Contains("floor") || objectName.Contains("background") || objectName.Contains("backlayer"))
                sortingOrder = -130;
            else if (objectName.Contains("door"))
                sortingOrder = -118;
            else if (objectName.Contains("foreground") || objectName.Contains("deco"))
                sortingOrder = -116;
            else if (objectName.Contains("wall"))
                sortingOrder = -114;

            tilemapRenderer.sortingLayerName = "Default";
            tilemapRenderer.sortingOrder = sortingOrder;
        }
    }

    private static void ElevateBossRoomActorsRenderOrder()
    {
        PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            SpriteRenderer playerRenderer = player.playerSpriteRenderer;
            if (playerRenderer == null)
                playerRenderer = player.GetComponent<SpriteRenderer>();

            if (playerRenderer != null)
            {
                playerRenderer.sortingLayerName = "Default";
                playerRenderer.sortingOrder = Mathf.Max(playerRenderer.sortingOrder, BossScenePlayerSortingOrder);
                if (!playerRenderer.enabled)
                    playerRenderer.enabled = true;

                Color playerColor = playerRenderer.color;
                if (playerColor.a < 1f)
                {
                    playerColor.a = 1f;
                    playerRenderer.color = playerColor;
                }
            }
        }

        BossEnemy[] bosses = Object.FindObjectsByType<BossEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
            BossEnemy boss = bosses[i];
            if (boss == null)
                continue;

            SpriteRenderer bossRenderer = boss.GetComponent<SpriteRenderer>();
            if (bossRenderer == null)
                continue;

            bossRenderer.sortingLayerName = "Default";
            bossRenderer.sortingOrder = Mathf.Max(bossRenderer.sortingOrder, BossSceneBossSortingOrder);
            if (!bossRenderer.enabled)
                bossRenderer.enabled = true;

            Color bossColor = bossRenderer.color;
            if (bossColor.a < 1f)
            {
                bossColor.a = 1f;
                bossRenderer.color = bossColor;
            }
        }
    }

    private static void PositionBossesInArena(BattleArenaBuilder arena, BossArenaSpawnConfig spawnConfig)
    {
        BossEnemy[] bosses = Object.FindObjectsByType<BossEnemy>(FindObjectsSortMode.None);
        if (bosses == null || bosses.Length == 0)
            return;

        float minX;
        float maxX;
        float minY;
        float maxY;
        if (!TryGetBossArenaBounds(arena, out minX, out maxX, out minY, out maxY))
            return;

        Vector3 defaultTarget = new Vector3(
            (minX + maxX) * 0.5f,
            Mathf.Clamp(maxY - BossArenaBossTopInset, minY, maxY),
            0f);

        Vector3 targetPosition = defaultTarget;
        if (spawnConfig != null && spawnConfig.overrideBossSpawn)
        {
            targetPosition = ResolveSpawnPointFromConfig(
                minX,
                maxX,
                minY,
                maxY,
                spawnConfig.bossAnchor,
                spawnConfig.bossOffset,
                spawnConfig.bossCustomWorldPosition,
                defaultTarget);
        }

        for (int i = 0; i < bosses.Length; i++)
        {
            BossEnemy boss = bosses[i];
            if (boss == null)
                continue;

            boss.transform.position = targetPosition;

            Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = targetPosition;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    private static Vector3 ResolveSpawnPointFromConfig(
        float minX,
        float maxX,
        float minY,
        float maxY,
        BossArenaSpawnConfig.SpawnAnchor anchor,
        Vector2 offset,
        Vector2 customWorldPosition,
        Vector3 fallback)
    {
        Vector3 basePoint;
        switch (anchor)
        {
            case BossArenaSpawnConfig.SpawnAnchor.Top:
                basePoint = new Vector3((minX + maxX) * 0.5f, maxY, 0f);
                break;
            case BossArenaSpawnConfig.SpawnAnchor.Bottom:
                basePoint = new Vector3((minX + maxX) * 0.5f, minY, 0f);
                break;
            case BossArenaSpawnConfig.SpawnAnchor.Left:
                basePoint = new Vector3(minX, (minY + maxY) * 0.5f, 0f);
                break;
            case BossArenaSpawnConfig.SpawnAnchor.Right:
                basePoint = new Vector3(maxX, (minY + maxY) * 0.5f, 0f);
                break;
            case BossArenaSpawnConfig.SpawnAnchor.TopLeft:
                basePoint = new Vector3(minX, maxY, 0f);
                break;
            case BossArenaSpawnConfig.SpawnAnchor.TopRight:
                basePoint = new Vector3(maxX, maxY, 0f);
                break;
            case BossArenaSpawnConfig.SpawnAnchor.BottomLeft:
                basePoint = new Vector3(minX, minY, 0f);
                break;
            case BossArenaSpawnConfig.SpawnAnchor.BottomRight:
                basePoint = new Vector3(maxX, minY, 0f);
                break;
            case BossArenaSpawnConfig.SpawnAnchor.CustomWorldPosition:
                basePoint = new Vector3(customWorldPosition.x, customWorldPosition.y, 0f);
                break;
            case BossArenaSpawnConfig.SpawnAnchor.Center:
                basePoint = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
                break;
            default:
                basePoint = fallback;
                break;
        }

        Vector3 resolved = basePoint + new Vector3(offset.x, offset.y, 0f);
        resolved.x = Mathf.Clamp(resolved.x, minX, maxX);
        resolved.y = Mathf.Clamp(resolved.y, minY, maxY);
        resolved.z = 0f;
        return resolved;
    }

    private static bool TryGetBossArenaBounds(BattleArenaBuilder arena, out float minX, out float maxX, out float minY, out float maxY)
    {
        if (arena != null)
        {
            arena.GetBattleArenaBounds(BossArenaBoundsMargin, out minX, out maxX, out minY, out maxY);
            return true;
        }

        GameObject bossRoom = FindInActiveSceneByName("Boss_Room");
        if (bossRoom != null)
        {
            TilemapRenderer[] tilemapRenderers = bossRoom.GetComponentsInChildren<TilemapRenderer>(true);
            bool hasBounds = false;
            Bounds combinedBounds = default;

            for (int i = 0; i < tilemapRenderers.Length; i++)
            {
                TilemapRenderer tilemapRenderer = tilemapRenderers[i];
                if (tilemapRenderer == null)
                    continue;

                if (!hasBounds)
                {
                    combinedBounds = tilemapRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(tilemapRenderer.bounds);
                }
            }

            if (hasBounds)
            {
                minX = combinedBounds.min.x + BossArenaBoundsMargin;
                maxX = combinedBounds.max.x - BossArenaBoundsMargin;
                minY = combinedBounds.min.y + BossArenaBoundsMargin;
                maxY = combinedBounds.max.y - BossArenaBoundsMargin;

                if (minX >= maxX || minY >= maxY)
                {
                    Vector3 center = combinedBounds.center;
                    minX = center.x - 8f;
                    maxX = center.x + 8f;
                    minY = center.y - 8f;
                    maxY = center.y + 8f;
                }

                return true;
            }
        }

        Vector3 fallbackCenter;
        if (TryGetBossRoomCenter(out fallbackCenter))
        {
            minX = fallbackCenter.x - 8f;
            maxX = fallbackCenter.x + 8f;
            minY = fallbackCenter.y - 8f;
            maxY = fallbackCenter.y + 8f;
            return true;
        }

        minX = -8f;
        maxX = 8f;
        minY = -8f;
        maxY = 8f;
        return false;
    }

    private static void ConfigureBossArenaBoundsForCurrentLayout(BattleArenaBuilder arena)
    {
        BossEnemy[] bosses = Object.FindObjectsByType<BossEnemy>(FindObjectsSortMode.None);
        if (bosses == null || bosses.Length == 0)
            return;

        float minX;
        float maxX;
        float minY;
        float maxY;
        if (!TryGetBossArenaBounds(arena, out minX, out maxX, out minY, out maxY))
            return;

        for (int i = 0; i < bosses.Length; i++)
        {
            BossEnemy boss = bosses[i];
            if (boss == null)
                continue;

            boss.ConfigureArenaBounds(minX, maxX, minY, maxY);
        }
    }

    private static void ApplyBossArenaZoom(Camera cam, CameraFollow follow, BossArenaSpawnConfig spawnConfig)
    {
        if (cam == null || !cam.orthographic)
            return;

        float zoomSize = BossArenaCameraSize;
        if (spawnConfig != null && spawnConfig.overrideCameraSize)
            zoomSize = Mathf.Max(1f, spawnConfig.cameraSize);

        cam.orthographicSize = zoomSize;

        if (follow != null)
            follow.orthographicSize = zoomSize;
    }

    private static void EnsureBossEncounterFallback(PlayerMovement player, BossArenaSpawnConfig spawnConfig)
    {
        if (player == null)
            return;

        if (Object.FindFirstObjectByType<BattleArenaTrigger>() != null)
            return;

        if (Object.FindFirstObjectByType<BossArenaTrigger>() != null)
            return;

        if (Object.FindFirstObjectByType<BossAutoWakeFallback>() != null)
            return;

        GameObject fallbackObject = new GameObject("BossAutoWakeFallback");
        BossAutoWakeFallback fallback = fallbackObject.AddComponent<BossAutoWakeFallback>();
        fallback.Configure(player.transform, spawnConfig);
    }

    private static bool TryFindDirectionalMarker(Vector2Int moveDirection, out Vector3 markerPosition)
    {
        BossCorridor corridor;
        if (!TryGetEntryCorridor(moveDirection, out corridor))
        {
            markerPosition = default;
            return false;
        }

        string[] markerNames = GetCorridorMarkerNames(corridor);
        for (int i = 0; i < markerNames.Length; i++)
        {
            GameObject marker = FindInActiveSceneByName(markerNames[i]);
            if (marker == null)
                continue;

            markerPosition = marker.transform.position;
            markerPosition.z = 0f;
            return true;
        }

        markerPosition = default;
        return false;
    }

    private static bool TryFindCorridorDoorSpawn(Vector2Int moveDirection, out Vector3 spawnPosition)
    {
        BossCorridor corridor;
        if (!TryGetEntryCorridor(moveDirection, out corridor))
        {
            spawnPosition = default;
            return false;
        }

        GameObject doorObject = FindInActiveSceneByName(GetCorridorDoorName(corridor));
        if (doorObject == null)
        {
            spawnPosition = default;
            return false;
        }

        Renderer doorRenderer = doorObject.GetComponent<Renderer>();
        if (doorRenderer == null)
        {
            spawnPosition = doorObject.transform.position;
            spawnPosition.z = 0f;
            return true;
        }

        spawnPosition = doorRenderer.bounds.center + (GetCorridorDirection(corridor) * CorridorSpawnInset);
        spawnPosition.z = 0f;
        return true;
    }

    private static bool TryGetEntryCorridor(Vector2Int moveDirection, out BossCorridor corridor)
    {
        // Entering boss room to the right means player came from the left corridor, and so on.
        if (moveDirection == Vector2Int.right)
        {
            corridor = BossCorridor.West;
            return true;
        }

        if (moveDirection == Vector2Int.left)
        {
            corridor = BossCorridor.East;
            return true;
        }

        if (moveDirection == Vector2Int.up)
        {
            corridor = BossCorridor.South;
            return true;
        }

        if (moveDirection == Vector2Int.down)
        {
            corridor = BossCorridor.North;
            return true;
        }

        corridor = BossCorridor.South;
        return false;
    }

    private static string[] GetCorridorMarkerNames(BossCorridor corridor)
    {
        switch (corridor)
        {
            case BossCorridor.North:
                return NorthCorridorMarkerNames;
            case BossCorridor.South:
                return SouthCorridorMarkerNames;
            case BossCorridor.East:
                return EastCorridorMarkerNames;
            case BossCorridor.West:
                return WestCorridorMarkerNames;
            default:
                return SouthCorridorMarkerNames;
        }
    }

    private static string GetCorridorDoorName(BossCorridor corridor)
    {
        switch (corridor)
        {
            case BossCorridor.North:
                return "NorthDoor";
            case BossCorridor.South:
                return "SouthDoor";
            case BossCorridor.East:
                return "EastDoor";
            case BossCorridor.West:
                return "WestDoor";
            default:
                return "SouthDoor";
        }
    }

    private static Vector3 GetCorridorDirection(BossCorridor corridor)
    {
        switch (corridor)
        {
            case BossCorridor.North:
                return Vector3.up;
            case BossCorridor.South:
                return Vector3.down;
            case BossCorridor.East:
                return Vector3.right;
            case BossCorridor.West:
                return Vector3.left;
            default:
                return Vector3.down;
        }
    }

    private static bool ShouldUseLeftStair(Vector2Int moveDirection, float playerXBeforeTransition)
    {
        // Horizontal entries map directly to the same side in the boss room.
        if (moveDirection == Vector2Int.right)
            return true;

        if (moveDirection == Vector2Int.left)
            return false;

        // Vertical entries use pre-transition X as a tiebreaker.
        if (Mathf.Abs(playerXBeforeTransition) > 0.01f)
            return playerXBeforeTransition < 0f;

        // Deterministic fallback for perfectly centered vertical entries.
        return moveDirection == Vector2Int.down;
    }

    private static bool TryFindStairMarkerPosition(bool useLeftStair, out Vector3 markerPosition)
    {
        string[] markerNames = useLeftStair ? LeftStairMarkerNames : RightStairMarkerNames;

        for (int i = 0; i < markerNames.Length; i++)
        {
            GameObject marker = FindInActiveSceneByName(markerNames[i]);
            if (marker == null)
                continue;

            markerPosition = marker.transform.position;
            return true;
        }

        markerPosition = default;
        return false;
    }

    private static void EnsureMainCameraExists()
    {
        Camera main = Camera.main;
        if (main != null)
            return;

        Camera existingCamera = Object.FindFirstObjectByType<Camera>();
        if (existingCamera != null)
        {
            existingCamera.tag = "MainCamera";
            if (!existingCamera.enabled)
                existingCamera.enabled = true;
            return;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        Camera createdCamera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";

        createdCamera.orthographic = true;
        createdCamera.orthographicSize = DefaultOrthoSize;
        createdCamera.nearClipPlane = DefaultNearClip;
        createdCamera.farClipPlane = DefaultFarClip;
        createdCamera.transform.position = new Vector3(0f, 0f, -10f);

        if (Object.FindFirstObjectByType<AudioListener>() == null)
            cameraObject.AddComponent<AudioListener>();
    }

    private static GameObject FindInActiveSceneByName(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return null;

        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChildByNameRecursive(roots[i].transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindChildByNameRecursive(Transform parent, string objectName)
    {
        if (parent == null)
            return null;

        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildByNameRecursive(parent.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private sealed class BossArenaLateBinder : MonoBehaviour
    {
        private float remainingRetryTime = BossArenaSetupRetryDuration;

        private void Update()
        {
            if (TryFinalizeBossArenaSetup())
            {
                Destroy(gameObject);
                return;
            }

            remainingRetryTime -= Time.unscaledDeltaTime;
            if (remainingRetryTime <= 0f)
                Destroy(gameObject);
        }
    }

    private sealed class BossAutoWakeFallback : MonoBehaviour
    {
        private Transform playerTransform;
        private float elapsed;
        private bool cutsceneStarted;
        private bool useCutscene = true;
        private float panInDuration = BossAutoWakePanInDuration;
        private float focusHoldDuration = BossAutoWakeFocusHoldDuration;
        private float postWakeHoldDuration = BossAutoWakePostWakeHoldDuration;
        private float panOutDuration = BossAutoWakePanOutDuration;

        public void Configure(Transform player, BossArenaSpawnConfig spawnConfig)
        {
            playerTransform = player;

            if (spawnConfig == null)
                return;

            useCutscene = spawnConfig.enableFallbackIntroCutscene;
            panInDuration = Mathf.Max(0f, spawnConfig.fallbackPanInDuration);
            focusHoldDuration = Mathf.Max(0f, spawnConfig.fallbackFocusHoldDuration);
            postWakeHoldDuration = Mathf.Max(0f, spawnConfig.fallbackPostWakeHoldDuration);
            panOutDuration = Mathf.Max(0f, spawnConfig.fallbackPanOutDuration);
        }

        private void Update()
        {
            if (cutsceneStarted)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.name != "Boss Arena")
            {
                Destroy(gameObject);
                return;
            }

            BossEnemy boss = Object.FindFirstObjectByType<BossEnemy>();
            if (boss == null)
            {
                Destroy(gameObject);
                return;
            }

            if (boss.HasAwoken)
            {
                Destroy(gameObject);
                return;
            }

            if (playerTransform == null)
            {
                PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
                if (player != null)
                    playerTransform = player.transform;
            }

            if (playerTransform == null)
                return;

            elapsed += Time.unscaledDeltaTime;
            float distance = Vector2.Distance(playerTransform.position, boss.transform.position);
            if (distance > BossAutoWakeDistance && elapsed < BossAutoWakeDelay)
                return;

            cutsceneStarted = true;

            if (!useCutscene)
            {
                StartBossEncounter(boss);
                Destroy(gameObject);
                return;
            }

            StartCoroutine(PlayFallbackIntroCutscene(boss));
        }

        private IEnumerator PlayFallbackIntroCutscene(BossEnemy boss)
        {
            Camera cam = Camera.main;
            CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            bool followWasEnabled = follow != null && follow.enabled;
            Vector3 camStart = cam != null ? cam.transform.position : Vector3.zero;

            if (followWasEnabled)
                follow.enabled = false;

            Time.timeScale = 0f;

            if (cam != null && boss != null)
            {
                Vector3 camBoss = new Vector3(boss.transform.position.x, boss.transform.position.y, camStart.z);
                yield return PanCameraUnscaled(cam.transform, camStart, camBoss, panInDuration);
            }

            yield return new WaitForSecondsRealtime(focusHoldDuration);

            Time.timeScale = 1f;

            StartBossEncounter(boss);

            yield return new WaitForSecondsRealtime(postWakeHoldDuration);

            if (cam != null)
            {
                Vector3 camReturn = camStart;
                if (playerTransform != null)
                    camReturn = new Vector3(playerTransform.position.x, playerTransform.position.y, camStart.z);

                yield return PanCameraUnscaled(cam.transform, cam.transform.position, camReturn, panOutDuration);
            }

            if (follow != null)
            {
                follow.enabled = followWasEnabled;
                if (followWasEnabled)
                    follow.SnapToTarget();
            }

            Time.timeScale = 1f;
            Destroy(gameObject);
        }

        private static void StartBossEncounter(BossEnemy boss)
        {
            BossArenaAudioController bossArenaAudio = BossArenaAudioController.FindInstance();
            if (bossArenaAudio != null)
                bossArenaAudio.OnBossArenaEntered();

            BossHealthBar bossHealthBar = Object.FindFirstObjectByType<BossHealthBar>();
            if (bossHealthBar != null)
                bossHealthBar.RevealBar();

            PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm == null || !pm.isSuperActive)
            {
                PlayerHealth ph = Object.FindFirstObjectByType<PlayerHealth>();
                if (ph != null)
                    ph.ResetCombatInvincibility();
            }

            if (boss != null && !boss.HasAwoken)
                boss.WakeUp();
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

            float elapsedPan = 0f;
            while (elapsedPan < duration)
            {
                elapsedPan += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsedPan / duration);
                float eased = t * t * (3f - 2f * t);
                camTransform.position = Vector3.Lerp(from, to, eased);
                yield return null;
            }

            camTransform.position = to;
        }
    }
}
