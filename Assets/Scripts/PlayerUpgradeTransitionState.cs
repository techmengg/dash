using System.Collections.Generic;

/// <summary>
/// One-shot handoff for player card upgrades from Rooms to Boss Arena.
/// Captured before scene load and consumed on Boss Arena bootstrap.
/// </summary>
public static class PlayerUpgradeTransitionState
{
    private static bool hasPendingSnapshot;
    private static List<PlayerUpgradeDeck.UpgradeStackSnapshot> pendingSnapshot;

    public static void QueueFromDeck(PlayerUpgradeDeck deck)
    {
        if (deck == null)
        {
            Clear();
            return;
        }

        List<PlayerUpgradeDeck.UpgradeStackSnapshot> snapshot = deck.CreateRuntimeSnapshot();
        if (snapshot == null || snapshot.Count == 0)
        {
            Clear();
            return;
        }

        pendingSnapshot = CloneSnapshot(snapshot);
        hasPendingSnapshot = pendingSnapshot.Count > 0;
    }

    public static bool TryConsume(out List<PlayerUpgradeDeck.UpgradeStackSnapshot> snapshot)
    {
        if (!hasPendingSnapshot || pendingSnapshot == null || pendingSnapshot.Count == 0)
        {
            snapshot = null;
            return false;
        }

        snapshot = CloneSnapshot(pendingSnapshot);
        Clear();
        return true;
    }

    public static void Clear()
    {
        pendingSnapshot = null;
        hasPendingSnapshot = false;
    }

    private static List<PlayerUpgradeDeck.UpgradeStackSnapshot> CloneSnapshot(List<PlayerUpgradeDeck.UpgradeStackSnapshot> source)
    {
        List<PlayerUpgradeDeck.UpgradeStackSnapshot> clone = new List<PlayerUpgradeDeck.UpgradeStackSnapshot>();
        if (source == null)
            return clone;

        for (int i = 0; i < source.Count; i++)
        {
            PlayerUpgradeDeck.UpgradeStackSnapshot entry = source[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.upgradeId) || entry.stackCount <= 0)
                continue;

            clone.Add(new PlayerUpgradeDeck.UpgradeStackSnapshot
            {
                upgradeId = entry.upgradeId,
                stackCount = entry.stackCount
            });
        }

        return clone;
    }
}
