using UnityEngine;

[DisallowMultipleComponent]
public class Brick : MonoBehaviour
{
    [Tooltip("Starting hit points for this brick.")]
    public float maxHp = 3f;

    [Tooltip("Currency granted on destruction.")]
    public int reward = 1;

    [Tooltip("Continuous downward push force applied to the ship while in contact (optional).")]
    public float pushDownForce = 10f;

    [SerializeField] private float currentHp;
    public float CurrentHp => currentHp;
    private ScaleJiggle jiggle;
    [Tooltip("Child transform that holds the SpriteRenderer to scale on damage. If not set, the first SpriteRenderer found is used.")]
    public Transform visual;
    private Vector3 visualBaseScale = Vector3.one;
    private Color spawnTint = Color.white;
    // Prevents chain reactions for on-kill explosion: depth 0 triggers, deeper calls skip.
    private static int onKillExplosionDepth;
    // One-shot flag: when true, this damage should not trigger neighbor explosion.
    private bool suppressKillExplosion;

    private void Awake()
    {
        jiggle = GetComponentInChildren<ScaleJiggle>();
        currentHp = Mathf.Max(1f, maxHp);
        // Ensure no per-brick Rigidbody2D remains (performance: rely on WallGrid root body)
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            Destroy(rb);

        // Resolve visual child (SpriteRenderer) to scale for damage feedback
        if (visual == null)
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
                visual = sr.transform;
        }
        if (visual != null)
            visualBaseScale = visual.localScale;

        // Initialize spawn tint defensively from current sprite color
        var srInit = GetComponentInChildren<SpriteRenderer>();
        if (srInit != null)
            spawnTint = srInit.color;

        SetVisualScaleByHp();
    }

    public void Configure(float hp, int rewardValue)
    {
        maxHp = Mathf.Max(1f, hp);
        currentHp = maxHp;
        reward = Mathf.Max(0, rewardValue);
        // visuals handled by single SpriteRenderer again
        SetVisualScaleByHp();
    }

    public void SetSpawnTint(Color c)
    {
        spawnTint = c;
    }

    public Color GetSpawnTint()
    {
        return spawnTint;
    }

    // No collider-based push; player uses grid math collision against WallGrid.

    public void TakeDamage(float amount)
    {
        float applied = Mathf.Max(0f, amount);
        float prevHp = currentHp;
        currentHp -= applied;
        if (jiggle != null)
            jiggle.PlayForDamage(Mathf.Max(1, Mathf.RoundToInt(applied))); // scale by magnitude
        // Avoid shrinking to zero before death anim; update visual scale only if not killed
        if (currentHp > 0f)
            SetVisualScaleByHp();
        if (currentHp <= 0f)
        {
            Die();
        }
        else
        {
            // simple hit feedback hook; optional to implement later
        }
    }

    // Explosion damage that should not cause a new explosion chain.
    public void TakeExplosionDamage(float amount)
    {
        suppressKillExplosion = true;
        TakeDamage(amount);
        suppressKillExplosion = false;
    }

    private void Die()
    {
        CurrencyStore.AddRunCurrency(reward);
        // Disable collider immediately to avoid extra hits during death anim
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        // Notify parent wall to decrement alive count
        var wall = GetComponentInParent<WallGrid>();
        if (wall != null)
        {
            wall.NotifyBrickDestroyed(this);
        }

        // Only play the explosion stretch/squash effect (and schedule neighbor damage)
        // when the on-kill explosion upgrade is active.
        if (RunModifiers.OnKillExplosionEnabled && RunModifiers.OnKillExplosionDamage > 0f)
        {
            StartCoroutine(DeathExplosionSequence());
            return;
        }

        // No upgrade: despawn immediately (legacy simple death)
        gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator DeathExplosionSequence()
    {
        // Target visual to animate
        Transform t = visual != null ? visual : transform;
        SpriteRenderer sr = t != null ? t.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        Color original = sr != null ? sr.color : Color.white;
        Vector3 baseScale = t != null ? t.localScale : Vector3.one;

        // Determine visual stretch width based on explosion depth (depth 1 => 4, depth 2 => 8, ...)
        int depth = 1;
        if (RunModifiers.OnKillExplosionEnabled)
            depth = 1 + Mathf.Max(0, RunModifiers.OnKillExplosionExtraDepth);
        float xTargetFactor = 4f * Mathf.Max(1, depth);

        // Animate: set Y to 0.2, X 0 -> xTargetFactor quickly, then back
        float outDur = 0.04f;
        float inDur = 0.04f;
        Vector3 start = new Vector3(baseScale.x * 0f, baseScale.y * 0.2f, baseScale.z);
        Vector3 target = new Vector3(baseScale.x * xTargetFactor, baseScale.y * 0.2f, baseScale.z);
        float tOut = 0f;
        if (sr != null) sr.color = Color.white;
        if (t != null) t.localScale = start;
        while (tOut < outDur)
        {
            tOut += Time.deltaTime;
            float u = Mathf.Clamp01(tOut / outDur);
            float e = 1f - (1f - u) * (1f - u);
            if (t != null) t.localScale = Vector3.LerpUnclamped(start, target, e);
            yield return null;
        }
        if (t != null) t.localScale = target;

        float tIn = 0f;
        while (tIn < inDur)
        {
            tIn += Time.deltaTime;
            float u = Mathf.Clamp01(tIn / inDur);
            float e = u * u;
            if (t != null) t.localScale = Vector3.LerpUnclamped(target, baseScale, e);
            yield return null;
        }
        if (t != null) t.localScale = baseScale;
        if (sr != null) sr.color = original;

        // On-kill explosion: schedule after the stretch/squash completes. No chain reactions.
        if (!suppressKillExplosion && RunModifiers.OnKillExplosionEnabled && RunModifiers.OnKillExplosionDamage > 0f && onKillExplosionDepth == 0)
        {
            WallGrid wall = GetComponentInParent<WallGrid>();
            if (wall != null)
            {
                float dmg = RunModifiers.OnKillExplosionDamage;
                onKillExplosionDepth++;
                wall.QueueNeighborExplosion(this, dmg, 0f);
                onKillExplosionDepth--;
            }
        }

        // Finally despawn
        gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator DeactivateAfterDelay(float seconds)
    {
        if (seconds > 0f)
            yield return new WaitForSeconds(seconds);
        gameObject.SetActive(false);
    }

    private void SetVisualScaleByHp()
    {
        if (visual == null)
            return;
        float ratio = Mathf.Clamp01(currentHp / Mathf.Max(1f, maxHp));
        visual.localScale = visualBaseScale * ratio;
    }
}


