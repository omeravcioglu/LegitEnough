using System;
using System.Collections.Generic;
using System.Linq;
using LegitEnough.Sim;
using C = LegitEnough.Sim.GameSession.ChoiceIds;

namespace LegitEnough.SimTests
{
    public static class Tests
    {
        // ------------------------------------------------------------------ primitives

        public static void Rng_IsDeterministic()
        {
            var a = new Rng(99);
            var b = new Rng(99);
            for (int i = 0; i < 100; i++) Assert.Equal(a.NextUInt(), b.NextUInt(), "same seed same sequence");
            var c = new Rng(100);
            Assert.True(new Rng(99).NextUInt() != c.NextUInt(), "different seeds differ");
            for (int i = 0; i < 1000; i++)
            {
                float f = a.NextFloat();
                Assert.True(f >= 0f && f < 1f, "float in [0,1)");
                int r = a.Range(3, 7);
                Assert.True(r >= 3 && r < 7, "range in bounds");
            }
        }

        public static void Generator_SameIdSameCustomer()
        {
            var p1 = CustomerGenerator.Generate(142, 777);
            var p2 = CustomerGenerator.Generate(142, 777);
            Assert.Equal(p1.DisplayName, p2.DisplayName, "name");
            Assert.Equal(p1.Look.Hair, p2.Look.Hair, "hair");
            Assert.Equal(p1.Look.SkinTone, p2.Look.SkinTone, "skin");
            Assert.Equal(p1.Primary, p2.Primary, "trait");
            Assert.Equal(p1.Identity.DateOfBirth, p2.Identity.DateOfBirth, "dob");
            Assert.Equal(p1.Slip.Count, p2.Slip.Count, "slip");
            var db = new ClientDatabase(777);
            Assert.Equal(p1.DisplayName, db.Get(142).DisplayName, "database regenerates the same person");
        }

        public static void Generator_VarietyAcrossIds()
        {
            var names = new HashSet<string>();
            var traits = new HashSet<Trait>();
            var hair = new HashSet<HairStyle>();
            int glasses = 0;
            for (int i = 1; i <= 300; i++)
            {
                var p = CustomerGenerator.Generate(i, 4242);
                names.Add(p.DisplayName);
                traits.Add(p.Primary);
                hair.Add(p.Look.Hair);
                if (p.Look.Glasses.Count > 0) glasses++;
                Assert.True(p.Identity.Surname == p.Identity.Surname.ToUpperInvariant(), "surname uppercase");
                Assert.True(p.Identity.HeightCm >= 148 && p.Identity.HeightCm <= 201, "height range");
                Assert.True(p.Slip.Count >= 5, "slip lines");
                foreach (var ch in p.DisplayName) Assert.True(ch < 128, "ascii name");
            }
            Console.WriteLine($"    300 customers: {names.Count} names, {traits.Count} traits, {hair.Count} hairstyles, {glasses} wear glasses");
            Assert.True(names.Count > 280, "names mostly unique");
            Assert.True(traits.Count >= 12, "trait variety");
            Assert.True(glasses > 40 && glasses < 200, "some glasses");
        }

        public static void Labels_DateFormatsRoundTrip()
        {
            var d = new DateTime(1987, 3, 14);
            foreach (DateFormat f in Enum.GetValues(typeof(DateFormat)))
            {
                string s = Labels.FormatDate(d, f);
                Assert.True(Labels.TryParse(s, f, out var back) && back == d, $"round trip {f}: {s}");
                Assert.True(Labels.TryParseAnyFormat(s, out var any) && any == d, $"any-format parse {f}");
            }
            Assert.Equal("14.03.1987", Labels.FormatDate(d, DateFormat.DmyDots), "dots");
            Assert.Equal("03/14/1987", Labels.FormatDate(d, DateFormat.MdySlashes), "US");
            Assert.Equal("14 MAR 1987", Labels.FormatDate(d, DateFormat.DMonY), "month abbreviation");
            Assert.True(!Labels.TryParse("31.02.1987", DateFormat.DmyDots, out _), "invalid date rejected");
        }

        public static void Labels_TurkishCultureSafe()
        {
            Assert.Equal("LEILA", Labels.Normalize("leila"), "invariant uppercase (no dotted I)");
            Assert.Equal("SAMIR HADDAD", Labels.Normalize("  samir   haddad "), "collapse spaces");
            Assert.Equal("$1,500", Labels.Money(1500), "money format");
            Assert.Equal("Wednesday", Calendar.WeekdayName(3), "invariant weekday");
            Assert.Equal("Istanbul", CustomerGenerator.TitleCase("ISTANBUL"), "title case invariant");
        }

