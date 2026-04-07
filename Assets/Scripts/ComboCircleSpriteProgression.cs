using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class ComboCircleSpriteProgression : MonoBehaviour
{
    [Header("Combo Circle Sprite Progression")]
    public Image comboCircleImage;
    public bool alwaysUseFirstSprite = false;
    [Min(1)] public int comboHitsPerSpriteStep = 2;
    public Sprite comboSprite1;
    public Sprite comboSprite2;
    public Sprite comboSprite3;
    public Sprite comboSprite4;
    public Sprite comboSprite5;
    public Sprite comboSprite6;
    public Sprite comboSprite7;

    private Sprite defaultSprite;

    private void Awake()
    {
        if (comboCircleImage == null)
            comboCircleImage = GetComponent<Image>();

        if (comboCircleImage != null)
            defaultSprite = comboCircleImage.sprite;
    }

    public void SetComboHits(int comboHits)
    {
        if (comboCircleImage == null)
            return;

        comboCircleImage.enabled = true;

        if (alwaysUseFirstSprite)
        {
            comboCircleImage.sprite = comboSprite1 != null ? comboSprite1 : defaultSprite;
            return;
        }

        Sprite targetSprite = GetComboProgressionSprite(comboHits);
        comboCircleImage.sprite = targetSprite != null ? targetSprite : defaultSprite;
    }

    private Sprite GetComboProgressionSprite(int comboHits)
    {
        if (comboHits <= 0)
            return comboSprite1 != null ? comboSprite1 : defaultSprite;

        int hitsPerStep = Mathf.Max(1, comboHitsPerSpriteStep);
        int spriteIndex = Mathf.Clamp((comboHits - 1) / hitsPerStep, 0, 6);
        switch (spriteIndex)
        {
            case 0: return comboSprite1;
            case 1: return comboSprite2;
            case 2: return comboSprite3;
            case 3: return comboSprite4;
            case 4: return comboSprite5;
            case 5: return comboSprite6;
            default: return comboSprite7;
        }
    }
}
