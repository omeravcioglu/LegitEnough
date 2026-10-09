using LegitEnough.Sim;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>Open a finished booklet and look at it before it leaves your hands (design doc §23, §27).</summary>
    public sealed class PassportView : Overlay
    {
        readonly FinishedDocument _doc;

        public PassportView(GameRoot game, FinishedDocument doc) : base(game, "le-overlay--dim")
        {
            _doc = doc;
            Root.RegisterCallback<ClickEvent>(_ => Close());
            var book = Ui.El("le-passport", Root).At(260, 120, 1400, 760);
            book.RegisterCallback<ClickEvent>(e => e.StopPropagation());

            var cover = Ui.El("le-passport__cover", book);
            cover.style.backgroundColor = Looks.Of(doc.BookletCover);
            var spec = S.Country(doc.Data.CountryId)?.Spec;
            Ui.Text(spec != null ? spec.DocumentHeader : "", "le-passport__cover-title", cover);
            cover.Add(UiArt.Image(UiArt.Emblem(), 6, "le-passport__emblem"));
            Ui.Text("PASSPORT", "le-passport__cover-title", cover);
            Ui.Text($"{doc.Data.Pages?.ToString() ?? "??"} PAGES", "le-passport__pages", cover);
            Ui.Text($"Cover: {Labels.Of(doc.BookletCover)}", "le-passport__meta", cover);

            var page = new DataPageView(S) { Data = doc.Data, Smudged = doc.Smudged };
            page.AddToClassList("le-datapage--book");
            book.Add(page);
            page.Refresh();

            var order = S.Orders.Get(doc.OrderId);
            string who = order != null ? $"Order #{order.Id} for {S.Customer(order.CustomerId).DisplayName}" : $"Order #{doc.OrderId}";
            Ui.Text(who + $"  -  bound {Calendar.Relative(doc.BoundAt.Day, S.Day).ToLowerInvariant()} {doc.BoundAt.Clock}", "le-passport__who", Root).At(260, 890, 1400, 30);

            var row = Ui.El("le-row le-row--center", Root).At(260, 940, 1400, 80);
            row.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            Ui.Btn("SHRED IT", () => Game.Open(new ConfirmView(Game, "Shred this booklet?", "The booklet and the page are gone. You'll have to print and bind again.", "SHRED", () =>
            {
                S.ShredDocument(_doc.Id);
                Close();
            })), "le-btn--danger", row);
            Ui.Btn("CLOSE", Close, "le-btn--ghost", row);
            var counter = S.CounterVisit;
            if (counter != null && counter.Order != null && !counter.Moving)
            {
                Ui.Btn($"HAND TO {counter.Customer.FirstName.ToUpperInvariant()}", () =>
                {
                    if (!S.GiveDocument(counter.Id, _doc.Id)) Game.Toasts.Show("They're not ready to take it.", "bad");
                    Close();
                }, "le-btn--primary", row);
            }
        }
    }
}
