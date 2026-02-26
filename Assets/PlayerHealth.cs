using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    
    // Tracks if the player is currently invincible
    public bool isInvincible = false; 
    
    // Tracks if the player is physically overlapping a trap zone
    private bool isTouchingTrap = false;

    // Called by the movement script when a dash starts
    public void BecomeInvincible()
    {
        isInvincible = true;
        spriteRenderer.color = Color.green; // Visual proof of i-frames!
    }

    // Called by the movement script when a dash ends
    public void RemoveInvincibility()
    {
        isInvincible = false;
        
        // If the dash ends and we are STILL standing on a trap, take damage immediately
        if (isTouchingTrap)
        {
            spriteRenderer.color = Color.red;
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
            isTouchingTrap = true;
            
            // Only turn red (take damage) if we are NOT invincible
            if (!isInvincible) 
            {
                spriteRenderer.color = Color.red;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Trap"))
        {
            isTouchingTrap = false;
            
            // Only revert to white if we aren't currently glowing green from a dash
            if (!isInvincible) 
            {
                spriteRenderer.color = Color.white;
            }
        }
    }
}