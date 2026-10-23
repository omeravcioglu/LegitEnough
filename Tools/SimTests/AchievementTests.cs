using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LegitEnough.Sim;
using A = LegitEnough.Sim.AchievementCatalog;
using C = LegitEnough.Sim.GameSession.ChoiceIds;

namespace LegitEnough.SimTests
{
    /// <summary>Achievements: the catalog, the read-only tracker (it must never change a game) and the Book.</summary>
    public static class AchievementTests
    {
        static GameSession NewDemo(ulong seed) => new GameSession(seed, GameRules.Demo());

        static string Ids(Visit v) => string.Join(",", v.Choices.Select(c => c.Id));

        static string Fired(AchievementTracker t) => string.Join(" ", A.All.Where(d => t.Fired.Contains(d.Id)).Select(d => d.Id));

        static void Expect(AchievementTracker t, IEnumerable<string> expected, string label)
        {
            var want = new HashSet<string>(expected);
            var got = new HashSet<string>(t.Fired);
            Assert.True(want.SetEquals(got), $"{label}: fired [{Fired(t)}], expected [{string.Join(" ", A.All.Where(d => want.Contains(d.Id)).Select(d => d.Id))}]");
        }

        static readonly string[] DemoDefault =
        {
            A.FirstPhoto, A.PercussiveMaintenance, A.FirstJob, A.Quality90, A.SamirMadeIt, A.ReadThePaper, A.RushJob,
        };

        // ------------------------------------------------------------------ catalog

        public static void Ach_CatalogValid()
        {
            var all = A.All;
            Assert.Equal(36, all.Count, "36 achievements");
            Assert.Equal(12, all.Count(d => d.Demo), "12 demo achievements");
            Assert.Equal(12, A.DemoCount, "DemoCount");
            Assert.Equal(6, A.Stats.Length, "6 stats");
            Assert.Equal(A.Stats.Length, A.Stats.Distinct().Count(), "stats unique");
            var idPattern = new Regex("^[A-Z][A-Z0-9_]{2,63}$");
            var seen = new HashSet<string>();
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                Assert.True(idPattern.IsMatch(d.Id), $"{d.Id}: API name format");
                Assert.True(seen.Add(d.Id), $"{d.Id}: unique");
                Assert.True(A.Get(d.Id) == d, $"{d.Id}: Get");
                Assert.True(!string.IsNullOrEmpty(d.Name) && d.Name.Length <= 32, $"{d.Id}: name length");
                Assert.True(!string.IsNullOrEmpty(d.Description) && d.Description.Length <= 100, $"{d.Id}: description length");
                foreach (var text in new[] { d.Name, d.Description, d.Teaser ?? "" })
                    Assert.True(text.All(ch => ch >= 32 && ch < 127), $"{d.Id}: ASCII only");
                Assert.Equal(d.Hidden, d.Teaser != null, $"{d.Id}: hidden <=> teaser");
                if (d.Teaser != null) Assert.True(d.Teaser.Length <= 100, $"{d.Id}: teaser length");
                if (d.Stat != null)
                {
                    Assert.True(A.Stats.Contains(d.Stat) && d.Target > 0, $"{d.Id}: progress stat");
                    Assert.Equal(AchCategory.Progress, d.Category, $"{d.Id}: progress category");
                }
                else Assert.Equal(0, d.Target, $"{d.Id}: event target");
                Assert.Equal(i < 12, d.Demo, $"{d.Id}: demo achievements come first");
            }
            Assert.True(A.Get("NOPE") == null && A.Get(null) == null, "unknown id");
            Assert.Equal(15, all.Count(d => d.Category == AchCategory.Progress), "15 progress");
            Assert.Equal(10, all.Count(d => d.Category == AchCategory.Skill), "10 skill");
            Assert.Equal(4, all.Count(d => d.Category == AchCategory.Story), "4 story");
            Assert.Equal(7, all.Count(d => d.Category == AchCategory.Secret), "7 secret");
            Assert.Equal(8, all.Count(d => d.Hidden), "8 hidden");
            Assert.True(all.Where(d => d.Demo).All(d => d.Stat == null), "demo achievements are events");
        }

        // ------------------------------------------------------------------ the tracker never changes a game

        public static void Ach_FullModeUnchanged()
        {
            var plain = TwentyDays(false, out _, out _);
            var tracked = TwentyDays(true, out var t, out var book);
            CompareDigests(plain, tracked, "20-day bot");
            Console.WriteLine("    " + plain[plain.Count - 6]);
            var earned = A.All.Where(d => t.Fired.Contains(d.Id) || book.Unlocked(d)).Select(d => d.Id).ToList();
            Console.WriteLine("    fired: " + Fired(t));
            Console.WriteLine("    book:  " + string.Join(" ", earned) + " | " +
                string.Join(" ", A.Stats.Select(st => $"{st}={book.Store.GetStat(st)}")));
            foreach (var id in new[] { A.FirstPhoto, A.FirstJob, A.WeekOne, A.RepWord, A.Cash10K, A.Sections5 })
                Assert.True(earned.Contains(id), $"20-day run earns {id}");
            Assert.True(t.Fired.All(id => A.Get(id) != null), "only catalog ids");
        }

        public static void Ach_DemoUnchanged()
        {
            var plain = PlayDemo(70, new Plan { BuyGermanyMorning = true, Track = false });
            var tracked = PlayDemo(70, new Plan { BuyGermanyMorning = true, Track = true });
            CompareDigests(plain.Digest(), tracked.Digest(), "demo run");
            Assert.True(plain.Tracker == null && tracked.Tracker != null, "tracker attached to one run only");
        }

        static void CompareDigests(List<string> a, List<string> b, string label)
        {
            for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
                Assert.Equal(a[i], b[i], $"{label}: digest line {i}");
            Assert.Equal(a.Count, b.Count, $"{label}: digest length");
        }

        // ------------------------------------------------------------------ demo paths

