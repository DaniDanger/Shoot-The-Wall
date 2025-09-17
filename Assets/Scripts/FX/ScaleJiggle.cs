using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ScaleJiggle : MonoBehaviour
{
    [Tooltip("How the scale returns to original (0 = linear, 1 = very snappy).")]
    [Range(0f, 1f)] public float ease = 0.25f;

    public float xScale = 0.9f;
    public float yScale = 1.1f;
    public float duration = 0.08f;

    [Tooltip("Extra squash/stretch per damage above 1 (multiplier on base amplitude).")]
    public float extraPerDamage = 0.25f;
    [Tooltip("Clamp for min X scale when amplifying by damage.")]
    public float minXScale = 0.75f;
    [Tooltip("Clamp for max Y scale when amplifying by damage.")]
    public float maxYScale = 1.35f;

    private Vector3 originalScale;
    private bool baselineCaptured;
    private Coroutine playRoutine;

    private void Awake()
    {
        baselineCaptured = false;
    }

    private void Start()
    {
        // Capture baseline after layout/resizing has been applied (WallGrid/UI set scale before Start)
        // but do not overwrite if we already captured it on first Play()
        if (!baselineCaptured)
        {
            originalScale = transform.localScale;
            baselineCaptured = true;
        }
    }

    public void Play()
    {
        if (duration <= 0f) return;
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
        if (!baselineCaptured)
        {
            originalScale = transform.localScale;
            baselineCaptured = true;
        }
        // Always return to baseline before starting a new jiggle
        transform.localScale = originalScale;
        playRoutine = StartCoroutine(PlayRoutine(xScale, yScale, duration));
    }

    public void PlayForDamage(int damage)
    {
        if (duration <= 0f) return;
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
        if (!baselineCaptured)
        {
            originalScale = transform.localScale;
            baselineCaptured = true;
        }
        // Reset to baseline before computing new targets
        transform.localScale = originalScale;

        int dmg = Mathf.Max(1, damage);
        float multiplier = 1f + (dmg - 1) * Mathf.Max(0f, extraPerDamage);

        float baseDx = Mathf.Abs(1f - Mathf.Clamp(xScale, 0f, 1f)); // compression amplitude
        float baseDy = Mathf.Abs(Mathf.Max(1f, yScale) - 1f);       // stretch amplitude

        float unClampedX = 1f - baseDx * multiplier;
        float unClampedY = 1f + baseDy * multiplier;

        float targetX = Mathf.Max(Mathf.Clamp(minXScale, 0.01f, 1f), unClampedX);
        float targetY = Mathf.Min(Mathf.Max(1f, maxYScale), unClampedY);

        playRoutine = StartCoroutine(PlayRoutine(targetX, targetY, duration));
    }

    public void ResetScale()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
        transform.localScale = originalScale;
    }

    private IEnumerator PlayRoutine(float xScale, float yScale, float duration)
    {
        Vector3 target = new Vector3(originalScale.x * xScale, originalScale.y * yScale, originalScale.z);
        transform.localScale = target;
        float t = 0f;
        float k = Mathf.Clamp01(ease);
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            // Smooth return with ease tweak
            float s = 1f - Mathf.Pow(1f - u, 2f + 4f * k);
            transform.localScale = Vector3.LerpUnclamped(target, originalScale, s);
            yield return null;
        }
        transform.localScale = originalScale;
        playRoutine = null;
    }
}


