using System;
using System.Collections.Generic;
using System.Globalization;

namespace LegitEnough.Sim
{
    public enum Issue
    {
        // Photo
        NoPhoto,
        WrongPerson,
        WrongBackdrop,
        BackdropMoving,
        EyesClosed,
        EyesHalfClosed,
        LookingAway,
        HeadTurned,
        HeadTilted,
        WrongExpression,
        GlassesWorn,
        SunglassesWorn,
        HeadwearWorn,
        HeadTooSmall,
        HeadTooLarge,
        OffCenter,
        HeadCutOff,
        LowResolution,
        Blurry,
        Grainy,

        // Document
        WrongCountry,
        WrongDocType,
        WrongCategory,
        WrongCover,
        WrongPages,
        MissingField,
        Misspelled,
        WrongValue,
        WrongDateFormat,
        WrongIssueDate,
        WrongExpiry,
        MissingNumber,
        Smudged,
    }

    public enum ScoreArea { Photo, Information, Formatting }

    /// <summary>One thing wrong with a job. Soft issues subtract points; hard issues also multiply the final score.</summary>
    public readonly struct Finding
    {
        public readonly Issue Issue;
        public readonly ScoreArea Area;
        public readonly DocField? Field;
        public readonly float Penalty;
        public readonly float Multiplier;
        public readonly string Detail;

        public Finding(Issue issue, ScoreArea area, float penalty, float multiplier = 1f, DocField? field = null, string detail = "")
        {
            Issue = issue;
            Area = area;
            Penalty = penalty;
            Multiplier = multiplier;
            Field = field;
            Detail = detail ?? "";
        }

        public bool IsHard => Multiplier < 1f;

        public override string ToString()
        {
            string f = Field.HasValue ? $" [{Field.Value}]" : "";
            string m = Multiplier < 1f ? $" x{Multiplier.ToString("0.00", CultureInfo.InvariantCulture)}" : "";
            return $"{Issue}{f} -{Penalty.ToString("0", CultureInfo.InvariantCulture)}{m} {Detail}".TrimEnd();
        }
    }

    /// <summary>
    /// Internal quality of a delivered job (design doc §28). Never shown to the player as a
    /// number; it drives customer reactions, outcomes and messages.
    /// </summary>
    public sealed class QualityReport
    {
        public float Photo = 100f;
        public float Information = 100f;
        public float Formatting = 100f;
        public float Final;
        /// <summary>Equipment bonus (studio lighting) added to the photo score; can be negative.</summary>
        public float PhotoBonus;
        public readonly List<Finding> Findings = new List<Finding>();

        public void Add(Finding f) => Findings.Add(f);

        public bool Has(Issue issue)
        {
            foreach (var f in Findings) if (f.Issue == issue) return true;
            return false;
        }

        public void Finish()
        {
            float photo = 100f, info = 100f, format = 100f, mult = 1f;
            foreach (var f in Findings)
            {
                switch (f.Area)
                {
                    case ScoreArea.Photo: photo -= f.Penalty; break;
                    case ScoreArea.Information: info -= f.Penalty; break;
                    default: format -= f.Penalty; break;
                }
                mult *= f.Multiplier;
            }
            Photo = Clamp(photo + PhotoBonus);
            Information = Clamp(info);
            Formatting = Clamp(format);
            Final = Clamp((0.45f * Photo + 0.35f * Information + 0.20f * Formatting) * mult);
        }

        static float Clamp(float v) => Math.Max(0f, Math.Min(100f, v));

        public override string ToString() =>
            $"PHOTO {Photo:0} | INFO {Information:0} | FORMAT {Formatting:0} | FINAL {Final:0}";
    }

    public static class IssueInfo
    {
        /// <summary>Fixing this means a new photo (a missing or wrong photo is fixed in the editor instead).</summary>
        public static bool NeedsRetake(Finding f) =>
            f.Area == ScoreArea.Photo && f.Issue != Issue.NoPhoto && f.Issue != Issue.WrongPerson;

        /// <summary>Chance a customer spots this when they look over their document at the counter.</summary>
        public static float Noticeability(Finding f)
        {
            switch (f.Issue)
            {
                case Issue.WrongPerson: return 1f;
                case Issue.NoPhoto: return 1f;
                case Issue.WrongCountry: return 0.95f;
                case Issue.EyesClosed: return 0.85f;
                case Issue.HeadCutOff: return 0.75f;
                case Issue.Blurry: return 0.4f;
                case Issue.Smudged: return 0.5f;
                case Issue.LookingAway: return 0.25f;
                case Issue.WrongExpression: return 0.15f;
                case Issue.WrongBackdrop: return 0.08f;
                case Issue.WrongCover: return 0.1f;
                case Issue.MissingField: return 0.6f;
                case Issue.Misspelled:
                case Issue.WrongValue:
                    if (f.Field == DocField.Surname || f.Field == DocField.GivenNames) return 0.9f;
                    if (f.Field == DocField.DateOfBirth) return 0.7f;
                    if (f.Field == DocField.PlaceOfBirth) return 0.45f;
                    return 0.15f;
                default: return 0f;
            }
        }
    }
}
