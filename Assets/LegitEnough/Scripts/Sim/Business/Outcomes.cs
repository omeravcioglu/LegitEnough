using System;

namespace LegitEnough.Sim
{
    /// <summary>A consequence waiting to land: decided at delivery, revealed days later.</summary>
    public sealed class PendingOutcome
    {
        public int OrderId;
        public int CustomerId;
        public OutcomeKind Kind;
        public GameTime ResolvesAt;
        public int Price;
        public string CountryId = "";
        public string City = "";
        public bool Applied;
    }

    /// <summary>How hidden quality, border risk and heat turn into success or failure (design doc §39, §57).</summary>
    public static class OutcomeModel
    {
        public static float SuccessChance(float quality, float countryRisk, float heat, float customerRisk)
        {
            double logistic = 1.0 / (1.0 + Math.Exp(-(quality - Balance.OutcomeMidpoint) / Balance.OutcomeSpread));
            double factor = (1.0 - countryRisk * 0.3) * (1.0 - heat / 100.0 * 0.25) * (1.0 - customerRisk * 0.15);
            return (float)Math.Max(0.0, Math.Min(1.0, logistic * factor));
        }

        public static PendingOutcome Decide(Order order, CustomerProfile customer, float countryRisk, float heat,
            GameTime deliveredAt, string city, Rng rng)
        {
            float p = SuccessChance(order.Quality?.Final ?? 0f, countryRisk, heat, customer.Stats.Risk);
            OutcomeKind kind = rng.Chance(p) ? OutcomeKind.Success : OutcomeKind.Failed;

            // Some people simply never write back.
            if (rng.Chance(Balance.UnknownOutcomeChance)) kind = OutcomeKind.Unknown;
            // High-risk customers occasionally vanish.
            if (kind == OutcomeKind.Failed && rng.Chance(0.12f * customer.Stats.Risk)) kind = OutcomeKind.Missing;

            int travelDays = rng.Range(1, 4);
            int silenceDays = kind == OutcomeKind.Missing ? rng.Range(5, 9) : rng.Range(0, 4);
            int day = deliveredAt.Day + travelDays + silenceDays;
            float minute = rng.Range(8 * 60, 17 * 60 + 30);

            return new PendingOutcome
            {
                OrderId = order.Id,
                CustomerId = customer.Id,
                Kind = kind,
                ResolvesAt = new GameTime(day, minute),
                Price = order.Price,
                CountryId = order.CountryId,
                City = city,
            };
        }

        public static float ReputationDelta(OutcomeKind kind, int price) => kind switch
        {
            OutcomeKind.Success => Math.Min(35f, 10f + price / 60f),
            OutcomeKind.Failed => -Math.Min(45f, 18f + price / 40f),
            OutcomeKind.Missing => -5f,
            _ => 0f,
        };

        public static float HeatDelta(OutcomeKind kind) => kind switch
        {
            OutcomeKind.Failed => Balance.HeatOnFailure,
            OutcomeKind.Missing => 6f,
            _ => 0f,
        };
    }
}