        public static void Ach_DemoDefaultPath()
        {
            var run = PlayDemo(70, new Plan { BuyGermanyMorning = true });
            Console.WriteLine("    default: " + Fired(run.Tracker));
            Expect(run.Tracker, DemoDefault, "default demo path");
            var book = new AchievementBook(new MemoryAchievementStore(), true);
            var news = book.Apply(run.Signals);
            Assert.Equal(7, book.CountUnlocked(true), "7 demo achievements unlocked");
            Assert.Equal(7, book.CountUnlocked(false), "nothing outside the demo 12");
            Assert.Equal(0, book.CountBanked(), "nothing banked");
            Assert.Equal(7, news.Count, "7 new unlocks");
            Assert.True(book.Value(A.Get(A.Jobs25)) == 4, $"4 jobs counted (got {book.Value(A.Get(A.Jobs25))})");
        }

        public static void Ach_DemoDesignedPath()
        {
            var run = PlayDemo(70, new Plan
            {
                DesignedDayOne = true, HaggleElif = true, BuyStationAfterViktor = true, BuyGermanyWithAnja = true,
            });
            Console.WriteLine("    designed: " + Fired(run.Tracker));
            Expect(run.Tracker, DemoDefault.Concat(new[] { A.SayCheese, A.Haggler, A.FirstUpgrade, A.JustInTime }), "designed demo path");
            var book = new AchievementBook(new MemoryAchievementStore(), true);
            book.Apply(run.Signals);
            Assert.Equal(11, book.CountUnlocked(true), "11 of 12");
            Assert.Equal(0, book.CountBanked(), "nothing banked");
            var anja = run.S.Orders.All.Single(o => o.CustomerId == DemoCast.AnjaId);
            Assert.Equal(OrderStatus.Delivered, anja.Status, "Anja's job delivered");
            var elif = run.S.Orders.All.Single(o => o.CustomerId == DemoCast.ElifId);
            Assert.True(elif.Price > 380, $"Elif paid above her opening offer ({elif.Price})");
        }

        public static void Ach_DemoNightShift()
        {
            var late = PlayDemo(70, new Plan { NightShift = true, Days = 1 });
            Console.WriteLine("    night shift: " + Fired(late.Tracker));
            Assert.True(late.Tracker.Fired.Contains(A.NightShift), "still at the desk at 23:00");
            Assert.True(late.S.Clock.Minute >= GameSession.ForcedCloseMinute, "the day ended at 23:00");
            var early = PlayDemo(70, new Plan { Days = 1 });
            Assert.True(!early.Tracker.Fired.Contains(A.NightShift), "closing early: no night shift");
        }

        public static void Ach_DemoCarelessSamir()
        {
            var s = NewDemo(51);
            var t = new AchievementTracker(s);
            var v = DemoSamirToBench(s, false, 0);
            v.Order.Draft.Set(DocField.Surname, "HADAD");
            var doc = Bot.PrintAndBind(s, v.Order);
            PayUp(s, v, doc);
            Bot.TickUntil(s, () => s.Visits.Count == 0, 60f, "Samir leaves");
            Assert.True(s.CloseShop(), "close early");
            s.StartNextDay();
            Console.WriteLine($"    careless: quality {v.Order.Quality.Final:0}, fired {Fired(t)}");
            Assert.True(!t.Fired.Contains(A.Quality90) && !t.Fired.Contains(A.SamirMadeIt), "no quality, no Lyon");
            Assert.True(t.Fired.Contains(A.SamirCaught), "Samir doesn't make it");
            var book = new AchievementBook(new MemoryAchievementStore(), true);
            var news = book.Apply(t.Drain());
            var caught = A.Get(A.SamirCaught);
            Assert.True(book.Banked(caught) && !book.Unlocked(caught), "banked for the full game, not unlocked");
            Assert.True(news.Any(u => u.Def == caught && u.Banked), "reported as banked");
        }

        public static void Ach_TutorialNothingEarly()
        {
            var s = NewDemo(34);
            var t = new AchievementTracker(s);
            void NothingYet(string when)
            {
                t.Poll();
                var sig = t.Drain();
                Assert.True(t.Fired.Count == 0 && sig.All(x => x.Kind != AchievementSignalKind.Unlock),
                    $"{when}: nothing unlocked yet [{string.Join(", ", sig)}]");
            }
            NothingYet("construction");
            Assert.True(s.Inbox.Messages.Any(m => m.From == "R."), "R.'s mail is in");
            s.OpenShop();
            NothingYet("open");
            var v = Bot.WaitForChoice(s, C.Accept, 300f);
            Assert.Equal(StoryCast.SamirId, v.Customer.Id, "Samir");
            NothingYet("Samir arrives");
            s.Choose(v.Id, C.Accept);
            Bot.WaitForChoice(s, C.TakePhoto, 60f);
            s.Choose(v.Id, C.TakePhoto);
            Bot.TickUntil(s, () => v.Phase == VisitPhase.PhotoSitting && !v.Moving, 30f, "seated");
            NothingYet("seated");
            Bot.TakeGoodPhoto(s, v);
            var first = t.Drain().FirstOrDefault(x => x.Kind == AchievementSignalKind.Unlock);
            Assert.Equal(A.FirstPhoto, first.Id, "first unlock");
            Assert.Equal(A.FirstPhoto, Fired(t), "only FIRST_PHOTO");
        }

