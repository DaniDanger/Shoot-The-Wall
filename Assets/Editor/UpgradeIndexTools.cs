using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class UpgradeIndexTools
{
    [MenuItem("Tools/Upgrades/Assign Unique Indices (All)")]
    public static void AssignUniqueIndicesAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:UpgradeDefinition");
        var upgrades = new List<UpgradeDefinition>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var def = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
            if (def != null)
                upgrades.Add(def);
        }
        upgrades.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Assign Upgrade Indices");
        int group = Undo.GetCurrentGroup();

        for (int i = 0; i < upgrades.Count; i++)
        {
            var def = upgrades[i];
            if (def == null) continue;
            Undo.RecordObject(def, "Assign Upgrade Index");
            def.index = i;
            EditorUtility.SetDirty(def);
        }
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(group);
        Debug.Log($"[UpgradeIndexTools] Assigned indices 0..{(upgrades.Count > 0 ? upgrades.Count - 1 : 0)} to {upgrades.Count} upgrades.");
    }
}


