using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using LegitEnough.Sim;

namespace LegitEnough.SimTests
{
    /// <summary>Minimal test runner (no NUnit dependency) for the engine-free simulation.</summary>
    public static class TestMain
    {
        static int _passed, _failed;
        static readonly List<string> Failures = new List<string>();

        public static int Main(string[] args)
        {
            string filter = args.Length > 0 ? args[0] : "";
            // Run under a Turkish culture on purpose: dotless-i bugs show up immediately.
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("tr-TR");

            var tests = new List<(string, Action)>
            {
                ("Rng_IsDeterministic", Tests.Rng_IsDeterministic),
                ("Generator_SameIdSameCustomer", Tests.Generator_SameIdSameCustomer),
                ("Generator_VarietyAcrossIds", Tests.Generator_VarietyAcrossIds),
                ("Labels_DateFormatsRoundTrip", Tests.Labels_DateFormatsRoundTrip),
                ("Labels_TurkishCultureSafe", Tests.Labels_TurkishCultureSafe),
                ("Labels_Levenshtein", Tests.Labels_Levenshtein),
                ("Negotiation_Behaves", Tests.Negotiation_Behaves),
                ("HeadPose_Steps", Tests.HeadPose_Steps),
                ("Geometry_CoverageMath", Tests.Geometry_CoverageMath),
                ("PhotoEvaluator_RulesMatter", Tests.PhotoEvaluator_RulesMatter),
                ("PhotoSubject_BlinksAndDrifts", Tests.PhotoSubject_BlinksAndDrifts),
                ("DocumentEvaluator_ScoresErrors", Tests.DocumentEvaluator_ScoresErrors),
                ("Outcome_ProbabilityShape", Tests.Outcome_ProbabilityShape),
                ("Handbook_MatchesSpec", Tests.Handbook_MatchesSpec),
                ("News_RuleChangeAndOutages", Tests.News_RuleChangeAndOutages),
                ("Session_Day1_PerfectJob_ToMessage", Tests.Session_Day1_PerfectJob_ToMessage),
                ("Session_Day1_SloppyJob", Tests.Session_Day1_SloppyJob),
                ("Session_TwentyDays_Bot", Tests.Session_TwentyDays_Bot),
                ("Demo_OnlyDemoCountriesRequested", DemoTests.Demo_OnlyDemoCountriesRequested),
                ("Demo_LocksAndDayTwoMorning", DemoTests.Demo_LocksAndDayTwoMorning),
                ("Demo_TutorialSamirRestricted", DemoTests.Demo_TutorialSamirRestricted),
                ("Demo_FirstPrintJamsCleanly", DemoTests.Demo_FirstPrintJamsCleanly),
                ("Demo_SamirOutcomeOnDayTwoCard", DemoTests.Demo_SamirOutcomeOnDayTwoCard),
                ("Demo_BotPlaysWholeDemo", DemoTests.Demo_BotPlaysWholeDemo),
                ("Demo_CounterSignAndClockRules", DemoTests.Demo_CounterSignAndClockRules),
                ("Retake_VoluntaryFullMode", DemoTests.Retake_VoluntaryFullMode),
                ("Retake_VoluntaryDemo", DemoTests.Retake_VoluntaryDemo),
                ("Ach_CatalogValid", AchievementTests.Ach_CatalogValid),
                ("Ach_BookRules", AchievementTests.Ach_BookRules),
                ("Ach_FullModeUnchanged", AchievementTests.Ach_FullModeUnchanged),
                ("Ach_DemoUnchanged", AchievementTests.Ach_DemoUnchanged),
                ("Ach_DemoDefaultPath", AchievementTests.Ach_DemoDefaultPath),
                ("Ach_DemoDesignedPath", AchievementTests.Ach_DemoDesignedPath),
                ("Ach_DemoNightShift", AchievementTests.Ach_DemoNightShift),
                ("Ach_DemoCarelessSamir", AchievementTests.Ach_DemoCarelessSamir),
                ("Ach_TutorialNothingEarly", AchievementTests.Ach_TutorialNothingEarly),
                ("Ach_RuleChangeSameDayOnly", AchievementTests.Ach_RuleChangeSameDayOnly),
                ("Ach_FullProbes", AchievementTests.Ach_FullProbes),
            };

            foreach (var (name, test) in tests)
            {
                if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    test();
                    _passed++;
                    Console.WriteLine($"  PASS  {name}");
                }
                catch (Exception e)
                {
                    _failed++;
                    Failures.Add($"{name}: {e}");
                    Console.WriteLine($"  FAIL  {name}: {e.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"{_passed} passed, {_failed} failed");
            foreach (var f in Failures) Console.WriteLine("\n--- " + f);
            return _failed == 0 ? 0 : 1;
        }
    }

    public sealed class AssertException : Exception
    {
        public AssertException(string m) : base(m) { }
    }

    public static class Assert
    {
        public static void True(bool cond, string msg)
        {
            if (!cond) throw new AssertException(msg);
        }

        public static void Equal<T>(T expected, T actual, string msg)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new AssertException($"{msg}: expected <{expected}> got <{actual}>");
        }

        public static void Near(float expected, float actual, float tol, string msg)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new AssertException($"{msg}: expected {expected} ± {tol}, got {actual}");
        }
    }
}
