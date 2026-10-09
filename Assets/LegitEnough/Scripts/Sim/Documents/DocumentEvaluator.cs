using System;
using System.Text;

namespace LegitEnough.Sim
{
    /// <summary>
    /// Compares a finished booklet with what the order needed: the right template, the
    /// country's fictional format rules, the customer's identity and the photo rules.
    /// </summary>
    public static class DocumentEvaluator
    {
        public static QualityReport Evaluate(FinishedDocument doc, Order order, CountrySpec target,
            CustomerProfile customer, PhotoLibrary photos)
        {
            var report = new QualityReport();
            var d = doc.Data;
            const ScoreArea F = ScoreArea.Formatting;

            if (d.CountryId != order.CountryId)
                report.Add(new Finding(Issue.WrongCountry, F, 60f, 0.12f, null, $"template {d.CountryId ?? "none"} for {order.CountryId}"));
            if (d.DocType != order.DocType)
                report.Add(new Finding(Issue.WrongDocType, F, 60f, 0.15f));

            var spec = target.Passport;
            if (d.Category != spec.Category)
                report.Add(new Finding(Issue.WrongCategory, F, 20f, 0.7f, null, d.Category.HasValue ? Labels.Of(d.Category.Value) : "none"));
            if (d.Cover != spec.Cover)
                report.Add(new Finding(Issue.WrongCover, F, 20f, 0.72f, null, d.Cover.HasValue ? Labels.Of(d.Cover.Value) : "none"));
            if (d.Pages != spec.Pages)
                report.Add(new Finding(Issue.WrongPages, F, 15f, 0.85f, null, d.Pages?.ToString() ?? "none"));
            if (doc.Smudged)
                report.Add(new Finding(Issue.Smudged, F, 15f, 0.92f));

            foreach (var field in spec.PersonalFields)
                CheckPersonalField(field, d.Get(field), customer.Identity, spec.DateFormat, report);

            CheckAdministrative(d, doc.IssuedDay, spec, report);

            PhotoEvaluator.Assess(photos.Get(d.PhotoId), target.Photo, customer.Id, report);
            report.Finish();
            return report;
        }

        public static string Expected(DocField field, Identity id, DateFormat format) => field switch
        {
            DocField.Surname => id.Surname,
            DocField.GivenNames => id.GivenNames,
            DocField.Sex => Labels.Of(id.Sex),
            DocField.DateOfBirth => Labels.FormatDate(id.DateOfBirth, format),
            DocField.PlaceOfBirth => id.PlaceOfBirth,
            DocField.Height => id.HeightCm.ToString(),
            DocField.EyeColor => Labels.Of(id.EyeColor),
            _ => "",
        };

