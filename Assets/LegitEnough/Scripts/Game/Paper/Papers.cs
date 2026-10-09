using System;
using LegitEnough.Sim;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// The customer's handwritten client sheet (design doc §4 "they place a folder on your
    /// desk"). Formats are the customer's own; converting them is the player's job.
    /// </summary>
    public sealed class SlipView : Overlay
    {
        public SlipView(GameRoot game, int orderId) : base(game, "le-overlay--dim")
        {
            var o = S.Orders.Get(orderId);
            var c = S.Customer(o.CustomerId);
            var paper = Ui.El("le-slip", Root).At(560, 90, 800, 860);
            paper.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            Root.RegisterCallback<ClickEvent>(_ => Close());
            BuildSheet(paper, S, o, c);

            var buttons = Ui.El("le-row le-slip__buttons", paper);
            Ui.Btn("OPEN IN EDITOR", () =>
            {
                Close();
                Game.OpenComputer(orderId);
            }, "le-btn--primary", buttons);
            Ui.Btn("CLOSE", Close, "le-btn--ghost", buttons);
        }

        /// <summary>Shared by this overlay and the copy pinned beside the monitor.</summary>
        public static void BuildSheet(VisualElement paper, GameSession s, Order o, CustomerProfile c)
        {
            Ui.Text("CLIENT SHEET", "le-slip__title", paper);
            Ui.Text($"Order #{o.Id}", "le-slip__order", paper);
            var lines = Ui.El("le-slip__lines", paper);
            foreach (var line in c.Slip)
            {
                var row = Ui.El("le-slip__row", lines);
                Ui.Text(line.Label + ":", "le-slip__label", row);
                Ui.Text(line.Value, "le-slip__hand", row);
            }
            Ui.Text("(no ID. no questions.)", "le-slip__aside", lines);

            var job = Ui.El("le-slip__job", paper);
            var country = s.Country(o.CountryId).Spec;
            Ui.Text($"{country.Name.ToUpperInvariant()}  -  {Labels.Of(o.DocType)}", "le-slip__jobtitle", job);
            Ui.Text($"Agreed: {Labels.Money(o.Price)}" + (o.Urgent ? "   URGENT" : ""), "le-slip__jobline", job);
            string when = o.WaitsInShop
                ? $"Due: {Calendar.Relative(o.Deadline.Day, s.Day)} {o.Deadline.Clock} (waiting in the shop)"
                : $"Pickup: {Calendar.Relative(o.Deadline.Day, s.Day)} at {o.Deadline.Clock}";
            Ui.Text(when, "le-slip__jobline", job);
        }
    }

    /// <summary>Simple yes/no card.</summary>
    public sealed class ConfirmView : Overlay
    {
        public ConfirmView(GameRoot game, string title, string text, string confirm, Action onConfirm) : base(game, "le-overlay--dim")
        {
            var card = Ui.El("le-card le-confirm", Root).At(610, 330, 700, 360);
            Ui.Text(title, "le-h2", card);
            Ui.Text(text, "le-text", card);
            var row = Ui.El("le-row le-row--end", card);
            Ui.Btn("CANCEL", Close, "le-btn--ghost", row);
            Ui.Btn(confirm, () =>
            {
                Close();
                onConfirm?.Invoke();
            }, "le-btn--danger", row);
        }

        public override bool PausesGame => true;
    }

    /// <summary>What to do with a page from the printer tray.</summary>
    public sealed class PrintMenu : Overlay
    {
        public PrintMenu(GameRoot game, int pageId) : base(game, "le-overlay--dim")
        {
            var page = S.Printer.Tray.Find(p => p.Id == pageId);
            var card = Ui.El("le-card le-printmenu", Root).At(420, 120, 1080, 820);
            if (page == null)
            {
                Ui.Text("The tray is empty.", "le-h2", card);
                Ui.Btn("CLOSE", Close, "le-btn--ghost", card);
                return;
            }
            Ui.Text($"PRINTED DATA PAGE  -  ORDER #{page.OrderId}", "le-h2", card);
            Ui.Text("Check it before it goes into a booklet. A booklet costs " + Labels.Money(Balance.BlankBookletCost) + ".", "le-muted", card);
            var preview = new DataPageView(S) { Data = page.Data, Smudged = page.Smudged };
            preview.AddToClassList("le-datapage--large");
            card.Add(preview);
            preview.Refresh();

            var row = Ui.El("le-row le-row--end", card);
            Ui.Btn("SHRED", () =>
            {
                S.ShredPage(pageId);
                Close();
            }, "le-btn--danger", row);
            Ui.Btn("LEAVE IT", Close, "le-btn--ghost", row);
            Ui.Btn("BIND INTO BOOKLET", () =>
            {
                S.BindPage(pageId);
                Close();
            }, "le-btn--primary", row);
        }
    }
}
