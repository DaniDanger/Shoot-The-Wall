using UnityEngine;

[DisallowMultipleComponent]
public class AutoShooter : MonoBehaviour
{
    public float fireRate = 3f;

    public float projectileSpeed = 14f;

    public float projectileLifetime = 2.5f;

    public float projectileDamage = 1f;

    [Tooltip("Spawn offset from the shooter position (world units).")]
    public Vector2 spawnOffset = new Vector2(0f, 0.4f);

    public ProjectilePool projectilePool;
    [Header("Side Cannons")]
    public Transform sideLeftMuzzle;
    public Transform sideRightMuzzle;
    public float sideAngleDegrees = 25f;
    public float sideFireRate = 1f;
    [Header("Side Cannons (Horizontal)")]
    [Tooltip("Base fire rate for horizontal side cannons (shots/sec) before upgrades.")]
    public float sideHoriBaseFireRate = 1f;
    private float nextSideFireTime;
    private float nextSideHoriFireTime;
    private float baseSideFireRate;
    public float offsetJitter = 0.03f;

    private float nextFireTime;
    private bool shootingEnabled = true;
    private float baseProjectileDamage;
    private float baseFireRate;
    private float baseProjectileSpeed;
    public float critChance = 0f;
    public float critMultiplier = 2f;
    private float baseCritChance;
    private float baseCritMultiplier;
    private ScaleJiggle scaleJiggle;
    private AudioManager audioManager;

    private void Reset()
    {
        fireRate = baseFireRate;
        projectileSpeed = baseProjectileSpeed;
        projectileLifetime = baseProjectileSpeed;
        spawnOffset = new Vector2(0f, 0.4f);
    }

    private void Update()
    {
        if (!shootingEnabled)
            return;

        if (projectilePool == null || fireRate <= 0f)
            return;

        if (Time.time >= nextFireTime)
        {
            FireOnce();
            nextFireTime = Time.time + 1f / fireRate;
        }

        // Independent side cannon cadence (if enabled)
        if (RunModifiers.SideCannonsEnabled && projectilePool != null)
        {
            float cadence = sideFireRate > 0f ? sideFireRate : fireRate;
            if (cadence > 0f && Time.time >= nextSideFireTime)
            {
                FireSideVolley();
                nextSideFireTime = Time.time + 1f / cadence;
            }
        }
        // Independent horizontal side cannons cadence
        if (RunModifiers.SideCannonsHorizontalEnabled && projectilePool != null)
        {
            float add = Mathf.Max(0f, RunModifiers.SideHoriFireRate);
            float rate = Mathf.Max(0f, sideHoriBaseFireRate + add);
            if (rate > 0f && Time.time >= nextSideHoriFireTime)
            {
                FireSideHoriVolley();
                nextSideHoriFireTime = Time.time + 1f / rate;
            }
        }
    }

    public void SetFireRate(float newRate)
    {
        fireRate = Mathf.Max(0f, newRate);
    }

    public void SetShootingEnabled(bool enabled)
    {
        shootingEnabled = enabled;
        if (!enabled)
        {
            // Reset timer so we don't instantly fire on resume
            nextFireTime = Time.time + 1f / Mathf.Max(0.0001f, fireRate);
            float cadence = sideFireRate > 0f ? sideFireRate : fireRate;
            nextSideFireTime = Time.time + 1f / Mathf.Max(0.0001f, cadence);
            float add = Mathf.Max(0f, RunModifiers.SideHoriFireRate);
            float rate = Mathf.Max(0f, sideHoriBaseFireRate + add);
            nextSideHoriFireTime = Time.time + 1f / Mathf.Max(0.0001f, rate);
        }
    }

    public void SetSideFireRate(float newRate)
    {
        sideFireRate = Mathf.Max(0f, newRate);
    }

