using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>
    /// The demo's scripted Day 2: four handcrafted visitors, R.'s notes and the country tuning
    /// that makes five countries reachable in two days. Ids from 10101 up (story cast is 10000+).
    /// </summary>
    public static class DemoCast
    {
        public const int ElifId = 10101;
        public const int KenjiId = 10102;
        public const int ViktorId = 10103;
        public const int AnjaId = 10104;

        public const string Demo = "demo";
        public const string TurkishBeatId = "demo_tr";
        public const string JapanBeatId = "demo_jp";
        public const string UrgentBeatId = "demo_urgent";
        public const string GermanBeatId = "demo_de";

        public const int DayTwo = 2;
        /// <summary>
        /// The demo's visitors keep their spacing but count from when the sign flips: these times
        /// assume it flips at 09:00 (see GameSession.ShiftDemoArrivals).
        /// </summary>
        public const float SamirArrivalMinute = 9 * 60 + 2;
        /// <summary>Samir's message lands just before the Day 2 card.</summary>
        public const float SamirOutcomeMinute = 8 * 60 + 5;
        /// <summary>Samir's verdict in the demo: a passport this good gets him through, anything worse doesn't.</summary>
        public const float SamirPassQuality = 60f;
        /// <summary>R. says to close up after the last visitor, or at this time at the latest (also shifted by a late opening).</summary>
        public const float QuietAfternoonMinute = 15 * 60 + 30;
        /// <summary>Viktor's rush job is due this long after he walks in. His lines say "four hours".</summary>
        public const int UrgentDeadlineMinutes = 240;
        /// <summary>Token in a beat's OfferLine, replaced by the clock time the job is due.</summary>
        public const string DueToken = "{due}";

        public const string DayTwoSubject = "day two";
        public const string DayTwoBody =
            "Word gets around. A Turkish client called. I put the Turkey section in your binder.\n\n" +
            "A German client said she'd come by before lunch. Germany's section is in the SHOP, right here on the computer ($250), if you want the job.\n\n" +
            "And read the news. France changed its rules: the $80 update is in the SHOP too. Gear's half off today. — R.";
        public const string QuietSubject = "quiet";
        public const string QuietBody = "Quiet afternoon. Close up early.";

        /// <summary>The rush client spots the old French background (the Day 2 rule change).</summary>
        public const string UrgentBackdropComplaint = "Wrong background. France wants white now. It was in the paper.";
        /// <summary>Someone waiting for their passport when the sign says closed. In the demo nobody is sent home.</summary>
        public const string AfterHoursWait = "You're closed? I'll wait. I'm not leaving without it.";
        /// <summary>A client at the counter when the player buys the section they asked for.</summary>
        public const string HandbookBoughtLine = "Oh, you've got it now? Good.";
        /// <summary>A client at the counter when the player starts taking their country's jobs again.</summary>
        public const string TakingJobsAgainLine = "So you do take them? Good.";
        /// <summary>FIX IT over a photo problem: they'll sit for a new one.</summary>
        public const string RetakeLine = "Fine. Another photo, then.";

        public static bool IsDemoBeat(StoryBeat beat) => beat != null && beat.StoryId == Demo;

        /// <summary>Samir's demo verdict depends only on how good his passport is.</summary>
        public static OutcomeKind SamirOutcome(float finalQuality) =>
            finalQuality >= SamirPassQuality ? OutcomeKind.Success : OutcomeKind.Failed;

        /// <summary>When Samir's outcome message lands in the demo.</summary>
        public static GameTime SamirOutcomeAt(GameTime deliveredAt) =>
            deliveredAt.Day < DayTwo ? new GameTime(DayTwo, SamirOutcomeMinute) : deliveredAt.AddMinutes(90);

        /// <summary>Demo prices and thresholds: Germany right away, Italy after Samir's good news, the US later.</summary>
        public static void Tune(CountrySpec spec)
        {
            switch (spec.Id)
            {
                case Countries.Germany:
                    spec.UnlockReputation = 0;
                    spec.HandbookPrice = 250;
                    break;
                case Countries.Italy:
                    spec.UnlockReputation = 20;
                    spec.HandbookPrice = 350;
                    break;
                case Countries.UnitedStates:
                    spec.UnlockReputation = 35;
                    spec.HandbookPrice = 450;
                    break;
            }
        }

        // ------------------------------------------------------------------ people

        public static IEnumerable<CustomerProfile> CreateProfiles()
        {
            // Turkish client: glasses and a beanie, both fine for Turkey.
            var elif = new CustomerProfile
            {
                Id = ElifId,
                Seed = ElifId,
                StoryId = Demo,
                Primary = Trait.Cooperative,
                Secondary = Trait.Friendly,
                Motivation = Motivation.NewLife,
                VoicePitch = 1.2f,
                Stats = new PersonalityStats { Patience = 0.7f, Cooperation = 0.8f, Trust = 0.65f, Wealth = 0.3f, Desperation = 0.4f, Risk = 0.1f },
                Identity = new Identity
                {
                    Surname = "KAYA", GivenNames = "ELIF", Sex = Sex.Female, DateOfBirth = new DateTime(1977, 8, 14),
                    PlaceOfBirth = "BERLIN", HeightCm = 166, EyeColor = EyeColor.Hazel,
                },
                Look = new Appearance
                {
                    SkinTone = 2, Face = FaceShape.Heart, FaceWidth = 0.76f, Hair = HairStyle.Long, HairColor = 1,
                    FacialHair = FacialHair.None, Eyes = EyeColor.Hazel, EyeSize = 1.08f, NoseSize = 0.95f,
                    MouthWidth = 0.95f, BrowThickness = 0.95f, Age = 31, Build = 0.95f, Top = TopStyle.Sweater,
                    TopColor = 6, InnerColor = 2, Earrings = true, Hat = HatStyle.Beanie,
                    Glasses = new List<GlassesStyle> { GlassesStyle.Round },
                },
                Slip = new List<SlipLine>
                {
                    new SlipLine("Name", "Kaya, Elif"),
                    new SlipLine("Born", "14.08.1977"),
                    new SlipLine("Place of birth", "Berlin"),
                    new SlipLine("Sex", "F"),
                    new SlipLine("Height", "166 cm"),
                },
            };
            elif.Behavior = Personality.DeriveBehavior(elif.Primary, elif.Secondary, elif.Stats);
            yield return elif;

            // Wants Japan: the full-game teaser.
            var kenji = new CustomerProfile
            {
                Id = KenjiId,
                Seed = KenjiId,
                StoryId = Demo,
                Primary = Trait.Quiet,
                Secondary = Trait.Cooperative,
                Motivation = Motivation.Employment,
                VoicePitch = 0.88f,
                Stats = new PersonalityStats { Patience = 0.8f, Cooperation = 0.85f, Trust = 0.6f, Wealth = 0.5f, Desperation = 0.3f, Risk = 0.1f },
                Identity = new Identity
                {
                    Surname = "MORI", GivenNames = "KENJI", Sex = Sex.Male, DateOfBirth = new DateTime(1971, 5, 12),
                    PlaceOfBirth = "SAO PAULO", HeightCm = 172, EyeColor = EyeColor.DarkBrown,
                },
                Look = new Appearance
                {
                    SkinTone = 1, Face = FaceShape.Long, FaceWidth = 0.74f, Hair = HairStyle.SidePart, HairColor = 0,
                    FacialHair = FacialHair.None, Eyes = EyeColor.DarkBrown, EyeSize = 0.92f, NoseSize = 0.9f,
                    MouthWidth = 0.9f, BrowThickness = 1.1f, Age = 37, Build = 0.95f, Top = TopStyle.Suit,
                    TopColor = 0, InnerColor = 10,
                },
                Slip = new List<SlipLine>
                {
                    new SlipLine("Name", "MORI Kenji"),
                    new SlipLine("Born", "1971.05.12"),
                    new SlipLine("Place of birth", "Sao Paulo"),
                    new SlipLine("Sex", "M"),
                    new SlipLine("Height", "172 cm"),
                },
            };
            kenji.Behavior = Personality.DeriveBehavior(kenji.Primary, kenji.Secondary, kenji.Stats);
            yield return kenji;

            // The rush job: rich, in a hurry, trusts nobody, reads every line.
            var viktor = new CustomerProfile
            {
                Id = ViktorId,
                Seed = ViktorId,
                StoryId = Demo,
                Primary = Trait.Suspicious,
                Secondary = Trait.Wealthy,
                Motivation = Motivation.Business,
                VoicePitch = 0.8f,
                Stats = new PersonalityStats { Patience = 0.55f, Cooperation = 0.6f, Trust = 0.1f, Wealth = 0.9f, Desperation = 0.5f, Risk = 0.3f },
                Identity = new Identity
                {
                    Surname = "HALE", GivenNames = "VIKTOR", Sex = Sex.Male, DateOfBirth = new DateTime(1961, 3, 3),
                    PlaceOfBirth = "ROTTERDAM", HeightCm = 188, EyeColor = EyeColor.Gray,
                },
                Look = new Appearance
                {
                    SkinTone = 0, Face = FaceShape.Square, FaceWidth = 0.8f, Hair = HairStyle.Receding, HairColor = 6,
                    FacialHair = FacialHair.Stubble, Eyes = EyeColor.Gray, EyeSize = 0.9f, NoseSize = 1.15f,
                    MouthWidth = 0.9f, BrowThickness = 1.3f, Age = 47, Build = 1.1f, Top = TopStyle.Coat,
                    TopColor = 0, InnerColor = 12, Scarf = true, ScarfColor = 13,
                },
                Slip = new List<SlipLine>
                {
                    new SlipLine("Name", "Hale, Viktor"),
                    new SlipLine("Born", "March 3, 1961"),
                    new SlipLine("Place of birth", "Rotterdam"),
                    new SlipLine("Sex", "Male"),
                    new SlipLine("Height", "1.88 m"),
                },
            };
            viktor.Behavior = Personality.DeriveBehavior(viktor.Primary, viktor.Secondary, viktor.Stats);
            yield return viktor;

            // German client: Germany needs eye colour, and it isn't on her sheet.
            var anja = new CustomerProfile
            {
                Id = AnjaId,
                Seed = AnjaId,
                StoryId = Demo,
                Primary = Trait.Friendly,
                Motivation = Motivation.Employment,
                VoicePitch = 1.25f,
                Stats = new PersonalityStats { Patience = 0.75f, Cooperation = 0.75f, Trust = 0.7f, Wealth = 0.35f, Desperation = 0.35f, Risk = 0.1f },
                Identity = new Identity
                {
                    Surname = "WEBER", GivenNames = "ANJA", Sex = Sex.Female, DateOfBirth = new DateTime(1981, 1, 21),
                    PlaceOfBirth = "KYIV", HeightCm = 171, EyeColor = EyeColor.Blue,
                },
                Look = new Appearance
                {
                    SkinTone = 0, Face = FaceShape.Oval, FaceWidth = 0.77f, Hair = HairStyle.Ponytail, HairColor = 3,
                    FacialHair = FacialHair.None, Eyes = EyeColor.Blue, EyeSize = 1.1f, NoseSize = 0.9f,
                    MouthWidth = 1.05f, BrowThickness = 0.85f, Freckles = true, Age = 27, Build = 0.95f,
                    Top = TopStyle.Jacket, TopColor = 9, InnerColor = 1,
                },
                Slip = new List<SlipLine>
                {
                    new SlipLine("Name", "Anja Weber"),
                    new SlipLine("Born", "21 January 1981"),
                    new SlipLine("Place of birth", "Kyiv"),
                    new SlipLine("Sex", "Female"),
                    new SlipLine("Height", "171 cm"),
                },
            };
            anja.Behavior = Personality.DeriveBehavior(anja.Primary, anja.Secondary, anja.Stats);
            yield return anja;
        }

        // ------------------------------------------------------------------ Day 2

        /// <summary>The scripted Day 2 visitors, in order of arrival.</summary>
        public static IEnumerable<StoryBeat> DayTwoBeats()
        {
            yield return new StoryBeat
            {
                StoryId = Demo,
                BeatId = TurkishBeatId,
                CustomerId = ElifId,
                CountryId = Countries.Turkey,
                Offer = 380,
                Reservation = 450,
                DestinationCity = "Izmir",
                Opening = new[] { "Hello. I called this morning. About the Turkish thing." },
                Request = "I need a Turkish passport.",
                Motivation = "My grandmother left me her flat in Izmir. And her cat. The cat is not negotiable.",
                OfferLine = "I can pay $380.",
                SuccessMessage = "Izmir. The flat smells of mothballs and the cat ignores me. Thank you.",
                FailureMessage = "Six hours in a small room at the airport. The cat went to the neighbour. Thanks.",
                EarliestDay = DayTwo,
                ArrivalMinute = 9 * 60 + 5,
                PatienceMultiplier = 2f,
            };
            yield return new StoryBeat
            {
                StoryId = Demo,
                BeatId = JapanBeatId,
                CustomerId = KenjiId,
                CountryId = Countries.Japan,
                Offer = 1100,
                DestinationCity = "Osaka",
                Opening = new[] { "Good morning. I hope I'm not early." },
                Request = "A Japanese passport, please.",
                Motivation = "My brother opened a noodle bar in Osaka. He says it needs a second pessimist.",
                OfferLine = "I can pay $1,100.",
                DeclineReply = "I understand. I'll come back when you're bigger.",
                EarliestDay = DayTwo,
                ArrivalMinute = 9 * 60 + 45,
                PatienceMultiplier = 2f,
            };
            yield return new StoryBeat
            {
                StoryId = Demo,
                BeatId = UrgentBeatId,
                CustomerId = ViktorId,
                CountryId = Countries.France,
                Offer = 1200,
                Reservation = 1500,
                Urgent = true,
                DeadlineMinutes = UrgentDeadlineMinutes,
                DestinationCity = "Paris",
                Opening = new[] { "Lock the door. No? Fine. I'll be quick." },
                Request = "French passport. Today.",
                Motivation = "There's a flight to Paris this evening. You have four hours. Not a minute more.",
                OfferLine = "$1,200. Ready by " + DueToken + ", or I walk.",
                DeclineReply = "Then I was never here.",
                SuccessMessage = "Landed. We never met.",
                FailureMessage = "The man at passport control had a magnifying glass. A real one. We will speak again.",
                EarliestDay = DayTwo,
                ArrivalMinute = 10 * 60 + 20,
                PatienceMultiplier = 2f,
            };
            yield return new StoryBeat
            {
                StoryId = Demo,
                BeatId = GermanBeatId,
                CustomerId = AnjaId,
                CountryId = Countries.Germany,
                Offer = 520,
                Reservation = 600,
                DestinationCity = "Hamburg",
                Opening = new[] { "Hi! I said I'd come before lunch. I'm hungry, so let's be quick." },
                Request = "I need a German passport.",
                Motivation = "There's a warehouse job in Hamburg. Night shifts. I'm told it builds character.",
                OfferLine = "I can pay $520.",
                DeclineReply = "Oh. R. said you might not. Never mind.",
                SuccessMessage = "Hamburg is grey and wet and I love it. Thank you.",
                FailureMessage = "They looked at my eyes, then at the passport, then at my eyes again. Then they took me somewhere.",
                EarliestDay = DayTwo,
                ArrivalMinute = 11 * 60 + 30,
                PatienceMultiplier = 2f,
            };
        }
    }
}
