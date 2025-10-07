using UnityEngine;

public static class RunModifiers
{
    // Reset per run in GameManager before applying upgrades if needed later.
    public static float HeavySpawnChance = 0f;
    public static float WallDescendMultiplier = 1f;
    public static int ExtraProjectiles = 0;
    public static float OverflowCarryPercent = 0f; // 0..1
    public static bool SideCannonsEnabled = false;
    public static int SideCannonDamage = 0;

    // Pass-through cluster shards (projectile splits when crossing pass zone)
    public static bool PassThroughClusterEnabled = false;
    public static int ClusterShardCount = 0; // 0 = use default
    public static float ClusterShardDamage = 0f; // 0 = use default
    public static float ClusterShardSpeed = 0f; // 0 = use default
    public static float ClusterShardLifetime = 0f; // 0 = use default
    public static float ClusterAngleDegrees = 15f; // half-spread from straight down

    // On-kill explosion (neighbors left/right)
    public static bool OnKillExplosionEnabled = false;
    public static float OnKillExplosionDamage = 0f;
    public static int OnKillExplosionExtraDepth = 0; // number of extra rings (each 50% damage)

    // Side cannon crits (scoped to current side cannons only)
    public static bool SideCritsEnabled = false;
    public static float SideCritChance = 0f;
    public static float SideCritMultiplier = 1f;

    // Side cannon bounce (single ricochet chance 0..1)
    public static float SideBounceChance = 0f;

    // Grave bomb (spawn a bomb brick next run at death location)
    public static bool GraveBombEnabled = false;
    public static float GraveBombDamage = 1f;
    public static int GraveBombDepth = 1; // 1 = 8-neighbor ring

    // Horizontal side cannons (fire straight left/right)
    public static bool SideCannonsHorizontalEnabled = false;
    public static int SideHoriDamage = 0;
    public static float SideHoriFireRate = 0f; // 0 = fallback to main fire rate
    public static bool SideHoriCritsEnabled = false;
    public static float SideHoriCritChance = 0f;
    public static float SideHoriCritMultiplier = 1f;

    // Auto Run (Auto-Pilot)
    public static bool AutoRunUnlocked = false;

    private const string AutoRunEnabledKey = "AutoRunEnabled";
    public static bool AutoRunEnabled
    {
        get { return PlayerPrefs.GetInt(AutoRunEnabledKey, 0) == 1; }
        set { PlayerPrefs.SetInt(AutoRunEnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Passive income (currency per second during runs)
    public static float PassiveIncomePerSecond = 0f;

    // Shard amplifier (per-run)
    public static float ShardGainPercent = 0f;
    public static float ShardGainCarry = 0f;

    // Stage selector + Re-run on clear
    public static bool StageSelectorUnlocked = false;

    private const string ReRunOnClearKey = "ReRunOnClearEnabled";
    public static bool ReRunOnClearEnabled
    {
        get { return PlayerPrefs.GetInt(ReRunOnClearKey, 0) == 1; }
        set { PlayerPrefs.SetInt(ReRunOnClearKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Explosive shots (main cannon only)
    public static float ExplosiveMainChance = 0f;           // 0..1
    public static float ExplosiveMainDamagePercent = 0.10f; // fraction of projectile dmg

    // Helper Drone
    public static bool HelperDroneEnabled = false;
    public static float HelperDroneDamageAdd = 0f;
    public static float HelperDroneFireRateAdd = 0f;
}


