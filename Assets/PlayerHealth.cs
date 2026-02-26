using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    // This will hold a reference to the component that draws our player's color
    public SpriteRenderer spriteRenderer;

    // This built-in Unity function fires automatically when the player enters an "Is Trigger" collider
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the thing we just touched has the "Trap" tag
        if (collision.CompareTag("Trap"))
        {
            // Turn the player red!
            spriteRenderer.color = Color.red;
        }
    }

    // Bonus: This fires when the player leaves the trigger zone
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Trap"))
        {
            // Turn the player back to their normal color (white is the default tint)
            spriteRenderer.color = Color.white; 
        }
    }
}