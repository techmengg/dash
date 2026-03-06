using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 3f;
    public float currentHealth = 3f;
    public Image healthBarFill;

    [Header("References")]
    public SpriteRenderer spriteRenderer;
    public PlayerMovement playerMovement;

    public bool isInvincible = false;
    private Transform currentTrap = null;
    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthUI();
    }

    public void BecomeInvincible()
    {
        isInvincible = true;
        spriteRenderer.color = Color.green;
    }

    public void RemoveInvincibility()
    {
        isInvincible = false;

        if (currentTrap != null && !isDead)
        {
            TakeDamage();
            Vector2 knockbackDirection = (transform.position - currentTrap.position).normalized;
            if (playerMovement != null)
            {
                playerMovement.ApplyKnockback(knockbackDirection);
            }
        }
        else
        {
            spriteRenderer.color = Color.white;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Trap"))
        {
            currentTrap = collision.transform;

            if (!isInvincible && !isDead) 
            {
                TakeDamage();

                Vector2 knockbackDirection = (transform.position - collision.transform.position).normalized;
                
                if (playerMovement != null)
                {
                    playerMovement.ApplyKnockback(knockbackDirection);
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Trap"))
        {
            if (currentTrap == collision.transform)
            {
                currentTrap = null;
            }

            if (!isInvincible && !isDead) 
            {
                spriteRenderer.color = Color.white;
            }
        }
    }

    private void TakeDamage()
    {
        if (isDead) return;

        spriteRenderer.color = Color.red;
        currentHealth -= 1f;
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            StartCoroutine(DieCoroutine());
        }
    }

    private void UpdateHealthUI()
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = currentHealth / maxHealth;
        }
    }

    private IEnumerator DieCoroutine()
    {
        isDead = true;
        spriteRenderer.color = Color.black;

        if (playerMovement != null) playerMovement.isStunned = true;

        yield return new WaitForSeconds(2f);

        currentHealth = maxHealth;
        UpdateHealthUI();
        spriteRenderer.color = Color.white;
        isDead = false;

        if (playerMovement != null) playerMovement.isStunned = false;
    }
}