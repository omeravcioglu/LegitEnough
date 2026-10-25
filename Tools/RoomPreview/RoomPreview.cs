#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LegitEnough.Game;
using LegitEnough.Sim;
using UnityEditor;
using UnityEngine;
using Calendar = LegitEnough.Sim.Calendar;

namespace LegitEnoughCI
{
    /// <summary>
    /// Renders the shop room art (RoomArt + lights) at a few equipment tiers, times and weathers,
    /// with a customer at the counter and, in the busy shots, papers on the desk and cash on the
    /// counter where ShopView puts them. Writes 480x270 PNGs: a quick look at a room art change
    /// without playing the game. No UI, no game session.
    /// Run: Unity -batchmode -projectPath COPY -executeMethod LegitEnoughCI.RoomPreview.Run
    /// </summary>
    public static class RoomPreview
    {
        static string _out;

        struct Shot
        {
            public string Name;
            public int Tier, Day;
            public Weather Weather;
            public float Minute;
            public bool Customer, Busy;
        }

        static readonly Shot[] Shots =
        {
            new Shot { Name = "t1_morning", Tier = 1, Day = 1, Weather = Weather.Rain, Minute = 9 * 60 + 13, Customer = true },
            new Shot { Name = "t1_busy", Tier = 1, Day = 3, Weather = Weather.Clear, Minute = 14 * 60, Customer = true, Busy = true },
            new Shot { Name = "t1_night", Tier = 1, Day = 1, Weather = Weather.Rain, Minute = 22 * 60, Customer = true },
            new Shot { Name = "t2_day", Tier = 2, Day = 40, Weather = Weather.Cloudy, Minute = 11 * 60, Customer = true, Busy = true },
            new Shot { Name = "t3_night", Tier = 3, Day = 100, Weather = Weather.Snow, Minute = 21 * 60, Customer = true, Busy = true },
            new Shot { Name = "t3_day", Tier = 3, Day = 100, Weather = Weather.Clear, Minute = 13 * 60 },
        };

