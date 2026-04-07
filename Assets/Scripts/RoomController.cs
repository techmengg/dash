using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class RoomController : MonoBehaviour
{
    public GameObject[] roomLayouts;
    private GameObject currentActiveLayout;

    [Header("References")]
    public DungeonGenerator generator;
    public ScreenFader fader;
    public MinimapDisplay minimap;

    [Header("Doors")]
    public GameObject doorN, doorS, doorE, doorW;

    [Header("Enemy Spawning")]
    public GameObject meleeEnemyPrefab;
    public GameObject rangedEnemyPrefab;
    public int enemiesPerRoom = 10;
    public LayerMask wallLayer;
    public float spawnCheckRadius = 0.5f;
    public float spawnDelay = 5f;
    private List<GameObject> currentEnemies = new List<GameObject>();

    [Header("Trap Spawning")]
    public GameObject spikeTrapPrefab;
    public int trapsPerRoom = 5;
    public float trapMinSpacing = 5f;
    private List<GameObject> currentTraps = new List<GameObject>();

    [Header("Wave UI")]
    public TMP_FontAsset waveFont;
    private GameObject waveCanvas;
    private TextMeshProUGUI waveText;
    private Coroutine waveTextCoroutine;
    private Coroutine waveClearedCoroutine;

    [Header("Debug Wave Controls")]
    public bool enableQuickWaveClear = true;
    public KeyCode quickWaveClearKey = KeyCode.Q;

    [Header("State")]
    public Vector2Int currentGridPos = Vector2Int.zero;
    private bool waveActive = false;
    private int currentWave = 1;
    private int maxWave = 5;
    private bool hasInitializedFirstRoom;
    private HashSet<Vector2Int> clearedRooms = new HashSet<Vector2Int>();
    private bool hasLoggedRoomDataNotReady;
    private bool hasLoggedStartRoomNotReady;
    private bool hasPlayedBossAvailableSfx;
    private bool isTransitioningToBossArena;
    private PlayerUpgradeDeck upgradeDeck;
    private RoomAudioController roomAudio;
    private WaveGoalChecklistUI waveGoalUi;

    [Header("Boss Arena Transition")]
    public string bossArenaSceneName = "Boss Arena";
    [Min(1)] public int wavesRequiredForBossRoom = 3;
    public bool IsWaveActive() => waveActive;
    public int GetCurrentWave() => currentWave;

    void Start()
    {
        // Recover from any stale pause state left by UI systems.
        Time.timeScale = 1f;

        if (generator == null)
            generator = Object.FindFirstObjectByType<DungeonGenerator>();

        if (minimap == null)
            minimap = Object.FindFirstObjectByType<MinimapDisplay>();

        roomAudio = RoomAudioController.FindInstance();
        waveGoalUi = WaveGoalChecklistUI.FindInstance();

        StartCoroutine(BootstrapRoomStartup());
    }

    private IEnumerator BootstrapRoomStartup()
    {
        if (generator == null)
        {
            Debug.LogError("RoomController: No DungeonGenerator found. Room scene cannot start.");
            yield break;
        }

        // Give other Start() methods one frame to run first.
        yield return null;

        var roomData = generator.GetRoomData();
        if (roomData == null || roomData.Count == 0)
            generator.GenerateDungeon();

        const int maxFramesToWait = 30;
        for (int i = 0; i < maxFramesToWait && !hasInitializedFirstRoom; i++)
        {
            InitializeFirstRoom();
            if (hasInitializedFirstRoom)
                yield break;

            yield return null;
        }

        if (!hasInitializedFirstRoom)
            Debug.LogError("RoomController: Failed to initialize first room. Check DungeonGenerator and scene references.");
    }

    void Update()
    {
        if (hasInitializedFirstRoom && currentActiveLayout == null)
            UpdateRoomVisuals();

        if (waveActive)
        {
            if (enableQuickWaveClear && Input.GetKeyDown(quickWaveClearKey))
            {
                ForceClearCurrentWave();
                return;
            }

            // Remove destroyed/null enemies from the list
            currentEnemies.RemoveAll(e => e == null);

            // Check if all enemies are dead
            bool allDead = true;
            foreach (GameObject enemy in currentEnemies)
            {
                EnemyHealth eh = enemy.GetComponent<EnemyHealth>();
                if (eh != null && !eh.IsDead)
                {
                    allDead = false;
                    break;
                }
            }

            if (allDead && currentEnemies.Count == 0 || allDead)
            {
                waveActive = false;
                clearedRooms.Add(currentGridPos);
                SetDoorsLocked(false);
                currentWave++;
                RefreshWaveGoalUi();
                waveClearedCoroutine = StartCoroutine(ShowWaveCleared());
            }
        }
    }

    private void ForceClearCurrentWave()
    {
        if (!waveActive)
            return;

        ClearEnemies();
        waveActive = false;
        clearedRooms.Add(currentGridPos);
        SetDoorsLocked(false);
        currentWave++;
        RefreshWaveGoalUi();

        if (waveClearedCoroutine != null)
            StopCoroutine(waveClearedCoroutine);

        waveClearedCoroutine = StartCoroutine(ShowWaveCleared());
        Debug.Log($"Wave force-cleared via {quickWaveClearKey}. CurrentWave is now {currentWave}.");
    }

    public void InitializeFirstRoom()
    {
        if (hasInitializedFirstRoom)
            return;

        if (generator == null)
            generator = Object.FindFirstObjectByType<DungeonGenerator>();

        if (generator == null)
        {
            Debug.LogError("RoomController: Cannot initialize first room without DungeonGenerator.");
            return;
        }

        var rooms = generator.GetRoomData();
        if (rooms == null || !rooms.ContainsKey(Vector2Int.zero))
        {
            generator.GenerateDungeon();
            rooms = generator.GetRoomData();
        }

        if (rooms == null || !rooms.ContainsKey(Vector2Int.zero))
        {
            if (!hasLoggedStartRoomNotReady)
            {
                Debug.LogWarning("RoomController: Start room data is not ready yet.");
                hasLoggedStartRoomNotReady = true;
            }
            return;
        }

        hasLoggedStartRoomNotReady = false;

        // Set maxWave to the number of normal (combat) rooms in the dungeon
        int normalRoomCount = 0;
        foreach (var room in rooms.Values)
        {
            if (room.type == RoomType.Normal)
                normalRoomCount++;
        }
        maxWave = Mathf.Max(1, normalRoomCount);
        wavesRequiredForBossRoom = Mathf.Max(1, maxWave - 1);
        Debug.Log($"Dungeon has {normalRoomCount} combat rooms. maxWave={maxWave}, bossUnlock={wavesRequiredForBossRoom}");

        currentGridPos = Vector2Int.zero;
        UpdateRoomVisuals();
        if (minimap != null)
            minimap.UpdatePlayerLocation(currentGridPos);

        RefreshWaveGoalUi();

        hasInitializedFirstRoom = true;
    }

    private void RefreshWaveGoalUi()
    {
        if (waveGoalUi == null)
            waveGoalUi = WaveGoalChecklistUI.FindInstance();

        if (roomAudio == null)
            roomAudio = RoomAudioController.FindInstance();

        int clearedWaves = Mathf.Max(0, currentWave - 1);
        int requiredWaves = Mathf.Max(1, wavesRequiredForBossRoom);
        bool bossAvailable = clearedWaves >= requiredWaves;

        if (waveGoalUi != null)
            waveGoalUi.Refresh(clearedWaves, requiredWaves, bossAvailable);

        if (bossAvailable)
        {
            if (!hasPlayedBossAvailableSfx && roomAudio != null)
            {
                roomAudio.PlayBossAvailableSfx();
                hasPlayedBossAvailableSfx = true;
            }
        }
        else
        {
            hasPlayedBossAvailableSfx = false;
        }
    }

    public void TryMove(Vector2Int direction)
    {
        if (isTransitioningToBossArena)
            return;

        if (generator == null)
            generator = Object.FindFirstObjectByType<DungeonGenerator>();

        if (generator == null)
        {
            Debug.LogError("RoomController: Cannot move rooms without DungeonGenerator.");
            return;
        }

        Vector2Int targetPos = currentGridPos + direction;
        Debug.Log($"Attempting to move from {currentGridPos} to {targetPos}");

        var roomData = generator.GetRoomData();

        if (roomData.ContainsKey(targetPos))
        {
            RoomData targetRoom = roomData[targetPos];
            if (targetRoom != null && targetRoom.type == RoomType.Boss)
            {
                int clearedWaves = Mathf.Max(0, currentWave - 1);
                if (clearedWaves < wavesRequiredForBossRoom)
                {
                    Debug.Log($"Boss room is locked. Clear {wavesRequiredForBossRoom} waves first. Cleared: {clearedWaves}");
                    return;
                }

                CacheBossArenaEntry(direction);
                StartCoroutine(EnterBossArenaSequence());
                return;
            }

            Debug.Log("Room found! Starting transition...");
            StartCoroutine(MoveSequence(targetPos, direction));
        }
        else
        {
            Debug.LogWarning($"No room exists at {targetPos} on the map!");
        }
    }

    private IEnumerator EnterBossArenaSequence()
    {
        if (isTransitioningToBossArena)
            yield break;

        isTransitioningToBossArena = true;

        PlayerUpgradeDeck deck = GetOrCreateUpgradeDeck();
        if (deck != null)
            PlayerUpgradeTransitionState.QueueFromDeck(deck);
        else
            PlayerUpgradeTransitionState.Clear();

        if (Time.timeScale <= 0f)
            Time.timeScale = 1f;

        if (fader != null)
            yield return StartCoroutine(fader.FadeToBlack());

        SceneManager.LoadScene(bossArenaSceneName);
    }

    private void CacheBossArenaEntry(Vector2Int direction)
    {
        float playerX = 0f;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
            if (playerMovement != null)
                playerObj = playerMovement.gameObject;
        }

        if (playerObj != null)
            playerX = playerObj.transform.position.x;

        BossArenaTransitionState.QueueEntry(direction, playerX);
    }

    IEnumerator MoveSequence(Vector2Int targetPos, Vector2Int direction)
    {
        // Guard against stale global pause states when changing rooms.
        if (Time.timeScale <= 0f)
            Time.timeScale = 1f;

        // 1. Start the Fade to Black
        if (fader != null)
        {
            // We call a new version of the fade that stops at full black
            yield return StartCoroutine(fader.FadeToBlack());
        }

        // --- EVERYTHING BELOW HAPPENS WHILE THE SCREEN IS PITCH BLACK ---

        // 2. Update the logic and visuals
        currentGridPos = targetPos;
        ClearEnemies();
        ClearTraps();
        UpdateRoomVisuals();
        TeleportPlayer(direction);
        Physics2D.SyncTransforms();
        SpawnTraps();

        // 3. Update the Minimap
        if (minimap != null)
        {
            minimap.UpdatePlayerLocation(currentGridPos);
        }

        // 4. Wait a tiny fraction of a second so the camera can catch up
        yield return new WaitForSeconds(0.1f);

        // 5. Fade back to clear
        if (fader != null)
        {
            yield return StartCoroutine(fader.FadeToClear());
        }

        if (Time.timeScale <= 0f)
            Time.timeScale = 1f;

        // 6. Show wave UI and spawn enemies after delay, or show cleared text
        var rooms = generator.GetRoomData();
        if (rooms != null && rooms.ContainsKey(currentGridPos))
        {
            RoomData data = rooms[currentGridPos];
            if (data.type != RoomType.Start && data.type != RoomType.Boss)
            {
                if (clearedRooms.Contains(currentGridPos))
                {
                    StartCoroutine(ShowRoomAlreadyCleared());
                }
                else
                {
                    StartCoroutine(DelayedSpawn());
                }
            }
        }
    }

    IEnumerator DelayedSpawn()
    {
        if (currentWave > maxWave) yield break;

        // Lock doors when wave starts
        SetDoorsLocked(true);

        // Show current wave text
        ShowWaveText($"WAVE {currentWave}");

        yield return new WaitForSeconds(spawnDelay);

        // Hide the wave text
        HideWaveText();

        // Spawn enemies based on current wave and mark wave as active
        SpawnWaveEnemies();
        waveActive = true;
    }

    void ShowWaveText(string text)
    {
        if (roomAudio == null)
            roomAudio = RoomAudioController.FindInstance();

        if (roomAudio != null)
            roomAudio.PlayWaveTextPopupSfx();

        // Stop any existing wave text animation to prevent it from fading out new text
        if (waveTextCoroutine != null) StopCoroutine(waveTextCoroutine);

        if (waveCanvas == null)
        {
            // Create canvas
            waveCanvas = new GameObject("WaveCanvas");
            Canvas canvas = waveCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = waveCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Create text
            GameObject textObj = new GameObject("WaveText");
            textObj.transform.SetParent(waveCanvas.transform, false);
            waveText = textObj.AddComponent<TextMeshProUGUI>();
            waveText.alignment = TextAlignmentOptions.Center;
            waveText.fontSize = 80;
            waveText.color = Color.white;
            waveText.textWrappingMode = TextWrappingModes.NoWrap;

            // Use the configured wave font, or fall back to TMP's global default.
            if (waveFont != null)
                waveText.font = waveFont;
            else if (TMP_Settings.defaultFontAsset != null)
                waveText.font = TMP_Settings.defaultFontAsset;

            // Position at upper-center of screen
            RectTransform rt = waveText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.7f);
            rt.anchorMax = new Vector2(0.5f, 0.7f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(800, 150);
        }

        waveText.text = text;
        waveText.color = Color.white;
        waveCanvas.SetActive(true);
        waveTextCoroutine = StartCoroutine(AnimateWaveText());
    }

    IEnumerator AnimateWaveText()
    {
        if (waveText == null) yield break;

        // Punch scale in
        float elapsed = 0f;
        float punchDuration = 0.3f;
        waveText.transform.localScale = Vector3.one * 1.5f;

        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / punchDuration;
            float scale = Mathf.Lerp(1.5f, 1f, t * t);
            waveText.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        waveText.transform.localScale = Vector3.one;

        // Hold, then fade out near the end of the delay
        yield return new WaitForSeconds(spawnDelay - 1f);

        // Fade out over 0.5s
        elapsed = 0f;
        float fadeDuration = 0.5f;
        Color c = waveText.color;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            waveText.color = c;
            yield return null;
        }
    }

    void HideWaveText()
    {
        if (waveCanvas != null) waveCanvas.SetActive(false);
        if (waveText != null)
        {
            Color c = waveText.color;
            c.a = 1f;
            waveText.color = c;
        }
    }

    IEnumerator ShowWaveCleared()
    {
        if (roomAudio == null)
            roomAudio = RoomAudioController.FindInstance();

        if (roomAudio != null)
            roomAudio.PlayWaveClearedSfx();

        ShowWaveText("WAVE CLEARED");

        // Stop the default animation so we control timing ourselves
        if (waveTextCoroutine != null) StopCoroutine(waveTextCoroutine);
        waveTextCoroutine = null;

        // Hold for 2 seconds then fade out
        yield return new WaitForSeconds(2f);

        if (waveText != null)
        {
            float elapsed = 0f;
            float fadeDuration = 0.5f;
            Color c = waveText.color;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                waveText.color = c;
                yield return null;
            }
        }

        HideWaveText();

        yield return StartCoroutine(ShowCardSelectionReward());
    }

    IEnumerator ShowRoomAlreadyCleared()
    {
        ShowWaveText("ROOM CLEARED");

        // Stop the default animation so we control timing ourselves
        if (waveTextCoroutine != null) StopCoroutine(waveTextCoroutine);
        waveTextCoroutine = null;

        // Hold for 2 seconds then fade out
        yield return new WaitForSeconds(2f);

        if (waveText != null)
        {
            float elapsed = 0f;
            float fadeDuration = 0.5f;
            Color c = waveText.color;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                waveText.color = c;
                yield return null;
            }
        }

        HideWaveText();
    }

    private IEnumerator ShowCardSelectionReward()
    {
        PlayerUpgradeDeck deck = GetOrCreateUpgradeDeck();
        if (deck == null)
        {
            Debug.LogWarning("RoomController: No PlayerUpgradeDeck available, skipping card reward.");
            yield break;
        }

        PlayerUpgradeDeck.UpgradeDefinition leftUpgrade;
        PlayerUpgradeDeck.UpgradeDefinition rightUpgrade;
        if (!deck.TryGetCardPair(out leftUpgrade, out rightUpgrade))
        {
            Debug.Log("RoomController: No available upgrades left to offer.");
            yield break;
        }

        GameObject cardObj = new GameObject("RoomCardSelection");
        CardSelectionUI cardUI = cardObj.AddComponent<CardSelectionUI>();

        CardSelectionUI.CardData card1 = new CardSelectionUI.CardData
        {
            title = leftUpgrade.title,
            description = leftUpgrade.description,
            accentColor = leftUpgrade.accentColor
        };

        CardSelectionUI.CardData card2 = new CardSelectionUI.CardData
        {
            title = rightUpgrade.title,
            description = rightUpgrade.description,
            accentColor = rightUpgrade.accentColor
        };

        bool done = false;
        int selection = -1;

        cardUI.Show(card1, card2, picked =>
        {
            selection = picked;
            done = true;
        });

        while (!done)
            yield return null;

        if (selection == 0)
            deck.ApplyUpgrade(leftUpgrade);
        else if (selection == 1)
            deck.ApplyUpgrade(rightUpgrade);

        if (cardObj != null)
            Destroy(cardObj);

        yield return new WaitForSeconds(0.35f);
    }

    private PlayerUpgradeDeck GetOrCreateUpgradeDeck()
    {
        if (upgradeDeck != null)
            return upgradeDeck;

        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement == null)
            return null;

        upgradeDeck = playerMovement.GetComponent<PlayerUpgradeDeck>();
        if (upgradeDeck == null)
            upgradeDeck = playerMovement.gameObject.AddComponent<PlayerUpgradeDeck>();

        return upgradeDeck;
    }

    public GameObject[] roomLayoutPrefabs; // Drag your 6 prefabs here
    private GameObject currentLayoutInstance;

    void UpdateRoomVisuals()
    {
        // 1. SELF-HEAL: If the generator is null, try to find it
        if (generator == null)
        {
            generator = Object.FindFirstObjectByType<DungeonGenerator>();
        }

        // 2. CRITICAL CHECK: If we still can't find it, stop before we crash
        if (generator == null)
        {
            Debug.LogError("RoomController: No DungeonGenerator found in scene!");
            return;
        }

        var rooms = generator.GetRoomData();

        // 3. SAFETY CHECK: If the dictionary is empty or doesn't have our current position, STOP.
        if (rooms == null || !rooms.ContainsKey(currentGridPos))
        {
            // Fallback: still show a default layout so the room is playable even if data is late.
            if (currentActiveLayout == null && roomLayouts != null && roomLayouts.Length > 0 && roomLayouts[0] != null)
            {
                currentActiveLayout = Instantiate(roomLayouts[0], transform);
                currentActiveLayout.transform.localPosition = Vector3.zero;
                currentActiveLayout.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
            }

            if (!hasLoggedRoomDataNotReady)
            {
                Debug.LogWarning($"RoomController: Data for {currentGridPos} not ready yet!");
                hasLoggedRoomDataNotReady = true;
            }
            return;
        }

        hasLoggedRoomDataNotReady = false;

        RoomData data = rooms[currentGridPos];

        data.isVisited = true;

        if (MinimapDisplay.instance != null)
        {
            MinimapDisplay.instance.DrawMap(rooms);
            MinimapDisplay.instance.UpdatePlayerLocation(currentGridPos);
        }

        // --- Door Logic ---
        if (doorN != null) doorN.SetActive(rooms.ContainsKey(currentGridPos + Vector2Int.up));
        if (doorS != null) doorS.SetActive(rooms.ContainsKey(currentGridPos + Vector2Int.down));
        if (doorE != null) doorE.SetActive(rooms.ContainsKey(currentGridPos + Vector2Int.right));
        if (doorW != null) doorW.SetActive(rooms.ContainsKey(currentGridPos + Vector2Int.left));

        // --- Layout Spawning ---
        if (currentActiveLayout != null) Destroy(currentActiveLayout);

        if (data.layoutIndex == -1)
        {
            // 1. FORCE THE START ROOM
            // If the generator labeled this as the Start room, use the first prefab in your list (Index 0)
            if (data.type == RoomType.Start)
            {
                data.layoutIndex = 0;
            }
            else
            {
                // 2. RANDOMIZE EVERYTHING ELSE
                // We start the range at 1 so the Start Layout isn't used for normal rooms
                if (roomLayouts.Length > 1)
                    data.layoutIndex = Random.Range(1, roomLayouts.Length);
                else
                    data.layoutIndex = 0; // Fallback if you only have one prefab
            }
        }

        if (roomLayouts == null || roomLayouts.Length == 0)
        {
            Debug.LogError("RoomController: roomLayouts is empty. Assign room layout prefabs in the inspector.");
            return;
        }

        if (data.layoutIndex != -1)
        {
            // 1. Spawn the layout
            currentActiveLayout = Instantiate(roomLayouts[data.layoutIndex], transform);

            // 2. SET POSITION & SCALE
            currentActiveLayout.transform.localPosition = Vector3.zero;

            float targetScale = 2.5f;
            currentActiveLayout.transform.localScale = new Vector3(targetScale, targetScale, 1f);

            // 3. FORCE TO FRONT
            var renderers = currentActiveLayout.GetComponentsInChildren<TilemapRenderer>();

            Transform doorsParent = currentActiveLayout.transform.Find("Grid/Tilemap-Doors");

            if (doorsParent != null)
            {
                // Check the dictionary for neighbors and toggle the art
                doorsParent.Find("NorthDoor")?.gameObject.SetActive(rooms.ContainsKey(currentGridPos + Vector2Int.up));
                doorsParent.Find("SouthDoor")?.gameObject.SetActive(rooms.ContainsKey(currentGridPos + Vector2Int.down));
                doorsParent.Find("EastDoor")?.gameObject.SetActive(rooms.ContainsKey(currentGridPos + Vector2Int.right));
                doorsParent.Find("WestDoor")?.gameObject.SetActive(rooms.ContainsKey(currentGridPos + Vector2Int.left));
            }
        }

        CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
        if (cam != null)
        {
            ApplyCameraBounds(cam, new Vector2(-4f, -5.5f), new Vector2(4f, 5.5f));
        }
    }

    private void ApplyCameraBounds(CameraFollow cam, Vector2 min, Vector2 max)
    {
        if (cam == null)
            return;

        System.Type camType = cam.GetType();
        bool appliedAny = false;

        // Prefer rectangular bounds when available.
        var minBoundsField = camType.GetField("minBounds");
        var maxBoundsField = camType.GetField("maxBounds");

        if (minBoundsField != null && minBoundsField.FieldType == typeof(Vector2))
        {
            minBoundsField.SetValue(cam, min);
            appliedAny = true;
        }

        if (maxBoundsField != null && maxBoundsField.FieldType == typeof(Vector2))
        {
            maxBoundsField.SetValue(cam, max);
            appliedAny = true;
        }

        // Fallback to vertical-only bounds.
        var minYField = camType.GetField("minY");
        var maxYField = camType.GetField("maxY");

        if (minYField != null && minYField.FieldType == typeof(float))
        {
            minYField.SetValue(cam, min.y);
            appliedAny = true;
        }

        if (maxYField != null && maxYField.FieldType == typeof(float))
        {
            maxYField.SetValue(cam, max.y);
            appliedAny = true;
        }

        if (!appliedAny)
            Debug.LogWarning("RoomController: CameraFollow has no recognized bounds fields.");
    }
    void TeleportPlayer(Vector2Int dir)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (!player) return;

        Vector3 newPos = Vector3.zero;

        // Up means you entered from the bottom of the NEW room
        if (dir == Vector2Int.up)    newPos = new Vector3(0, -7f, 0);
        // Down means you entered from the top of the NEW room
        if (dir == Vector2Int.down)  newPos = new Vector3(0, 7.5f, 0);
        // Right means you entered from the left of the NEW room
        if (dir == Vector2Int.right) newPos = new Vector3(-13.5f, 0, 0);
        // Left means you entered from the right of the NEW room
        if (dir == Vector2Int.left)  newPos = new Vector3(13.5f, 0, 0);

        player.transform.position = newPos;
    }
    void SpawnWaveEnemies()
    {
        var rooms = generator.GetRoomData();
        if (rooms == null || !rooms.ContainsKey(currentGridPos)) return;

        RoomData data = rooms[currentGridPos];
        if (data.type == RoomType.Start || data.type == RoomType.Boss) return;

        if (meleeEnemyPrefab == null || rangedEnemyPrefab == null)
        {
            Debug.LogWarning("RoomController: Enemy prefabs not assigned!");
            return;
        }

        // Progressive wave scaling: starts at 4 enemies, grows steadily
        // with a cap of 18 so late waves stay challenging but fair
        if (currentWave > maxWave) return;

        float progress = (float)(currentWave - 1) / Mathf.Max(1, maxWave - 1); // 0..1
        int enemyCount = Mathf.RoundToInt(Mathf.Lerp(4f, 18f, progress));

        // Wave type: early = mixed, later waves lean melee or ranged
        // 0 = random mix, 1 = melee only, 2 = ranged only
        int waveType;
        if (progress < 0.6f)
            waveType = 0; // mixed
        else if (currentWave % 2 == 0)
            waveType = 2; // ranged
        else
            waveType = 1; // melee

        float xMin = -10f, xMax = 10f;
        float yMin = -5f, yMax = 5f;
        float minSpacing = 3f;
        float minDistFromPlayer = 5f;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        Vector3 playerPos = playerObj != null ? playerObj.transform.position : Vector3.zero;

        List<Vector3> spawnPositions = new List<Vector3>();

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPos = Vector3.zero;
            int attempts = 0;
            bool valid = false;

            while (!valid && attempts < 30)
            {
                spawnPos = new Vector3(
                    Random.Range(xMin, xMax),
                    Random.Range(yMin, yMax),
                    0f
                );

                valid = !Physics2D.OverlapCircle(spawnPos, spawnCheckRadius, wallLayer);

                if (valid && Vector3.Distance(spawnPos, playerPos) < minDistFromPlayer)
                    valid = false;

                if (valid)
                {
                    foreach (Vector3 existing in spawnPositions)
                    {
                        if (Vector3.Distance(spawnPos, existing) < minSpacing)
                        {
                            valid = false;
                            break;
                        }
                    }
                }
                attempts++;
            }

            if (!valid) continue;

            spawnPositions.Add(spawnPos);

            // Pick prefab based on wave type
            // In mixed waves, ranged ratio increases with progress (30% → 55%)
            GameObject prefab;
            if (waveType == 1)
                prefab = meleeEnemyPrefab;
            else if (waveType == 2)
                prefab = rangedEnemyPrefab;
            else
            {
                float rangedChance = Mathf.Lerp(0.3f, 0.55f, progress);
                prefab = Random.value < rangedChance ? rangedEnemyPrefab : meleeEnemyPrefab;
            }

            GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);

            SpriteRenderer enemySr = enemy.GetComponent<SpriteRenderer>();
            if (enemySr != null) enemySr.sortingOrder = 5;

            EnemyHealth eh = enemy.GetComponent<EnemyHealth>();
            if (eh != null) eh.canRespawn = false;

            currentEnemies.Add(enemy);
        }
    }

    void ClearEnemies()
    {
        foreach (GameObject enemy in currentEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        currentEnemies.Clear();
    }

    void SpawnTraps()
    {
        var rooms = generator.GetRoomData();
        if (rooms == null || !rooms.ContainsKey(currentGridPos)) return;

        RoomData data = rooms[currentGridPos];

        // No traps in start room or boss room
        if (data.type == RoomType.Start || data.type == RoomType.Boss) return;

        if (spikeTrapPrefab == null)
        {
            Debug.LogWarning("RoomController: Spike trap prefab not assigned!");
            return;
        }

        float xMin = -10f, xMax = 10f;
        float yMin = -5f, yMax = 5f;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        Vector3 playerPos = playerObj != null ? playerObj.transform.position : Vector3.zero;

        List<Vector3> trapPositions = new List<Vector3>();

        for (int i = 0; i < trapsPerRoom; i++)
        {
            Vector3 spawnPos = Vector3.zero;
            int attempts = 0;
            bool valid = false;

            while (!valid && attempts < 30)
            {
                spawnPos = new Vector3(
                    Random.Range(xMin, xMax),
                    Random.Range(yMin, yMax),
                    0f
                );

                // Not in walls
                valid = !Physics2D.OverlapCircle(spawnPos, spawnCheckRadius, wallLayer);

                // Away from player
                if (valid && Vector3.Distance(spawnPos, playerPos) < 3f)
                    valid = false;

                // Spaced from other traps
                if (valid)
                {
                    foreach (Vector3 existing in trapPositions)
                    {
                        if (Vector3.Distance(spawnPos, existing) < trapMinSpacing)
                        {
                            valid = false;
                            break;
                        }
                    }
                }
                attempts++;
            }

            if (!valid) continue; // skip if no valid position found

            trapPositions.Add(spawnPos);

            GameObject trap = Instantiate(spikeTrapPrefab, spawnPos, Quaternion.identity);

            SpriteRenderer trapSr = trap.GetComponent<SpriteRenderer>();
            if (trapSr != null) trapSr.sortingOrder = 3;

            currentTraps.Add(trap);
        }
    }

    void ClearTraps()
    {
        foreach (GameObject trap in currentTraps)
        {
            if (trap != null) Destroy(trap);
        }
        currentTraps.Clear();
    }

    void SetDoorsLocked(bool locked)
    {
        if (doorN != null) doorN.SetActive(!locked);
        if (doorS != null) doorS.SetActive(!locked);
        if (doorE != null) doorE.SetActive(!locked);
        if (doorW != null) doorW.SetActive(!locked);

        // Also lock/unlock doors inside the layout prefab
        if (currentActiveLayout != null)
        {
            Transform doorsParent = currentActiveLayout.transform.Find("Grid/Tilemap-Doors");
            if (doorsParent != null)
            {
                doorsParent.gameObject.SetActive(!locked);
            }
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        if (generator == null)
            return;

        var rooms = generator.GetRoomData();
        if (rooms == null || !rooms.ContainsKey(currentGridPos))
            return;
        UpdateRoomVisuals();
    }
}
