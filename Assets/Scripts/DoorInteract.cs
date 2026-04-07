using UnityEngine;
using TMPro;

public class DoorInteract : MonoBehaviour
{
    [Header("Settings")]
    public Vector2Int moveDirection; // Set these in Inspector: N(0,1), S(0,-1), E(1,0), W(-1,0)
    public KeyCode interactKey = KeyCode.E;

    [Header("Prompt UI")]
    public Vector3 promptOffset = new Vector3(0, 0.5f, 0);

    private RoomController roomController;
    private bool playerInRange = false;
    private GameObject promptUI;
    private TextMeshPro promptText;

    private static bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        if (other.GetComponent<PlayerHitbox2D>() != null || other.GetComponentInParent<PlayerHitbox2D>() != null)
            return true;

        return other.GetComponent<PlayerMovement>() != null
            || other.GetComponentInParent<PlayerMovement>() != null;
    }

    void Start()
    {
        roomController = GetComponentInParent<RoomController>();
        CreatePromptUI();
    }

    void CreatePromptUI()
    {
        promptUI = new GameObject("DoorPrompt");
        promptUI.transform.position = transform.position + promptOffset;

        promptText = promptUI.AddComponent<TextMeshPro>();
        promptText.text = "[E] Open";
        promptText.fontSize = 4f;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = Color.white;
        promptText.sortingOrder = 20;
        promptText.textWrappingMode = TextWrappingModes.NoWrap;

        // Pull font from RoomController so you only set it once
        if (roomController != null && roomController.waveFont != null)
            promptText.font = roomController.waveFont;

        RectTransform rt = promptText.rectTransform;
        rt.sizeDelta = new Vector2(3f, 1f);

        promptUI.SetActive(false);
    }

    void Update()
    {
        // Keep prompt positioned on the door
        if (promptUI != null && promptUI.activeSelf)
        {
            promptUI.transform.position = transform.position + promptOffset;
        }

        if (!HasConnectedRoom())
        {
            if (promptUI != null)
                promptUI.SetActive(false);

            return;
        }

        if (playerInRange)
        {
            bool locked = roomController != null && roomController.IsWaveActive();

            // Show prompt
            if (promptUI != null)
            {
                promptUI.SetActive(true);
                if (locked)
                {
                    promptText.text = "Locked";
                    promptText.color = Color.red;
                }
                else
                {
                    promptText.text = "[" + GameKeybinds.ToDisplayString(GameKeybinds.Interact) + "] Open";
                    promptText.color = Color.white;
                }
            }

            // Only allow interaction when not locked
            if (!locked && Input.GetKeyDown(GameKeybinds.Interact))
            {
                if (roomController != null)
                {
                    roomController.TryMove(moveDirection);
                    if (promptUI != null) promptUI.SetActive(false);
                }
            }
        }
        else if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayerCollider(other) && HasConnectedRoom())
            playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayerCollider(other))
        {
            playerInRange = false;
            if (promptUI != null) promptUI.SetActive(false);
        }
    }

    private bool HasConnectedRoom()
    {
        if (roomController == null)
            roomController = GetComponentInParent<RoomController>();

        if (roomController == null || roomController.generator == null)
            return false;

        var rooms = roomController.generator.GetRoomData();
        if (rooms == null)
            return false;

        Vector2Int targetPos = roomController.currentGridPos + moveDirection;
        return rooms.ContainsKey(targetPos);
    }
}