        public static void Labels_Levenshtein()
        {
            Assert.Equal(0, Labels.Levenshtein("HADDAD", "HADDAD"), "same");
            Assert.Equal(1, Labels.Levenshtein("HADAD", "HADDAD"), "deletion");
            Assert.Equal(2, Labels.Levenshtein("SMAIR", "SAMIR"), "transposition = 2");
            Assert.Equal(6, Labels.Levenshtein("", "HADDAD"), "empty");
        }

        public static void Negotiation_Behaves()
        {
            var p = CustomerGenerator.Generate(3, 1);
            var rng = new Rng(5);
            var n = new Negotiation(p, 400, 500);
            var r = n.Ask(450, rng);
            Assert.Equal(Bargain.Accepted, r.Result, "ask below reservation is accepted");
            Assert.Equal(450, r.Amount, "at the asked price");

            int countered = 0, walked = 0;
            for (int i = 0; i < 200; i++)
            {
                var n2 = new Negotiation(p, 400, 500);
                var r2 = n2.Ask(600, new Rng((ulong)i));
                if (r2.Result == Bargain.Countered)
                {
                    countered++;
                    Assert.Equal(500, r2.Amount, "counter at reservation");
                    Assert.Equal(500, n2.Current, "final offer on the table");
                }
                else if (r2.Result == Bargain.WalkedOut) walked++;
            }
            Assert.True(countered > 0, "asking slightly over gets counters");

            var n3 = new Negotiation(p, 400, 400);
            Assert.Equal(400, n3.Accept().Amount, "accept takes the offer");
            Assert.True(n3.IsOver, "closed");
        }

        public static void HeadPose_Steps()
        {
            var p = new HeadPose(-2, 2);
            Assert.Equal("UP-LEFT", p.Name, "named pose");
            var t = HeadPose.Center;
            int steps = 0;
            while (p != t && steps < 10)
            {
                p = p.StepToward(t);
                steps++;
            }
            Assert.Equal(2, steps, "two half-steps from corner to center (both axes move together)");
            Assert.Equal(new HeadPose(5, -9), new HeadPose(2, -2), "clamped");
        }

        static PhotoCapture MakeCapture(Appearance look, Backdrop backdrop)
        {
            var c = new PhotoCapture
            {
                Id = 1, CustomerId = 7, Look = look, Pose = HeadPose.Center, Expression = Expression.Neutral,
                Lid = 0f, Backdrop = backdrop, Zoom = 2f, PanX = 0f, PanY = 0.25f, SensorHeightPx = 1600,
            };
            var head = c.HeadInFrame;
            float cropH = head.H / 0.65f;
            c.Crop = PortraitGeometry.MakeCrop(head.CenterX, head.Y - 0.1f * cropH + cropH * 0.5f, cropH);
            return c;
        }

        public static void Geometry_CoverageMath()
        {
            var look = new Appearance { FaceWidth = 0.78f, Hair = HairStyle.Bald };
            var c = MakeCapture(look, Backdrop.LightGray);
            Assert.Near(0.65f, c.Coverage, 0.01f, "coverage of a crafted crop");
            var head = c.HeadInFrame;
            // Zoom 2 → frame shows 2 head heights; bald head is exactly 1 head tall.
            Assert.Near(0.5f, head.H, 0.001f, "head is half the frame at zoom 2");
            var full = PortraitGeometry.FullCrop();
            Assert.Near(1f, full.W, 0.001f, "full crop is width-limited");
            Assert.Near(35f / 45f, full.W * Balance.SensorAspect / full.H, 0.001f, "35:45 output");
            var cropped = PortraitGeometry.MakeCrop(0.9f, 0.9f, 0.5f);
            Assert.True(cropped.Inside(new Box(0, 0, 1, 1)), "crop clamped inside frame");
        }

