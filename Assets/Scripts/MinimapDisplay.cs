using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapDisplay : MonoBehaviour
{
    public GameObject iconPrefab;
    public RectTransform playerIconTransform;
    public int gridSize = 11;

    [Header("Grid Layout")]
    public bool autoFitGridToContainer = true;
    [Range(0.5f, 1f)] public float playerIconCellRatio = 0.85f;

    // --- ADD THESE SPRITE SLOTS ---
    [Header("Custom Room Icons")]
    public Sprite startRoomSprite;
    public Sprite bossRoomSprite;
    public Sprite normalRoomSprite;
    public Sprite playerSprite; // Optional: if you have a custom player head icon

    private Dictionary<Vector2Int, Image> iconGrid = new Dictionary<Vector2Int, Image>();
    private GridLayoutGroup gridLayout;
    private RectTransform rectTransform;
    private Vector2Int activeGridOffset = Vector2Int.zero;
    public static MinimapDisplay instance;

    private GameObject CreateFallbackIcon()
    {
        GameObject icon = new GameObject("MinimapIcon");
        RectTransform rt = icon.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(14f, 14f);
        Image img = icon.AddComponent<Image>();
        img.color = Color.clear;
        return icon;
    }

    void Awake()
    {
        instance = this;
        CacheLayoutRefs();
        UpdateGridLayoutCellSize();
        BuildGridIcons();
        EnsurePlayerIconExists();
    }

    public void DrawMap(Dictionary<Vector2Int, RoomData> rooms)
    {
        // 1. Clear everything first
        foreach (var icon in iconGrid.Values) 
        {
            icon.color = Color.clear;
            icon.sprite = null;
        }

        foreach (var room in rooms.Values)
        {
            if (!iconGrid.ContainsKey(room.gridPos)) continue;

            bool shouldShow = false;

            // Check if WE have been in this room
            if (room.isVisited) 
            {
                shouldShow = true;
            }
            else 
            {
                // Check if any NEIGHBOR has been visited
                Vector2Int[] neighbors = {
                    room.gridPos + Vector2Int.up,
                    room.gridPos + Vector2Int.down,
                    room.gridPos + Vector2Int.left,
                    room.gridPos + Vector2Int.right
                };

                foreach (var nPos in neighbors)
                {
                    if (rooms.ContainsKey(nPos) && rooms[nPos].isVisited)
                    {
                        shouldShow = true;
                        break;
                    }
                }
            }

            // 2. Only draw if it's "discovered"
            if (shouldShow)
            {
                Image img = iconGrid[room.gridPos];
                img.sprite = GetSpriteForRoom(room.type);
                
                // OPTIONAL: Make unvisited neighbors slightly darker/transparent
                if (!room.isVisited)
                    img.color = new Color(1f, 1f, 1f, 0.3f); 
                else
                    img.color = Color.white; 
            }
        }
    }

    Sprite GetSpriteForRoom(RoomType type)
    {
        switch (type)
        {
            case RoomType.Start:
                return startRoomSprite;
            case RoomType.Boss:
                return bossRoomSprite;
            default:
                return normalRoomSprite; // Everything else uses the normal icon
        }
    }

    private void EnsurePlayerIconExists()
    {
        if (playerIconTransform == null)
        {
            GameObject playerIcon = new GameObject("MinimapPlayerIcon");
            playerIcon.transform.SetParent(transform, false);
            RectTransform rt = playerIcon.AddComponent<RectTransform>();

            Image img = playerIcon.AddComponent<Image>();

            // Use your custom player sprite if assigned, otherwise fallback to white square
            if (playerSprite != null) img.sprite = playerSprite;
            img.color = Color.white;

            playerIconTransform = rt;
        }

        float iconSize = GetPlayerIconSize();
        playerIconTransform.sizeDelta = new Vector2(iconSize, iconSize);
    }

    public void UpdatePlayerLocation(Vector2Int newPos)
    {
        Vector2Int mappedPos = newPos + activeGridOffset;

        if (playerIconTransform != null && iconGrid.ContainsKey(mappedPos))
        {
            // 1. Change the parent
            playerIconTransform.SetParent(iconGrid[mappedPos].rectTransform);

            // 2. Clear all position data instantly
            playerIconTransform.localPosition = Vector3.zero;
            playerIconTransform.anchoredPosition = Vector2.zero;

            // 3. Force the anchors and pivot to the dead center
            playerIconTransform.anchorMin = new Vector2(0.5f, 0.5f);
            playerIconTransform.anchorMax = new Vector2(0.5f, 0.5f);
            playerIconTransform.pivot = new Vector2(0.5f, 0.5f);

            // 4. Ensure it stays at scale 1 and on top
            playerIconTransform.localScale = Vector3.one;
            playerIconTransform.SetAsLastSibling();
        }
    }

    Color GetColorForRoom(RoomType type)
    {
        switch (type)
        {
            case RoomType.Start: return Color.blue;
            case RoomType.Boss: return Color.red;
            case RoomType.Treasure: return new Color(1f, 0.84f, 0f);
            default: return new Color(0.3f, 0.3f, 0.3f);
        }
    }

    // Inside MinimapDisplay.cs
    public void InitializeGrid()
    {
        CacheLayoutRefs();
        UpdateGridLayoutCellSize();

        int safeGridSize = Mathf.Max(1, gridSize);
        int expectedCellCount = safeGridSize * safeGridSize;
        if (iconGrid.Count != expectedCellCount)
        {
            BuildGridIcons();
        }

        EnsurePlayerIconExists();
    }

    private void CacheLayoutRefs()
    {
        if (gridLayout == null)
            gridLayout = GetComponent<GridLayoutGroup>();

        if (rectTransform == null)
            rectTransform = transform as RectTransform;
    }

    private void UpdateGridLayoutCellSize()
    {
        if (!autoFitGridToContainer || gridLayout == null || rectTransform == null)
            return;

        int safeGridSize = Mathf.Max(1, gridSize);

        float availableWidth = rectTransform.rect.width
            - gridLayout.padding.left
            - gridLayout.padding.right
            - gridLayout.spacing.x * (safeGridSize - 1);

        float availableHeight = rectTransform.rect.height
            - gridLayout.padding.top
            - gridLayout.padding.bottom
            - gridLayout.spacing.y * (safeGridSize - 1);

        float cellSize = Mathf.Floor(Mathf.Min(availableWidth, availableHeight) / safeGridSize);
        cellSize = Mathf.Max(1f, cellSize);

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = safeGridSize;
        gridLayout.cellSize = new Vector2(cellSize, cellSize);
    }

    private void BuildGridIcons()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        iconGrid.Clear();

        Vector2Int minCoord;
        Vector2Int maxCoord;
        GetGridBounds(out minCoord, out maxCoord);

        for (int y = maxCoord.y; y >= minCoord.y; y--)
        {
            for (int x = minCoord.x; x <= maxCoord.x; x++)
            {
                GameObject icon = iconPrefab != null
                    ? Instantiate(iconPrefab, transform)
                    : CreateFallbackIcon();
                if (icon.transform.parent != transform)
                    icon.transform.SetParent(transform, false);

                icon.name = $"MinimapRoom_{x}_{y}";

                Image img = icon.GetComponent<Image>();
                if (img == null)
                    img = icon.AddComponent<Image>();
                img.color = Color.clear;
                iconGrid.Add(new Vector2Int(x, y), img);
            }
        }
    }

    private void GetGridBounds(out Vector2Int minCoord, out Vector2Int maxCoord)
    {
        int safeGridSize = Mathf.Max(1, gridSize);
        int half = safeGridSize / 2;

        // Works for odd and even sizes (e.g. 11 => -5..5, 10 => -5..4)
        minCoord = new Vector2Int(-half, -half);
        maxCoord = new Vector2Int(minCoord.x + safeGridSize - 1, minCoord.y + safeGridSize - 1);
    }

    private Vector2Int ComputeBestGridOffset(Dictionary<Vector2Int, RoomData> rooms)
    {
        if (rooms == null || rooms.Count == 0)
            return Vector2Int.zero;

        Vector2Int minRoom = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int maxRoom = new Vector2Int(int.MinValue, int.MinValue);
        Vector2Int spawnRoom = Vector2Int.zero;
        bool foundSpawn = false;

        foreach (var room in rooms.Values)
        {
            Vector2Int p = room.gridPos;
            if (p.x < minRoom.x) minRoom.x = p.x;
            if (p.y < minRoom.y) minRoom.y = p.y;
            if (p.x > maxRoom.x) maxRoom.x = p.x;
            if (p.y > maxRoom.y) maxRoom.y = p.y;

            if (!foundSpawn && room.type == RoomType.Start)
            {
                spawnRoom = p;
                foundSpawn = true;
            }
        }

        Vector2Int minCoord;
        Vector2Int maxCoord;
        GetGridBounds(out minCoord, out maxCoord);

        int minOffsetX = minCoord.x - minRoom.x;
        int maxOffsetX = maxCoord.x - maxRoom.x;
        int minOffsetY = minCoord.y - minRoom.y;
        int maxOffsetY = maxCoord.y - maxRoom.y;

        // Priority: center spawn room when possible.
        int preferredOffsetX = -spawnRoom.x;
        int preferredOffsetY = -spawnRoom.y;

        return new Vector2Int(
            Mathf.Clamp(preferredOffsetX, minOffsetX, maxOffsetX),
            Mathf.Clamp(preferredOffsetY, minOffsetY, maxOffsetY)
        );
    }

    private float GetPlayerIconSize()
    {
        float baseSize = 12f;
        if (gridLayout != null)
            baseSize = Mathf.Min(gridLayout.cellSize.x, gridLayout.cellSize.y);

        return Mathf.Max(4f, baseSize * Mathf.Clamp(playerIconCellRatio, 0.5f, 1f));
    }
}