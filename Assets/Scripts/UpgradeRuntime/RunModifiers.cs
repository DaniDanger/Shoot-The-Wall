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

    // Horizontal side cannons (fire straight left/right)
    public static bool SideCannonsHorizontalEnabled = false;
    public static int SideHoriDamage = 0;
    public static float SideHoriFireRate = 0f; // 0 = fallback to main fire rate
    public static bool SideHoriCritsEnabled = false;
    public static float SideHoriCritChance = 0f;
    public static float SideHoriCritMultiplier = 1f;

    // Grave bomb (spawn a bomb brick next run at death location)
    public static bool GraveBombEnabled = false;
    public static float GraveBombDamage = 1f;
    public static int GraveBombDepth = 1; // 1 = 8-neighbor ring
}