        public static void PhotoEvaluator_RulesMatter()
        {
            var rules = Countries.CreateAll().First(x => x.Id == Countries.France).Photo;
            var look = new Appearance { FaceWidth = 0.78f, Hair = HairStyle.Short };

            var good = MakeCapture(look, Backdrop.LightGray);
            var r = PhotoEvaluator.Score(good, rules);
            Console.WriteLine($"    clean photo: {r}  [{string.Join("; ", r.Findings)}]");
            Assert.True(r.Final >= 90f, "clean photo scores high");

            var wrongBg = MakeCapture(look, Backdrop.White);
            var r2 = PhotoEvaluator.Score(wrongBg, rules);
            Assert.True(r2.Has(Issue.WrongBackdrop) && r2.Final < r.Final - 15f, "wrong backdrop hurts");

            var blink = MakeCapture(look, Backdrop.LightGray);
            blink.Lid = 1f;
            Assert.True(PhotoEvaluator.Score(blink, rules).Has(Issue.EyesClosed), "eyes closed detected");

            var glasses = MakeCapture(look, Backdrop.LightGray);
            glasses.Glasses.Add(GlassesStyle.Round);
            Assert.True(PhotoEvaluator.Score(glasses, rules).Has(Issue.GlassesWorn), "glasses not allowed in France");

            var smile = MakeCapture(look, Backdrop.LightGray);
            smile.Expression = Expression.Smile;
            Assert.True(PhotoEvaluator.Score(smile, rules).Has(Issue.WrongExpression), "smile not allowed in France");

            var uncropped = MakeCapture(look, Backdrop.LightGray);
            uncropped.Zoom = 1f;
            uncropped.Crop = PortraitGeometry.FullCrop();
            var r3 = PhotoEvaluator.Score(uncropped, rules);
            Assert.True(r3.Has(Issue.HeadTooSmall), "uncropped zoom-1 photo has a tiny head");

            var turned = MakeCapture(look, Backdrop.LightGray);
            turned.Pose = new HeadPose(2, 0);
            Assert.True(PhotoEvaluator.Score(turned, rules).Has(Issue.HeadTurned), "turned head detected");
        }

        public static void PhotoSubject_BlinksAndDrifts()
        {
            var db = new ClientDatabase(31);
            CustomerProfile nervous = null;
            for (int i = 1; i < 400 && nervous == null; i++)
            {
                var p = db.Get(i);
                if (p.Primary == Trait.Nervous) nervous = p;
            }
            Assert.True(nervous != null, "found a nervous customer");
            var subj = new PhotoSubject(nervous, new Rng(8));
            int blinks = 0, poseChanges = 0;
            bool wasClosed = false;
            HeadPose last = subj.Pose;
            subj.BeginDrag();
            subj.DragTo(0, 0);
            for (float t = 0; t < 1f; t += 0.02f) subj.Tick(0.02f);
            subj.EndDrag();
            Assert.Equal(HeadPose.Center, subj.Pose, "drag centers the head");
            for (float t = 0; t < 60f; t += 0.02f)
            {
                subj.Tick(0.02f);
                bool closed = subj.Lid > 0.9f;
                if (closed && !wasClosed) blinks++;
                wasClosed = closed;
                if (subj.Pose != last) poseChanges++;
                last = subj.Pose;
            }
            Console.WriteLine($"    nervous subject over 60s: {blinks} blinks, {poseChanges} pose steps");
            Assert.True(blinks >= 10, "nervous people blink a lot");
            Assert.True(poseChanges > 0, "nervous people look away");

            int beforeGlasses = subj.Glasses.Count;
            subj.Redress();
            Assert.Equal(nervous.Look.Glasses.Count, subj.Glasses.Count, "redress restores glasses");
            Assert.True(beforeGlasses == subj.Glasses.Count, "nothing removed yet");
        }

        static (GameSession s, Order o, CustomerProfile c) MadeUpOrder()
        {
            var s = new GameSession(7);
            var c = s.Clients.Get(StoryCast.SamirId);
            var req = new JobRequest { CustomerId = c.Id, CountryId = Countries.France, Deadline = new GameTime(1, 1080) };
            var o = s.Orders.Create(req, 450, s.Now);
            var photo = MakeCapture(c.Look, Backdrop.LightGray);
            photo.CustomerId = c.Id;
            photo.Glasses.Clear();
            photo.Id = s.Photos.NextId();
            s.Photos.Keep(photo);
            Bot.FillPerfect(s, o, photo);
            return (s, o, c);
        }

        static FinishedDocument Finish(GameSession s, Order o) => new FinishedDocument
        {
            Id = 1, OrderId = o.Id, Data = o.Draft.Clone(), IssuedDay = s.Day, BookletCover = o.Draft.Cover ?? CoverColor.Black,
        };

