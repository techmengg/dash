using UnityEngine;

public class ReinitializeBlitterExample : MonoBehaviour
{
    public void ReinitializeBlitter()
    {
        Debug.LogWarning(
            "ReinitializeBlitter is disabled. Calling Blitter.Cleanup/Initialize here can invalidate URP internal materials (for example Sprite-Lit-Default)."
        );
    }
}