using UnityEngine;

public enum PlayerHitboxType
{
    Feet,
    Hurtbox
}

/// <summary>
/// Attach this to player child colliders so gameplay code can distinguish
/// feet-only world collision from full-body damage hitboxes.
/// </summary>
public class PlayerHitbox2D : MonoBehaviour
{
    public PlayerHitboxType hitboxType = PlayerHitboxType.Hurtbox;

    [Header("Auto-Resolved References")]
    public PlayerHealth playerHealth;
    public PlayerMovement playerMovement;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();

        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();
    }

    public bool IsFeetHitbox => hitboxType == PlayerHitboxType.Feet;
    public bool IsHurtbox => hitboxType == PlayerHitboxType.Hurtbox;
}
