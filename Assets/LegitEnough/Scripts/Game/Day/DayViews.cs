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
            // The demo's Day-2 morning carries word from yesterday's client: a little more room.
            var word = S.IsDemo ? OvernightWord() : new System.Collections.Generic.List<Message>();
            var card = Ui.El("le-daycard" + (word.Count > 0 ? " le-daycard--word" : ""), Root).At(460, word.Count > 0 ? 64 : 120, 1000, word.Count > 0 ? 960 : 840);
            card.Add(UiArt.Image(UiArt.Logo(), 3, "le-daycard__logo"));
            Ui.Text($"DAY {S.Day}", "le-daycard__day", card);
            Ui.Text(Calendar.LongDate(S.Day), "le-daycard__date", card);
            if (S.IsDemo)
                Ui.Text(S.Day >= S.Rules.LastDay ? "LAST DAY OF THE DEMO" : $"DEMO  ·  DAY {S.Day} OF {S.Rules.LastDay}", "le-daycard__demo", card);

            var list = Ui.El("le-daycard__list", card);
            int unread = S.Inbox.Unread;
            Ui.Text(unread > 0 ? $"✉  {unread} unread message{(unread > 1 ? "s" : "")} on the computer" : "✉  No new messages.", "le-daycard__item", list);
            foreach (var m in word) Word(list, m);
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
            string note = Note();
            // On the tutorial morning R.'s pointer hangs above the button: leave it room.
            if (note != null) Ui.Text(note, "le-daycard__note" + (S.TutorialActive ? " le-daycard__note--coached" : ""), card);

            Ui.Btn("START THE DAY", Close, "le-btn--primary le-btn--big", card).name = "daycard-start";
        }

        /// <summary>Client mail that came in overnight, oldest first (the demo's delayed payoff: Samir's verdict).</summary>
        System.Collections.Generic.List<Message> OvernightWord()
        {
            var word = new System.Collections.Generic.List<Message>();
            foreach (var m in S.Inbox.Messages)
                if (!m.Read && m.Kind == MessageKind.Outcome && m.OrderId.HasValue && m.ArrivesAt.Day == S.Day)
                    word.Insert(0, m);
            return word;
        }

        /// <summary>"Samir made it. Reputation +17" and his own words under it.</summary>
        void Word(VisualElement list, Message m)
        {
            int orderId = m.OrderId.Value;
            var po = S.PendingOutcomes.Find(p => p.OrderId == orderId);
            if (po == null) return;
            string name = StreetName.Person(S.Customer(po.CustomerId).FirstName);
            bool good = po.Kind == OutcomeKind.Success;
            string head = po.Kind switch
            {
                OutcomeKind.Success => $"✉  {name} made it.",
                OutcomeKind.Failed => $"✉  {name} was caught.",
                _ => $"✉  Nobody has heard from {name}.",
            };
            int rep = Mathf.RoundToInt(OutcomeModel.ReputationDelta(po.Kind, po.Price));
            if (rep != 0) head += $"   Reputation {(rep > 0 ? "+" : "")}{rep}";
            Ui.Text(head, "le-daycard__item le-daycard__item--" + (good ? "good" : "bad"), list);
            Ui.Text("\"" + m.Body + "\"", "le-daycard__item le-daycard__item--quote", list);
        }

        /// <summary>R.'s note on the counter: the full game's first morning, or the demo's two days.</summary>
        string Note()
        {
            if (!S.IsDemo)
                return S.Day == 1 ? "A note on the counter says: \"Read the handbook. Flip the sign when you're ready. - R.\"" : null;
            if (S.Day == 1)
                return Game.ShowHints
                    ? "A note on the counter says: \"First day. Don't panic. I'll stick notes up as you go. Do what they say. - R.\""
                    : "A note on the counter says: \"One customer today. Read the handbook. Flip the sign when you're ready. - R.\"";
            if (S.Day >= S.Rules.LastDay)
                return "A note on the counter says: \"Busier today. Read your mail and the news before you flip the sign. - R.\"";
            return null;
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
            Line(card, "Reputation", $"{Mathf.RoundToInt(r.ReputationEnd)} {Arrow(dRep)}   ({StreetName.Of(S.Standing.Tier)})");
            Line(card, "Heat", $"{Mathf.RoundToInt(r.HeatEnd)} {Arrow(dHeat)}   ({Standing.Name(S.Standing.HeatLevel)})");
            Line(card, "Cash", Labels.Money(r.CashEnd), r.CashEnd >= 0 ? "" : "le-ledger__row--bad");
            if (r.CashEnd < 0) Ui.Text("You're in debt. The landlord has started knocking.", "le-ledger__warn", card);

            var row = Ui.El("le-row le-row--center", card);
            if (S.DemoOver)
            {
                // The demo stops here: no next day, the thank-you screen instead.
                Ui.Btn("FINISH DEMO", () =>
                {
                    Close();
                    Game.Open(new DemoEndView(Game));
                }, "le-btn--primary le-btn--big", row).name = "report-finish";
                return;
            }
            Ui.Btn("NEXT DAY", () =>
            {
                Close();
                S.StartNextDay();
            }, "le-btn--primary le-btn--big", row).name = "report-next";
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

    /// <summary>
    /// The reputation tier as the street puts it. The lowest tier reads as a standing ("New in
    /// town"), not as missing data ("Unknown").
    /// </summary>
    public static class StreetName
    {
        public static string Of(RepTier tier) => tier == RepTier.Unknown ? "New in town" : Standing.Name(tier);

        /// <summary>"SAMIR" → "Samir": client names as they'd sign a letter.</summary>
        public static string Person(string name) => string.IsNullOrEmpty(name) ? name
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
    }

    public sealed class PauseView : Overlay
    {
        public PauseView(GameRoot game) : base(game, "le-overlay--dim")
        {
            var center = Ui.El("le-center", Root);
            var card = Ui.El("le-card le-pause", center);
            card.style.width = 600;
            card.Add(UiArt.Image(UiArt.Mark(), 4, "le-pause__mark"));
            Ui.Text("PAUSED", "le-h1", card);
            Ui.Btn("RESUME", Close, "le-btn--primary le-btn--big", card).name = "pause-resume";
            Ui.Btn("HANDBOOK", () =>
            {
                Close();
                Game.ToggleHandbook();
            }, "le-btn--big", card).name = "pause-handbook";
            Ui.Btn("SETTINGS", () => Game.Open(new SettingsView(Game)), "le-btn--big", card).name = "pause-settings";
            Ui.Btn("ACHIEVEMENTS", () => Game.Open(new AchievementsView(Game)), "le-btn--big", card).name = "pause-achievements";
            bool demo = S.IsDemo;
            Ui.Btn(demo ? "RESTART DEMO" : "NEW GAME", () => Game.Open(new ConfirmView(Game, "Start over?",
                demo ? "Back to the title. This run of the demo is lost." : "This campaign will be lost.",
                demo ? "RESTART" : "NEW GAME", () =>
                {
                    Game.NewGame((ulong)System.DateTime.Now.Ticks);
                    if (demo) Game.Open(new TitleView(Game));
                })), "le-btn--big le-btn--ghost", card).name = "pause-newgame";
            Ui.Btn("QUIT", () => Game.Open(new ConfirmView(Game, "Quit?",
                demo ? "The demo doesn't save. Your shop closes with you." : "Nothing is saved yet. This campaign will be lost.",
                "QUIT", Game.QuitGame)), "le-btn--big le-btn--ghost", card).name = "pause-quit";
            Ui.Text("F1 hints on/off   ·   H handbook", "le-muted le-pause__keys", card);
            Ui.Text("SPACE shutter   ·   ESC back", "le-muted", card);
        }

        public override bool PausesGame => true;
    }
}
