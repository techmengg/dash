using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    public float floatSpeed = 2f;
    public float overlayFloatSpeed = 90f;
    public float destroyTime = 1f;

    private TMP_Text textMesh;
    private bool isInitialized = false;
    private bool useOverlayMotion = false;

    private void Awake()
    {
        textMesh = GetComponent<TMP_Text>();
        if (textMesh == null)
            textMesh = GetComponentInChildren<TMP_Text>(true);
    }

    public void Setup(float damageAmount, bool overlayMotion = false)
    {
        useOverlayMotion = overlayMotion;

        if (textMesh == null)
        {
            textMesh = GetComponent<TMP_Text>();
            if (textMesh == null)
                textMesh = GetComponentInChildren<TMP_Text>(true);
        }

        if (textMesh == null)
        {
            Debug.LogWarning("DamagePopup: No TMP_Text found on popup instance.", this);
            Destroy(gameObject, destroyTime);
            return;
        }

        textMesh.text = damageAmount.ToString("F1");
        Destroy(gameObject, destroyTime);
        isInitialized = true;
    }

    void Update()
    {
        if (isInitialized)
        {
            float speed = useOverlayMotion ? overlayFloatSpeed : floatSpeed;
            float delta = useOverlayMotion ? Time.unscaledDeltaTime : Time.deltaTime;
            transform.position += new Vector3(0f, speed * delta, 0f);
        }
    }
}