        public static void Ach_RuleChangeSameDayOnly()
        {
            bool Run(Backdrop? backdrop, bool nextDay)
            {
                var s = new GameSession(12345);
                var t = new AchievementTracker(s);
                s.InjectNews(new NewsItem { Effect = NewsEffect.RuleChange, CountryId = Countries.France, RuleBackdrop = Backdrop.White, Headline = "TEST" });
                if (nextDay)
                {
                    Assert.True(s.CloseShop(), "close before anyone came");
                    s.StartNextDay();
                }
                var (v, _) = FullSamirToBench(s, backdrop, 2f);
                var doc = Bot.PrintAndBind(s, v.Order);
                PayUp(s, v, doc);
                Assert.Equal(nextDay ? 2 : 1, s.Day, "delivery day");
                Assert.Equal(1, s.Country(Countries.France).LastRuleChangeDay, "the only French rule change was on day 1");
                bool wrong = v.Order.Quality.Has(Issue.WrongBackdrop);
                Assert.Equal(backdrop == Backdrop.LightGray, wrong, "backdrop as shot");
                return t.Fired.Contains(A.ReadThePaper);
            }
            Assert.True(Run(null, false), "white, same day: read the paper");
            Assert.True(!Run(Backdrop.LightGray, false), "gray, same day: no");
            Assert.True(!Run(null, true), "white, next day: no");
        }

        public static void Ach_FullProbes()
        {
            var lines = new List<string>();

            // Kit, cash, a flawless job and a jam-free print run on the tier-3 gear.
            {
                var s = new GameSession(12345);
                var t = new AchievementTracker(s);
                var book = new AchievementBook(new MemoryAchievementStore(), false);
                s.Ledger.Record(s.Now, LedgerKind.Other, 20000, "test money");
                t.Poll();
                foreach (var id in new[] { "cam3", "prn3", "pc3", "stn3" }) Assert.True(s.BuyEquipment(id), $"buy {id}");
                t.Poll();
                var (v, _) = FullSamirToBench(s, null, 2f);
                for (int i = 0; i < 6; i++) s.PrintDraft(v.Order.Id);
                Bot.TickUntil(s, () => s.Printer.Tray.Count >= 6, 120f, "six pages");
                var doc = s.BindPage(s.Printer.Tray[s.Printer.Tray.Count - 1].Id);
                PayUp(s, v, doc);
                lines.Add($"kit job: quality {v.Order.Quality.Final:0} [{string.Join("; ", v.Order.Quality.Findings)}]");
                EndDayAt2300(s);
                book.Apply(t.Drain());
                Has(t, book, "tier-3 kit", A.FirstUpgrade, A.TopShelf, A.FullKit, A.Cash10K, A.Flawless, A.CleanPrintDay, A.FirstJob, A.Quality90, A.NightShift);
                Assert.Equal(4, book.Store.GetStat(A.StatTier3Best), "4 tier-3 pieces");
            }

            // Reputation and every handbook section.
            {
                var s = new GameSession(7);
                var t = new AchievementTracker(s);
                var book = new AchievementBook(new MemoryAchievementStore(), false);
                s.Standing.AddReputation(99f);
                t.Poll();
                book.Apply(t.Drain());
                Assert.True(!book.Unlocked(A.Get(A.RepWord)), "99 reputation is not 100");
                s.Standing.AddReputation(1000f);
                s.Ledger.Record(s.Now, LedgerKind.Other, 100000, "test money");
                foreach (var c in s.CountryList) if (!c.Owned) Assert.True(s.BuyHandbook(c.Id), $"buy {c.Id}");
                s.Tick(Bot.Step);
                book.Apply(t.Drain());
                Has(t, book, "rep and sections", A.RepWord, A.RepPro, A.RepLegend, A.Sections5, A.SectionsAll, A.Cash10K);
                Assert.Equal(10, book.Store.GetStat(A.StatSectionsBest), "10 sections");
            }

            // Heat: down from HIGH RISK to LOW PROFILE, but only after really being hot.
            {
                var s = new GameSession(8);
                var t = new AchievementTracker(s);
                s.Standing.AddHeat(50f);
                t.Poll();
                s.Standing.AddHeat(-40f);
                t.Poll();
                Assert.True(!t.Fired.Contains(A.LyingLow), "never HIGH RISK: no lying low");
                s.Standing.AddHeat(70f);
                t.Poll();
                s.Standing.AddHeat(-62f);
                t.Poll();
                Assert.True(t.Fired.Contains(A.LyingLow), $"heat {s.Standing.Heat:0}: lying low");
            }

            // Candlelight: hand over with the power out.
            {
                var s = new GameSession(12345);
                var t = new AchievementTracker(s);
                var (v, _) = FullSamirToBench(s, null, 2f);
                var doc = Bot.PrintAndBind(s, v.Order);
                s.InjectNews(new NewsItem { Effect = NewsEffect.PowerOutage, Magnitude = s.Clock.Minute, Days = 240 });
                Assert.True(!s.PowerOn, "power out");
                PayUp(s, v, doc);
                Assert.True(!s.PowerOn, "still out when paid");
                Assert.True(t.Fired.Contains(A.Candlelight), "candlelight");
            }

            // Not today: five walk-ins, no jobs.
            {
                var s = new GameSession(9);
                var t = new AchievementTracker(s);
                s.OpenShop();
                for (int i = 0; i < 5; i++)
                {
                    s.DebugSpawnWalkIn();
                    Bot.TickUntil(s, () => { DeclineAll(s, null); return s.Visits.Count == 0; }, 300f, "walk-in turned away");
                }
                Assert.True(s.CloseShop(), "close");
                Assert.True(s.LastReport.Customers >= 5 && s.LastReport.JobsAccepted == 0, "five customers, no jobs");
                Assert.True(t.Fired.Contains(A.NotToday), "not today");
                Assert.True(!t.Fired.Contains(A.FirstJob), "no job");
            }

            // Mix-up: someone else's booklet.
            {
                var s = new GameSession(10);
                var t = new AchievementTracker(s);
                s.OpenShop();
                var first = AcceptToBench(s, null);
                var second = AcceptToBench(s, first);
                var doc = Bot.PrintAndBind(s, second.Order);
                Assert.True(!t.Fired.Contains(A.MixUp), "not yet");
                Bot.TickUntil(s, () => { DeclineAll(s, first, second); return s.GiveDocument(first.Id, doc.Id); }, 60f, "hand over the wrong one");
                Bot.TickUntil(s, () => { DeclineAll(s, first, second); return first.Holding != null || t.Fired.Contains(A.MixUp); }, 60f, "handed over");
                Assert.True(t.Fired.Contains(A.MixUp), "mix-up");
            }

            foreach (var l in lines) Console.WriteLine("    " + l);
        }

