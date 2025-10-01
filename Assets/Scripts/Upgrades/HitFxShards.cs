using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HitFxShards : MonoBehaviour
{
    [Header("Shard Visuals")]
    public float shardSize = 0.06f; // desired world units (width/height)
    public float shardLifetime = 0.5f;
    public float gravity = -2.5f;
    public float speedMin = 1.5f;
    public float speedMax = 3.0f;
    public float spreadAngleDeg = 12f; // tight cone around downward direction
    public Color critTint = new Color(1f, 0.9f, 0.2f, 1f);
    public float critSpeedMult = 1.25f;
    [Range(0f, 1f)] public float critTintStrength = 1f; // 1 = full yellow, 0.5 = blend
    [Tooltip("Spawn shards this far along the impact normal. Use negative to spawn inside the brick face.")]
    public float spawnNormalOffset = -0.02f;

    [Header("Bundling Visuals")]
    [Tooltip("Tint applied to bundled (units > 1) shards to aid visibility.")]
    public Color bigShardTint = Color.white;
    [Tooltip("Scale multiplier for bundled (units > 1) shards.")]
    public float bigShardSizeMultiplier = 1.6f;

    [Range(0.8f, 1.2f)] public float colorJitter = 0.95f; // random brightness multiplier around base

    private readonly List<Shard> active = new List<Shard>(256);
    private readonly Stack<Shard> pool = new Stack<Shard>(256);
    private static Sprite pixelSprite;
    private CurrencyPingManager pingManager;
    [Header("Pooling")]
    [Tooltip("Instantiate this many shard objects at Awake to avoid runtime spikes.")]
    public int prewarmCount = 0;

    private void Awake()
    {
        EnsurePixelSprite();
        pingManager = FindAnyObjectByType<CurrencyPingManager>();
        // Prewarm pool to reduce first-hit allocations
        int target = Mathf.Max(0, prewarmCount);
        for (int i = 0; i < target; i++)
        {
            var s = Get();
            if (s.obj != null) s.obj.SetActive(false);
            pool.Push(s);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var s = active[i];
            s.life -= dt;
            if (s.life <= 0f)
            {
                Recycle(i);
                continue;
            }

            s.velocity += new Vector2(0f, gravity) * dt;
            s.transform.position += (Vector3)(s.velocity * dt);

            // Keep shards fully opaque; removal happens on recycle to avoid UI ping inheriting zero alpha

            active[i] = s;
        }
    }

    public void EmitShards(Vector3 position, Vector2 direction, int count, Color baseColor, bool crit)
    {
        // Delegate to the unified custom emitter with defaults
        EmitCustomShards(position, direction, Mathf.Max(0, count), 1, 1f, baseColor, crit);
    }

    private void EnsurePixelSprite()
    {
        if (pixelSprite != null) return;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixel(0, 0, Color.white);
        tex.SetPixel(1, 0, Color.white);
        tex.SetPixel(0, 1, Color.white);
        tex.SetPixel(1, 1, Color.white);
        tex.Apply();
        pixelSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
    }

    private Shard Get()
    {
        if (pool.Count > 0)
        {
            var s = pool.Pop();
            s.obj.SetActive(true);
            return s;
        }
        else
        {
            var go = new GameObject("Shard", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 100; // draw above bricks
            return new Shard { obj = go, transform = go.transform, renderer = sr, life = 0f, velocity = Vector2.zero };
        }
    }

    private void Recycle(int index)
    {
        int last = active.Count - 1;
        var s = active[index];

        // Capture final world position and tint before disabling
        Vector3 pos = s.transform.position;
        Color col = s.renderer != null ? s.renderer.color : Color.white;
        float worldSize = pixelSprite != null ? pixelSprite.bounds.size.x : shardSize;

        // Swap-remove to avoid O(n) shifts
        if (index != last)
            active[index] = active[last];
        active.RemoveAt(last);

        // Spawn a ping at the shard's final position with its units value
        if (pingManager == null)
            pingManager = FindAnyObjectByType<CurrencyPingManager>();
        if (pingManager != null)
            pingManager.EnqueueUnits(pos, col, worldSize * Mathf.Max(1f, s.sizeMultiplier), Mathf.Max(1, s.units));

        s.obj.SetActive(false);
        pool.Push(s);
    }

    private struct Shard
    {
        public GameObject obj;
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector2 velocity;
        public float life;
        public int units;
        public float sizeMultiplier;
    }

    // Extended API: emit shards with custom units and size multiplier
    public void EmitCustomShards(Vector3 position, Vector2 direction, int count, int unitsPerShard, float sizeMultiplier, Color baseColor, bool crit)
    {
        EnsurePixelSprite();
        float angle0 = -90f;
        float spread = Mathf.Abs(spreadAngleDeg);
        float speedMul = crit ? Mathf.Max(1f, critSpeedMult) : 1f;
        Vector3 pos = position + (Vector3)(direction.normalized * spawnNormalOffset);

        float baseWorld = pixelSprite.bounds.size.x;
        float baseScale = baseWorld > 0.0001f ? shardSize / baseWorld : 1f;
        float scaleMult = Mathf.Max(0.01f, sizeMultiplier);
        for (int i = 0; i < count; i++)
        {
            var s = Get();
            s.transform.position = pos;
            float scale = baseScale * scaleMult;
            s.transform.localScale = new Vector3(scale, scale, 1f);
            s.renderer.sprite = pixelSprite;
            float bright = Random.Range(colorJitter, 1f / Mathf.Max(0.0001f, colorJitter));
            Color col = baseColor;
            if (crit)
                col = Color.Lerp(baseColor, critTint, Mathf.Clamp01(critTintStrength));
            if (unitsPerShard > 1)
                col = bigShardTint;
            s.renderer.color = col * new Color(bright, bright, bright, 1f);
            s.life = shardLifetime;
            float ang = angle0 + Random.Range(-spread, spread);
            float spd = Random.Range(speedMin, speedMax) * speedMul;
            s.velocity = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * spd;
            s.units = Mathf.Max(1, unitsPerShard);
            s.sizeMultiplier = scaleMult;
            active.Add(s);
        }
    }
}


