using UnityEngine;

public static class GameKeybinds
{
    public static readonly KeyCode FixedPause = KeyCode.Escape;
    public static KeyCode Pause = KeyCode.Escape;
    public static KeyCode Dash = KeyCode.Space;
    public static KeyCode Ability = KeyCode.F;
    public static KeyCode Super = KeyCode.LeftShift;
    public static KeyCode Interact = KeyCode.E;
    public static KeyCode Stats = KeyCode.Tab;
    public static KeyCode Minimap = KeyCode.M;

    private static bool initialized;

    public static void EnsureInitialized(KeyCode pauseFallback)
    {
        if (initialized)
            return;

        Pause = FixedPause;

        PlayerAbilitySlots abilitySlots = Object.FindFirstObjectByType<PlayerAbilitySlots>();
        if (abilitySlots != null)
        {
            Ability = abilitySlots.dashEnhancerActivationKey;
            Super = abilitySlots.superActivationKey;
        }

        DoorInteract door = Object.FindFirstObjectByType<DoorInteract>();
        if (door != null)
            Interact = door.interactKey;

        PlayerStatsAbilityPopup statsPopup = Object.FindFirstObjectByType<PlayerStatsAbilityPopup>();
        if (statsPopup != null)
            Stats = statsPopup.holdPopupKey;

        MinimapToggle minimapToggle = Object.FindFirstObjectByType<MinimapToggle>();
        if (minimapToggle != null)
            Minimap = minimapToggle.toggleKey;

        initialized = true;
        ApplyToLiveObjects();
    }

    public static void ApplyToLiveObjects()
    {
        Pause = FixedPause;

        PauseMenuUI pauseMenu = Object.FindFirstObjectByType<PauseMenuUI>();
        if (pauseMenu != null)
            pauseMenu.pauseKey = Pause;

        PlayerAbilitySlots abilitySlots = Object.FindFirstObjectByType<PlayerAbilitySlots>();
        if (abilitySlots != null)
        {
            abilitySlots.dashEnhancerActivationKey = Ability;
            abilitySlots.superActivationKey = Super;
        }

        PlayerStatsAbilityPopup statsPopup = Object.FindFirstObjectByType<PlayerStatsAbilityPopup>();
        if (statsPopup != null)
            statsPopup.holdPopupKey = Stats;

        MinimapToggle minimapToggle = Object.FindFirstObjectByType<MinimapToggle>();
        if (minimapToggle != null)
            minimapToggle.toggleKey = Minimap;

        DoorInteract[] doors = Object.FindObjectsByType<DoorInteract>(FindObjectsSortMode.None);
        foreach (DoorInteract d in doors)
        {
            if (d != null)
                d.interactKey = Interact;
        }
    }

    public static string ToDisplayString(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftShift:
            case KeyCode.RightShift:
                return "Shift";
            case KeyCode.LeftControl:
            case KeyCode.RightControl:
                return "Ctrl";
            case KeyCode.Escape:
                return "Esc";
            case KeyCode.Space:
                return "Space";
            case KeyCode.Mouse0:
                return "Mouse1";
            case KeyCode.Mouse1:
                return "Mouse2";
            case KeyCode.Mouse2:
                return "Mouse3";
            default:
                return key.ToString();
        }
    }
}
