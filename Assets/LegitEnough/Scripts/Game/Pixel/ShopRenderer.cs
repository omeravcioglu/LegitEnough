using System.Globalization;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;
using Calendar = LegitEnough.Sim.Calendar;

namespace LegitEnough.Game
{
    /// <summary>
    /// Draws the shop as lit pixel art. The room behind the customers and the counter/desk in
    /// front of them are baked into two 480x270 layers and only redrawn when the shop changes
    /// (equipment, day, light); small layers animate on top (rain, door, clock, screen, printer).
    /// One <see cref="LightMap"/> lights all of it, people included.
    /// </summary>
    public sealed class ShopRenderer
    {
        public const int Scale = 4;

        readonly GameRoot _game;
        GameSession S => _game.Session;

        public readonly LightMap Light = new LightMap(RoomArt.W, RoomArt.H);
        /// <summary>Changes whenever the light map is rebaked (figures relight then).</summary>
        public int LightVersion { get; private set; }
        public readonly RoomState State = new RoomState();
        /// <summary>Main menu: the empty shop at night in the rain, whatever the clock says.</summary>
        public bool TitleMood;

        public readonly PixelImage Back = new PixelImage("le-px-layer");
        public readonly PixelImage Front = new PixelImage("le-px-layer");
        readonly PixelImage _window = new PixelImage("le-px-layer");
        readonly PixelImage _door = new PixelImage("le-px-layer");
        readonly PixelImage _clock = new PixelImage("le-px-layer");
        readonly PixelImage _backdrop = new PixelImage("le-px-layer");
        readonly PixelImage _screen = new PixelImage("le-px-layer");
        readonly PixelImage _printer = new PixelImage("le-px-layer");
        readonly PixelImage _lcd = new PixelImage("le-px-layer");
        readonly VisualElement _flicker;

        readonly PixelBuffer _backAlbedo = new PixelBuffer(RoomArt.W, RoomArt.H);
        readonly PixelBuffer _frontAlbedo = new PixelBuffer(RoomArt.W, RoomArt.H);
        readonly PixelBuffer _lit = new PixelBuffer(RoomArt.W, RoomArt.H);
        readonly PixelBuffer _windowBuf = new PixelBuffer(79, 64);
        readonly PixelBuffer _doorBuf = new PixelBuffer(60, 124);
        readonly PixelBuffer _clockBuf = new PixelBuffer(32, 32);
        readonly PixelBuffer _backdropBuf = new PixelBuffer(96, 112);
        readonly PixelBuffer _screenBuf = new PixelBuffer(64, 42);
        readonly PixelBuffer _printerBuf = new PixelBuffer(72, 45);
        readonly PixelBuffer _lcdBuf = new PixelBuffer(36, 10);

        string _backKey, _frontKey, _lightKey, _backdropKey, _screenKey, _printerKey, _lcdKey, _doorKey;
        int _clockMinute = -1, _clockDecor;
        float _time, _flickerT, _nextFlicker = 6f;

        public ShopRenderer(GameRoot game)
        {
            _game = game;
            Place(Back, 0, 0, RoomArt.W, RoomArt.H);
            Place(Front, 0, 0, RoomArt.W, RoomArt.H);
            Place(_window, 18, 30, 79, 64);
            Place(_door, 160, 12, 60, 124);
            Place(_clock, 232, 15, 32, 32);
            Place(_backdrop, 366, 24, 96, 112);
            Place(_printer, 342, 196, 72, 45);
            Place(_lcd, 427, 237, 36, 10);
            _flicker = Ui.Deco("le-flicker", null).At(0, 0, GameRoot.StageWidth, GameRoot.StageHeight);
            _flicker.style.opacity = 0f;
        }

        static void Place(VisualElement e, int x, int y, int w, int h) => e.At(x * Scale, y * Scale, w * Scale, h * Scale);

        /// <summary>Layers that go behind the customers, back to front.</summary>
        public void AddBackLayers(VisualElement parent)
        {
            parent.Add(Back);
            parent.Add(_window);
            parent.Add(_clock);
            parent.Add(_backdrop);
            parent.Add(_door);
        }

        /// <summary>Layers in front of the customers: counter, desk and the props on it.</summary>
        public void AddFrontLayers(VisualElement parent)
        {
            parent.Add(Front);
            parent.Add(_screen);
            parent.Add(_printer);
            parent.Add(_lcd);
        }

        public VisualElement Flicker => _flicker;

        // ------------------------------------------------------------------ per frame

