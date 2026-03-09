using UnityEngine;

public class BossEnemy : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 20;
    public float moveSpeed = 1.5f;
    public int contactDamage = 2;

    [Header("Detection")]
    public float detectionRange = 15f;

    [Header("Projectile Attack")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 8f;
    public float shootCooldown = 2f;

    [Header("Dash Attack")]
    public float dashForce = 8f;
    public float dashCooldown = 4f;
    public float dashDuration = 0.35f;

    private Transform player;
    private PlayerHealth playerHealth;
    private Rigidbody2D rb;
    private int currentHealth;

    private float shootTimer;
    private float dashTimer;

    private bool isDashing;
    private float dashTimeLeft;
    private Vector2 dashDirection;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
        shootTimer = shootCooldown;
        dashTimer = dashCooldown;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }
    }

    private bool IsPlayerDead()
    {
        return playerHealth != null && playerHealth.IsDead();
    }

    private void Update()
    {
        if (player == null || IsPlayerDead()) return;

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance > detectionRange) return;

        shootTimer -= Time.deltaTime;
        dashTimer -= Time.deltaTime;

        if (shootTimer <= 0f)
        {
            ShootAtPlayer();
            shootTimer = shootCooldown;
        }

        if (dashTimer <= 0f && !isDashing)
        {
            StartDash();
            dashTimer = dashCooldown;
        }
    }

    private void FixedUpdate()
    {
        if (player == null || IsPlayerDead()) return;

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance > detectionRange) return;

        if (isDashing)
        {
            rb.linearVelocity = dashDirection * dashForce;
            dashTimeLeft -= Time.fixedDeltaTime;

            if (dashTimeLeft <= 0f)
            {
                isDashing = false;
                rb.linearVelocity = Vector2.zero;
            }

            return;
        }

        Vector2 direction = ((Vector2)player.position - rb.position).normalized;
        rb.MovePosition(rb.position + direction * moveSpeed * Time.fixedDeltaTime);
    }

    private void ShootAtPlayer()
    {
        if (projectilePrefab == null || firePoint == null || player == null) return;

        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        EnemyProjectile projScript = projectile.GetComponent<EnemyProjectile>();

        if (projScript != null)
        {
            Vector2 direction = ((Vector2)player.position - (Vector2)firePoint.position).normalized;
            projScript.Initialize(direction, projectileSpeed);
        }
    }

    private void StartDash()
    {
        if (player == null) return;

        dashDirection = ((Vector2)player.position - rb.position).normalized;
        isDashing = true;
        dashTimeLeft = dashDuration;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsPlayerDead()) return;
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(contactDamage);
            }
        }
    }
}