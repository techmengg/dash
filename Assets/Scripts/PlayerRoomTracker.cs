using UnityEngine;
using System.Collections.Generic;

public class PlayerRoomTracker : MonoBehaviour
{
    public MinimapDisplay minimap;
    public float roomSize = 20f;
    private Vector2Int currentGridPos;

    void Update()
    {
        // Calculate current grid position based on world position
        Vector2Int newGridPos = new Vector2Int(
            Mathf.RoundToInt(transform.position.x / roomSize),
            Mathf.RoundToInt(transform.position.y / roomSize)
        );

        if (newGridPos != currentGridPos)
        {
            currentGridPos = newGridPos;
            minimap.UpdatePlayerLocation(currentGridPos);
        }
    }
}