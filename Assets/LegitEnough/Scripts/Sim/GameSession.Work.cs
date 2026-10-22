using System;
using System.Globalization;

namespace LegitEnough.Sim
{
    /// <summary>The work itself: photographs, printing, binding, handing over, getting paid.</summary>
    public sealed partial class GameSession
    {
        // ------------------------------------------------------------------ photography

        void SitForPhoto(Visit v)
        {
            v.Phase = VisitPhase.PhotoSitting;
            if (v.Subject == null)
                v.Subject = new PhotoSubject(v.Customer, Rng.For(Seed, (ulong)(v.Id * 7919 + v.Customer.Id)));
            v.Say(Lines.SitDown(v.Customer, Rng));
        }

        /// <summary>Look through the camera at the customer on the stool.</summary>
        public PhotoSession BeginPhotoSession(int visitId)
        {
            var v = GetVisit(visitId);
            if (v == null || v.Phase != VisitPhase.PhotoSitting || v.Moving || v.Subject == null) return null;
            if (ActivePhoto != null)
            {
                if (ActivePhotoVisit == v) return ActivePhoto;
                EndPhotoSession();
            }
            ActivePhoto = new PhotoSession(v.Subject, Rig, Camera, Station, v.Order?.Id ?? 0, Photos.NextId, () => Clock.Now);
            ActivePhotoVisit = v;
            return ActivePhoto;
        }

        /// <summary>Step away from the camera; the customer stays seated.</summary>
        public void EndPhotoSession()
        {
            ActivePhoto = null;
            ActivePhotoVisit = null;
        }

        void TickPhoto(float dt)
        {
            if (ActivePhoto == null) return;
            var v = ActivePhotoVisit;
            if (v == null || v.Phase == VisitPhase.Gone || v.Subject != ActivePhoto.Subject)
            {
                EndPhotoSession();
                return;
            }
            ActivePhoto.Tick(dt);
            while (ActivePhoto.Subject.TryTakeLine(out var line)) v.Say(line);
        }

        public void KeepPhoto(PhotoCapture photo)
        {
            if (photo == null || photo.Kept) return;
            Photos.Keep(photo);
            var o = Orders.OpenFor(photo.CustomerId);
            if (o != null && o.Status == OrderStatus.AwaitingPhoto) o.Status = OrderStatus.InProduction;
            if (o != null) o.NeedsRetake = false;
            Emit(SimEventType.PhotoKept, orderId: o?.Id ?? 0, amount: photo.Id);
        }

        /// <summary>"That's all, thanks." The customer gets up. Fails if no photo was kept yet.</summary>
        public bool FinishPhotoSitting(int visitId)
        {
            var v = GetVisit(visitId);
            if (v == null || v.Phase != VisitPhase.PhotoSitting || v.Moving) return false;
            var o = v.Order;
            if (o == null || Photos.ForCustomer(v.Customer.Id).Count == 0 || o.NeedsRetake)
            {
                v.Say(v.Customer.Has(Trait.Impatient) ? "We're not even done?" : "So... no photo?");
                return false;
            }
            if (ActivePhotoVisit == v) EndPhotoSession();
            v.Subject?.Redress();
            v.Subject = null;
            if (o.Status == OrderStatus.AwaitingPhoto) o.Status = OrderStatus.InProduction;

            v.ClearLines();
            if (v.Retake)
            {
                // The player's own retake: fixed lines (no dice), back to the bench if there's room.
                v.Retake = false;
                if (FreeBenchSeat(v) >= 0)
                {
                    v.Say(Lines.RetakeDone);
                    Then(v, () => WalkTo(v, Spot.Bench, () => v.Phase = VisitPhase.Waiting));
                    return true;
                }
            }
            v.Say(Lines.AfterPhoto(v.Customer, Rng));
            if (o.WaitsInShop && FreeBenchSeat(v) >= 0)
            {
                v.Say(Lines.WillWait(v.Customer, Rng));
                Then(v, () => WalkTo(v, Spot.Bench, () => v.Phase = VisitPhase.Waiting));
            }
            else
            {
                var at = o.WaitsInShop ? Clock.Now.AddMinutes(90) : o.Deadline;
                v.Say(Lines.WillReturn(v.Customer, at, Day, Rng));
                ConvertToPickup(o, at);
                LeaveAfterLines(v);
            }
            return true;
        }

