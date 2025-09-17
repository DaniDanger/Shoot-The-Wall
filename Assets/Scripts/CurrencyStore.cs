using UnityEngine;

// Minimal currency storage for MVP: run currency and persistent total.
public static class CurrencyStore
{
    private const string PersistKey = "Currency_Total";

    private static int runCurrency;

    public static int RunCurrency => runCurrency;
    public static int TotalCurrency => PlayerPrefs.GetInt(PersistKey, 0);

    public static void ResetRun()
    {
        runCurrency = 0;
    }

    public static void AddRunCurrency(int amount)
    {
        if (amount <= 0) return;
        runCurrency += amount;
    }

    // Call on death/game over to bank the run currency
    public static void BankRunToTotal()
    {
        int total = TotalCurrency + runCurrency;
        PlayerPrefs.SetInt(PersistKey, total);
        PlayerPrefs.Save();
        runCurrency = 0;
    }

    // Immediately add to the persistent total (used for ping deliveries)
    public static void AddToTotal(int amount)
    {
        if (amount <= 0) return;
        int total = TotalCurrency + amount;
        PlayerPrefs.SetInt(PersistKey, total);
        PlayerPrefs.Save();
    }

    public static bool TrySpendFromTotal(int amount)
    {
        if (amount <= 0) return true;
        int total = TotalCurrency;
        if (total < amount) return false;
        PlayerPrefs.SetInt(PersistKey, total - amount);
        PlayerPrefs.Save();
        return true;
    }
}


