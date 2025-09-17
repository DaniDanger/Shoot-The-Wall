using UnityEditor;
using UnityEngine;
using System.Text;

public static class SaveTools
{
    [MenuItem("Tools/Save/Reset All PlayerPrefs", priority = 0)]
    public static void ResetAllPlayerPrefs()
    {
        if (!EditorUtility.DisplayDialog("Reset All PlayerPrefs", "This will delete ALL PlayerPrefs for this project on this machine.", "Delete All", "Cancel"))
            return;

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("PlayerPrefs cleared.");
    }

    [MenuItem("Tools/Save/Reset Meta (Currency + Upgrades)", priority = 1)]
    public static void ResetMeta()
    {
        if (!EditorUtility.DisplayDialog("Reset Meta", "Delete currency and simple upgrade progress?", "Delete", "Cancel"))
            return;

        PlayerPrefs.DeleteKey("Currency_Total");
        // Legacy simple-upgrade keys
        PlayerPrefs.DeleteKey("Upg_Damage_Level");
        PlayerPrefs.DeleteKey("Upg_Fire_Level");
        PlayerPrefs.DeleteKey("Upg_Red_Level");
        PlayerPrefs.DeleteKey("Upg_Crit_Level");

        // Data-driven upgrade keys: Upg_<id>_Level. Discover all UpgradeDefinition assets and clear their keys.
        StringBuilder sb = new StringBuilder();
        string[] guids = AssetDatabase.FindAssets("t:UpgradeDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var def = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
            if (def == null || string.IsNullOrEmpty(def.id)) continue;
            string key = $"Upg_{def.id}_Level";
            PlayerPrefs.DeleteKey(key);
            if (sb.Length == 0) sb.Append("[ResetMeta] Cleared keys: "); else sb.Append(", ");
            sb.Append(key);
        }

        PlayerPrefs.Save();
        if (sb.Length > 0) Debug.Log(sb.ToString());
        PlayerPrefs.DeleteKey("Wave_Reached");
        Debug.Log($"[ResetMeta] Currency_Total now={PlayerPrefs.GetInt("Currency_Total", 0)}");
    }
}


