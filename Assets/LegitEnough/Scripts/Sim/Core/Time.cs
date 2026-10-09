using System;
using System.Globalization;

namespace LegitEnough.Sim
{
    /// <summary>A moment in the campaign: day number (1-based) plus minutes since midnight.</summary>
    public readonly struct GameTime : IComparable<GameTime>
    {
        public readonly int Day;
        public readonly float Minute;

        public GameTime(int day, float minute)
        {
            Day = day;
            Minute = minute;
        }

        public double TotalMinutes => (Day - 1) * 1440.0 + Minute;

        public static GameTime FromTotalMinutes(double total)
        {
            int day = (int)Math.Floor(total / 1440.0) + 1;
            return new GameTime(day, (float)(total - (day - 1) * 1440.0));
        }

        public GameTime AddMinutes(double minutes) => FromTotalMinutes(TotalMinutes + minutes);

        public static double MinutesBetween(GameTime from, GameTime to) => to.TotalMinutes - from.TotalMinutes;

        public int CompareTo(GameTime other) => TotalMinutes.CompareTo(other.TotalMinutes);
        public static bool operator <(GameTime a, GameTime b) => a.TotalMinutes < b.TotalMinutes;
        public static bool operator >(GameTime a, GameTime b) => a.TotalMinutes > b.TotalMinutes;
        public static bool operator <=(GameTime a, GameTime b) => a.TotalMinutes <= b.TotalMinutes;
        public static bool operator >=(GameTime a, GameTime b) => a.TotalMinutes >= b.TotalMinutes;

        public string Clock => Calendar.ClockText(Minute);
        public override string ToString() => $"Day {Day} {Clock}";
    }

    public static class Calendar
    {
        /// <summary>Day 1 of the campaign. A Monday.</summary>
        public static readonly DateTime FirstDay = new DateTime(2008, 10, 6);

        public static DateTime DateOf(int day) => FirstDay.AddDays(day - 1);

        public static string ClockText(float minute)
        {
            int m = (int)Math.Floor(minute);
            if (m < 0) m = 0;
            return $"{(m / 60) % 24:00}:{m % 60:00}";
        }

        public static string WeekdayName(int day) =>
            DateOf(day).ToString("dddd", CultureInfo.InvariantCulture);

        public static string LongDate(int day) =>
            DateOf(day).ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture);

        public static string ShortDate(int day) =>
            DateOf(day).ToString("d MMM yyyy", CultureInfo.InvariantCulture);

        /// <summary>"Today", "Tomorrow", "in 3 days", "2 days ago".</summary>
        public static string Relative(int day, int today)
        {
            int delta = day - today;
            if (delta == 0) return "Today";
            if (delta == 1) return "Tomorrow";
            if (delta == -1) return "Yesterday";
            return delta > 0 ? $"in {delta} days" : $"{-delta} days ago";
        }
    }

    /// <summary>The shop clock. Runs only during business hours; the morning waits for the player.</summary>
    public sealed class Clock
    {
        public int Day = 1;
        public float Minute = Balance.MorningMinute;
        public bool Running;

        public GameTime Now => new GameTime(Day, Minute);

        public void Advance(float realSeconds)
        {
            if (!Running || realSeconds <= 0f) return;
            Minute += realSeconds * Balance.GameMinutesPerRealSecond;
        }

        /// <summary>Explicit time cost for an action (binding a booklet, a long conversation...).</summary>
        public void Spend(float gameMinutes)
        {
            if (gameMinutes > 0f) Minute += gameMinutes;
        }

        public bool IsAfterClosing => Minute >= Balance.CloseMinute;
    }
}
