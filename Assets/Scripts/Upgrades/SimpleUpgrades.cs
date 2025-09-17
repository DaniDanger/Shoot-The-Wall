using UnityEngine;

public static class SimpleUpgrades
{
    private const string DamageLevelKey = "Upg_Damage_Level";
    private const string FireLevelKey = "Upg_Fire_Level";
    private const string RedLevelKey = "Upg_Red_Level";
    private const string CritLevelKey = "Upg_Crit_Level";
    private static int damageBaseCost = 5;
    private static int fireBaseCost = 5;
    private static int redBaseCost = 10;
    private static int critBaseCost = 10;
    private const int repeatablePriceStep = 10;

    public static int DamageLevel
    {
        get { return PlayerPrefs.GetInt(DamageLevelKey, 0); }
        private set { PlayerPrefs.SetInt(DamageLevelKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public static void SetDamageBaseCost(int cost)
    {
        damageBaseCost = Mathf.Max(0, cost);
    }

    public static int GetDamageCost(int nextLevel)
    {
        // MVP: flat base cost for first node; curve will be decided later.
        if (nextLevel <= 0) return 0;
        // Cost increases +10 per level (base + step*(nextLevel-1))
        return Mathf.Max(0, damageBaseCost + repeatablePriceStep * (nextLevel - 1));
    }

    public static int FireLevel
    {
        get { return PlayerPrefs.GetInt(FireLevelKey, 0); }
        private set { PlayerPrefs.SetInt(FireLevelKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public static void SetFireBaseCost(int cost)
    {
        fireBaseCost = Mathf.Max(0, cost);
    }

    public static int GetFireCost(int nextLevel)
    {
        if (nextLevel <= 0) return 0;
        return Mathf.Max(0, fireBaseCost + repeatablePriceStep * (nextLevel - 1));
    }

    public static int RedLevel
    {
        get { return PlayerPrefs.GetInt(RedLevelKey, 0); }
        private set { PlayerPrefs.SetInt(RedLevelKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public static void SetRedBaseCost(int cost)
    {
        redBaseCost = Mathf.Max(0, cost);
    }

    public static int GetRedCost(int nextLevel)
    {
        if (nextLevel <= 0) return 0;
        return Mathf.Max(0, redBaseCost + repeatablePriceStep * (nextLevel - 1));
    }

    public static int CritLevel
    {
        get { return PlayerPrefs.GetInt(CritLevelKey, 0); }
        private set { PlayerPrefs.SetInt(CritLevelKey, Mathf.Clamp(value, 0, 10)); PlayerPrefs.Save(); }
    }

    public static void SetCritBaseCost(int cost)
    {
        critBaseCost = Mathf.Max(0, cost);
    }

    public static int GetCritCost(int nextLevel)
    {
        if (nextLevel <= 0) return 0;
        return Mathf.Max(0, critBaseCost + repeatablePriceStep * (nextLevel - 1));
    }

    public static bool TryBuyDamage()
    {
        int nextLevel = DamageLevel + 1;
        int cost = GetDamageCost(nextLevel);
        if (CurrencyStore.TotalCurrency < cost)
            return false;

        int newTotal = CurrencyStore.TotalCurrency - cost;
        PlayerPrefs.SetInt("Currency_Total", newTotal);
        PlayerPrefs.Save();

        DamageLevel = nextLevel;
        return true;
    }

    public static bool TryBuyFire()
    {
        int nextLevel = FireLevel + 1;
        int cost = GetFireCost(nextLevel);
        if (CurrencyStore.TotalCurrency < cost)
            return false;

        int newTotal = CurrencyStore.TotalCurrency - cost;
        PlayerPrefs.SetInt("Currency_Total", newTotal);
        PlayerPrefs.Save();

        FireLevel = nextLevel;
        return true;
    }

    public static bool TryBuyRed()
    {
        int nextLevel = RedLevel + 1;
        int cost = GetRedCost(nextLevel);
        if (CurrencyStore.TotalCurrency < cost)
            return false;

        int newTotal = CurrencyStore.TotalCurrency - cost;
        PlayerPrefs.SetInt("Currency_Total", newTotal);
        PlayerPrefs.Save();

        RedLevel = nextLevel;
        return true;
    }

    public static bool TryBuyCrit()
    {
        if (CritLevel >= 10) return false; // cap 10
        int nextLevel = CritLevel + 1;
        int cost = GetCritCost(nextLevel);
        if (CurrencyStore.TotalCurrency < cost)
            return false;

        int newTotal = CurrencyStore.TotalCurrency - cost;
        PlayerPrefs.SetInt("Currency_Total", newTotal);
        PlayerPrefs.Save();

        CritLevel = nextLevel;
        return true;
    }

    public static void ApplyToRun(AutoShooter shooter, RedLine red)
    {
        if (shooter == null) return;
        // Damage
        shooter.projectileDamage += Mathf.Max(0, DamageLevel);
        // Fire rate: +0.25 per level
        float fireBonus = 0.25f * Mathf.Max(0, FireLevel);
        shooter.SetFireRate(shooter.fireRate + fireBonus);
        // Crit chance: +2% per level, capped by purchase limit (10)
        shooter.critChance += 0.02f * Mathf.Clamp(CritLevel, 0, 10);

        if (red != null)
        {
            // 1% dampening per level (multiplicative). Equivalent to riseSpeed *= pow(0.99, RedLevel)
            int lvl = Mathf.Max(0, RedLevel);
            if (lvl > 0)
            {
                float factor = Mathf.Pow(0.99f, lvl);
                red.riseSpeed *= factor;
            }
        }
    }
}


