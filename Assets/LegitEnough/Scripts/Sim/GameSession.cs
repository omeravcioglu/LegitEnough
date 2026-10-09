using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum DayPhase
    {
        /// <summary>Sign says CLOSED; clock runs; no walk-ins yet.</summary>
        Morning,
        Open,
        /// <summary>Past closing time: no new customers, finish what's on the desk.</summary>
        AfterHours,
        /// <summary>Day is over; the report is showing.</summary>
        Closed,
    }

    /// <summary>
    /// The whole simulation: one campaign of LEGIT ENOUGH. Pure C# with no engine
    /// dependency, so it runs (and is tested) outside Unity. The presentation layer calls
    /// <see cref="Tick"/> every frame, reads state for drawing and issues player commands.
    /// </summary>
    public sealed partial class GameSession
    {
        public const int BenchSeats = 3;
        public const int ForcedCloseMinute = 23 * 60;

        public readonly ulong Seed;
        public readonly Rng Rng;
        public readonly Clock Clock = new Clock();
        public readonly Ledger Ledger;
        public readonly Standing Standing = new Standing();
        public readonly Dictionary<string, CountryState> Countries = new Dictionary<string, CountryState>();
        public readonly List<CountryState> CountryList = new List<CountryState>();
        public readonly ClientDatabase Clients;
        public readonly OrderBook Orders = new OrderBook();
        public readonly PhotoLibrary Photos = new PhotoLibrary();
        public readonly Inbox Inbox = new Inbox();
        public readonly CustomerDirector Director = new CustomerDirector();
        public readonly Printer Printer;
        public readonly CameraRig Rig = new CameraRig();
        public readonly Dictionary<EquipmentSlot, EquipmentDef> Equipment = new Dictionary<EquipmentSlot, EquipmentDef>();
        public readonly List<Visit> Visits = new List<Visit>();
        public readonly List<FinishedDocument> DeskDocuments = new List<FinishedDocument>();
        public readonly List<CashPile> CounterCash = new List<CashPile>();
        public readonly List<PendingOutcome> PendingOutcomes = new List<PendingOutcome>();
        public readonly List<DayReport> Reports = new List<DayReport>();

        public DayPhase Phase { get; private set; } = DayPhase.Morning;
        /// <summary>Pause menu / transition cards. Nothing moves while paused.</summary>
        public bool Paused;
        public PhotoSession ActivePhoto { get; private set; }
        public Visit ActivePhotoVisit { get; private set; }
        public DayReport LastReport => Reports.Count > 0 ? Reports[Reports.Count - 1] : null;

        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly List<StoryBeat> _storyQueue = new List<StoryBeat>();
        readonly HashSet<string> _flags = new HashSet<string>();
        int _nextVisitId = 1;
        int _nextPageId = 1;
        int _nextDocId = 1;
        int _nextCashId = 1;
        float _counterFreeFor;

        // Day statistics.
        float _repAtStart;
        float _heatAtStart;
        int _customersToday;
        int _jobsAcceptedToday;
        int _jobsCompletedToday;
        int _messagesToday;

        public GameSession(ulong seed)
        {
            Seed = seed;
            Rng = new Rng(seed, 1);
            Ledger = new Ledger(Balance.StartingCash);
            Clients = new ClientDatabase(seed);
            foreach (var p in StoryCast.CreateProfiles()) Clients.Register(p);

            foreach (var spec in Sim.Countries.CreateAll())
            {
                var state = new CountryState { Spec = spec, HandbookSpec = spec, Owned = spec.OwnedAtStart };
                Countries[spec.Id] = state;
                CountryList.Add(state);
            }

            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                Equipment[slot] = EquipmentCatalog.Starter(slot);
            Printer = new Printer(Equipment[EquipmentSlot.Printer]);

            _storyQueue.Add(StoryCast.SamirArrives());
            Inbox.Schedule(new Message
            {
                ArrivesAt = new GameTime(1, Balance.MorningMinute - 30),
                From = MessageTemplates.WelcomeFrom,
                Subject = MessageTemplates.WelcomeSubject,
                Body = MessageTemplates.WelcomeBody,
                Kind = MessageKind.System,
            });
            StartDay(1);
        }

        // ------------------------------------------------------------------ accessors

        public GameTime Now => Clock.Now;
        public int Day => Clock.Day;
        public EquipmentDef Camera => Equipment[EquipmentSlot.Camera];
        public EquipmentDef Computer => Equipment[EquipmentSlot.Computer];
        public EquipmentDef Station => Equipment[EquipmentSlot.PhotoStation];
        public CountryState Country(string id) => id != null && Countries.TryGetValue(id, out var c) ? c : null;
        public CustomerProfile Customer(int id) => Clients.Get(id);
        public Visit GetVisit(int id) => Visits.Find(v => v.Id == id);
        public bool HasFlag(string flag) => _flags.Contains(flag);

        public List<SimEvent> DrainEvents()
        {
            var list = new List<SimEvent>(_events);
            _events.Clear();
            return list;
        }

        void Emit(SimEventType type, int visitId = 0, int orderId = 0, int amount = 0, string text = "") =>
            _events.Add(new SimEvent(type, visitId, orderId, amount, text));

        // ------------------------------------------------------------------ main loop

        public void Tick(float dt)
        {
            if (Paused || dt <= 0f) return;
            dt = Math.Min(dt, 0.25f);

            if (Phase != DayPhase.Closed)
            {
                Clock.Running = true;
                Clock.Advance(dt);
                if (Phase == DayPhase.Open && Clock.Minute >= Balance.CloseMinute) EnterAfterHours();
                if (Clock.Minute >= ForcedCloseMinute) ForceEndDay();
            }
            else
            {
                Clock.Running = false;
            }

            DeliverMessages();
            ResolveSilentOutcomes();
            if (Phase == DayPhase.Open) ProcessArrivals(dt);
            TickVisits(dt);
            TickPhoto(dt);
            TickPrinter(dt);
        }

        // ------------------------------------------------------------------ day cycle

        void StartDay(int day)
        {
            Clock.Day = day;
            Clock.Minute = Balance.MorningMinute;
            Phase = DayPhase.Morning;
            _repAtStart = Standing.Reputation;
            _heatAtStart = Standing.Heat;
            _customersToday = 0;
            _jobsAcceptedToday = 0;
            _jobsCompletedToday = 0;
            _messagesToday = 0;

            for (int i = _storyQueue.Count - 1; i >= 0; i--)
            {
                var beat = _storyQueue[i];
                if (beat.EarliestDay > day) continue;
                _storyQueue.RemoveAt(i);
                Director.Add(new ScheduledArrival
                {
                    At = new GameTime(day, beat.ArrivalMinute),
                    Kind = ArrivalKind.Story,
                    CustomerId = beat.CustomerId,
                    Beat = beat,
                });
            }
            RunMorningNews();
            Director.PlanWalkIns(day, Standing.ExpectedCustomersPerDay, TrafficMultiplier, Rng);
            DeliverMessages();
            Emit(SimEventType.DayStarted, amount: day);
        }

        /// <summary>Flip the sign to OPEN.</summary>
        public void OpenShop()
        {
            if (Phase != DayPhase.Morning) return;
            Phase = Clock.Minute >= Balance.CloseMinute ? DayPhase.AfterHours : DayPhase.Open;
            Emit(SimEventType.ShopOpened);
        }

        public bool CanCloseShop(out string reason)
        {
            reason = "";
            if (Phase == DayPhase.Closed) { reason = "Already closed."; return false; }
            foreach (var v in Visits)
            {
                if (v.Phase == VisitPhase.Gone) continue;
                reason = "Customers are still inside.";
                return false;
            }
            return true;
        }

        /// <summary>Flip the sign to CLOSED and end the day. Requires an empty shop.</summary>
        public bool CloseShop()
        {
            if (!CanCloseShop(out _)) return false;
            EndDay();
            return true;
        }

        void EnterAfterHours()
        {
            Phase = DayPhase.AfterHours;
            Director.DropNewJobsBefore(new GameTime(Day, 1440f));
            Emit(SimEventType.ClosingTime);
            foreach (var v in Visits.ToArray())
            {
                if (v.Phase == VisitPhase.Queueing)
                {
                    v.Say(Lines.ClosingTime(v.Customer, Rng));
                    LeaveAfterLines(v);
                }
                else if (v.Phase == VisitPhase.Waiting && v.Order != null && v.Order.IsOpen && v.PendingHandover == null)
                {
                    v.Say(Lines.ClosingTime(v.Customer, Rng));
                    ConvertToPickup(v.Order, new GameTime(Day + 1, Balance.OpenMinute + 60 + Rng.Range(0, 5) * 30));
                    LeaveAfterLines(v);
                }
            }
        }

        void ForceEndDay()
        {
            if (ActivePhoto != null) EndPhotoSession();
            foreach (var v in Visits.ToArray())
            {
                if (v.Order != null && v.Order.IsOpen && !Director.HasPickupFor(v.Order.Id))
                    ConvertToPickup(v.Order, new GameTime(Day + 1, Balance.OpenMinute + 60));
                if (v.PendingHandover != null) DeskDocuments.Add(v.PendingHandover);
                if (v.Holding != null) DeskDocuments.Add(v.Holding);
                v.Phase = VisitPhase.Gone;
            }
            Visits.Clear();
            Emit(SimEventType.Toast, text: "You fell asleep at the desk.");
            EndDay();
        }

        void EndDay()
        {
            if (ActivePhoto != null) EndPhotoSession();
            Phase = DayPhase.Closed;
            Clock.Running = false;

            // Cash nobody swept off the counter still counts.
            foreach (var pile in CounterCash.ToArray()) CollectCash(pile.Id);

            var now = Clock.Now;
            Ledger.Record(now, LedgerKind.Rent, -Balance.DailyRent, "Rent");
            Ledger.Record(now, LedgerKind.Utilities, -Balance.DailyUtilities, "Electricity & water");

            float decay = Balance.HeatDailyDecay + (_jobsAcceptedToday == 0 ? Balance.HeatQuietDayBonusDecay : 0f);
            Standing.AddHeat(-decay);

            int openOrders = 0;
            foreach (var o in Orders.Open()) openOrders++;

            var report = new DayReport
            {
                Day = Day,
                Revenue = Ledger.Revenue(Day),
                Expenses = Ledger.Expenses(Day),
                CashEnd = Ledger.Cash,
                Customers = _customersToday,
                JobsAccepted = _jobsAcceptedToday,
                JobsCompleted = _jobsCompletedToday,
                OrdersOpen = openOrders,
                MessagesReceived = _messagesToday,
                ReputationStart = _repAtStart,
                ReputationEnd = Standing.Reputation,
                HeatStart = _heatAtStart,
                HeatEnd = Standing.Heat,
            };
            var byKind = new Dictionary<string, int>();
            foreach (var e in Ledger.Entries)
            {
                if (e.Day != Day) continue;
                string key = e.Kind.ToString();
                byKind.TryGetValue(key, out int sum);
                byKind[key] = sum + e.Amount;
            }
            foreach (var kv in byKind) report.Lines.Add(kv);
            Reports.Add(report);
            Emit(SimEventType.DayEnded, amount: Day);
        }

        /// <summary>After the report: wake up, it's tomorrow.</summary>
        public void StartNextDay()
        {
            if (Phase != DayPhase.Closed) return;
            StartDay(Day + 1);
        }

        // ------------------------------------------------------------------ messages & outcomes

        void DeliverMessages()
        {
            if (InternetDown) return;
            foreach (var m in Inbox.DeliverDue(Clock.Now))
            {
                _messagesToday++;
                if (m.OrderId.HasValue)
                {
                    var po = PendingOutcomes.Find(p => p.OrderId == m.OrderId.Value && !p.Applied);
                    if (po != null) ApplyOutcome(po);
                }
                Emit(SimEventType.MessageArrived, orderId: m.OrderId ?? 0, text: m.From);
            }
        }

        void ResolveSilentOutcomes()
        {
            foreach (var po in PendingOutcomes)
            {
                if (po.Applied || po.Kind != OutcomeKind.Unknown || po.ResolvesAt > Clock.Now) continue;
                ApplyOutcome(po);
            }
        }

        void ApplyOutcome(PendingOutcome po)
        {
            po.Applied = true;
            var order = Orders.Get(po.OrderId);
            if (order != null) order.Outcome = po.Kind;

            float rep = OutcomeModel.ReputationDelta(po.Kind, po.Price);
            if (rep != 0f)
            {
                Standing.AddReputation(rep);
                Emit(SimEventType.ReputationChanged, amount: (int)Math.Round(rep));
            }
            Standing.AddHeat(OutcomeModel.HeatDelta(po.Kind));

            var record = Clients.RecordFor(po.CustomerId, Day);
            record.Status = po.Kind switch
            {
                OutcomeKind.Success => ClientStatus.Success,
                OutcomeKind.Failed => ClientStatus.Failed,
                OutcomeKind.Missing => ClientStatus.Missing,
                _ => record.Status,
            };
            if (po.Kind != OutcomeKind.Unknown) record.Log.Add($"Day {Day}: {po.Kind.ToString().ToUpperInvariant()}");

            var customer = Clients.Get(po.CustomerId);
            if (po.Kind == OutcomeKind.Success)
            {
                if (customer.Id == StoryCast.SamirId && !_flags.Contains("brother_scheduled"))
                {
                    _flags.Add("brother_scheduled");
                    _storyQueue.Add(StoryCast.BrotherArrives(Day + 20));
                }
                if (customer.StoryId == null && Rng.Chance(0.35f))
                {
                    Director.Add(new ScheduledArrival
                    {
                        At = new GameTime(Day + Rng.Range(1, 5), Rng.Range(Balance.OpenMinute + 30, Balance.LastArrivalMinute)),
                        Kind = ArrivalKind.Referral,
                        ReferredBy = customer.Id,
                    });
                }
            }
        }

        // ------------------------------------------------------------------ purchases & settings

        public bool BuyHandbook(string countryId)
        {
            var c = Country(countryId);
            if (c == null || c.Owned) return false;
            if (Standing.Reputation < c.Spec.UnlockReputation) return false;
            if (!Ledger.CanAfford(c.Spec.HandbookPrice)) return false;
            Ledger.Record(Clock.Now, LedgerKind.Handbook, -c.Spec.HandbookPrice, $"Handbook: {c.Spec.Name}");
            c.Owned = true;
            Emit(SimEventType.Purchase, text: c.Spec.Name);
            return true;
        }

        public bool BuyEquipment(string equipmentId)
        {
            var e = EquipmentCatalog.Get(equipmentId);
            if (e == null || Equipment[e.Slot].Tier >= e.Tier) return false;
            int price = PriceOf(e);
            if (!Ledger.CanAfford(price)) return false;
            Ledger.Record(Clock.Now, LedgerKind.Equipment, -price, e.Name);
            Equipment[e.Slot] = e;
            if (e.Slot == EquipmentSlot.Printer)
            {
                Printer.Model = e;
                PrinterBroken = false;
                Printer.Slowdown = 1f;
            }
            if (e.Slot == EquipmentSlot.Camera) Rig.ClampTo(e);
            Emit(SimEventType.Purchase, text: e.Name);
            return true;
        }

        public void SetAcceptingJobs(string countryId, bool accepting)
        {
            var c = Country(countryId);
            if (c != null) c.AcceptingJobs = accepting;
        }
    }
}
