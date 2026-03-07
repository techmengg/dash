using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Stats")]
    public float maxHealth = 5f;
    public float respawnTime = 3f;

    [Header("Knockback & Stun")]
    public float knockbackDuration = 0.15f;
    public float stunDuration = 1f;

    private float currentHealth;
    private bool isDead = false;
    
    private SpriteRenderer spriteRenderer;
    private Collider2D dummyCollider;
    private Rigidbody2D rb;
    
    private Color originalColor;
    private Vector3 originalPosition;

    void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        dummyCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        
        originalColor = spriteRenderer.color; 
        originalPosition = transform.position;
    }

    public void TakeDamage(float damageAmount, Vector2 hitDirection, float hitForce)
    {
        if (isDead) return;

        currentHealth -= damageAmount;
        StartCoroutine(FlashRed());

        if (currentHealth <= 0)
        {
            isDead = true;
            StartCoroutine(RespawnRoutine());
        }
        else
        {

            StartCoroutine(KnockbackRoutine(hitDirection, hitForce));
        }
    }

    private IEnumerator FlashRed()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (!isDead) spriteRenderer.color = originalColor;
    }

    private IEnumerator KnockbackRoutine(Vector2 knockbackDirection, float force)
    {
        float angle = Mathf.Atan2(knockbackDirection.y, knockbackDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        float startTime = Time.time;

        while (Time.time < startTime + knockbackDuration)
        {
            rb.MovePosition(rb.position + knockbackDirection * force * Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        yield return new WaitForSeconds(stunDuration - knockbackDuration);

        transform.rotation = Quaternion.identity;
    }

    private IEnumerator RespawnRoutine()
    {
        spriteRenderer.enabled = false;
        dummyCollider.enabled = false;

        yield return new WaitForSeconds(respawnTime);

        currentHealth = maxHealth;
        isDead = false;
        transform.position = originalPosition; 
        transform.rotation = Quaternion.identity;
        spriteRenderer.color = originalColor; 
        
        spriteRenderer.enabled = true;
        dummyCollider.enabled = true;
    }
}