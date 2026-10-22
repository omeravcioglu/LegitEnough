using System;
using System.Collections.Generic;
using System.Linq;
using LegitEnough.Sim;
using C = LegitEnough.Sim.GameSession.ChoiceIds;

namespace LegitEnough.SimTests
{
    /// <summary>The demo edition: locks, the guided Day 1 and the scripted Day 2.</summary>
    public static class DemoTests
    {
        static GameSession NewDemo(ulong seed) => new GameSession(seed, GameRules.Demo());

        static string Ids(Visit v) => string.Join(",", v.Choices.Select(c => c.Id));

        // ------------------------------------------------------------------ (a) countries

        public static void Demo_OnlyDemoCountriesRequested()
        {
            // Untutored demo so Samir can be turned away like everyone else.
            var s = new GameSession(31, new GameRules { Edition = Edition.Demo, MaxEquipmentTier = 2, Tutorial = false });
            Assert.True(s.IsDemo && !new GameSession(31).IsDemo, "edition flag");
            foreach (var c in s.CountryList)
            {
                bool demo = Array.IndexOf(GameRules.DemoCountries, c.Id) >= 0;
                Assert.Equal(!demo, c.FullGameOnly, $"{c.Id} locked flag");
                Assert.Equal(!demo, s.IsCountryLocked(c.Id), $"{c.Id} IsCountryLocked");
                Assert.True(!c.Requestable(1000f) || demo, $"{c.Id} never requestable");
            }
            Assert.Equal(10, s.CountryList.Count, "all ten countries stay in the session");
            Assert.Equal(0, s.Country(Countries.Germany).Spec.UnlockReputation, "Germany tuned");
            Assert.Equal(250, s.Country(Countries.Germany).Spec.HandbookPrice, "Germany price");
            Assert.Equal(20, s.Country(Countries.Italy).Spec.UnlockReputation, "Italy tuned");
            Assert.Equal(35, s.Country(Countries.UnitedStates).Spec.UnlockReputation, "US tuned");
            Assert.Equal(35, new GameSession(31).Country(Countries.Germany).Spec.UnlockReputation, "full game untouched");

            // Famous shop: every demo country is on the table, the locked ones never are.
            s.Standing.AddReputation(600f);
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < 150; i++)
            {
                s.Clock.Minute = 10 * 60; // keep the day young
                s.DebugSpawnWalkIn();
                var v = s.Visits[s.Visits.Count - 1];
                Bot.TickUntil(s, () =>
                {
                    foreach (var x in s.Visits.ToArray())
                    {
                        s.SkipLine(x.Id);
                        if (x.Phase == VisitPhase.Choosing) s.Choose(x.Id, C.Decline);
                    }
                    return !s.Visits.Contains(v);
                }, 200f, "walk-in asks and leaves");
                Assert.True(v.Request != null && v.Kind == ArrivalKind.NewJob, "walk-in made a request");
                string id = v.Request.CountryId;
                Assert.True(!s.IsCountryLocked(id), $"walk-in asked for locked {id}");
                seen.TryGetValue(id, out int n);
                seen[id] = n + 1;
            }
            Console.WriteLine("    150 walk-ins asked for: " + string.Join(" ", seen.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")));
            Assert.True(seen.Count == GameRules.DemoCountries.Length, "all five demo countries get asked for");

            // Morning news never covers a locked country either.
            s.Clock.Day = 2;
            var rng = new Rng(5);
            int stories = 0;
            for (int i = 0; i < 400; i++)
                foreach (var n in NewsDesk.Morning(s, rng))
                {
                    if (n.CountryId == null) continue;
                    stories++;
                    Assert.True(!s.IsCountryLocked(n.CountryId), $"news about locked {n.CountryId}");
                }
            Assert.True(stories > 100, "country stories were written");
        }

        // ------------------------------------------------------------------ (b) shop locks, Day 2 gift

        public static void Demo_LocksAndDayTwoMorning()
        {
            var s = NewDemo(32);
            s.Standing.AddReputation(1000f);
            s.Ledger.Record(s.Now, LedgerKind.Other, 100000, "test money");
            foreach (var c in s.CountryList)
            {
                if (c.Owned) continue;
                bool bought = s.BuyHandbook(c.Id);
                Assert.Equal(!c.FullGameOnly, bought, $"handbook {c.Id}");
            }
            foreach (var e in EquipmentCatalog.All)
            {
                if (e.Tier < 2) continue;
                Assert.Equal(e.Tier >= 3, s.EquipmentLocked(e), $"{e.Id} locked");
                bool bought = s.BuyEquipment(e.Id);
                Assert.Equal(e.Tier == 2, bought, $"buy {e.Id}");
            }
            var full = new GameSession(32);
            full.Ledger.Record(full.Now, LedgerKind.Other, 100000, "test money");
            Assert.True(!full.EquipmentLocked(EquipmentCatalog.Get("cam3")) && full.BuyEquipment("cam3"), "full game sells tier 3");

            // Day 2 morning: Turkey is a gift, R. writes, the paper is fixed.
            var d = NewDemo(33);
            Assert.True(d.TutorialActive, "Day 1 is the tutorial");
            Assert.True(!d.Country(Countries.Turkey).Owned, "Turkey not owned on Day 1");
            Assert.True(!d.BuyHandbook(Countries.Turkey), "Turkey not for sale yet on Day 1");
            Assert.True(d.CloseShop(), "skip Day 1");
            d.StartNextDay();
            Assert.Equal(2, d.Day, "Day 2");
            Assert.True(!d.TutorialActive, "tutorial over");
            Assert.True(d.Country(Countries.Turkey).Owned, "Turkey owned on Day 2");
            var note = d.Inbox.Messages.FirstOrDefault(m => m.Subject == DemoCast.DayTwoSubject);
            Assert.True(note != null && note.From == "R." && note.Body.Length <= 420, "R.'s Day 2 note");
            var news = d.NewsFor(2);
            Console.WriteLine("    day 2 news: " + string.Join(" | ", news.Select(n => n.Headline)));
            Assert.Equal(3, news.Count, "three headlines");
            var fr = d.Country(Countries.France);
            Assert.True(fr.HandbookOutdated, "France handbook outdated");
            Assert.Equal(Backdrop.White, fr.Spec.Photo.Backdrop, "France now wants white");
            Assert.Equal(Backdrop.LightGray, fr.HandbookSpec.Photo.Backdrop, "old handbook keeps light gray");
            Assert.True(fr.Spec.Notes[0].StartsWith("WHITE") && fr.HandbookSpec.Notes[0].StartsWith("Gray"), "the binder note follows the update");
            Assert.Near(0.60f, fr.Spec.Photo.CoverageMin, 0.001f, "coverage unchanged");
            Assert.True(news[0].Headline.Contains("WHITE") && news[0].Body.Contains("gray"), "the paper states the new rule");
            Console.WriteLine($"    rule change: {fr.LastRuleChange}");
            Assert.True(d.SaleActive && d.PriceOf(EquipmentCatalog.Get("prn2")) == 450 && d.SaleDiscountPercent == 50, "50% sale");
            Assert.True(d.Inbox.Messages.All(m => m.From != "R." || m.Subject == m.Subject.ToLowerInvariant()), "R.'s subjects are lowercase");
            Assert.True(d.PowerOn && !d.InternetDown && !d.PrinterBroken, "no outages in the demo");
            Assert.True(!d.Director.Upcoming.Any(a => a.Kind == ArrivalKind.NewJob), "no random walk-ins");
            Assert.Equal(4, d.Director.Upcoming.Count(a => DemoCast.IsDemoBeat(a.Beat)), "four scripted visitors");
        }

        // ------------------------------------------------------------------ (c) Samir can't be lost

        public static void Demo_TutorialSamirRestricted()
        {
            var s = NewDemo(34);
            s.OpenShop();
            var v = Bot.WaitForChoice(s, C.Accept, 300f);
            Assert.Equal(StoryCast.SamirId, v.Customer.Id, "Samir first");
            Assert.Equal("accept,ask_small", Ids(v), "first offer: take it or ask once");
            s.Choose(v.Id, C.Decline);
            s.Choose(v.Id, C.AskBig);
            Assert.Equal(VisitPhase.Choosing, v.Phase, "missing replies do nothing");
            s.Choose(v.Id, C.AskSmall);
            Bot.WaitForChoice(s, C.Accept, 60f);
            Assert.Equal("accept", Ids(v), "after the counter only the final offer");
            Assert.Equal(Lines.Player.AcceptFinal(450), v.Choices[0].Text, "Fine. $450.");
            s.Choose(v.Id, C.Push);
            Assert.Equal(VisitPhase.Choosing, v.Phase, "can't push");
            s.Choose(v.Id, C.Accept);
            Bot.WaitForChoice(s, C.TakePhoto, 60f);
            Assert.Equal(450, v.Order.Price, "deal at $450");

            // Out of patience: he grumbles but stays.
            v.Patience = -10f;
            Bot.Tick(s, 5f);
            Assert.True(!v.Leaving && s.Visits.Contains(v), "Samir never storms out of the tutorial");

            // The full game keeps every reply.
            var f = new GameSession(34);
            f.OpenShop();
            var fv = Bot.WaitForChoice(f, C.Accept, 300f);
            Assert.Equal("accept,ask_small,ask_big,decline", Ids(fv), "full game choices");
        }

        // ------------------------------------------------------------------ (d) the teaching jam

        public static void Demo_FirstPrintJamsCleanly()
        {
            Assert.True(!new GameSession(1).Printer.ForceJamNextPage, "full game: no scripted jam");
            for (ulong seed = 40; seed < 48; seed++)
            {
                var s = NewDemo(seed);
                var v = ServeSamirUntilPrint(s, true);
                var page = s.PrintDraft(v.Order.Id);
                Bot.TickUntil(s, () => s.Printer.Jammed, 30f, "first page jams");
                Assert.Near(0.4f, s.Printer.Progress, 0.001f, "jams at 40%");
                Assert.True(s.Printer.SafeJam, "tutorial jam");
                s.ClearPrinterJam();
                Assert.True(!s.Printer.Jammed && !s.Printer.SafeJam, "cleared");
                Bot.TickUntil(s, () => s.Printer.Tray.Exists(p => p.Id == page.Id), 30f, "page in tray");
                Assert.True(!page.Smudged, $"seed {seed}: clearing the tutorial jam never smudges");
                Assert.True(!s.Printer.ForceJamNextPage, "only the first page");
            }
        }

        // ------------------------------------------------------------------ (e) Samir writes on Day 2

        public static void Demo_SamirOutcomeOnDayTwoCard()
        {
            int success = 0, failed = 0, fixOnly = 0;
            for (ulong seed = 50; seed < 62; seed++)
            {
                var s = NewDemo(seed);
                bool careful = seed % 3 != 0;
                var v = ServeSamirUntilPrint(s, careful);
                if (!careful) v.Order.Draft.Set(DocField.Surname, "HADAD");
                var doc = Bot.PrintAndBind(s, v.Order);
                Assert.True(s.GiveDocument(v.Id, doc.Id), "hand over");
                Bot.TickUntil(s, () =>
                {
                    if (v.Phase == VisitPhase.Choosing)
                    {
                        Assert.True(!v.Choices.Exists(c => c.Id == C.GetOut), $"seed {seed}: the tutorial never offers GET OUT");
                        if (Ids(v) == C.FixIt) fixOnly++;
                    }
                    if (v.Phase == VisitPhase.Choosing && v.Choices.Exists(c => c.Id == C.TrustMe)) s.Choose(v.Id, C.TrustMe);
                    else if (v.Phase == VisitPhase.Choosing && v.Choices.Exists(c => c.Id == C.FixIt)) s.Choose(v.Id, C.FixIt);
                    else if (v.Phase == VisitPhase.Waiting && !v.Moving && s.DeskDocuments.Contains(doc)) s.GiveDocument(v.Id, doc.Id);
                    return s.CounterCash.Count > 0;
                }, 300f, "paid");
                Bot.TickUntil(s, () => s.Visits.Count == 0, 60f, "Samir leaves");
                Assert.True(s.CloseShop(), "close early");
                float repBefore = s.Standing.Reputation;

                var po = s.PendingOutcomes.Single();
                float q = v.Order.Quality.Final;
                Console.WriteLine($"    seed {seed}: {(careful ? "careful " : "careless")} quality {q:0} -> {po.Kind}");
                Assert.Equal(careful ? OutcomeKind.Success : OutcomeKind.Failed, po.Kind, $"seed {seed}: quality {q:0} decides");
                Assert.Equal(DemoCast.SamirOutcome(q), po.Kind, "the pass mark");
                Assert.Equal(new GameTime(2, DemoCast.SamirOutcomeMinute).TotalMinutes, po.ResolvesAt.TotalMinutes, "lands Day 2 08:05");
                s.StartNextDay();
                var msg = s.Inbox.Messages.FirstOrDefault(m => m.OrderId == v.Order.Id);
                Assert.True(msg != null && po.Applied, "message on the Day 2 card");
                Assert.Equal("SAMIR", msg.From, "from Samir");
                if (po.Kind == OutcomeKind.Success) success++;
                else failed++;
                float rep = s.Standing.Reputation;
                if (po.Kind == OutcomeKind.Success) Assert.Near(repBefore + OutcomeModel.ReputationDelta(OutcomeKind.Success, 450), rep, 0.01f, "good news raises rep");
                else Assert.True(rep < repBefore || rep == 0f, "bad news costs rep");
                Assert.Equal(rep >= 20f, s.Country(Countries.Italy).Requestable(rep), "Italy opens at 20");
                if (careful && po.Kind == OutcomeKind.Success) Assert.True(rep >= 20f, $"seed {seed}: a clean first job unlocks Italy (rep {rep:0})");
            }
            Console.WriteLine($"    12 demo runs: Samir success {success}, failed {failed}; failed TRUST ME -> FIX IT only {fixOnly}x");
            Assert.Equal(8, success, "every careful run succeeds");
            Assert.Equal(4, failed, "every careless run fails");
        }

        // ------------------------------------------------------------------ (f) the whole demo

        public static void Demo_BotPlaysWholeDemo()
        {
            var skip = PlayDemo(70, buyGermany: false);
            var buy = PlayDemo(70, buyGermany: true);
            Console.WriteLine($"    bot real time: day 1 {skip.Day1Seconds:0} s, day 2 {skip.Day2Seconds:0} s (no Germany); " +
                $"day 2 {buy.Day2Seconds:0} s (with Germany)");

            foreach (var run in new[] { skip, buy })
            {
                var s = run.S;
                var beats = DemoCast.DayTwoBeats().ToList();
                foreach (var b in beats)
                {
                    Assert.True(run.Arrived.ContainsKey(b.BeatId), $"{b.BeatId} came");
                    var at = run.Arrived[b.BeatId];
                    float due = run.OpenedAt[2] + (b.ArrivalMinute - Balance.OpenMinute);
                    Assert.True(at.Day == 2 && at.Minute >= due && at.Minute < due + 2f, $"{b.BeatId} on time ({at}, opened {run.OpenedAt[2]})");
                }
                float samirDue = run.OpenedAt[1] + (DemoCast.SamirArrivalMinute - Balance.OpenMinute);
                Assert.True(run.Arrived[StoryCast.SamirBeatId].Minute < samirDue + 2f, "Samir comes right after the sign flips");

                // Japan: the teaser.
                Assert.Equal(C.Decline, run.FirstChoices[DemoCast.JapanBeatId], "Japan: decline only");
                Assert.True(run.Toasts.Contains("Japan is in the full game."), "Japan toast");
                Assert.True(s.Orders.All.All(o => o.CustomerId != DemoCast.KenjiId), "no Japan order");

                // The rush job.
                var urgent = s.Orders.All.Single(o => o.CustomerId == DemoCast.ViktorId);
                Assert.True(urgent.Urgent && urgent.Price == 1200 && urgent.CountryId == Countries.France, "urgent France job at $1,200");
                Assert.Near(DemoCast.UrgentDeadlineMinutes, (float)GameTime.MinutesBetween(run.Arrived[DemoCast.UrgentBeatId], urgent.Deadline), 0.01f, "deadline four hours after arrival");
                Assert.True(run.Lines.Contains($"$1,200. Ready by {urgent.Deadline.Clock}, or I walk."), "the rush client says the real due time");
                Assert.True(!run.Lines.Any(l => l.Contains(DemoCast.DueToken)), "no raw token spoken");
                Assert.Equal(OrderStatus.Delivered, urgent.Status, "rush job delivered");

                Assert.Equal(OrderStatus.Delivered, s.Orders.All.Single(o => o.CustomerId == DemoCast.ElifId).Status, "Turkish job delivered");

                // Close-early note, once.
                Assert.Equal(1, s.Inbox.Messages.Count(m => m.Body == DemoCast.QuietBody && m.From == "R."), "R.'s close-early note");

                // Report and the end.
                Assert.Equal(2, s.LastReport.Day, "Day 2 report");
                Assert.Equal(4, s.LastReport.Customers, "only the scripted visitors");
                Assert.True(s.DemoOver, "demo over");
                s.StartNextDay();
                Assert.True(s.Day == 2 && s.Phase == DayPhase.Closed && s.DemoOver, "no Day 3");
            }

            Assert.Equal(C.Decline, skip.FirstChoices[DemoCast.GermanBeatId], "Germany not owned: decline only");
            Assert.True(skip.S.Orders.All.All(o => o.CustomerId != DemoCast.AnjaId), "no German job without the handbook");
            Assert.True(buy.FirstChoices[DemoCast.GermanBeatId].Contains(C.Accept), "Germany owned: normal haggle");
            Assert.Equal(OrderStatus.Delivered, buy.S.Orders.All.Single(o => o.CustomerId == DemoCast.AnjaId).Status, "German job delivered");
            Console.WriteLine($"    end cash {skip.S.Ledger.Cash} / {buy.S.Ledger.Cash}, rep {skip.S.Standing.Reputation:0} / {buy.S.Standing.Reputation:0}");
        }

        // ------------------------------------------------------------------ (g) counter, sign and clock rules

        public static void Demo_CounterSignAndClockRules()
        {
            // Day 1: the sign can't be flipped back before Samir has been; the guided day never hits 18:00.
            var t = NewDemo(80);
            t.OpenShop();
            Assert.True(!t.CanCloseShop(out string why) && why.Length > 0, "can't close before Samir came");
            Console.WriteLine($"    day 1 close refused: \"{why}\"");
            var samirAt = t.Director.Upcoming.Single(a => a.Beat?.BeatId == StoryCast.SamirBeatId).At;
            Assert.Near(t.Clock.Minute + 2f, samirAt.Minute, 0.01f, "Samir two minutes after opening (opened 08:30)");
            var idle = NewDemo(81);
            idle.Clock.Minute = 17 * 60 + 25;
            Bot.Tick(idle, 60f);
            Assert.True(idle.Clock.Minute <= Balance.CloseMinute - 30 + 0.01f && idle.Day == 1 && idle.Phase == DayPhase.Morning, $"tutorial clock holds before closing ({idle.Now})");
            idle.OpenShop();
            var sv = Bot.WaitForChoice(idle, C.Accept, 120f);
            Assert.Equal(StoryCast.SamirId, sv.Customer.Id, "late opening: Samir still comes, right away");

            // Day 2 (Germany not bought, France update not bought).
            var s = NewDemo(82);
            var samir = ServeSamirUntilPrint(s, true);
            var sdoc = Bot.PrintAndBind(s, samir.Order);
            Assert.True(s.GiveDocument(samir.Id, sdoc.Id), "hand Samir his passport");
            Bot.TickUntil(s, () =>
            {
                if (samir.Phase == VisitPhase.Choosing && samir.Choices.Count > 0) s.Choose(samir.Id, samir.Choices[0].Id);
                else if (samir.Phase == VisitPhase.Waiting && !samir.Moving && s.DeskDocuments.Contains(sdoc)) s.GiveDocument(samir.Id, sdoc.Id);
                return s.Visits.Count == 0;
            }, 300f, "Samir paid and gone");
            Assert.True(s.CloseShop(), "close early once Samir has been");
            s.StartNextDay();
            s.Clock.Minute = 9 * 60 + 30; // read the mail for a while
            s.SetAcceptingJobs(Countries.Turkey, false);
            s.OpenShop();
            Assert.True(!s.CanCloseShop(out why) && why.Length > 0, "can't close with visitors still due");
            var elifArrival = s.Director.Upcoming.First(a => a.Beat?.BeatId == DemoCast.TurkishBeatId).At;
            Assert.Near(9 * 60 + 35, elifArrival.Minute, 0.01f, "the schedule moves with a late opening");

            // Border Watch: not taking Turkey means only "no"; taking it again re-offers the job.
            var elif = Bot.WaitForChoice(s, C.Decline, 200f);
            Assert.Equal(DemoCast.ElifId, elif.Customer.Id, "Elif first");
            Assert.Equal(C.Decline, Ids(elif), "not taking Turkey: decline only");
            Assert.Equal(Lines.Player.NotTaking("Turkey"), elif.Choices[0].Text, "says why");
            s.SetAcceptingJobs(Countries.Turkey, true);
            Bot.WaitForChoice(s, C.Accept, 30f);
            Assert.True(elif.Choices.Exists(c => c.Id == C.Accept), "taking Turkey again: the deal is back");
            s.Choose(elif.Id, C.Accept);
            Bot.WaitForChoice(s, C.WaitBench, 30f);
            s.Choose(elif.Id, C.WaitBench);
            Bot.TickUntil(s, () => elif.Phase == VisitPhase.Waiting && !elif.Moving, 30f, "Elif on the bench");

            // Kenji: decline; Viktor: take the rush job, shoot him on the old gray background.
            var kenji = Bot.WaitForChoice(s, C.Decline, 200f);
            Assert.Equal(DemoCast.KenjiId, kenji.Customer.Id, "Kenji next");
            s.Choose(kenji.Id, C.Decline);
            var viktor = Bot.WaitForChoice(s, C.Accept, 200f);
            Assert.Equal(DemoCast.ViktorId, viktor.Customer.Id, "Viktor next");
            s.Choose(viktor.Id, C.Accept);
            Bot.WaitForChoice(s, C.TakePhoto, 30f);
            s.Choose(viktor.Id, C.TakePhoto);
            Bot.TickUntil(s, () => viktor.Phase == VisitPhase.PhotoSitting && !viktor.Moving, 30f, "Viktor seated");
            var gray = Bot.TakeGoodPhoto(s, viktor, true, Backdrop.LightGray);
            Assert.True(s.FinishPhotoSitting(viktor.Id), "Viktor gets up");
            Bot.TickUntil(s, () => viktor.Phase == VisitPhase.Waiting && !viktor.Moving, 30f, "Viktor on the bench");
            var vo = viktor.Order;
            Bot.FillPerfect(s, vo, gray);
            var doc = Bot.PrintAndBind(s, vo);
            Assert.True(s.GiveDocument(viktor.Id, doc.Id), "hand Viktor his passport");
            Bot.TickUntil(s, () => viktor.Phase == VisitPhase.Choosing && !viktor.IsTalking, 60f, "Viktor inspects");
            Assert.True(viktor.HasComplaint && viktor.Complaint.Issue == Issue.WrongBackdrop, "he read the paper: wrong background");

            // FIX IT over a photo problem: back to the stool, the old photo comes off the order.
            s.Choose(viktor.Id, C.FixIt);
            Assert.True(vo.NeedsRetake && vo.Status == OrderStatus.AwaitingPhoto && vo.Draft.PhotoId == null, "retake needed");
            Bot.TickUntil(s, () => viktor.Phase == VisitPhase.Waiting && !viktor.Moving, 30f, "Viktor back on the bench");
            s.ShredDocument(doc.Id);
            Assert.Equal(OrderStatus.AwaitingPhoto, vo.Status, "shredding keeps the retake");
            s.CallVisit(viktor.Id);
            Bot.TickUntil(s, () => viktor.Phase == VisitPhase.PhotoSitting && !viktor.Moving, 30f, "called back to the stool");
            Assert.True(!s.FinishPhotoSitting(viktor.Id), "can't send him off without a new photo");
            Bot.TickUntil(s, () => !viktor.IsTalking, 20f, "Viktor done talking");
            var white = Bot.TakeGoodPhoto(s, viktor);
            Assert.True(!vo.NeedsRetake && vo.Status == OrderStatus.InProduction, "new photo kept");
            Assert.True(s.FinishPhotoSitting(viktor.Id), "now he can get up");
            Bot.TickUntil(s, () => viktor.Phase == VisitPhase.Waiting && !viktor.Moving, 30f, "Viktor on the bench again");
            Bot.FillPerfect(s, vo, white);
            var doc2 = Bot.PrintAndBind(s, vo);

            // Anja: no Germany section, so only "no" — until the player buys it while she waits.
            var anja = Bot.WaitForChoice(s, C.Decline, 300f);
            Assert.Equal(DemoCast.AnjaId, anja.Customer.Id, "Anja last");
            Assert.Equal(C.Decline, Ids(anja), "no handbook: decline only");
            Assert.True(s.BuyHandbook(Countries.Germany), "buy Germany with her at the counter");
            Bot.WaitForChoice(s, C.Accept, 30f);
            Assert.True(anja.Choices.Exists(c => c.Id == C.Accept), "she gets the real offer");
            s.Choose(anja.Id, C.Accept);
            Bot.WaitForChoice(s, C.WaitBench, 30f);
            s.Choose(anja.Id, C.WaitBench);
            Bot.TickUntil(s, () => anja.Phase == VisitPhase.Waiting && !anja.Moving, 30f, "Anja on the bench");
            Bot.TickUntil(s, () => s.GiveDocument(viktor.Id, doc2.Id), 30f, "hand over the redo");
            Bot.TickUntil(s, () => vo.Status == OrderStatus.Delivered, 60f, "Viktor pays for the redo");
            Assert.True(!s.CanCloseShop(out _), "customers inside");
            Assert.True(!s.Inbox.Messages.Any(m => m.Body == DemoCast.QuietBody), "no 'close up' with customers inside");

            // 18:00 on the last day: nobody waiting is sent home or turned into a pickup.
            s.Clock.Minute = Balance.CloseMinute - 1f;
            Bot.Tick(s, 3f);
            Assert.Equal(DayPhase.AfterHours, s.Phase, "after hours");
            foreach (var w in new[] { elif, anja })
            {
                Assert.True(s.Visits.Contains(w) && !w.Leaving && w.Phase == VisitPhase.Waiting, $"{w.Customer.FirstName} still waits");
                Assert.True(!s.Director.HasPickupFor(w.Order.Id), $"{w.Customer.FirstName}: no Day-3 pickup");
                Assert.Near(GameSession.ForcedCloseMinute, w.Order.Deadline.Minute, 0.01f, $"{w.Customer.FirstName}: not late at 18:00");
            }
        }

        // ------------------------------------------------------------------ (h) another photo (player's call)

        /// <summary>Full game: call Samir back for a better photo after his booklet is made, reprint, hand over.</summary>
        public static void Retake_VoluntaryFullMode() => VoluntaryRetake(() => new GameSession(12345), false, "full");

        /// <summary>Demo: the same with the tutorial's Samir (who never refuses), on the guided Day 1.</summary>
        public static void Retake_VoluntaryDemo() => VoluntaryRetake(() => NewDemo(90), true, "demo");

        static void VoluntaryRetake(Func<GameSession> make, bool tutorial, string label)
        {
            // Twin sessions, same seed and same moves: the call-back must not touch the shop's dice.
            var s = make();
            var twin = make();
            var (v, first, doc1) = UpToBooklet(s);
            var (tv, _, _) = UpToBooklet(twin);
            Assert.True(s.CanCallBackForPhoto(v.Id), $"{label}: on the bench with a booklet made, he can be called back");
            Assert.Equal(OrderStatus.Ready, v.Order.Status, $"{label}: booklet bound");

            // Fed up: a normal customer says no (and nothing changes); the tutorial's Samir always sits.
            float patience = v.Patience;
            v.Patience = tv.Patience = v.PatienceMax * (Balance.RetakeRefuseBelow - 0.05f);
            v.ComplaintLevel = tv.ComplaintLevel = 2; // no grumbling (which rolls dice) while the twins tick
            bool sat = s.CallBackForPhoto(v.Id);
            Assert.Equal(twin.Rng.NextUInt(), s.Rng.NextUInt(), $"{label}: asking draws no dice");
            Assert.Equal(tutorial, sat, $"{label}: fed-up customer {(tutorial ? "still sits (tutorial)" : "refuses")}");
            if (!sat)
            {
                Assert.True(!v.Retake && v.Phase == VisitPhase.Waiting && !v.Moving, $"{label}: refused, stays on the bench");
                Bot.Tick(s, 0.1f);
                Assert.Equal(Lines.RetakeRefused, v.Line?.Text, $"{label}: says no");
                Bot.Tick(twin, 0.1f);
                v.Patience = tv.Patience = patience;
                float before = v.Patience;
                sat = s.CallBackForPhoto(v.Id);
                Assert.True(sat, $"{label}: patient again, he sits");
                Assert.Near(before - v.PatienceMax * Balance.RetakePatienceCost, v.Patience, 0.01f, $"{label}: costs a little patience");
            }
            Assert.Equal(twin.Rng.NextUInt(), s.Rng.NextUInt(), $"{label}: calling back draws no dice");
            Assert.True(v.Retake && v.To == Spot.Stool && v.RetakeAfterPhoto == first.Id, $"{label}: walking back to the stool");
            Assert.True(!s.CanCallBackForPhoto(v.Id), $"{label}: can't call twice");
            Bot.Tick(s, 0.1f);
            Assert.Equal(Lines.RetakeCalled(v.Customer), v.Line?.Text, $"{label}: 'another one?'");

            Bot.TickUntil(s, () => v.Phase == VisitPhase.PhotoSitting && !v.Moving, 30f, "back on the stool");
            Bot.TickUntil(s, () => !v.IsTalking, 20f, "done talking");
            var second = Bot.TakeGoodPhoto(s, v, true);
            Assert.True(second.Id > first.Id, $"{label}: a new photo");
            Assert.Equal(OrderStatus.Ready, v.Order.Status, $"{label}: still ready (nothing shredded)");
            Assert.True(s.DeskDocuments.Contains(doc1), $"{label}: the old booklet stays on the desk");
            Assert.Equal(first.Id, v.Order.Draft.PhotoId ?? -1, $"{label}: the draft keeps its photo until the player picks");
            Assert.True(s.FinishPhotoSitting(v.Id), $"{label}: DONE - THANK YOU");
            Bot.Tick(s, 0.1f);
            Assert.Equal(Lines.RetakeDone, v.Line?.Text, $"{label}: back to the bench");
            Bot.TickUntil(s, () => v.Phase == VisitPhase.Waiting && !v.Moving, 30f, "on the bench again");
            Assert.True(!v.Retake && v.Retakes == 1, $"{label}: retake done");
            Assert.Equal(2, s.Photos.ForCustomer(v.Customer.Id).Count, $"{label}: both photos kept");

            // Reprint with the new photo; the order's booklet is the new one.
            v.Order.Draft.PhotoId = second.Id;
            var doc2 = Bot.PrintAndBind(s, v.Order);
            Assert.True(v.Order.Document == doc2 && s.DeskDocuments.Contains(doc1) && s.DeskDocuments.Contains(doc2), $"{label}: two booklets, the order's is the new one");
            var spec = s.Country(v.Order.CountryId).Spec;
            var q1 = DocumentEvaluator.Evaluate(doc1, v.Order, spec, v.Customer, s.Photos);
            var q2 = DocumentEvaluator.Evaluate(doc2, v.Order, spec, v.Customer, s.Photos);
            Console.WriteLine($"    {label}: first photo booklet {q1.Final:0}, retake booklet {q2.Final:0}");
            Assert.True(q2.Final > q1.Final, $"{label}: the retake makes a better passport");

            Assert.True(s.GiveDocument(v.Id, doc2.Id), $"{label}: hand over the new booklet");
            Bot.TickUntil(s, () => v.Phase == VisitPhase.Inspecting, 30f, "inspecting");
            Assert.True(!s.CanCallBackForPhoto(v.Id), $"{label}: not while they hold it");
            Bot.TickUntil(s, () =>
            {
                if (v.Phase == VisitPhase.Choosing && v.Choices.Count > 0) s.Choose(v.Id, v.Choices[0].Id);
                return v.Order.Status == OrderStatus.Delivered;
            }, 120f, "paid");
            Assert.True(v.Order.Document == doc2, $"{label}: delivered the new one");
            Assert.True(!s.CanCallBackForPhoto(v.Id), $"{label}: not after delivery");
        }

        /// <summary>Samir's job up to a bound booklet, with a careless first photo; he waits on the bench.</summary>
        static (Visit, PhotoCapture, FinishedDocument) UpToBooklet(GameSession s)
        {
            s.OpenShop();
            var v = Bot.WaitForChoice(s, C.Accept, 300f);
            Assert.Equal(StoryCast.SamirId, v.Customer.Id, "Samir first");
            s.Choose(v.Id, C.Accept);
            Bot.WaitForChoice(s, C.TakePhoto, 60f);
            Assert.True(!s.CanCallBackForPhoto(v.Id), "no call-back before the first photo");
            s.Choose(v.Id, C.TakePhoto);
            Bot.TickUntil(s, () => v.Phase == VisitPhase.PhotoSitting && !v.Moving, 30f, "seated");
            var first = Bot.TakeGoodPhoto(s, v, false);
            Assert.True(!s.CanCallBackForPhoto(v.Id), "not while sitting");
            Assert.True(s.FinishPhotoSitting(v.Id), "gets up");
            Bot.TickUntil(s, () => v.Phase == VisitPhase.Waiting && !v.Moving, 40f, "bench");
            Bot.FillPerfect(s, v.Order, first);
            var doc = Bot.PrintAndBind(s, v.Order);
            Bot.TickUntil(s, () => !v.IsTalking, 20f, "quiet");
            return (v, first, doc);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Day 1 up to a filled draft: accept, photo, bench, editor.</summary>
        static Visit ServeSamirUntilPrint(GameSession s, bool careful)
        {
            s.OpenShop();
            var v = Bot.WaitForChoice(s, C.Accept, 300f);
            s.Choose(v.Id, C.Accept);
            Bot.WaitForChoice(s, C.TakePhoto, 60f);
            s.Choose(v.Id, C.TakePhoto);
            Bot.TickUntil(s, () => v.Phase == VisitPhase.PhotoSitting && !v.Moving, 30f, "seated");
            var photo = Bot.TakeGoodPhoto(s, v, careful);
            Assert.True(s.FinishPhotoSitting(v.Id), "gets up");
            Bot.TickUntil(s, () => v.Phase == VisitPhase.Waiting, 40f, "bench");
            Bot.FillPerfect(s, v.Order, photo);
            return v;
        }

        sealed class DemoRun
        {
            public GameSession S;
            public float Day1Seconds, Day2Seconds;
            public readonly Dictionary<string, GameTime> Arrived = new Dictionary<string, GameTime>();
            public readonly Dictionary<string, string> FirstChoices = new Dictionary<string, string>();
            public readonly List<string> Toasts = new List<string>();
            public readonly HashSet<string> Lines = new HashSet<string>();
            public readonly Dictionary<int, float> OpenedAt = new Dictionary<int, float>();
        }

        /// <summary>A careful player: does every job, closes early when R. says so.</summary>
        static DemoRun PlayDemo(ulong seed, bool buyGermany)
        {
            var run = new DemoRun { S = NewDemo(seed) };
            var s = run.S;
            var printedFor = new HashSet<int>();
            for (int day = 1; day <= 2; day++)
            {
                if (day == 2)
                {
                    s.StartNextDay();
                    Assert.True(s.BuyHandbookUpdate(Countries.France), "France update");
                    if (buyGermany) Assert.True(s.BuyHandbook(Countries.Germany), "buy Germany");
                }
                run.OpenedAt[day] = s.Clock.Minute;
                s.OpenShop();
                float guard = 0f;
                while (s.Phase != DayPhase.Closed)
                {
                    foreach (var e in s.DrainEvents())
                        if (e.Type == SimEventType.Toast) run.Toasts.Add(e.Text);
                    foreach (var v in s.Visits.ToArray())
                    {
                        if (v.Line != null) run.Lines.Add(v.Line.Text);
                        if (v.Beat != null && !run.Arrived.ContainsKey(v.Beat.BeatId)) run.Arrived[v.Beat.BeatId] = v.ArrivedAt;
                        if (v.Phase == VisitPhase.Choosing)
                        {
                            if (v.Beat != null && !run.FirstChoices.ContainsKey(v.Beat.BeatId)) run.FirstChoices[v.Beat.BeatId] = Ids(v);
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

                    // Close early: Day 1 once Samir is gone, Day 2 when R. says so.
                    bool told = day == 1 ? s.Orders.All.Any(o => o.Status == OrderStatus.Delivered)
                        : s.Inbox.Messages.Any(m => m.Body == DemoCast.QuietBody);
                    if (told && s.CloseShop()) break;

                    s.Tick(Bot.Step);
                    guard += Bot.Step;
                    if (guard > 3000f) throw new AssertException($"demo day {day} stuck: {s.Phase} {s.Now} " +
                        string.Join(",", s.Visits.Select(x => $"{x.Customer.FirstName}:{x.Phase}:{Ids(x)}")));
                }
                // Real seconds the day took: game minutes at 0.75/s, minus the minutes binding skips.
                int binds = s.Ledger.Entries.Count(en => en.Day == day && en.Note == "Blank booklet");
                float seconds = (s.Clock.Minute - Balance.MorningMinute - binds * Balance.BindMinutes) / Balance.GameMinutesPerRealSecond;
                if (day == 1) run.Day1Seconds = seconds;
                else run.Day2Seconds = seconds;
                Assert.Equal(day, s.LastReport.Day, $"day {day} report");
            }
            return run;
        }

        static string Pick(Visit v)
        {
            bool Has(string id) => v.Choices.Exists(c => c.Id == id && c.Enabled);
            if (Has(C.Accept)) return C.Accept;
            if (Has(C.TakePhoto)) return C.TakePhoto;
            if (Has(C.WaitBench)) return C.WaitBench;
            if (Has(C.TrustMe)) return C.TrustMe;
            if (Has(C.FixIt)) return C.FixIt;
            if (Has(C.AlmostDone)) return C.AlmostDone;
            return v.Choices.First(c => c.Enabled).Id;
        }
    }
}
