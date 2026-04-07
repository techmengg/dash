using UnityEngine;

public class SpikeTrap : MonoBehaviour
{
    public int damage = 1;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        ApplyTrapDamage(collision, damageEnemies: true);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        ApplyTrapDamage(collision, damageEnemies: false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ApplyTrapDamage(collision.collider, damageEnemies: true);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        ApplyTrapDamage(collision.collider, damageEnemies: false);
    }

    private void ApplyTrapDamage(Collider2D collision, bool damageEnemies)
    {
        if (TryResolvePlayer(collision, out PlayerHealth playerHealth, out PlayerMovement playerMovement, out bool isFeetHitbox))
        {
            // If marker exists, only feet hitbox should trigger trap damage.
            if (HasPlayerHitboxMarker(collision) && !isFeetHitbox)
                return;

            // Let active dashes pass through traps without taking damage.
            if (playerMovement != null && playerMovement.IsDashing)
            {
                return;
            }

            if (playerHealth != null)
            {
                Vector2 knockbackDirection = ((Vector2)collision.transform.position - (Vector2)transform.position).normalized;
                if (knockbackDirection.sqrMagnitude < 0.0001f)
                {
                    knockbackDirection = Vector2.up;
                }

                playerHealth.TakeDamage(damage, knockbackDirection);
                return;
            }
        }

        if (collision.GetComponentInParent<BossEnemy>() != null)
        {
            return;
        }

        EnemyHealth enemyHealth = collision.GetComponent<EnemyHealth>();
        if (damageEnemies && enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
        }
    }

    private static bool HasPlayerHitboxMarker(Collider2D collision)
    {
        if (collision == null)
            return false;

        return collision.GetComponent<PlayerHitbox2D>() != null
            || collision.GetComponentInParent<PlayerHitbox2D>() != null;
    }

    private static bool TryResolvePlayer(
        Collider2D collision,
        out PlayerHealth playerHealth,
        out PlayerMovement playerMovement,
        out bool isFeetHitbox)
    {
        playerHealth = null;
        playerMovement = null;
        isFeetHitbox = false;

        if (collision == null)
            return false;

        PlayerHitbox2D hitbox = collision.GetComponent<PlayerHitbox2D>();
        if (hitbox == null)
            hitbox = collision.GetComponentInParent<PlayerHitbox2D>();

        if (hitbox != null)
        {
            playerHealth = hitbox.playerHealth != null ? hitbox.playerHealth : hitbox.GetComponentInParent<PlayerHealth>();
            playerMovement = hitbox.playerMovement != null ? hitbox.playerMovement : hitbox.GetComponentInParent<PlayerMovement>();
            isFeetHitbox = hitbox.IsFeetHitbox;
            return playerHealth != null;
        }

        playerHealth = collision.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = collision.GetComponentInParent<PlayerHealth>();

        playerMovement = collision.GetComponent<PlayerMovement>();
        if (playerMovement == null)
            playerMovement = collision.GetComponentInParent<PlayerMovement>();

        return playerHealth != null;
    }
}