    private void FireOnce()
    {
        int totalProjectiles = 1 + Mathf.Max(0, RunModifiers.ExtraProjectiles);
        if (totalProjectiles <= 0 || projectilePool == null)
            return;

        Vector2 dir = Vector2.up;
        Vector3 basePos = transform.position + (Vector3)spawnOffset;
        float spacing = 0.18f;

        for (int i = 0; i < totalProjectiles; i++)
        {
            // Compute symmetric offsets: for N=2 => [-0.5, +0.5]; for N=3 => [-1,0,+1], etc.
            float index = i - (totalProjectiles - 1) * 0.5f;
            Vector2 perp = new Vector2(-dir.y, dir.x); // rotate 90 degrees
            float jitter = offsetJitter > 0f ? Random.Range(-offsetJitter, offsetJitter) : 0f;
            Vector3 offset = (Vector3)(perp.normalized * (index * spacing + jitter));

            Projectile projectile = projectilePool.Get();
            if (projectile == null)
                continue;

            float dmg = projectileDamage;
            bool crit = false;
            if (Random.value < Mathf.Clamp01(critChance))
            {
                crit = true;
                dmg = Mathf.Max(0f, dmg * Mathf.Max(1f, critMultiplier));
            }
            projectile.Launch(basePos + offset, dir, projectileSpeed, projectileLifetime, dmg, crit);
            // Configure explosive main chance (per projectile roll)
            projectile.ConfigureExplosive(RunModifiers.ExplosiveMainChance, RunModifiers.ExplosiveMainDamagePercent);
        }

        // Side cannons fire on their own cadence in Update via FireSideVolley()
        // Play shoot SFX once per volley via AudioManager (if present)
        if (audioManager != null)
            audioManager.PlaySfx(AudioManager.SfxId.Shoot, 1f, 0.04f);
        // Subtle recoil pulse on firing
        if (scaleJiggle != null)
            scaleJiggle.Play();
    }

    private void FireSideVolley()
    {
        if (!RunModifiers.SideCannonsEnabled || projectilePool == null)
            return;

        Vector2 dir = Vector2.up;
        Vector3 basePos = transform.position + (Vector3)spawnOffset;
        float sideDmg = Mathf.Max(0f, RunModifiers.SideCannonDamage > 0 ? RunModifiers.SideCannonDamage : 1);
        float angle = Mathf.Clamp(sideAngleDegrees, 0f, 80f);

        Vector3 leftPos = sideLeftMuzzle != null ? sideLeftMuzzle.position : (basePos + new Vector3(-0.4f, 0f, 0f));
        Vector2 leftDir = Quaternion.Euler(0f, 0f, angle) * dir;
        var pL = projectilePool.Get();
        if (pL != null)
        {
            float dmgL = sideDmg;
            bool critL = false;
            if (RunModifiers.SideCritsEnabled && Random.value < Mathf.Clamp01(RunModifiers.SideCritChance))
            {
                critL = true;
                dmgL = Mathf.Max(0f, dmgL * Mathf.Max(1f, RunModifiers.SideCritMultiplier));
            }
            pL.Launch(leftPos, leftDir, projectileSpeed, projectileLifetime, dmgL, critL);
            // Enable single-bounce ricochet on angled side cannons only
            pL.ConfigureRicochet(true, RunModifiers.SideBounceChance);
        }

        Vector3 rightPos = sideRightMuzzle != null ? sideRightMuzzle.position : (basePos + new Vector3(0.4f, 0f, 0f));
        Vector2 rightDir = Quaternion.Euler(0f, 0f, -angle) * dir;
        var pR = projectilePool.Get();
        if (pR != null)
        {
            float dmgR = sideDmg;
            bool critR = false;
            if (RunModifiers.SideCritsEnabled && Random.value < Mathf.Clamp01(RunModifiers.SideCritChance))
            {
                critR = true;
                dmgR = Mathf.Max(0f, dmgR * Mathf.Max(1f, RunModifiers.SideCritMultiplier));
            }
            pR.Launch(rightPos, rightDir, projectileSpeed, projectileLifetime, dmgR, critR);
            // Enable single-bounce ricochet on angled side cannons only
            pR.ConfigureRicochet(true, RunModifiers.SideBounceChance);
        }

        // Play side shoot SFX once per side volley
        if (audioManager != null)
            audioManager.PlaySfx(AudioManager.SfxId.SideShoot, 1f, 0.02f);
    }

