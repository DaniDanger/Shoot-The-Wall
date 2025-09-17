using UnityEngine;

[DisallowMultipleComponent]
public class GraveBombMarker : MonoBehaviour
{
    public Color graveTint = new Color(0.8f, 0.8f, 0.2f, 1f);
    public bool isDetonating;
    private Color originalTint = Color.white;
    private SpriteRenderer sprite;

    private void Awake()
    {
        sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null)
            originalTint = sprite.color;
    }

    public void ApplyTint()
    {
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null)
            sprite.color = graveTint;
    }

    public void ClearTint()
    {
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null)
            sprite.color = originalTint;
    }
}