        public static void DocumentEvaluator_ScoresErrors()
        {
            var (s, o, c) = MadeUpOrder();
            var spec = s.Country(Countries.France).Spec;
            var perfect = DocumentEvaluator.Evaluate(Finish(s, o), o, spec, c, s.Photos);
            Console.WriteLine($"    perfect: {perfect}  [{string.Join("; ", perfect.Findings)}]");
            Assert.True(perfect.Final >= 88f, "perfect job scores high");

            o.Draft.Set(DocField.Surname, "HADAD");
            var typo = DocumentEvaluator.Evaluate(Finish(s, o), o, spec, c, s.Photos);
            Assert.True(typo.Has(Issue.Misspelled), "typo detected");
            Assert.True(typo.Final < perfect.Final, "typo costs");

            o.Draft.Set(DocField.Surname, "HADDAD");
            o.Draft.Set(DocField.DateOfBirth, "23/11/1970");
            var fmt = DocumentEvaluator.Evaluate(Finish(s, o), o, spec, c, s.Photos);
            Assert.True(fmt.Has(Issue.WrongDateFormat) && !fmt.Has(Issue.WrongValue), "right date, wrong format");

            o.Draft.Set(DocField.DateOfBirth, "23.11.1970");
            o.Draft.Category = DocCategory.TypeA;
            o.Draft.Cover = CoverColor.Navy;
            var cat = DocumentEvaluator.Evaluate(Finish(s, o), o, spec, c, s.Photos);
            Assert.True(cat.Has(Issue.WrongCategory) && cat.Has(Issue.WrongCover), "format rules");
            Assert.True(cat.Final < 60f, $"two hard format errors sink it ({cat.Final:0})");

            o.Draft.Category = DocCategory.TypeC;
            o.Draft.Cover = CoverColor.DarkRed;
            o.Draft.CountryId = Countries.Germany;
            var wrongTemplate = DocumentEvaluator.Evaluate(Finish(s, o), o, spec, c, s.Photos);
            Assert.True(wrongTemplate.Has(Issue.WrongCountry) && wrongTemplate.Final < 15f, "wrong template is fatal");

            o.Draft.CountryId = Countries.France;
            o.Draft.PhotoId = null;
            var noPhoto = DocumentEvaluator.Evaluate(Finish(s, o), o, spec, c, s.Photos);
            Assert.True(noPhoto.Has(Issue.NoPhoto) && noPhoto.Final < 15f, "missing photo is fatal");
        }

        public static void Outcome_ProbabilityShape()
        {
            float hi = OutcomeModel.SuccessChance(95f, 0.15f, 0f, 0.1f);
            float mid = OutcomeModel.SuccessChance(65f, 0.15f, 0f, 0.1f);
            float lo = OutcomeModel.SuccessChance(40f, 0.15f, 0f, 0.1f);
            float hot = OutcomeModel.SuccessChance(95f, 0.6f, 90f, 0.8f);
            Console.WriteLine($"    P(success): q95={hi:0.00} q65={mid:0.00} q40={lo:0.00} q95-risky={hot:0.00}");
            Assert.True(hi > 0.9f && mid > 0.4f && mid < 0.75f && lo < 0.1f, "logistic shape");
            Assert.True(hot < hi - 0.2f, "risk and heat matter");
        }

        public static void Handbook_MatchesSpec()
        {
            foreach (var c in Countries.CreateAll())
            {
                var photo = Handbook.Photo(c);
                Assert.True(photo.Lines.Exists(l => l.Label == "BACKGROUND" && l.Value == Labels.Of(c.Photo.Backdrop)), $"{c.Id} backdrop");
                var pass = Handbook.Passport(c);
                Assert.True(pass.Lines.Exists(l => l.Label == "CATEGORY" && l.Value == Labels.Of(c.Passport.Category)), $"{c.Id} category");
                Assert.True(pass.Lines.Exists(l => l.Label == "DATE FORMAT" && l.Value == Labels.Pattern(c.Passport.DateFormat)), $"{c.Id} date format");
            }
        }

