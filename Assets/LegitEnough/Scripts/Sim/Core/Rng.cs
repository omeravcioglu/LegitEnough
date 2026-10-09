using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>
    /// Deterministic PCG32 generator. The same seed produces the same sequence on every
    /// platform, which is what lets customer #142 look and act the same every time.
    /// </summary>
    public sealed class Rng
    {
        ulong _state;
        readonly ulong _inc;

        public Rng(ulong seed, ulong stream = 54)
        {
            _state = 0;
            _inc = (stream << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = old * 6364136223846793005UL + _inc;
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        public bool Chance(float probability) => NextFloat() < probability;

        public T Pick<T>(IReadOnlyList<T> items) => items[Range(0, items.Count)];

        public T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weight)
        {
            float total = 0f;
            for (int i = 0; i < items.Count; i++) total += Math.Max(0f, weight(items[i]));
            if (total <= 0f) return items[Range(0, items.Count)];
            float roll = NextFloat() * total;
            for (int i = 0; i < items.Count; i++)
            {
                roll -= Math.Max(0f, weight(items[i]));
                if (roll < 0f) return items[i];
            }
            return items[items.Count - 1];
        }

        /// <summary>Approximately normal value (Box-Muller).</summary>
        public float Gaussian(float mean, float stdDev)
        {
            float u1 = 1f - NextFloat();
            float u2 = NextFloat();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * (float)z;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary>Independent child generator; does not disturb this generator's sequence.</summary>
        public static Rng For(ulong seed, ulong salt) => new Rng(Hash.Combine(seed, salt), salt & 0xFFFF);
    }

    public static class Hash
    {
        /// <summary>SplitMix64 finalizer.</summary>
        public static ulong Mix(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }

        public static ulong Combine(ulong a, ulong b) => Mix(a ^ Mix(b + 0x632BE59BD9B4E019UL));

        public static ulong Of(string text)
        {
            ulong h = 1469598103934665603UL;
            foreach (char c in text)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return Mix(h);
        }
    }
}
