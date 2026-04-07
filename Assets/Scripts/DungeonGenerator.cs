using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    public int[] roomsPerLevel = { 7, 10, 15 };
    public int currentLevel = 0;
    public MinimapDisplay minimap;

    [Header("Minimap Scaling")]
    public bool scaleDungeonWithMinimapSize = true;
    [Min(1)] public int referenceMinimapGridSize = 11;

    [Header("Random Generation")]
    public bool randomizeSeedOnGenerate = true;
    public int fixedSeed = 12345;
    [SerializeField] private int lastUsedSeed;

    private Dictionary<Vector2Int, RoomData> dungeonRooms = new Dictionary<Vector2Int, RoomData>();

    private MinimapDisplay EnsureMinimap()
    {
        if (minimap != null)
            return minimap;

        minimap = Object.FindFirstObjectByType<MinimapDisplay>();
        if (minimap != null)
            return minimap;

        GameObject canvasObj = new GameObject("RuntimeMinimapCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject minimapObj = new GameObject("RuntimeMinimap");
        minimapObj.transform.SetParent(canvasObj.transform, false);
        RectTransform rt = minimapObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-95f, 95f);
        rt.sizeDelta = new Vector2(170f, 170f);

        UnityEngine.UI.Image bg = minimapObj.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.35f);

        UnityEngine.UI.GridLayoutGroup grid = minimapObj.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 11;
        grid.cellSize = new Vector2(14f, 14f);
        grid.spacing = Vector2.zero;

        minimap = minimapObj.AddComponent<MinimapDisplay>();
        minimap.gridSize = 11;
        minimap.iconPrefab = null;
        minimap.playerIconTransform = null;
        return minimap;
    }

    // Generate in Awake so RoomController.Start can immediately read (0,0) room data.
    void Awake() => GenerateDungeon();

    public void GenerateDungeon()
    {
        ReseedRandomGenerator();

        minimap = EnsureMinimap();

        if (minimap != null)
            minimap.InitializeGrid();

        dungeonRooms.Clear();

        int targetCount = 7;
        if (roomsPerLevel != null && roomsPerLevel.Length > 0)
            targetCount = roomsPerLevel[Mathf.Min(currentLevel, roomsPerLevel.Length - 1)];

        targetCount = GetScaledTargetCount(targetCount);

        targetCount = Mathf.Max(1, targetCount);
        Queue<Vector2Int> spawnQueue = new Queue<Vector2Int>();
        Vector2Int startPos = Vector2Int.zero;

        AddRoom(startPos, RoomType.Start, spawnQueue);

        if (!dungeonRooms.ContainsKey(startPos))
            dungeonRooms[startPos] = new RoomData(startPos, RoomType.Start);

        while (dungeonRooms.Count < targetCount && spawnQueue.Count > 0)
        {
            Vector2Int currentPos = spawnQueue.Dequeue();

            foreach (Vector2Int neighbor in GetRandomNeighbors(currentPos))
            {
                if (dungeonRooms.Count < targetCount && !dungeonRooms.ContainsKey(neighbor))
                {
                    if (!IsWithinMinimapBounds(neighbor))
                        continue;

                    // The "Isaac" Randomness: 50% chance to skip growing here
                    if (Random.value < 0.5f && dungeonRooms.Count > 1) continue;

                    if (CountNeighbors(neighbor) == 1)
                    {
                        AddRoom(neighbor, RoomType.Normal, spawnQueue);
                    }
                }
            }

            // Safety: If we run out of paths but need more rooms, try growing from existing ones
            if (spawnQueue.Count == 0 && dungeonRooms.Count < targetCount)
            {
                foreach (var pos in dungeonRooms.Keys) spawnQueue.Enqueue(pos);
            }
        }

        PlaceSpecialRoom(RoomType.Boss);

        // Hard guarantee: there is always a valid start room.
        if (!dungeonRooms.ContainsKey(Vector2Int.zero))
            dungeonRooms[Vector2Int.zero] = new RoomData(Vector2Int.zero, RoomType.Start);

        // Tell the minimap to draw and highlight the start (if present)
        if (minimap != null)
        {
            minimap.DrawMap(dungeonRooms);
            minimap.UpdatePlayerLocation(Vector2Int.zero);
        }

        RoomController rc = Object.FindFirstObjectByType<RoomController>();
        if (rc != null)
        {
            rc.InitializeFirstRoom();
        }
    }

    private void ReseedRandomGenerator()
    {
        if (randomizeSeedOnGenerate)
        {
            unchecked
            {
                lastUsedSeed = System.Guid.NewGuid().GetHashCode()
                    ^ (int)System.DateTime.UtcNow.Ticks
                    ^ Time.frameCount;
            }
        }
        else
        {
            lastUsedSeed = fixedSeed;
        }

        Random.InitState(lastUsedSeed);
    }

    private int GetScaledTargetCount(int baseTargetCount)
    {
        if (!scaleDungeonWithMinimapSize || minimap == null)
            return baseTargetCount;

        int safeReference = Mathf.Max(1, referenceMinimapGridSize);
        int safeGridSize = Mathf.Max(1, minimap.gridSize);

        float linearScale = safeGridSize / (float)safeReference;
        float areaScale = linearScale * linearScale;
        int scaledTarget = Mathf.RoundToInt(baseTargetCount * areaScale);

        int maxVisibleCells = safeGridSize * safeGridSize;
        return Mathf.Clamp(scaledTarget, 1, maxVisibleCells);
    }

    private bool IsWithinMinimapBounds(Vector2Int gridPos)
    {
        if (minimap == null)
            return true;

        int half = Mathf.Max(0, minimap.gridSize / 2);
        return gridPos.x >= -half && gridPos.x <= half && gridPos.y >= -half && gridPos.y <= half;
    }

    void AddRoom(Vector2Int pos, RoomType type, Queue<Vector2Int> queue)
    {
        RoomData newRoom = new RoomData(pos, type);

        if (type == RoomType.Start)
        {
            newRoom.isVisited = true; // Make sure James can see where he starts!
        }

        if (!dungeonRooms.ContainsKey(pos))
        {
            dungeonRooms.Add(pos, newRoom);
            if (queue != null) queue.Enqueue(pos);
        }

        // This is the line that actually "creates" the data for (0,0)
        if (!dungeonRooms.ContainsKey(pos))
        {
            dungeonRooms.Add(pos, newRoom);
            if (queue != null) queue.Enqueue(pos);
        }
    }

    void PlaceSpecialRoom(RoomType type)
    {
        Vector2Int furthestPos = Vector2Int.zero;
        float maxDist = -1f;

        foreach (var room in dungeonRooms.Values)
        {
            // Calculate distance from start (0,0)
            float dist = Vector2Int.Distance(Vector2Int.zero, room.gridPos);

            // ONLY pick this room if:
            // 1. It is further than the current max
            // 2. It is a Normal room (don't overwrite the Start room)
            // 3. It only has ONE neighbor (it's a dead end)
            if (dist > maxDist && room.type == RoomType.Normal && CountNeighbors(room.gridPos) == 1)
            {
                maxDist = dist;
                furthestPos = room.gridPos;
            }
        }

        // Safety check: If for some reason no dead end was found,
        // it will fall back to the last furthest room found.
        if (furthestPos != Vector2Int.zero)
        {
            dungeonRooms[furthestPos].type = type;
        }
    }

    int CountNeighbors(Vector2Int pos)
    {
        int count = 0;
        if (dungeonRooms.ContainsKey(pos + Vector2Int.up)) count++;
        if (dungeonRooms.ContainsKey(pos + Vector2Int.down)) count++;
        if (dungeonRooms.ContainsKey(pos + Vector2Int.left)) count++;
        if (dungeonRooms.ContainsKey(pos + Vector2Int.right)) count++;
        return count;
    }

    Vector2Int[] GetRandomNeighbors(Vector2Int pos)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        for (int i = 0; i < directions.Length; i++)
        {
            int rnd = Random.Range(i, directions.Length);
            Vector2Int temp = directions[rnd];
            directions[rnd] = directions[i];
            directions[i] = temp;
        }
        for (int i = 0; i < directions.Length; i++) directions[i] += pos;
        return directions;
    }

    public Dictionary<Vector2Int, RoomData> GetRoomData() { return dungeonRooms; }
}

