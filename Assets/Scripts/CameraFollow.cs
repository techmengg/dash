using UnityEngine;

/// <summary>
/// Smooth camera follow that tracks the player between arenas.
/// Attach to the Main Camera or it auto-attaches at runtime.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    public float zOffset = -10f;

    [Header("Camera Zoom")]
    [Min(1f)] public float orthographicSize = 7f;

    [Header("Rect Bounds (optional)")]
    public Vector2 minBounds = Vector2.zero;
    public Vector2 maxBounds = Vector2.zero;

    [Header("Optional Bounds (0 = no limit)")]
    public float minY = -58f;
    public float maxY = 20f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (cam.GetComponent<CameraFollow>() != null) return;

        // Auto-attach in known gameplay scenes that rely on camera follow.
        bool hasBattleArena = FindFirstObjectByType<BattleArenaBuilder>() != null;
        bool hasDungeon = FindFirstObjectByType<DungeonGenerator>() != null;
        bool hasRoomController = FindFirstObjectByType<RoomController>() != null;
        bool hasPlayerController = FindFirstObjectByType<PlayerMovement>() != null;
        if (!hasBattleArena && !hasDungeon && !hasRoomController && !hasPlayerController) return;

        CameraFollow cf = cam.gameObject.AddComponent<CameraFollow>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            cf.target = playerObj.transform;
            return;
        }

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
            cf.target = player.transform;
    }

    private bool hasSnapped = false;
    private float nextTargetLookupTime;

    private void Awake()
    {
        ApplyZoom();
    }

    private void OnValidate()
    {
        orthographicSize = Mathf.Max(1f, orthographicSize);
        ApplyZoom();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            if (Time.time >= nextTargetLookupTime)
            {
                nextTargetLookupTime = Time.time + 0.5f;
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    target = playerObj.transform;
                }
                else
                {
                    PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
                    if (player != null)
                        target = player.transform;
                }
            }

            if (target == null) return;
        }

        // Snap to player on first frame (no slow pan from origin)
        if (!hasSnapped)
        {
            hasSnapped = true;
            transform.position = new Vector3(target.position.x, target.position.y, zOffset);
        }

        Vector3 desired = new Vector3(target.position.x, target.position.y, zOffset);

        ApplyBounds(ref desired);

        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Instantly snaps camera to the target position. Use after teleporting the player.
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        Vector3 pos = new Vector3(target.position.x, target.position.y, zOffset);
        ApplyBounds(ref pos);
        transform.position = pos;
    }

    private void ApplyBounds(ref Vector3 position)
    {
        bool hasRectBounds = minBounds != Vector2.zero || maxBounds != Vector2.zero;
        if (hasRectBounds)
        {
            float minX = Mathf.Min(minBounds.x, maxBounds.x);
            float maxX = Mathf.Max(minBounds.x, maxBounds.x);
            float minBoundY = Mathf.Min(minBounds.y, maxBounds.y);
            float maxBoundY = Mathf.Max(minBounds.y, maxBounds.y);

            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.y = Mathf.Clamp(position.y, minBoundY, maxBoundY);
            return;
        }

        if (minY != 0 || maxY != 0)
            position.y = Mathf.Clamp(position.y, minY, maxY);
    }

    private void ApplyZoom()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic)
            return;

        cam.orthographicSize = orthographicSize;
    }
}
