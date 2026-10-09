using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>A scripted visit from a handcrafted recurring character (design doc §8).</summary>
    public sealed class StoryBeat
    {
        public string StoryId = "";
        public string BeatId = "";
        public int CustomerId;
        /// <summary>Someone who walks in with the customer and does the talking first.</summary>
        public int? CompanionId;
        public string CountryId = Countries.France;
        public int Offer;
        public bool WaitsInShop = true;
        public string DestinationCity = "";
        /// <summary>Lines said before the request. Prefix "C:" for the companion.</summary>
        public string[] Opening = Array.Empty<string>();
        public string Request;
        public string Motivation;
        public string OfferLine;
        public string SuccessMessage;
        public string FailureMessage;
        /// <summary>First day this beat may happen.</summary>
        public int EarliestDay = 1;
        public float ArrivalMinute = 9 * 60 + 12;
    }

    /// <summary>Handcrafted characters. Ids from 10000 up so they never collide with generated ones.</summary>
    public static class StoryCast
    {
        public const int SamirId = 10001;
        public const int YoussefId = 10002;
        public const string Brothers = "brothers";

        public static IEnumerable<CustomerProfile> CreateProfiles()
        {
            var samir = new CustomerProfile
            {
                Id = SamirId,
                Seed = 10001,
                StoryId = Brothers,
                Primary = Trait.Nervous,
                Secondary = Trait.Cooperative,
                Motivation = Motivation.FamilyReunion,
                VoicePitch = 0.9f,
                Stats = new PersonalityStats { Patience = 0.75f, Cooperation = 0.8f, Trust = 0.6f, Wealth = 0.15f, Desperation = 0.8f, Risk = 0.15f },
                Identity = new Identity
                {
                    Surname = "HADDAD", GivenNames = "SAMIR", Sex = Sex.Male, DateOfBirth = new DateTime(1970, 11, 23),
                    PlaceOfBirth = "TUNIS", HeightCm = 174, EyeColor = EyeColor.DarkBrown,
                },
                Look = new Appearance
                {
                    SkinTone = 3, Face = FaceShape.Oval, FaceWidth = 0.76f, Hair = HairStyle.Short, HairColor = 0,
                    FacialHair = FacialHair.Mustache, Eyes = EyeColor.DarkBrown, EyeSize = 1.05f, NoseSize = 1.1f,
                    MouthWidth = 0.95f, BrowThickness = 1.2f, Age = 37, Build = 1f, Top = TopStyle.Jacket,
                    // The red jacket and mustache make him recognisable from the door (design doc §8).
                    TopColor = 13, InnerColor = 4, Glasses = new List<GlassesStyle> { GlassesStyle.Square },
                },
                Slip = new List<SlipLine>
                {
                    new SlipLine("Name", "Samir Haddad"),
                    new SlipLine("Born", "23 November 1970"),
                    new SlipLine("Place of birth", "Tunis"),
                    new SlipLine("Sex", "M"),
                    new SlipLine("Height", "174 cm"),
                },
            };
            samir.Behavior = Personality.DeriveBehavior(samir.Primary, samir.Secondary, samir.Stats);
            yield return samir;

            var youssef = new CustomerProfile
            {
                Id = YoussefId,
                Seed = 10002,
                StoryId = Brothers,
                Primary = Trait.Quiet,
                Secondary = Trait.Suspicious,
                Motivation = Motivation.Employment,
                VoicePitch = 0.82f,
                Stats = new PersonalityStats { Patience = 0.6f, Cooperation = 0.55f, Trust = 0.45f, Wealth = 0.3f, Desperation = 0.6f, Risk = 0.2f },
                Identity = new Identity
                {
                    Surname = "HADDAD", GivenNames = "YOUSSEF", Sex = Sex.Male, DateOfBirth = new DateTime(1978, 4, 17),
                    PlaceOfBirth = "TUNIS", HeightCm = 181, EyeColor = EyeColor.Brown,
                },
                Look = new Appearance
                {
                    SkinTone = 3, Face = FaceShape.Square, FaceWidth = 0.8f, Hair = HairStyle.Curly, HairColor = 0,
                    FacialHair = FacialHair.Beard, Eyes = EyeColor.Brown, EyeSize = 0.95f, NoseSize = 1.15f,
                    MouthWidth = 1f, BrowThickness = 1.35f, Age = 30, Build = 1.1f, Top = TopStyle.Hoodie,
                    TopColor = 1, InnerColor = 0,
                },
                Slip = new List<SlipLine>
                {
                    new SlipLine("Name", "HADDAD, Youssef"),
                    new SlipLine("Born", "1978-04-17"),
                    new SlipLine("Place of birth", "Tunis"),
                    new SlipLine("Sex", "Male"),
                    new SlipLine("Height", "1.81 m"),
                },
            };
            youssef.Behavior = Personality.DeriveBehavior(youssef.Primary, youssef.Secondary, youssef.Stats);
            yield return youssef;
        }

        /// <summary>Day 1: the very first customer.</summary>
        public static StoryBeat SamirArrives() => new StoryBeat
        {
            StoryId = Brothers,
            BeatId = "samir",
            CustomerId = SamirId,
            CountryId = Countries.France,
            Offer = 450,
            DestinationCity = "Lyon",
            Opening = new[] { "Is this... the place? A man at the cafe said you could help." },
            Request = "I need a French passport.",
            Motivation = "My wife and daughter are in Lyon. I haven't seen them in two years.",
            OfferLine = "I have $450. It's everything I have.",
            SuccessMessage = "It worked. I'm with my family. Thank you.",
            FailureMessage = "They stopped me at the airport. I never even saw the plane. Why did you do this to me?",
            EarliestDay = 1,
            ArrivalMinute = 9 * 60 + 10,
        };

        /// <summary>Twenty days after Samir's good news, he comes back with his brother.</summary>
        public static StoryBeat BrotherArrives(int earliestDay) => new StoryBeat
        {
            StoryId = Brothers,
            BeatId = "youssef",
            CustomerId = YoussefId,
            CompanionId = SamirId,
            CountryId = Countries.France,
            Offer = 500,
            DestinationCity = "Lyon",
            Opening = new[] { "C:Remember me? You helped me.", "C:This is my brother, Youssef. He needs your help now." },
            Request = "Same as him. French.",
            Motivation = "There's work in Lyon. Samir found it for me.",
            OfferLine = "$500. We saved it together.",
            SuccessMessage = "We are all in Lyon now. My brother says you are a magician. I say thank you.",
            FailureMessage = "They stopped me at the border. Samir trusted you. I don't know what to tell him.",
            EarliestDay = earliestDay,
            ArrivalMinute = 11 * 60,
        };
    }
}
