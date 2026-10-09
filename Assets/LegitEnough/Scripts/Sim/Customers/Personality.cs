using System;

namespace LegitEnough.Sim
{
    /// <summary>Hidden statistics (design doc §9). All values are 0..1.</summary>
    public sealed class PersonalityStats
    {
        public float Patience;
        public float Cooperation;
        public float Trust;
        public float Wealth;
        public float Desperation;
        public float Risk;
    }

    public enum DriftKind
    {
        None,
        /// <summary>Keeps looking toward the door (nervous, suspicious).</summary>
        GlanceAtDoor,
        /// <summary>Slowly sways off-pose (drunk).</summary>
        Wobble,
        /// <summary>Head slowly sinks (exhausted).</summary>
        Sag,
        /// <summary>Small random adjustments (impatient).</summary>
        Fidget,
        /// <summary>Avoids the lens, looks down (scared).</summary>
        LookDown,
        /// <summary>Chin creeps upward (arrogant).</summary>
        ChinUp,
    }

    /// <summary>How a customer behaves in front of the camera. Derived from personality.</summary>
    public sealed class PoseBehavior
    {
        public float BlinkInterval = 3.6f;
        public float BlinkDuration = 0.16f;
        /// <summary>Resting eyelid closure, 0 = wide open.</summary>
        public float LidDroop;
        public DriftKind Drift;
        public float DriftIntervalMin = 4f;
        public float DriftIntervalMax = 8f;
        public Expression Resting = Expression.Neutral;
        /// <summary>Goes back to smiling some seconds after being told to stop.</summary>
        public bool SmileRelapse;
        public float RelapseMin = 6f;
        public float RelapseMax = 12f;
        /// <summary>Chance a spoken command is obeyed on the first try.</summary>
        public float Compliance = 0.85f;
        /// <summary>When corrected, sometimes swings past the target (drunk).</summary>
        public bool Overshoots;
    }

    public static class Personality
    {
        public static PersonalityStats DeriveStats(Trait primary, Trait? secondary, Motivation motivation, Rng rng)
        {
            var s = new PersonalityStats
            {
                Patience = rng.Range(0.3f, 0.75f),
                Cooperation = rng.Range(0.45f, 0.8f),
                Trust = rng.Range(0.35f, 0.7f),
                Wealth = rng.Range(0.15f, 0.55f),
                Desperation = rng.Range(0.15f, 0.55f),
                Risk = rng.Range(0.05f, 0.35f),
            };
            Apply(s, primary, 1f);
            if (secondary.HasValue) Apply(s, secondary.Value, 0.5f);
            ApplyMotivation(s, motivation);
            Clamp(s);
            return s;
        }

        static void Apply(PersonalityStats s, Trait t, float w)
        {
            switch (t)
            {
                case Trait.Friendly: s.Cooperation += 0.15f * w; s.Trust += 0.2f * w; s.Patience += 0.1f * w; break;
                case Trait.Nervous: s.Patience -= 0.1f * w; s.Trust -= 0.1f * w; s.Risk += 0.1f * w; break;
                case Trait.Aggressive: s.Cooperation -= 0.3f * w; s.Patience -= 0.2f * w; break;
                case Trait.Quiet: s.Patience += 0.1f * w; break;
                case Trait.Arrogant: s.Cooperation -= 0.15f * w; s.Patience -= 0.15f * w; s.Wealth += 0.15f * w; break;
                case Trait.Funny: s.Patience += 0.1f * w; s.Trust += 0.1f * w; break;
                case Trait.Impatient: s.Patience -= 0.35f * w; break;
                case Trait.Cooperative: s.Cooperation += 0.3f * w; s.Patience += 0.15f * w; break;
                case Trait.Suspicious: s.Trust -= 0.3f * w; s.Cooperation -= 0.1f * w; break;
                case Trait.Exhausted: s.Wealth -= 0.1f * w; s.Desperation += 0.1f * w; break;
                case Trait.Drunk: s.Cooperation -= 0.25f * w; s.Patience -= 0.1f * w; s.Risk += 0.1f * w; break;
                case Trait.Scared: s.Desperation += 0.3f * w; s.Trust -= 0.15f * w; s.Cooperation += 0.1f * w; break;
                case Trait.Wealthy: s.Wealth += 0.5f * w; s.Desperation -= 0.15f * w; break;
                case Trait.Desperate: s.Desperation += 0.5f * w; s.Patience += 0.2f * w; s.Wealth -= 0.15f * w; break;
            }
        }

