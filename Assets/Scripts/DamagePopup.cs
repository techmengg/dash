using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    public float floatSpeed = 2f;
    public float destroyTime = 1f;

    private TextMeshPro textMesh;
    private bool isInitialized = false;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        
    }

    public void Setup(float damageAmount)
    {
        textMesh.text = damageAmount.ToString("F1");
        Destroy(gameObject, destroyTime);
        isInitialized = true;
    }

    void Update()
    {
        if (isInitialized)
        {
            transform.position += new Vector3(0, floatSpeed * Time.deltaTime, 0);
        }
    }
}