        static void Has(AchievementTracker t, AchievementBook book, string label, params string[] ids)
        {
            foreach (var id in ids)
                Assert.True(t.Fired.Contains(id) || book.Unlocked(A.Get(id)), $"{label}: {id} (fired [{Fired(t)}])");
        }

        // ------------------------------------------------------------------ Book rules

        public static void Ach_BookRules()
        {
            var store = new MemoryAchievementStore();
            var book = new AchievementBook(store, false);
            var jobs25 = A.Get(A.Jobs25);
            var jobs100 = A.Get(A.Jobs100);
            int unlocks25 = 0;
            for (int i = 0; i < 30; i++)
                unlocks25 += book.Apply(new[] { AchievementSignal.Add(A.StatJobsDone, 1) }).Count(u => u.Def == jobs25);
            Assert.Equal(1, unlocks25, "JOBS_25 once");
            Assert.True(book.Unlocked(jobs25) && !book.Unlocked(jobs100), "25 not 100");
            Assert.Equal(30, book.Value(jobs100), "progress value");

            var again = new AchievementBook(store, false);
            var news = again.Apply(Enumerable.Repeat(AchievementSignal.Add(A.StatJobsDone, 1), 70));
            Assert.True(news.Count == 1 && news[0].Def == jobs100 && again.Unlocked(jobs100), "a new Book on the same store continues to 100");

            again.Apply(new[] { AchievementSignal.Max(A.StatRepBest, 120) });
            again.Apply(new[] { AchievementSignal.Max(A.StatRepBest, 40) });
            Assert.Equal(120, store.GetStat(A.StatRepBest), "Max never lowers");
            Assert.True(again.Unlocked(A.Get(A.RepWord)), "REP_WORD from the stat");

            var sig = new[] { AchievementSignal.Unlock(A.FirstPhoto), AchievementSignal.Unlock(A.MixUp), AchievementSignal.Unlock("NOT_A_THING") };
            Assert.Equal(2, again.Apply(sig).Count, "unlocks (unknown ids ignored)");
            Assert.Equal(0, again.Apply(sig).Count, "idempotent");

            // Demo: full-game events are banked, progress never is, demo ones unlock.
            var demoStore = new MemoryAchievementStore();
            var demo = new AchievementBook(demoStore, true);
            var d = demo.Apply(new[]
            {
                AchievementSignal.Unlock(A.FirstJob), AchievementSignal.Unlock(A.SamirCaught),
                AchievementSignal.Add(A.StatJobsDone, 30), AchievementSignal.Unlock(A.TopShelf),
            });
            Assert.True(demo.Unlocked(A.Get(A.FirstJob)) && !demo.Banked(A.Get(A.FirstJob)), "demo achievement unlocks");
            Assert.True(demo.Banked(A.Get(A.SamirCaught)) && !demo.Unlocked(A.Get(A.SamirCaught)), "full-game event banked");
            Assert.True(!demo.Banked(jobs25) && !demo.Unlocked(jobs25), "progress never banked");
            Assert.Equal(30, demoStore.GetStat(A.StatJobsDone), "stat still counted");
            Assert.Equal(3, d.Count, "first job + 2 banked");
            Assert.Equal(2, d.Count(u => u.Banked), "two banked");
            Assert.Equal(0, demo.Apply(new[] { AchievementSignal.Unlock(A.SamirCaught) }).Count, "banked once");
            Assert.Equal(1, demo.CountUnlocked(true), "demo count");
            Assert.Equal(0, demo.Import(store).Count, "the demo never imports");

            // Import into the full game: unlocked and banked, plus the stats ("every run counts").
            var fullStore = new MemoryAchievementStore();
            var full = new AchievementBook(fullStore, false);
            fullStore.SetStat(A.StatJobsDone, 4);
            fullStore.SetStat(A.StatRepBest, 50);
            demoStore.SetStat(A.StatRepBest, 20);
            var imported = full.Import(demoStore);
            Assert.Equal(4, imported.Count, "three imported + JOBS_25 from the carried stat");
            Assert.True(imported.All(u => !u.Banked), "imports are unlocks");
            foreach (var id in new[] { A.FirstJob, A.SamirCaught, A.TopShelf }) Assert.True(full.Unlocked(A.Get(id)), $"{id} imported");
            Assert.Equal(34, fullStore.GetStat(A.StatJobsDone), "demo deliveries add to the lifetime count");
            Assert.Equal(50, fullStore.GetStat(A.StatRepBest), "a best keeps the higher value");
            Assert.True(full.Unlocked(jobs25), "carried stat unlocks progress");
            Assert.Equal(0, full.Import(demoStore).Count, "second import: nothing new");
            Assert.Equal(0, full.Apply(new[] { AchievementSignal.Unlock(A.FirstJob) }).Count, "already unlocked");
        }

        // ------------------------------------------------------------------ the 20-day bot (copy of Session_TwentyDays_Bot)

