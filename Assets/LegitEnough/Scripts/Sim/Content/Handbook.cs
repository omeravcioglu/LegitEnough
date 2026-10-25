using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public readonly struct HandbookLine
    {
        public readonly string Label;
        public readonly string Value;
        /// <summary>Handwritten margin note rather than printed rule.</summary>
        public readonly bool Handwritten;

        public HandbookLine(string label, string value, bool handwritten = false)
        {
            Label = label;
            Value = value;
            Handwritten = handwritten;
        }
    }

    public sealed class HandbookPage
    {
        public string Tab = "";
        public string Title = "";
        public readonly List<HandbookLine> Lines = new List<HandbookLine>();

        public HandbookPage Add(string label, string value)
        {
            Lines.Add(new HandbookLine(label, value));
            return this;
        }

        public HandbookPage Note(string text)
        {
            Lines.Add(new HandbookLine("", text, true));
            return this;
        }
    }

    /// <summary>
    /// The physical binder on the desk (design doc §13). Country pages are generated from
    /// the same specs the evaluator uses, so the handbook can never disagree with the rules.
    /// </summary>
    public static class Handbook
    {
        public static HandbookPage General()
        {
            var p = new HandbookPage { Tab = "BASICS", Title = "HOW THIS SHOP WORKS" };
            p.Add("1. LISTEN", "Customer says what they need and what they'll pay. Accept, haggle or refuse.");
            p.Add("2. THE SHEET", "They hand over their details. Keep it next to the computer.");
            p.Add("3. PHOTO", "Send them to the stool. Use the CAMERA. Check this binder for the country's photo rules.");
            p.Add("4. CROP", "Top of head on the dashed line, chin in the country's band. Measure. Don't guess.");
            p.Add("5. OFFICIAL-ISH", "On the computer: pick the order, the template, category, cover, pages. Type the data. Insert the photo.");
            p.Add("6. PRINT & BIND", "Print the data page. Bind it into a booklet. Look at it before it leaves your hands.");
            p.Add("7. HAND OVER", "Drag the booklet to the customer. They'll look it over. Then they pay. Probably.");
            p.Add("8. WAIT", "You'll hear how it went. Eventually. Maybe.");
            p.Note("Every country is different. That's the job.");
            p.Note("Nothing needs to be perfect. It needs to be legit enough.");
            return p;
        }

        public static HandbookPage Photo(CountrySpec c)
        {
            var r = c.Photo;
            var p = new HandbookPage { Tab = "PHOTO", Title = $"{c.Name.ToUpperInvariant()} - PHOTO" };
            p.Add("BACKGROUND", Labels.Of(r.Backdrop));
            p.Add("EXPRESSION", r.SmileAllowed ? "NEUTRAL OR NATURAL SMILE" : "NEUTRAL, MOUTH CLOSED");
            p.Add("EYES", "OPEN, LOOKING INTO THE CAMERA");
            p.Add("HEAD", "FACING FORWARD, LEVEL");
            p.Add("GLASSES", r.GlassesAllowed ? "ALLOWED (NO TINTED LENSES)" : "NOT ALLOWED");
            p.Add("HEADWEAR", r.HeadwearAllowed ? "ALLOWED" : "NOT ALLOWED");
            p.Add("HEAD SIZE", $"{Labels.Percent(r.CoverageMin)} - {Labels.Percent(r.CoverageMax)}");
            p.Add("", "(top of hair or hat to chin, measured on the crop ruler)");
            p.Add("PHOTO SIZE", "35 x 45, head centred");
            return p;
        }

        public static HandbookPage Passport(CountrySpec c)
        {
            var s = c.Passport;
            var p = new HandbookPage { Tab = "PASSPORT", Title = $"{c.Name.ToUpperInvariant()} - PASSPORT" };
            p.Add("TEMPLATE", $"{c.Name.ToUpperInvariant()} / PASSPORT");
            p.Add("CATEGORY", Labels.Of(s.Category));
            p.Add("COVER", Labels.Of(s.Cover));
            p.Add("PAGES", s.Pages.ToString());
            p.Add("VALIDITY", $"{s.ValidityYears} YEARS");
            p.Add("DATE FORMAT", Labels.Pattern(s.DateFormat));
            var fields = new List<string>();
            foreach (var f in s.PersonalFields) fields.Add(Labels.Of(f));
            p.Add("DATA FIELDS", string.Join(", ", fields));
            p.Add("DATE OF ISSUE", "The day it is printed");
            p.Add("DATE OF EXPIRY", $"Issue date + {s.ValidityYears} years");
            p.Add("DOCUMENT NO.", "Generate in the editor");
            return p;
        }

        public static HandbookPage Notes(CountrySpec c)
        {
            var p = new HandbookPage { Tab = "NOTES", Title = $"{c.Name.ToUpperInvariant()} - NOTES" };
            p.Add("BASE PRICE", $"~{Labels.Money(c.BasePassportPrice)}");
            if (c.Notes.Length == 0) p.Note("(nothing written here yet)");
            foreach (var n in c.Notes) p.Note(n);
            return p;
        }

        public static IEnumerable<HandbookPage> CountryPages(CountrySpec c)
        {
            yield return Passport(c);
            yield return Photo(c);
            yield return Notes(c);
        }
    }
}
