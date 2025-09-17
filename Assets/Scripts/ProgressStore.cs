using UnityEngine;

// Stores meta progress like the highest wall (wave) reached.
public static class ProgressStore
{
    private const string WaveReachedKey = "Wave_Reached"; // 0-based index

    public static int GetWaveReached()
    {
        return PlayerPrefs.GetInt(WaveReachedKey, 0);
    }

    public static void SetWaveReached(int waveIndex)
    {
        int clamped = Mathf.Max(0, waveIndex);
        // Only write if improved or different
        if (GetWaveReached() != clamped)
        {
            PlayerPrefs.SetInt(WaveReachedKey, clamped);
            PlayerPrefs.Save();
        }
    }
}