        static List<string> TwentyDays(bool track, out AchievementTracker tracker, out AchievementBook book)
        {
            var digest = new List<string>();
            var s = new GameSession(2024);
            tracker = track ? new AchievementTracker(s) : null;
            book = new AchievementBook(new MemoryAchievementStore(), false);
            var rng = new Rng(77);
            var printedFor = new HashSet<int>();
            int handed = 0, photos = 0, choices = 0;
            ulong eventHash = 14695981039346656037UL;
            int events = 0;
            void Hash(string text)
            {
                foreach (char ch in text)
                {
                    eventHash ^= ch;
                    eventHash *= 1099511628211UL;
                }
            }
            for (int day = 1; day <= 20; day++)
            {
                if (day > 1) s.StartNextDay();
                s.OpenShop();
                foreach (var c in s.CountryList)
                    if (!c.Owned && s.Standing.Reputation >= c.Spec.UnlockReputation && s.Ledger.Cash > c.Spec.HandbookPrice + 300)
                        s.BuyHandbook(c.Id);

                float guard = 0f;
                while (true)
                {
                    foreach (var v in s.Visits.ToArray())
                    {
                        if (v.Phase == VisitPhase.Choosing)
                        {
                            choices++;
                            s.Choose(v.Id, PickChoice(v, rng));
                        }
                        else if (v.Phase == VisitPhase.PhotoSitting && !v.Moving && s.ActivePhoto == null && !v.IsTalking)
                        {
                            Bot.TakeGoodPhoto(s, v, careful: rng.Chance(0.8f));
                            photos++;
                            if (!s.FinishPhotoSitting(v.Id)) throw new AssertException("could not finish sitting after keeping a photo");
                        }
                        else if (v.Phase == VisitPhase.AwaitingHandover && !v.Moving)
                        {
                            var doc = s.DeskDocuments.Find(dd => dd.OrderId == v.Order.Id);
                            if (doc != null && s.GiveDocument(v.Id, doc.Id)) handed++;
                        }
                        else if (v.Phase == VisitPhase.Waiting && !v.Moving && v.Order != null && s.CounterVisit == null)
                        {
                            var doc = s.DeskDocuments.Find(dd => dd.OrderId == v.Order.Id);
                            if (doc != null && s.GiveDocument(v.Id, doc.Id)) handed++;
                        }
                    }
                    foreach (var o in s.Orders.Open().ToList())
                    {
                        if (o.Status != OrderStatus.InProduction || printedFor.Contains(o.Id)) continue;
                        var photo = s.Photos.ForCustomer(o.CustomerId).LastOrDefault();
                        if (photo == null) continue;
                        Bot.FillPerfect(s, o, photo);
                        if (rng.Chance(0.2f)) o.Draft.Set(DocField.Surname, o.Draft.Get(DocField.Surname) + "X");
                        s.PrintDraft(o.Id);
                        printedFor.Add(o.Id);
                    }
                    foreach (var p in s.Printer.Tray.ToList()) s.BindPage(p.Id);
                    if (s.Printer.Jammed) s.ClearPrinterJam();
                    foreach (var cash in s.CounterCash.ToList()) s.CollectCash(cash.Id);
                    foreach (var e in s.DrainEvents())
                    {
                        Hash(e.ToString());
                        events++;
                    }

                    if (s.Phase == DayPhase.AfterHours && s.CloseShop()) break;
                    if (s.Phase == DayPhase.Closed) break;
                    s.Tick(Bot.Step);
                    guard += Bot.Step;
                    if (guard > 4000f) throw new AssertException($"day {day} stuck: {s.Phase} {s.Now}");
                }
                foreach (var e in s.DrainEvents())
                {
                    Hash(e.ToString());
                    events++;
                }
                if (tracker != null) book.Apply(tracker.Drain());
                var r = s.LastReport;
                digest.Add($"day {r.Day,2}: cust {r.Customers} acc {r.JobsAccepted} done {r.JobsCompleted} open {r.OrdersOpen} " +
                    $"rev {r.Revenue,5} exp {r.Expenses,4} cash {r.CashEnd,6} rep {r.ReputationEnd,5:0} heat {r.HeatEnd,4:0} msgs {r.MessagesReceived}");
            }
            int successes = s.Orders.All.Count(o => o.Outcome == OutcomeKind.Success);
            int failures = s.Orders.All.Count(o => o.Outcome == OutcomeKind.Failed);
            digest.Add($"totals: orders {s.Orders.All.Count}, handed {handed}, photos {photos}, choices {choices}, " +
                $"success {successes}, failed {failures}, inbox {s.Inbox.Messages.Count}, clients {s.Clients.Count}, owned " +
                string.Join("/", s.CountryList.Where(c => c.Owned).Select(c => c.Id)));
            digest.Add($"clients {s.Clients.Count} photos {s.Photos.All.Count()} inbox {s.Inbox.Messages.Count} ledger {s.Ledger.Entries.Count}");
            digest.Add($"events {events} hash {eventHash:x16}");
            digest.Add($"news {s.News.Count} visits {s.Visits.Count} flags {s.HasFlag("brother_scheduled")}");
            digest.Add($"rng {s.Rng.NextUInt()}");
            return digest;
        }

        static string PickChoice(Visit v, Rng rng)
        {
            bool Has(string id) => v.Choices.Exists(c => c.Id == id && c.Enabled);
            if (Has(C.Accept)) return rng.Chance(0.3f) && Has(C.AskSmall) ? C.AskSmall : C.Accept;
            if (Has(C.TakePhoto)) return C.TakePhoto;
            if (Has(C.WaitBench)) return C.WaitBench;
            if (Has(C.TrustMe)) return rng.Chance(0.5f) ? C.TrustMe : C.FixIt;
            if (Has(C.FixIt)) return C.FixIt;
            if (Has(C.GiveHour)) return C.GiveHour;
            if (Has(C.ComeTomorrow)) return C.ComeTomorrow;
            if (Has(C.AlmostDone)) return C.AlmostDone;
            return v.Choices.First(c => c.Enabled).Id;
        }

        // ------------------------------------------------------------------ the demo player (after DemoTests.PlayDemo)

        sealed class Plan
        {
            public bool Track = true;
            public int Days = 2;
            public bool BuyFranceUpdate = true;
            public bool BuyGermanyMorning;
            /// <summary>Day 1 by hand: haggle with Samir once, then call him back to the stool three times.</summary>
            public bool DesignedDayOne;
            public bool HaggleElif;
            public bool BuyStationAfterViktor;
            public bool BuyGermanyWithAnja;
            /// <summary>Day 1: don't close after Samir; jump to 22:59 and let the clock run out.</summary>
            public bool NightShift;
        }

        sealed class DemoRun
        {
            public GameSession S;
            public AchievementTracker Tracker;
            public readonly List<AchievementSignal> Signals = new List<AchievementSignal>();
            public readonly List<string> Days = new List<string>();
            public readonly List<string> Events = new List<string>();

