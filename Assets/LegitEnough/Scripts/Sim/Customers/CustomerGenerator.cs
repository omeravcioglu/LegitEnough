using System;
using System.Collections.Generic;
using System.Globalization;

namespace LegitEnough.Sim
{
    /// <summary>
    /// Builds customers from (world seed, customer id). Deterministic: regenerating id 142
    /// gives the same face, name, personality and history hooks every time.
    /// </summary>
    public static class CustomerGenerator
    {
        static readonly Trait[] TraitPool = (Trait[])Enum.GetValues(typeof(Trait));

        static float TraitWeight(Trait t) => t switch
        {
            Trait.Friendly => 10f,
            Trait.Nervous => 12f,
            Trait.Aggressive => 5f,
            Trait.Quiet => 9f,
            Trait.Arrogant => 5f,
            Trait.Funny => 7f,
            Trait.Impatient => 9f,
            Trait.Cooperative => 9f,
            Trait.Suspicious => 7f,
            Trait.Exhausted => 8f,
            Trait.Drunk => 3f,
            Trait.Scared => 6f,
            Trait.Wealthy => 5f,
            Trait.Desperate => 8f,
            _ => 5f,
        };

        static bool Clashes(Trait a, Trait b) =>
            a == b
            || Pair(a, b, Trait.Friendly, Trait.Aggressive)
            || Pair(a, b, Trait.Cooperative, Trait.Aggressive)
            || Pair(a, b, Trait.Quiet, Trait.Funny)
            || Pair(a, b, Trait.Wealthy, Trait.Desperate)
            || Pair(a, b, Trait.Cooperative, Trait.Suspicious);

        static bool Pair(Trait a, Trait b, Trait x, Trait y) => (a == x && b == y) || (a == y && b == x);

        public static CustomerProfile Generate(int id, ulong worldSeed)
        {
            ulong seed = Hash.Combine(worldSeed, (ulong)id);
            var rng = new Rng(seed, 7);
            var p = new CustomerProfile { Id = id, Seed = seed };

            p.Primary = rng.PickWeighted(TraitPool, TraitWeight);
            if (rng.Chance(0.4f))
            {
                for (int i = 0; i < 6; i++)
                {
                    Trait s = rng.PickWeighted(TraitPool, TraitWeight);
                    if (!Clashes(p.Primary, s)) { p.Secondary = s; break; }
                }
            }

            p.Motivation = PickMotivation(p.Primary, rng);
            p.Stats = Personality.DeriveStats(p.Primary, p.Secondary, p.Motivation, rng);
            p.Behavior = Personality.DeriveBehavior(p.Primary, p.Secondary, p.Stats);

            Sex sex = rng.Chance(0.5f) ? Sex.Male : Sex.Female;
            int age = Math.Max(19, Math.Min(68, (int)rng.Gaussian(36f, 11f)));
            p.Look = GenerateLook(sex, age, p.Primary, p.Stats, rng);
            p.Identity = GenerateIdentity(sex, age, p.Look, rng);
            p.VoicePitch = (sex == Sex.Male ? rng.Range(0.78f, 1.0f) : rng.Range(1.1f, 1.35f)) * (age > 55 ? 0.94f : 1f);
            p.Slip = WriteSlip(p.Identity, rng);
            return p;
        }

        static Motivation PickMotivation(Trait primary, Rng rng)
        {
            var weights = new Dictionary<Motivation, float>();
            foreach (Motivation m in Enum.GetValues(typeof(Motivation))) weights[m] = 1f;
            weights[Motivation.FamilyReunion] = 2.5f;
            weights[Motivation.Employment] = 2.5f;
            weights[Motivation.NewLife] = 1.8f;
            switch (primary)
            {
                case Trait.Desperate: weights[Motivation.FamilyReunion] += 3f; weights[Motivation.PersonalSafety] += 2f; weights[Motivation.EscapingDebt] += 2f; break;
                case Trait.Wealthy: weights[Motivation.Business] += 4f; weights[Motivation.Mysterious] += 2f; break;
                case Trait.Aggressive: weights[Motivation.Criminal] += 3f; weights[Motivation.RefusesToExplain] += 3f; break;
                case Trait.Scared: weights[Motivation.PersonalSafety] += 3f; weights[Motivation.RunningAway] += 2f; weights[Motivation.Political] += 1.5f; break;
                case Trait.Suspicious: weights[Motivation.RefusesToExplain] += 3f; weights[Motivation.Mysterious] += 2f; break;
                case Trait.Funny: weights[Motivation.Relationship] += 2f; weights[Motivation.NewLife] += 1.5f; break;
                case Trait.Exhausted: weights[Motivation.Employment] += 2f; weights[Motivation.EscapingDebt] += 1.5f; break;
                case Trait.Quiet: weights[Motivation.Mysterious] += 1.5f; weights[Motivation.RefusesToExplain] += 1.5f; break;
            }
            var list = new List<Motivation>(weights.Keys);
            return rng.PickWeighted(list, m => weights[m]);
        }

