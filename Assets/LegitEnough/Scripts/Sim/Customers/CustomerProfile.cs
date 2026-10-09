using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>The identity a customer wants printed. Ground truth for the information score.</summary>
    public sealed class Identity
    {
        public string Surname = "";
        public string GivenNames = "";
        public Sex Sex;
        public DateTime DateOfBirth;
        public string PlaceOfBirth = "";
        public int HeightCm;
        public EyeColor EyeColor;

        public string FullName => $"{GivenNames} {Surname}";
        public string FirstName => GivenNames.Split(' ')[0];

        public int AgeOn(DateTime date)
        {
            int age = date.Year - DateOfBirth.Year;
            if (date < DateOfBirth.AddYears(age)) age--;
            return age;
        }
    }

    /// <summary>One handwritten line on the client sheet.</summary>
    public readonly struct SlipLine
    {
        public readonly string Label;
        public readonly string Value;

        public SlipLine(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }

    /// <summary>
    /// A persistent customer. Generated customers are fully determined by their seed;
    /// story customers are handcrafted. Either way the same Id always means the same person.
    /// </summary>
    public sealed class CustomerProfile
    {
        public int Id;
        public ulong Seed;
        public Identity Identity = new Identity();
        public Appearance Look = new Appearance();
        public Trait Primary;
        public Trait? Secondary;
        public PersonalityStats Stats = new PersonalityStats();
        public PoseBehavior Behavior = new PoseBehavior();
        public Motivation Motivation;
        /// <summary>1.0 = average voice; feeds the speech blips.</summary>
        public float VoicePitch = 1f;
        /// <summary>Non-null for handcrafted recurring characters.</summary>
        public string StoryId;
        /// <summary>How this person writes their details down (the client sheet).</summary>
        public List<SlipLine> Slip = new List<SlipLine>();

        public string FirstName => Identity.FirstName;
        public string DisplayName => Identity.FullName;

        public bool Has(Trait t) => Primary == t || Secondary == t;

        public string TraitText =>
            Secondary.HasValue ? $"{Primary}, {Secondary.Value}" : Primary.ToString();
    }
}
