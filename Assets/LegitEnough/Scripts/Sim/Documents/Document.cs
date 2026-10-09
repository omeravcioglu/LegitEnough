using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>What the player has entered in the editor for one order. Nothing is validated.</summary>
    public sealed class DocumentDraft
    {
        public int OrderId;
        /// <summary>Chosen template country; null until picked.</summary>
        public string CountryId;
        public DocType DocType = DocType.Passport;
        public DocCategory? Category;
        public CoverColor? Cover;
        public int? Pages;
        public int? PhotoId;
        public readonly Dictionary<DocField, string> Fields = new Dictionary<DocField, string>();

        public string Get(DocField f) => Fields.TryGetValue(f, out var v) ? v ?? "" : "";

        public void Set(DocField f, string value) => Fields[f] = value ?? "";

        public DocumentDraft Clone()
        {
            var c = new DocumentDraft
            {
                OrderId = OrderId,
                CountryId = CountryId,
                DocType = DocType,
                Category = Category,
                Cover = Cover,
                Pages = Pages,
                PhotoId = PhotoId,
            };
            foreach (var kv in Fields) c.Fields[kv.Key] = kv.Value;
            return c;
        }
    }

    /// <summary>A data page sitting in (or coming out of) the printer.</summary>
    public sealed class PrintedPage
    {
        public int Id;
        public int OrderId;
        public DocumentDraft Data;
        public GameTime PrintedAt;
        /// <summary>The old printer sometimes streaks ink across the page.</summary>
        public bool Smudged;
    }

    /// <summary>A bound booklet, ready to hand over.</summary>
    public sealed class FinishedDocument
    {
        public int Id;
        public int OrderId;
        public DocumentDraft Data;
        /// <summary>Issue date used for scoring: the day the page was printed.</summary>
        public int IssuedDay;
        public CoverColor BookletCover;
        public GameTime BoundAt;
        public bool Smudged;
    }
}
