using System;
using System.Globalization;
using System.Text;

namespace LegitEnough.Sim
{
    /// <summary>Display strings and text helpers. All casing is culture-invariant (Turkish-I safe).</summary>
    public static class Labels
    {
        static readonly string[] MonthAbbrev =
            { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

        public static string Of(Backdrop b) => b switch
        {
            Backdrop.White => "WHITE",
            Backdrop.LightGray => "LIGHT GRAY",
            Backdrop.Blue => "BLUE",
            Backdrop.Red => "RED",
            _ => b.ToString().ToUpperInvariant(),
        };

        public static string Of(CoverColor c) => c switch
        {
            CoverColor.DarkRed => "DARK RED",
            CoverColor.Navy => "NAVY",
            CoverColor.Black => "BLACK",
            CoverColor.Green => "GREEN",
            CoverColor.Brown => "BROWN",
            _ => c.ToString().ToUpperInvariant(),
        };

        public static string Of(DocCategory c) => c switch
        {
            DocCategory.TypeA => "TYPE A",
            DocCategory.TypeB => "TYPE B",
            DocCategory.TypeC => "TYPE C",
            DocCategory.TypeD => "TYPE D",
            _ => c.ToString(),
        };

        public static string Of(DocType t) => t == DocType.Passport ? "PASSPORT" : "VISA";

        public static string Of(Sex s) => s == Sex.Male ? "M" : "F";

        public static string Of(EyeColor e) => e switch
        {
            EyeColor.DarkBrown => "DARK BROWN",
            _ => e.ToString().ToUpperInvariant(),
        };

        public static string Of(DocField f) => f switch
        {
            DocField.Surname => "SURNAME",
            DocField.GivenNames => "GIVEN NAMES",
            DocField.Sex => "SEX",
            DocField.DateOfBirth => "DATE OF BIRTH",
            DocField.PlaceOfBirth => "PLACE OF BIRTH",
            DocField.Height => "HEIGHT (CM)",
            DocField.EyeColor => "EYE COLOUR",
            DocField.DateOfIssue => "DATE OF ISSUE",
            DocField.DateOfExpiry => "DATE OF EXPIRY",
            DocField.DocumentNumber => "DOCUMENT NO.",
            _ => f.ToString().ToUpperInvariant(),
        };

        public static string Of(Expression e) => e switch
        {
            Expression.Neutral => "NEUTRAL",
            Expression.Smile => "SMILING",
            Expression.Grin => "GRINNING",
            Expression.Angry => "ANGRY",
            Expression.Nervous => "NERVOUS",
            Expression.Tired => "TIRED",
            Expression.Surprised => "SURPRISED",
            _ => e.ToString().ToUpperInvariant(),
        };

        /// <summary>The pattern string the handbook prints, e.g. "DD.MM.YYYY".</summary>
        public static string Pattern(DateFormat f) => f switch
        {
            DateFormat.DmyDots => "DD.MM.YYYY",
            DateFormat.DmySlashes => "DD/MM/YYYY",
            DateFormat.YmdDashes => "YYYY-MM-DD",
            DateFormat.MdySlashes => "MM/DD/YYYY",
            DateFormat.YmdDots => "YYYY.MM.DD",
            DateFormat.DMonY => "DD MON YYYY",
            _ => "?",
        };

        public static string FormatDate(DateTime d, DateFormat f)
        {
            string dd = d.Day.ToString("00", CultureInfo.InvariantCulture);
            string mm = d.Month.ToString("00", CultureInfo.InvariantCulture);
            string yyyy = d.Year.ToString("0000", CultureInfo.InvariantCulture);
            return f switch
            {
                DateFormat.DmyDots => $"{dd}.{mm}.{yyyy}",
                DateFormat.DmySlashes => $"{dd}/{mm}/{yyyy}",
                DateFormat.YmdDashes => $"{yyyy}-{mm}-{dd}",
                DateFormat.MdySlashes => $"{mm}/{dd}/{yyyy}",
                DateFormat.YmdDots => $"{yyyy}.{mm}.{dd}",
                DateFormat.DMonY => $"{dd} {MonthAbbrev[d.Month - 1]} {yyyy}",
                _ => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            };
        }

        /// <summary>Try every known format; used to tell "right date, wrong format" from "wrong date".</summary>
        public static bool TryParseAnyFormat(string text, out DateTime date)
        {
            date = default;
            string t = Normalize(text);
            if (t.Length == 0) return false;
            foreach (DateFormat f in Enum.GetValues(typeof(DateFormat)))
            {
                if (TryParse(t, f, out date)) return true;
            }
            return false;
        }

        public static bool TryParse(string text, DateFormat f, out DateTime date)
        {
            date = default;
            string t = Normalize(text);
            string[] parts;
            int y, m, d;
            switch (f)
            {
                case DateFormat.DmyDots: parts = t.Split('.'); if (!Three(parts, out d, out m, out y)) return false; break;
                case DateFormat.DmySlashes: parts = t.Split('/'); if (!Three(parts, out d, out m, out y)) return false; break;
                case DateFormat.YmdDashes: parts = t.Split('-'); if (!Three(parts, out y, out m, out d)) return false; break;
                case DateFormat.MdySlashes: parts = t.Split('/'); if (!Three(parts, out m, out d, out y)) return false; break;
                case DateFormat.YmdDots: parts = t.Split('.'); if (!Three(parts, out y, out m, out d)) return false; break;
                case DateFormat.DMonY:
                    parts = t.Split(' ');
                    if (parts.Length != 3) return false;
                    if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out d)) return false;
                    m = Array.IndexOf(MonthAbbrev, parts[1]) + 1;
                    if (m <= 0) return false;
                    if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out y)) return false;
                    break;
                default: return false;
            }
            if (y < 1000 || m < 1 || m > 12 || d < 1 || d > DateTime.DaysInMonth(y, m)) return false;
            date = new DateTime(y, m, d);
            return true;
        }

        static bool Three(string[] p, out int a, out int b, out int c)
        {
            a = b = c = 0;
            return p.Length == 3
                && int.TryParse(p[0], NumberStyles.None, CultureInfo.InvariantCulture, out a)
                && int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out b)
                && int.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out c);
        }

        /// <summary>Uppercase (invariant), trimmed, single spaces.</summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            bool space = false;
            foreach (char ch in s.Trim())
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!space) sb.Append(' ');
                    space = true;
                }
                else
                {
                    sb.Append(char.ToUpperInvariant(ch));
                    space = false;
                }
            }
            return sb.ToString();
        }

        public static int Levenshtein(string a, string b)
        {
            a ??= "";
            b ??= "";
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                var t = prev;
                prev = cur;
                cur = t;
            }
            return prev[b.Length];
        }

        public static string Money(int dollars) =>
            (dollars < 0 ? "-$" : "$") + Math.Abs(dollars).ToString("#,0", CultureInfo.InvariantCulture);

        public static string Percent(float fraction) =>
            Math.Round(fraction * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
