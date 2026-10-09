using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum LedgerKind { Payment, Rent, Utilities, Supplies, Equipment, Handbook, Refund, Other }

    public readonly struct LedgerEntry
    {
        public readonly int Day;
        public readonly float Minute;
        public readonly LedgerKind Kind;
        public readonly int Amount;
        public readonly string Note;

        public LedgerEntry(int day, float minute, LedgerKind kind, int amount, string note)
        {
            Day = day;
            Minute = minute;
            Kind = kind;
            Amount = amount;
            Note = note ?? "";
        }
    }

    /// <summary>Cash on hand and every movement of money (design doc §33).</summary>
    public sealed class Ledger
    {
        readonly List<LedgerEntry> _entries = new List<LedgerEntry>();

        public int Cash { get; private set; }
        public int LifetimeRevenue { get; private set; }
        public IReadOnlyList<LedgerEntry> Entries => _entries;

        public Ledger(int startingCash)
        {
            Cash = startingCash;
        }

        public void Record(GameTime at, LedgerKind kind, int amount, string note)
        {
            if (amount == 0) return;
            Cash += amount;
            if (amount > 0 && kind == LedgerKind.Payment) LifetimeRevenue += amount;
            _entries.Add(new LedgerEntry(at.Day, at.Minute, kind, amount, note));
        }

        public bool CanAfford(int amount) => Cash >= amount;

        public int Revenue(int day)
        {
            int sum = 0;
            foreach (var e in _entries) if (e.Day == day && e.Amount > 0) sum += e.Amount;
            return sum;
        }

        public int Expenses(int day)
        {
            int sum = 0;
            foreach (var e in _entries) if (e.Day == day && e.Amount < 0) sum -= e.Amount;
            return sum;
        }
    }
}
