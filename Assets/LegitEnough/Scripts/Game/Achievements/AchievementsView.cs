using System.Collections.Generic;
using System.Globalization;
using LegitEnough.Sim;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// The ACHIEVEMENTS screen, from the title or the pause menu: every badge in a 6-column grid (the
    /// demo's twelve first), the picked one large on the right with how to earn it. Secrets stay "???"
    /// with their teaser until earned. In the demo the rest wait under "IN THE FULL GAME" with a padlock
    /// and the store link. Like SETTINGS it hides the card that opened it; ESC or BACK returns.
    /// </summary>
    public sealed class AchievementsView : Overlay
    {
        const int Columns = 6;

        sealed class Cell
        {
            public AchievementDef Def;
            public Button Button;
            public VisualElement Bar, Fill;
            public VisualElement New;
        }

        readonly Overlay _under;
        readonly AchievementService _svc;
        readonly bool _demo;
        readonly List<Cell> _cells = new List<Cell>();
        readonly VisualElement _detail;
        readonly VisualElement _detailBadge;
        readonly Label _name, _desc, _progressText, _status;
        readonly VisualElement _progress, _progressFill, _fullGame;
        int _pick = -1;

        public AchievementsView(GameRoot game) : base(game, "le-overlay--dim")
        {
            _under = game.Top; // Open() adds this view after the constructor runs
            Root.name = "ach-view";
            _svc = game.Achievements;
            var book = _svc?.Book;
            _demo = book != null ? book.Demo : S.IsDemo;

            var card = Ui.El("le-card le-ach", Root).At(160, 60, 1600, 960);

            var head = Ui.El("le-row le-ach__header", card);
            Ui.Text("ACHIEVEMENTS", "le-h1 le-ach__title", head);
            int total = AchievementCatalog.All.Count;
            int got = book != null ? book.CountUnlocked(_demo) : 0;
            Ui.Text(_demo ? $"DEMO {got} / {AchievementCatalog.DemoCount}" : $"{got} / {total}", "le-ach__count", head).name = "ach-count";
            if (_demo)
            {
                int banked = book != null ? book.CountBanked() : 0;
                string more = $"{total - AchievementCatalog.DemoCount} more in the full game";
                if (banked > 0) more += $"  ·  {banked} earned for it";
                Ui.Text(more, "le-muted le-ach__more", head).name = "ach-more";
            }

            var body = Ui.El("le-row le-ach__body", card);
            var grid = Ui.El("le-ach__grid", body);
            grid.name = "ach-grid";
            int i = 0;
            foreach (var d in AchievementCatalog.All)
            {
                if (_demo && i == AchievementCatalog.DemoCount)
                    Ui.Text("IN THE FULL GAME", "le-ach__divider", grid).name = "ach-divider";
                _cells.Add(MakeCell(grid, d, book, i));
                i++;
            }

            _detail = Ui.El("le-col le-ach__detail", body);
            _detail.name = "ach-detail";
            var top = Ui.El("le-row le-ach__detail-top", _detail);
            _detailBadge = Ui.El("le-ach__detail-badge", top);
            var words = Ui.El("le-col le-ach__detail-words", top);
            _status = Ui.Text("", "le-ach__status", words);
            _status.name = "ach-detail-status";
            _name = Ui.Text("", "le-ach__name", words);
            _name.name = "ach-detail-name";
            _desc = Ui.Text("", "le-ach__desc", _detail);
            _desc.name = "ach-detail-desc";
            _progress = Ui.El("le-row le-ach__progress", _detail);
            _progressText = Ui.Text("", "le-ach__progress-text", _progress);
            _progressText.name = "ach-detail-progress";
            var bar = Ui.El("le-ach__bar le-ach__bar--big", _progress);
            _progressFill = Ui.El("le-ach__bar-fill", bar);
            _fullGame = Ui.El("le-row le-ach__fullgame", _detail);
            _fullGame.Add(UiArt.Image(DemoArt.FullGameBadge(), 3, "le-ach__fullgame-badge"));
            Ui.Btn("WISHLIST ON STEAM", StoreLinks.OpenWishlist, "le-btn--primary", _fullGame).name = "ach-detail-wishlist";

            Ui.El("le-ach__spacer", _detail);
            var foot = Ui.El("le-row le-ach__foot", _detail);
            Ui.Btn("BACK", Close, "le-btn--big", foot).name = "ach-back";
            Ui.Text("◀ ▶ ▲ ▼ pick   ·   ESC back", "le-muted le-ach__keys", foot);

            Select(0);
        }

        Cell MakeCell(VisualElement grid, AchievementDef d, AchievementBook book, int index)
        {
            var c = new Cell { Def = d };
            c.Button = Ui.Btn("", () => Select(index), "le-ach__cell", grid);
            c.Button.name = "ach-cell-" + d.Id;
            bool unlocked = book != null && book.Unlocked(d);
            bool banked = book != null && book.Banked(d);
            bool fullOnly = FullGameOnly(d);
            string state = unlocked ? "le-ach__cell--unlocked"
                : banked ? "le-ach__cell--banked"
                : fullOnly ? "le-ach__cell--fullgame"
                : d.Hidden ? "le-ach__cell--hidden" : "le-ach__cell--locked";
            c.Button.AddToClassList(state);
            c.Button.Add(UiArt.Image(AchievementArt.ForCell(d, unlocked, banked, fullOnly), 3, "le-ach__cell-badge"));
            if (d.IsProgress && !fullOnly)
            {
                c.Bar = Ui.Deco("le-ach__bar", c.Button);
                c.Bar.name = "ach-cell-" + d.Id + "-bar";
                c.Fill = Ui.Deco("le-ach__bar-fill", c.Bar);
                c.Fill.style.width = Length.Percent(Percent(d, book));
            }
            if ((unlocked || banked) && _svc != null && _svc.IsNew(d))
            {
                c.Button.AddToClassList("le-ach__cell--new");
                c.New = UiArt.Image(AchievementArt.NewTag(), 2, "le-ach__new");
                c.New.name = "ach-cell-" + d.Id + "-new";
                c.Button.Add(c.New);
            }
            return c;
        }

        /// <summary>In the demo, everything past the twelve waits in the full game.</summary>
        bool FullGameOnly(AchievementDef d) => _demo && !d.Demo;

        static float Percent(AchievementDef d, AchievementBook book)
        {
            if (book == null || d.Target <= 0) return 0f;
            if (book.Unlocked(d)) return 100f;
            return UnityEngine.Mathf.Clamp01(book.Value(d) / (float)d.Target) * 100f;
        }

        void Select(int index)
        {
            if (index < 0 || index >= _cells.Count) return;
            if (_pick >= 0) _cells[_pick].Button.RemoveFromClassList("le-ach__cell--selected");
            _pick = index;
            var c = _cells[index];
            c.Button.AddToClassList("le-ach__cell--selected");
            var d = c.Def;
            var book = _svc?.Book;
            bool unlocked = book != null && book.Unlocked(d);
            bool banked = book != null && book.Banked(d);
            bool fullOnly = FullGameOnly(d);
            bool secret = d.Hidden && !unlocked && !banked;

            if (c.New != null)
            {
                _svc?.MarkSeen(d);
                c.New.RemoveFromHierarchy();
                c.New = null;
                c.Button.RemoveFromClassList("le-ach__cell--new");
            }

            _detailBadge.Clear();
            _detailBadge.Add(UiArt.Image(AchievementArt.ForCell(d, unlocked, banked, fullOnly), 6, "le-ach__big-badge"));
            _name.text = secret ? "???" : d.Name;
            _desc.text = secret ? d.Teaser : d.Description;

            string status, kind;
            if (unlocked) { status = "UNLOCKED"; kind = "good"; }
            else if (banked) { status = "EARNED FOR THE FULL GAME"; kind = "good"; }
            else if (fullOnly) { status = "FULL GAME"; kind = "full"; }
            else if (secret) { status = "SECRET"; kind = "secret"; }
            else { status = "LOCKED"; kind = "locked"; }
            _status.text = status;
            foreach (var k in new[] { "good", "full", "secret", "locked" })
                _status.EnableInClassList("le-ach__status--" + k, k == kind);

            bool showProgress = d.IsProgress && !fullOnly;
            _progress.Show(showProgress);
            if (showProgress)
            {
                int target = d.Target;
                int value = unlocked ? target : UnityEngine.Mathf.Min(book != null ? book.Value(d) : 0, target);
                _progressText.text = value.ToString("N0", CultureInfo.InvariantCulture) + " / " + target.ToString("N0", CultureInfo.InvariantCulture);
                _progressFill.style.width = Length.Percent(Percent(d, book));
            }
            _fullGame.Show(fullOnly || banked);
        }

        public override void OnOpened()
        {
            if (_under != null) _under.Root.visible = false;
        }

        public override void OnClosed()
        {
            if (_under != null) _under.Root.visible = true;
        }

        public override void Tick(float dt)
        {
            if (Game.Top != this) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            int step = 0;
            if (kb.rightArrowKey.wasPressedThisFrame) step = 1;
            else if (kb.leftArrowKey.wasPressedThisFrame) step = -1;
            else if (kb.downArrowKey.wasPressedThisFrame) step = Columns;
            else if (kb.upArrowKey.wasPressedThisFrame) step = -Columns;
            if (step == 0) return;
            int next = _pick + step;
            if (next < 0 || next >= _cells.Count) return;
            Select(next);
            Sfx.Play(SfxId.Click, 0.6f);
        }

        public override bool PausesGame => true;
    }
}