    private void FireSideHoriVolley()
    {
        if (!RunModifiers.SideCannonsHorizontalEnabled || projectilePool == null)
            return;

        Vector2 dir = Vector2.up; // not used for left/right rotation
        Vector3 basePos = transform.position + (Vector3)spawnOffset;
        float sideDmg = Mathf.Max(0f, RunModifiers.SideHoriDamage > 0 ? RunModifiers.SideHoriDamage : 1);

        Vector3 leftPos = sideLeftMuzzle != null ? sideLeftMuzzle.position : (basePos + new Vector3(-0.4f, 0f, 0f));
        Vector2 leftDir = Vector2.left;
        var pL = projectilePool.Get();
        if (pL != null)
        {
            float dmgL = sideDmg;
            bool critL = false;
            if (RunModifiers.SideHoriCritsEnabled && Random.value < Mathf.Clamp01(RunModifiers.SideHoriCritChance))
            {
                critL = true;
                dmgL = Mathf.Max(0f, dmgL * Mathf.Max(1f, RunModifiers.SideHoriCritMultiplier));
            }
            pL.Launch(leftPos, leftDir, projectileSpeed, projectileLifetime, dmgL, critL);
        }

        Vector3 rightPos = sideRightMuzzle != null ? sideRightMuzzle.position : (basePos + new Vector3(0.4f, 0f, 0f));
        Vector2 rightDir = Vector2.right;
        var pR = projectilePool.Get();
        if (pR != null)
        {
            float dmgR = sideDmg;
            bool critR = false;
            if (RunModifiers.SideHoriCritsEnabled && Random.value < Mathf.Clamp01(RunModifiers.SideHoriCritChance))
            {
                critR = true;
                dmgR = Mathf.Max(0f, dmgR * Mathf.Max(1f, RunModifiers.SideHoriCritMultiplier));
            }
            pR.Launch(rightPos, rightDir, projectileSpeed, projectileLifetime, dmgR, critR);
        }

        if (audioManager != null)
            audioManager.PlaySfx(AudioManager.SfxId.SideShoot, 1f, 0.02f);
    }

    private void Awake()
    {
        baseProjectileDamage = Mathf.Max(1, projectileDamage);
        baseFireRate = Mathf.Max(0f, fireRate);
        baseSideFireRate = Mathf.Max(0f, sideFireRate);
        baseProjectileSpeed = Mathf.Max(0f, projectileSpeed);
        baseCritChance = Mathf.Clamp01(critChance);
        baseCritMultiplier = Mathf.Max(1f, critMultiplier);
        scaleJiggle = GetComponent<ScaleJiggle>();
        if (scaleJiggle == null)
            scaleJiggle = GetComponentInChildren<ScaleJiggle>();
        audioManager = Object.FindFirstObjectByType<AudioManager>();
    }

    public void ResetDamageToBase()
    {
        projectileDamage = Mathf.Max(1, baseProjectileDamage);
    }

    public void ResetFireRateToBase()
    {
        fireRate = Mathf.Max(0f, baseFireRate);
    }

    public void ResetSideFireRateToBase()
    {
        sideFireRate = Mathf.Max(0f, baseSideFireRate);
    }

    public void ResetCritToBase()
    {
        critChance = Mathf.Clamp01(baseCritChance);
        critMultiplier = Mathf.Max(1f, baseCritMultiplier);
    }

    // Recoil animation handled by ScaleJiggle component on the ship visual
}


