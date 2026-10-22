using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// The main menu is the empty shop itself (visual direction: computer glowing, rain outside,
    /// the printer quiet, the desk lamp on), with the stamp logo and a short menu on top.
    /// The demo build stamps DEMO under the logo and adds settings and the store link.
    /// </summary>
    public sealed class TitleView : Overlay
    {
        readonly Button _hints;
        VisualElement _credits;

        public TitleView(GameRoot game) : base(game, "le-overlay--title")
        {
            game.Shop.Renderer.TitleMood = true;
            bool demo = S.IsDemo;
            var panel = Ui.El("le-title" + (demo ? " le-title--demo" : ""), Root).At(72, 64, 520, -1);
            // The demo logo is taller (DEMO stamp under ENOUGH): x5 keeps the panel above the CRT.
            panel.Add(UiArt.Image(demo ? DemoLogo() : UiArt.Logo(), demo ? 5 : 6, "le-title__logo"));
            var menu = Ui.El("le-title__menu", panel);
            if (demo)
            {
                Ui.Btn("▶ PLAY DEMO", Close, "le-btn--menu", menu).name = "title-play";
                Ui.Btn("SETTINGS", () => Game.Open(new SettingsView(Game)), "le-btn--menu", menu).name = "title-settings";
                Ui.Btn("CREDITS", ToggleCredits, "le-btn--menu", menu).name = "title-credits";
                Ui.Btn("WISHLIST ON STEAM", StoreLinks.OpenWishlist, "le-btn--menu le-btn--wish", menu).name = "title-wishlist";
                Ui.Btn("QUIT", game.QuitGame, "le-btn--menu", menu).name = "title-quit";
                Ui.Text("About 15 minutes. No saves in the demo.", "le-title__note", panel);
                var foot = Ui.El("le-title__foot", panel);
                Ui.Text("McHunter Studio", "", foot);
                Ui.Text("v" + Application.version + " · DEMO", "", foot).name = "title-version";
            }
            else
            {
                Ui.Btn("▶ NEW BUSINESS", Close, "le-btn--menu", menu).name = "title-play";
                _hints = Ui.Btn(HintsLabel(), ToggleHints, "le-btn--menu", menu);
                Ui.Btn("CREDITS", ToggleCredits, "le-btn--menu", menu).name = "title-credits";
                Ui.Btn("QUIT", game.QuitGame, "le-btn--menu", menu).name = "title-quit";
                var foot = Ui.El("le-title__foot", panel);
                Ui.Text("No saves yet", "", foot);
                Ui.Text("v0.2 pixel", "", foot);
            }
        }

        /// <summary>The stamp logo with the red DEMO stamp tucked under ENOUGH (70x49).</summary>
        static PixelBuffer DemoLogo()
        {
            var logo = UiArt.Logo();
            var tag = DemoArt.DemoTag();
            var b = new PixelBuffer(logo.W, Mathf.Max(logo.H, DemoArt.DemoTagY + tag.H));
            b.Blit(logo, 0, 0);
            b.Blit(tag, DemoArt.DemoTagX, DemoArt.DemoTagY);
            return b;
        }

        string HintsLabel() => Game.ShowHints ? "HINTS: ON" : "HINTS: OFF";

        void ToggleHints()
        {
            GameSettings.SetHints(Game, !Game.ShowHints);
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
            _credits.name = "title-credits-card";
            Ui.Text("CREDITS", "le-h2", _credits);
            _credits.Add(UiArt.Image(DemoArt.StudioLogo(), 3, "le-credits__studio"));
            Ui.Text("LEGIT ENOUGH: a game about paperwork that is legit enough. Made by McHunter Studio.", "le-text", _credits);
            Ui.Text("We drew every pixel, every customer and both fonts (Legit Pixel, Legit Stamp) in code, and synthesized every sound.", "le-text", _credits);
            if (S.IsDemo) Ui.Text("Thanks for trying the demo. Wishlist us on Steam to hear when the shop opens for good.", "le-text", _credits);
            Ui.Text("Made with Unity.", "le-muted", _credits);
            Ui.Btn("CLOSE", ToggleCredits, "le-btn--ghost le-credits__close", _credits);
        }

        public override bool PausesGame => true;

        /// <summary>ESC closes the credits card; the title itself stays.</summary>
        public override bool OnEscape()
        {
            if (_credits != null) ToggleCredits();
            return true;
        }

        public override void OnClosed() => Game.Shop.Renderer.TitleMood = false;
    }
}
