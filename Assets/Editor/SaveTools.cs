using UnityEditor;
using UnityEngine;
using System.Text;

public static class SaveTools
{
    [MenuItem("Tools/Save/Reset Save State (All)", priority = 0)]
    public static void ResetAll()
    {
        if (!EditorUtility.DisplayDialog("Reset Save State", "This will delete ALL PlayerPrefs for this project on this machine (currency, upgrades, passives, waves, etc).", "Delete All", "Cancel"))
            return;

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[Reset] All PlayerPrefs cleared.");
    }
}