        /// <summary>
        /// The player wants a better photo of someone whose order already has one: they're waiting on
        /// the bench (or at the counter for their booklet), nothing's handed over yet, and the stool is free.
        /// </summary>
        public bool CanCallBackForPhoto(int visitId)
        {
            var v = GetVisit(visitId);
            if (v == null || v.Moving || v.Leaving || v.Phase == VisitPhase.Gone) return false;
            var o = v.Order;
            if (o == null || !o.IsOpen || o.Status == OrderStatus.AwaitingPhoto || o.NeedsRetake) return false;
            if (v.Holding != null || v.PendingHandover != null) return false;
            bool bench = v.Phase == VisitPhase.Waiting && v.To == Spot.Bench;
            bool counter = v.Phase == VisitPhase.AwaitingHandover && v.To == Spot.Counter;
            if (!bench && !counter) return false;
            return StoolFree(v) && Photos.ForCustomer(v.Customer.Id).Count > 0;
        }

        /// <summary>
        /// "One more photo?" Calls them back to the stool (costs a little patience; fed-up customers
        /// say no). Their kept photos stay: the editor picks which one goes in. A booklet already made
        /// stays on the desk; print again with the new photo. Draws no dice, so it never changes what
        /// happens next in the shop.
        /// </summary>
        public bool CallBackForPhoto(int visitId)
        {
            if (!CanCallBackForPhoto(visitId)) return false;
            var v = GetVisit(visitId);
            bool tutorialGuest = TutorialActive && v.Beat?.BeatId == StoryCast.SamirBeatId;
            if (!tutorialGuest && v.PatienceFraction < Balance.RetakeRefuseBelow)
            {
                v.ClearLines();
                v.Say(Lines.RetakeRefused);
                return false;
            }
            v.Patience -= v.PatienceMax * Balance.RetakePatienceCost;
            v.Retake = true;
            v.Retakes++;
            v.RetakeAfterPhoto = -1;
            foreach (var p in Photos.ForCustomer(v.Customer.Id)) v.RetakeAfterPhoto = Math.Max(v.RetakeAfterPhoto, p.Id);
            v.ClearLines();
            v.Say(Lines.RetakeCalled(v.Customer));
            WalkTo(v, Spot.Stool, () => SitForRetake(v));
            return true;
        }

        void SitForRetake(Visit v)
        {
            v.Phase = VisitPhase.PhotoSitting;
            // A fresh sitting (they shift on the stool, blink on their own clock), from its own seed.
            ulong salt = (ulong)(v.Id * 7919 + v.Customer.Id) + (ulong)v.Retakes * 104729UL;
            v.Subject = new PhotoSubject(v.Customer, Rng.For(Seed, salt));
        }

        // ------------------------------------------------------------------ the computer

        public bool ImportNames(int orderId)
        {
            var o = Orders.Get(orderId);
            if (o == null || !Computer.ImportsNames) return false;
            var id = Clients.Get(o.CustomerId).Identity;
            o.Draft.Set(DocField.Surname, id.Surname);
            o.Draft.Set(DocField.GivenNames, id.GivenNames);
            return true;
        }

        /// <summary>Workstation feature: rewrite any parseable date into the template's format.</summary>
        public bool AutoFormatDates(int orderId)
        {
            var o = Orders.Get(orderId);
            var c = Country(o?.Draft.CountryId);
            if (o == null || c == null || !Computer.AutoFormatsDates) return false;
            foreach (var f in new[] { DocField.DateOfBirth, DocField.DateOfIssue, DocField.DateOfExpiry })
            {
                if (Labels.TryParseAnyFormat(o.Draft.Get(f), out var d))
                    o.Draft.Set(f, Labels.FormatDate(d, c.Spec.Passport.DateFormat));
            }
            return true;
        }

