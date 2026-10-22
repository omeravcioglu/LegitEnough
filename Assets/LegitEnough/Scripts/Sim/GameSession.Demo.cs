using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>The demo's script: the guided Day 1 and the fixed Day 2. Nothing here runs in the full game.</summary>
    public sealed partial class GameSession
    {
        const string QuietAfternoonFlag = "demo_quiet_sent";

        /// <summary>Game minutes the scripted visitors were moved by when the sign flipped (opened late = positive).</summary>
        float _demoShift;

        /// <summary>Replaces the random morning (news, walk-ins) on demo days.</summary>
        void RunDemoMorning(int day)
        {
            // Day 1 teaches the jam: the first page sticks on purpose and clears cleanly.
            Printer.ForceJamNextPage = TutorialActive;
            _demoShift = 0f;
            if (day != DemoCast.DayTwo) return;

            Countries[Sim.Countries.Turkey].Owned = true;
            Inbox.Schedule(new Message
            {
                ArrivesAt = new GameTime(day, Balance.MorningMinute - 30),
                From = MessageTemplates.WelcomeFrom,
                Subject = DemoCast.DayTwoSubject,
                Body = DemoCast.DayTwoBody,
                Kind = MessageKind.System,
            });
            foreach (var n in NewsDesk.DemoMorning(day))
            {
                n.Id = _nextNewsId++;
                ApplyNews(n);
                News.Add(n);
            }
        }

        /// <summary>
        /// The demo's visitors come relative to when the sign flips (their times assume 09:00), so
        /// reading the mail first never piles everyone up at the door, and nobody waits at an open shop.
        /// </summary>
        void ShiftDemoArrivals()
        {
            _demoShift = Clock.Minute - Balance.OpenMinute;
            if (Math.Abs(_demoShift) < 0.01f) return;
            var today = new List<ScheduledArrival>();
            foreach (var a in Director.Upcoming)
                if (a.Kind == ArrivalKind.Story && a.At.Day == Day) today.Add(a);
            // Never later than the last arrival of the day (the guided day's clock holds just after it).
            var last = new GameTime(Day, Balance.LastArrivalMinute);
            foreach (var a in today)
            {
                var at = a.At.AddMinutes(_demoShift);
                Director.Reschedule(a, at > last ? last : at);
            }
        }

        /// <summary>Samir hasn't come yet, or is still in the shop.</summary>
        bool SamirPending()
        {
            foreach (var a in Director.Upcoming)
                if (a.Kind == ArrivalKind.Story && a.Beat?.BeatId == StoryCast.SamirBeatId) return true;
            foreach (var v in Visits)
                if (v.Phase != VisitPhase.Gone && v.Customer.Id == StoryCast.SamirId) return true;
            return false;
        }

        /// <summary>A scripted visitor is still expected today (closing early would skip them).</summary>
        bool DemoVisitorStillDue()
        {
            if (_flags.Contains(QuietAfternoonFlag)) return false; // R. said to close up.
            foreach (var a in Director.Upcoming)
                if (a.Kind == ArrivalKind.Story && a.At.Day <= Day) return true;
            return false;
        }

        /// <summary>
        /// The job is possible again (handbook bought, country taken again): anyone at the counter who
        /// could only be told "no" gets the real offer instead.
        /// </summary>
        void ReofferJobs(string countryId, string line)
        {
            foreach (var v in Visits)
            {
                if (v.Phase != VisitPhase.Choosing || v.Moving || v.Leaving || v.Order != null || v.Negotiation == null) continue;
                if (v.Request?.CountryId != countryId) continue;
                if (v.Choices.Count != 1 || v.Choices[0].Id != ChoiceIds.Decline) continue;
                var visit = v;
                visit.Choices.Clear();
                visit.Say(line);
                Then(visit, () => ShowNegotiation(visit));
            }
        }

        /// <summary>
        /// Demo copy honesty: true when a scripted visitor who wants this country is still to come or is
        /// in the shop today. "Customers are asking" only when someone actually will.
        /// </summary>
        public bool DemoClientDue(string countryId)
        {
            if (!IsDemo || countryId == null) return false;
            foreach (var a in Director.Upcoming)
                if (a.Kind == ArrivalKind.Story && a.Beat != null && a.Beat.CountryId == countryId) return true;
            foreach (var b in _storyQueue)
                if (b.CountryId == countryId && b.EarliestDay <= Rules.LastDay) return true;
            foreach (var v in Visits)
                if (v.Phase != VisitPhase.Gone && v.Beat != null && v.Beat.CountryId == countryId && v.Order == null) return true;
            return false;
        }

        /// <summary>Once the scripted visitors are done and the shop is empty, R. says to call it a day.</summary>
        void TickDemo()
        {
            if (Day != DemoCast.DayTwo || Phase != DayPhase.Open || _flags.Contains(QuietAfternoonFlag)) return;
            // Same rule as closing: never tell the player to close while someone is inside.
            foreach (var v in Visits)
                if (v.Phase != VisitPhase.Gone) return;
            // Mid-afternoon (later if the shop opened late), stop waiting for anyone who never came.
            float fallback = DemoCast.QuietAfternoonMinute + Math.Max(0f, _demoShift);
            if (Clock.Minute < fallback)
                foreach (var a in Director.Upcoming)
                    if (a.Kind == ArrivalKind.Story && DemoCast.IsDemoBeat(a.Beat)) return;
            _flags.Add(QuietAfternoonFlag);
            Inbox.Schedule(new Message
            {
                ArrivesAt = Clock.Now,
                From = MessageTemplates.WelcomeFrom,
                Subject = DemoCast.QuietSubject,
                Body = DemoCast.QuietBody,
                Kind = MessageKind.System,
            });
        }
    }
}
