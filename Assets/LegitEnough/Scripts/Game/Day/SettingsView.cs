using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// The player's settings: master volume, music volume, fullscreen and hints. Kept in PlayerPrefs and applied
    /// at boot. Batch runs (smoke test, store shots, trailer) neither read nor write them, so a
    /// player's choices never leak into a harness and a harness never changes them.
    /// </summary>
    public static class GameSettings
    {
        const string VolumeKey = "le.volume";
        const string FullscreenKey = "le.fullscreen";
        const string HintsKey = "le.hints";
        const string MusicKey = "le.music";
        public const int DefaultMusicVolume = 6;

        /// <summary>Master volume in tenths (0-10).</summary>
        public static int Volume { get; private set; } = 10;
        /// <summary>Music volume in tenths (0-10), under the master volume.</summary>
        public static int MusicVolume { get; private set; } = DefaultMusicVolume;
        public static bool Fullscreen { get; private set; } = true;

        static bool Persist => !Application.isBatchMode;

        static FullScreenMode _lastMode = (FullScreenMode)(-1);
        static int _capCheck;

        public static void ApplySaved(GameRoot game)
        {
            Fullscreen = Screen.fullScreen;
            if (!Persist) return;
            Volume = Mathf.Clamp(PlayerPrefs.GetInt(VolumeKey, 10), 0, 10);
            AudioListener.volume = Volume / 10f;
            MusicVolume = Mathf.Clamp(PlayerPrefs.GetInt(MusicKey, DefaultMusicVolume), 0, 10);
            game.ShowHints = PlayerPrefs.GetInt(HintsKey, 1) != 0;
            if (PlayerPrefs.HasKey(FullscreenKey))
            {
                bool full = PlayerPrefs.GetInt(FullscreenKey) != 0;
                if (full != Screen.fullScreen) ApplyFullscreen(full);
                Fullscreen = full;
            }
        }

        public static void SetVolume(int tenths)
        {
            Volume = Mathf.Clamp(tenths, 0, 10);
            AudioListener.volume = Volume / 10f;
            Save(VolumeKey, Volume);
        }

        public static void SetMusicVolume(int tenths)
        {
            MusicVolume = Mathf.Clamp(tenths, 0, 10);
            Save(MusicKey, MusicVolume);
            MusicPlayer.OnVolumeChanged();
        }

        public static void SetFullscreen(bool on)
        {
            Fullscreen = on;
            ApplyFullscreen(on);
            Save(FullscreenKey, on ? 1 : 0);
        }

        public static void SetHints(GameRoot game, bool on)
        {
            game.ShowHints = on;
            Save(HintsKey, on ? 1 : 0);
        }

        /// <summary>
        /// Called every frame by GameRoot. Alt+Enter (or the OS) can switch fullscreen behind the
        /// settings card's back: take the new state, size it the game's way and remember it. Also
        /// re-checks the frame cap now and then, for a window dragged onto another monitor.
        /// </summary>
        public static void Poll()
        {
            if (Application.isEditor || Application.isBatchMode) return;
            if (++_capCheck >= 120)
            {
                _capCheck = 0;
                ApplyFrameCap();
            }
            var mode = Screen.fullScreenMode;
            if (mode == _lastMode) return;
            bool first = (int)_lastMode < 0;
            _lastMode = mode;
            bool full = mode != FullScreenMode.Windowed;
            // The first frame, or our own SetResolution landing: nothing changed behind our back.
            if (first || full == Fullscreen) return;
            Fullscreen = full;
            ApplyFullscreen(full);
            Save(FullscreenKey, full ? 1 : 0);
        }

        /// <summary>
        /// Smooth 60 fps: vSync on a ~60 Hz display, otherwise vSync off and a 60 fps cap (Ultra's
        /// vSync would otherwise run the shop at 144 or 165 fps). Batch runs keep the plain cap.
        /// </summary>
        public static void ApplyFrameCap()
        {
            Application.targetFrameRate = 60;
            if (Application.isBatchMode) return;
            double hz = Screen.currentResolution.refreshRateRatio.value;
            int vsync = hz >= 58.5 && hz <= 61.5 ? 1 : 0;
            if (QualitySettings.vSyncCount != vsync) QualitySettings.vSyncCount = vsync;
        }

        /// <summary>
        /// Fullscreen fills the monitor the window is on. Windowed prefers a whole multiple of the
        /// 1920x1080 stage (crisp pixels and fonts) that fits with room for the title bar and the
        /// taskbar; on a smaller screen it falls back to the largest 16:9 window in 80% of it.
        /// </summary>
        static void ApplyFullscreen(bool on)
        {
            if (Application.isEditor || Application.isBatchMode) return;
            var di = Screen.mainWindowDisplayInfo;
            int sw = di.width, sh = di.height;
            if (sw <= 0 || sh <= 0)
            {
                sw = Display.main.systemWidth;
                sh = Display.main.systemHeight;
            }
            if (on)
            {
                Screen.SetResolution(sw, sh, FullScreenMode.FullScreenWindow);
            }
            else
            {
                int k = Mathf.Min(sw * 9 / 10 / (int)GameRoot.StageWidth, sh * 85 / 100 / (int)GameRoot.StageHeight);
                if (k >= 1)
                {
                    Screen.SetResolution((int)GameRoot.StageWidth * k, (int)GameRoot.StageHeight * k, FullScreenMode.Windowed);
                }
                else
                {
                    int w = Mathf.Min(sw * 4 / 5, sh * 4 / 5 * 16 / 9) & ~1;
                    Screen.SetResolution(w, (w * 9 / 16) & ~1, FullScreenMode.Windowed);
                }
            }
            ApplyFrameCap();
        }

        static void Save(string key, int value)
        {
            if (!Persist) return;
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Settings card, from the title or the pause menu. ESC or BACK returns to it.</summary>
    public sealed class SettingsView : Overlay
    {
        readonly VisualElement[] _pips = new VisualElement[10];
        readonly VisualElement[] _musicPips = new VisualElement[10];
        readonly Label _volume;
        readonly Label _music;
        /// <summary>The rows the arrow keys adjust: 0 = VOLUME, 1 = MUSIC (UP/DOWN picks).</summary>
        readonly Label[] _rowLabels = new Label[2];
        int _row;
        readonly Button _fullscreen;
        readonly Button _hints;
        /// <summary>The card that opened this one (title or pause): hidden meanwhile, so no edges show around it.</summary>
        readonly Overlay _under;
        bool _shownFull;

        public SettingsView(GameRoot game) : base(game, "le-overlay--dim")
        {
            _under = game.Top; // Open() adds this view after the constructor runs
            var center = Ui.El("le-center", Root);
            var card = Ui.El("le-card le-settings", center);
            Ui.Text("SETTINGS", "le-h1 le-settings__title", card);

            var vol = Row(card, "VOLUME", out _rowLabels[0]);
            Ui.Btn("-", () => SetVolume(GameSettings.Volume - 1), "le-btn--small le-btn--icon", vol).name = "settings-volume-down";
            var pips = Ui.El("le-row le-set__pips", vol);
            for (int i = 0; i < _pips.Length; i++)
            {
                int tenths = i + 1;
                var pip = Ui.Btn("", () => SetVolume(GameSettings.Volume == tenths ? tenths - 1 : tenths), "le-set__pip", pips);
                pip.name = "settings-volume-" + tenths;
                _pips[i] = pip;
            }
            Ui.Btn("+", () => SetVolume(GameSettings.Volume + 1), "le-btn--small le-btn--icon", vol).name = "settings-volume-up";
            _volume = Ui.Text("", "le-set__value", vol);

            var mus = Row(card, "MUSIC", out _rowLabels[1]);
            Ui.Btn("-", () => SetMusic(GameSettings.MusicVolume - 1), "le-btn--small le-btn--icon", mus).name = "settings-music-down";
            var mpips = Ui.El("le-row le-set__pips", mus);
            for (int i = 0; i < _musicPips.Length; i++)
            {
                int tenths = i + 1;
                var pip = Ui.Btn("", () => SetMusic(GameSettings.MusicVolume == tenths ? tenths - 1 : tenths), "le-set__pip", mpips);
                pip.name = "settings-music-" + tenths;
                _musicPips[i] = pip;
            }
            Ui.Btn("+", () => SetMusic(GameSettings.MusicVolume + 1), "le-btn--small le-btn--icon", mus).name = "settings-music-up";
            _music = Ui.Text("", "le-set__value", mus);

            var full = Row(card, "FULLSCREEN");
            _fullscreen = Ui.Btn("", () =>
            {
                GameSettings.SetFullscreen(!GameSettings.Fullscreen);
                Sync();
            }, "le-set__toggle", full);
            _fullscreen.name = "settings-fullscreen";

            var hints = Row(card, S.IsDemo ? "HINTS & TUTORIAL" : "HINTS");
            _hints = Ui.Btn("", () =>
            {
                GameSettings.SetHints(Game, !Game.ShowHints);
                Sync();
            }, "le-set__toggle", hints);
            _hints.name = "settings-hints";
            Ui.Text(S.IsDemo
                ? "R.'s notes on both days and the hint line at the bottom of the shop. F1 switches them in play."
                : "The hint line at the bottom of the shop. F1 switches it in play.", "le-muted le-settings__help", card);

            var foot = Ui.El("le-row le-row--center le-settings__foot", card);
            Ui.Btn("BACK", Close, "le-btn--primary le-btn--big", foot).name = "settings-back";
            Ui.Text("▲ ▼ pick   ·   ◀ ▶ adjust   ·   ESC back", "le-muted le-settings__keys", card);
            Sync();
        }

        static VisualElement Row(VisualElement card, string label)
        {
            return Row(card, label, out _);
        }

        static VisualElement Row(VisualElement card, string label, out Label text)
        {
            var row = Ui.El("le-row le-set__row", card);
            text = Ui.Text(label, "le-set__label", row);
            return row;
        }

        void SetVolume(int tenths)
        {
            _row = 0;
            GameSettings.SetVolume(tenths);
            Sync();
        }

        void SetMusic(int tenths)
        {
            _row = 1;
            GameSettings.SetMusicVolume(tenths);
            Sync();
        }

        void Sync()
        {
            for (int i = 0; i < _pips.Length; i++) _pips[i].Toggle("le-set__pip--on", i < GameSettings.Volume);
            _volume.SetTextIfChanged(GameSettings.Volume * 10 + "%");
            for (int i = 0; i < _musicPips.Length; i++) _musicPips[i].Toggle("le-set__pip--on", i < GameSettings.MusicVolume);
            _music.SetTextIfChanged(GameSettings.MusicVolume * 10 + "%");
            for (int i = 0; i < _rowLabels.Length; i++) _rowLabels[i].Toggle("le-set__label--picked", i == _row);
            OnOff(_fullscreen, GameSettings.Fullscreen);
            _shownFull = GameSettings.Fullscreen;
            OnOff(_hints, Game.ShowHints);
        }

        static void OnOff(Button b, bool on)
        {
            b.text = on ? "ON" : "OFF";
            b.Toggle("le-set__toggle--on", on);
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
            if (_shownFull != GameSettings.Fullscreen) Sync(); // Alt+Enter while the card is up
            if (Game.Top != this) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                int pick = (kb.downArrowKey.wasPressedThisFrame ? 1 : 0) - (kb.upArrowKey.wasPressedThisFrame ? 1 : 0);
                if (pick != 0 && _row + pick >= 0 && _row + pick < _rowLabels.Length)
                {
                    _row += pick;
                    Sync();
                    Sfx.Play(SfxId.Click, 0.6f);
                }
                int step = (kb.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (kb.leftArrowKey.wasPressedThisFrame ? 1 : 0);
                int now = _row == 0 ? GameSettings.Volume : GameSettings.MusicVolume;
                if (step != 0 && now + step >= 0 && now + step <= 10)
                {
                    if (_row == 0) SetVolume(now + step);
                    else SetMusic(now + step);
                    Sfx.Play(SfxId.Click);
                }
            }
        }

        public override bool PausesGame => true;
    }
}