        public void Tick(float dt, float doorOpen)
        {
            _time += dt;
            Gather();
            var st = State;

            string backKey = $"{st.Decor}|{st.Station}|{st.Day}|{st.Weather}|{st.Power}";
            string frontKey = $"{st.Decor}|{st.Camera}|{st.Computer}|{st.Printer}|{st.Recording}|{st.Power}|{TabsKey()}";
            int bucket = Mathf.FloorToInt(st.Minute / 10f);
            string lightKey = $"{bucket}|{st.Weather}|{st.Power}|{st.Decor}|{st.Station}|{st.Computer}|{st.Open}|{st.Day}";

            bool relight = false;
            if (lightKey != _lightKey)
            {
                _lightKey = lightKey;
                RoomArt.Lights(Light, st);
                Light.Bake();
                LightVersion++;
                relight = true;
                _clockMinute = -1;
                _backdropKey = null;
                _screenKey = null;
                _printerKey = null;
                _lcdKey = null;
                _doorKey = null;
            }
            if (backKey != _backKey)
            {
                _backKey = backKey;
                RoomArt.Back(_backAlbedo, st);
                relight = true;
                _clockMinute = -1;
                _backdropKey = null;
            }
            if (frontKey != _frontKey)
            {
                _frontKey = frontKey;
                RoomArt.Front(_frontAlbedo, st);
                var sr = RoomArt.ScreenRect(st.Computer);
                Place(_screen, sr.x, sr.y, sr.width, sr.height);
                _screenBuf.Resize(sr.width, sr.height);
                _screenKey = null;
                _printerKey = null;
                relight = true;
            }
            if (relight)
            {
                Upload(Back, _backAlbedo, 0, 0);
                Upload(Front, _frontAlbedo, 0, 0);
            }

            // Window: rain and snow keep moving.
            RoomArt.WindowLayer(_windowBuf, st, _time);
            Light.Apply(_windowBuf, 18, 30);
            _window.Upload(_windowBuf);

            // Door: redraw while it swings, the sign flickers or the state changes.
            float bell = doorOpen > 0f ? Mathf.Sin(_time * 30f) * 2f * Mathf.Clamp01(doorOpen) : 0f;
            bool neonFlicker = st.Open && Mathf.Repeat(_time, 7.3f) < 0.12f;
            string doorKey = $"{st.Open}|{st.Power}|{st.Decor}|{st.Weather}|{neonFlicker}|{LightVersion}";
            bool animate = doorOpen > 0f || st.Weather == Weather.Rain || st.Weather == Weather.Snow;
            if (animate || doorKey != _doorKey)
            {
                _doorKey = doorKey;
                float open = doorOpen < 1f ? Mathf.Clamp01(doorOpen / 1f) : Mathf.Clamp01((1.4f - doorOpen) / 0.4f);
                RoomArt.DoorLayer(_doorBuf, st, doorOpen > 0f ? open : 0f, bell, _time);
                Light.Apply(_doorBuf, 160, 12);
                _door.Upload(_doorBuf);
            }

            int minute = Mathf.FloorToInt(st.Minute);
            if (minute != _clockMinute || _clockDecor != st.Decor)
            {
                _clockMinute = minute;
                _clockDecor = st.Decor;
                RoomArt.ClockLayer(_clockBuf, st.Minute, st.Decor);
                Light.Apply(_clockBuf, 232, 15);
                _clock.Upload(_clockBuf);
            }

            string bdKey = $"{st.Backdrop}|{st.Pending}|{Mathf.RoundToInt(st.Swap * 40f)}|{st.Station}";
            if (bdKey != _backdropKey)
            {
                _backdropKey = bdKey;
                RoomArt.BackdropLayer(_backdropBuf, st);
                Light.Apply(_backdropBuf, 366, 24);
                _backdrop.Upload(_backdropBuf);
            }

            ScreenTick(st);
            PrinterTick(st);

            string cash = Labels.Money(S.Ledger.Cash);
            string lcdKey = cash + st.Power;
            if (lcdKey != _lcdKey)
            {
                _lcdKey = lcdKey;
                RoomArt.LcdLayer(_lcdBuf, cash, st.Power);
                Light.Apply(_lcdBuf, 427, 237);
                _lcd.Upload(_lcdBuf);
            }

            // The Day 1 tube flickers now and then.
            if (st.Decor == 1 && st.Power)
            {
                _nextFlicker -= dt;
                if (_nextFlicker <= 0f)
                {
                    _flickerT = 0.18f;
                    _nextFlicker = 5f + Mathf.Abs(Mathf.Sin(_time * 12.9f)) * 9f;
                }
            }
            if (_flickerT > 0f)
            {
                _flickerT -= dt;
                _flicker.style.opacity = _flickerT > 0f && Mathf.Repeat(_flickerT, 0.08f) < 0.04f ? 0.16f : 0f;
            }
        }

