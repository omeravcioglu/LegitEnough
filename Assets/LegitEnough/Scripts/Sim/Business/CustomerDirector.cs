using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum ArrivalKind { NewJob, Pickup, Referral, Story }

    public sealed class ScheduledArrival
    {
        public GameTime At;
        public ArrivalKind Kind;
        /// <summary>0 = generate a new customer on arrival.</summary>
        public int CustomerId;
        public int OrderId;
        public int ReferredBy;
        public StoryBeat Beat;
    }

    /// <summary>Decides who walks through the door and when (design doc §34, §44).</summary>
    public sealed class CustomerDirector
    {
        readonly List<ScheduledArrival> _arrivals = new List<ScheduledArrival>();

        public IReadOnlyList<ScheduledArrival> Upcoming => _arrivals;

        public void Add(ScheduledArrival a)
        {
            _arrivals.Add(a);
            _arrivals.Sort((x, y) => x.At.CompareTo(y.At));
        }

        public void RemoveFor(int orderId) => _arrivals.RemoveAll(a => a.Kind == ArrivalKind.Pickup && a.OrderId == orderId);

        public bool HasPickupFor(int orderId) => _arrivals.Exists(a => a.Kind == ArrivalKind.Pickup && a.OrderId == orderId);

        /// <summary>Next arrival that is due, or null.</summary>
        public ScheduledArrival PopDue(GameTime now)
        {
            if (_arrivals.Count == 0 || _arrivals[0].At > now) return null;
            var a = _arrivals[0];
            _arrivals.RemoveAt(0);
            return a;
        }

        /// <summary>Pushes an arrival back (shop full, nobody at the counter yet...).</summary>
        public void Postpone(ScheduledArrival a, float minutes)
        {
            a.At = a.At.AddMinutes(minutes);
            Add(a);
        }

        /// <summary>Moves an arrival to a new time (the demo's schedule follows the opening time).</summary>
        public void Reschedule(ScheduledArrival a, GameTime at)
        {
            if (!_arrivals.Remove(a)) return;
            a.At = at;
            Add(a);
        }

        /// <summary>Drops arrivals that can no longer happen today (shop closed).</summary>
        public void DropNewJobsBefore(GameTime cutoff)
        {
            _arrivals.RemoveAll(a => a.At <= cutoff && (a.Kind == ArrivalKind.NewJob || a.Kind == ArrivalKind.Referral));
        }

        /// <summary>Generates today's walk-in customers.</summary>
        public void PlanWalkIns(int day, float expectedPerDay, float trafficMultiplier, Rng rng)
        {
            if (day == 1) return; // Day 1 belongs to the story.
            int count = Poisson(expectedPerDay * trafficMultiplier, rng);
            count = Math.Max(0, Math.Min(9, count));
            if (count == 0) return;

            var times = new List<float>();
            for (int i = 0; i < count; i++) times.Add(rng.Range(Balance.OpenMinute + 15f, Balance.LastArrivalMinute));
            times.Sort();
            float last = -999f;
            foreach (float t0 in times)
            {
                float t = Math.Max(t0, last + 22f);
                if (t > Balance.LastArrivalMinute) break;
                last = t;
                Add(new ScheduledArrival { At = new GameTime(day, t), Kind = ArrivalKind.NewJob });
            }
        }

        static int Poisson(float lambda, Rng rng)
        {
            if (lambda <= 0f) return 0;
            double l = Math.Exp(-lambda);
            double p = 1.0;
            int k = 0;
            do
            {
                k++;
                p *= rng.NextFloat();
            } while (p > l && k < 50);
            return k - 1;
        }
    }
}
