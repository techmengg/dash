using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages enemy waves in the battle arena.
/// Spawns enemies per wave, shows wave title text, and unlocks the exit door after all waves are cleared.
/// </summary>
public class WaveManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject meleePrefab;
    public GameObject rangedPrefab;

    [Header("Spawn Settings")]
    public float spawnRadius = 5f;
    public float delayBeforeFirstWave = 0.5f;
    public float delayBetweenWaves = 3f;

    [Header("Wave Text Style")]
    public float waveTextFontSize = 52f;
    public Color waveTextColor = new Color(0.85f, 0.25f, 0.2f, 1f);
    public float waveTextFadeIn = 0.4f;
    public float waveTextHold = 1f;
    public float waveTextFadeOut = 0.4f;

    [Header("Wave Text SFX")]
    public AudioSource waveTextSfxSource;
    public AudioClip waveTextPopupSfx;
    [Range(0f, 1f)] public float waveTextPopupSfxVolume = 0.9f;

    [Header("Debug Wave Controls")]
    public bool enableQuickWaveClear = true;
    public KeyCode quickWaveClearKey = KeyCode.Q;

    // Arena bounds for spawn clamping
    private float arenaCenterY;
    private float arenaHalfW;
    private float arenaHalfH;

    private List<GameObject> activeEnemies = new List<GameObject>();
    private Transform player;
    private int currentWave = 0;
    private bool wavesStarted = false;
    private bool wavesComplete = false;
    private Coroutine waveRoutine;
    private PlayerUpgradeDeck upgradeDeck;

    private void Update()
    {
        if (!enableQuickWaveClear || !wavesStarted || wavesComplete)
            return;

        if (Input.GetKeyDown(quickWaveClearKey))
            ForceClearCurrentWave();
    }

    private void Awake()
    {
        if (waveTextSfxSource == null)
            waveTextSfxSource = gameObject.AddComponent<AudioSource>();

        waveTextSfxSource.playOnAwake = false;
        waveTextSfxSource.loop = false;
        waveTextSfxSource.spatialBlend = 0f;
    }

    /// <summary>
    /// Starts the wave sequence. Call after the area title popup finishes.
    /// </summary>
    public void StartWaves(float centerY, float halfW, float halfH)
    {
        if (wavesStarted) return;
        wavesStarted = true;
        arenaCenterY = centerY;
        arenaHalfW = halfW;
        arenaHalfH = halfH;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        // Try to grab prefabs from the boss if not assigned
        if (meleePrefab == null || rangedPrefab == null)
        {
            BossEnemy boss = FindFirstObjectByType<BossEnemy>();
            if (boss != null)
            {
                if (meleePrefab == null) meleePrefab = boss.meleePrefab;
                if (rangedPrefab == null) rangedPrefab = boss.rangedPrefab;
            }
        }

        waveRoutine = StartCoroutine(WaveSequence());
    }

    /// <summary>
    /// Resets the wave manager for a fresh run (player respawn).
    /// </summary>
    public void ResetWaves()
    {
        if (waveRoutine != null)
            StopCoroutine(waveRoutine);

        // Destroy all active wave enemies
        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        activeEnemies.Clear();

        currentWave = 0;
        wavesStarted = false;
        wavesComplete = false;
    }

    private IEnumerator WaveSequence()
    {
        yield return new WaitForSeconds(delayBeforeFirstWave);

        // ── Wave 1: 5 melee ──
        yield return StartCoroutine(RunWave(1, 7, EnemyType.Melee));
        yield return StartCoroutine(ShowCardSelection());

        yield return new WaitForSeconds(delayBetweenWaves);

        // ── Wave 2: 3 ranged ──
        yield return StartCoroutine(RunWave(2, 7, EnemyType.Ranged));
        yield return StartCoroutine(ShowCardSelection());

        yield return new WaitForSeconds(delayBetweenWaves);

        // ── Wave 3: 5 random ──
        yield return StartCoroutine(RunWave(3, 10, EnemyType.Random));
        yield return StartCoroutine(ShowCardSelection());

        yield return new WaitForSeconds(delayBetweenWaves);

        // ── Wave 4: 10 random ──
        yield return StartCoroutine(RunWave(4, 15, EnemyType.Random));
        yield return StartCoroutine(ShowCardSelection());

        yield return new WaitForSeconds(delayBetweenWaves);

        // ── Wave 5: 10 random ──
        yield return StartCoroutine(RunWave(5, 20, EnemyType.Random));

        // All waves cleared — unlock the exit door to boss hallway
        wavesComplete = true;
        yield return StartCoroutine(ShowWaveText("WAVES CLEARED", new Color(0.2f, 0.85f, 0.3f, 1f)));

        UnlockBossDoor();
    }

    // ── CARD SELECTION ───────────────────────────────────────────

    private IEnumerator ShowCardSelection()
    {
        // Brief pause before showing cards
        yield return new WaitForSeconds(0.8f);

        PlayerUpgradeDeck deck = GetOrCreateUpgradeDeck();
        if (deck == null)
        {
            Debug.LogWarning("WaveManager: No PlayerUpgradeDeck available, skipping card reward.");
            yield break;
        }

        PlayerUpgradeDeck.UpgradeDefinition leftUpgrade;
        PlayerUpgradeDeck.UpgradeDefinition rightUpgrade;
        if (!deck.TryGetCardPair(out leftUpgrade, out rightUpgrade))
        {
            Debug.Log("WaveManager: No available upgrades left to offer.");
            yield break;
        }

        // Create the card UI
        GameObject cardObj = new GameObject("CardSelection");
        CardSelectionUI cardUI = cardObj.AddComponent<CardSelectionUI>();

        // Generate two cards from the player's upgrade deck.
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
        int choice = -1;

        cardUI.Show(card1, card2, (selected) =>
        {
            choice = selected;
            done = true;
        });

        // Wait for selection (game is paused via timeScale=0, use unscaled)
        while (!done)
            yield return null;

        if (choice == 0)
            deck.ApplyUpgrade(leftUpgrade);
        else if (choice == 1)
            deck.ApplyUpgrade(rightUpgrade);

        // Cleanup
        if (cardObj != null) Destroy(cardObj);

        yield return new WaitForSeconds(0.5f);
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

    private enum EnemyType { Melee, Ranged, Random }

    private IEnumerator RunWave(int waveNumber, int enemyCount, EnemyType type)
    {
        currentWave = waveNumber;

        // Show wave title
        yield return StartCoroutine(ShowWaveText("WAVE " + waveNumber));

        // Small pause after text fades out
        yield return new WaitForSeconds(0.5f);

        // Spawn enemies
        SpawnEnemies(enemyCount, type);

        // Wait until all enemies in this wave are dead
        while (true)
        {
            CleanupDead();
            if (activeEnemies.Count == 0)
                break;
            yield return new WaitForSeconds(0.3f);
        }
    }

    private void SpawnEnemies(int count, EnemyType type)
    {
        StartCoroutine(SpawnEnemiesStaggered(count, type));
    }

    private IEnumerator SpawnEnemiesStaggered(int count, EnemyType type)
    {
        float margin = 2.5f;
        float minSeparation = 7f;
        List<Vector2> usedPositions = new List<Vector2>();

        // Include player position as an occupied spot so enemies don't spawn on top of them
        if (player != null)
            usedPositions.Add(player.position);

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = PickPrefab(type);
            if (prefab == null) continue;

            // Find a position well-separated from all other spawns
            Vector2 spawnPos = FindSpreadPosition(usedPositions, minSeparation, margin);
            usedPositions.Add(spawnPos);

            GameObject spawned = Instantiate(prefab, spawnPos, Quaternion.identity);

            EnemyHealth eh = spawned.GetComponent<EnemyHealth>();
            if (eh != null)
                eh.canRespawn = false;

            activeEnemies.Add(spawned);

            // Pop-in effect
            StartCoroutine(SpawnPop(spawned.transform));

            // Stagger spawns so they don't all appear at once
            yield return new WaitForSeconds(0.15f);
        }
    }

    private GameObject PickPrefab(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Melee:
                return meleePrefab;
            case EnemyType.Ranged:
                return rangedPrefab;
            case EnemyType.Random:
                if (meleePrefab != null && rangedPrefab != null)
                    return Random.value < 0.5f ? meleePrefab : rangedPrefab;
                return meleePrefab != null ? meleePrefab : rangedPrefab;
            default:
                return meleePrefab;
        }
    }

    private Vector2 FindSpreadPosition(List<Vector2> used, float minDist, float margin)
    {
        float minX = -arenaHalfW + margin;
        float maxX = arenaHalfW - margin;
        float minY = arenaCenterY - arenaHalfH + margin;
        float maxY = arenaCenterY + arenaHalfH - margin;

        // Try random positions, pick the one farthest from all existing spawns
        Vector2 best = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
        float bestMinDist = 0f;

        // More attempts for better spread, especially with many enemies
        int attempts = 60;
        for (int a = 0; a < attempts; a++)
        {
            Vector2 candidate = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));

            // Find closest existing spawn/player to this candidate
            float closest = float.MaxValue;
            foreach (Vector2 pos in used)
            {
                float d = Vector2.Distance(candidate, pos);
                if (d < closest) closest = d;
            }

            if (closest > bestMinDist)
            {
                bestMinDist = closest;
                best = candidate;
                // Good enough — stop early
                if (bestMinDist >= minDist) break;
            }
        }

        return best;
    }

    private IEnumerator SpawnPop(Transform t)
    {
        if (t == null) yield break;
        Vector3 targetScale = t.localScale;
        t.localScale = Vector3.zero;

        float dur = 0.35f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            if (t == null) yield break;
            elapsed += Time.deltaTime;
            float p = elapsed / dur;
            // Overshoot bounce
            float scale;
            if (p < 0.7f)
                scale = Mathf.Lerp(0f, 1.15f, p / 0.7f);
            else
                scale = Mathf.Lerp(1.15f, 1f, (p - 0.7f) / 0.3f);
            t.localScale = targetScale * scale;
            yield return null;
        }
        if (t != null) t.localScale = targetScale;
    }

    private void CleanupDead()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
            {
                activeEnemies.RemoveAt(i);
                continue;
            }
            EnemyHealth eh = activeEnemies[i].GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead)
            {
                activeEnemies.RemoveAt(i);
            }
        }
    }

    private void ForceClearCurrentWave()
    {
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            if (activeEnemies[i] != null)
                Destroy(activeEnemies[i]);
        }

        activeEnemies.Clear();
        Debug.Log($"WaveManager: force-cleared current wave via {quickWaveClearKey}.");
    }

    // ── WAVE TEXT UI ──────────────────────────────────────────────

    private IEnumerator ShowWaveText(string text)
    {
        yield return StartCoroutine(ShowWaveText(text, waveTextColor));
    }

    private IEnumerator ShowWaveText(string text, Color color)
    {
        bool playedWavePopupSfx = PlayWaveTextPopupSfx();

        // Reuse AreaTitlePopup for consistent styling
        GameObject go = new GameObject("WavePopup");
        AreaTitlePopup popup = go.AddComponent<AreaTitlePopup>();
        // If explicit wave SFX path didn't play, let popup run its default title SFX logic.
        popup.playShowSfx = !playedWavePopupSfx;
        popup.areaName = text;
        popup.subtitle = "";
        popup.titleFontSize = waveTextFontSize;
        popup.titleColor = color;
        popup.titleLetterSpacing = 14f;
        popup.fadeInDuration = waveTextFadeIn;
        popup.holdDuration = waveTextHold;
        popup.fadeOutDuration = waveTextFadeOut;
        popup.lineColor = new Color(color.r, color.g, color.b, 0.4f);
        popup.lineWidth = 250f;

        bool done = false;
        popup.onComplete = () => done = true;
        popup.Show();

        while (!done)
            yield return null;

        if (go != null) Destroy(go);
    }

    private bool PlayWaveTextPopupSfx()
    {
        // Rooms scene wave popups should be driven by RoomAudioController.
        RoomAudioController roomAudio = RoomAudioController.FindInstance();
        if (roomAudio != null)
        {
            if (roomAudio.PlayWaveTextPopupSfx())
                return true;
        }

        // Keep boss fallback for non-Rooms contexts that still use WaveManager.
        BossArenaAudioController bossArenaAudio = BossArenaAudioController.FindInstance();
        if (bossArenaAudio != null)
        {
            if (bossArenaAudio.PlayTitlePopupSfx())
                return true;
        }

        if (waveTextSfxSource != null && waveTextPopupSfx != null)
        {
            waveTextSfxSource.PlayOneShot(waveTextPopupSfx, Mathf.Clamp01(waveTextPopupSfxVolume));
            return true;
        }

        Debug.LogWarning("WaveManager: Wave popup SFX could not play. Assign titlePopupSfx on RoomAudio/BossArenaAudio or assign WaveManager.waveTextPopupSfx.");
        return false;
    }

    // ── DOOR UNLOCK ──────────────────────────────────────────────

    private void UnlockBossDoor()
    {
        // Unlock the battle→boss hallway door (DoorBattleBarrier)
        // Also unlock the retreat door (DoorTransitionBarrier)
        DoorInteractable[] doors = FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
        foreach (DoorInteractable door in doors)
        {
            if (door.gameObject.name == "DoorBattleBarrier" ||
                door.gameObject.name == "DoorTransitionBarrier")
            {
                door.UnlockDoor();
            }
        }
    }

    public bool AreWavesComplete => wavesComplete;
    public int CurrentWave => currentWave;
}
