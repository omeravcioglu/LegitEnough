using UnityEditor;

namespace LegitEnough.EditorTools
{
    /// <summary>
    /// Legit Enough > Play Full Game in Editor: a checked menu item that makes Play mode start the
    /// full game instead of the demo. It only writes an EditorPrefs switch that GameRoot.Awake reads
    /// in the Editor (never in batch mode or a player), so GameRoot.DemoBuild stays true in source
    /// and the player build (BuildDemo, which refuses anything else) is always the demo.
    /// </summary>
    public static class FullGameToggle
    {
        const string Menu = "Legit Enough/Play Full Game in Editor";
        /// <summary>Must match the key GameRoot.Awake reads.</summary>
        public const string PrefKey = "LegitEnough.PlayFullGameInEditor";

        [MenuItem(Menu)]
        static void Toggle()
        {
            bool full = !EditorPrefs.GetBool(PrefKey, false);
            EditorPrefs.SetBool(PrefKey, full);
            UnityEditor.Menu.SetChecked(Menu, full);
            UnityEngine.Debug.Log(full
                ? "Legit Enough: Play mode now starts the FULL GAME (Editor only). Builds are still the demo."
                : "Legit Enough: Play mode now starts the DEMO.");
        }

        [MenuItem(Menu, true)]
        static bool ToggleValidate()
        {
            UnityEditor.Menu.SetChecked(Menu, EditorPrefs.GetBool(PrefKey, false));
            return !EditorApplication.isPlaying;
        }
    }
}
