using System;

namespace LegitEnough.Sim
{
    public enum RepTier { Unknown, WordSpreading, Established, Professional, HighlyRespected, Legendary }

    public enum HeatLevel { LowProfile, Attention, Watched, HighRisk, Extreme }

    /// <summary>Reputation (design doc §34) and Heat (§35) — how known the shop is, and how watched.</summary>
    public sealed class Standing
    {
        public float Reputation { get; private set; }
        public float Heat { get; private set; }

        public void AddReputation(float delta) =>
            Reputation = Math.Max(0f, Math.Min(Balance.ReputationMax, Reputation + delta));

        public void AddHeat(float delta) => Heat = Math.Max(0f, Math.Min(100f, Heat + delta));

        public RepTier Tier =>
            Reputation >= 1000f ? RepTier.Legendary :
            Reputation >= 750f ? RepTier.HighlyRespected :
            Reputation >= 500f ? RepTier.Professional :
            Reputation >= 250f ? RepTier.Established :
            Reputation >= 100f ? RepTier.WordSpreading : RepTier.Unknown;

        public HeatLevel HeatLevel =>
            Heat >= 80f ? HeatLevel.Extreme :
            Heat >= 60f ? HeatLevel.HighRisk :
            Heat >= 40f ? HeatLevel.Watched :
            Heat >= 20f ? HeatLevel.Attention : HeatLevel.LowProfile;

        public static string Name(RepTier t) => t switch
        {
            RepTier.Unknown => "Unknown",
            RepTier.WordSpreading => "Word spreading",
            RepTier.Established => "Established",
            RepTier.Professional => "Professional",
            RepTier.HighlyRespected => "Highly respected",
            RepTier.Legendary => "Legendary underground specialist",
            _ => t.ToString(),
        };

        public static string Name(HeatLevel h) => h switch
        {
            HeatLevel.LowProfile => "LOW PROFILE",
            HeatLevel.Attention => "ATTENTION",
            HeatLevel.Watched => "WATCHED",
            HeatLevel.HighRisk => "HIGH RISK",
            HeatLevel.Extreme => "EXTREME RISK",
            _ => h.ToString(),
        };

        /// <summary>Average new-job customers per day.</summary>
        public float ExpectedCustomersPerDay
        {
            get
            {
                float rep = Tier switch
                {
                    RepTier.Unknown => 1.6f,
                    RepTier.WordSpreading => 2.6f,
                    RepTier.Established => 3.6f,
                    RepTier.Professional => 4.6f,
                    RepTier.HighlyRespected => 5.6f,
                    _ => 6.5f,
                };
                rep += Reputation / 250f * 0.4f;
                float heat = HeatLevel switch
                {
                    HeatLevel.Watched => 0.8f,
                    HeatLevel.HighRisk => 0.65f,
                    HeatLevel.Extreme => 0.5f,
                    _ => 1f,
                };
                return rep * heat;
            }
        }
    }

    public enum RiskLevel { Low, Moderate, High }

    /// <summary>Per-campaign state for a country: ownership, today's risk, job toggle (design doc §36-§37).</summary>
    public sealed class CountryState
    {
        /// <summary>The rules as they are today. The evaluator always uses these.</summary>
        public CountrySpec Spec;
        /// <summary>The rules as printed in the player's handbook. Falls behind after a rule change.</summary>
        public CountrySpec HandbookSpec;
        public bool Owned;
        public bool AcceptingJobs = true;
        public float RiskModifier;
        public int RiskModifierUntilDay;
        public float PriceMultiplier = 1f;
        public int PriceMultiplierUntilDay;
        public int JobsDone;
        public int LastRuleChangeDay = -100;
        public string LastRuleChange = "";

        public string Id => Spec.Id;

        public float Demand(int day) => day <= PriceMultiplierUntilDay ? PriceMultiplier : 1f;

        public float Risk(int day)
        {
            float mod = day <= RiskModifierUntilDay ? RiskModifier : 0f;
            return Math.Max(0f, Math.Min(1f, Spec.BaseRisk + mod));
        }

        public RiskLevel RiskLevel(int day)
        {
            float r = Risk(day);
            return r >= 0.45f ? Sim.RiskLevel.High : r >= 0.25f ? Sim.RiskLevel.Moderate : Sim.RiskLevel.Low;
        }

        public bool HandbookOutdated => HandbookSpec != null && HandbookSpec != Spec;
    }
}
