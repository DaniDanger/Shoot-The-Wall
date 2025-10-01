using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UiPunch : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Target")]
    [Tooltip("Which RectTransform to scale. If null, scales this GameObject's RectTransform.")]
    public RectTransform target;

    [Header("Scales")]
    [Range(0.5f, 2f)] public float hoverScale = 1.06f;
    [Range(0.5f, 2f)] public float pressScale = 0.96f;

    [Header("Timing")]
    public float inDuration = 0.06f;
    public float outDuration = 0.08f;
    public bool useUnscaledTime = true;

    [Header("Behavior")]
    [Tooltip("Skip animations if a Selectable is present and not interactable.")]
    public bool requireInteractable = true;

    [Header("SFX")]
    public bool playClickSfx = true;
    public AudioManager.SfxId clickSfxId = AudioManager.SfxId.UI_Click;
    [Range(0f, 1f)] public float clickSfxVolume = 1f;
    [Range(0f, 0.2f)] public float clickPitchJitter = 0.02f;

    private RectTransform self;
    private Vector3 baseScale = Vector3.one;
    private Coroutine anim;
    private bool isHovered;
    private bool isPressed;
    private Selectable selectable;

    private void Awake()
    {
        self = GetComponent<RectTransform>();
        if (target == null) target = self;
        if (target != null) baseScale = target.localScale;
        selectable = GetComponent<Selectable>();
    }

    private bool CanAnimate()
    {
        if (!isActiveAndEnabled) return false;
        if (target == null) return false;
        if (requireInteractable && selectable != null && !selectable.interactable) return false;
        return true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (!CanAnimate()) return;
        float to = isPressed ? pressScale : hoverScale;
        StartAnim(to, inDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (!CanAnimate()) return;
        float to = baseScale.x; // uniform
        StartAnim(to, outDuration);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        if (!CanAnimate()) return;
        StartAnim(pressScale, inDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        if (!CanAnimate()) return;
        float to = isHovered ? hoverScale : baseScale.x;
        StartAnim(to, outDuration);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (playClickSfx && AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(clickSfxId, clickSfxVolume, clickPitchJitter);
    }

    private void StartAnim(float targetUniformScale, float duration)
    {
        if (anim != null) StopCoroutine(anim);
        float t = Mathf.Max(0.0001f, duration);
        float from = target.localScale.x;
        float to = targetUniformScale;
        anim = StartCoroutine(AnimScale(from, to, t));
    }

    private System.Collections.IEnumerator AnimScale(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - u) * (1f - u); // ease-out quadratic
            float s = Mathf.LerpUnclamped(from, to, eased);
            target.localScale = baseScale * s;
            yield return null;
        }
        target.localScale = baseScale * to;
        anim = null;
    }

    private void OnDisable()
    {
        if (target != null)
            target.localScale = baseScale;
        anim = null;
        isHovered = false;
        isPressed = false;
    }
}


