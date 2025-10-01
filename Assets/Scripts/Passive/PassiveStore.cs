using UnityEngine;

public static class PassiveStore
{
    private const string Wall1CurrencyKey = "Passive_Wall1_Currency";
    private const string Wall1ProgressKey = "Passive_Wall1_Progress";
    private const string Wall1CompletionsKey = "Passive_Wall1_Completions";
    private const string Wall1IncomeLevelKey = "Passive_Wall1_Income_Level";
    private const string Wall1IncomeProgressKey = "Passive_Wall1_Income_Progress";
    private const string Wall1AmplifierBonusKey = "Passive_Wall1_Amplifier_Bonus";
    private const string Wall1AmplifierCarryKey = "Passive_Wall1_Amplifier_Carry";

    public static int GetWall1Currency()
    {
        return PlayerPrefs.GetInt(Wall1CurrencyKey, 0);
    }

    public static void AddWall1Currency(int amount)
    {
        if (amount <= 0) return;
        int cur = GetWall1Currency();
        PlayerPrefs.SetInt(Wall1CurrencyKey, cur + amount);
        PlayerPrefs.Save();
    }

    public static bool TrySpendWall1Currency(int amount)
    {
        if (amount <= 0) return true;
        int cur = GetWall1Currency();
        if (cur < amount) return false;
        PlayerPrefs.SetInt(Wall1CurrencyKey, cur - amount);
        PlayerPrefs.Save();
        return true;
    }

    public static float GetWall1Progress()
    {
        return PlayerPrefs.GetFloat(Wall1ProgressKey, 0f);
    }

    public static void SetWall1Progress(float value)
    {
        PlayerPrefs.SetFloat(Wall1ProgressKey, Mathf.Max(0f, value));
        PlayerPrefs.Save();
    }

    public static int GetWall1Completions()
    {
        return PlayerPrefs.GetInt(Wall1CompletionsKey, 0);
    }

    public static void AddWall1Completions(int count)
    {
        if (count <= 0) return;
        int cur = GetWall1Completions();
        PlayerPrefs.SetInt(Wall1CompletionsKey, cur + count);
        PlayerPrefs.Save();
    }

    // Wall 1 – Passive Income generator
    public static int GetWall1IncomeLevel()
    {
        return PlayerPrefs.GetInt(Wall1IncomeLevelKey, 0);
    }

    public static void SetWall1IncomeLevel(int level)
    {
        PlayerPrefs.SetInt(Wall1IncomeLevelKey, Mathf.Max(0, level));
        PlayerPrefs.Save();
    }

    public static float GetWall1IncomeProgress()
    {
        return PlayerPrefs.GetFloat(Wall1IncomeProgressKey, 0f);
    }

    public static void SetWall1IncomeProgress(float value)
    {
        PlayerPrefs.SetFloat(Wall1IncomeProgressKey, Mathf.Max(0f, value));
        PlayerPrefs.Save();
    }

    // Wall 1 – Shard Amplifier
    private static float s_PassiveIncomePerSecond;

    public static void SetPassiveIncomeRate(float perSecond)
    {
        s_PassiveIncomePerSecond = Mathf.Max(0f, perSecond);
    }

    public static float GetPassiveIncomeRate()
    {
        return Mathf.Max(0f, s_PassiveIncomePerSecond);
    }

    public static float GetWall1AmplifierBonus()
    {
        return PlayerPrefs.GetFloat(Wall1AmplifierBonusKey, 0f);
    }

    public static void AddWall1AmplifierBonus(float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        float cur = GetWall1AmplifierBonus();
        PlayerPrefs.SetFloat(Wall1AmplifierBonusKey, Mathf.Max(0f, cur + delta));
        PlayerPrefs.Save();
    }

    public static float GetWall1AmplifierCarry()
    {
        return PlayerPrefs.GetFloat(Wall1AmplifierCarryKey, 0f);
    }

    public static void SetWall1AmplifierCarry(float value)
    {
        PlayerPrefs.SetFloat(Wall1AmplifierCarryKey, Mathf.Max(0f, value));
        PlayerPrefs.Save();
    }
}