            public List<string> Digest()
            {
                var s = S;
                var d = new List<string>(Days);
                d.Add($"clients {s.Clients.Count} photos {s.Photos.All.Count()} inbox {s.Inbox.Messages.Count} ledger {s.Ledger.Entries.Count} cash {s.Ledger.Cash}");
                d.Add($"rep {s.Standing.Reputation:0.000} heat {s.Standing.Heat:0.000} orders {s.Orders.All.Count} news {s.News.Count}");
                d.AddRange(Events);
                d.Add($"rng {s.Rng.NextUInt()}");
                return d;
            }
        }

        static DemoRun PlayDemo(ulong seed, Plan plan)
        {
            var run = new DemoRun { S = NewDemo(seed) };
            var s = run.S;
            if (plan.Track) run.Tracker = new AchievementTracker(s);
            var printedFor = new HashSet<int>();
            var haggled = new HashSet<int>();
            bool stationBought = false, jumped = false;

            void Drain()
            {
                foreach (var e in s.DrainEvents()) run.Events.Add(e.ToString());
                if (run.Tracker != null) run.Signals.AddRange(run.Tracker.Drain());
            }

            string Pick(Visit v)
            {
                bool Has(string id) => v.Choices.Exists(c => c.Id == id && c.Enabled);
                if (plan.HaggleElif && v.Customer.Id == DemoCast.ElifId && Has(C.AskSmall) && haggled.Add(v.Id)) return C.AskSmall;
                if (Has(C.Accept)) return C.Accept;
                if (Has(C.TakePhoto)) return C.TakePhoto;
                if (Has(C.WaitBench)) return C.WaitBench;
                if (Has(C.TrustMe)) return C.TrustMe;
                if (Has(C.FixIt)) return C.FixIt;
                if (Has(C.AlmostDone)) return C.AlmostDone;
                return v.Choices.First(c => c.Enabled).Id;
            }

            for (int day = 1; day <= plan.Days; day++)
            {
                if (day == 2)
                {
                    s.StartNextDay();
                    if (plan.BuyFranceUpdate) Assert.True(s.BuyHandbookUpdate(Countries.France), "France update");
                    if (plan.BuyGermanyMorning) Assert.True(s.BuyHandbook(Countries.Germany), "buy Germany");
                }
                if (day == 1 && plan.DesignedDayOne)
                {
                    DesignedDayOne(s, run);
                    Drain();
                    run.Days.Add(DayLine(s));
                    continue;
                }
                s.OpenShop();
                float guard = 0f;
                while (s.Phase != DayPhase.Closed)
                {
                    Drain();
                    if (plan.BuyGermanyWithAnja && !s.Country(Countries.Germany).Owned)
                    {
                        var anja = s.Visits.Find(x => x.Customer.Id == DemoCast.AnjaId && x.Phase == VisitPhase.Choosing && Ids(x) == C.Decline);
                        if (anja != null) Assert.True(s.BuyHandbook(Countries.Germany), "buy Germany with Anja at the counter");
                    }
                    if (plan.BuyStationAfterViktor && !stationBought &&
                        s.Orders.All.Any(o => o.CustomerId == DemoCast.ViktorId && o.Status == OrderStatus.Delivered))
                    {
                        Assert.Equal(350, s.PriceOf(EquipmentCatalog.Get("stn2")), "stn2 on sale");
                        Assert.True(s.BuyEquipment("stn2"), "buy stn2");
                        stationBought = true;
                    }
                    foreach (var v in s.Visits.ToArray())
                    {
                        if (v.Phase == VisitPhase.Choosing)
                        {
                            s.Choose(v.Id, Pick(v));
                        }
                        else if (v.Phase == VisitPhase.PhotoSitting && !v.Moving && s.ActivePhoto == null && !v.IsTalking)
                        {
                            Bot.TakeGoodPhoto(s, v);
                            Assert.True(s.FinishPhotoSitting(v.Id), "finish sitting");
                        }
                        else if ((v.Phase == VisitPhase.AwaitingHandover || (v.Phase == VisitPhase.Waiting && s.CounterVisit == null)) && !v.Moving && v.Order != null)
                        {
                            var doc = s.DeskDocuments.Find(dd => dd.OrderId == v.Order.Id);
                            if (doc != null) s.GiveDocument(v.Id, doc.Id);
                        }
                    }
                    foreach (var o in s.Orders.Open().ToList())
                    {
                        if (o.Status != OrderStatus.InProduction || printedFor.Contains(o.Id)) continue;
                        var photo = s.Photos.ForCustomer(o.CustomerId).LastOrDefault();
                        if (photo == null) continue;
                        Bot.FillPerfect(s, o, photo);
                        s.PrintDraft(o.Id);
                        printedFor.Add(o.Id);
                    }
                    foreach (var p in s.Printer.Tray.ToList()) s.BindPage(p.Id);
                    if (s.Printer.Jammed) s.ClearPrinterJam();
                    foreach (var cash in s.CounterCash.ToList()) s.CollectCash(cash.Id);

                    bool delivered = s.Orders.All.Any(o => o.Status == OrderStatus.Delivered);
                    if (day == 1 && plan.NightShift)
                    {
                        if (delivered && s.Visits.Count == 0 && !jumped)
                        {
                            jumped = true;
                            s.Clock.Minute = GameSession.ForcedCloseMinute - 1;
                        }
                    }
                    else
                    {
                        bool told = day == 1 ? delivered : s.Inbox.Messages.Any(m => m.Body == DemoCast.QuietBody);
                        if (told && s.CloseShop()) break;
                    }

                    s.Tick(Bot.Step);
                    guard += Bot.Step;
                    if (guard > 3000f) throw new AssertException($"demo day {day} stuck: {s.Phase} {s.Now} " +
                        string.Join(",", s.Visits.Select(x => $"{x.Customer.FirstName}:{x.Phase}:{Ids(x)}")));
                }
                Drain();
                Assert.Equal(day, s.LastReport.Day, $"day {day} report");
                run.Days.Add(DayLine(s));
            }
            if (plan.BuyStationAfterViktor) Assert.True(stationBought, "stn2 bought");
            return run;
        }

