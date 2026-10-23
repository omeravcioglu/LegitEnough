using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum AchievementSignalKind { Unlock, Add, Max }

    /// <summary>What the tracker saw: unlock an achievement, or add to / raise a progress stat.</summary>
    public readonly struct AchievementSignal
    {
        public readonly AchievementSignalKind Kind;
        /// <summary>Achievement API name (Unlock) or stat API name (Add / Max).</summary>
        public readonly string Id;
        public readonly int Value;

        AchievementSignal(AchievementSignalKind kind, string id, int value)
        {
            Kind = kind;
            Id = id;
            Value = value;
        }

        public static AchievementSignal Unlock(string id) => new AchievementSignal(AchievementSignalKind.Unlock, id, 0);
        public static AchievementSignal Add(string stat, int d) => new AchievementSignal(AchievementSignalKind.Add, stat, d);
        public static AchievementSignal Max(string stat, int v) => new AchievementSignal(AchievementSignalKind.Max, stat, v);

        /// <summary>The stat API name for Add / Max, null for Unlock.</summary>
        public string Stat => Kind == AchievementSignalKind.Unlock ? null : Id;

        public override string ToString() =>
            Kind == AchievementSignalKind.Unlock ? "Unlock " + Id : $"{Kind} {Id} {Value}";
    }

    /// <summary>
    /// Per-session observer that turns what happens in a <see cref="GameSession"/> into achievement
    /// signals. Strictly read-only: it never draws dice, never creates client records or profiles,
    /// never queues lines, events or messages, so a session plays out the same with or without it.
    /// It reports every achievement in both editions; what the demo may unlock is the Book's call.
    /// Wired by three hooks in GameSession: the property, the event emitter and the end of Tick.
    /// </summary>
    public sealed class AchievementTracker
    {
        readonly GameSession _s;
        readonly List<AchievementSignal> _signals = new List<AchievementSignal>();
        readonly HashSet<string> _fired = new HashSet<string>();

        // READ_THE_PAPER: last rules seen per country, and the latest change (day + issues it affects).
        readonly Dictionary<string, CountrySpec> _spec = new Dictionary<string, CountrySpec>();
        readonly Dictionary<string, (int Day, Issue[] Issues)> _changed = new Dictionary<string, (int Day, Issue[] Issues)>();

        bool _wasJammed;
        /// <summary>JUST_IN_TIME: visit id -> the country whose section was bought while they waited.</summary>
        readonly Dictionary<int, string> _justInTime = new Dictionary<int, string>();
        /// <summary>WORD_OF_MOUTH: customers who came in on a referral.</summary>
        readonly HashSet<int> _referred = new HashSet<int>();
        /// <summary>LYING_LOW: heat reached HIGH RISK this run.</summary>
        bool _hot;
        /// <summary>Order id -> the deadline agreed when the job was taken (the Sim moves Order.Deadline later).</summary>
        readonly Dictionary<int, GameTime> _due = new Dictionary<int, GameTime>();
        /// <summary>Orders whose booklet was bound by that agreed deadline (a pickup collected after it isn't late).</summary>
        readonly HashSet<int> _boundInTime = new HashSet<int>();

        // Per day, reset on DayStarted.
        int _pagesToday, _jamsToday, _deliveredToday;
        bool _dirtyToday;

        // Last stat values sent ("Max" signals only go out when a value goes up).
        int _repBest, _cashBest, _sections, _tier3, _days;

        /// <summary>False: keeps watching, but sends nothing.</summary>
        public bool Enabled = true;

        public AchievementTracker(GameSession s)
        {
            _s = s ?? throw new ArgumentNullException(nameof(s));
            foreach (var c in s.CountryList) _spec[c.Id] = c.Spec;
            _wasJammed = s.Printer.Jammed;
            s.Achievements = this;
        }

        public GameSession Session => _s;

        /// <summary>Achievements reported this session (each at most once).</summary>
        public IReadOnlyCollection<string> Fired => _fired;

        public List<AchievementSignal> Drain()
        {
            var list = new List<AchievementSignal>(_signals);
            _signals.Clear();
            return list;
        }

        // ------------------------------------------------------------------ events

        /// <summary>Called by GameSession for every event it emits, right after it is queued.</summary>
        internal void OnEvent(in SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.DayStarted:
                    _pagesToday = _deliveredToday = 0;
                    // A jam left overnight still has to be cleared today.
                    _jamsToday = _s.Printer.Jammed ? 1 : 0;
                    _dirtyToday = false;
                    CheckRuleChanges();
                    break;

                case SimEventType.PhotoKept:
                {
                    Fire(AchievementCatalog.FirstPhoto);
                    var p = _s.Photos.Get(e.Amount);
                    if (p != null && p.Glasses.Count > 0 && p.Glasses[p.Glasses.Count - 1] == GlassesStyle.Sunglasses)
                        Fire(AchievementCatalog.Incognito);
                    break;
                }

                case SimEventType.OrderCreated:
                    OnOrderCreated(e);
                    break;

                case SimEventType.OrderCancelled:
                    _dirtyToday = true;
                    _due.Remove(e.OrderId);
                    _boundInTime.Remove(e.OrderId);
                    break;

                case SimEventType.PagePrinted:
                    _pagesToday++;
                    break;

                case SimEventType.PrinterJammed:
                    _jamsToday++;
                    break;

                case SimEventType.DocumentBound:
                    if (_due.TryGetValue(e.OrderId, out var due) && _s.Clock.Now <= due) _boundInTime.Add(e.OrderId);
                    break;

                case SimEventType.DocumentHandedOver:
                {
                    var v = _s.GetVisit(e.VisitId);
                    if (v?.Holding != null && v.Order != null && v.Holding.OrderId != v.Order.Id)
                        Fire(AchievementCatalog.MixUp);
                    break;
                }

                case SimEventType.CashPlaced:
                    OnCashPlaced(e);
                    break;

                case SimEventType.VisitArrived:
                {
                    var v = _s.GetVisit(e.VisitId);
                    if (v != null && v.Kind == ArrivalKind.Referral && v.Customer != null) _referred.Add(v.Customer.Id);
                    break;
                }

                case SimEventType.MessageArrived:
                    OnMessage(e);
                    break;

                case SimEventType.Purchase:
                    OnPurchase(e);
                    break;

                case SimEventType.DayEnded:
                    OnDayEnded(e);
                    break;
            }
        }

        void OnOrderCreated(in SimEvent e)
        {
            var v = _s.GetVisit(e.VisitId);
            var o = _s.Orders.Get(e.OrderId);
            if (v == null || o == null) return;
            _due[o.Id] = o.Deadline;
            if (v.Negotiation != null && e.Amount > v.Negotiation.Offer) Fire(AchievementCatalog.Haggler);
            if (_justInTime.TryGetValue(v.Id, out var country) && o.CountryId == country) Fire(AchievementCatalog.JustInTime);
            _justInTime.Remove(v.Id);
        }

        void OnCashPlaced(in SimEvent e)
        {
            var o = _s.Orders.Get(e.OrderId);
            var v = _s.GetVisit(e.VisitId);
            Fire(AchievementCatalog.FirstJob);
            Add(AchievementCatalog.StatJobsDone, 1);
            if (o == null) return;

            var q = o.Quality;
            bool known = _due.TryGetValue(o.Id, out var due);
            if (!known) due = o.Deadline;
            bool delivered = o.DeliveredAt.HasValue && o.DeliveredAt.Value <= due;
            // A pickup comes back at (or after) the deadline and pays a few minutes later: it was on
            // time if the booklet was bound by then.
            bool onTime = delivered || (v != null && v.Kind == ArrivalKind.Pickup && _boundInTime.Contains(o.Id));
            bool complaint = v != null && v.HasComplaint;

            if (q != null && q.Final >= 90f) Fire(AchievementCatalog.Quality90);
            if (q != null && q.Findings.Count == 0) Fire(AchievementCatalog.Flawless);
            if (o.Urgent && known && delivered) Fire(AchievementCatalog.RushJob);
            if (complaint && e.Amount == o.Price) Fire(AchievementCatalog.SmoothTalker);
            if (!_s.PowerOn) Fire(AchievementCatalog.Candlelight);
            if (_referred.Contains(o.CustomerId)) Fire(AchievementCatalog.WordOfMouth);
            if (q != null && FollowsTodaysRuleChange(o, q)) Fire(AchievementCatalog.ReadThePaper);

            _deliveredToday++;
            if (o.Redos > 0 || e.Amount < o.Price || !onTime || complaint) _dirtyToday = true;
            _due.Remove(o.Id);
            _boundInTime.Remove(o.Id);
        }

        void OnMessage(in SimEvent e)
        {
            int orderId = e.OrderId;
            if (orderId == 0) return;
            var po = _s.PendingOutcomes.Find(p => p.OrderId == orderId);
            if (po == null || !po.Applied) return;
            if (po.CustomerId == StoryCast.SamirId)
            {
                if (po.Kind == OutcomeKind.Success) Fire(AchievementCatalog.SamirMadeIt);
                else if (po.Kind == OutcomeKind.Failed || po.Kind == OutcomeKind.Missing) Fire(AchievementCatalog.SamirCaught);
            }
            else if (po.CustomerId == StoryCast.YoussefId && po.Kind == OutcomeKind.Success)
            {
                Fire(AchievementCatalog.YoussefMadeIt);
            }
        }

        void OnPurchase(in SimEvent e)
        {
            // Equipment, updates and repairs never carry a bare country name.
            string name = e.Text;
            var c = _s.CountryList.Find(x => x.Spec.Name == name);
            if (c == null) return;
            foreach (var v in _s.Visits)
            {
                if (v.Phase == VisitPhase.Gone || v.Leaving || v.Order != null) continue;
                if (v.Request?.CountryId == c.Id) _justInTime[v.Id] = c.Id;
            }
        }

        void OnDayEnded(in SimEvent e)
        {
            if (_s.Clock.Minute >= GameSession.ForcedCloseMinute) Fire(AchievementCatalog.NightShift);
            if (_pagesToday >= 6 && _jamsToday == 0) Fire(AchievementCatalog.CleanPrintDay);
            if (_deliveredToday >= 4 && !_dirtyToday) Fire(AchievementCatalog.CleanSheet);
            var r = _s.LastReport;
            if (r != null && r.Customers >= 5 && r.JobsAccepted == 0) Fire(AchievementCatalog.NotToday);
            if (e.Amount > _days && Max(AchievementCatalog.StatDaysBest, e.Amount)) _days = e.Amount;
        }

        // ------------------------------------------------------------------ polling

        /// <summary>Checks the session's public state. Cheap and idempotent: call it as often as you like.</summary>
        public void Poll()
        {
            var s = _s;

            bool jammed = s.Printer.Jammed;
            if (_wasJammed && !jammed) Fire(AchievementCatalog.PercussiveMaintenance);
            _wasJammed = jammed;

            foreach (var v in s.Visits)
                if (v.Retakes >= 3) Fire(AchievementCatalog.SayCheese);

            var subject = s.ActivePhoto?.Subject;
            if (subject != null && subject.RemovedGlasses.Count > 0 && subject.Glasses.Count > 0)
                Fire(AchievementCatalog.BackupPair);

            int upgraded = 0, tier3 = 0;
            foreach (var kv in s.Equipment)
            {
                if (kv.Value == null) continue;
                if (kv.Value.Tier >= 2) upgraded++;
                if (kv.Value.Tier >= 3) tier3++;
            }
            if (upgraded > 0) Fire(AchievementCatalog.FirstUpgrade);
            if (tier3 > 0) Fire(AchievementCatalog.TopShelf);
            if (tier3 > _tier3 && Max(AchievementCatalog.StatTier3Best, tier3)) _tier3 = tier3;

            int rep = (int)Math.Floor(s.Standing.Reputation);
            if (rep > _repBest && Max(AchievementCatalog.StatRepBest, rep)) _repBest = rep;

            int cash = s.Ledger.Cash;
            if (cash > _cashBest && Max(AchievementCatalog.StatCashBest, cash)) _cashBest = cash;

            int owned = 0;
            foreach (var c in s.CountryList)
                if (c.Owned) owned++;
            if (owned > _sections && Max(AchievementCatalog.StatSectionsBest, owned)) _sections = owned;

            float heat = s.Standing.Heat;
            if (heat >= 60f) _hot = true;
            else if (_hot && heat < 20f) Fire(AchievementCatalog.LyingLow);

            CheckRuleChanges();
        }

        // ------------------------------------------------------------------ rule changes

        /// <summary>A rule change swaps in a fresh spec object: note the day and what it affects.</summary>
        void CheckRuleChanges()
        {
            foreach (var c in _s.CountryList)
            {
                if (_spec.TryGetValue(c.Id, out var old) && ReferenceEquals(old, c.Spec)) continue;
                _spec[c.Id] = c.Spec;
                if (old == null) continue;
                var issues = Diff(old, c.Spec);
                if (issues.Count == 0) continue;
                // The day the paper ran it (a change can be noticed a little later, e.g. across a day end).
                int day = c.LastRuleChangeDay;
                // Two changes on one day: the job has to follow both.
                if (_changed.TryGetValue(c.Id, out var prev) && prev.Day == day)
                    foreach (var i in prev.Issues)
                        if (!issues.Contains(i)) issues.Add(i);
                _changed[c.Id] = (day, issues.ToArray());
            }
        }

        static List<Issue> Diff(CountrySpec a, CountrySpec b)
        {
            var list = new List<Issue>();
            if (a.Photo.Backdrop != b.Photo.Backdrop) list.Add(Issue.WrongBackdrop);
            if (a.Photo.CoverageMin != b.Photo.CoverageMin || a.Photo.CoverageMax != b.Photo.CoverageMax)
            {
                list.Add(Issue.HeadTooSmall);
                list.Add(Issue.HeadTooLarge);
            }
            if (a.Passport.Cover != b.Passport.Cover) list.Add(Issue.WrongCover);
            if (a.Passport.Category != b.Passport.Category) list.Add(Issue.WrongCategory);
            if (a.Passport.Pages != b.Passport.Pages) list.Add(Issue.WrongPages);
            return list;
        }

        bool FollowsTodaysRuleChange(Order o, QualityReport q)
        {
            if (o.CountryId == null || !_changed.TryGetValue(o.CountryId, out var ch) || ch.Day != _s.Day) return false;
            if (ch.Issues.Length == 0 || q.Has(Issue.WrongCountry) || q.Has(Issue.WrongDocType)) return false;
            foreach (var i in ch.Issues)
            {
                if (q.Has(i)) return false;
                // A photo rule is only followed by a real photo of the customer.
                if (IsPhotoIssue(i) && (q.Has(Issue.NoPhoto) || q.Has(Issue.WrongPerson))) return false;
            }
            return true;
        }

        static bool IsPhotoIssue(Issue i) =>
            i == Issue.WrongBackdrop || i == Issue.HeadTooSmall || i == Issue.HeadTooLarge;

        // ------------------------------------------------------------------ signals

        void Fire(string id)
        {
            if (!Enabled || _fired.Contains(id)) return;
            _fired.Add(id);
            _signals.Add(AchievementSignal.Unlock(id));
        }

        void Add(string stat, int delta)
        {
            if (Enabled) _signals.Add(AchievementSignal.Add(stat, delta));
        }

        /// <summary>Sends a Max signal; false while disabled (so the caller keeps its last-sent value).</summary>
        bool Max(string stat, int value)
        {
            if (!Enabled) return false;
            _signals.Add(AchievementSignal.Max(stat, value));
            return true;
        }
    }
}
