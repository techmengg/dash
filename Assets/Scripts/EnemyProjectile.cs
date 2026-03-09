using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public int damage = 1;
    public float lifeTime = 3f;

    private Vector2 direction;
    private float speed;
    private Collider2D ownerCollider;

    public void Initialize(Vector2 dir, float moveSpeed, Collider2D owner = null)
    {
        direction = dir.normalized;
        speed = moveSpeed;
        ownerCollider = owner;

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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Skip the enemy that shot this
        if (ownerCollider != null && collision == ownerCollider) return;
        // Skip other enemies
        if (collision.CompareTag("Enemy")) return;

        if (collision.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.TakeDamage(damage);

            Destroy(gameObject);
            return;
        }

        if (collision.CompareTag("Trap"))
        {
            Destroy(gameObject);
            return;
        }

        if (!collision.isTrigger)
            Destroy(gameObject);
    }
}