        /// <summary>The editor's GENERATE button. Obviously fictional numbering.</summary>
        public string GenerateDocumentNumber(int orderId)
        {
            var o = Orders.Get(orderId);
            if (o == null) return "";
            string prefix = o.Draft.CountryId ?? "XX";
            char letter = (char)('A' + Rng.Range(0, 26));
            string number = $"{prefix}-{Rng.Range(1000, 9999).ToString(CultureInfo.InvariantCulture)}-{Rng.Range(100, 999).ToString(CultureInfo.InvariantCulture)}{letter}";
            o.Draft.Set(DocField.DocumentNumber, number);
            return number;
        }

        // ------------------------------------------------------------------ printer & binding

        public PrintedPage PrintDraft(int orderId)
        {
            var o = Orders.Get(orderId);
            if (o == null || !o.IsOpen) return null;
            Ledger.Record(Clock.Now, LedgerKind.Supplies, -Balance.PrintCost, "Ink & paper");
            var page = new PrintedPage { Id = _nextPageId++, OrderId = o.Id, Data = o.Draft.Clone(), PrintedAt = Clock.Now };
            Printer.Enqueue(page);
            o.PagesPrinted++;
            Emit(SimEventType.PrintQueued, orderId: o.Id, amount: page.Id);
            return page;
        }

        void TickPrinter(float dt)
        {
            bool wasJammed = Printer.Jammed;
            Printer.Powered = PowerOn;
            var done = Printer.Tick(dt, Rng);
            if (done != null)
            {
                done.PrintedAt = Clock.Now;
                Emit(SimEventType.PagePrinted, orderId: done.OrderId, amount: done.Id);
            }
            if (Printer.Jammed && !wasJammed) Emit(SimEventType.PrinterJammed);
        }

        public void ClearPrinterJam() => Printer.ClearJam(Rng);

        /// <summary>Put a printed data page into a blank booklet of the chosen cover colour.</summary>
        public FinishedDocument BindPage(int pageId)
        {
            var page = Printer.TakeFromTray(pageId);
            if (page == null) return null;
            Ledger.Record(Clock.Now, LedgerKind.Supplies, -Balance.BlankBookletCost, "Blank booklet");
            Clock.Spend(Balance.BindMinutes);
            var doc = new FinishedDocument
            {
                Id = _nextDocId++,
                OrderId = page.OrderId,
                Data = page.Data,
                IssuedDay = page.PrintedAt.Day,
                BookletCover = page.Data.Cover ?? CoverColor.Black,
                BoundAt = Clock.Now,
                Smudged = page.Smudged,
            };
            DeskDocuments.Add(doc);
            var o = Orders.Get(page.OrderId);
            if (o != null && o.IsOpen)
            {
                o.Document = doc;
                o.Status = OrderStatus.Ready;
            }
            Emit(SimEventType.DocumentBound, orderId: doc.OrderId, amount: doc.Id);
            return doc;
        }

        public void ShredPage(int pageId)
        {
            var page = Printer.TakeFromTray(pageId);
            if (page != null) Emit(SimEventType.DocumentShredded, orderId: page.OrderId);
        }

        public void ShredDocument(int docId)
        {
            var doc = DeskDocuments.Find(d => d.Id == docId);
            if (doc == null) return;
            DeskDocuments.Remove(doc);
            var o = Orders.Get(doc.OrderId);
            if (o != null && o.Document == doc)
            {
                o.Document = DeskDocuments.Find(d => d.OrderId == o.Id);
                if (o.IsOpen)
                {
                    if (o.NeedsRetake) o.Status = OrderStatus.AwaitingPhoto;
                    else if (o.Document != null) o.Status = OrderStatus.Ready;
                    else o.Status = Photos.ForCustomer(o.CustomerId).Count > 0 ? OrderStatus.InProduction : OrderStatus.AwaitingPhoto;
                }
            }
            Emit(SimEventType.DocumentShredded, orderId: doc.OrderId);
        }

        // ------------------------------------------------------------------ delivery & payment