        void Upload(PixelImage img, PixelBuffer albedo, int ox, int oy)
        {
            _lit.Resize(albedo.W, albedo.H);
            System.Array.Copy(albedo.Px, _lit.Px, albedo.W * albedo.H);
            System.Array.Copy(albedo.Em, _lit.Em, albedo.W * albedo.H);
            Light.Apply(_lit, ox, oy);
            img.Upload(_lit);
        }

        void ScreenTick(RoomState st)
        {
            string title = st.Computer == 1 ? "OFFICIAL-ISH 98" : st.Computer == 2 ? "OFFICIAL-ISH XP" : "OFFICIAL-ISH PRO";
            int unread = S.Inbox.Unread;
            bool blink = Mathf.FloorToInt(_time * 2f) % 2 == 0;
            string line1, line2;
            bool alert = unread > 0;
            if (alert) line1 = unread == 1 ? "1 NEW MESSAGE" : $"{unread} NEW MESSAGES";
            else
            {
                int n = 0;
                foreach (var _ in S.Orders.Open()) n++;
                line1 = n == 1 ? "1 OPEN ORDER" : $"{n} OPEN ORDERS";
            }
            line2 = $"DAY {S.Day}  {S.Now.Clock}";
            if (st.Computer == 3)
            {
                // Twin monitors: the left screen is narrow, so keep it terse.
                title = "ORDERS";
                if (alert) line1 = $"{unread} NEW MSG";
                else
                {
                    int n = 0;
                    foreach (var _ in S.Orders.Open()) n++;
                    line1 = $"{n} OPEN";
                }
                line2 = S.Now.Clock;
            }
            string key = $"{title}|{line1}|{line2}|{alert && blink}|{st.Power}|{LightVersion}";
            if (key == _screenKey) return;
            _screenKey = key;
            RoomArt.ScreenLayer(_screenBuf, st, title, line1, line2, alert, blink);
            var sr = RoomArt.ScreenRect(st.Computer);
            Light.Apply(_screenBuf, sr.x, sr.y);
            _screen.Upload(_screenBuf);
        }

        void PrinterTick(RoomState st)
        {
            var pr = S.Printer;
            string light = !st.Power ? "off" : pr.Jammed ? "jam" : pr.Printing != null ? "busy" : "idle";
            string status = !st.Power ? "NO POWER"
                : pr.Jammed ? "JAM! CLICK"
                : pr.Printing != null ? $"PRINT {Mathf.RoundToInt(pr.Progress * 100f)}%"
                : st.PrinterBroken ? "GRINDING" : pr.Tray.Count > 0 ? "TRAY: " + pr.Tray.Count : "READY";
            bool printing = pr.Printing != null && st.Power;
            int blinkPhase = light == "busy" ? Mathf.FloorToInt(_time / 0.3f) % 2 : light == "jam" ? Mathf.FloorToInt(_time / 0.2f) % 2 : 0;
            string key = $"{status}|{light}|{blinkPhase}|{(printing ? Mathf.RoundToInt(pr.Progress * 18f) : -1)}|{st.Printer}|{LightVersion}";
            if (key == _printerKey) return;
            _printerKey = key;
            RoomArt.PrinterLayer(_printerBuf, st, status, light, printing ? pr.Progress : 0f, printing, _time);
            Light.Apply(_printerBuf, 342, 196);
            _printer.Upload(_printerBuf);
        }

        string TabsKey()
        {
            string k = "";
            foreach (var (id, outdated) in State.Tabs) k += id + (outdated ? "!" : ",");
            return k;
        }

        void Gather()
        {
            var st = State;
            var s = S;
            st.Camera = s.Camera.Tier;
            st.Printer = s.Equipment[EquipmentSlot.Printer].Tier;
            st.Computer = s.Computer.Tier;
            st.Station = s.Station.Tier;
            int sum = st.Camera + st.Printer + st.Computer + st.Station;
            st.Decor = sum <= 6 ? 1 : sum <= 9 ? 2 : 3;
            st.Day = s.Day;
            var date = Calendar.DateOf(s.Day);
            st.Month = date.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
            st.Weekday = date.ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant();
            st.DayOfMonth = date.Day;
            st.Weather = TitleMood ? Weather.Rain : ShopMood.WeatherFor(s.Seed, s.Day);
            st.Minute = TitleMood ? 22f * 60f : s.Clock.Minute;
            st.Sky = ShopMood.At(st.Minute, st.Weather);
            st.Power = s.PowerOn;
            st.Open = !TitleMood && (s.Phase == DayPhase.Open || s.Phase == DayPhase.AfterHours);
            st.PrinterBroken = s.PrinterBroken;
            st.Recording = s.ActivePhoto != null;
            st.Tabs.Clear();
            st.Tabs.Add(("BASICS", false));
            foreach (var c in s.CountryList)
                if (c.Owned) st.Tabs.Add((c.Id, c.HandbookOutdated));
            var rig = s.Rig;
            st.Backdrop = rig.Backdrop;
            st.Pending = rig.SwappingBackdrop ? rig.PendingBackdrop : null;
            st.Swap = rig.SwappingBackdrop ? 1f - rig.BackdropSwapRemaining / Mathf.Max(0.01f, s.Station.BackdropSwapSeconds) : 0f;
        }
    }

