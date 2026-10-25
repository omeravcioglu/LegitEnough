using LegitEnough.Game;
using LegitEnough.Sim;
using Steamworks;

namespace LegitEnough.Steam
{
    /// <summary>
    /// Full-game achievements on Steam, mirrored into the local store (le.ach.full.) so the in-game screen,
    /// the "new" markers and the demo import work offline and before Steam's stats arrive.
    /// Reads take the best of both (players can earn things offline or on another PC); on the first Tick after
    /// Steam's stats arrive, local-only unlocks and higher local stats go up to Steam, and Steam-only ones come
    /// down. All six stats are best-of or lifetime counts (Increment Only on Steam), so nothing is ever lowered.
    /// Until the achievements are published in Steamworks, every Steam call fails quietly and only the local
    /// store moves.
    /// </summary>
    sealed class SteamAchievementBackend : IAchievementBackend
    {
        readonly LocalAchievementBackend _local = new LocalAchievementBackend(LocalAchievementBackend.FullPrefix);
        bool _synced;
        bool _pending;

        public string Name => "steam+" + _local.Name;

        public bool IsUnlocked(string id)
        {
            if (_local.IsUnlocked(id)) return true;
            return SteamBoot.IsUp && SteamUserStats.GetAchievement(id, out bool achieved) && achieved;
        }

        public void SetUnlocked(string id)
        {
            _local.SetUnlocked(id);
            if (SteamBoot.IsUp && SteamUserStats.SetAchievement(id)) _pending = true;
        }

        // Banking is the demo's: the full game never banks, but the mirror keeps the interface honest.
        public bool IsBanked(string id) => _local.IsBanked(id);
        public void SetBanked(string id) => _local.SetBanked(id);

        public int GetStat(string stat)
        {
            int local = _local.GetStat(stat);
            if (SteamBoot.IsUp && SteamUserStats.GetStat(stat, out int steam) && steam > local) return steam;
            return local;
        }

        public void SetStat(string stat, int value)
        {
            _local.SetStat(stat, value);
            if (SteamBoot.IsUp && SteamUserStats.GetStat(stat, out int steam) && value > steam
                && SteamUserStats.SetStat(stat, value)) _pending = true;
        }

        public bool IsNew(string id) => _local.IsNew(id);
        public void SetSeen(string id) => _local.SetSeen(id);

        public bool Imported
        {
            get => _local.Imported;
            set => _local.Imported = value;
        }

        public void Tick()
        {
            if (!SteamBoot.IsUp) return;
            SteamAPI.RunCallbacks();
            if (!_synced) TrySync();
        }

        /// <summary>
        /// Steam loads the user's stats by itself after SteamAPI.Init (SDK 1.61+); GetStat fails until they're
        /// here, or for good while the stats aren't published.
        /// </summary>
        void TrySync()
        {
            if (!SteamUserStats.GetStat(AchievementCatalog.Stats[0], out int _)) return;
            _synced = true;
            foreach (var stat in AchievementCatalog.Stats)
            {
                if (!SteamUserStats.GetStat(stat, out int steam)) continue;
                int local = _local.GetStat(stat);
                if (local > steam)
                {
                    if (SteamUserStats.SetStat(stat, local)) _pending = true;
                }
                else if (steam > local) _local.SetStat(stat, steam);
            }
            foreach (var d in AchievementCatalog.All)
            {
                if (!SteamUserStats.GetAchievement(d.Id, out bool onSteam)) continue;
                bool here = _local.IsUnlocked(d.Id);
                if (here && !onSteam)
                {
                    if (SteamUserStats.SetAchievement(d.Id)) _pending = true;
                }
                else if (onSteam && !here)
                {
                    // Earned on another PC: mirror it, but don't flag it as new on this one.
                    _local.SetUnlocked(d.Id);
                    _local.SetSeen(d.Id);
                }
            }
            Flush();
        }

        public void Flush()
        {
            _local.Flush();
            if (!_pending || !SteamBoot.IsUp) return;
            _pending = false;
            SteamUserStats.StoreStats();
        }

        public void ShowProgress(AchievementDef d, int value)
        {
            // The unlock itself goes through SetAchievement; Steam's progress toast is only for the steps before.
            if (!SteamBoot.IsUp || d == null || !d.IsProgress || d.Target <= 0 || value <= 0 || value >= d.Target) return;
            SteamUserStats.IndicateAchievementProgress(d.Id, (uint)value, (uint)d.Target);
        }

        /// <summary>Dev only (Shift+F8): forgets everything this edition earned, on Steam too.</summary>
        public void ResetAll()
        {
            _local.ResetAll();
            _pending = false;
            _synced = false;
            if (!SteamBoot.IsUp) return;
            SteamUserStats.ResetAllStats(true);
            SteamUserStats.StoreStats();
        }

        // SteamBoot owns SteamAPI.Shutdown (at quit); an edition change only needs the writes stored.
        public void Shutdown() => Flush();
    }
}