        public static void News_RuleChangeAndOutages()
        {
            var s = new GameSession(55);
            var fr = s.Country(Countries.France);
            var before = fr.Spec;
            s.InjectNews(new NewsItem { Effect = NewsEffect.RuleChange, CountryId = Countries.France, Headline = "TEST" });
            Assert.True(fr.Spec != before, "spec replaced");
            Assert.True(fr.HandbookOutdated, "handbook now outdated");
            Assert.True(s.HandbookRules(Countries.France) == before, "binder still shows the old rules");
            Console.WriteLine($"    rule change: {fr.LastRuleChange}");
            Assert.True(s.BuyHandbookUpdate(Countries.France), "update purchasable");
            Assert.True(!fr.HandbookOutdated && s.HandbookRules(Countries.France) == fr.Spec, "binder updated");

            s.InjectNews(new NewsItem { Effect = NewsEffect.PowerOutage, Magnitude = s.Clock.Minute + 10, Days = 60 });
            Assert.True(s.PowerOn, "power still on before the cut");
            s.DebugAdvanceMinutes(15);
            Assert.True(!s.PowerOn, "power cut active");
            s.DebugAdvanceMinutes(60);
            Assert.True(s.PowerOn, "power back");

            s.InjectNews(new NewsItem { Effect = NewsEffect.EquipmentSale, Magnitude = 0.75f, Days = 3 });
            var cam = EquipmentCatalog.Get("cam2");
            Assert.True(s.PriceOf(cam) < cam.Price, "sale discount");

            int days = 0, effects = 0;
            for (int d = 0; d < 30; d++)
            {
                s.Tick(1f);
                if (s.Phase != DayPhase.Closed)
                {
                    foreach (var v in s.Visits) v.Phase = VisitPhase.Gone;
                    s.Visits.Clear();
                    s.DebugAdvanceMinutes(Balance.CloseMinute - s.Clock.Minute + 1);
                    s.OpenShop();
                    s.Tick(0.1f);
                    s.CloseShop();
                }
                s.StartNextDay();
                days++;
                foreach (var n in s.NewsFor(s.Day)) if (n.Effect != NewsEffect.None) effects++;
            }
            Console.WriteLine($"    30 mornings: {s.News.Count} headlines, {effects} with effects");
            Assert.True(s.News.Count >= 30, "at least one headline per day");
            Assert.True(effects >= 10, "news moves the simulation");
        }

        // ------------------------------------------------------------------ full sessions

        public static void Session_Day1_PerfectJob_ToMessage()
        {
            var s = new GameSession(12345);
            Assert.Equal(DayPhase.Morning, s.Phase, "starts in the morning");
            Assert.True(s.Inbox.Messages.Count == 1, "welcome note waiting");
            s.OpenShop();

            var v = Bot.WaitForChoice(s, C.Accept, 300f);
            Assert.Equal(StoryCast.SamirId, v.Customer.Id, "Samir is the first customer");
            Assert.Equal(Countries.France, v.Request.CountryId, "France");
            s.Choose(v.Id, C.Accept);
            Bot.WaitForChoice(s, C.TakePhoto, 60f);
            Assert.True(v.Order != null && v.Order.Status == OrderStatus.AwaitingPhoto, "order awaiting photo");
            s.Choose(v.Id, C.TakePhoto);
            Bot.TickUntil(s, () => v.Phase == VisitPhase.PhotoSitting && !v.Moving, 30f, "seated");

            var photo = Bot.TakeGoodPhoto(s, v);
            var photoScore = PhotoEvaluator.Score(photo, s.Country(Countries.France).Spec.Photo);
            Console.WriteLine($"    kept photo: {photoScore} [{string.Join("; ", photoScore.Findings)}] coverage {photo.Coverage:0.00}");
            Assert.True(s.FinishPhotoSitting(v.Id), "customer gets up");
            Bot.TickUntil(s, () => v.Phase == VisitPhase.Waiting, 40f, "waiting on bench");

            Bot.FillPerfect(s, v.Order, photo);
            var doc = Bot.PrintAndBind(s, v.Order);
            Assert.Equal(OrderStatus.Ready, v.Order.Status, "ready after binding");
            Assert.True(s.GiveDocument(v.Id, doc.Id), "hand over");
            Bot.TickUntil(s, () => s.CounterCash.Count > 0, 60f, "cash on counter");
            Console.WriteLine($"    delivered: {v.Order.Quality} [{string.Join("; ", v.Order.Quality.Findings)}]");
            Assert.True(v.Order.Quality.Final >= 85f, "careful work scores high");

            int before = s.Ledger.Cash;
            int got = s.CollectCash(s.CounterCash[0].Id);
            Assert.Equal(450, got, "paid in full");
            Assert.Equal(before + 450, s.Ledger.Cash, "cash box");
            Bot.TickUntil(s, () => s.Visits.Count == 0, 60f, "Samir leaves");

            Bot.TickUntil(s, () => s.Phase == DayPhase.AfterHours, 2000f, "closing time");
            Assert.True(s.CloseShop(), "close the shop");
            var report = s.LastReport;
            Console.WriteLine($"    day 1: revenue {report.Revenue} expenses {report.Expenses} profit {report.Profit} rep {report.ReputationEnd:0} heat {report.HeatEnd:0}");
            Assert.Equal(450, report.Revenue, "revenue");

            var outcome = s.PendingOutcomes.Single();
            Console.WriteLine($"    outcome decided: {outcome.Kind} on day {outcome.ResolvesAt.Day}");
            int messages = s.Inbox.Messages.Count;
            for (int guard = 0; guard < 12 && !outcome.Applied; guard++)
            {
                s.StartNextDay();
                RunDeclineDay(s);
            }
            Assert.True(outcome.Applied, "outcome resolved within a dozen days");
            if (outcome.Kind != OutcomeKind.Unknown)
            {
                var msg = s.Inbox.Messages.First(m => m.OrderId == v.Order.Id);
                Console.WriteLine($"    day {msg.ArrivesAt.Day} message from {msg.From}: \"{msg.Body}\"");
                Assert.True(s.Inbox.Messages.Count > messages, "message arrived");
                var rec = s.Clients.RecordFor(StoryCast.SamirId, s.Day);
                Assert.True(rec.Status == ClientStatus.Success || rec.Status == ClientStatus.Failed || rec.Status == ClientStatus.Missing, "client status updated");
                if (outcome.Kind == OutcomeKind.Success) Assert.True(s.HasFlag("brother_scheduled"), "brother arc scheduled after success");
            }
        }

