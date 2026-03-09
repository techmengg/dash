using System.Collections;
using UnityEngine;

public class MeleeEnemy : EnemyBase
{
    [Header("Melee")]
    public float stopDistance = 0.2f;
    public float attackPauseDuration = 0.85f;
    private bool isPaused = false;

    public void Stun(float duration)
    {
        if (!isStunned)
            StartCoroutine(StunCoroutine(duration));
    }

    private IEnumerator StunCoroutine(float duration)
    {
        isStunned = true;

        // optional visual feedback
        sr.color = Color.yellow;

        yield return new WaitForSeconds(duration);

        isStunned = false;
        sr.color = Color.red;
    }

    protected override void Awake()
    {
        base.Awake();
        detectionRange = 12f;
        moveSpeed = 4f;
        sr.color = Color.red;
    }

    protected override void OnCollisionStay2D(Collision2D collision)
    {
        if (isPaused || isStunned || IsPlayerDead()) return;
        if (!collision.gameObject.CompareTag("Player")) return;

        PlayerMovement playerMovement = collision.gameObject.GetComponent<PlayerMovement>();
        if (playerMovement != null && playerMovement.IsDashing) return;

        PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            Vector2 knockDir = (collision.transform.position - transform.position).normalized;
            playerHealth.TakeDamage(contactDamage, knockDir);
            StartCoroutine(AttackPause());
        }
    }

    private IEnumerator AttackPause()
    {
        isPaused = true;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(attackPauseDuration);
        isPaused = false;
    }

    private void FixedUpdate()
    {
        if (isStunned || isPaused || IsPlayerDead()) return;
        if (player == null || health == null || health.IsDead) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist <= detectionRange && dist > stopDistance)
        {
            Vector2 dir = GetDirectionToPlayer();
            MoveWithAvoidance(dir);
        }
    }
}