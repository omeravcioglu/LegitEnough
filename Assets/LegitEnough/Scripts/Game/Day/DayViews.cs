using System.Globalization;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;
using Calendar = LegitEnough.Sim.Calendar;

namespace LegitEnough.Game
{
    /// <summary>Morning card: date, overnight mail, today's deadlines and the headline (design doc §44).</summary>
    public sealed class DayStartView : Overlay
    {
        public DayStartView(GameRoot game) : base(game, "le-overlay--black")
        {
            var card = Ui.El("le-daycard", Root).At(460, 120, 1000, 840);
            card.Add(UiArt.Image(UiArt.Logo(), 3, "le-daycard__logo"));
            Ui.Text($"DAY {S.Day}", "le-daycard__day", card);
            Ui.Text(Calendar.LongDate(S.Day), "le-daycard__date", card);

            var list = Ui.El("le-daycard__list", card);
            int unread = S.Inbox.Unread;
            Ui.Text(unread > 0 ? $"✉  {unread} unread message{(unread > 1 ? "s" : "")} on the computer" : "✉  No new messages.", "le-daycard__item", list);
            int dueToday = 0, pickups = 0;
            foreach (var o in S.Orders.Open())
            {
                if (o.Deadline.Day <= S.Day) dueToday++;
                if (!o.WaitsInShop && o.Deadline.Day == S.Day) pickups++;
            }
            if (dueToday > 0) Ui.Text($"⏰  {dueToday} order{(dueToday > 1 ? "s" : "")} due today" + (pickups > 0 ? $" ({pickups} pickup{(pickups > 1 ? "s" : "")})" : ""), "le-daycard__item", list);
            foreach (var n in S.NewsFor(S.Day))
                if (n.Effect != NewsEffect.None) Ui.Text("▸  " + n.Headline, "le-daycard__item le-daycard__item--news", list);
            Ui.Text($"Cash: {Labels.Money(S.Ledger.Cash)}", "le-daycard__item", list);
            if (S.Day == 1)
                Ui.Text("A note on the counter says: \"Read the handbook. Flip the sign when you're ready. - R.\"", "le-daycard__note", card);

            Ui.Btn("START THE DAY", Close, "le-btn--primary le-btn--big", card);
        }

        public override bool PausesGame => true;
    }

    /// <summary>End-of-day report (design doc §44).</summary>
    public sealed class DayReportView : Overlay
    {
        public DayReportView(GameRoot game) : base(game, "le-overlay--black")
        {
            var r = S.LastReport;
            var card = Ui.El("le-ledger", Root).At(460, 80, 1000, 920);
            Ui.Text($"DAY {r.Day}  -  {Calendar.LongDate(r.Day)}", "le-ledger__title", card);

            Line(card, "Revenue", Labels.Money(r.Revenue));
            foreach (var kv in r.Lines)
                if (kv.Value < 0) Line(card, "  " + Pretty(kv.Key), Labels.Money(kv.Value), "le-ledger__row--minor");
            Line(card, "Expenses", Labels.Money(-r.Expenses));
            Line(card, "Profit", Labels.Money(r.Profit), r.Profit >= 0 ? "le-ledger__row--good" : "le-ledger__row--bad");
            Ui.Deco("le-ledger__rule", card);
            Line(card, "Customers", r.Customers.ToString(CultureInfo.InvariantCulture));
            Line(card, "Jobs accepted", r.JobsAccepted.ToString(CultureInfo.InvariantCulture));
            Line(card, "Jobs completed", r.JobsCompleted.ToString(CultureInfo.InvariantCulture));
            Line(card, "Orders remaining", r.OrdersOpen.ToString(CultureInfo.InvariantCulture));
            Ui.Deco("le-ledger__rule", card);
            float dRep = r.ReputationEnd - r.ReputationStart;
            float dHeat = r.HeatEnd - r.HeatStart;
            Line(card, "Reputation", $"{Mathf.RoundToInt(r.ReputationEnd)} {Arrow(dRep)}   ({Standing.Name(S.Standing.Tier)})");
            Line(card, "Heat", $"{Mathf.RoundToInt(r.HeatEnd)} {Arrow(dHeat)}   ({Standing.Name(S.Standing.HeatLevel)})");
            Line(card, "Cash", Labels.Money(r.CashEnd), r.CashEnd >= 0 ? "" : "le-ledger__row--bad");
            if (r.CashEnd < 0) Ui.Text("You're in debt. The landlord has started knocking.", "le-ledger__warn", card);

            var row = Ui.El("le-row le-row--center", card);
            Ui.Btn("NEXT DAY", () =>
            {
                Close();
                S.StartNextDay();
            }, "le-btn--primary le-btn--big", row);
        }

        public override bool PausesGame => true;

        public override bool OnEscape() => true;

        static string Arrow(float d) => d > 0.5f ? "↑" : d < -0.5f ? "↓" : "-";

        static string Pretty(string kind) => kind switch
        {
            "Rent" => "Rent",
            "Utilities" => "Electricity & water",
            "Supplies" => "Paper, ink, booklets",
            "Equipment" => "Equipment",
            "Handbook" => "Handbook",
            _ => kind,
        };

        static void Line(VisualElement card, string label, string value, string extra = "")
        {
            var row = Ui.El("le-ledger__row " + extra, card);
            Ui.Text(label, "le-ledger__label", row);
            Ui.Text(value, "le-ledger__value", row);
        }
    }

    public sealed class PauseView : Overlay
    {
        public PauseView(GameRoot game) : base(game, "le-overlay--dim")
        {
            var card = Ui.El("le-card le-pause", Root).At(660, 190, 600, 680);
            card.Add(UiArt.Image(UiArt.Mark(), 4, "le-pause__mark"));
            Ui.Text("PAUSED", "le-h1", card);
            Ui.Btn("RESUME", Close, "le-btn--primary le-btn--big", card);
            Ui.Btn("HANDBOOK", () =>
            {
                Close();
                Game.ToggleHandbook();
            }, "le-btn--big", card);
            Ui.Btn("NEW GAME", () => Game.Open(new ConfirmView(Game, "Start over?", "This campaign will be lost.", "NEW GAME", () => Game.NewGame((ulong)System.DateTime.Now.Ticks))), "le-btn--big le-btn--ghost", card);
            Ui.Btn("QUIT", Game.QuitGame, "le-btn--big le-btn--ghost", card);
            Ui.Text("F1 hints on/off   ·   H handbook   ·   SPACE shutter   ·   ESC back", "le-muted", card);
            Ui.Text("dev: F5 spawn a walk-in   ·   F6 +30 minutes", "le-muted", card);
        }

        public override bool PausesGame => true;
    }
}
