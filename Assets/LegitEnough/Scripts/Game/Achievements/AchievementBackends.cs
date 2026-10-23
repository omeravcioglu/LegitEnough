using System;
using System.Collections.Generic;
using LegitEnough.Sim;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>
    /// Where the achievements live on this machine or platform: PlayerPrefs, memory (batch runs) or,
    /// later, Steam (which mirrors every write into the local store so the in-game screen works offline).
    /// </summary>
    public interface IAchievementBackend : IAchievementStore
    {
        string Name { get; }
        /// <summary>Once a frame (Steam: RunCallbacks).</summary>
        void Tick();
        /// <summary>After a change and at quit (PlayerPrefs.Save / StoreStats).</summary>
        void Flush();
        /// <summary>A progress achievement moved on (Steam: IndicateAchievementProgress; local: nothing).</summary>
        void ShowProgress(AchievementDef d, int value);
        /// <summary>Dev only: forget everything this edition has earned.</summary>
        void ResetAll();
        void Shutdown();

        /// <summary>Earned (or banked) and not yet looked at on the ACHIEVEMENTS screen.</summary>
        bool IsNew(string id);
        void SetSeen(string id);
        /// <summary>The full edition has taken over the demo's achievements (once, on the first boot).</summary>
        bool Imported { get; set; }
    }

    /// <summary>
    /// For the Editor menu (whose assembly sees only LegitEnough.Game, so nothing here exposes Sim types):
    /// forgets the local achievements of both editions in this process's PlayerPrefs.
    /// </summary>
    public static class AchievementPrefs
    {
        public static void ResetLocal()
        {
            new LocalAchievementBackend(LocalAchievementBackend.DemoPrefix).ResetAll();
            new LocalAchievementBackend(LocalAchievementBackend.FullPrefix).ResetAll();
        }
    }

    /// <summary>Set by the optional LegitEnough.Steam assembly when Steam is up; null without it.</summary>
    public static class AchievementPlatform
    {
        public static Func<IAchievementBackend> SteamFactory;
    }

    /// <summary>
    /// PlayerPrefs, under a prefix per edition ("le.ach.demo." / "le.ach.full."): u.ID unlocked,
    /// b.ID banked (demo), s.STAT values, new.ID unseen, imported, v schema version. PlayerPrefs can't
    /// list keys, so a reset walks the catalog.
    /// </summary>
    public sealed class LocalAchievementBackend : IAchievementBackend
    {
        public const string DemoPrefix = "le.ach.demo.";
        public const string FullPrefix = "le.ach.full.";
        const int Version = 1;

        readonly string _p;
        bool _dirty;

        public LocalAchievementBackend(string prefix)
        {
            _p = prefix;
        }

        public string Name => "local:" + _p;

        public bool IsUnlocked(string id) => PlayerPrefs.GetInt(_p + "u." + id, 0) != 0;
        public bool IsBanked(string id) => PlayerPrefs.GetInt(_p + "b." + id, 0) != 0;
        public int GetStat(string stat) => PlayerPrefs.GetInt(_p + "s." + stat, 0);
        public bool IsNew(string id) => PlayerPrefs.GetInt(_p + "new." + id, 0) != 0;

        public void SetUnlocked(string id)
        {
            Set("u." + id, 1);
            Set("new." + id, 1);
        }

        public void SetBanked(string id)
        {
            Set("b." + id, 1);
            Set("new." + id, 1);
        }

        public void SetStat(string stat, int value) => Set("s." + stat, value);

        public void SetSeen(string id)
        {
            if (!PlayerPrefs.HasKey(_p + "new." + id)) return;
            PlayerPrefs.DeleteKey(_p + "new." + id);
            _dirty = true;
        }

        public bool Imported
        {
            get => PlayerPrefs.GetInt(_p + "imported", 0) != 0;
            set => Set("imported", value ? 1 : 0);
        }

        void Set(string key, int value)
        {
            if (PlayerPrefs.GetInt(_p + key, int.MinValue) == value) return;
            PlayerPrefs.SetInt(_p + key, value);
            if (PlayerPrefs.GetInt(_p + "v", 0) != Version) PlayerPrefs.SetInt(_p + "v", Version);
            _dirty = true;
        }

        public void Tick() { }

        public void Flush()
        {
            if (!_dirty) return;
            _dirty = false;
            PlayerPrefs.Save();
        }

        public void ShowProgress(AchievementDef d, int value) { }

        public void ResetAll()
        {
            foreach (var d in AchievementCatalog.All)
            {
                PlayerPrefs.DeleteKey(_p + "u." + d.Id);
                PlayerPrefs.DeleteKey(_p + "b." + d.Id);
                PlayerPrefs.DeleteKey(_p + "new." + d.Id);
            }
            foreach (var s in AchievementCatalog.Stats) PlayerPrefs.DeleteKey(_p + "s." + s);
            PlayerPrefs.DeleteKey(_p + "imported");
            PlayerPrefs.DeleteKey(_p + "v");
            _dirty = true;
            Flush();
        }

        public void Shutdown() => Flush();
    }

    /// <summary>Batch runs (smoke test, trailer, store shots): nothing read from or written to the player's machine.</summary>
    public sealed class MemoryAchievementBackend : IAchievementBackend
    {
        readonly MemoryAchievementStore _store = new MemoryAchievementStore();
        readonly HashSet<string> _new = new HashSet<string>();

        public string Name => "memory";

        public bool IsUnlocked(string id) => _store.IsUnlocked(id);
        public bool IsBanked(string id) => _store.IsBanked(id);
        public int GetStat(string stat) => _store.GetStat(stat);
        public void SetStat(string stat, int value) => _store.SetStat(stat, value);

        public void SetUnlocked(string id)
        {
            _store.SetUnlocked(id);
            _new.Add(id);
        }

        public void SetBanked(string id)
        {
            _store.SetBanked(id);
            _new.Add(id);
        }

        public bool IsNew(string id) => _new.Contains(id);
        public void SetSeen(string id) => _new.Remove(id);
        public bool Imported { get; set; }

        public void Tick() { }
        public void Flush() { }
        public void ShowProgress(AchievementDef d, int value) { }

        public void ResetAll()
        {
            _store.Clear();
            _new.Clear();
            Imported = false;
        }

        public void Shutdown() { }
    }
}
