using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [Tooltip("Units per second.")]
    public float speed = 14f;

    [Tooltip("Lifetime in seconds before returning to pool.")]
    public float lifetime = 2.5f;

    [Tooltip("Damage applied to bricks on hit.")]
    public float damage = 1f;

    [Header("Cluster Split FX")]
    [Tooltip("Minimum time to decelerate to zero before popping.")]
    public float clusterDecelMin = 0.6f;
    [Tooltip("Maximum time to decelerate to zero before popping.")]
    public float clusterDecelMax = 0.9f;
    public float clusterHoldDuration = 0.25f;
    public float clusterSquashScale = 0.5f;
    public float clusterSquashTime = 0.08f;
    public float clusterPopScale = 1.2f;
    public float clusterPopTime = 0.06f;
    public float shardAngleJitter = 5f;
    public float shardSpawnOffsetJitter = 0.05f;
    public GameObject clusterExplosionVfx;

    private Rigidbody2D rigidBody;
    private Collider2D ownCollider;
    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private float lifeRemaining;
    private Vector2 direction = Vector2.up;

    private ProjectilePool ownerPool;
    private Camera mainCamera;
    private HitFxShards shardFx;
    private WallGrid cachedWall;
    private Vector3 prevPos;

    private static WallGrid sCachedWall;
    private static HitFxShards sCachedShards;

    private bool hasHit;
    private bool wasCrit;
    public bool isClusterShard;
    private bool clusterSequenceActive;
    private Vector3 originalLocalScale;
    private bool scaleCaptured;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody2D>();
        ownCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
        if (sCachedShards == null)
            sCachedShards = FindAnyObjectByType<HitFxShards>();
        shardFx = sCachedShards;
        if (rigidBody != null)
        {
            rigidBody.gravityScale = 0f;
            rigidBody.bodyType = RigidbodyType2D.Kinematic;
            rigidBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rigidBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
        if (ownCollider != null)
        {
            ownCollider.isTrigger = true; // projectiles overlap rather than collide physically
        }
    }

    private void OnDisable()
    {
        if (rigidBody != null)
            rigidBody.linearVelocity = Vector2.zero;
    }

    public void Initialize(ProjectilePool pool, Camera camera)
    {
        ownerPool = pool;
        mainCamera = camera;
    }

    public void Launch(Vector3 position, Vector2 launchDirection, float launchSpeed, float life, float dmg = 1f, bool crit = false)
    {
        transform.position = position;
        direction = launchDirection.sqrMagnitude > 0f ? launchDirection.normalized : Vector2.up;
        speed = launchSpeed;
        lifetime = life;
        damage = Mathf.Max(0f, dmg);
        lifeRemaining = lifetime;
        hasHit = false;
        wasCrit = crit;
        clusterSequenceActive = false;
        isClusterShard = false;
        // Visual: tint projectile on crit using shard tint color
        if (spriteRenderer != null)
        {
            if (crit)
            {
                Color c = shardFx != null ? shardFx.critTint : new Color(1f, 0.9f, 0.2f, 1f);
                spriteRenderer.color = c;
            }
            else
            {
                spriteRenderer.color = baseColor;
            }
        }
        if (ownCollider != null)
            ownCollider.enabled = true;
        if (rigidBody != null)
            rigidBody.linearVelocity = direction * speed;
        if (sCachedWall == null)
            sCachedWall = FindAnyObjectByType<WallGrid>();
        cachedWall = sCachedWall;
        prevPos = transform.position;
    }

    private void Update()
    {
        if (!clusterSequenceActive)
            lifeRemaining -= Time.deltaTime;
        if (lifeRemaining <= 0f)
        {
            ReturnToPool();
            return;
        }

        if (mainCamera == null)
            return;

        // Logic-level collision: cast segment each frame (cheap) and resolve first brick hit
        var wall = cachedWall;
        if (wall != null)
        {
            Vector3 prev = prevPos;
            Vector3 curr = transform.position;

            System.Action<Brick, Vector3> resolveHit = (hitBrick, impactPoint) =>
            {
                hasHit = true;
                // Detonate grave bomb if this brick is marked
                if (hitBrick.GetComponent<GraveBombMarker>() != null && wall != null)
                {
                    wall.DetonateGrave(hitBrick);
                }
                // Use brick's spawn tint for shard color (captured pre-damage)
                Color preColor = hitBrick != null ? hitBrick.GetSpawnTint() : Color.white;
                float dmg = Mathf.Max(0f, damage);
                float before = hitBrick.CurrentHp;
                hitBrick.TakeDamage(dmg);
                float after = hitBrick.CurrentHp;

                if (RunModifiers.OverflowCarryPercent > 0f && after <= 0f && before > 0f)
                {
                    float overkill = Mathf.Max(0f, dmg - Mathf.Max(0f, before));
                    if (overkill > 0f)
                    {
                        float carry = Mathf.Max(0f, overkill * Mathf.Max(0f, RunModifiers.OverflowCarryPercent));
                        if (carry > 0f)
                        {
                            Brick above = wall.FindBrickAboveImmediate(hitBrick);
                            if (above != null)
                                above.TakeDamage(carry);
                        }
                    }
                }

                // Compute emitted shard count and log hit info (always)
                int emittedShards2 = after > 0f ? Mathf.Max(1, Mathf.RoundToInt(dmg)) : Mathf.Max(1, Mathf.RoundToInt(hitBrick.maxHp));
                emittedShards2 = Mathf.Max(1, emittedShards2);
                try { Debug.Log($"[BrickHit] dmg={dmg:0.##} emittedShards={emittedShards2}"); } catch { }

                if (shardFx == null)
                    shardFx = sCachedShards;
                if (shardFx != null)
                {
                    EmitBundledShards(shardFx, impactPoint, direction, preColor, wasCrit, emittedShards2);
                }

                bool wasKill = after <= 0f && before > 0f;
                var shaker = CameraShaker.Instance;
                if (shaker != null)
                {
                    shaker.AddHitShake(wasCrit, wasKill);
                }
                var am = AudioManager.Instance;
                if (am != null)
                {
                    if (wasKill)
                        am.PlaySfx(AudioManager.SfxId.BrickKill, 1f, 0.02f);
                    //else
                    //am.PlaySfx(AudioManager.SfxId.BrickHit, 1f, 0.04f);
                }

                ReturnToPool();
            };

            Brick hit; Vector3 point;
            if (BrickCollisionService.CastSegment(wall, prev, curr, out hit, out point))
            {
                resolveHit(hit, point);
                return;
            }

            // Widen the cast by sampling ± perpendicular offsets based on projectile radius, clamped to spacing
            float radius = 0f;
            if (ownCollider != null)
            {
                var ext = ownCollider.bounds.extents;
                radius = Mathf.Max(ext.x, ext.y);
            }
            float pixelWorld = (Camera.main != null ? Camera.main.orthographicSize : 5f) * 2f / Mathf.Max(1, Screen.height);
            radius = Mathf.Max(radius, pixelWorld * 0.5f);
            Vector2 cell = wall.GetCellSize();
            Vector2 step = wall.GetStepSize();
            float spacingX = Mathf.Max(0f, step.x - cell.x);
            float spacingY = Mathf.Max(0f, step.y - cell.y);
            float spacingMin = Mathf.Min(spacingX, spacingY);
            if (spacingMin > 0f)
                radius = Mathf.Min(radius, spacingMin * 0.45f);
            if (radius <= 0f)
                radius = 0.01f;

            Vector2 seg = new Vector2(curr.x - prev.x, curr.y - prev.y);
            Vector2 dir2 = seg.sqrMagnitude > 0.000001f ? seg.normalized : (direction.sqrMagnitude > 0f ? direction.normalized : Vector2.up);
            Vector2 perp = new Vector2(-dir2.y, dir2.x);
            Vector3 off = (Vector3)(perp * radius);

            // +offset
            if (BrickCollisionService.CastSegment(wall, prev + off, curr + off, out hit, out point))
            {
                resolveHit(hit, point);
                return;
            }
            // -offset
            if (BrickCollisionService.CastSegment(wall, prev - off, curr - off, out hit, out point))
            {
                resolveHit(hit, point);
                return;
            }
        }
        prevPos = transform.position;
    }

    private static void EmitBundledShards(HitFxShards fx, Vector3 impactPoint, Vector2 direction, Color preColor, bool wasCrit, int emittedShards)
    {
        int big = emittedShards / 10;
        int small = emittedShards % 10;
        if (big > 0)
            fx.EmitCustomShards(impactPoint, direction, big, 10, Mathf.Max(0.01f, fx.bigShardSizeMultiplier), preColor, wasCrit);
        if (small > 0)
            fx.EmitCustomShards(impactPoint, direction, small, 1, 1f, preColor, wasCrit);
    }

    public void ReturnToPool()
    {
        if (ownerPool != null)
            ownerPool.Return(this);
        else
            gameObject.SetActive(false);
    }

    public void BeginClusterSplit(int shardCount, float shardDamage, float shardSpeed, float shardLifetime, float spreadDegrees)
    {
        if (clusterSequenceActive)
            return;
        StartCoroutine(ClusterSplitRoutine(Mathf.Max(1, shardCount), Mathf.Max(0f, shardDamage), Mathf.Max(0.01f, shardSpeed), Mathf.Max(0.01f, shardLifetime), Mathf.Clamp(spreadDegrees, 0f, 85f)));
    }

    private System.Collections.IEnumerator ClusterSplitRoutine(int shardCount, float shardDamage, float shardSpeed, float shardLifetime, float spreadDegrees)
    {
        clusterSequenceActive = true;
        // Capture baseline scale
        if (!scaleCaptured)
        {
            originalLocalScale = transform.localScale;
            scaleCaptured = true;
        }

        // Disable collider during staged animation to avoid accidental hits
        if (ownCollider != null) ownCollider.enabled = false;
        // Decelerate linearly to zero
        Vector2 initialVel = rigidBody != null ? rigidBody.linearVelocity : direction * speed;
        float initialSpeed = initialVel.magnitude;
        Vector2 moveDir = initialSpeed > 0.0001f ? initialVel.normalized : direction;
        float t = 0f;
        float minDecel = Mathf.Max(0f, clusterDecelMin);
        float maxDecel = Mathf.Max(minDecel, clusterDecelMax);
        float decelDur = Random.Range(minDecel, maxDecel);
        while (t < decelDur)
        {
            t += Time.deltaTime;
            float u = decelDur > 0f ? Mathf.Clamp01(t / decelDur) : 1f;
            float s = Mathf.Lerp(initialSpeed, 0f, u);
            if (rigidBody != null) rigidBody.linearVelocity = moveDir * s;
            yield return null;
        }
        if (rigidBody != null) rigidBody.linearVelocity = Vector2.zero;

        // Hold
        float hold = Mathf.Max(0f, clusterHoldDuration);
        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        // Squash
        float squashT = 0f;
        float squashDur = Mathf.Max(0f, clusterSquashTime);
        Vector3 squashScale = originalLocalScale * Mathf.Max(0.01f, clusterSquashScale);
        while (squashT < squashDur)
        {
            squashT += Time.deltaTime;
            float u = squashDur > 0f ? Mathf.Clamp01(squashT / squashDur) : 1f;
            transform.localScale = Vector3.Lerp(originalLocalScale, squashScale, u);
            yield return null;
        }
        transform.localScale = squashScale;

        // Pop expand and VFX
        if (SimpleVfxPool.Instance != null)
            SimpleVfxPool.Instance.PlayAt(transform.position);
        float popT = 0f;
        float popDur = Mathf.Max(0f, clusterPopTime);
        Vector3 popScale = originalLocalScale * Mathf.Max(0.01f, clusterPopScale);
        while (popT < popDur)
        {
            popT += Time.deltaTime;
            float u = popDur > 0f ? Mathf.Clamp01(popT / popDur) : 1f;
            transform.localScale = Vector3.Lerp(squashScale, popScale, u);
            yield return null;
        }
        transform.localScale = popScale;

        // Spawn shards
        ProjectilePool pool = ownerPool;
        if (pool != null)
        {
            Vector3 spawnPos = transform.position;
            Vector2 down = Vector2.down;
            float spread = spreadDegrees;
            System.Action<Vector2> spawnOne = (dir) =>
            {
                Vector3 posJitter = Vector3.zero;
                if (shardSpawnOffsetJitter > 0f)
                {
                    posJitter.x = Random.Range(-shardSpawnOffsetJitter, shardSpawnOffsetJitter);
                }
                var s = pool.Get();
                if (s != null)
                {
                    s.Launch(spawnPos + posJitter, dir, shardSpeed, shardLifetime, shardDamage, false);
                    s.isClusterShard = true;
                }
            };

            if (shardCount == 1)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float jitter = shardAngleJitter > 0f ? Random.Range(-shardAngleJitter, shardAngleJitter) : 0f;
                float ang = side * spread + jitter;
                Vector2 dir = Quaternion.Euler(0f, 0f, ang) * down;
                spawnOne(dir);
            }
            else if (shardCount == 2)
            {
                Vector2 left = Quaternion.Euler(0f, 0f, +spread) * down;
                Vector2 right = Quaternion.Euler(0f, 0f, -spread) * down;
                spawnOne(left);
                spawnOne(right);
            }
            else
            {
                for (int i = 0; i < shardCount; i++)
                {
                    float tt = shardCount == 1 ? 0f : (i / Mathf.Max(1f, (shardCount - 1f))) * 2f - 1f; // [-1..1]
                    float ang = tt * spread;
                    Vector2 dir = Quaternion.Euler(0f, 0f, ang) * down;
                    spawnOne(dir);
                }
            }
        }

        // Re-enable collider on original just before pooling (for future reuse)
        if (ownCollider != null) ownCollider.enabled = true;
        // Cleanup
        ReturnToPool();
        yield break;
    }
}


