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
            Emit(SimEventType.PhotoKept, orderId: o?.Id ?? 0, amount: photo.Id);
        }

        /// <summary>"That's all, thanks." The customer gets up. Fails if no photo was kept yet.</summary>
        public bool FinishPhotoSitting(int visitId)
        {
            var v = GetVisit(visitId);
            if (v == null || v.Phase != VisitPhase.PhotoSitting || v.Moving) return false;
            var o = v.Order;
            if (o == null || Photos.ForCustomer(v.Customer.Id).Count == 0)
            {
                v.Say(v.Customer.Has(Trait.Impatient) ? "We're not even done?" : "So... no photo?");
                return false;
            }
            if (ActivePhotoVisit == v) EndPhotoSession();
            v.Subject?.Redress();
            v.Subject = null;
            if (o.Status == OrderStatus.AwaitingPhoto) o.Status = OrderStatus.InProduction;

            v.ClearLines();
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
                    if (o.Document != null) o.Status = OrderStatus.Ready;
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
            foreach (var f in report.Findings)
            {
                float n = IssueInfo.Noticeability(f);
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

            if (noticed > 0)
            {
                var docCountry = Country(v.Holding?.Data.CountryId)?.Spec;
                v.Say(Lines.Complaint(v.Customer, worst, docCountry, Rng));
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
            var po = OutcomeModel.Decide(o, v.Customer, country.Risk(Day), Standing.Heat, Clock.Now, city, Rng);
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