        static string DayLine(GameSession s)
        {
            var r = s.LastReport;
            return $"day {r.Day}: cust {r.Customers} acc {r.JobsAccepted} done {r.JobsCompleted} open {r.OrdersOpen} " +
                $"rev {r.Revenue} exp {r.Expenses} cash {r.CashEnd} rep {r.ReputationEnd:0.000} heat {r.HeatEnd:0.000} msgs {r.MessagesReceived} at {s.Now}";
        }

        /// <summary>Haggle with Samir once, three call-backs for another photo, then deliver and close.</summary>
        static void DesignedDayOne(GameSession s, DemoRun run)
        {
            var v = DemoSamirToBench(s, true, 3, haggle: true);
            var last = s.Photos.ForCustomer(v.Customer.Id).OrderBy(p => p.Id).Last();
            v.Order.Draft.PhotoId = last.Id;
            var doc = Bot.PrintAndBind(s, v.Order);
            PayUp(s, v, doc);
            foreach (var cash in s.CounterCash.ToList()) s.CollectCash(cash.Id);
            Bot.TickUntil(s, () => s.Visits.Count == 0, 60f, "Samir leaves");
            Assert.True(s.CloseShop(), "close early");
        }

        /// <summary>Demo Day 1: Samir accepted (maybe after one ask), photographed, called back <paramref name="retakes"/> times, filled.</summary>
        static Visit DemoSamirToBench(GameSession s, bool careful, int retakes, bool haggle = false)
        {
            s.OpenShop();
            var v = Bot.WaitForChoice(s, C.Accept, 300f);
            Assert.Equal(StoryCast.SamirId, v.Customer.Id, "Samir");
            if (haggle)
            {
                s.Choose(v.Id, C.AskSmall);
                Bot.TickUntil(s, () => v.Phase == VisitPhase.Choosing && Ids(v) == C.Accept, 60f, "Samir's final offer");
            }
            s.Choose(v.Id, C.Accept);
            Bot.WaitForChoice(s, C.TakePhoto, 60f);
            s.Choose(v.Id, C.TakePhoto);
            Bot.TickUntil(s, () => v.Phase == VisitPhase.PhotoSitting && !v.Moving, 30f, "seated");
            var photo = Bot.TakeGoodPhoto(s, v, careful);
            Assert.True(s.FinishPhotoSitting(v.Id), "gets up");
            Bot.TickUntil(s, () => v.Phase == VisitPhase.Waiting && !v.Moving, 40f, "bench");
            for (int i = 0; i < retakes; i++)
            {
                Assert.True(s.CallBackForPhoto(v.Id), $"call back #{i + 1}");
                Bot.TickUntil(s, () => v.Phase == VisitPhase.PhotoSitting && !v.Moving, 30f, "back on the stool");
                Bot.TickUntil(s, () => !v.IsTalking, 20f, "done talking");
                photo = Bot.TakeGoodPhoto(s, v, careful);
                Assert.True(s.FinishPhotoSitting(v.Id), "gets up again");
                Bot.TickUntil(s, () => v.Phase == VisitPhase.Waiting && !v.Moving, 40f, "bench again");
            }
            Assert.Equal(retakes, v.Retakes, "call-backs counted");
            Bot.FillPerfect(s, v.Order, photo);
            return v;
        }

        // ------------------------------------------------------------------ full-game helpers

        /// <summary>Turn away everyone at the counter except the given visits (and Samir, the probes' customer).</summary>
        static void DeclineAll(GameSession s, params Visit[] keep)
        {
            foreach (var x in s.Visits.ToArray())
            {
                if (keep != null && keep.Contains(x)) continue;
                if (x.Customer.Id == StoryCast.SamirId && (keep == null || !keep.Any(k => k != null))) continue;
                s.SkipLine(x.Id);
                if (x.Phase == VisitPhase.Choosing && x.Choices.Exists(c => c.Id == C.Decline)) s.Choose(x.Id, C.Decline);
            }
        }

        static void TickUntil(GameSession s, Visit keep, Func<bool> cond, float max, string what) =>
            Bot.TickUntil(s, () =>
            {
                DeclineAll(s, keep);
                return cond();
            }, max, what);

        /// <summary>Full game: Samir accepted, photographed carefully (zoom capped), on the bench, draft filled.</summary>
        static (Visit, PhotoCapture) FullSamirToBench(GameSession s, Backdrop? backdrop, float maxZoom)
        {
            if (s.Phase == DayPhase.Morning) s.OpenShop();
            Visit v = null;
            TickUntil(s, null, () =>
            {
                v = s.Visits.Find(x => x.Customer.Id == StoryCast.SamirId && x.Phase == VisitPhase.Choosing && x.Choices.Exists(c => c.Id == C.Accept));
                return v != null;
            }, 600f, "Samir's offer");
            s.Choose(v.Id, C.Accept);
            TickUntil(s, v, () => v.Phase == VisitPhase.Choosing && v.Choices.Exists(c => c.Id == C.TakePhoto && c.Enabled), 60f, "photo offer");
            s.Choose(v.Id, C.TakePhoto);
            TickUntil(s, v, () => v.Phase == VisitPhase.PhotoSitting && !v.Moving, 30f, "seated");
            TickUntil(s, v, () => !v.IsTalking, 20f, "quiet");
            var photo = TakePhoto(s, v, backdrop, maxZoom);
            Assert.True(s.FinishPhotoSitting(v.Id), "gets up");
            TickUntil(s, v, () => v.Phase == VisitPhase.Waiting && !v.Moving, 40f, "bench");
            Bot.FillPerfect(s, v.Order, photo);
            return (v, photo);
        }

