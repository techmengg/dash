using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(EnemyHealth))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("General")]
    public float moveSpeed = 2f;
    public float detectionRange = 8f;
    public int contactDamage = 1;

    [Header("Trap Avoidance")]
    public float trapCheckRadius = 0.8f;
    public float trapAvoidWeight = 0.8f;
    public LayerMask trapLayer;

    protected Transform player;
    protected Rigidbody2D rb;
    protected SpriteRenderer sr;
    protected EnemyHealth health;
    protected PlayerHealth playerHealth;
    protected Vector2 spawnPoint;
    protected bool isStunned = false;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        health = GetComponent<EnemyHealth>();
        spawnPoint = transform.position;
    }

    protected virtual void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        if (health != null)
            health.SetSpawnPoint(spawnPoint);
    }

    protected Vector2 GetDirectionToPlayer()
    {
        if (player == null) return Vector2.zero;
        return ((Vector2)player.position - rb.position).normalized;
    }

    protected Vector2 GetTrapAvoidance()
    {
        Collider2D[] nearbyTraps = Physics2D.OverlapCircleAll(transform.position, trapCheckRadius, trapLayer);
        if (nearbyTraps.Length == 0) return Vector2.zero;

        Vector2 avoid = Vector2.zero;

        foreach (Collider2D trap in nearbyTraps)
        {
            Vector2 away = (Vector2)transform.position - (Vector2)trap.transform.position;
            float dist = away.magnitude;

            if (dist < 0.001f) continue;

            float weight = Mathf.Clamp01((trapCheckRadius - dist) / trapCheckRadius);
            avoid += away.normalized * weight;
        }

        return avoid.normalized * trapAvoidWeight;
    }

    protected void MoveWithAvoidance(Vector2 desiredDirection)
    {
        Vector2 finalDir = (desiredDirection + GetTrapAvoidance()).normalized;
        Vector2 targetPos = rb.position + finalDir * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPos);
    }

    protected bool IsPlayerDead()
    {
        return playerHealth != null && playerHealth.IsDead();
    }

    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        if (isStunned || IsPlayerDead()) return;
        if (!collision.gameObject.CompareTag("Player")) return;

        PlayerMovement playerMovement = collision.gameObject.GetComponent<PlayerMovement>();
        if (playerMovement != null && playerMovement.IsDashing) return;

        PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            Vector2 knockDir = (collision.transform.position - transform.position).normalized;
            playerHealth.TakeDamage(contactDamage, knockDir);
        }
    }
}
