using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum NewsEffect
    {
        None,
        RiskUp,
        RiskDown,
        DemandUp,
        RuleChange,
        TrafficUp,
        TrafficDown,
        PrinterBroken,
        PowerOutage,
        InternetDown,
        EquipmentSale,
    }

    /// <summary>A headline on the computer's NEWS tab. Some change the simulation (design doc §41, §43).</summary>
    public sealed class NewsItem
    {
        public int Id;
        public int Day;
        public string Section = "WORLD";
        public string Headline = "";
        public string Body = "";
        public NewsEffect Effect;
        public string CountryId;
        public int Days;
        public float Magnitude;
    }

    /// <summary>Writes the morning news and decides which events hit the shop.</summary>
    public static class NewsDesk
    {
        static readonly string[] Flavor =
        {
            "RAIN EXPECTED ALL WEEK|Umbrella sales up. Mood down.",
            "LOCAL CAFE CLOSES AFTER 40 YEARS|Regulars say the coffee was never good, but they'll miss it.",
            "MAYOR PROMISES CLEANER STREETS|Again.",
            "STRAY CAT ELECTED 'MASCOT OF THE MONTH'|The cat could not be reached for comment.",
            "TRAM LINE 4 DELAYED INDEFINITELY|Commuters advised to 'just walk'.",
            "NEW PHONE MODEL SELLS OUT|Nobody can explain what's different about it.",
            "POLICE CHIEF: 'CRIME IS DOWN'|Crime unavailable for comment.",
            "FOOTBALL CLUB LOSES AGAIN|Fans remain loyal, tired.",
        };

        public static List<NewsItem> Morning(GameSession s, Rng rng)
        {
            var list = new List<NewsItem>();
            int day = s.Day;
            if (day <= 1) return list;

            float roll = rng.NextFloat();
            if (roll < 0.5f) list.Add(CountryStory(s, rng));
            else if (roll < 0.68f) list.Add(TrafficStory(day, rng));
            else if (roll < 0.85f) list.Add(ShopEvent(s, rng));
            list.RemoveAll(n => n == null);

            var parts = rng.Pick(Flavor).Split('|');
            list.Add(new NewsItem { Day = day, Section = "LOCAL", Headline = parts[0], Body = parts[1] });
            return list;
        }

        static NewsItem CountryStory(GameSession s, Rng rng)
        {
            var candidates = new List<CountryState>();
            foreach (var c in s.CountryList)
                if (c.Owned || s.Standing.Reputation >= c.Spec.UnlockReputation) candidates.Add(c);
            if (candidates.Count == 0) return null;
            var country = rng.Pick(candidates);
            string name = country.Spec.Name.ToUpperInvariant();
            float r = rng.NextFloat();

            if (r < 0.32f)
                return new NewsItem
                {
                    Day = s.Day, Effect = NewsEffect.RiskUp, CountryId = country.Id, Days = rng.Range(4, 8), Magnitude = 0.2f,
                    Headline = $"INCREASED SCREENING FOR {name} TRAVEL",
                    Body = $"Border officials announce 'enhanced document checks' for travellers to {country.Spec.Name}.",
                };
            if (r < 0.5f)
                return new NewsItem
                {
                    Day = s.Day, Effect = NewsEffect.RiskDown, CountryId = country.Id, Days = 3, Magnitude = -0.1f,
                    Headline = $"{name} BORDER STAFF ON STRIKE",
                    Body = "Queues are long and checks, reportedly, are short.",
                };
            if (r < 0.75f)
                return new NewsItem
                {
                    Day = s.Day, Effect = NewsEffect.DemandUp, CountryId = country.Id, Days = 5, Magnitude = 1.4f,
                    Headline = $"{name} APPLICATION BACKLOG",
                    Body = $"Official appointments for {country.Spec.Name} are booked for months. Some people won't wait.",
                };
            if (country.Owned && s.Day - country.LastRuleChangeDay >= 8)
                return new NewsItem
                {
                    Day = s.Day, Effect = NewsEffect.RuleChange, CountryId = country.Id,
                    Headline = $"{name} INTRODUCES NEW TRAVEL REGULATIONS",
                    Body = "Updated document requirements take effect immediately. Handbook update available in the SHOP.",
                };
            return new NewsItem
            {
                Day = s.Day, Effect = NewsEffect.RiskUp, CountryId = country.Id, Days = 3, Magnitude = 0.12f,
                Headline = $"{name} TIGHTENS AIRPORT SECURITY",
                Body = "Longer lines, sharper eyes.",
            };
        }

        static NewsItem TrafficStory(int day, Rng rng)
        {
            int k = rng.Range(0, 3);
            if (k == 0)
                return new NewsItem { Day = day, Section = "LOCAL", Effect = NewsEffect.TrafficUp, Days = 3, Magnitude = 1.4f,
                    Headline = "HOLIDAY TRAVEL SURGE", Body = "Everyone wants to be somewhere else this week." };
            if (k == 1)
                return new NewsItem { Day = day, Section = "LOCAL", Effect = NewsEffect.TrafficUp, Days = 5, Magnitude = 1.4f,
                    Headline = "BUSY WEEK AHEAD", Body = "The rumour mill says your name a lot lately." };
            return new NewsItem { Day = day, Section = "LOCAL", Effect = NewsEffect.TrafficDown, Days = 4, Magnitude = 0.7f,
                Headline = "QUIET WEEK", Body = "Police patrols in the neighbourhood. People are staying home." };
        }

        static NewsItem ShopEvent(GameSession s, Rng rng)
        {
            int k = rng.Range(0, 4);
            switch (k)
            {
                case 0:
                    if (s.Printer.Model.Tier >= 3 || s.PrinterBroken) return null;
                    return new NewsItem { Day = s.Day, Section = "SHOP", Effect = NewsEffect.PrinterBroken,
                        Headline = "PRINTER MAKING GRINDING NOISE", Body = $"It still prints. Slowly. A repair costs {Labels.Money(Balance.PrinterRepairCost)} (SHOP tab)." };
                case 1:
                    return new NewsItem { Day = s.Day, Section = "SHOP", Effect = NewsEffect.PowerOutage, Days = rng.Range(60, 121),
                        Magnitude = rng.Range(10 * 60, 16 * 60),
                        Headline = "SCHEDULED POWER CUT IN YOUR BLOCK", Body = "Computer and printer will be unavailable for a while today." };
                case 2:
                    return new NewsItem { Day = s.Day, Section = "SHOP", Effect = NewsEffect.InternetDown,
                        Headline = "INTERNET OUTAGE IN THE AREA", Body = "Messages will arrive late today." };
                default:
                    return new NewsItem { Day = s.Day, Section = "SHOP", Effect = NewsEffect.EquipmentSale, Days = 3, Magnitude = 0.75f,
                        Headline = "EQUIPMENT SALE AT THE ELECTRONICS MARKET", Body = "25% off upgrades for three days." };
            }
        }

        /// <summary>Changes one rule of a country's spec. Returns a description of what changed.</summary>
        public static string ChangeRule(CountrySpec spec, Rng rng)
        {
            int k = rng.Range(0, 5);
            switch (k)
            {
                case 0:
                {
                    var old = spec.Photo.Backdrop;
                    Backdrop b;
                    do b = (Backdrop)rng.Range(0, 4); while (b == old);
                    spec.Photo.Backdrop = b;
                    return $"Photo background: {Labels.Of(old)} -> {Labels.Of(b)}";
                }
                case 1:
                {
                    float shift = rng.Chance(0.5f) ? 0.05f : -0.05f;
                    float min = Math.Max(0.45f, Math.Min(0.75f, spec.Photo.CoverageMin + shift));
                    spec.Photo.CoverageMin = min;
                    spec.Photo.CoverageMax = min + 0.1f;
                    return $"Head coverage: now {Labels.Percent(min)} - {Labels.Percent(min + 0.1f)}";
                }
                case 2:
                {
                    var old = spec.Passport.Cover;
                    CoverColor c;
                    do c = (CoverColor)rng.Range(0, 5); while (c == old);
                    spec.Passport.Cover = c;
                    return $"Cover colour: {Labels.Of(old)} -> {Labels.Of(c)}";
                }
                case 3:
                {
                    var old = spec.Passport.Category;
                    DocCategory c;
                    do c = (DocCategory)rng.Range(0, 4); while (c == old);
                    spec.Passport.Category = c;
                    return $"Document category: {Labels.Of(old)} -> {Labels.Of(c)}";
                }
                default:
                {
                    int[] options = { 24, 32, 48, 64 };
                    int old = spec.Passport.Pages;
                    int p;
                    do p = options[rng.Range(0, options.Length)]; while (p == old);
                    spec.Passport.Pages = p;
                    return $"Pages: {old} -> {p}";
                }
            }
        }
    }
}
