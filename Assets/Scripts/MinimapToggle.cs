using UnityEngine;

public class MinimapToggle : MonoBehaviour
{
    public GameObject minimapUI;
    public KeyCode toggleKey = KeyCode.M;
    private bool isVisible = true;

    void Update()
    {
        if (Input.GetKeyDown(GameKeybinds.Minimap))
        {
            ToggleMinimap();
        }
    }

    void ToggleMinimap()
    {
        isVisible = !isVisible;
        minimapUI.SetActive(isVisible);
    }
}