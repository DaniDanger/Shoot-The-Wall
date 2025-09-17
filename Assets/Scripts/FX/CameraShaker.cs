using UnityEngine;

[DisallowMultipleComponent]
public class CameraShaker : MonoBehaviour
{
    [Header("Shake Settings")]
    [Tooltip("Overall shake strength in world units.")]
    public float amplitude = 0.05f;

    // Defaults tuned for subtle shake; keep private to reduce inspector clutter
    private const float DECAY_RATE = 7f;
    private const float FREQUENCY = 22f;
    private const float MAX_INTENSITY = 1f;

    // Hit response defaults
    private const float BASE_HIT = 0.12f;
    private const float CRIT_MULT = 1.5f;
    private const float KILL_MULT = 1.25f;

    private Vector3 originalPosition;
    private float intensity; // aka trauma
    private Camera cam;
    private bool initialized;

    // Per-frame aggregation for hits
    private float pendingTrauma;
    private int pendingHits;
    [Tooltip("Clamp the total trauma applied per frame to avoid excessive shake.")]
    public float maxTraumaPerFrame = 0.25f;

    private static CameraShaker instance;
    public static CameraShaker Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }
        instance = this;
        cam = GetComponent<Camera>();
        originalPosition = transform.localPosition;
        initialized = true;
    }

    private void OnDisable()
    {
        // Ensure no drift when disabled
        transform.localPosition = originalPosition;
        intensity = 0f;
    }

    public void AddHitShake(bool wasCrit, bool wasKill)
    {
        float add = Mathf.Max(0f, BASE_HIT);
        if (wasCrit) add *= Mathf.Max(1f, CRIT_MULT);
        if (wasKill) add *= Mathf.Max(1f, KILL_MULT);
        // Aggregate within this frame; apply in LateUpdate with diminishing returns
        pendingTrauma += add;
        pendingHits++;
    }

    public void AddTrauma(float amount)
    {
        intensity = Mathf.Clamp(intensity + Mathf.Max(0f, amount), 0f, Mathf.Max(0.0001f, MAX_INTENSITY));
    }

    private void LateUpdate()
    {
        if (!initialized) return;

        // Apply aggregated hit trauma once per frame with diminishing returns
        if (pendingHits > 0)
        {
            float scaled = pendingTrauma * Mathf.Sqrt(Mathf.Max(1, pendingHits));
            float clamped = Mathf.Min(maxTraumaPerFrame, scaled);
            AddTrauma(clamped);
            pendingTrauma = 0f;
            pendingHits = 0;
        }
        float dt = Time.unscaledDeltaTime;
        if (intensity <= 0.0001f)
        {
            // Reset and early out
            transform.localPosition = originalPosition;
            intensity = 0f;
            return;
        }

        float t = Time.unscaledTime * Mathf.Max(0.0001f, FREQUENCY);
        // Two-axis smooth noise using Perlin
        float nx = (Mathf.PerlinNoise(t, 0.123f) * 2f - 1f);
        float ny = (Mathf.PerlinNoise(0.456f, t) * 2f - 1f);
        float amt = Mathf.Max(0f, amplitude) * Mathf.Clamp01(intensity);
        Vector3 offset = new Vector3(nx, ny, 0f) * amt;
        transform.localPosition = originalPosition + offset;

        // Decay intensity
        intensity = Mathf.Max(0f, intensity - Mathf.Max(0f, DECAY_RATE) * dt);
        if (intensity <= 0.0001f)
        {
            transform.localPosition = originalPosition;
            intensity = 0f;
        }
    }
}