        /// <summary>
        /// Player hands a booklet to a customer. Customers on the bench walk over first.
        /// Any booklet can be handed to anyone — handing over the wrong one is a mistake, not a rule.
        /// </summary>
        public bool GiveDocument(int visitId, int docId)
        {
            var v = GetVisit(visitId);
            var doc = DeskDocuments.Find(d => d.Id == docId);
            if (v == null || doc == null || v.Order == null || !v.Order.IsOpen) return false;

            bool atCounter = v.To == Spot.Counter && !v.Moving;
            if (atCounter && (v.Phase == VisitPhase.AwaitingHandover || v.Phase == VisitPhase.Choosing))
            {
                v.ClearLines();
                v.Choices.Clear();
                HandOver(v, doc);
                return true;
            }
            if (v.Phase == VisitPhase.Waiting && CounterFree(v))
            {
                DeskDocuments.Remove(doc);
                v.PendingHandover = doc;
                WalkTo(v, Spot.Counter, () => ArriveAtCounter(v));
                return true;
            }
            return false;
        }

        void HandOver(Visit v, FinishedDocument doc)
        {
            DeskDocuments.Remove(doc);
            v.Holding = doc;
            v.Phase = VisitPhase.Inspecting;
            v.InspectTimer = 2.6f;
            v.Say(Lines.Inspecting(v.Customer, Rng));
            var order = v.Order;
            order.Quality = DocumentEvaluator.Evaluate(doc, order, Country(order.CountryId).Spec, v.Customer, Photos);
            Emit(SimEventType.DocumentHandedOver, v.Id, order.Id, doc.Id);
        }

        void FinishInspection(Visit v)
        {
            var report = v.Order.Quality;
            float scrutiny = 0.55f + 0.7f * (1f - v.Customer.Stats.Trust);
            Finding worst = default;
            float worstScore = 0f;
            int noticed = 0;
            // The demo's rush client read this morning's paper: he checks the French background.
            bool rulesReader = IsDemo && v.Beat?.BeatId == DemoCast.UrgentBeatId;
            foreach (var f in report.Findings)
            {
                float n = IssueInfo.Noticeability(f);
                if (rulesReader && f.Issue == Issue.WrongBackdrop) n = 1f;
                if (n <= 0f || !Rng.Chance(Math.Min(1f, n * scrutiny))) continue;
                noticed++;
                float s = n * (f.Penalty + (1f - f.Multiplier) * 100f);
                if (s > worstScore)
                {
                    worstScore = s;
                    worst = f;
                }
            }
            v.Noticed = noticed;
            v.HasComplaint = noticed > 0;
            v.Complaint = worst;

            if (noticed > 0)
            {
                var docCountry = Country(v.Holding?.Data.CountryId)?.Spec;
                string complaint = Lines.Complaint(v.Customer, worst, docCountry, Rng);
                v.Say(rulesReader && worst.Issue == Issue.WrongBackdrop ? DemoCast.UrgentBackdropComplaint : complaint);
                Then(v, () => ShowChoices(v,
                    new Choice(ChoiceIds.FixIt, Lines.Player.FixIt),
                    new Choice(ChoiceIds.TrustMe, Lines.Player.TrustMe)));
            }
            else
            {
                v.Say(Lines.Satisfied(v.Customer, Rng));
                Then(v, () => Pay(v, v.Order.Price, false));
            }
        }

        void TryPersuade(Visit v)
        {
            var s = v.Customer.Stats;
            float chance = 0.2f + 0.5f * s.Trust + 0.2f * s.Desperation - 0.12f * Math.Max(0, v.Noticed - 1);
            if (Rng.Chance(chance))
            {
                v.Say(Lines.Persuaded(v.Customer, Rng));
                bool reduced = s.Desperation < 0.7f;
                int amount = reduced ? Pricing.RoundTo((int)(v.Order.Price * 0.8f), 25) : v.Order.Price;
                Standing.AddReputation(-2f);
                Then(v, () => Pay(v, amount, reduced));
            }
            else
            {
                v.Say(Lines.NotPersuaded(v.Customer, Rng));
                if (TutorialActive && v.Beat?.BeatId == StoryCast.SamirBeatId)
                    Then(v, () => ShowChoices(v, new Choice(ChoiceIds.FixIt, Lines.Player.FixIt)));
                else
                    Then(v, () => ShowChoices(v,
                        new Choice(ChoiceIds.FixIt, Lines.Player.FixIt),
                        new Choice(ChoiceIds.GetOut, Lines.Player.GetOut)));
            }
        }

