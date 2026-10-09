using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>Customer flow: arrivals, walking, talking, negotiating, waiting, leaving.</summary>
    public sealed partial class GameSession
    {
        public static class ChoiceIds
        {
            public const string Accept = "accept";
            public const string AskSmall = "ask_small";
            public const string AskBig = "ask_big";
            public const string Push = "push";
            public const string Decline = "decline";
            public const string TakePhoto = "photo";
            public const string WaitBench = "wait_bench";
            public const string FixIt = "fix_it";
            public const string TrustMe = "trust_me";
            public const string GetOut = "get_out";
            public const string GiveHour = "give_hour";
            public const string ComeTomorrow = "come_tomorrow";
            public const string CantDoIt = "cant_do";
            public const string AlmostDone = "almost_done";
        }

        // ------------------------------------------------------------------ queries

        /// <summary>The customer standing at (or walking to) the counter.</summary>
        public Visit CounterVisit => Visits.Find(v => v.Phase != VisitPhase.Gone && v.To == Spot.Counter);

        public Visit StoolVisit => Visits.Find(v => v.Phase != VisitPhase.Gone && v.To == Spot.Stool);

        public IEnumerable<Visit> BenchVisits
        {
            get
            {
                foreach (var v in Visits)
                    if (v.Phase != VisitPhase.Gone && v.To == Spot.Bench) yield return v;
            }
        }

        bool CounterFree(Visit except = null)
        {
            foreach (var v in Visits)
                if (v != except && v.Phase != VisitPhase.Gone && v.To == Spot.Counter) return false;
            return true;
        }

        bool StoolFree(Visit except = null)
        {
            foreach (var v in Visits)
                if (v != except && v.Phase != VisitPhase.Gone && v.To == Spot.Stool) return false;
            return true;
        }

        int FreeBenchSeat(Visit except = null)
        {
            for (int seat = 0; seat < BenchSeats; seat++)
            {
                bool taken = false;
                foreach (var v in Visits)
                    if (v != except && v.Phase != VisitPhase.Gone && v.To == Spot.Bench && v.BenchSeat == seat) taken = true;
                if (!taken) return seat;
            }
            return -1;
        }

        // ------------------------------------------------------------------ arrivals

        void ProcessArrivals(float dt)
        {
            var a = Director.PopDue(Clock.Now);
            if (a == null) return;

            bool roomAtCounter = CounterFree();
            bool roomOnBench = FreeBenchSeat() >= 0;
            if (!roomAtCounter && !roomOnBench)
            {
                // Full house. Walk-ins sometimes give up; everyone else tries again later.
                if ((a.Kind == ArrivalKind.NewJob || a.Kind == ArrivalKind.Referral) && Rng.Chance(0.5f)) return;
                Director.Postpone(a, 20f);
                return;
            }
            StartVisit(a);
        }

        void StartVisit(ScheduledArrival a)
        {
            CustomerProfile customer;
            CustomerProfile companion = null;
            Order order = null;
            switch (a.Kind)
            {
                case ArrivalKind.Story:
                    customer = Clients.Get(a.Beat.CustomerId);
                    if (a.Beat.CompanionId.HasValue) companion = Clients.Get(a.Beat.CompanionId.Value);
                    break;
                case ArrivalKind.Pickup:
                    order = Orders.Get(a.OrderId);
                    if (order == null || !order.IsOpen) return;
                    customer = Clients.Get(order.CustomerId);
                    break;
                default:
                    customer = PickWalkIn();
                    break;
            }

            var v = new Visit
            {
                Id = _nextVisitId++,
                Customer = customer,
                Companion = companion,
                Kind = a.Kind,
                Beat = a.Beat,
                Order = order,
            };
            float patience = Balance.PatienceSecondsMin + (Balance.PatienceSecondsMax - Balance.PatienceSecondsMin) * customer.Stats.Patience;
            if (customer.StoryId != null) patience *= 1.5f;
            if (Day == 1) patience *= 1.4f;
            v.PatienceMax = v.Patience = patience;
            if (a.Kind == ArrivalKind.Referral) v.Request = new JobRequest { CustomerId = customer.Id };

            Visits.Add(v);
            Clients.NoteVisit(customer.Id, Day);
            if (a.Kind != ArrivalKind.Pickup) _customersToday++;
            if (a.Kind == ArrivalKind.Referral)
            {
                var referrer = Clients.Get(a.ReferredBy);
                v.Say($"{referrer.FirstName} gave me your number.");
            }

            Emit(SimEventType.DoorOpened, v.Id);
            Emit(SimEventType.VisitArrived, v.Id, order?.Id ?? 0);
            v.From = Spot.Outside;
            WalkTo(v, Spot.Door, () => RouteFromDoor(v));
        }

        CustomerProfile PickWalkIn()
        {
            // Occasionally a satisfied former client comes back for more.
            if (Rng.Chance(0.12f))
            {
                var returning = new List<ClientRecord>();
                foreach (var r in Clients.Records)
                {
                    if (r.Status != ClientStatus.Success || r.LastVisitDay >= Day) continue;
                    var p = Clients.Get(r.CustomerId);
                    if (p.StoryId == null && Visits.Find(x => x.Customer.Id == p.Id) == null) returning.Add(r);
                }
                if (returning.Count > 0) return Clients.Get(Rng.Pick(returning).CustomerId);
            }
            return Clients.CreateNew();
        }

        void RouteFromDoor(Visit v)
        {
            if (CounterFree(v)) WalkTo(v, Spot.Counter, () => ArriveAtCounter(v));
            else WalkTo(v, Spot.Bench, () => v.Phase = VisitPhase.Queueing);
        }

        // ------------------------------------------------------------------ movement

        static float WalkSeconds(Spot a, Spot b)
        {
            if (a == b) return 0.4f;
            if (a == Spot.Outside || b == Spot.Outside) return 0.9f;
            bool Is(Spot x, Spot y) => (a == x && b == y) || (a == y && b == x);
            if (Is(Spot.Door, Spot.Counter)) return 3.4f;
            if (Is(Spot.Door, Spot.Bench)) return 2.6f;
            if (Is(Spot.Door, Spot.Stool)) return 3.6f;
            if (Is(Spot.Counter, Spot.Stool)) return 2.4f;
            if (Is(Spot.Counter, Spot.Bench)) return 2.6f;
            if (Is(Spot.Bench, Spot.Stool)) return 3f;
            return 2.5f;
        }

        void WalkTo(Visit v, Spot to, Action onArrive)
        {
            Spot from = v.At;
            v.Choices.Clear();
            v.From = from;
            v.To = to;
            v.BenchSeat = to == Spot.Bench ? FreeBenchSeat(v) : -1;
            if (to == Spot.Bench && v.BenchSeat < 0) v.BenchSeat = 0;
            v.MoveDuration = WalkSeconds(from, to);
            v.MoveT = 0f;
            v.Phase = VisitPhase.Walking;
            v.Next = onArrive;
        }

        /// <summary>Continue with <paramref name="next"/> once queued lines finish (immediately if silent).</summary>
        void Then(Visit v, Action next)
        {
            if (!v.IsTalking)
            {
                v.AfterLines = null;
                next();
                return;
            }
            v.Phase = VisitPhase.Talking;
            v.AfterLines = next;
        }

        void ShowChoices(Visit v, params Choice[] choices)
        {
            v.Choices.Clear();
            v.Choices.AddRange(choices);
            v.Phase = VisitPhase.Choosing;
        }

        /// <summary>Finish what they're saying, then walk out. Nothing can interrupt them after this.</summary>
        void LeaveAfterLines(Visit v)
        {
            v.Leaving = true;
            v.Choices.Clear();
            Then(v, () => Leave(v));
        }

        void Leave(Visit v)
        {
            v.Leaving = true;
            if (ActivePhotoVisit == v) EndPhotoSession();
            v.Subject?.Redress();
            v.Choices.Clear();
            if (v.Holding != null)
            {
                DeskDocuments.Add(v.Holding);
                v.Holding = null;
            }
            if (v.PendingHandover != null)
            {
                DeskDocuments.Add(v.PendingHandover);
                v.PendingHandover = null;
            }
            WalkTo(v, Spot.Door, () =>
            {
                Emit(SimEventType.DoorOpened, v.Id);
                WalkTo(v, Spot.Outside, () =>
                {
                    v.Phase = VisitPhase.Gone;
                    Emit(SimEventType.VisitLeft, v.Id);
                });
            });
        }

        void LeaveAngry(Visit v)
        {
            if (v.Leaving) return;
            v.Leaving = true;
            if (ActivePhotoVisit == v) EndPhotoSession();
            v.Choices.Clear();
            v.ClearLines();
            v.Say(Lines.LeavesAngry(v.Customer, Rng));
            if (v.Order != null && v.Order.IsOpen)
            {
                CancelOrder(v.Order, "walked out");
                Standing.AddReputation(-10f);
            }
            else
            {
                Standing.AddReputation(-3f);
            }
            Clients.RecordFor(v.Customer.Id, Day).Log.Add($"Day {Day}: left angry");
            LeaveAfterLines(v);
        }

        // ------------------------------------------------------------------ per-frame visit logic

        void TickVisits(float dt)
        {
            bool counterFree = CounterFree();
            _counterFreeFor = counterFree ? _counterFreeFor + dt : 0f;
            if (_counterFreeFor > 1.2f && Phase != DayPhase.Closed)
            {
                Visit next = null;
                foreach (var v in Visits)
                    if (v.Phase == VisitPhase.Queueing && (next == null || v.Id < next.Id)) next = v;
                if (next != null)
                {
                    _counterFreeFor = 0f;
                    WalkTo(next, Spot.Counter, () => ArriveAtCounter(next));
                }
            }

            foreach (var v in Visits.ToArray())
            {
                if (v.Phase == VisitPhase.Gone) continue;

                if (v.Moving)
                {
                    v.MoveT = Math.Min(1f, v.MoveT + dt / Math.Max(0.05f, v.MoveDuration));
                    if (v.MoveT >= 1f)
                    {
                        var next = v.Next;
                        v.Next = null;
                        next?.Invoke();
                    }
                }

                if (v.TickLines(dt) && v.AfterLines != null)
                {
                    var after = v.AfterLines;
                    v.AfterLines = null;
                    after();
                }

                if (v.Subject != null && v != ActivePhotoVisit && v.Phase == VisitPhase.PhotoSitting)
                {
                    v.Subject.Tick(dt);
                    v.Subject.CollectPatienceCost();
                    while (v.Subject.TryTakeLine(out _)) { }
                }

                if (v.Phase == VisitPhase.Inspecting)
                {
                    v.InspectTimer -= dt;
                    if (v.InspectTimer <= 0f && !v.IsTalking) FinishInspection(v);
                }

                DrainPatience(v, dt);
            }
            Visits.RemoveAll(v => v.Phase == VisitPhase.Gone);
        }

        void DrainPatience(Visit v, float dt)
        {
            if (v.Leaving) return;
            float rate = v.Phase switch
            {
                VisitPhase.Talking => 0.25f,
                VisitPhase.Choosing => 0.8f,
                VisitPhase.Queueing => 0.8f,
                VisitPhase.PhotoSitting => v == ActivePhotoVisit ? 0f : 1f,
                VisitPhase.Waiting => Balance.WaitingPatienceDrainPerSecond * 0.5f,
                VisitPhase.AwaitingHandover => 0.9f,
                _ => 0f,
            };
            if (v.Order != null && v.Order.IsOpen && Clock.Now > v.Order.Deadline) rate *= 2f;
            if (v == ActivePhotoVisit && ActivePhoto != null) v.Patience -= ActivePhoto.Subject.CollectPatienceCost();
            v.Patience -= rate * dt;

            if (v.Moving || v.Phase == VisitPhase.Inspecting || v.Phase == VisitPhase.Walking) return;

            float f = v.PatienceFraction;
            if (f <= 0f)
            {
                LeaveAngry(v);
                return;
            }
            if (v.IsTalking) return;
            if (f < 0.25f && v.ComplaintLevel < 2)
            {
                v.ComplaintLevel = 2;
                v.Say(v.Phase == VisitPhase.PhotoSitting ? Lines.PhotoImpatient(v.Customer, 2, Rng) : Lines.WaitingComplaint(v.Customer, 2, Rng));
            }
            else if (f < 0.5f && v.ComplaintLevel < 1)
            {
                v.ComplaintLevel = 1;
                v.Say(v.Phase == VisitPhase.PhotoSitting ? Lines.PhotoImpatient(v.Customer, 1, Rng) : Lines.WaitingComplaint(v.Customer, 1, Rng));
            }
        }

        /// <summary>Player clicked a speech bubble.</summary>
        public void SkipLine(int visitId) => GetVisit(visitId)?.SkipLine();

        // ------------------------------------------------------------------ counter

        void ArriveAtCounter(Visit v)
        {
            if (v.PendingHandover != null)
            {
                var doc = v.PendingHandover;
                v.PendingHandover = null;
                HandOver(v, doc);
                return;
            }

            if (v.Order == null)
            {
                if (v.Negotiation == null) BeginJobConversation(v);
                else ShowNegotiation(v);
                return;
            }

            if (v.Kind == ArrivalKind.Pickup && !v.Greeted)
            {
                // First time at the counter on a pickup trip.
                v.Greeted = true;
                v.Say(Lines.PickupGreeting(v.Customer, Rng));
                Then(v, () => PickupCheck(v));
                return;
            }
            PickupCheck(v);
        }

        void PickupCheck(Visit v)
        {
            var o = v.Order;
            if (o == null || !o.IsOpen)
            {
                Leave(v);
                return;
            }
            switch (o.Status)
            {
                case OrderStatus.AwaitingPhoto:
                    OfferPhoto(v);
                    break;
                case OrderStatus.Ready:
                    v.Phase = VisitPhase.AwaitingHandover;
                    break;
                default:
                    if (v.Kind == ArrivalKind.Pickup)
                    {
                        v.Say(Lines.NotReady(v.Customer, Rng));
                        Then(v, () => ShowChoices(v,
                            new Choice(ChoiceIds.GiveHour, Lines.Player.GiveMeAnHour, FreeBenchSeat(v) >= 0),
                            new Choice(ChoiceIds.ComeTomorrow, Lines.Player.ComeBackTomorrow),
                            new Choice(ChoiceIds.CantDoIt, Lines.Player.CantDoIt)));
                    }
                    else
                    {
                        v.Say(Lines.WaitingComplaint(v.Customer, 1, Rng));
                        Then(v, () => ShowChoices(v, new Choice(ChoiceIds.AlmostDone, Lines.Player.AlmostDone)));
                    }
                    break;
            }
        }

        void BeginJobConversation(Visit v)
        {
            var p = v.Customer;
            var beat = v.Beat;
            var req = v.Request ?? new JobRequest();
            req.CustomerId = p.Id;
            int rep = (int)Standing.Reputation;

            if (beat != null)
            {
                req.CountryId = beat.CountryId;
                req.Offer = beat.Offer;
                req.Reservation = beat.Offer;
                req.DestinationCity = beat.DestinationCity;
                req.WaitsInShop = beat.WaitsInShop;
            }
            else
            {
                var country = PickRequestedCountry();
                req.CountryId = country.Id;
                req.Urgent = Day >= 3 && Standing.Reputation >= 40f && Rng.Chance(0.12f);
                req.WaitsInShop = req.Urgent || Day <= 2 || Rng.Chance(0.55f);
                req.Offer = Pricing.ComputeOffer(country.Spec, p, req.Urgent, rep, Standing.Heat, country.Risk(Day), Rng);
                req.Offer = Pricing.RoundTo((int)(req.Offer * country.Demand(Day)), 25);
                req.Reservation = Pricing.ComputeReservation(req.Offer, p, rep);
                req.DestinationCity = NamePools.DestinationCity(country.Id, Rng);
            }
            req.Deadline = DeadlineFor(req);
            v.Request = req;
            v.Negotiation = new Negotiation(p, req.Offer, req.Reservation);

            var spec = Country(req.CountryId).Spec;
            if (beat != null)
            {
                foreach (var line in beat.Opening)
                {
                    bool byCompanion = line.StartsWith("C:", StringComparison.Ordinal);
                    v.Say(byCompanion ? line.Substring(2) : line, byCompanion);
                }
                v.Say(beat.Request ?? Lines.Request(p, req, spec, Rng));
                v.Say(beat.Motivation);
                v.Say(beat.OfferLine ?? Lines.Offer(p, req.Offer, req.Urgent, Rng));
            }
            else
            {
                bool returning = Clients.TryGetRecord(p.Id, out var rec) && rec.Jobs > 0;
                if (returning) v.Say(rec.Status == ClientStatus.Success ? "Me again. Your last one worked." : "Me again.");
                else if (v.Kind != ArrivalKind.Referral) v.Say(Lines.Greeting(p, Rng));
                v.Say(Lines.Request(p, req, spec, Rng));
                if (Rng.Chance(0.75f)) v.Say(Lines.Motivation(p, req, Rng));
                v.Say(Lines.Offer(p, req.Offer, req.Urgent, Rng));
            }
            Then(v, () => ShowNegotiation(v));
        }

        GameTime DeadlineFor(JobRequest req)
        {
            if (req.Urgent)
            {
                float m = Math.Min(Balance.CloseMinute, Clock.Minute + Rng.Range(120, 200));
                return new GameTime(Day, (float)Math.Round(m / 15f) * 15f);
            }
            if (req.WaitsInShop) return new GameTime(Day, Balance.CloseMinute);
            int days = Rng.Range(1, 3);
            float minute = Balance.OpenMinute + 60 + Rng.Range(0, 12) * 30;
            return new GameTime(Day + days, minute);
        }

        CountryState PickRequestedCountry()
        {
            var options = new List<CountryState>();
            foreach (var c in CountryList)
                if (Standing.Reputation >= c.Spec.UnlockReputation) options.Add(c);
            if (options.Count == 0) return Countries[Sim.Countries.France];
            return Rng.PickWeighted(options, c =>
            {
                if (!c.Owned) return 0.35f;
                return c.AcceptingJobs ? 1f : 0.15f;
            });
        }

        void ShowNegotiation(Visit v)
        {
            var c = Country(v.Request.CountryId);
            var n = v.Negotiation;
            if (!c.Owned)
            {
                ShowChoices(v, new Choice(ChoiceIds.Decline, Lines.Player.DontDo(c.Spec.Name)));
                return;
            }
            if (n.FinalOffer.HasValue)
            {
                int push = Math.Max(n.SmallAsk, n.FinalOffer.Value + 50);
                ShowChoices(v,
                    new Choice(ChoiceIds.Accept, Lines.Player.AcceptFinal(n.FinalOffer.Value)),
                    new Choice(ChoiceIds.Push, Lines.Player.Ask(push)),
                    new Choice(ChoiceIds.Decline, Lines.Player.NoDeal));
                return;
            }
            ShowChoices(v,
                new Choice(ChoiceIds.Accept, Lines.Player.Accept(n.Offer)),
                new Choice(ChoiceIds.AskSmall, Lines.Player.Ask(n.SmallAsk)),
                new Choice(ChoiceIds.AskBig, Lines.Player.AskBig(n.BigAsk)),
                new Choice(ChoiceIds.Decline, c.AcceptingJobs ? Lines.Player.Decline : Lines.Player.NotTaking(c.Spec.Name)));
        }

        /// <summary>Player picked a reply.</summary>
        public void Choose(int visitId, string choiceId)
        {
            var v = GetVisit(visitId);
            if (v == null || v.Phase != VisitPhase.Choosing) return;
            var choice = v.Choices.Find(c => c.Id == choiceId);
            if (choice == null || !choice.Enabled) return;
            v.Choices.Clear();
            var p = v.Customer;
            var n = v.Negotiation;

            switch (choiceId)
            {
                case ChoiceIds.Accept:
                {
                    var reply = n.Accept();
                    v.Say(Lines.Accepted(p, reply.Amount, Rng));
                    AcceptJob(v, reply.Amount);
                    break;
                }
                case ChoiceIds.AskSmall:
                case ChoiceIds.AskBig:
                case ChoiceIds.Push:
                {
                    int amount = choiceId == ChoiceIds.AskSmall ? n.SmallAsk
                        : choiceId == ChoiceIds.AskBig ? n.BigAsk
                        : Math.Max(n.SmallAsk, (n.FinalOffer ?? n.Offer) + 50);
                    var reply = n.Ask(amount, Rng);
                    switch (reply.Result)
                    {
                        case Bargain.Accepted:
                            v.Say(Lines.Accepted(p, reply.Amount, Rng));
                            AcceptJob(v, reply.Amount);
                            break;
                        case Bargain.Countered:
                            v.Say(Lines.Countered(p, reply.Amount, Rng));
                            Then(v, () => ShowNegotiation(v));
                            break;
                        default:
                            v.Say(Lines.WalkedOut(p, Rng));
                            Standing.AddReputation(-1f);
                            LeaveAfterLines(v);
                            break;
                    }
                    break;
                }
                case ChoiceIds.Decline:
                    n?.Decline();
                    v.Say(Lines.Declined(p, Rng));
                    LeaveAfterLines(v);
                    break;

                case ChoiceIds.TakePhoto:
                    if (StoolFree(v)) WalkTo(v, Spot.Stool, () => SitForPhoto(v));
                    else OfferPhoto(v);
                    break;
                case ChoiceIds.WaitBench:
                    WalkTo(v, Spot.Bench, () => v.Phase = VisitPhase.Waiting);
                    break;

                case ChoiceIds.FixIt:
                    if (v.Holding != null)
                    {
                        DeskDocuments.Add(v.Holding);
                        v.Holding = null;
                    }
                    if (v.Order != null) v.Order.Redos++;
                    v.Patience -= v.PatienceMax * 0.15f;
                    v.ComplaintLevel = 0;
                    v.Say(v.Customer.Has(Trait.Aggressive) ? "Hurry up." : "Okay. I'll wait.");
                    Then(v, () => WalkTo(v, Spot.Bench, () => v.Phase = VisitPhase.Waiting));
                    break;
                case ChoiceIds.TrustMe:
                    TryPersuade(v);
                    break;
                case ChoiceIds.GetOut:
                    if (v.Holding != null)
                    {
                        DeskDocuments.Add(v.Holding);
                        v.Holding = null;
                    }
                    if (v.Order != null && v.Order.IsOpen) CancelOrder(v.Order, "refused the document");
                    Standing.AddReputation(-12f);
                    v.Say(Lines.LeavesAngry(p, Rng));
                    LeaveAfterLines(v);
                    break;

                case ChoiceIds.GiveHour:
                    v.Order.Deadline = Clock.Now.AddMinutes(60);
                    Standing.AddReputation(-2f);
                    v.Patience -= v.PatienceMax * 0.1f;
                    v.Say("One hour. Not a minute more.");
                    Then(v, () => WalkTo(v, Spot.Bench, () => v.Phase = VisitPhase.Waiting));
                    break;
                case ChoiceIds.ComeTomorrow:
                    if (v.PatienceFraction < 0.3f || v.Customer.Has(Trait.Impatient))
                    {
                        v.Say("No. Forget it. I'm done with you.");
                        CancelOrder(v.Order, "gave up");
                        Standing.AddReputation(-8f);
                    }
                    else
                    {
                        v.Say("Tomorrow. Last chance.");
                        ConvertToPickup(v.Order, new GameTime(Day + 1, Math.Max(Balance.OpenMinute + 30, Math.Min(Balance.LastArrivalMinute, Clock.Minute))));
                        Standing.AddReputation(-4f);
                    }
                    LeaveAfterLines(v);
                    break;
                case ChoiceIds.CantDoIt:
                    CancelOrder(v.Order, "shop gave up");
                    Standing.AddReputation(-8f);
                    v.Say(Lines.LeavesAngry(p, Rng));
                    LeaveAfterLines(v);
                    break;
                case ChoiceIds.AlmostDone:
                    v.Patience = Math.Min(v.PatienceMax, v.Patience + v.PatienceMax * 0.25f);
                    v.ComplaintLevel = 0;
                    v.Say("Fine.");
                    Then(v, () => WalkTo(v, Spot.Bench, () => v.Phase = VisitPhase.Waiting));
                    break;
            }
        }

        void AcceptJob(Visit v, int price)
        {
            var order = Orders.Create(v.Request, price, Clock.Now);
            v.Order = order;
            _jobsAcceptedToday++;
            var country = Country(order.CountryId);
            Standing.AddHeat(Balance.HeatPerJobBase + Balance.HeatPerJobRiskScale * (country.Risk(Day) * 0.5f + v.Customer.Stats.Risk * 0.5f));
            Clients.RecordFor(v.Customer.Id, Day).Log.Add($"Day {Day}: {country.Spec.Name} passport, {Labels.Money(price)}");
            Emit(SimEventType.OrderCreated, v.Id, order.Id, price);

            v.Say(Lines.Handover(v.Customer, Rng));
            Then(v, () =>
            {
                Emit(SimEventType.FolderPlaced, v.Id, order.Id);
                OfferPhoto(v);
            });
        }

        void OfferPhoto(Visit v)
        {
            bool stool = StoolFree(v);
            bool bench = FreeBenchSeat(v) >= 0;
            ShowChoices(v,
                new Choice(ChoiceIds.TakePhoto, Lines.Player.TakePhoto, stool),
                new Choice(ChoiceIds.WaitBench, Lines.Player.WaitBench, bench));
        }

        /// <summary>
        /// Player clicked a customer in the room: call them to the counter, or to the stool
        /// if they still need their photo.
        /// </summary>
        public void CallVisit(int visitId)
        {
            var v = GetVisit(visitId);
            if (v == null || v.Moving) return;
            if (v.Phase == VisitPhase.Queueing && CounterFree(v))
            {
                WalkTo(v, Spot.Counter, () => ArriveAtCounter(v));
                return;
            }
            if (v.Phase != VisitPhase.Waiting || v.Order == null) return;
            if (v.Order.Status == OrderStatus.AwaitingPhoto && StoolFree(v))
                WalkTo(v, Spot.Stool, () => SitForPhoto(v));
            else if (CounterFree(v))
                WalkTo(v, Spot.Counter, () => ArriveAtCounter(v));
        }

        /// <summary>Send the customer at the counter to the photo stool (player clicked the stool).</summary>
        public bool SendToStool(int visitId)
        {
            var v = GetVisit(visitId);
            if (v == null || v.Moving || v.Order == null || !StoolFree(v)) return false;
            if (v.Phase != VisitPhase.Choosing && v.Phase != VisitPhase.Waiting) return false;
            if (v.Order.Status != OrderStatus.AwaitingPhoto && v.Phase != VisitPhase.Choosing) return false;
            WalkTo(v, Spot.Stool, () => SitForPhoto(v));
            return true;
        }

        void ConvertToPickup(Order o, GameTime at)
        {
            if (o == null) return;
            o.WaitsInShop = false;
            if (at > o.Deadline) o.Deadline = at;
            Director.RemoveFor(o.Id);
            Director.Add(new ScheduledArrival { At = at, Kind = ArrivalKind.Pickup, OrderId = o.Id, CustomerId = o.CustomerId });
        }

        void CancelOrder(Order o, string reason)
        {
            if (o == null || !o.IsOpen) return;
            o.Status = OrderStatus.Cancelled;
            Director.RemoveFor(o.Id);
            Clients.RecordFor(o.CustomerId, Day).Log.Add($"Day {Day}: order #{o.Id} cancelled ({reason})");
            Emit(SimEventType.OrderCancelled, orderId: o.Id, text: reason);
        }
    }
}
