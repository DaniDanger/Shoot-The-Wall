using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class UpgradePanelPanner : MonoBehaviour, IDragHandler
{
    [Tooltip("The RectTransform to pan (e.g., gridRoot). If null, uses own RectTransform.")]
    public RectTransform content;

    [Tooltip("Speed multiplier for drag panning (pixels per pixel).")]
    public float dragSpeed = 1f;

    private RectTransform self;

    private void Awake()
    {
        self = GetComponent<RectTransform>();
        if (content == null) content = self;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (content == null) return;
        Vector2 delta = eventData.delta * dragSpeed;
        content.anchoredPosition += delta;
    }
}


