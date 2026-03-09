using UnityEngine;

public class RangedEnemy : EnemyBase
{
    [Header("Ranged")]
    public float preferredDistance = 10f;
    public float retreatDistance = 3.5f;
    public float shootCooldown = 1.25f;
    public float projectileSpeed = 10f;
    public GameObject projectilePrefab;
    public Transform firePoint;

    private float shootTimer;

    protected override void Awake()
    {
        base.Awake();
        detectionRange = 20f;
        sr.color = Color.cyan;
    }

    protected override void Start()
    {
        base.Start();
        shootTimer = shootCooldown;
    }

    private void Update()
    {
        if (player == null || health == null || health.IsDead || IsPlayerDead()) return;

        shootTimer -= Time.deltaTime;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist <= detectionRange && dist <= preferredDistance && shootTimer <= 0f)
        {
            Shoot();
            shootTimer = shootCooldown;
        }
    }

    private void FixedUpdate()
    {
        if (player == null || health == null || health.IsDead || IsPlayerDead()) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > detectionRange) return;

        Vector2 dirToPlayer = GetDirectionToPlayer();

        // Move closer if too far
        if (dist > preferredDistance)
        {
            MoveWithAvoidance(dirToPlayer);
        }
        // Back away if too close
        else if (dist < retreatDistance)
        {
            MoveWithAvoidance(-dirToPlayer);
        }
        // Stay still if in good shooting range
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void Shoot()
    {
        if (projectilePrefab == null || firePoint == null || player == null) return;

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        Vector2 dir = ((Vector2)player.position - (Vector2)firePoint.position).normalized;

        EnemyProjectile projectile = proj.GetComponent<EnemyProjectile>();
        if (projectile != null)
        {
            Collider2D myCollider = GetComponent<Collider2D>();
            projectile.Initialize(dir, projectileSpeed, myCollider);
        }
    }
}