        public static void Session_Day1_SloppyJob()
        {
            var s = new GameSession(999);
            s.OpenShop();
            var v = Bot.WaitForChoice(s, C.AskBig, 300f);
            s.Choose(v.Id, C.AskBig);
            // Samir can't pay more: expect a counter at the same $450.
            var again = Bot.WaitForChoice(s, C.Accept, 60f);
            Assert.Equal(450, v.Negotiation.Current, "Samir's final offer is all he has");
            s.Choose(v.Id, C.Accept);
            Bot.WaitForChoice(s, C.TakePhoto, 60f);
            s.Choose(v.Id, C.TakePhoto);
            Bot.TickUntil(s, () => v.Phase == VisitPhase.PhotoSitting && !v.Moving, 30f, "seated");
            var photo = Bot.TakeGoodPhoto(s, v, careful: false);
            s.FinishPhotoSitting(v.Id);
            Bot.TickUntil(s, () => v.Phase == VisitPhase.Waiting, 40f, "bench");

            // Lazy editor work: right template, wrong details.
            var d = v.Order.Draft;
            d.CountryId = Countries.France;
            d.Category = DocCategory.TypeA;
            d.Cover = CoverColor.Navy;
            d.Pages = 24;
            d.PhotoId = photo.Id;
            d.Set(DocField.Surname, "HADAD");
            d.Set(DocField.GivenNames, "SAMIR");
            d.Set(DocField.DateOfBirth, "11/23/1970");
            var doc = Bot.PrintAndBind(s, v.Order);
            s.GiveDocument(v.Id, doc.Id);
            Bot.TickUntil(s, () => v.Phase == VisitPhase.Choosing || s.CounterCash.Count > 0, 60f, "reaction");
            Console.WriteLine($"    sloppy: {v.Order.Quality} [{string.Join("; ", v.Order.Quality.Findings)}]");
            Assert.True(v.Order.Quality.Final < 40f, "sloppy work scores low");
            if (v.Phase == VisitPhase.Choosing)
            {
                Console.WriteLine($"    customer noticed {v.Noticed} problem(s); choices: {string.Join(", ", v.Choices.Select(c => c.Text))}");
                s.Choose(v.Id, C.TrustMe);
                Bot.TickUntil(s, () => v.Phase == VisitPhase.Choosing || s.CounterCash.Count > 0 || v.Phase == VisitPhase.Gone, 60f, "after persuasion");
            }
        }

