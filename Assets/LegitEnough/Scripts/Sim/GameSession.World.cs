using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>The world outside the shop: news, rule changes, outages, sales (design doc §41, §43).</summary>
    public sealed partial class GameSession
    {
        public readonly List<NewsItem> News = new List<NewsItem>();
        int _nextNewsId = 1;
        float _trafficMultiplier = 1f;
        int _trafficUntilDay;
        float _outageStart = -1f;
        float _outageEnd = -1f;
        int _outageDay;
        int _internetDownDay;
        float _saleMultiplier = 1f;
        int _saleUntilDay;

        public bool PrinterBroken { get; private set; }

        public bool PowerOn => !(Clock.Day == _outageDay && Clock.Minute >= _outageStart && Clock.Minute < _outageEnd);
        public bool InternetDown => Clock.Day == _internetDownDay;
        public float TrafficMultiplier => Day <= _trafficUntilDay ? _trafficMultiplier : 1f;
        public bool SaleActive => Day <= _saleUntilDay;

        /// <summary>Today's price for a piece of equipment (sales apply).</summary>
        public int PriceOf(EquipmentDef e) => SaleActive ? Pricing.RoundTo((int)(e.Price * _saleMultiplier), 25) : e.Price;

        public List<NewsItem> NewsFor(int day)
        {
            var list = new List<NewsItem>();
            foreach (var n in News) if (n.Day == day) list.Add(n);
            return list;
        }

        /// <summary>Publish a news item now and apply its effect (scripted events, tests, debug).</summary>
        public void InjectNews(NewsItem n)
        {
            n.Id = _nextNewsId++;
            if (n.Day == 0) n.Day = Day;
            ApplyNews(n);
            News.Add(n);
        }

        void RunMorningNews()
        {
            foreach (var n in NewsDesk.Morning(this, Rng))
            {
                n.Id = _nextNewsId++;
                ApplyNews(n);
                News.Add(n);
            }
        }

        void ApplyNews(NewsItem n)
        {
            var c = Country(n.CountryId);
            switch (n.Effect)
            {
                case NewsEffect.RiskUp:
                case NewsEffect.RiskDown:
                    c.RiskModifier = n.Magnitude;
                    c.RiskModifierUntilDay = Day + n.Days - 1;
                    break;
                case NewsEffect.DemandUp:
                    c.PriceMultiplier = n.Magnitude;
                    c.PriceMultiplierUntilDay = Day + n.Days - 1;
                    break;
                case NewsEffect.RuleChange:
                {
                    var fresh = c.Spec.Clone();
                    c.LastRuleChange = NewsDesk.ChangeRule(fresh, Rng);
                    if (c.HandbookSpec == null) c.HandbookSpec = c.Spec;
                    c.Spec = fresh;
                    c.LastRuleChangeDay = Day;
                    break;
                }
                case NewsEffect.TrafficUp:
                case NewsEffect.TrafficDown:
                    _trafficMultiplier = n.Magnitude;
                    _trafficUntilDay = Day + n.Days - 1;
                    break;
                case NewsEffect.PrinterBroken:
                    PrinterBroken = true;
                    Printer.Slowdown = Balance.BrokenPrinterSlowdown;
                    break;
                case NewsEffect.PowerOutage:
                    _outageDay = Day;
                    _outageStart = n.Magnitude;
                    _outageEnd = n.Magnitude + n.Days;
                    break;
                case NewsEffect.InternetDown:
                    _internetDownDay = Day;
                    break;
                case NewsEffect.EquipmentSale:
                    _saleMultiplier = n.Magnitude;
                    _saleUntilDay = Day + n.Days - 1;
                    break;
            }
        }

        /// <summary>Bring the handbook up to date with the current rules.</summary>
        public bool BuyHandbookUpdate(string countryId)
        {
            var c = Country(countryId);
            if (c == null || !c.Owned || !c.HandbookOutdated) return false;
            if (!Ledger.CanAfford(Balance.HandbookUpdateCost)) return false;
            Ledger.Record(Clock.Now, LedgerKind.Handbook, -Balance.HandbookUpdateCost, $"Handbook update: {c.Spec.Name}");
            c.HandbookSpec = c.Spec;
            Emit(SimEventType.Purchase, text: $"{c.Spec.Name} update");
            return true;
        }

        public bool RepairPrinter()
        {
            if (!PrinterBroken || !Ledger.CanAfford(Balance.PrinterRepairCost)) return false;
            Ledger.Record(Clock.Now, LedgerKind.Equipment, -Balance.PrinterRepairCost, "Printer repair");
            PrinterBroken = false;
            Printer.Slowdown = 1f;
            Emit(SimEventType.Purchase, text: "Printer repair");
            return true;
        }

        /// <summary>The rules the player can read in their binder for this country.</summary>
        public CountrySpec HandbookRules(string countryId)
        {
            var c = Country(countryId);
            return c?.HandbookSpec ?? c?.Spec;
        }
    }
}
