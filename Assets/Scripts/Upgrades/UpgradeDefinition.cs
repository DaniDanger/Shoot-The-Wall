using System;
using System.Collections.Generic;
using UnityEngine;

public enum LayoutDirection8
{
    None,
    Up,
    Down,
    Left,
    Right,
    UpLeft,
    UpRight,
    DownLeft,
    DownRight
}

[CreateAssetMenu(menuName = "Upgrades/Upgrade Definition", fileName = "UpgradeDefinition")]
public class UpgradeDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id;              // used for save keys
    public int index = 0;          // reveal/order hint

    [Header("Display")]
    public string displayName;
    public Sprite icon;

    [Header("Progression")]
    public int maxLevel = 0;       // 0 or less means uncapped
    public int baseCost = 5;
    public int stepPerLevel = 10;

    [Header("Per-Level Stat Adds (data-driven)")]
    [Tooltip("Flat damage added per level.")]
    public int damageAdd = 0;
    [Tooltip("Fire rate (shots/sec) added per level.")]
    public float fireRateAdd = 0f;
    [Tooltip("Crit chance added per level (0..1).")]
    public float critChanceAdd = 0f;
    [Tooltip("Crit damage multiplier added per level (e.g., 0.2 = +0.2x per level).")]
    public float critMultiplierAdd = 0f;
    [Tooltip("Red line dampening per level (0..1), 0.01 = -1% rise speed.")]
    public float redRiseDampen = 0f;

    [Tooltip("Additional parallel projectiles per level (e.g., 1 = +1 projectile).")]
    public int projectilesAdd = 0;

    [Tooltip("Heavy spawn chance added per level (0..1). Example: 0.1 = +10% per level.")]
    public float heavySpawnChanceAdd = 0f;

    [Tooltip("Wall descend speed dampening per level (0..1), 0.02 = -2% descend speed.")]
    public float wallDescendDampen = 0f;

    [Tooltip("Portion of overkill damage carried to the next brick per level (0..1). Example: 0.1 = +10% per level.")]
    public float overflowCarryAdd = 0f;

    [Header("Overflow")]
    [Tooltip("If true, this upgrade removes the 1.0 cap from overflow carry when level > 0.")]
    public bool enablesOverflowUncapped = false;

    [Header("Shard Amplifier")]
    [Tooltip("Shard gain percent added per level (e.g., 0.01 = +1% per level). Affects only shard payouts.")]
    public float shardGainPercentAdd = 0f;

    [Header("Side Cannons")]
    [Tooltip("If true, this upgrade enables side cannons when level > 0.")]
    public bool enablesSideCannons = false;
    [Tooltip("Base damage per side shot when enabled. Used once if level > 0.")]
    public int sideCannonBaseDamage = 0;
    [Tooltip("Additional damage per level for side cannons.")]
    public int sideCannonDamageAdd = 0;
    [Tooltip("Side cannon fire rate (shots/sec) added per level.")]
    public float sideCannonFireRateAdd = 0f;
    [Tooltip("Enable crit system on side cannons when level > 0 (chance/mult fields below apply only if enabled).")]
    public bool enablesSideCrits = false;
    [Tooltip("Crit chance added per level for side cannons (0..1).")]
    public float sideCritChanceAdd = 0f;
    [Tooltip("Crit multiplier added per level for side cannons (e.g., 0.2 = +0.2x per level).")]
    public float sideCritMultiplierAdd = 0f;

    [Header("Side Cannons (Horizontal)")]
    [Tooltip("If true, this upgrade enables horizontal side cannons (left/right) when level > 0.")]
    public bool enablesSideCannonsHorizontal = false;
    [Tooltip("Base damage per horizontal side shot when enabled. Used once if level > 0.")]
    public int horizSideCannonBaseDamage = 0;
    [Tooltip("Additional damage per level for horizontal side cannons.")]
    public int horizSideCannonDamageAdd = 0;
    [Tooltip("Horizontal side cannon fire rate (shots/sec) added per level.")]
    public float horizSideCannonFireRateAdd = 0f;
    [Tooltip("Enable crit system on horizontal side cannons when level > 0.")]
    public bool enablesSideCritsHorizontal = false;
    [Tooltip("Crit chance added per level for horizontal side cannons (0..1).")]
    public float horizSideCritChanceAdd = 0f;
    [Tooltip("Crit multiplier added per level for horizontal side cannons (e.g., 0.2 = +0.2x per level).")]
    public float horizSideCritMultiplierAdd = 0f;

    [Header("Pass-through Cluster (Main Cannon)")]
    [Tooltip("Enables cluster shards when projectiles pass the top pass-through zone (main cannon only). Level > 0 enables.")]
    public bool enablesPassThroughCluster = false;
    [Tooltip("Additional shard count per level (base is 2 when enabled).")]
    public int clusterShardCountAdd = 0;
    [Tooltip("Shard damage added per level (absolute, not %).")]
    public float clusterShardDamageAdd = 0f;
    [Tooltip("Shard speed added per level (units/sec).")]
    public float clusterShardSpeedAdd = 0f;
    [Tooltip("Shard lifetime added per level (seconds).")]
    public float clusterShardLifetimeAdd = 0f;
    [Tooltip("Shard angle spread added per level (degrees, half-spread).")]
    public float clusterAngleAdd = 0f;

    [Header("On-Kill Explosion")]
    [Tooltip("If true, killing a brick deals damage to its immediate left/right neighbors.")]
    public bool enablesKillExplosionSides = false;
    [Tooltip("Damage applied to each neighbor when the kill explosion triggers, per level.")]
    public float killExplosionDamageAdd = 0f;
    [Tooltip("Additional outward explosion depth (rings). Each depth step is 50% of previous.")]
    public int killExplosionExtraDepthAdd = 0;

    [Header("Grave Bomb")]
    [Tooltip("If true, enables grave bomb on next run after death when level > 0.")]
    public bool enablesGraveBomb = false;
    [Tooltip("Base damage of grave bomb when enabled (used once if level > 0).")]
    public float graveBombBaseDamage = 40f;
    [Tooltip("Additional grave bomb damage per level.")]
    public float graveBombDamageAdd = 0f;
    [Tooltip("Additional grave bomb depth (rings) per level. Depth 1 hits 8 neighbors.")]
    public int graveBombDepthAdd = 0;

    [Header("UI")]
    [Tooltip("Optional override. If empty, tooltip text is generated from non-zero stat fields.")]
    [TextArea] public string effectTextOverride;

    [Header("Stage Clear Bonus (Optional)")]
    [Tooltip("If > 0, level 1 grants this base stage clear bonus.")]
    public int stageClearBonusBase = 0;
    [Tooltip("If > 0, each level beyond 1 adds this amount to the stage clear bonus.")]
    public int stageClearBonusPerLevel = 0;

    [Header("Passive Income")]
    [Tooltip("Base passive currency per second granted when level > 0 (applied once).")]
    public float passiveIncomeBasePerSecond = 0f;
    [Tooltip("Additional passive income per second added per level beyond level 1.")]
    public float passiveIncomePerLevel = 0f;
    [Tooltip("If true, each level adds +1x to multiply the summed passive base per second.")]
    public bool enablesPassiveBaseMultiplier = false;

    [Header("Meta / QoL")]
    [Tooltip("If true, unlocking this enables the Auto-Run toggle (movement auto-hold up and auto-retry).")]
    public bool enablesAutoRunToggle = false;

    [Header("Layout")]
    [Tooltip("Optional override: anchor this node relative to this upgrade instead of the first prerequisite.")]
    public UpgradeDefinition layoutAnchor;
    [Tooltip("Placement relative to the anchor node (layoutAnchor or first prerequisite). If None, falls back to index-based layout.")]
    public LayoutDirection8 layoutDir = LayoutDirection8.None;
    [Tooltip("Extra pixel offset applied after directional placement.")]
    public Vector2 layoutOffset = Vector2.zero;

    [Header("Gating")]
    [Tooltip("Minimum cleared wave index (0-based) required for this upgrade to appear. -1 disables gating.")]
    public int minClearedWaveIndex = -1;

    [Serializable]
    public struct Prereq
    {
        public UpgradeDefinition upgrade;
        public int minLevel;
    }

    [Header("Prerequisites")]
    public List<Prereq> prerequisites = new List<Prereq>();
}


