using UnityEngine;

/// <summary>
/// Holds one-shot transition data from Rooms -> Boss Arena.
/// Data is consumed once on Boss Arena scene load.
/// </summary>
public static class BossArenaTransitionState
{
    public struct TransitionData
    {
        public Vector2Int moveDirection;
        public float playerXBeforeTransition;
    }

    private static bool hasPendingTransition;
    private static TransitionData pendingTransition;

    public static void QueueEntry(Vector2Int moveDirection, float playerXBeforeTransition)
    {
        if (moveDirection == Vector2Int.zero)
            moveDirection = Vector2Int.up;

        pendingTransition = new TransitionData
        {
            moveDirection = moveDirection,
            playerXBeforeTransition = playerXBeforeTransition
        };

        hasPendingTransition = true;
    }

    public static bool TryConsumeEntry(out TransitionData data)
    {
        if (!hasPendingTransition)
        {
            data = default;
            return false;
        }

        data = pendingTransition;
        pendingTransition = default;
        hasPendingTransition = false;
        return true;
    }

    public static void Clear()
    {
        pendingTransition = default;
        hasPendingTransition = false;
    }
}
