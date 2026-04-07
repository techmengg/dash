using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public int damage = 1;
    public float lifeTime = 3f;
    [Header("Visual")]
    public int projectileSortingOrder = 15;

    private Vector2 direction;
    private float speed;
    private Collider2D ownerCollider;

    private void EnsureVisibleRenderer()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            return;

        sr.enabled = true;

        Color c = sr.color;
        if (c.a <= 0f)
            c.a = 1f;
        sr.color = c;

        int targetOrder = projectileSortingOrder;
        if (ownerCollider != null)
        {
            SpriteRenderer ownerSr = ownerCollider.GetComponent<SpriteRenderer>();
            if (ownerSr == null)
                ownerSr = ownerCollider.GetComponentInParent<SpriteRenderer>();

            if (ownerSr != null)
            {
                sr.sortingLayerID = ownerSr.sortingLayerID;
                targetOrder = Mathf.Max(targetOrder, ownerSr.sortingOrder + 1);
            }
        }

        sr.sortingOrder = Mathf.Max(sr.sortingOrder, targetOrder);
    }

    public void Initialize(Vector2 dir, float moveSpeed, Collider2D owner = null)
    {
        direction = dir.normalized;
        speed = moveSpeed;
        ownerCollider = owner;

        EnsureVisibleRenderer();

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Ignore collision with the enemy that fired this
        if (ownerCollider != null)
        {
            Collider2D projCol = GetComponent<Collider2D>();
            if (projCol != null)
                Physics2D.IgnoreCollision(projCol, ownerCollider);
        }

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    private void PlayImpactSfx()
    {
        EnemyAudio enemyAudio = null;

        if (ownerCollider != null)
        {
            enemyAudio = ownerCollider.GetComponent<EnemyAudio>();

            if (enemyAudio == null && ownerCollider.attachedRigidbody != null)
                enemyAudio = ownerCollider.attachedRigidbody.GetComponent<EnemyAudio>();

            if (enemyAudio == null)
                enemyAudio = ownerCollider.GetComponentInParent<EnemyAudio>();
        }

        if (enemyAudio != null)
            enemyAudio.PlayProjectileImpactSfx();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Skip the enemy that shot this
        if (ownerCollider != null && collision == ownerCollider) return;
        // Skip other enemies
        if (collision.CompareTag("Enemy")) return;

        if (TryResolvePlayer(collision, out PlayerHealth playerHealth, out bool isFeetHitbox))
        {
            // Ignore feet hitbox when marker exists so body hurtbox drives enemy damage.
            if (isFeetHitbox)
                return;

            playerHealth.TakeDamage(damage);

            PlayImpactSfx();
            Destroy(gameObject);
            return;
        }

        if (collision.CompareTag("Trap"))
        {
            PlayImpactSfx();
            Destroy(gameObject);
            return;
        }

        if (!collision.isTrigger)
        {
            PlayImpactSfx();
            Destroy(gameObject);
        }
    }

    private bool TryResolvePlayer(Collider2D collision, out PlayerHealth playerHealth, out bool isFeetHitbox)
    {
        playerHealth = null;
        isFeetHitbox = false;

        if (collision == null)
            return false;

        PlayerHitbox2D hitbox = collision.GetComponent<PlayerHitbox2D>();
        if (hitbox == null)
            hitbox = collision.GetComponentInParent<PlayerHitbox2D>();

        if (hitbox != null)
        {
            playerHealth = hitbox.playerHealth != null ? hitbox.playerHealth : hitbox.GetComponentInParent<PlayerHealth>();
            isFeetHitbox = hitbox.IsFeetHitbox;
            return playerHealth != null;
        }

        playerHealth = collision.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = collision.GetComponentInParent<PlayerHealth>();

        return playerHealth != null;
    }
}