        static void CheckPersonalField(DocField field, string typed, Identity id, DateFormat format, QualityReport r)
        {
            const ScoreArea I = ScoreArea.Information;
            string value = Labels.Normalize(typed);
            string expected = Labels.Normalize(Expected(field, id, format));

            if (value.Length == 0)
            {
                bool core = field == DocField.Surname || field == DocField.GivenNames || field == DocField.DateOfBirth;
                r.Add(new Finding(Issue.MissingField, I, core ? 30f : 15f, core ? 0.6f : 1f, field));
                return;
            }

            switch (field)
            {
                case DocField.Surname:
                case DocField.GivenNames:
                {
                    if (value == expected) return;
                    int dist = Labels.Levenshtein(value, expected);
                    int allowed = expected.Length >= 10 ? 2 : 1;
                    if (dist <= allowed) r.Add(new Finding(Issue.Misspelled, I, 14f, 0.92f, field, $"'{value}' vs '{expected}'"));
                    else r.Add(new Finding(Issue.WrongValue, I, 30f, 0.6f, field, $"'{value}' vs '{expected}'"));
                    return;
                }
                case DocField.Sex:
                {
                    if (value == expected || value == (id.Sex == Sex.Male ? "MALE" : "FEMALE")) return;
                    r.Add(new Finding(Issue.WrongValue, I, 20f, 0.75f, field));
                    return;
                }
                case DocField.DateOfBirth:
                {
                    if (Labels.TryParse(value, format, out var parsed) && parsed == id.DateOfBirth.Date) return;
                    if (Labels.TryParseAnyFormat(value, out var any) && any == id.DateOfBirth.Date)
                        r.Add(new Finding(Issue.WrongDateFormat, I, 12f, 0.95f, field, $"'{value}' should be {Labels.Pattern(format)}"));
                    else
                        r.Add(new Finding(Issue.WrongValue, I, 25f, 0.7f, field, $"'{value}' vs '{expected}'"));
                    return;
                }
                case DocField.PlaceOfBirth:
                {
                    string city = value.Split(',')[0].Trim();
                    if (city == expected) return;
                    if (Labels.Levenshtein(city, expected) <= 1) r.Add(new Finding(Issue.Misspelled, I, 10f, 1f, field, $"'{city}'"));
                    else r.Add(new Finding(Issue.WrongValue, I, 20f, 0.9f, field, $"'{city}' vs '{expected}'"));
                    return;
                }
                case DocField.Height:
                {
                    string digits = Digits(value);
                    if (digits == expected) return;
                    if (int.TryParse(digits, out int h) && Math.Abs(h - id.HeightCm) <= 2)
                        r.Add(new Finding(Issue.Misspelled, I, 4f, 1f, field, $"{h} vs {id.HeightCm}"));
                    else
                        r.Add(new Finding(Issue.WrongValue, I, 12f, 1f, field, $"'{value}' vs {id.HeightCm}"));
                    return;
                }
                case DocField.EyeColor:
                {
                    if (value == expected) return;
                    r.Add(new Finding(Issue.WrongValue, I, 15f, 0.9f, field, $"'{value}' vs '{expected}'"));
                    return;
                }
            }
        }

        static void CheckAdministrative(DocumentDraft d, int issuedDay, PassportSpec spec, QualityReport r)
        {
            const ScoreArea F = ScoreArea.Formatting;
            DateTime issue = Calendar.DateOf(issuedDay);
            DateTime expiry = issue.AddYears(spec.ValidityYears);

            CheckDate(d.Get(DocField.DateOfIssue), issue, spec.DateFormat, DocField.DateOfIssue, Issue.WrongIssueDate, 15f, 0.92f, r);
            CheckDate(d.Get(DocField.DateOfExpiry), expiry, spec.DateFormat, DocField.DateOfExpiry, Issue.WrongExpiry, 20f, 0.8f, r);

            if (Labels.Normalize(d.Get(DocField.DocumentNumber)).Length == 0)
                r.Add(new Finding(Issue.MissingNumber, F, 20f, 0.85f, DocField.DocumentNumber));
        }

        static void CheckDate(string typed, DateTime expected, DateFormat format, DocField field, Issue wrongIssue,
            float penalty, float multiplier, QualityReport r)
        {
            const ScoreArea F = ScoreArea.Formatting;
            string value = Labels.Normalize(typed);
            if (value.Length == 0)
            {
                r.Add(new Finding(Issue.MissingField, F, penalty, multiplier, field));
                return;
            }
            if (Labels.TryParse(value, format, out var parsed) && parsed == expected.Date) return;
            if (Labels.TryParseAnyFormat(value, out var any) && any == expected.Date)
                r.Add(new Finding(Issue.WrongDateFormat, F, 8f, 0.97f, field, $"should be {Labels.Pattern(format)}"));
            else
                r.Add(new Finding(wrongIssue, F, penalty, multiplier, field, $"'{value}' vs '{Labels.FormatDate(expected, format)}'"));
        }

        static string Digits(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) if (c >= '0' && c <= '9') sb.Append(c);
            return sb.ToString();
        }
    }
}