        static Appearance GenerateLook(Sex sex, int age, Trait primary, PersonalityStats stats, Rng rng)
        {
            var a = new Appearance
            {
                Age = age,
                SkinTone = rng.Range(0, AppearancePalette.SkinTones),
                Face = (FaceShape)rng.Range(0, 5),
                FaceWidth = rng.Range(0.72f, 0.84f),
                EyeSize = rng.Range(0.88f, 1.15f),
                NoseSize = rng.Range(0.8f, 1.25f),
                MouthWidth = rng.Range(0.82f, 1.18f),
                BrowThickness = rng.Range(0.7f, 1.4f) * (sex == Sex.Male ? 1.15f : 0.9f),
                Build = rng.Range(0.88f, 1.18f) * (sex == Sex.Male ? 1.05f : 0.95f),
            };

            bool dark = a.SkinTone >= 4;
            a.Eyes = dark
                ? (rng.Chance(0.85f) ? (rng.Chance(0.5f) ? EyeColor.DarkBrown : EyeColor.Brown) : EyeColor.Hazel)
                : (EyeColor)rng.Range(0, 6);
            a.Freckles = a.SkinTone <= 1 && rng.Chance(0.25f);

            // Hair
            if (sex == Sex.Male)
            {
                var styles = new[] { HairStyle.Short, HairStyle.Short, HairStyle.SidePart, HairStyle.Buzz, HairStyle.Curly, HairStyle.Afro, HairStyle.Long, HairStyle.Bald, HairStyle.Receding };
                a.Hair = rng.Pick(styles);
                if (age > 45 && rng.Chance(0.35f)) a.Hair = rng.Chance(0.5f) ? HairStyle.Receding : HairStyle.Bald;
                float fh = rng.NextFloat();
                a.FacialHair = fh < 0.45f ? FacialHair.None : fh < 0.65f ? FacialHair.Stubble : fh < 0.75f ? FacialHair.Mustache : fh < 0.85f ? FacialHair.Goatee : FacialHair.Beard;
            }
            else
            {
                var styles = new[] { HairStyle.Long, HairStyle.Long, HairStyle.Ponytail, HairStyle.Bun, HairStyle.Short, HairStyle.Curly, HairStyle.Afro, HairStyle.SidePart };
                a.Hair = rng.Pick(styles);
                a.FacialHair = FacialHair.None;
            }
            a.HairColor = dark ? rng.Range(0, 3) : rng.Range(0, 6);
            if (age >= 55) a.HairColor = rng.Chance(0.6f) ? 6 : 7;
            else if (age >= 45 && rng.Chance(0.3f)) a.HairColor = 6;

            // Accessories
            float g = rng.NextFloat();
            if (g < 0.08f)
            {
                if (rng.Chance(0.35f)) a.Glasses.Add(RandomRegularGlasses(rng));
                a.Glasses.Add(GlassesStyle.Sunglasses);
            }
            else if (g < 0.08f + 0.28f + (age > 50 ? 0.2f : 0f))
            {
                a.Glasses.Add(RandomRegularGlasses(rng));
            }
            if (rng.Chance(primary == Trait.Exhausted ? 0.35f : 0.17f))
                a.Hat = (HatStyle)rng.Range(1, 4);
            a.Scarf = rng.Chance(0.15f);
            a.ScarfColor = rng.Range(0, AppearancePalette.ClothColors);
            a.Earrings = rng.Chance(sex == Sex.Female ? 0.4f : 0.08f);

            // Clothes
            if (primary == Trait.Wealthy || stats.Wealth > 0.8f)
                a.Top = rng.Chance(0.6f) ? TopStyle.Suit : TopStyle.Coat;
            else if (primary == Trait.Exhausted)
                a.Top = rng.Chance(0.6f) ? TopStyle.Hoodie : TopStyle.Sweater;
            else
                a.Top = (TopStyle)rng.Range(0, 6);
            a.TopColor = rng.Range(0, AppearancePalette.ClothColors);
            a.InnerColor = rng.Range(0, AppearancePalette.ClothColors);
            return a;
        }

