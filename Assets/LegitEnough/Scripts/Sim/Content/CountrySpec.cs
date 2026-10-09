using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>
    /// Fictional photo requirements. Real country names, invented rules: these are game
    /// specifications, not real issuing standards.
    /// </summary>
    public sealed class PhotoRules
    {
        public Backdrop Backdrop = Backdrop.White;
        public bool SmileAllowed;
        public bool GlassesAllowed;
        public bool HeadwearAllowed;
        /// <summary>Head height (crown to chin) as a fraction of photo height.</summary>
        public float CoverageMin = 0.6f;
        public float CoverageMax = 0.7f;

        public bool ExpressionOk(Expression e) =>
            e == Expression.Neutral || (SmileAllowed && e == Expression.Smile);

        public PhotoRules Clone() => (PhotoRules)MemberwiseClone();
    }

    /// <summary>Fictional passport template specification.</summary>
    public sealed class PassportSpec
    {
        public DocCategory Category = DocCategory.TypeA;
        public CoverColor Cover = CoverColor.Navy;
        public int Pages = 32;
        public int ValidityYears = 10;
        public DateFormat DateFormat = DateFormat.DmyDots;
        /// <summary>Personal fields printed on the data page, in order.</summary>
        public DocField[] PersonalFields = Array.Empty<DocField>();

        public static readonly DocField[] AdministrativeFields =
        {
            DocField.DateOfIssue,
            DocField.DateOfExpiry,
            DocField.DocumentNumber,
        };

        public IEnumerable<DocField> AllFields()
        {
            foreach (var f in PersonalFields) yield return f;
            foreach (var f in AdministrativeFields) yield return f;
        }

        public bool HasField(DocField field)
        {
            if (Array.IndexOf(PersonalFields, field) >= 0) return true;
            return Array.IndexOf(AdministrativeFields, field) >= 0;
        }

        public PassportSpec Clone()
        {
            var c = (PassportSpec)MemberwiseClone();
            c.PersonalFields = (DocField[])PersonalFields.Clone();
            return c;
        }
    }

    public sealed class CountrySpec
    {
        public string Id = "";
        public string Name = "";
        /// <summary>"French", "Turkish"... used in customer dialogue.</summary>
        public string Adjective = "";
        /// <summary>Header text on the data page template.</summary>
        public string DocumentHeader = "";
        public int Tier = 1;
        /// <summary>Reputation needed before customers start asking for this country.</summary>
        public int UnlockReputation;
        /// <summary>Price of this country's handbook section.</summary>
        public int HandbookPrice;
        public bool OwnedAtStart;

        public int BasePassportPrice = 400;
        /// <summary>0..1 baseline chance that something goes wrong at this border.</summary>
        public float BaseRisk = 0.2f;

        public PhotoRules Photo = new PhotoRules();
        public PassportSpec Passport = new PassportSpec();

        /// <summary>Handwritten notes from the previous owner, shown in the handbook.</summary>
        public string[] Notes = Array.Empty<string>();

        public CountrySpec Clone()
        {
            var c = (CountrySpec)MemberwiseClone();
            c.Photo = Photo.Clone();
            c.Passport = Passport.Clone();
            c.Notes = (string[])Notes.Clone();
            return c;
        }

        public override string ToString() => Name;
    }
}