        static void ApplyMotivation(PersonalityStats s, Motivation m)
        {
            switch (m)
            {
                case Motivation.FamilyReunion: s.Desperation += 0.15f; break;
                case Motivation.PersonalSafety: s.Desperation += 0.25f; s.Risk += 0.1f; break;
                case Motivation.EscapingDebt: s.Desperation += 0.2f; s.Wealth -= 0.1f; s.Risk += 0.1f; break;
                case Motivation.Criminal: s.Risk += 0.4f; s.Wealth += 0.15f; break;
                case Motivation.Political: s.Risk += 0.3f; break;
                case Motivation.RunningAway: s.Risk += 0.25f; s.Desperation += 0.1f; break;
                case Motivation.Mysterious: s.Risk += 0.2f; s.Wealth += 0.1f; break;
                case Motivation.Business: s.Wealth += 0.2f; break;
            }
        }

        static void Clamp(PersonalityStats s)
        {
            s.Patience = Clamp01(s.Patience);
            s.Cooperation = Clamp01(s.Cooperation);
            s.Trust = Clamp01(s.Trust);
            s.Wealth = Clamp01(s.Wealth);
            s.Desperation = Clamp01(s.Desperation);
            s.Risk = Clamp01(s.Risk);
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        public static Expression RestingExpression(Trait primary) => primary switch
        {
            Trait.Friendly => Expression.Smile,
            Trait.Funny => Expression.Grin,
            Trait.Nervous => Expression.Nervous,
            Trait.Scared => Expression.Nervous,
            Trait.Exhausted => Expression.Tired,
            Trait.Aggressive => Expression.Angry,
            Trait.Drunk => Expression.Smile,
            _ => Expression.Neutral,
        };

        public static PoseBehavior DeriveBehavior(Trait primary, Trait? secondary, PersonalityStats stats)
        {
            var b = new PoseBehavior
            {
                Resting = RestingExpression(primary),
                Compliance = 0.45f + 0.5f * stats.Cooperation,
            };
            ApplyBehavior(b, primary);
            if (secondary.HasValue && b.Drift == DriftKind.None) ApplyBehavior(b, secondary.Value);
            return b;
        }

        static void ApplyBehavior(PoseBehavior b, Trait t)
        {
            switch (t)
            {
                case Trait.Nervous:
                    b.Drift = DriftKind.GlanceAtDoor; b.BlinkInterval = 1.7f; b.DriftIntervalMin = 3f; b.DriftIntervalMax = 6f;
                    break;
                case Trait.Suspicious:
                    b.Drift = DriftKind.GlanceAtDoor; b.DriftIntervalMin = 5f; b.DriftIntervalMax = 10f;
                    break;
                case Trait.Drunk:
                    b.Drift = DriftKind.Wobble; b.BlinkInterval = 2.6f; b.BlinkDuration = 0.45f; b.LidDroop = 0.25f;
                    b.DriftIntervalMin = 2.5f; b.DriftIntervalMax = 5f; b.Overshoots = true;
                    break;
                case Trait.Exhausted:
                    b.Drift = DriftKind.Sag; b.BlinkInterval = 2.2f; b.BlinkDuration = 0.35f; b.LidDroop = 0.3f;
                    b.DriftIntervalMin = 5f; b.DriftIntervalMax = 9f;
                    break;
                case Trait.Impatient:
                    b.Drift = DriftKind.Fidget; b.DriftIntervalMin = 3f; b.DriftIntervalMax = 6f;
                    break;
                case Trait.Scared:
                    b.Drift = DriftKind.LookDown; b.BlinkInterval = 2f; b.DriftIntervalMin = 4f; b.DriftIntervalMax = 8f;
                    break;
                case Trait.Arrogant:
                    b.Drift = DriftKind.ChinUp; b.DriftIntervalMin = 6f; b.DriftIntervalMax = 11f;
                    break;
                case Trait.Friendly:
                    b.SmileRelapse = true; b.RelapseMin = 6f; b.RelapseMax = 12f;
                    break;
                case Trait.Funny:
                    b.SmileRelapse = true; b.RelapseMin = 4f; b.RelapseMax = 9f;
                    break;
                case Trait.Quiet:
                    b.BlinkInterval = 4.5f;
                    break;
                case Trait.Cooperative:
                    b.Compliance = Math.Max(b.Compliance, 0.95f);
                    break;
            }
        }
    }
}