        /// <summary>Opens, declines everyone politely, closes.</summary>
        static void RunDeclineDay(GameSession s)
        {
            s.OpenShop();
            float guard = 0f;
            while (true)
            {
                foreach (var v in s.Visits.ToArray())
                {
                    if (v.Phase != VisitPhase.Choosing) continue;
                    var c = v.Choices.FirstOrDefault(x => x.Id == C.Decline) ?? v.Choices.FirstOrDefault(x => x.Enabled);
                    if (c != null) s.Choose(v.Id, c.Id);
                }
                if ((s.Phase == DayPhase.AfterHours) && s.CloseShop()) return;
                s.Tick(Bot.Step);
                guard += Bot.Step;
                if (guard > 3000f) throw new AssertException($"day {s.Day} never ended ({s.Phase}, {s.Now}, visits {s.Visits.Count})");
            }
        }

        public static void Session_TwentyDays_Bot()
        {
            var s = new GameSession(2024);
            var rng = new Rng(77);
            var printedFor = new HashSet<int>();
            int handed = 0, photos = 0, choices = 0;
            for (int day = 1; day <= 20; day++)
            {
                if (day > 1) s.StartNextDay();
                s.OpenShop();
                // Buy the next handbook section when affordable.
                foreach (var c in s.CountryList)
                    if (!c.Owned && s.Standing.Reputation >= c.Spec.UnlockReputation && s.Ledger.Cash > c.Spec.HandbookPrice + 300)
                        s.BuyHandbook(c.Id);

                float guard = 0f;
                bool dumped = false;
                while (true)
                {
                    foreach (var v in s.Visits.ToArray())
                    {
                        if (v.Phase == VisitPhase.Choosing)
                        {
                            choices++;
                            string pick = PickChoice(v, rng);
                            s.Choose(v.Id, pick);
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

                    if (s.Phase == DayPhase.AfterHours && s.CloseShop()) break;
                    if (s.Phase == DayPhase.AfterHours && s.Clock.Minute > Balance.CloseMinute + 120 && !dumped)
                    {
                        dumped = true;
                        Console.WriteLine($"    !! day {day} still open at {s.Now.Clock}: " + string.Join(" | ", s.Visits.Select(x =>
                            $"{x.Customer.FirstName} phase={x.Phase} from={x.From} to={x.To} moveT={x.MoveT:0.00} talking={x.IsTalking} " +
                            $"choices=[{string.Join(",", x.Choices.Select(c => c.Id + (c.Enabled ? "" : "(off)")))}] order={x.Order?.Status} " +
                            $"kind={x.Kind} patience={x.PatienceFraction:0.00} subj={(x.Subject != null)} active={(s.ActivePhotoVisit == x)}")));
                    }
                    if (s.Phase == DayPhase.Closed) break;
                    s.Tick(Bot.Step);
                    guard += Bot.Step;
                    if (guard > 4000f) throw new AssertException($"day {day} stuck: {s.Phase} {s.Now} visits={s.Visits.Count} " +
                        string.Join(",", s.Visits.Select(x => $"{x.Customer.FirstName}:{x.Phase}:{x.To}")));
                }
                var r = s.LastReport;
                Console.WriteLine($"    day {r.Day,2}: cust {r.Customers} acc {r.JobsAccepted} done {r.JobsCompleted} open {r.OrdersOpen} " +
                    $"rev {r.Revenue,5} exp {r.Expenses,4} cash {r.CashEnd,6} rep {r.ReputationEnd,5:0} heat {r.HeatEnd,4:0} msgs {r.MessagesReceived}");
            }
            int successes = s.Orders.All.Count(o => o.Outcome == OutcomeKind.Success);
            int failures = s.Orders.All.Count(o => o.Outcome == OutcomeKind.Failed);
            Console.WriteLine($"    totals: orders {s.Orders.All.Count}, handed {handed}, photos {photos}, choices {choices}, " +
                $"success {successes}, failed {failures}, inbox {s.Inbox.Messages.Count}, clients {s.Clients.Count}, owned " +
                string.Join("/", s.CountryList.Where(c => c.Owned).Select(c => c.Id)));
            Assert.True(s.Orders.All.Count >= 10, "a steady stream of work over 20 days");
            Assert.True(handed >= 8, "documents get delivered");
            Assert.True(successes + failures > 0, "outcomes land");
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
    }
}
