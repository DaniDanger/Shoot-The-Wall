using UnityEngine;

[System.Serializable]
public class GraveBombState
{
    public static bool Pending;
    public static int PendingWaveIndex;
    public static bool HasExactCell;
    public static int PendingRow;
    public static int PendingCol;
    public static float PendingWorldX;
    public static bool ActivePlaced; // true when a grave is currently in the wall

    // No Clear() retained to avoid unused code; fields are set explicitly where needed
}







