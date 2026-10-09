using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>
    /// Country content. All requirements are invented game rules (see design doc §12):
    /// real names and geography, fictional document specifications.
    /// Each call returns fresh instances so a campaign can mutate its own copy (news events
    /// change rules mid-game).
    /// </summary>
    public static class Countries
    {
        public const string France = "FR";
        public const string Turkey = "TR";
        public const string Germany = "DE";
        public const string Italy = "IT";
        public const string UnitedKingdom = "UK";
        public const string UnitedStates = "US";
        public const string Japan = "JP";
        public const string SouthKorea = "KR";
        public const string Emirates = "AE";
        public const string Iran = "IR";

        static DocField[] Basic() =>
            new[] { DocField.Surname, DocField.GivenNames, DocField.Sex, DocField.DateOfBirth, DocField.PlaceOfBirth };

        public static List<CountrySpec> CreateAll() => new List<CountrySpec>
        {
            new CountrySpec
            {
                Id = France, Name = "France", Adjective = "French", DocumentHeader = "REPUBLIQUE FRANCAISE",
                Tier = 1, UnlockReputation = 0, HandbookPrice = 0, OwnedAtStart = true,
                BasePassportPrice = 450, BaseRisk = 0.15f,
                Photo = new PhotoRules
                {
                    Backdrop = Backdrop.LightGray, GlassesAllowed = false, HeadwearAllowed = false,
                    CoverageMin = 0.60f, CoverageMax = 0.70f,
                },
                Passport = new PassportSpec
                {
                    Category = DocCategory.TypeC, Cover = CoverColor.DarkRed, Pages = 32, ValidityYears = 10,
                    DateFormat = DateFormat.DmyDots,
                    PersonalFields = new[]
                    {
                        DocField.Surname, DocField.GivenNames, DocField.Sex, DocField.DateOfBirth,
                        DocField.PlaceOfBirth, DocField.Height,
                    },
                },
                Notes = new[]
                {
                    "Gray. Not white. GRAY.",
                    "Height in centimetres. Write what they tell you, not what you see.",
                    "Expiry = same day and month, ten years later.",
                },
            },
            new CountrySpec
            {
                Id = Turkey, Name = "Turkey", Adjective = "Turkish", DocumentHeader = "TURKIYE CUMHURIYETI",
                Tier = 1, UnlockReputation = 15, HandbookPrice = 250,
                BasePassportPrice = 380, BaseRisk = 0.10f,
                Photo = new PhotoRules
                {
                    Backdrop = Backdrop.White, GlassesAllowed = true, HeadwearAllowed = true,
                    CoverageMin = 0.70f, CoverageMax = 0.80f,
                },
                Passport = new PassportSpec
                {
                    Category = DocCategory.TypeA, Cover = CoverColor.Navy, Pages = 24, ValidityYears = 10,
                    DateFormat = DateFormat.DmySlashes, PersonalFields = Basic(),
                },
                Notes = new[]
                {
                    "Glasses are fine. Sunglasses are NOT. Obviously.",
                    "Slashes in the dates, not dots.",
                    "Big head. 70 to 80. Zoom in.",
                },
            },
            new CountrySpec
            {
                Id = Germany, Name = "Germany", Adjective = "German", DocumentHeader = "BUNDESREPUBLIK DEUTSCHLAND",
                Tier = 1, UnlockReputation = 35, HandbookPrice = 400,
                BasePassportPrice = 520, BaseRisk = 0.20f,
                Photo = new PhotoRules
                {
                    Backdrop = Backdrop.Blue, GlassesAllowed = false, HeadwearAllowed = false,
                    CoverageMin = 0.65f, CoverageMax = 0.75f,
                },
                Passport = new PassportSpec
                {
                    Category = DocCategory.TypeB, Cover = CoverColor.Black, Pages = 48, ValidityYears = 10,
                    DateFormat = DateFormat.YmdDashes,
                    PersonalFields = new[]
                    {
                        DocField.Surname, DocField.GivenNames, DocField.Sex, DocField.DateOfBirth,
                        DocField.PlaceOfBirth, DocField.EyeColor,
                    },
                },
                Notes = new[]
                {
                    "EYE COLOUR is not on the client sheet. Look at them.",
                    "Year first. Dashes.",
                    "Blue backdrop. I know. Don't ask.",
                },
            },
            new CountrySpec
            {
                Id = Italy, Name = "Italy", Adjective = "Italian", DocumentHeader = "REPUBBLICA ITALIANA",
                Tier = 2, UnlockReputation = 150, HandbookPrice = 900,
                BasePassportPrice = 650, BaseRisk = 0.25f,
                Photo = new PhotoRules
                {
                    Backdrop = Backdrop.White, GlassesAllowed = true, HeadwearAllowed = false,
                    CoverageMin = 0.60f, CoverageMax = 0.75f,
                },
                Passport = new PassportSpec
                {
                    Category = DocCategory.TypeD, Cover = CoverColor.Green, Pages = 32, ValidityYears = 10,
                    DateFormat = DateFormat.DmyDots,
                    PersonalFields = new[]
                    {
                        DocField.Surname, DocField.GivenNames, DocField.Sex, DocField.DateOfBirth,
                        DocField.PlaceOfBirth, DocField.Height, DocField.EyeColor,
                    },
                },
                Notes = new[] { "Height AND eye colour. They want everything." },
            },
            new CountrySpec
            {
                Id = UnitedKingdom, Name = "United Kingdom", Adjective = "British", DocumentHeader = "UNITED KINGDOM",
                Tier = 2, UnlockReputation = 200, HandbookPrice = 1100,
                BasePassportPrice = 750, BaseRisk = 0.35f,
                Photo = new PhotoRules
                {
                    Backdrop = Backdrop.LightGray, GlassesAllowed = false, HeadwearAllowed = false,
                    CoverageMin = 0.65f, CoverageMax = 0.75f,
                },
                Passport = new PassportSpec
                {
                    Category = DocCategory.TypeA, Cover = CoverColor.Navy, Pages = 48, ValidityYears = 10,
                    DateFormat = DateFormat.DmySlashes, PersonalFields = Basic(),
                },
                Notes = new[] { "Same gray as France. Different everything else." },
            },
            new CountrySpec
            {
                Id = UnitedStates, Name = "United States", Adjective = "American", DocumentHeader = "UNITED STATES",
                Tier = 2, UnlockReputation = 260, HandbookPrice = 1400,
                BasePassportPrice = 950, BaseRisk = 0.45f,
                Photo = new PhotoRules
                {
                    Backdrop = Backdrop.White, SmileAllowed = true, GlassesAllowed = false, HeadwearAllowed = false,
                    CoverageMin = 0.50f, CoverageMax = 0.69f,
                },
                Passport = new PassportSpec
                {
                    Category = DocCategory.TypeB, Cover = CoverColor.Navy, Pages = 32, ValidityYears = 10,
                    DateFormat = DateFormat.MdySlashes, PersonalFields = Basic(),
                },
                Notes = new[] { "MONTH FIRST. Every single time someone gets this wrong.", "They can smile. Let them." },
            },
            new CountrySpec
            {
                Id = Japan, Name = "Japan", Adjective = "Japanese", DocumentHeader = "JAPAN",
                Tier = 3, UnlockReputation = 450, HandbookPrice = 2200,
                BasePassportPrice = 1100, BaseRisk = 0.40f,
                Photo = new PhotoRules
                {
                    Backdrop = Backdrop.White, GlassesAllowed = false, HeadwearAllowed = false,
                    CoverageMin = 0.70f, CoverageMax = 0.80f,
                },
                Passport = new PassportSpec
                {
                    Category = DocCategory.TypeC, Cover = CoverColor.DarkRed, Pages = 48, ValidityYears = 10,
                    DateFormat = DateFormat.YmdDots, PersonalFields = Basic(),
                },
            },
            new CountrySpec
            {
                Id = SouthKorea, Name = "South Korea", Adjective = "Korean", DocumentHeader = "REPUBLIC OF KOREA",
                Tier = 3, UnlockReputation = 500, HandbookPrice = 2400,
                BasePassportPrice = 1150, BaseRisk = 0.40f,
                Photo = new PhotoRules
                {
                    Backdrop = Backdrop.Blue, GlassesAllowed = false, HeadwearAllowed = false,
                    CoverageMin = 0.70f, CoverageMax = 0.80f,
                },
                Passport = new PassportSpec
                {
                    Category = DocCategory.TypeD, Cover = CoverColor.Green, Pages = 64, ValidityYears = 10,
                    DateFormat = DateFormat.DMonY,
                    PersonalFields = new[]
                    {
                        DocField.Surname, DocField.GivenNames, DocField.Sex, DocField.DateOfBirth, DocField.Height,
                    },
                },
            },
            new CountrySpec
            {
                Id = Emirates, Name = "United Arab Emirates", Adjective = "Emirati", DocumentHeader = "UNITED ARAB EMIRATES",
                Tier = 3, UnlockReputation = 550, HandbookPrice = 2800,
                BasePassportPrice = 1300, BaseRisk = 0.45f,
                Photo = new PhotoRules
                {
                    Backdrop = Backdrop.Red, GlassesAllowed = false, HeadwearAllowed = true,
                    CoverageMin = 0.65f, CoverageMax = 0.75f,
                },
                Passport = new PassportSpec
                {
                    Category = DocCategory.TypeB, Cover = CoverColor.Black, Pages = 48, ValidityYears = 5,
                    DateFormat = DateFormat.DmySlashes, PersonalFields = Basic(),
                },
                Notes = new[] { "FIVE years. Not ten." },
            },
            new CountrySpec
            {
                Id = Iran, Name = "Iran", Adjective = "Iranian", DocumentHeader = "ISLAMIC REPUBLIC OF IRAN",
                Tier = 4, UnlockReputation = 750, HandbookPrice = 3500,
                BasePassportPrice = 1600, BaseRisk = 0.60f,
                Photo = new PhotoRules
                {
                    Backdrop = Backdrop.White, GlassesAllowed = false, HeadwearAllowed = true,
                    CoverageMin = 0.70f, CoverageMax = 0.80f,
                },
                Passport = new PassportSpec
                {
                    Category = DocCategory.TypeA, Cover = CoverColor.Brown, Pages = 32, ValidityYears = 5,
                    DateFormat = DateFormat.YmdDashes, PersonalFields = Basic(),
                },
            },
        };
    }
}
