using System;

namespace LegitEnough.Sim
{
    /// <summary>What a customer asks for when they reach the counter.</summary>
    public sealed class JobRequest
    {
        public int CustomerId;
        public string CountryId = Countries.France;
        public DocType DocType = DocType.Passport;
        public int Offer;
        /// <summary>Hidden: the most this customer will pay.</summary>
        public int Reservation;
        public bool Urgent;
        public GameTime Deadline;
        /// <summary>True: they sit on the bench until it's done. False: they come back at the deadline.</summary>
        public bool WaitsInShop = true;
        public string DestinationCity = "";
    }

    /// <summary>Pricing model (design doc §29, §30, §34, §35).</summary>
    public static class Pricing
    {
        public static int RoundTo(int value, int step) => Math.Max(step, (int)Math.Round(value / (double)step) * step);

        public static int ComputeOffer(CountrySpec country, CustomerProfile customer, bool urgent,
            int reputation, float heat, float countryRisk, Rng rng)
        {
            var s = customer.Stats;
            float wealth = 0.75f + 0.7f * s.Wealth;
            float rep = 1f + reputation / (float)Balance.ReputationMax * 0.6f;
            float risk = 1f + (countryRisk + s.Risk * 0.5f + heat / 100f * 0.3f) * 0.5f;
            float urgency = urgent ? Balance.UrgentMultiplier : 1f;
            float noise = rng.Range(0.92f, 1.08f);
            return RoundTo((int)(country.BasePassportPrice * wealth * rep * risk * urgency * noise), 25);
        }

        public static int ComputeReservation(int offer, CustomerProfile customer, int reputation)
        {
            var s = customer.Stats;
            float stretch = 1.12f + 0.45f * s.Wealth + 0.4f * s.Desperation - 0.15f * (1f - s.Trust);
            stretch *= 1f + reputation / 2000f;
            return Math.Max(offer, RoundTo((int)(offer * stretch), 25));
        }
    }
}
