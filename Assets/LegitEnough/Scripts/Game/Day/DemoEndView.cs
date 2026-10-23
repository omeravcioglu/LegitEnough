using System.Collections.Generic;
using System.Globalization;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// The end of the demo, after the Day-2 report: thanks, what the player's two days came to,
    /// what the full game adds, and the way to the store page. The night shop rains on behind it.
    /// It stays until the player picks something (ESC does nothing).
    /// </summary>
    public sealed class DemoEndView : Overlay
    {
        readonly Label _wishNote;

        public DemoEndView(GameRoot game) : base(game, "le-demoend")
        {
            Root.name = "demo-end";
            var center = Ui.El("le-center", Root);
            var card = Ui.El("le-demoend__card", center);

            var head = Ui.El("le-row le-demoend__head", card);
            var logo = UiArt.Logo();
            var tag = DemoArt.DemoTag();
            var stamp = new PixelBuffer(logo.W, Mathf.Max(logo.H, DemoArt.DemoTagY + tag.H));
            stamp.Blit(logo, 0, 0);
            stamp.Blit(tag, DemoArt.DemoTagX, DemoArt.DemoTagY);
            head.Add(UiArt.Image(stamp, 4, "le-demoend__logo"));
            var words = Ui.El("le-col le-demoend__words", head);
            Ui.Text("THANKS FOR PLAYING", "le-demoend__title", words);
            Ui.Text("That's the demo: two days behind the counter. In the full game the shop stays open, " +
                    "the borders keep changing and word of your work keeps spreading.", "le-demoend__lede", words);

            var cols = Ui.El("le-row le-demoend__cols", card);
            Stats(Ui.El("le-demoend__stats", cols));
            FullGame(Ui.El("le-col le-demoend__full", cols));

            var row = Ui.El("le-row le-row--center le-demoend__buttons", card);
            Ui.Btn("WISHLIST ON STEAM", Wishlist, "le-btn--primary le-btn--big", row).name = "demoend-wishlist";
            Ui.Btn("PLAY AGAIN", PlayAgain, "le-btn--big", row).name = "demoend-again";
            Ui.Btn("QUIT", game.QuitGame, "le-btn--big le-btn--ghost", row).name = "demoend-quit";
            _wishNote = Ui.Text("", "le-demoend__note", card);
            Ui.Text("McHunter Studio   ·   LEGIT ENOUGH   ·   v" + Application.version + " DEMO", "le-demoend__foot", card);
        }

        /// <summary>The two days in numbers, on a ledger page like the day reports.</summary>
        void Stats(VisualElement page)
        {
            Ui.Text("YOUR TWO DAYS", "le-ledger__title", page);
            int customers = 0, jobs = 0;
            foreach (var r in S.Reports)
            {
                customers += r.Customers;
                jobs += r.JobsCompleted;
            }
            Line(page, "Customers", customers.ToString(CultureInfo.InvariantCulture));
            Line(page, "Jobs done", jobs.ToString(CultureInfo.InvariantCulture));
            Line(page, "Money taken", Labels.Money(S.Ledger.LifetimeRevenue));
            Line(page, "Cash in the drawer", Labels.Money(S.Ledger.Cash), S.Ledger.Cash >= Balance.StartingCash ? "le-ledger__row--good" : "le-ledger__row--bad");
            Line(page, "Reputation", $"{Mathf.RoundToInt(S.Standing.Reputation)}  ({StreetName.Of(S.Standing.Tier)})");
            Later(page);
        }

        /// <summary>
        /// The delayed consequences: what the clients who already wrote back said, and who hasn't
        /// written yet (their stories carry on in the full game).
        /// </summary>
        void Later(VisualElement page)
        {
            var heard = new List<Message>();
            foreach (var m in S.Inbox.Messages)
                if (m.Kind == MessageKind.Outcome && m.OrderId.HasValue) heard.Insert(0, m);
            var waiting = new List<string>();
            foreach (var po in S.PendingOutcomes)
            {
                string name = StreetName.Person(S.Customer(po.CustomerId).FirstName);
                if (!po.Applied && !waiting.Contains(name)) waiting.Add(name);
            }
            if (heard.Count == 0 && waiting.Count == 0) return;

            var block = Ui.El("le-col le-demoend__later", page);
            Ui.Text("A FEW DAYS LATER...", "le-ledger__title", block);
            foreach (var m in heard)
            {
                int orderId = m.OrderId.Value;
                var po = S.PendingOutcomes.Find(p => p.OrderId == orderId);
                if (po == null) continue;
                string name = StreetName.Person(S.Customer(po.CustomerId).FirstName);
                string verdict = po.Kind switch
                {
                    OutcomeKind.Success => name + " made it: ",
                    OutcomeKind.Failed => name + " was caught: ",
                    _ => "About " + name + ": ",
                };
                Ui.Text(verdict + "\"" + m.Body + "\"", "le-demoend__word le-demoend__word--" + (po.Kind == OutcomeKind.Success ? "good" : "bad"), block);
            }
            if (waiting.Count > 0)
                Ui.Text(Names(waiting) + (waiting.Count == 1 ? " hasn't written yet. The story continues in the full game."
                                                            : " haven't written yet. Their stories continue in the full game."),
                    "le-demoend__wait", block);
        }

        static string Names(List<string> names) => names.Count == 1 ? names[0]
            : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];

        static void Line(VisualElement page, string label, string value, string extra = "")
        {
            var row = Ui.El("le-ledger__row " + extra, page);
            Ui.Text(label, "le-ledger__label", row);
            Ui.Text(value, "le-ledger__value", row);
        }

        /// <summary>What waits in the full game, with the five countries the demo keeps locked.</summary>
        void FullGame(VisualElement col)
        {
            Ui.Text("IN THE FULL GAME", "le-h2 le-demoend__h", col);
            foreach (var line in new[]
            {
                "All 10 countries, each with its own rules and tricks",
                "Visas, on top of passports",
                "Top-tier cameras, printers, computers and studios",
                "More stories, regulars and trouble at the door",
                "No two-day limit: keep the shop going as long as it lasts",
            })
                Ui.Text("▸  " + line, "le-demoend__item", col);

            var flags = Ui.El("le-row le-demoend__flags", col);
            var names = new List<string>();
            foreach (var c in S.CountryList)
            {
                if (!c.FullGameOnly) continue;
                flags.Add(UiArt.Image(DemoArt.LockedFlag(c.Spec.Id), 4, "le-demoend__flag"));
                names.Add(c.Spec.Name);
            }
            if (names.Count > 0) Ui.Text("Waiting for you: " + Names(names) + ".", "le-demoend__caption", col);
            if (Game.Achievements?.Book != null) Ui.Text(AchievementLine(Game.Achievements.Book), "le-demoend__caption", col).name = "demoend-achievements";
            flags.Add(UiArt.Image(DemoArt.FullGameBadge(), 3, "le-demoend__badge"));
        }

        /// <summary>All demo runs on this machine count; banked ones are granted when the full game starts.</summary>
        static string AchievementLine(AchievementBook book)
        {
            string line = $"You unlocked {book.CountUnlocked(true)} of {AchievementCatalog.DemoCount} demo achievements.";
            int banked = book.CountBanked();
            if (banked > 0) line += $" {banked} more {(banked == 1 ? "is" : "are")} waiting in the full game.";
            return line;
        }

        void Wishlist()
        {
            StoreLinks.OpenWishlist();
            _wishNote.text = "The store page opens in your browser. Thank you!";
        }

        void PlayAgain()
        {
            Game.NewGame((ulong)System.DateTime.Now.Ticks);
            Game.Open(new TitleView(Game));
        }

        public override bool PausesGame => true;

        public override bool OnEscape() => true;

        public override void OnOpened()
        {
            Game.Shop.Renderer.TitleMood = true;
            Sfx.Play(SfxId.Stamp, 0.8f);
        }

        public override void OnClosed() => Game.Shop.Renderer.TitleMood = false;
    }
}
