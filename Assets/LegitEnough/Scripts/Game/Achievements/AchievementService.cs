using System.Collections.Generic;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// The Game's side of the achievements: owns the Book and the backend for the running edition,
    /// attaches a tracker to every new session, turns what the Book records into banners, flushes
    /// the store, and imports the demo's achievements on the full game's first boot.
    /// The demo never talks to Steam (Valve's advice for demos); batch runs keep everything in memory.
    /// </summary>
    public sealed class AchievementService
    {
        public AchievementBook Book { get; private set; }
        public IAchievementBackend Backend { get; private set; }
        public AchievementTracker Tracker { get; private set; }
        /// <summary>The edition the backend belongs to (null until the first session attaches).</summary>
        public bool? Demo { get; private set; }

        /// <summary>Unlocks waiting for the banner layer (GameRoot hands them over each frame).</summary>
        readonly List<AchievementUnlock> _fresh = new List<AchievementUnlock>();
        readonly Dictionary<string, int> _before = new Dictionary<string, int>();

        AchievementService() { }

        public static AchievementService Create() => new AchievementService();

        /// <summary>Picks (or keeps) the backend for the session's edition and starts watching it.</summary>
        public void Attach(GameSession session)
        {
            if (session == null) return;
            bool demo = session.IsDemo;
            if (Demo != demo || Backend == null)
            {
                Backend?.Shutdown();
                Backend = MakeBackend(demo);
                Demo = demo;
                Book = new AchievementBook(Backend, demo);
                _fresh.Clear();
                if (!demo) ImportDemo();
            }
            Tracker = new AchievementTracker(session);
        }

        static IAchievementBackend MakeBackend(bool demo)
        {
            if (Application.isBatchMode) return new MemoryAchievementBackend();
            if (demo) return new LocalAchievementBackend(LocalAchievementBackend.DemoPrefix);
            var steam = AchievementPlatform.SteamFactory?.Invoke();
            return steam ?? new LocalAchievementBackend(LocalAchievementBackend.FullPrefix);
        }

        /// <summary>
        /// Once, on the full game's first boot: everything the demo unlocked or banked on this machine
        /// is granted here (and on Steam through the backend), and its stats carry over. It shows as NEW on the screen.
        /// </summary>
        void ImportDemo()
        {
            if (Backend.Imported || Application.isBatchMode) return;
            var granted = Book.Import(new LocalAchievementBackend(LocalAchievementBackend.DemoPrefix));
            Backend.Imported = true;
            Backend.Flush();
            if (granted.Count > 0) Debug.Log($"LEGIT ENOUGH: {granted.Count} demo achievement(s) carried over.");
        }

        /// <summary>Every frame, after the session ticked (also while paused: the tracker polls here).</summary>
        public void Tick(GameRoot game, float dt)
        {
            if (Tracker == null || Book == null) return;
            if (game != null && Tracker.Session != game.Session) Attach(game.Session);
            Tracker.Poll();
            var signals = Tracker.Drain();
            if (signals.Count > 0) Apply(signals);
            Backend.Tick();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevKeys(game);
#endif
            if (game != null && game.AchToasts != null)
            {
                foreach (var u in _fresh) game.AchToasts.Enqueue(u);
                _fresh.Clear();
            }
        }

        void Apply(List<AchievementSignal> signals)
        {
            _before.Clear();
            foreach (var s in signals)
                if (s.Stat != null && !_before.ContainsKey(s.Stat)) _before[s.Stat] = Backend.GetStat(s.Stat);
            var news = Book.Apply(signals);
            _fresh.AddRange(news);
            bool statsMoved = false;
            foreach (var kv in _before)
            {
                int now = Backend.GetStat(kv.Key);
                if (now == kv.Value) continue;
                statsMoved = true;
                // Steam shows progress at 20/40/60/80% of a target, never at 100%.
                foreach (var d in AchievementCatalog.All)
                {
                    if (d.Stat != kv.Key || d.Target <= 0 || now >= d.Target || Book.Unlocked(d)) continue;
                    if (Book.Demo && !d.Demo) continue;
                    if (kv.Value * 5 / d.Target < now * 5 / d.Target) Backend.ShowProgress(d, now);
                }
            }
            if (news.Count > 0 || statsMoved) Backend.Flush();
        }

        public bool IsNew(AchievementDef d) => d != null && Backend != null && Backend.IsNew(d.Id);

        public void MarkSeen(AchievementDef d)
        {
            if (d == null || Backend == null || !Backend.IsNew(d.Id)) return;
            Backend.SetSeen(d.Id);
            Backend.Flush();
        }

        public void Shutdown()
        {
            Backend?.Flush();
            Backend?.Shutdown();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// F8 earns the next achievement this edition can still earn (to test banners and art);
        /// Shift+F8 resets this edition. Ignored while typing and over the menu screens.
        /// </summary>
        void DevKeys(GameRoot game)
        {
            var kb = Keyboard.current;
            if (kb == null || game == null || !kb.f8Key.wasPressedThisFrame) return;
            if (game.MenuUp || Typing(game)) return;
            if (kb.shiftKey.isPressed)
            {
                Backend.ResetAll();
                Book = new AchievementBook(Backend, Book.Demo);
                Tracker = new AchievementTracker(game.Session);
                _fresh.Clear();
                game.AchToasts?.Clear();
                game.Toasts.Show("[dev] achievements reset (" + (Book.Demo ? "demo" : "full") + ")");
                return;
            }
            foreach (var d in AchievementCatalog.All)
            {
                if (Book.Unlocked(d) || Book.Banked(d)) continue;
                if (Book.Demo && !d.Demo && d.Stat != null) continue; // the demo never banks progress
                Apply(new List<AchievementSignal> { AchievementSignal.Unlock(d.Id) });
                game.Toasts.Show("[dev] achievement " + d.Id);
                return;
            }
            game.Toasts.Show("[dev] nothing left to earn");
        }

        static bool Typing(GameRoot game)
        {
            var focused = game.Stage?.panel?.focusController?.focusedElement as VisualElement;
            return focused != null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
        }
#endif
    }
}