    /// <summary>Pixel sprites for the paper and cash that sit on the desk and the counter.</summary>
    public static class DeskSprites
    {
        static Color32 C(string hex) => Pal.Hex(hex);

        /// <summary>Client sheet: 27x31.</summary>
        public static void Sheet(PixelBuffer b, int orderId, string surname, string country)
        {
            b.Resize(27, 31);
            b.Rect(1, 1, 26, 30, C("#2a2016"));
            b.Rect(0, 0, 26, 30, C("#e8e0cc"));
            b.Rect(20, 0, 6, 6, C("#cfc4a8"));
            b.Line(20, 0, 25, 5, C("#b8ad92"));
            b.Text("#" + orderId.ToString(CultureInfo.InvariantCulture), 2, 2, C("#a8342b"), PixelFont.Tiny);
            b.Text(PixelFont.Tiny.Fit(surname, 23), 2, 10, C("#23407a"), PixelFont.Tiny);
            b.Text(PixelFont.Tiny.Fit(country, 23), 2, 17, C("#5a5146"), PixelFont.Tiny);
            b.HLine(2, 22, 24, C("#c9bda2"));
            b.HLine(2, 16, 27, C("#c9bda2"));
            // Paper clip.
            b.VLine(5, -1, 4, C("#8a8c88"));
            b.VLine(7, -1, 4, C("#8a8c88"));
            b.HLine(5, 7, 4, C("#8a8c88"));
        }

        /// <summary>Booklet: 19x28.</summary>
        public static void Booklet(PixelBuffer b, Color cover, int orderId)
        {
            b.Resize(19, 28);
            Color32 c = cover;
            b.Rect(1, 1, 18, 27, C("#1a1410"));
            b.Rect(0, 0, 18, 27, c);
            b.VLine(0, 0, 26, Pal.Mul(c, 0.7f));
            b.HLine(0, 17, 0, Pal.Mix(c, C("#ffffff"), 0.2f));
            b.Ring(9f, 9f, 4f, 4f, C("#d8b45a"));
            b.Set(9, 9, C("#d8b45a"));
            b.HLine(4, 13, 16, C("#d8b45a"));
            b.Rect(2, 19, 14, 7, C("#efe8d6"));
            b.Text(PixelFont.Tiny.Fit("#" + orderId.ToString(CultureInfo.InvariantCulture), 13), 3, 20, C("#2a241c"), PixelFont.Tiny);
        }

        /// <summary>Printed page in the tray: 12x16.</summary>
        public static void TrayPage(PixelBuffer b, int orderId)
        {
            b.Resize(13, 17);
            b.Rect(1, 1, 12, 16, C("#2a2016"));
            b.Rect(0, 0, 12, 16, C("#f6f1e2"));
            b.Rect(1, 1, 4, 5, C("#b8c4cc"));
            b.HLine(6, 10, 2, C("#9a927c"));
            b.HLine(6, 9, 4, C("#9a927c"));
            b.HLine(1, 10, 8, C("#9a927c"));
            b.Text(PixelFont.Tiny.Fit(orderId.ToString(CultureInfo.InvariantCulture), 10), 1, 10, C("#a8342b"), PixelFont.Tiny);
        }

        /// <summary>Cash on the counter: 34x14.</summary>
        public static void Cash(PixelBuffer b, int amount, int seed)
        {
            b.Resize(34, 14);
            var r = new PixelRandom(seed);
            for (int i = 2; i >= 0; i--)
            {
                int ox = r.Range(0, 3), oy = i * 2;
                b.Rect(ox + 1, oy + 1, 30, 8, C("#2a3a22"));
                b.Rect(ox, oy, 30, 8, i == 0 ? C("#86a872") : C("#6f8f5a"));
                b.Box(ox, oy, 30, 8, C("#4f6a3e"));
                b.Ring(ox + 15f, oy + 4f, 2.5f, 2.5f, C("#4f6a3e"));
            }
            b.Rect(0, 7, 33, 7, C("#e8e0cc"));
            b.Box(0, 7, 33, 7, C("#4f6a3e"));
            b.TextCentered(PixelFont.Tiny.Fit(Labels.Money(amount), 30), 17, 8, C("#1c3a14"), PixelFont.Tiny);
        }
    }
}
