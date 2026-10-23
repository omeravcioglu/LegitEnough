using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>Where unlocks, demo "banked" unlocks and progress stats are kept.</summary>
    public interface IAchievementStore
    {
        bool IsUnlocked(string id);
        void SetUnlocked(string id);
        /// <summary>Demo only: earned for the full game, granted when the full game imports.</summary>
        bool IsBanked(string id);
        void SetBanked(string id);
        int GetStat(string stat);
        void SetStat(string stat, int value);
    }

    /// <summary>An achievement the Book just recorded. <see cref="Banked"/>: demo, earned for the full game.</summary>
    public readonly struct AchievementUnlock
    {
        public readonly AchievementDef Def;
        public readonly bool Banked;

        public AchievementUnlock(AchievementDef def, bool banked)
        {
            Def = def;
            Banked = banked;
        }

        public override string ToString() => Banked ? Def.Id + " (banked)" : Def.Id;
    }

    /// <summary>
    /// Turns tracker signals into persistent unlocks (achievements spec, section 2.4): dedupe, stats to progress
    /// unlocks, demo banking of full-game event achievements, and the demo-to-full import.
    /// </summary>
    public sealed class AchievementBook
    {
        readonly IAchievementStore _store;
        public readonly bool Demo;

        public AchievementBook(IAchievementStore store, bool demo)
        {
            _store = store;
            Demo = demo;
        }

        public IAchievementStore Store => _store;

        /// <summary>Applies the signals and returns only what is new (unlocked or banked now).</summary>
        public List<AchievementUnlock> Apply(IEnumerable<AchievementSignal> signals)
        {
            var result = new List<AchievementUnlock>();
            if (signals == null) return result;
            foreach (var s in signals)
            {
                switch (s.Kind)
                {
                    case AchievementSignalKind.Unlock:
                        Unlock(AchievementCatalog.Get(s.Id), result);
                        break;
                    case AchievementSignalKind.Add:
                        if (s.Id == null) break;
                        _store.SetStat(s.Id, _store.GetStat(s.Id) + s.Value);
                        CheckStat(s.Id, result);
                        break;
                    case AchievementSignalKind.Max:
                        if (s.Id == null) break;
                        if (s.Value > _store.GetStat(s.Id)) _store.SetStat(s.Id, s.Value);
                        CheckStat(s.Id, result);
                        break;
                }
            }
            return result;
        }

        public bool Unlocked(AchievementDef d) => d != null && _store.IsUnlocked(d.Id);
        public bool Banked(AchievementDef d) => d != null && !_store.IsUnlocked(d.Id) && _store.IsBanked(d.Id);

        /// <summary>Current stat value for progress bars (0 for an event achievement).</summary>
        public int Value(AchievementDef d) => d?.Stat != null ? _store.GetStat(d.Stat) : 0;

        public int CountUnlocked(bool demoOnly)
        {
            int n = 0;
            foreach (var d in AchievementCatalog.All)
                if ((!demoOnly || d.Demo) && _store.IsUnlocked(d.Id)) n++;
            return n;
        }

        public int CountBanked()
        {
            int n = 0;
            foreach (var d in AchievementCatalog.All)
                if (Banked(d)) n++;
            return n;
        }

        /// <summary>
        /// Full edition: grants everything the demo store unlocked or banked, and carries its stats
        /// (the passports delivered add to the lifetime count, the bests stand), which can unlock
        /// progress achievements. Returns what is new. Run it once: the caller keeps the "imported once" flag.
        /// </summary>
        public List<AchievementUnlock> Import(IAchievementStore demoStore)
        {
            var result = new List<AchievementUnlock>();
            if (Demo || demoStore == null) return result;
            foreach (var d in AchievementCatalog.All)
            {
                if (!demoStore.IsUnlocked(d.Id) && !demoStore.IsBanked(d.Id)) continue;
                if (_store.IsUnlocked(d.Id)) continue;
                _store.SetUnlocked(d.Id);
                result.Add(new AchievementUnlock(d, false));
            }
            // "Every run counts": demo runs too.
            foreach (var stat in AchievementCatalog.Stats)
            {
                int demo = demoStore.GetStat(stat), now = _store.GetStat(stat);
                if (demo <= 0) continue;
                int next = stat == AchievementCatalog.StatJobsDone ? now + demo : Math.Max(now, demo);
                if (next == now) continue;
                _store.SetStat(stat, next);
                CheckStat(stat, result);
            }
            return result;
        }

        void CheckStat(string stat, List<AchievementUnlock> result)
        {
            int value = _store.GetStat(stat);
            foreach (var d in AchievementCatalog.All)
                if (d.Stat == stat && value >= d.Target) Unlock(d, result);
        }

        void Unlock(AchievementDef d, List<AchievementUnlock> result)
        {
            if (d == null || _store.IsUnlocked(d.Id)) return;
            if (!Demo || d.Demo)
            {
                _store.SetUnlocked(d.Id);
                result.Add(new AchievementUnlock(d, false));
                return;
            }
            // Demo, full-game achievement: event ones are banked for the full game; progress ones never are.
            if (d.Stat != null || _store.IsBanked(d.Id)) return;
            _store.SetBanked(d.Id);
            result.Add(new AchievementUnlock(d, true));
        }
    }

    /// <summary>In-memory store: tests and batch-mode runs.</summary>
    public sealed class MemoryAchievementStore : IAchievementStore
    {
        readonly HashSet<string> _unlocked = new HashSet<string>();
        readonly HashSet<string> _banked = new HashSet<string>();
        readonly Dictionary<string, int> _stats = new Dictionary<string, int>();

        public bool IsUnlocked(string id) => _unlocked.Contains(id);
        public void SetUnlocked(string id) => _unlocked.Add(id);
        public bool IsBanked(string id) => _banked.Contains(id);
        public void SetBanked(string id) => _banked.Add(id);
        public int GetStat(string stat) => _stats.TryGetValue(stat, out var v) ? v : 0;
        public void SetStat(string stat, int value) => _stats[stat] = value;

        public void Clear()
        {
            _unlocked.Clear();
            _banked.Clear();
            _stats.Clear();
        }
    }
}
