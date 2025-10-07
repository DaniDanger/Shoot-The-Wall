using UnityEngine;

namespace FX
{
    [DisallowMultipleComponent]
    public class ExplosionShotVfx : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("SpriteRenderer to color/scale. If null, the first SpriteRenderer on this object or its children is used.")]
        public SpriteRenderer targetRenderer;
        [Tooltip("Transform to scale. If null, uses the targetRenderer's transform.")]
        public Transform targetTransform;

        [Header("Timing")]
        [Tooltip("Effect lifetime in seconds.")]
        public float duration = 0.08f;
        [Tooltip("Use unscaled time for the effect.")]
        public bool useUnscaledTime = true;

        [Header("Animation")]
        [Tooltip("Start scale as a multiplier of the target's base scale.")]
        public float startScale = 0f;
        [Tooltip("End scale as a multiplier of the target's base scale.")]
        public float endScale = 1.0f;
        [Tooltip("Start color (typically white).")]
        public Color startColor = Color.white;
        [Tooltip("End color (e.g., orange).")]
        public Color endColor = new Color(1f, 0.6f, 0.1f, 1f);

        [Header("Behavior")]
        [Tooltip("If true, disables the GameObject after the effect completes.")]
        public bool disableOnComplete = true;

        private Vector3 baseScale;
        private Color baseColor;
        private System.Collections.IEnumerator running;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<SpriteRenderer>();
            if (targetTransform == null && targetRenderer != null)
                targetTransform = targetRenderer.transform;
            if (targetTransform == null)
                targetTransform = transform;
            baseScale = targetTransform.localScale;
            if (targetRenderer != null)
                baseColor = targetRenderer.color;
        }

        private void OnDisable()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
            if (targetTransform != null)
                targetTransform.localScale = baseScale;
            if (targetRenderer != null)
                targetRenderer.color = baseColor;
        }

        public void Play()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
            running = RunEffect();
            StartCoroutine(running);
        }

        public void PlayAt(Vector3 worldPosition, float endScaleOverride = -1f)
        {
            transform.position = worldPosition;
            if (endScaleOverride > 0f)
                endScale = endScaleOverride;
            Play();
        }

        private System.Collections.IEnumerator RunEffect()
        {
            if (targetTransform == null || targetRenderer == null)
                yield break;

            Vector3 s0 = baseScale * Mathf.Max(0f, startScale);
            Vector3 s1 = baseScale * Mathf.Max(0f, endScale);
            targetTransform.localScale = s0;
            targetRenderer.color = startColor;

            float t = 0f;
            float d = Mathf.Max(0.0001f, duration);
            while (t < d)
            {
                t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float u = Mathf.Clamp01(t / d);
                float e = 1f - (1f - u) * (1f - u); // ease-out quad
                targetTransform.localScale = Vector3.LerpUnclamped(s0, s1, e);
                targetRenderer.color = Color.LerpUnclamped(startColor, endColor, e);
                yield return null;
            }
            targetTransform.localScale = s1;
            targetRenderer.color = endColor;

            var pool = ExplosionShotVfxPool.Instance;
            if (pool != null)
                pool.Return(this);
            else if (disableOnComplete)
                gameObject.SetActive(false);

            running = null;
        }
    }
}


