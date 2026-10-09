using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// The main menu is the empty shop itself (visual direction: computer glowing, rain outside,
    /// the printer quiet, the desk lamp on), with the stamp logo and a short menu on top.
    /// </summary>
    public sealed class TitleView : Overlay
    {
        readonly Button _hints;
        VisualElement _credits;

        public TitleView(GameRoot game) : base(game, "le-overlay--title")
        {
            game.Shop.Renderer.TitleMood = true;
            var panel = Ui.El("le-title", Root).At(72, 64, 520, -1);
            panel.Add(UiArt.Image(UiArt.Logo(), 6, "le-title__logo"));
            var menu = Ui.El("le-title__menu", panel);
            Ui.Btn("▶ NEW BUSINESS", Close, "le-btn--menu", menu);
            _hints = Ui.Btn(HintsLabel(), ToggleHints, "le-btn--menu", menu);
            Ui.Btn("CREDITS", ToggleCredits, "le-btn--menu", menu);
            Ui.Btn("QUIT", game.QuitGame, "le-btn--menu", menu);
            var foot = Ui.El("le-title__foot", panel);
            Ui.Text("No saves yet", "", foot);
            Ui.Text("v0.2 pixel", "", foot);
        }

        string HintsLabel() => Game.ShowHints ? "HINTS: ON" : "HINTS: OFF";

        void ToggleHints()
        {
            Game.ShowHints = !Game.ShowHints;
            _hints.text = HintsLabel();
        }

        void ToggleCredits()
        {
            if (_credits != null)
            {
                _credits.RemoveFromHierarchy();
                _credits = null;
                return;
            }
            _credits = Ui.El("le-card le-credits", Root).At(640, 64, 640, -1);
            Ui.Text("CREDITS", "le-h2", _credits);
            Ui.Text("LEGIT ENOUGH, a game about paperwork that is legit enough.", "le-text", _credits);
            Ui.Text("Pixel art, characters and both fonts (Legit Pixel, Legit Stamp) are drawn in code for this game.", "le-text", _credits);
            Ui.Text("Made with Unity.", "le-muted", _credits);
        }

        public override bool PausesGame => true;

        public override bool OnEscape() => true;

        public override void OnClosed() => Game.Shop.Renderer.TitleMood = false;
    }
}
