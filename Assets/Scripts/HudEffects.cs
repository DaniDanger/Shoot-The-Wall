using UnityEngine;

[DisallowMultipleComponent]
public class HudEffects : MonoBehaviour
{
    [Header("Refs")]
    public CurrencyPingManager pingManager; // optional; auto-found if not set

    [Header("Pop Settings")]
    [Tooltip("How long the pop returns to normal scale (seconds, unscaled time).")]
    public float popDuration = 0.12f;

    [Tooltip("Scale added per delivered unit (1 => +this, 10 => +10*this).")]
    public float perUnitScaleStep = 0.03f;

    [Tooltip("Maximum absolute scale factor during pop.")]
    public float maxScale = 1.35f;

    [Header("Pop Behavior Tuning")]
    [Tooltip("Minimum added scale per pop, even for 1 unit.")]
    public float minBump = 0.02f;

    [Tooltip("Extra headroom above max to allow visible pops when saturated.")]
    public float headroom = 0.06f;

    [Tooltip("Tiny overshoot when already at cap, to ensure a visible pulse.")]
    public float pulseAtCap = 0.01f;

    private RectTransform icon;
    private Vector3 baseScale = Vector3.one;
    private Coroutine popRoutine;

    private void Start()
    {
        if (pingManager == null)
            pingManager = FindAnyObjectByType<CurrencyPingManager>();

        if (pingManager != null)
        {
            pingManager.onPingDelivered.AddListener(OnPingDelivered);
            icon = pingManager.currencyIcon;
        }

        if (icon != null)
            baseScale = icon.localScale;
    }

    private void OnDisable()
    {
        if (pingManager != null)
            pingManager.onPingDelivered.RemoveListener(OnPingDelivered);
        if (icon != null)
            icon.localScale = baseScale;
    }

    private void OnPingDelivered(int units)
    {
        PlayCurrencyPop(Mathf.Max(1, units));
    }

    public void PlayCurrencyPop(int units)
    {
        if (icon == null)
        {
            if (pingManager == null) return;
            icon = pingManager.currencyIcon;
            if (icon == null) return;
            baseScale = icon.localScale;
        }

        float currentFactor = (baseScale.x != 0f) ? icon.localScale.x / baseScale.x : 1f;
        if (currentFactor <= 0f || float.IsNaN(currentFactor) || float.IsInfinity(currentFactor))
            currentFactor = 1f;

        float cap = Mathf.Max(1f, maxScale) + Mathf.Max(0f, headroom);
        float added = Mathf.Max(minBump, Mathf.Max(0f, perUnitScaleStep) * Mathf.Max(1, units));
        float desiredFactor = Mathf.Max(1f, currentFactor) + added;
        if (currentFactor >= cap - 0.0001f)
        {
            float pulse = Mathf.Max(minBump, pulseAtCap);
            desiredFactor = Mathf.Min(cap + Mathf.Max(0f, pulseAtCap), currentFactor + pulse);
        }
        else
        {
            desiredFactor = Mathf.Min(cap, desiredFactor);
        }

        icon.localScale = new Vector3(baseScale.x * desiredFactor, baseScale.y * desiredFactor, baseScale.z);

        if (popRoutine != null)
            StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopReturnRoutine());
    }

    private System.Collections.IEnumerator PopReturnRoutine()
    {
        if (icon == null) yield break;
        Vector3 startScale = icon.localScale;
        float t = 0f;
        float dur = Mathf.Max(0.02f, popDuration);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float ease = 1f - (1f - u) * (1f - u);
            icon.localScale = Vector3.LerpUnclamped(startScale, baseScale, ease);
            yield return null;
        }
        icon.localScale = baseScale;
        popRoutine = null;
    }
}


