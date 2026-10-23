using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum AchCategory { Progress, Skill, Story, Secret }

    /// <summary>One achievement. <see cref="Id"/> is the Steam API name.</summary>
    public sealed class AchievementDef
    {
        public string Id;
        public string Name, Description;
        /// <summary>Shown instead of Name/Description while a hidden one is locked (null if not hidden).</summary>
        public string Teaser;
        public bool Hidden, Demo;
        public AchCategory Category;
        /// <summary>Progress stat API name, or null for an event achievement.</summary>
        public string Stat;
        /// <summary>Stat value that unlocks it (0 = event achievement).</summary>
        public int Target;

        public bool IsProgress => Stat != null;

        public override string ToString() => Id;
    }

    /// <summary>The single source of truth for achievements: in-game screen, Steam export and tracker.</summary>
    public static class AchievementCatalog
    {
        public const string StatJobsDone = "STAT_JOBS_DONE";
        public const string StatRepBest = "STAT_REP_BEST";
        public const string StatCashBest = "STAT_CASH_BEST";
        public const string StatSectionsBest = "STAT_SECTIONS_BEST";
        public const string StatTier3Best = "STAT_TIER3_BEST";
        public const string StatDaysBest = "STAT_DAYS_BEST";

        public const string FirstPhoto = "FIRST_PHOTO";
        public const string PercussiveMaintenance = "PERCUSSIVE_MAINTENANCE";
        public const string FirstJob = "FIRST_JOB";
        public const string Quality90 = "QUALITY_90";
        public const string SayCheese = "SAY_CHEESE";
        public const string SamirMadeIt = "SAMIR_MADE_IT";
        public const string Haggler = "HAGGLER";
        public const string ReadThePaper = "READ_THE_PAPER";
        public const string RushJob = "RUSH_JOB";
        public const string JustInTime = "JUST_IN_TIME";
        public const string FirstUpgrade = "FIRST_UPGRADE";
        public const string NightShift = "NIGHT_SHIFT";
        public const string Jobs25 = "JOBS_25";
        public const string Jobs100 = "JOBS_100";
        public const string RepWord = "REP_WORD";
        public const string RepPro = "REP_PRO";
        public const string RepLegend = "REP_LEGEND";
        public const string Cash10K = "CASH_10K";
        public const string Sections5 = "SECTIONS_5";
        public const string SectionsAll = "SECTIONS_ALL";
        public const string TopShelf = "TOP_SHELF";
        public const string FullKit = "FULL_KIT";
        public const string WeekOne = "WEEK_ONE";
        public const string Flawless = "FLAWLESS";
        public const string CleanPrintDay = "CLEAN_PRINT_DAY";
        public const string SmoothTalker = "SMOOTH_TALKER";
        public const string CleanSheet = "CLEAN_SHEET";
        public const string LyingLow = "LYING_LOW";
        public const string Candlelight = "CANDLELIGHT";
        public const string WordOfMouth = "WORD_OF_MOUTH";
        public const string YoussefMadeIt = "YOUSSEF_MADE_IT";
        public const string NotToday = "NOT_TODAY";
        public const string BackupPair = "BACKUP_PAIR";
        public const string Incognito = "INCOGNITO";
        public const string MixUp = "MIX_UP";
        public const string SamirCaught = "SAMIR_CAUGHT";

        /// <summary>The progress stat API names.</summary>
        public static readonly string[] Stats =
        {
            StatJobsDone, StatRepBest, StatCashBest, StatSectionsBest, StatTier3Best, StatDaysBest,
        };

        /// <summary>All shipped achievements, in table order: the 12 demo ones first.</summary>
        public static readonly IReadOnlyList<AchievementDef> All;

        static readonly Dictionary<string, AchievementDef> ById = new Dictionary<string, AchievementDef>(StringComparer.Ordinal);

        public static int DemoCount { get; }

        static AchievementCatalog()
        {
            const AchCategory P = AchCategory.Progress, S = AchCategory.Skill, T = AchCategory.Story, H = AchCategory.Secret;
            var list = new List<AchievementDef>
            {
                // ---- the demo 12
                Ev(FirstPhoto, "Don't Smile", "Keep your first passport photo.", P, demo: true),
                Ev(PercussiveMaintenance, "Percussive Maintenance", "Clear a printer jam.", P, demo: true),
                Ev(FirstJob, "Open for Business", "Get paid for your first job.", P, demo: true),
                Ev(Quality90, "Legit Enough", "Deliver a passport so good it could be real.", S, demo: true),
                Ev(SayCheese, "Just One More", "Call the same customer back to the stool three times.", H, demo: true,
                    teaser: "Perfection takes time. Theirs."),
                Ev(SamirMadeIt, "See You in Lyon", "Get Samir to his family.", T, demo: true),
                Ev(Haggler, "Name Your Price", "Talk a customer above their opening offer.", S, demo: true),
                Ev(ReadThePaper, "Read the Paper", "Deliver a job that follows a rule change on the day it's announced.", S, demo: true),
                Ev(RushJob, "Wheels Up", "Deliver a rush job before its deadline.", S, demo: true),
                Ev(JustInTime, "Just in Time", "Buy a country's handbook section while its customer waits, then take the job.", T, demo: true),
                Ev(FirstUpgrade, "Business Expense", "Buy your first equipment upgrade.", P, demo: true),
                Ev(NightShift, "Night Shift", "Still be at the desk at 23:00.", H, demo: true,
                    teaser: "Some nights the shop never closes."),

                // ---- full game
                Prog(Jobs25, "Paper Pusher", "Deliver 25 passports. Every run counts.", StatJobsDone, 25),
                Prog(Jobs100, "Cottage Industry", "Deliver 100 passports. Every run counts.", StatJobsDone, 100),
                Prog(RepWord, "Word Gets Around", "Reach 100 reputation.", StatRepBest, 100),
                Prog(RepPro, "Professional Courtesy", "Reach 500 reputation.", StatRepBest, 500),
                Prog(RepLegend, "Urban Legend", "Reach 1,000 reputation: legendary underground specialist.", StatRepBest, 1000),
                Prog(Cash10K, "Mattress Money", "Have $10,000 in the drawer at once.", StatCashBest, 10000),
                Prog(Sections5, "Well Travelled", "Own handbook sections for 5 countries.", StatSectionsBest, 5),
                Prog(SectionsAll, "Every Border", "Own handbook sections for all 10 countries.", StatSectionsBest, 10),
                Ev(TopShelf, "Top Shelf", "Own a piece of tier-3 equipment.", P),
                Prog(FullKit, "Studio Grade", "Own the tier-3 camera, printer, computer and photo station.", StatTier3Best, 4),
                Prog(WeekOne, "Still Open", "Finish your seventh day in business.", StatDaysBest, 7),
                Ev(Flawless, "Not a Hair Out of Place", "Deliver a passport with nothing wrong with it. Nothing.", S),
                Ev(CleanPrintDay, "Smooth Run", "Print 6 pages in one day without a single jam.", S),
                Ev(SmoothTalker, "Trust Me", "Talk a customer who found a problem into paying full price.", S),
                Ev(CleanSheet, "Clean Sheet", "Deliver 4 jobs in one day: no redos, no discounts, nothing late, no cancellations.", S),
                Ev(LyingLow, "Lying Low", "Bring your heat down from HIGH RISK to LOW PROFILE.", S),
                Ev(Candlelight, "Candlelight Service", "Deliver a job while the power is out.", S),
                Ev(WordOfMouth, "Word of Mouth", "Deliver a job for a customer a past client sent your way.", T),
                Ev(YoussefMadeIt, "Family Business", "Get Samir's brother to Lyon too.", T,
                    teaser: "Someone you helped comes back. Not alone."),
                Ev(NotToday, "Not Today", "Take no jobs on a day when 5 or more customers come in.", H,
                    teaser: "Some days the answer is no."),
                Ev(BackupPair, "Backup Pair", "Take off a customer's glasses and find another pair underneath.", H,
                    teaser: "Always carry a spare."),
                Ev(Incognito, "Incognito", "Keep a passport photo of someone in sunglasses.", H,
                    teaser: "Nobody will recognise them. That's the problem."),
                Ev(MixUp, "Mix-Up", "Hand a customer someone else's passport.", H,
                    teaser: "Paperwork is easy to mix up."),
                Ev(SamirCaught, "Lost in Transit", "Samir doesn't make it.", H,
                    teaser: "Not every story ends in Lyon."),
            };

            int demo = 0;
            foreach (var d in list)
            {
                ById.Add(d.Id, d);
                if (d.Demo) demo++;
            }
            All = list.AsReadOnly();
            DemoCount = demo;
        }

        /// <summary>The def with this API name, or null.</summary>
        public static AchievementDef Get(string id) =>
            id != null && ById.TryGetValue(id, out var d) ? d : null;

        static AchievementDef Ev(string id, string name, string desc, AchCategory cat, bool demo = false, string teaser = null) =>
            new AchievementDef
            {
                Id = id, Name = name, Description = desc, Category = cat, Demo = demo,
                Teaser = teaser, Hidden = teaser != null,
            };

        static AchievementDef Prog(string id, string name, string desc, string stat, int target) =>
            new AchievementDef
            {
                Id = id, Name = name, Description = desc, Category = AchCategory.Progress,
                Stat = stat, Target = target,
            };
    }
}
