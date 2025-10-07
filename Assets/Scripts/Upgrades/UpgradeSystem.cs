using UnityEngine;

public static class UpgradeSystem
{
    private static string LevelKey(string id) => string.IsNullOrEmpty(id) ? "" : ($"Upg_{id}_Level");

    public static int GetLevel(UpgradeDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return 0;
        return PlayerPrefs.GetInt(LevelKey(def.id), 0);
    }

    public static void SetLevel(UpgradeDefinition def, int level)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return;
        int capped = def.maxLevel > 0 ? Mathf.Clamp(level, 0, def.maxLevel) : Mathf.Max(0, level);
        PlayerPrefs.SetInt(LevelKey(def.id), capped);
        PlayerPrefs.Save();
    }

    public static int GetNextCost(UpgradeDefinition def)
    {
        if (def == null) return int.MaxValue;
        int nextLevel = GetLevel(def) + 1;
        if (def.maxLevel > 0 && nextLevel > def.maxLevel) return int.MaxValue;
        if (nextLevel <= 0) return 0;
        return Mathf.Max(0, def.baseCost + def.stepPerLevel * (nextLevel - 1));
    }

    public static bool MeetsPrerequisites(UpgradeDefinition def)
    {
        if (def == null) return false;
        // Gating by cleared wave index
        if (def.minClearedWaveIndex >= 0)
        {
            int cleared = PlayerPrefs.GetInt("Wave_Reached", 0);
            if (cleared < def.minClearedWaveIndex)
                return false;
        }
        if (def.prerequisites == null || def.prerequisites.Count == 0) return true;
        for (int i = 0; i < def.prerequisites.Count; i++)
        {
            var p = def.prerequisites[i];
            if (p.upgrade == null) continue;
            if (GetLevel(p.upgrade) < Mathf.Max(0, p.minLevel))
                return false;
        }
        return true;
    }

    public static bool IsMaxed(UpgradeDefinition def)
    {
        if (def == null) return true;
        if (def.maxLevel <= 0) return false;
        return GetLevel(def) >= def.maxLevel;
    }

    public static bool CanBuy(UpgradeDefinition def)
    {
        if (def == null) return false;
        if (!MeetsPrerequisites(def)) return false;
        if (IsMaxed(def)) return false;
        int cost = GetNextCost(def);
        return CurrencyStore.TotalCurrency >= cost;
    }

    public static bool TryBuy(UpgradeDefinition def)
    {
        if (!CanBuy(def)) return false;
        int cost = GetNextCost(def);
        if (!CurrencyStore.TrySpendFromTotal(cost)) return false;
        SetLevel(def, GetLevel(def) + 1);
        try
        {
            string name = def != null ? (string.IsNullOrEmpty(def.id) ? def.name : def.id) : "<null>";
            Debug.Log($"[UpgradeBuy] {name} -> level {GetLevel(def)} (spent {cost})");
        }
        catch { }
        return true;
    }
}