        void Pay(Visit v, int amount, bool reduced)
        {
            var o = v.Order;
            o.Paid = amount;
            o.Status = OrderStatus.Delivered;
            o.DeliveredAt = Clock.Now;
            if (v.Holding != null) o.Document = v.Holding;
            v.Holding = null;

            CounterCash.Add(new CashPile { Id = _nextCashId++, Amount = amount, OrderId = o.Id, From = v.Customer.FirstName });
            Emit(SimEventType.CashPlaced, v.Id, o.Id, amount);
            _jobsCompletedToday++;

            var record = Clients.RecordFor(v.Customer.Id, Day);
            record.Jobs++;
            record.TotalPaid += amount;
            Country(o.CountryId).JobsDone++;

            bool late = Clock.Now > o.Deadline;
            if (late) Standing.AddReputation(-5f);
            else if (!reduced) Standing.AddReputation(3f);

            PlanOutcome(v, o);
            v.Say(Lines.Pay(v.Customer, amount, reduced, Rng));
            v.Say(Lines.Farewell(v.Customer, Rng));
            LeaveAfterLines(v);
        }

        void PlanOutcome(Visit v, Order o)
        {
            var country = Country(o.CountryId);
            string city = string.IsNullOrEmpty(v.Request?.DestinationCity)
                ? NamePools.DestinationCity(o.CountryId, Rng)
                : v.Request.DestinationCity;
            // The demo's first job always reports back, on the Day 2 morning card.
            bool demoSamir = IsDemo && v.Beat?.BeatId == StoryCast.SamirBeatId;
            var po = OutcomeModel.Decide(o, v.Customer, country.Risk(Day), Standing.Heat, Clock.Now, city, Rng, demoSamir);
            if (demoSamir)
            {
                // Decide still ran (same dice as always); the demo verdict is the passport's quality alone.
                po.Kind = DemoCast.SamirOutcome(o.Quality?.Final ?? 0f);
                po.ResolvesAt = DemoCast.SamirOutcomeAt(Clock.Now);
            }
            PendingOutcomes.Add(po);
            o.OutcomeDay = po.ResolvesAt.Day;
            if (po.Kind == OutcomeKind.Unknown) return;

            string body = po.Kind switch
            {
                OutcomeKind.Success => v.Beat?.SuccessMessage ?? MessageTemplates.Success(v.Customer, city, Rng),
                OutcomeKind.Failed => v.Beat?.FailureMessage ?? MessageTemplates.Failure(v.Customer, city, o.Price, Rng),
                _ => MessageTemplates.Missing(v.Customer, Rng),
            };
            string from = po.Kind == OutcomeKind.Missing
                ? MessageTemplates.MissingSender(v.Customer, Rng)
                : v.Customer.FirstName.ToUpperInvariant();
            Inbox.Schedule(new Message
            {
                ArrivesAt = po.ResolvesAt,
                From = from,
                CustomerId = v.Customer.Id,
                OrderId = o.Id,
                Subject = MessageTemplates.Subject(po.Kind, Rng),
                Body = body,
                Kind = MessageKind.Outcome,
            });
        }

        /// <summary>Sweep the cash on the counter into the cash box.</summary>
        public int CollectCash(int pileId)
        {
            var pile = CounterCash.Find(c => c.Id == pileId);
            if (pile == null) return 0;
            CounterCash.Remove(pile);
            Ledger.Record(Clock.Now, LedgerKind.Payment, pile.Amount, $"Payment from {pile.From}");
            Emit(SimEventType.CashCollected, orderId: pile.OrderId, amount: pile.Amount);
            return pile.Amount;
        }

        // ------------------------------------------------------------------ debug

        /// <summary>Development helper: a walk-in arrives right now.</summary>
        public void DebugSpawnWalkIn()
        {
            if (Phase == DayPhase.Morning) OpenShop();
            StartVisit(new ScheduledArrival { At = Clock.Now, Kind = ArrivalKind.NewJob });
        }

        public void DebugAdvanceMinutes(float minutes) => Clock.Spend(minutes);
    }
}
