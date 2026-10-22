using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>Where the demo sends players who want the full game: the Steam store page.</summary>
    public static class StoreLinks
    {
        public const string WishlistUrl = "https://store.steampowered.com/app/5394180/Legit_Enough/";

        /// <summary>How many times a WISHLIST button was pressed this run (the smoke test checks the wiring).</summary>
        public static int OpenCount { get; private set; }

        /// <summary>Opens the store page in the player's browser.</summary>
        public static void OpenWishlist()
        {
            OpenCount++;
            // Batch runs (smoke test, store shots) press the button too; never open a browser from them.
            if (Application.isBatchMode)
            {
                Debug.Log("Wishlist: " + WishlistUrl);
                return;
            }
            // Mid-shift (a locked handbook page's button) the clock stops first: the player comes
            // back from the browser to PAUSED, not to a customer who walked out.
            if (GameRoot.Instance != null) GameRoot.Instance.PauseShift();
            Application.OpenURL(WishlistUrl);
        }
    }
}
