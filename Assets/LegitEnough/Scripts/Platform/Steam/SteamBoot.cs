using System;
using LegitEnough.Game;
using Steamworks;
using UnityEngine;

namespace LegitEnough.Steam
{
    /// <summary>
    /// The optional Steam layer for the FULL game (Legit Enough, app 5394180). At load it only registers
    /// <see cref="AchievementPlatform.SteamFactory"/>; Steam itself starts the first time the full edition asks
    /// for its achievement backend. The demo (its own app, local achievements) and batch harnesses never call
    /// the factory, so they never touch Steam. That also covers "Play Full Game in Editor", which flips
    /// GameRoot.DemoBuild in GameRoot.Awake, after BeforeSceneLoad.
    /// If Steam isn't running or the app isn't owned, the factory returns null and the game keeps its local
    /// achievements. In the Editor, steam_appid.txt (5394180) in the project root tells Steam which app this is.
    /// </summary>
    public static class SteamBoot
    {
        public const uint AppId = 5394180;

        static bool _tried;
        static bool _up;
        static SteamAchievementBackend _backend;

        /// <summary>SteamAPI.Init succeeded and Shutdown hasn't run.</summary>
        public static bool IsUp => _up;

        // With domain reload off, statics survive between play sessions in the Editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _tried = false;
            _up = false;
            _backend = null;
            AchievementPlatform.SteamFactory = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            AchievementPlatform.SteamFactory = CreateBackend;
#if !UNITY_EDITOR
            // Valve's recommended check: a full-game player started outside Steam relaunches through it.
            // DemoBuild is fixed at build time in a player, so the demo never asks.
            if (!GameRoot.DemoBuild && !Application.isBatchMode)
            {
                try
                {
                    if (SteamAPI.RestartAppIfNecessary(new AppId_t(AppId))) Application.Quit();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Steam] RestartAppIfNecessary failed: " + e.Message);
                }
            }
#endif
        }

        /// <summary>The factory: one backend per process, or null when Steam isn't available.</summary>
        static IAchievementBackend CreateBackend()
        {
            if (_backend != null) return _backend;
            if (_tried || Application.isBatchMode) return null;
            _tried = true;
            try
            {
                if (!Packsize.Test())
                {
                    Debug.LogWarning("[Steam] Steamworks.NET packsize check failed; achievements stay local.");
                    return null;
                }
                var result = SteamAPI.InitEx(out string error);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Debug.LogWarning($"[Steam] Not available ({result}: {error}); achievements stay local.");
                    return null;
                }
            }
            catch (Exception e)
            {
                // DllNotFoundException when steam_api64.dll is missing, and so on: never fatal.
                Debug.LogWarning("[Steam] Init failed; achievements stay local. " + e.Message);
                return null;
            }
            _up = true;
            Application.quitting += Shutdown;
            _backend = new SteamAchievementBackend();
            Debug.Log("[Steam] Up for app " + SteamUtils.GetAppID());
            return _backend;
        }

        /// <summary>At quit (and when leaving Play mode): store what's pending, then shut Steam down.</summary>
        static void Shutdown()
        {
            Application.quitting -= Shutdown;
            if (!_up) return;
            _backend?.Flush();
            SteamAPI.Shutdown();
            _up = false;
        }
    }
}