public static class UpgradeRuntimeApplier
{
    public static void ApplyToRun(System.Collections.Generic.IEnumerable<UpgradeDefinition> defs, AutoShooter shooter, RedLine red)
    {
        if (defs == null) return;
        int totalDamage = 0;
        float totalFire = 0f;
        float totalCrit = 0f;
        float totalCritMultAdd = 0f;
        int totalProjectilesAdd = 0;
        float totalRedDampen = 0f;
        float totalHeavyChance = 0f;
        float totalWallDampen = 0f;
        float totalOverflowCarry = 0f;
        bool overflowUncapped = false;
        bool sideCannons = false;
        int sideDamageBase = 0;
        int sideDamageAddOnly = 0;
        float totalSideFireAdd = 0f;
        bool sideCritsEnabled = false;
        float sideCritChanceAdd = 0f;
        float sideCritMultAdd = 0f;
        float sideBounceChanceAdd = 0f;
        // Helper drone
        bool helperEnabled = false;
        float helperDamageAdd = 0f;
        float helperFireAdd = 0f;
        // Explosive main
        float explosiveChanceAdd = 0f;
        float explosiveDamagePctAdd = 0f;
        // Horizontal side cannons
        bool sideHori = false;
        int sideHoriDamageBase = 0;
        int sideHoriDamageAddOnly = 0;
        float totalSideHoriFireAdd = 0f;
        bool sideHoriCritsEnabled = false;
        float sideHoriCritChanceAdd = 0f;
        float sideHoriCritMultAdd = 0f;
        // On-kill explosion
        bool killExplosionEnabled = false;
        float totalKillExplosionDamage = 0f;
        // Cluster
        bool clusterEnabled = false;
        int clusterCountAdd = 0;
        float clusterDamageAdd = 0f;
        float clusterSpeedAdd = 0f;
        float clusterLifetimeAdd = 0f;
        float clusterAngleAdd = 0f;
        int extraExplosionDepth = 0;
        // Grave bomb
        bool graveEnabled = false;
        float graveBaseDamage = 0f;
        float graveDamageAdd = 0f;
        int graveDepthAdd = 0;
        // Auto-Run availability (unlocked if any enabling upgrade has level > 0)
        bool autoRunAvailable = false;
        float passiveIncomeBaseAtLevelSum = 0f;
        int passiveBaseMultiplierLevels = 0;
        bool stageSelectorUnlocked = false;
        foreach (var def in defs)
        {
            if (def == null) continue;
            int lvl = UpgradeSystem.GetLevel(def);
            if (lvl <= 0) continue;
            try { Debug.Log($"[AutoRunApply] id={def.id} lvl={lvl} autoToggle={def.enablesAutoRunToggle}"); } catch { }
            if (def.damageAdd != 0) totalDamage += def.damageAdd * lvl;
            if (def.fireRateAdd != 0f) totalFire += def.fireRateAdd * lvl;
            if (def.critChanceAdd != 0f) totalCrit += def.critChanceAdd * lvl;
            if (def.critMultiplierAdd != 0f) totalCritMultAdd += def.critMultiplierAdd * lvl;
            if (def.projectilesAdd != 0) totalProjectilesAdd += def.projectilesAdd * lvl;
            if (def.redRiseDampen != 0f) totalRedDampen += def.redRiseDampen * lvl;
            if (def.heavySpawnChanceAdd != 0f) totalHeavyChance += def.heavySpawnChanceAdd * lvl;
            if (def.wallDescendDampen != 0f) totalWallDampen += def.wallDescendDampen * lvl;
            if (def.overflowCarryAdd != 0f) totalOverflowCarry += def.overflowCarryAdd * lvl;
            if (def.enablesOverflowUncapped && lvl > 0)
                overflowUncapped = true;
            if (def.enablesSideCannons && lvl > 0)
            {
                sideCannons = true;
                int candidate = Mathf.Max(0, def.sideCannonBaseDamage) + Mathf.Max(0, def.sideCannonDamageAdd) * (lvl - 1);
                sideDamageBase = Mathf.Max(sideDamageBase, candidate);
            }
            // Allow damage-only upgrades that don't toggle side cannons on
            if (!def.enablesSideCannons && def.sideCannonDamageAdd != 0)
            {
                sideDamageAddOnly += Mathf.Max(0, def.sideCannonDamageAdd) * lvl;
            }
            if (def.sideCannonFireRateAdd != 0f)
                totalSideFireAdd += def.sideCannonFireRateAdd * lvl;

            // Side crits
            if (def.enablesSideCrits && lvl > 0)
                sideCritsEnabled = true;
            if (def.sideCritChanceAdd != 0f)
                sideCritChanceAdd += def.sideCritChanceAdd * lvl;
            if (def.sideCritMultiplierAdd != 0f)
                sideCritMultAdd += def.sideCritMultiplierAdd * lvl;
            if (def.sideBounceChanceAdd != 0f)
                sideBounceChanceAdd += def.sideBounceChanceAdd * lvl;
            // Helper drone
            if (def.enablesHelperDrone && lvl > 0)
                helperEnabled = true;
            if (def.helperDroneDamageAdd != 0f)
                helperDamageAdd += def.helperDroneDamageAdd * lvl;
            if (def.helperDroneFireRateAdd != 0f)
                helperFireAdd += def.helperDroneFireRateAdd * lvl;
            // Explosive main
            if (def.explosiveMainChanceAdd != 0f)
                explosiveChanceAdd += def.explosiveMainChanceAdd * lvl;
            if (def.explosiveMainDamagePercentAdd != 0f)
                explosiveDamagePctAdd += def.explosiveMainDamagePercentAdd * lvl;

            // Horizontal side cannons
            if (def.enablesSideCannonsHorizontal && lvl > 0)
            {
                sideHori = true;
                int candH = Mathf.Max(0, def.horizSideCannonBaseDamage) + Mathf.Max(0, def.horizSideCannonDamageAdd) * (lvl - 1);
                sideHoriDamageBase = Mathf.Max(sideHoriDamageBase, candH);
            }
            if (!def.enablesSideCannonsHorizontal && def.horizSideCannonDamageAdd != 0)
            {
                sideHoriDamageAddOnly += Mathf.Max(0, def.horizSideCannonDamageAdd) * lvl;
            }
            if (def.horizSideCannonFireRateAdd != 0f)
                totalSideHoriFireAdd += def.horizSideCannonFireRateAdd * lvl;
            if (def.enablesSideCritsHorizontal && lvl > 0)
                sideHoriCritsEnabled = true;
            if (def.horizSideCritChanceAdd != 0f)
                sideHoriCritChanceAdd += def.horizSideCritChanceAdd * lvl;
            if (def.horizSideCritMultiplierAdd != 0f)
                sideHoriCritMultAdd += def.horizSideCritMultiplierAdd * lvl;

            // Cluster
            if (def.enablesPassThroughCluster && lvl > 0)
                clusterEnabled = true;
            if (def.clusterShardCountAdd != 0) clusterCountAdd += def.clusterShardCountAdd * lvl;
            if (def.clusterShardDamageAdd != 0f) clusterDamageAdd += def.clusterShardDamageAdd * lvl;
            if (def.clusterShardSpeedAdd != 0f) clusterSpeedAdd += def.clusterShardSpeedAdd * lvl;
            if (def.clusterShardLifetimeAdd != 0f) clusterLifetimeAdd += def.clusterShardLifetimeAdd * lvl;
            if (def.clusterAngleAdd != 0f) clusterAngleAdd += def.clusterAngleAdd * lvl;

            // On-kill explosion
            if (def.enablesKillExplosionSides && lvl > 0)
                killExplosionEnabled = true;
            if (def.killExplosionDamageAdd != 0f)
                totalKillExplosionDamage += def.killExplosionDamageAdd * lvl;
            if (def.killExplosionExtraDepthAdd != 0)
                extraExplosionDepth += Mathf.Max(0, def.killExplosionExtraDepthAdd) * lvl;
            // Grave bomb
            if (def.enablesGraveBomb && lvl > 0)
            {
                graveEnabled = true;
                graveBaseDamage = Mathf.Max(graveBaseDamage, Mathf.Max(0f, def.graveBombBaseDamage));
            }
            if (def.graveBombDamageAdd != 0f)
                graveDamageAdd += def.graveBombDamageAdd * lvl;
            if (def.graveBombDepthAdd != 0)
                graveDepthAdd += Mathf.Max(0, def.graveBombDepthAdd) * lvl;
            // Auto-Run toggle unlock
            if (def.enablesAutoRunToggle && lvl > 0)
                autoRunAvailable = true;
            // Stage selector unlock detection based on known id or dedicated field
            if (!string.IsNullOrEmpty(def.id) && def.id.Equals("stageselector", System.StringComparison.OrdinalIgnoreCase) && lvl > 0)
                stageSelectorUnlocked = true;
            // Passive income: compute base at current level (base + per-level*(lvl-1))
            if (def.passiveIncomeBasePerSecond > 0f && lvl > 0)
            {
                float baseAtLevel = def.passiveIncomeBasePerSecond;
                if (lvl > 1 && def.passiveIncomePerLevel > 0f)
                    baseAtLevel += def.passiveIncomePerLevel * (lvl - 1);
                passiveIncomeBaseAtLevelSum += baseAtLevel;
            }
            if (def.enablesPassiveBaseMultiplier && lvl > 0)
            {
                passiveBaseMultiplierLevels += lvl;
            }
        }

        if (shooter != null)
        {
            shooter.projectileDamage += Mathf.Max(0, totalDamage);
            if (totalFire != 0f) shooter.SetFireRate(shooter.fireRate + totalFire);
            if (totalCrit != 0f) shooter.critChance += totalCrit;
            if (totalCritMultAdd != 0f) shooter.critMultiplier = Mathf.Max(1f, shooter.critMultiplier + totalCritMultAdd);
            if (totalSideFireAdd != 0f) shooter.SetSideFireRate(shooter.sideFireRate + totalSideFireAdd);
        }
        if (red != null && totalRedDampen != 0f)
        {
            red.riseSpeed *= Mathf.Max(0f, 1f - totalRedDampen);
        }

        // Persist heavy chance for this run
        RunModifiers.HeavySpawnChance = Mathf.Clamp01(totalHeavyChance);
        RunModifiers.WallDescendMultiplier = Mathf.Max(0f, 1f - Mathf.Max(0f, totalWallDampen));
        RunModifiers.ExtraProjectiles = Mathf.Max(0, totalProjectilesAdd);
        RunModifiers.OverflowCarryPercent = overflowUncapped ? Mathf.Max(0f, totalOverflowCarry) : Mathf.Clamp01(totalOverflowCarry);
        RunModifiers.SideCannonsEnabled = sideCannons;
        RunModifiers.SideCannonDamage = sideCannons ? Mathf.Max(0, sideDamageBase + sideDamageAddOnly) : 0;
        RunModifiers.SideCritsEnabled = sideCritsEnabled;
        RunModifiers.SideCritChance = Mathf.Max(0f, sideCritChanceAdd);
        RunModifiers.SideCritMultiplier = Mathf.Max(1f, 1f + sideCritMultAdd);
        RunModifiers.SideBounceChance = Mathf.Clamp01(sideBounceChanceAdd);
        // Helper drone
        RunModifiers.HelperDroneEnabled = helperEnabled;
        RunModifiers.HelperDroneDamageAdd = Mathf.Max(0f, helperDamageAdd);
        RunModifiers.HelperDroneFireRateAdd = Mathf.Max(0f, helperFireAdd);
        // Explosive main
        RunModifiers.ExplosiveMainChance = Mathf.Clamp01(explosiveChanceAdd);
        RunModifiers.ExplosiveMainDamagePercent = Mathf.Max(0f, 0.10f + explosiveDamagePctAdd);
        // Horizontal
        RunModifiers.SideCannonsHorizontalEnabled = sideHori;
        RunModifiers.SideHoriDamage = sideHori ? Mathf.Max(0, sideHoriDamageBase + sideHoriDamageAddOnly) : 0;
        RunModifiers.SideHoriFireRate = Mathf.Max(0f, totalSideHoriFireAdd);
        RunModifiers.SideHoriCritsEnabled = sideHoriCritsEnabled;
        RunModifiers.SideHoriCritChance = Mathf.Max(0f, sideHoriCritChanceAdd);
        RunModifiers.SideHoriCritMultiplier = Mathf.Max(1f, 1f + sideHoriCritMultAdd);

        // Apply cluster settings (defaults: 2 shards, dmg=1, speed=some fraction of main?)
        RunModifiers.PassThroughClusterEnabled = clusterEnabled;
        RunModifiers.ClusterShardCount = Mathf.Max(0, clusterCountAdd);
        RunModifiers.ClusterShardDamage = Mathf.Max(0f, clusterDamageAdd);
        RunModifiers.ClusterShardSpeed = Mathf.Max(0f, clusterSpeedAdd);
        RunModifiers.ClusterShardLifetime = Mathf.Max(0f, clusterLifetimeAdd);
        // Set cluster angle fresh each apply to avoid compounding across multiple applies
        RunModifiers.ClusterAngleDegrees = Mathf.Max(0f, clusterAngleAdd);

        // On-kill explosion
        RunModifiers.OnKillExplosionEnabled = killExplosionEnabled;
        RunModifiers.OnKillExplosionDamage = Mathf.Max(0f, totalKillExplosionDamage);
        RunModifiers.OnKillExplosionExtraDepth = Mathf.Max(0, extraExplosionDepth);

        // Apply grave bomb
        RunModifiers.GraveBombEnabled = graveEnabled;
        if (graveEnabled)
        {
            RunModifiers.GraveBombDamage = Mathf.Max(0f, graveBaseDamage + graveDamageAdd);
            RunModifiers.GraveBombDepth = Mathf.Max(1, 1 + graveDepthAdd);
        }
        // Apply Auto-Run unlocked status after evaluating all defs
        RunModifiers.AutoRunUnlocked = autoRunAvailable;
        // Apply passive income rate: (sum base at current level) * (1 + levels)
        float passiveMult = 1f + Mathf.Max(0, passiveBaseMultiplierLevels);
        float passiveTotal = Mathf.Max(0f, passiveIncomeBaseAtLevelSum) * passiveMult;
        RunModifiers.PassiveIncomePerSecond = Mathf.Max(0f, passiveTotal);
        // Stage selector
        RunModifiers.StageSelectorUnlocked = stageSelectorUnlocked;
        try { Debug.Log($"[PassiveIncomeApply] rate={RunModifiers.PassiveIncomePerSecond:0.##}/s"); } catch { }
    }
}