        static GlassesStyle RandomRegularGlasses(Rng rng) => (GlassesStyle)rng.Range(1, 4);

        static Identity GenerateIdentity(Sex sex, int age, Appearance look, Rng rng)
        {
            var id = new Identity
            {
                Sex = sex,
                Surname = rng.Pick(NamePools.Surnames).ToUpperInvariant(),
                PlaceOfBirth = rng.Pick(NamePools.BirthCities),
                EyeColor = look.Eyes,
            };
            string first = rng.Pick(sex == Sex.Male ? NamePools.MaleGiven : NamePools.FemaleGiven);
            if (rng.Chance(0.2f))
            {
                string second = rng.Pick(sex == Sex.Male ? NamePools.MaleGiven : NamePools.FemaleGiven);
                if (second != first) first = first + " " + second;
            }
            id.GivenNames = first.ToUpperInvariant();

            DateTime reference = Calendar.FirstDay;
            id.DateOfBirth = reference.AddYears(-age).AddDays(-rng.Range(1, 364));
            float mean = sex == Sex.Male ? 177f : 164f;
            id.HeightCm = (int)Math.Round(Math.Max(148f, Math.Min(201f, rng.Gaussian(mean, 7f))));
            return id;
        }

        static readonly string[] MonthNames =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December",
        };

        /// <summary>How the customer writes their own details. Formats vary on purpose.</summary>
        static List<SlipLine> WriteSlip(Identity id, Rng rng)
        {
            var lines = new List<SlipLine>();
            string given = TitleCase(id.GivenNames);
            string sur = TitleCase(id.Surname);
            int nameStyle = rng.Range(0, 3);
            string name = nameStyle switch
            {
                0 => $"{given} {sur}",
                1 => $"{id.Surname}, {given}",
                _ => $"{given} {id.Surname}",
            };
            lines.Add(new SlipLine("Name", name));

            DateTime d = id.DateOfBirth;
            int dateStyle = rng.Range(0, 4);
            string born = dateStyle switch
            {
                0 => $"{d.Day} {MonthNames[d.Month - 1]} {d.Year}",
                1 => $"{MonthNames[d.Month - 1]} {d.Day}, {d.Year}",
                2 => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                // Numeric day-first only when unambiguous (day > 12).
                _ => d.Day > 12
                    ? $"{d.Day:00}/{d.Month:00}/{d.Year}"
                    : $"{d.Day} {MonthNames[d.Month - 1].Substring(0, 3)}. {d.Year}",
            };
            lines.Add(new SlipLine("Born", born));
            lines.Add(new SlipLine("Place of birth", TitleCase(id.PlaceOfBirth)));
            lines.Add(new SlipLine("Sex", rng.Chance(0.5f) ? (id.Sex == Sex.Male ? "M" : "F") : (id.Sex == Sex.Male ? "Male" : "Female")));
            string height = rng.Chance(0.7f)
                ? $"{id.HeightCm} cm"
                : (id.HeightCm / 100f).ToString("0.00", CultureInfo.InvariantCulture) + " m";
            lines.Add(new SlipLine("Height", height));
            return lines;
        }

        public static string TitleCase(string upper)
        {
            if (string.IsNullOrEmpty(upper)) return upper;
            var chars = upper.ToLowerInvariant().ToCharArray();
            bool start = true;
            for (int i = 0; i < chars.Length; i++)
            {
                if (start && char.IsLetter(chars[i])) { chars[i] = char.ToUpperInvariant(chars[i]); start = false; }
                else if (chars[i] == ' ' || chars[i] == '-') start = true;
            }
            return new string(chars);
        }
    }
}