        /// <summary>Hand over and see it through to payment (TRUST ME first, FIX IT and hand over again otherwise).</summary>
        static void PayUp(GameSession s, Visit v, FinishedDocument doc)
        {
            TickUntil(s, v, () => s.GiveDocument(v.Id, doc.Id), 60f, "hand over");
            TickUntil(s, v, () =>
            {
                if (v.Phase == VisitPhase.Choosing && v.Choices.Exists(c => c.Id == C.TrustMe)) s.Choose(v.Id, C.TrustMe);
                else if (v.Phase == VisitPhase.Choosing && v.Choices.Exists(c => c.Id == C.FixIt)) s.Choose(v.Id, C.FixIt);
                else if (v.Phase == VisitPhase.Waiting && !v.Moving && s.DeskDocuments.Contains(doc)) s.GiveDocument(v.Id, doc.Id);
                return v.Order.Status == OrderStatus.Delivered;
            }, 300f, "paid");
        }

        /// <summary>A walk-in (or Samir) who takes a deal and waits on the bench without a photo.</summary>
        static Visit AcceptToBench(GameSession s, Visit already)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (s.Visits.All(x => x == already)) s.DebugSpawnWalkIn();
                Visit v = null;
                Bot.TickUntil(s, () =>
                {
                    foreach (var x in s.Visits) if (x != already) s.SkipLine(x.Id);
                    v = s.Visits.Find(x => x != already && x.Phase == VisitPhase.Choosing);
                    if (v != null && !v.Choices.Exists(c => c.Id == C.Accept)) { s.Choose(v.Id, C.Decline); v = null; }
                    return v != null || s.Visits.All(x => x == already);
                }, 300f, "someone to take a deal");
                if (v == null) continue;
                s.Choose(v.Id, C.Accept);
                var keep = v;
                Bot.TickUntil(s, () =>
                {
                    DeclineAll(s, keep, already);
                    return keep.Phase == VisitPhase.Choosing && keep.Choices.Exists(c => c.Id == C.WaitBench);
                }, 60f, "photo or bench");
                if (!keep.Choices.Exists(c => c.Id == C.WaitBench && c.Enabled)) continue;
                s.Choose(keep.Id, C.WaitBench);
                Bot.TickUntil(s, () => { DeclineAll(s, keep, already); return keep.Phase == VisitPhase.Waiting && !keep.Moving; }, 60f, "on the bench");
                return keep;
            }
            throw new AssertException("nobody took a deal");
        }

        static void EndDayAt2300(GameSession s)
        {
            s.Clock.Minute = GameSession.ForcedCloseMinute - 1;
            Bot.TickUntil(s, () => s.Phase == DayPhase.Closed, 200f, "23:00");
        }

        /// <summary>Bot.TakeGoodPhoto with the zoom capped (the 3x camera can't fit the 35:45 crop at full zoom).</summary>
        static PhotoCapture TakePhoto(GameSession s, Visit v, Backdrop? backdrop, float maxZoom)
        {
            var session = s.BeginPhotoSession(v.Id);
            Assert.True(session != null, "photo session should start");
            var rules = s.Country(v.Order.CountryId).Spec.Photo;
            var subj = session.Subject;
            int guard = 0;
            while (subj.HasGlasses && (!rules.GlassesAllowed || subj.WearingSunglasses) && guard++ < 4)
                subj.Command(PhotoCommand.RemoveGlasses);
            if (subj.HatOn && !rules.HeadwearAllowed) subj.Command(PhotoCommand.RemoveHat);
            session.PullBackdrop(backdrop ?? rules.Backdrop);
            Bot.TickUntil(s, () => !session.Rig.SwappingBackdrop, 10f, "backdrop swap");
            while (session.Rig.Zoom < Math.Min(s.Camera.MaxZoom, maxZoom) - 0.001f) session.ZoomBy(0.25f);

            PhotoCapture shot = null;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                if (!rules.ExpressionOk(subj.Expression)) subj.Command(PhotoCommand.NeutralFace);
                subj.Command(PhotoCommand.HoldStill);
                subj.BeginDrag();
                subj.DragTo(0, 0);
                Bot.TickUntil(s, () => subj.Pose == HeadPose.Center, 3f, "pose centered");
                subj.EndDrag();
                Bot.Tick(s, 0.3f);
                if (Math.Abs(subj.GazeX) > 0.2f || Math.Abs(subj.GazeY) > 0.2f)
                {
                    subj.Command(PhotoCommand.LookAtCamera);
                    Bot.Tick(s, 0.4f);
                }
                var head = PortraitGeometry.HeadWorld(subj.Customer.Look, subj.Pose, subj.SeatOffset, subj.Slouch, subj.HatOn);
                session.Rig.PanX = head.CenterX;
                session.Rig.PanY = head.CenterY + 0.25f;
                session.Rig.ClampTo(s.Camera);
                if (subj.Customer.Behavior.LidDroop > 0f || subj.Expression == Expression.Tired)
                    subj.Command(PhotoCommand.OpenEyes);
                Bot.TickUntil(s, () => subj.Lid < 0.2f && !subj.InMotion, 10f, "eyes open");
                session.PressShutter();
                Bot.TickUntil(s, () => session.JustCaptured != null, 2f, "capture");
                shot = session.TakeJustCaptured();
                bool good = !shot.EyesClosed && !shot.EyesHalfClosed && shot.Pose == HeadPose.Center && !shot.Blurred
                    && rules.ExpressionOk(shot.Expression) && Math.Abs(shot.GazeX) < 0.3f && !shot.BackdropMoving;
                if (good) break;
            }
            var h = shot.HeadInFrame;
            float target = (rules.CoverageMin + rules.CoverageMax) * 0.5f;
            float cropH = h.H / target;
            float top = h.Y - 0.1f * cropH;
            shot.Crop = PortraitGeometry.MakeCrop(h.CenterX, top + cropH * 0.5f, cropH);
            s.KeepPhoto(shot);
            return shot;
        }
    }
}