        public static void Run()
        {
            _out = Environment.GetEnvironmentVariable("LE_ROOM_OUT");
            if (string.IsNullOrEmpty(_out)) _out = Path.GetFullPath("RoomPreviewOut");
            Directory.CreateDirectory(_out);
            int code = 0;
            try
            {
                foreach (var s in Shots) Render(s);
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(_out, "error.txt"), e.ToString());
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        static void Render(Shot s)
        {
            // Tier 2 everywhere gives Decor 2 (ShopRenderer.Gather: equipment sum 8).
            var st = new RoomState
            {
                Decor = s.Tier,
                Camera = s.Tier,
                Printer = s.Tier,
                Computer = s.Tier,
                Station = s.Tier,
                Day = s.Day,
                Weather = s.Weather,
                Minute = s.Minute,
                Power = true,
                Open = true,
                Backdrop = Backdrop.LightGray,
            };
            var date = Calendar.DateOf(s.Day);
            st.Month = date.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
            st.Weekday = date.ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant();
            st.DayOfMonth = date.Day;
            st.Sky = ShopMood.At(s.Minute, s.Weather);
            st.Tabs.Add(("BASICS", false));
            st.Tabs.Add((Countries.France, false));
            if (s.Tier > 1) st.Tabs.Add((Countries.Turkey, s.Tier == 2));
            if (s.Tier > 2) st.Tabs.Add((Countries.Japan, false));

            var light = new LightMap(RoomArt.W, RoomArt.H);
            RoomArt.Lights(light, st);
            light.Bake();

            var frame = new PixelBuffer(RoomArt.W, RoomArt.H);
            RoomArt.Back(frame, st);
            light.Apply(frame, 0, 0);
            Layer(frame, light, 18, 30, 79, 64, b => RoomArt.WindowLayer(b, st, 3.3f));
            Layer(frame, light, 232, 15, 32, 32, b => RoomArt.ClockLayer(b, st.Minute, st.Decor));
            Layer(frame, light, 366, 24, 96, 112, b => RoomArt.BackdropLayer(b, st));
            Layer(frame, light, 160, 12, 60, 124, b => RoomArt.DoorLayer(b, st, 0f, 0f, 3.3f));

            if (s.Customer)
            {
                Appearance look = null;
                foreach (var p in StoryCast.CreateProfiles())
                    if (p.Id == StoryCast.SamirId) look = p.Look;
                var fig = new PixelBuffer(RoomArt.W, RoomArt.H);
                var head = HeadState.Rest(look, Expression.Nervous);
                FigurePainter.DrawFigure(new PixelPainter(fig), new Vector2(240f, 317.5f), 1f, look, new BodyPose(), head);
                fig.Outline(Pal.Outline);
                light.Apply(fig, 0, 0);
                frame.Blit(fig, 0, 0);
            }

            var front = new PixelBuffer(RoomArt.W, RoomArt.H);
            RoomArt.Front(front, st);
            light.Apply(front, 0, 0);
            frame.Blit(front, 0, 0);
            string os = s.Tier == 1 ? "OFFICIAL-ISH 98" : s.Tier == 2 ? "OFFICIAL-ISH XP" : "ORDERS";
            string clock = $"{(int)(s.Minute / 60):00}:{(int)(s.Minute % 60):00}";
            var sr = RoomArt.ScreenRect(st.Computer);
            Layer(frame, light, sr.x, sr.y, sr.width, sr.height, b =>
                RoomArt.ScreenLayer(b, st, os, s.Tier == 3 ? "2 OPEN" : s.Busy ? "2 OPEN ORDERS" : "1 NEW MESSAGE", s.Tier == 3 ? clock : $"DAY {s.Day}  {clock}", !s.Busy, false));
            Layer(frame, light, 342, 196, 72, 45, b => RoomArt.PrinterLayer(b, st, s.Busy ? "TRAY: 1" : "READY", "idle", 0f, false, 0f));
            Layer(frame, light, 427, 237, 36, 10, b => RoomArt.LcdLayer(b, s.Busy ? "$1,240" : "$200", true));

            if (s.Busy)
            {
                // The same spots as ShopView.SyncDesk (stage pixels / 4).
                string[] names = { "OKAFOR", "LINDQVIST", "HADDAD" };
                string[] countries = { "FRANCE", "TURKEY", "FRANCE" };
                for (int i = 0; i < 3; i++)
                {
                    var b = new PixelBuffer(27, 31);
                    DeskSprites.Sheet(b, 11 + i, names[i], countries[i]);
                    Sprite(frame, light, b, 175 + (i % 3) * 30, 202 + (i / 3) * 33 + (i % 2) * 2);
                }
                for (int i = 0; i < 2; i++)
                {
                    var b = new PixelBuffer(19, 28);
                    DeskSprites.Booklet(b, LegitEnough.Game.Looks.Of(i == 0 ? CoverColor.DarkRed : CoverColor.Navy), 9 + i);
                    Sprite(frame, light, b, 268 + (i % 3) * 21, 202 + (i / 3) * 31);
                }
                var cash = new PixelBuffer(34, 14);
                DeskSprites.Cash(cash, 120, 3);
                Sprite(frame, light, cash, 280, 160);
                var page = new PixelBuffer(13, 17);
                DeskSprites.TrayPage(page, 12);
                Sprite(frame, light, page, 344, 249);
            }
            Save(frame, s.Name);
        }

        static void Sprite(PixelBuffer into, LightMap light, PixelBuffer b, int x, int y)
        {
            light.Apply(b, x, y);
            into.Blit(b, x, y);
        }

        static void Layer(PixelBuffer into, LightMap light, int x, int y, int w, int h, Action<PixelBuffer> draw)
        {
            var b = new PixelBuffer(w, h);
            draw(b);
            light.Apply(b, x, y);
            into.Blit(b, x, y);
        }

        static void Save(PixelBuffer b, string name)
        {
            var tex = new Texture2D(b.W, b.H, TextureFormat.RGBA32, false);
            var px = new Color32[b.W * b.H];
            for (int y = 0; y < b.H; y++)
                for (int x = 0; x < b.W; x++)
                {
                    var c = b.Px[y * b.W + x];
                    c.a = 255;
                    px[(b.H - 1 - y) * b.W + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(_out, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
#endif
