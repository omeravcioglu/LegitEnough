using UnityEditor;

namespace LegitEnough.EditorTools
{
    /// <summary>
    /// Legit Enough > Achievements > Reset Local (Demo + Full): forgets the achievements Play mode has
    /// earned in the Editor. Editor PlayerPrefs are separate from a built player's, so this never
    /// touches a player install. (On Steam: steam.exe -console, then reset_all_stats 5394180.)
    /// </summary>
    public static class AchievementMenu
    {
        [MenuItem("Legit Enough/Achievements/Reset Local (Demo + Full)")]
        static void ResetLocal()
        {
            if (!EditorUtility.DisplayDialog("Reset achievements",
                    "Forget every achievement, banked achievement and stat earned in the Editor (demo and full)?",
                    "Reset", "Cancel")) return;
            LegitEnough.Game.AchievementPrefs.ResetLocal();
            UnityEngine.Debug.Log("Legit Enough: local achievements reset (demo + full, Editor PlayerPrefs).");
        }
    }
}
