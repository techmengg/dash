using UnityEngine;

[System.Serializable] // This lets you see the data in the Inspector
public class RoomData
{
    public Vector2Int gridPos;
    public RoomType type;

    public int layoutIndex = -1; // -1 means "no layout assigned yet"
    public bool isVisited = false; 

    // Make sure your constructor matches this:
    public RoomData(Vector2Int pos, RoomType t)
    {
        gridPos = pos;
        type = t;
        layoutIndex = -1;
    }
}

// Ensure your RoomType enum includes the types you're using
public enum RoomType { Start, Normal, Boss, Treasure }