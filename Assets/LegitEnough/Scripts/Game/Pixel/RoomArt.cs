using System.Collections.Generic;
using System.Globalization;
using LegitEnough.Sim;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>What the room renderer needs to know about the shop right now.</summary>
    public sealed class RoomState
    {
        /// <summary>1 = Day 1 dump, 2 = getting there, 3 = proper business (from the equipment bought).</summary>
        public int Decor = 1;
        public int Camera = 1, Printer = 1, Computer = 1, Station = 1;
        public int Day = 1;
        public string Month = "OCT", Weekday = "MON";
        public int DayOfMonth = 6;
        public Weather Weather;
        public ShopMood.Outside Sky;
        public float Minute;
        public bool Power = true;
        public bool Open;
        public bool PrinterBroken;
        public bool Recording;
        public readonly List<(string id, bool outdated)> Tabs = new List<(string, bool)>();
        public Backdrop Backdrop;
        public Backdrop? Pending;
        /// <summary>0..1 progress of a backdrop swap.</summary>
        public float Swap;

        public bool Night => Sky.Daylight < 0.3f;
    }

    /// <summary>
    /// The shop drawn as pixel art on a 480x270 grid (one art pixel = 4 stage pixels). The
    /// layout follows <see cref="RoomLayout"/> and the interactive spots in <see cref="ShopView"/>;
    /// equipment tiers swap the physical objects (visual direction: "Day 1 → Day 100").
    /// </summary>
    public static class RoomArt
    {
        public const int W = 480, H = 270;

        static readonly Dictionary<string, Color32> Cache = new Dictionary<string, Color32>();

        static Color32 C(string hex)
        {
            if (!Cache.TryGetValue(hex, out var c)) Cache[hex] = c = Pal.Hex(hex);
            return c;
        }

        static Color32 M(Color32 c, float k) => Pal.Mul(c, k);

        // ================================================================== back wall, floor, furniture

        public static void Back(PixelBuffer b, RoomState st)
        {
            b.Clear();
            int d = st.Decor;

            // Ceiling strip.
            b.Rect(0, 0, W, 9, C("#24221e"));
            if (d < 3)
                for (int x = 20; x < W; x += 40) b.VLine(x, 0, 8, C("#1c1a16"));
            b.HLine(0, W - 1, 8, C("#181713"));

            // Wall.
            Color32 wall = d == 1 ? C("#7a7358") : d == 2 ? C("#7a7a62") : C("#4b5654");
            b.Rect(0, 9, W, 91, wall);
            b.Speck(0, 9, W, 91, M(wall, 0.93f), 0.16f, 11);
            if (d == 1)
            {
                // Water stain from the ceiling, a crack, grime along the rail.
                var r = new PixelRandom(5);
                for (int x = 104; x < 160; x++)
                {
                    int depth = 3 + Mathf.RoundToInt(6f * Mathf.Sin((x - 104) / 56f * Mathf.PI) + r.Next() * 3f);
                    b.VLine(x, 9, 9 + depth, M(wall, 0.88f));
                    b.Set(x, 10 + depth, M(wall, 0.8f));
                }
                int cx = 352;
                for (int y = 9; y < 46; y++)
                {
                    cx += r.Next() < 0.5f ? 0 : r.Next() < 0.5f ? 1 : -1;
                    b.Set(cx, y, M(wall, 0.65f));
                }
                b.Speck(0, 86, W, 14, M(wall, 0.84f), 0.22f, 4);
            }
            else if (d == 2)
            {
                b.Rect(104, 9, 40, 18, M(wall, 1.05f));
                b.Rect(340, 18, 18, 30, M(wall, 1.04f));
            }

            DrawCeilingLights(b, st);

            // Wainscot + rail.
            if (d < 3)
            {
                Color32 wain = d == 1 ? C("#465140") : C("#3f4b42");
                b.Rect(0, 100, W, 35, wain);
                b.Speck(0, 100, W, 35, M(wain, 0.9f), d == 1 ? 0.2f : 0.08f, 6);
                b.HLine(0, W - 1, 99, C("#2f352a"));
                b.HLine(0, W - 1, 100, C("#5a6650"));
            }
            else
            {
                b.Rect(0, 100, W, 35, C("#4a3826"));
                for (int x = 0; x < W; x += 12) b.VLine(x, 101, 134, C("#3a2b1d"));
                for (int x = 6; x < W; x += 24) b.Box(x - 4, 105, 16, 24, C("#3f2f20"));
                b.HLine(0, W - 1, 99, C("#8a7444"));
                b.HLine(0, W - 1, 100, C("#a88e56"));
            }
            b.HLine(0, W - 1, 133, C("#25211a"));
            b.HLine(0, W - 1, 134, C("#2d281f"));

            // Floor.
            for (int y = 135; y < 194; y++)
                for (int x = 0; x < W; x++)
                {
                    Color32 c;
                    if (d == 3) c = ((y / 4) & 1) == 0 ? C("#5a4330") : C("#523d2c");
                    else
                    {
                        bool tile = (((x / 12) + ((y - 135) / 6)) & 1) == 0;
                        c = d == 1 ? (tile ? C("#4a4334") : C("#3f392c")) : (tile ? C("#575041") : C("#4d4637"));
                    }
                    b.Set(x, y, c);
                }
            if (d == 3)
                for (int y = 135; y < 194; y += 4)
                    for (int x = (y * 7) % 37; x < W; x += 37) b.Set(x, y, C("#3d2c1e"));
            if (d == 1) b.Speck(0, 135, W, 40, C("#36302a"), 0.05f, 8);
            if (st.Weather == Weather.Rain || st.Weather == Weather.Snow)
            {
                // Wet footprints from the door toward the counter.
                for (int i = 0; i < 7; i++)
                {
                    int fx = 186 + i * 7 + ((i & 1) == 0 ? 0 : 3), fy = 140 + i * 4;
                    b.Rect(fx, fy, 3, 2, M(C("#3f392c"), 0.82f));
                    b.Set(fx + 1, fy, C("#6c7a80"));
                }
                b.Rect(150, 136, 12, 2, C("#5d6a72"));
            }

            Window(b, st);
            WallDecor(b, st);
            Seating(b, st);
            Calendar(b, st);
            Poster(b, st);
            Cabinet(b, st);
            PhotoCorner(b, st);
        }

        static void DrawCeilingLights(PixelBuffer b, RoomState st)
        {
            if (st.Decor < 3)
            {
                // Fluorescent fixture; on Day 1 one end of the tube is dying.
                b.Rect(188, 2, 104, 6, C("#6e6e66"));
                b.HLine(188, 291, 2, C("#8a8a80"));
                b.HLine(190, 289, 7, C("#4a4a44"));
                b.Emissive = st.Power;
                b.HLine(192, st.Decor == 1 ? 262 : 287, 5, st.Power ? C("#f2f5d8") : C("#8a8c78"));
                b.HLine(192, st.Decor == 1 ? 262 : 287, 4, st.Power ? C("#d8dcb8") : C("#7a7c6a"));
                b.Emissive = false;
                if (st.Decor == 1)
                {
                    b.HLine(263, 287, 5, C("#8a8c78"));
                    b.HLine(263, 287, 4, C("#7a7c6a"));
                    b.Set(240, 8, C("#3a3a34"));
                }
                return;
            }
            foreach (int px in new[] { 180, 300 })
            {
                b.VLine(px, 0, 13, C("#1a1a18"));
                b.Rect(px - 6, 14, 13, 5, C("#2f4a3a"));
                b.HLine(px - 7, px + 7, 19, C("#a88a4a"));
                b.HLine(px - 5, px - 2, 14, C("#4a6a52"));
                b.Emissive = st.Power;
                b.HLine(px - 4, px + 4, 20, st.Power ? C("#ffe2a0") : C("#7a7468"));
                b.Emissive = false;
            }
        }

        static void Window(PixelBuffer b, RoomState st)
        {
            // Frame and sill; the glass (outside view, rain) is a separate animated layer.
            b.Rect(15, 27, 85, 70, C("#2c2a23"));
            b.Rect(18, 30, 79, 64, C("#141820"));
            b.Rect(12, 97, 91, 3, st.Decor == 3 ? C("#6a5440") : C("#5d553f"));
            b.HLine(12, 102, 97, st.Decor == 3 ? C("#8a7058") : C("#776d52"));
            if (st.Decor == 3)
            {
                // A plant on the sill.
                b.Rect(84, 89, 8, 8, C("#8a5a3a"));
                b.HLine(84, 91, 89, C("#a06a44"));
                b.Ellipse(88f, 85f, 6f, 5f, C("#3f6a3a"));
                b.Ellipse(85f, 82f, 3f, 4f, C("#4f7a46"));
                b.Ellipse(91f, 84f, 3f, 3f, C("#365c32"));
            }
        }

        static void WallDecor(PixelBuffer b, RoomState st)
        {
            int d = st.Decor;
            if (d == 1)
            {
                // Fuse box with wires running up into the ceiling.
                b.Rect(108, 30, 20, 26, C("#6b6c64"));
                b.Box(108, 30, 20, 26, C("#4a4b45"));
                b.Rect(110, 32, 16, 3, C("#7c7d74"));
                b.Rect(110, 37, 4, 2, C("#d8cf9a"));
                b.Set(124, 44, C("#c9a24a"));
                b.Line(112, 30, 112, 9, C("#1d1c1a"));
                b.Line(116, 30, 120, 9, C("#1d1c1a"));
                b.Line(124, 30, 124, 20, C("#6a2a22"));
                b.Line(124, 20, 150, 12, C("#6a2a22"));
                for (int x = 128; x < 158; x++) b.Set(x, 58 + Mathf.RoundToInt(Mathf.Sin((x - 128) / 30f * Mathf.PI) * 7f), C("#1d1c1a"));
                // The tube's power cable, stapled along the ceiling line.
                b.Line(290, 9, 360, 9, C("#1d1c1a"));
                for (int x = 296; x < 360; x += 12) b.Set(x, 10, C("#9a9a90"));
            }
            else
            {
                // Framed certificate (the joke gets more official as the shop does).
                b.Rect(108, 30, 22, 18, C("#6b5434"));
                b.Rect(110, 32, 18, 14, C("#d8cfb2"));
                b.HLine(112, 125, 34, C("#8a7a5a"));
                b.HLine(112, 122, 37, C("#a89a7a"));
                b.HLine(112, 124, 40, C("#a89a7a"));
                b.Ellipse(124f, 43f, 2.2f, 2.2f, C("#a33a2e"));
                if (d == 3)
                {
                    b.Rect(110, 54, 22, 26, C("#3a2a1e"));
                    b.Rect(112, 56, 18, 22, C("#c9b78a"));
                    b.Rect(112, 67, 18, 11, C("#6a7a5a"));
                    b.Ellipse(126f, 61f, 3f, 3f, C("#e8d8a0"));
                }
            }

            // Coat rack by the door.
            b.VLine(150, 44, 134, C("#3a2c20"));
            b.HLine(146, 154, 134, C("#3a2c20"));
            b.HLine(147, 153, 44, C("#3a2c20"));
            b.Set(146, 43, C("#3a2c20"));
            b.Set(154, 43, C("#3a2c20"));
            Color32 coat = d == 3 ? C("#2a2b36") : C("#4a4a3a");
            for (int y = 46; y < 76; y++)
            {
                int w = 3 + Mathf.Min(4, (y - 46) / 4);
                b.HLine(148 - w, 148 + w, y, y % 7 == 0 ? M(coat, 0.85f) : coat);
            }
            if (st.Weather == Weather.Rain)
            {
                b.VLine(155, 46, 79, C("#2a3550"));
                b.Rect(154, 52, 3, 22, C("#2b3550"));
                b.Set(155, 82, C("#6d86a0"));
                b.Set(155, 87, C("#6d86a0"));
                b.Rect(151, 133, 9, 2, C("#5d6a72"));
            }
        }

        static void Seating(PixelBuffer b, RoomState st)
        {
            // Bench seats sit at x 32, 62, 92 (RoomLayout.Bench); seat tops at y 134.
            int d = st.Decor;
            if (d == 1)
            {
                string[] colors = { "#9c5a3c", "#4d6b66", "#8a8a80" };
                for (int i = 0; i < 3; i++)
                {
                    int cx = 32 + i * 30;
                    var col = C(colors[i]);
                    b.Rect(cx - 9, 117, 18, 16, col);
                    b.HLine(cx - 9, cx + 8, 117, Pal.Mix(col, C("#ffffff"), 0.15f));
                    b.Rect(cx - 9, 117, 3, 16, M(col, 0.9f));
                    b.Rect(cx - 10, 133, 20, 4, M(col, 0.85f));
                    b.VLine(cx - 8, 137, 154, C("#3a3a38"));
                    b.VLine(cx + 8, 137, 154, C("#3a3a38"));
                    b.VLine(cx - 6, 137, 152, C("#2e2e2c"));
                    b.VLine(cx + 6, 137, 152, C("#2e2e2c"));
                }
                b.Set(42, 125, C("#2a2622"));
                b.Set(43, 126, C("#2a2622"));
            }
            else
            {
                Color32 seat = d == 2 ? C("#6b4a33") : C("#5a2e2a");
                Color32 back = d == 2 ? C("#5b4030") : C("#4a2622");
                b.Rect(16, 116, 92, 17, back);
                if (d == 2) for (int y = 119; y < 133; y += 4) b.HLine(16, 107, y, M(back, 0.85f));
                else for (int x = 22; x < 108; x += 14) b.VLine(x, 118, 131, M(back, 0.8f));
                b.Rect(13, 133, 98, 5, seat);
                b.HLine(13, 110, 133, Pal.Mix(seat, C("#ffffff"), 0.15f));
                b.Rect(18, 138, 4, 16, M(seat, 0.6f));
                b.Rect(102, 138, 4, 16, M(seat, 0.6f));
                if (d == 3)
                {
                    b.Rect(114, 124, 12, 12, C("#3a2a20"));
                    b.VLine(116, 136, 152, C("#2a1d14"));
                    b.VLine(124, 136, 152, C("#2a1d14"));
                    b.Rect(116, 121, 6, 3, C("#c8c0a8"));
                    b.Rect(117, 119, 6, 2, C("#d8a85a"));
                }
            }
            // A trash can next to the seats.
            b.Rect(118 - (d == 3 ? 0 : 4), 140, 8, 13, C("#5a5c58"));
            b.HLine(117 - (d == 3 ? 0 : 4), 126 - (d == 3 ? 0 : 4), 140, C("#74766f"));
            if (d == 1)
            {
                b.Ellipse(117f, 138f, 2.5f, 2f, C("#d8d0b8"));
                b.Ellipse(121f, 137f, 2f, 2f, C("#c8c0a6"));
                b.Set(124, 153, C("#d8d0b8"));
            }
        }

        static void Calendar(PixelBuffer b, RoomState st)
        {
            int x = 270, y = 14;
            b.Set(x + 16, y - 2, C("#4a4438"));
            b.Rect(x, y, 33, 40, C("#f0e8d4"));
            b.Rect(x, y, 33, 9, C("#a8342b"));
            b.TextCentered(st.Month, x + 17, y + 2, C("#f6efe0"), PixelFont.Tiny);
            string dd = st.DayOfMonth.ToString(CultureInfo.InvariantCulture);
            b.TextCentered(dd, x + 17, y + 13, C("#2a241c"), PixelFont.Stamp);
            b.TextCentered(st.Weekday, x + 17, y + 31, C("#5a5146"), PixelFont.Tiny);
            b.HLine(x, x + 32, y + 39, C("#c8bc9e"));
            // Torn-off page stubs.
            b.HLine(x + 2, x + 30, y + 9, C("#d8ccb0"));
        }

        static void Poster(PixelBuffer b, RoomState st)
        {
            int x = 307, y = 21;
            if (st.Decor >= 2)
            {
                b.Rect(x - 2, y - 2, 42, 53, st.Decor == 3 ? C("#3a2a1e") : C("#2a2420"));
            }
            b.Rect(x, y, 38, 49, C("#d9b44a"));
            b.Speck(x, y, 38, 49, C("#c9a43a"), 0.08f, 21);
            b.TextCentered("SMILE!", x + 19, y + 5, C("#2a2420"), PixelFont.Body);
            // A smiley with a strike through it.
            b.Ring(x + 19f, y + 22f, 5f, 5f, C("#2a2420"));
            b.Set(x + 17, y + 21, C("#2a2420"));
            b.Set(x + 21, y + 21, C("#2a2420"));
            b.HLine(x + 17, x + 21, y + 24, C("#2a2420"));
            b.Line(x + 12, y + 29, x + 26, y + 15, C("#a8342b"));
            b.TextCentered("JUST NOT", x + 19, y + 31, C("#2a2420"), PixelFont.Tiny);
            b.TextCentered("IN YOUR", x + 19, y + 37, C("#2a2420"), PixelFont.Tiny);
            b.TextCentered("PASSPORT", x + 19, y + 43, C("#2a2420"), PixelFont.Tiny);
            if (st.Decor == 1)
            {
                b.Rect(x - 1, y - 1, 4, 3, C("#e0d8bc"));
                b.Rect(x + 35, y - 1, 4, 3, C("#e0d8bc"));
                b.Set(x + 37, y + 48, C("#7a7358"));
                b.Set(x + 36, y + 48, C("#e8d48a"));
            }
        }

        static void Cabinet(PixelBuffer b, RoomState st)
        {
            int x = 309, y = 75;
            Color32 body = st.Decor == 3 ? C("#5a4330") : C("#5c6662");
            b.Rect(x, y, 35, 60, body);
            b.HLine(x, x + 34, y, Pal.Mix(body, C("#ffffff"), 0.15f));
            b.Rect(x + 31, y, 4, 60, M(body, 0.82f));
            for (int i = 0; i < 4; i++)
            {
                int dy = y + 3 + i * 14;
                b.Box(x + 3, dy, 27, 12, M(body, 0.8f));
                b.Rect(x + 13, dy + 5, 7, 2, st.Decor == 3 ? C("#b9a46a") : C("#2b302e"));
                b.Rect(x + 12, dy + 2, 9, 2, C("#d8cfb2"));
            }
            if (st.Decor == 1)
            {
                b.Set(x + 8, y + 33, M(body, 0.7f));
                b.Set(x + 9, y + 34, M(body, 0.7f));
                b.Rect(x + 22, y + 16, 7, 7, C("#e2cd48"));
                b.HLine(x + 23, x + 27, y + 18, C("#8a7a2a"));
                // Folder stack on top.
                b.Rect(x + 2, y - 4, 22, 4, C("#c4a35a"));
                b.HLine(x + 3, x + 22, y - 2, C("#e8e0cc"));
            }
            else
            {
                b.Rect(x + 4, y - 6, 9, 6, C("#2a3a4a"));
                b.Rect(x + 14, y - 5, 8, 5, C("#4a2a2a"));
            }
        }

        internal static void PhotoCorner(PixelBuffer b, RoomState st)
        {
            int s = st.Station;
            // Shop sign over the corner.
            if (st.Decor == 3)
            {
                b.Emissive = st.Power;
                b.Rect(390, 4, 50, 12, st.Power ? C("#f4ead0") : C("#8a8678"));
                b.Emissive = false;
                b.Box(390, 4, 50, 12, C("#2a2622"));
                b.TextCentered("PHOTO", 415, 6, C("#a8342b"), PixelFont.Stamp);
            }
            else
            {
                b.Rect(392, 5, 46, 10, C("#d8cfb2"));
                b.Box(392, 5, 46, 10, C("#5a4a3a"));
                b.TextCentered("PHOTOS", 415, 7, C("#a8342b"), PixelFont.Body);
                if (st.Decor == 1) b.Set(437, 14, C("#7a7358"));
            }

            // Stand / rod (the backdrop surface itself is its own layer).
            if (s == 1)
            {
                b.HLine(364, 460, 22, C("#3a3a36"));
                b.HLine(364, 460, 23, C("#2a2a27"));
                b.VLine(365, 22, 134, C("#2e2e2c"));
                b.VLine(459, 22, 134, C("#2e2e2c"));
            }
            else
            {
                Color32 pole = s == 3 ? C("#1e1e20") : C("#4a4a4c");
                b.Rect(363, 20, 3, 116, pole);
                b.Rect(458, 20, 3, 116, pole);
                b.HLine(360, 368, 135, pole);
                b.HLine(455, 463, 135, pole);
            }

            // Lights.
            if (s == 1)
            {
                // Clamp lamp on a pole, bare bulb.
                b.VLine(468, 30, 134, C("#2e2e2c"));
                b.Line(468, 134, 463, 136, C("#2e2e2c"));
                b.Line(468, 134, 473, 136, C("#2e2e2c"));
                for (int i = 0; i < 7; i++) b.HLine(462 - i / 2, 468 - i, 26 + i, i < 2 ? C("#c4c4bc") : C("#a8a8a0"));
                b.Emissive = st.Power;
                b.Rect(458, 32, 3, 2, st.Power ? C("#fff6d8") : C("#9a9a90"));
                b.Emissive = false;
                b.Line(468, 40, 474, 90, C("#1d1c1a"));
            }
            else if (s == 2)
            {
                // Umbrella light.
                b.VLine(468, 40, 134, C("#3a3a3c"));
                for (int i = 0; i < 9; i++) b.HLine(458 + i / 2, 478 - i / 2, 28 + i, i < 2 ? C("#f0ede4") : C("#dcd8cc"));
                b.Emissive = st.Power;
                b.Rect(466, 37, 4, 2, st.Power ? C("#fffaf0") : C("#9a9a90"));
                b.Emissive = false;
            }
            else
            {
                // Softbox.
                b.VLine(470, 62, 134, C("#2a2a2c"));
                b.Rect(461, 34, 18, 28, C("#202022"));
                b.Emissive = st.Power;
                b.Rect(463, 36, 14, 24, st.Power ? C("#f4f1e6") : C("#8a8a86"));
                b.Emissive = false;
            }

            // Stool (seat top at y 139).
            if (s == 1)
            {
                b.Rect(400, 139, 24, 3, C("#cdc6ae"));
                b.HLine(400, 423, 139, C("#e0dac6"));
                b.Line(402, 142, 399, 158, C("#b8b19a"));
                b.Line(421, 142, 424, 158, C("#b8b19a"));
                b.Line(412, 142, 412, 157, C("#a8a18a"));
                b.Set(408, 140, C("#8a8470"));
            }
            else if (s == 2)
            {
                b.Ellipse(412f, 140f, 12f, 2.5f, C("#3a3a3e"));
                b.VLine(412, 142, 156, C("#8a8a86"));
                b.HLine(404, 420, 156, C("#5a5a58"));
                b.HLine(406, 418, 150, C("#6a6a68"));
            }
            else
            {
                b.Rect(399, 136, 26, 4, C("#3a2e2a"));
                b.HLine(399, 424, 136, C("#5a4a40"));
                b.Rect(401, 140, 22, 3, C("#6a4a30"));
                b.VLine(403, 143, 158, C("#5a3a24"));
                b.VLine(421, 143, 158, C("#5a3a24"));
                b.HLine(403, 421, 152, C("#5a3a24"));
            }
        }

        // ================================================================== backdrop layer

        /// <summary>Backdrop rolls and the hanging surface. Buffer is 96x112 placed at (366, 24).</summary>
        public static void BackdropLayer(PixelBuffer b, RoomState st)
        {
            b.Clear();
            int s = st.Station;
            var order = new[] { Backdrop.White, Backdrop.LightGray, Backdrop.Blue, Backdrop.Red };
            // Rolls along the top.
            for (int i = 0; i < 4; i++)
            {
                Color32 c = Looks.Of(order[i]);
                int rx = 4 + i * 22;
                b.Rect(rx, 1, 20, 5, c);
                b.HLine(rx, rx + 19, 1, Pal.Mix(c, C("#ffffff"), 0.3f));
                b.HLine(rx, rx + 19, 5, M(c, 0.75f));
                b.Rect(rx - 1, 2, 1, 3, C("#2a2a27"));
            }
            Color32 cur = Looks.Of(st.Backdrop);
            int top = 6, bottom = s == 3 ? 111 : 104;
            DrawSurface(b, cur, top, bottom, s);
            if (st.Pending.HasValue)
            {
                int y = top + Mathf.RoundToInt((bottom - top) * Mathf.Clamp01(st.Swap));
                var pc = Looks.Of(st.Pending.Value);
                var tmp = new PixelBuffer(b.W, b.H);
                DrawSurface(tmp, pc, top, bottom, s);
                for (int yy = top; yy < y; yy++)
                    for (int xx = 0; xx < b.W; xx++)
                    {
                        var c = tmp.Get(xx, yy);
                        if (c.a > 0) b.Set(xx, yy, c);
                    }
                b.HLine(2, b.W - 3, y, C("#2a2a27"));
                b.HLine(2, b.W - 3, y + 1, Pal.A(C("#000000"), 0.3f));
            }
        }

        static void DrawSurface(PixelBuffer b, Color32 c, int top, int bottom, int station)
        {
            for (int x = 2; x < b.W - 2; x++)
            {
                int sag = station == 1 ? Mathf.RoundToInt(Mathf.Sin(((x - 2) % 23) / 23f * Mathf.PI) * 2f) : 0;
                int hem = station == 1 ? Mathf.RoundToInt(Mathf.Sin(x * 0.7f) * 1.5f) : 0;
                for (int y = top + sag; y < bottom + hem; y++)
                {
                    Color32 px = c;
                    if (station == 1)
                    {
                        float f = Mathf.Sin(x * 0.42f + Mathf.Sin(y * 0.04f) * 1.6f);
                        px = f > 0.72f ? Pal.Mix(c, C("#ffffff"), 0.1f) : f < -0.6f ? M(c, 0.86f) : c;
                    }
                    else if (station == 3 && y > bottom - 14)
                    {
                        px = Pal.Mix(c, C("#ffffff"), (y - (bottom - 14)) / 40f);
                    }
                    else if (station == 2 && (x < 5 || x > b.W - 7)) px = M(c, 0.93f);
                    b.Set(x, y, px);
                }
            }
            if (station == 1)
            {
                // Clamps holding the sheet up.
                for (int x = 6; x < b.W - 4; x += 22) b.Rect(x, top - 1, 3, 3, C("#6a6a62"));
                b.Rect(b.W - 18, bottom - 14, 7, 5, C("#8e8f8a"));
            }
        }

        // ================================================================== window (animated)

        /// <summary>The window glass: 79x64 placed at (18, 30). Emissive outside, lit blinds/bars.</summary>
        public static void WindowLayer(PixelBuffer b, RoomState st, float time)
        {
            b.Clear();
            Outside(b, 0, 0, 79, 64, st, time, 13);
            // Mullions.
            b.Emissive = false;
            b.Rect(37, 0, 3, 64, C("#2c2a23"));
            b.Rect(0, 30, 79, 3, C("#2c2a23"));
            // Blinds: crooked on Day 1, straight later.
            Color32 bl = st.Decor == 1 ? C("#a69e80") : C("#c8bfa4");
            int down = st.Decor == 1 ? 12 : 8;
            for (int y = 0; y < down; y += 2) b.HLine(0, 78, y, bl);
            if (st.Decor == 1)
            {
                b.Line(0, 13, 20, 20, bl);
                b.Line(0, 14, 19, 21, M(bl, 0.8f));
            }
            b.VLine(75, 0, 30, C("#8a826a"));
            // Window decal, painted for the street, so it reads backwards from in here.
            b.Text("PASSPORT PHOTOS", 9, 54, Pal.A(C("#f0c878"), 0.75f), PixelFont.Tiny, true);
        }

        /// <summary>The street outside, used by the window and the door glass.</summary>
        static void Outside(PixelBuffer b, int x0, int y0, int w, int h, RoomState st, float time, int seed)
        {
            b.Emissive = true;
            var sky = st.Sky;
            bool night = sky.Daylight < 0.35f;
            for (int y = 0; y < h; y++) b.HLine(x0, x0 + w - 1, y0 + y, Color.Lerp(sky.SkyTop, sky.SkyBottom, y / (float)h));
            var r = new PixelRandom(seed);
            Color32 bld = night ? C("#0a0f18") : (Color32)Color.Lerp(Ui.Hex("#3a3f48"), Ui.Hex("#7a7f86"), sky.Daylight);
            Color32 lit = C("#d6a24c");
            int[] bx = { 0, 14, 22, 40, 52, 70, 90 };
            int[] tops = { 16, 9, 20, 12, 18, 10 };
            for (int i = 0; i < bx.Length - 1; i++)
            {
                int a = x0 + bx[i], bb = Mathf.Min(x0 + w, x0 + bx[i + 1]);
                if (a >= x0 + w) break;
                int top = y0 + tops[i % tops.Length];
                b.Rect(a, top, bb - a, y0 + h - top, bld);
                for (int yy = top + 3; yy < y0 + h - 14; yy += 5)
                    for (int xx = a + 2; xx < bb - 2; xx += 4)
                        b.Rect(xx, yy, 2, 2, r.Next() < (night ? 0.4f : 0.05f) ? lit : M(bld, night ? 1.6f : 0.85f));
            }
            // Street + wet reflections.
            int street = y0 + h - 11;
            Color32 road = night ? C("#101823") : (Color32)Color.Lerp(Ui.Hex("#3a3c40"), Ui.Hex("#7b7d7a"), sky.Daylight);
            b.Rect(x0, street, w, 11, road);
            b.HLine(x0, x0 + w - 1, street, M(road, 1.3f));
            // Street lamp.
            int lx = x0 + w - 12;
            b.VLine(lx, y0 + 8, y0 + h - 4, C("#06090f"));
            b.HLine(lx - 5, lx, y0 + 8, C("#06090f"));
            if (sky.StreetLights)
            {
                b.Rect(lx - 6, y0 + 9, 3, 1, C("#ffd98a"));
                for (int i = 0; i < 14; i++)
                {
                    float an = r.Next() * 6.28f, dd = 2f + r.Next() * 4f;
                    b.Set(lx - 5 + Mathf.RoundToInt(Mathf.Cos(an) * dd), y0 + 11 + Mathf.RoundToInt(Mathf.Sin(an) * dd * 0.7f), C("#6a5636"));
                }
                for (int yy = street + 2; yy < y0 + h; yy += 2) b.Set(lx - 5 + (yy % 3), yy, C("#b07a3a"));
            }
            // Parked car.
            Color32 car = night ? C("#06080d") : C("#4a5a6a");
            b.Rect(x0 + 4, street + 3, 18, 5, car);
            b.Rect(x0 + 8, street, 10, 3, car);
            if (night) b.Set(x0 + 21, street + 4, C("#c43a2a"));

            // Weather.
            if (st.Weather == Weather.Rain)
            {
                for (int i = 0; i < 46; i++)
                {
                    float sp = 30f + (i * 37 % 20);
                    int xx = x0 + (i * 53 % w);
                    int yy = y0 + Mathf.FloorToInt((time * sp + i * 17f) % h);
                    b.Set(xx, yy, C("#6a86a8"));
                    b.Set(xx - 1, yy + 1, C("#4c6585"));
                }
                for (int i = 0; i < 10; i++) b.Set(x0 + (i * 29 % w), y0 + (i * 13 % h), C("#9ab4d0"));
            }
            else if (st.Weather == Weather.Snow)
            {
                for (int i = 0; i < 30; i++)
                {
                    float sp = 6f + (i * 7 % 6);
                    int xx = x0 + Mathf.RoundToInt((i * 41 % w) + Mathf.Sin(time * 1.3f + i) * 2f);
                    int yy = y0 + Mathf.FloorToInt((time * sp + i * 23f) % h);
                    b.Set(xx, yy, C("#f0f4f8"));
                }
                b.Rect(x0, street - 1, w, 2, C("#e0e6ea"));
            }
            b.Emissive = false;
        }

        // ================================================================== door (animated)

        /// <summary>The door, its frame, the bell and the sign: 60x124 placed at (160, 12).</summary>
        static PixelBuffer _doorGlass;

        public static void DoorLayer(PixelBuffer b, RoomState st, float open, float bellSwing, float time)
        {
            b.Clear();
            // Bell.
            int bx = 30 + Mathf.RoundToInt(bellSwing);
            b.VLine(30, 0, 3, C("#2a2420"));
            b.Rect(bx - 2, 3, 5, 4, C("#b0913e"));
            b.HLine(bx - 3, bx + 3, 7, C("#8a6a2a"));
            b.Set(bx - 1, 4, C("#e0c06a"));

            // Frame.
            b.Rect(0, 9, 60, 115, C("#2f271e"));
            // Street seen through the doorway.
            Outside(b, 3, 12, 54, 112, st, time, 29);
            // The door swings inward (to the left as we look at it).
            float sw = Mathf.Sin(Mathf.Clamp01(open) * Mathf.PI * 0.5f);
            int pw = Mathf.Max(8, Mathf.RoundToInt(54 * (1f - 0.75f * sw)));
            Color32 wood = st.Decor == 3 ? C("#4e3a2a") : C("#5e4b36");
            b.Rect(3, 12, pw, 112, wood);
            b.VLine(3 + pw - 1, 12, 123, M(wood, 0.75f));
            // Glass upper panel shows the street; lower panels.
            int gx = 3 + Mathf.RoundToInt(pw * 0.14f), gw = Mathf.Max(2, Mathf.RoundToInt(pw * 0.72f));
            // Reused every frame while it rains, so keep one buffer around.
            var glass = _doorGlass ??= new PixelBuffer(gw, 34);
            glass.Resize(gw, 34);
            Outside(glass, 0, 0, gw, 34, st, time, 41);
            for (int y = 0; y < 34; y++)
                for (int x = 0; x < gw; x++)
                {
                    var c = glass.Get(x, y);
                    if (((x + y) % 3) == 0) c = Pal.Mix(c, C("#7f97ad"), 0.3f);
                    b.Emissive = true;
                    b.Set(gx + x, 16 + y, c);
                }
            b.Emissive = false;
            b.Box(gx - 1, 15, gw + 2, 36, M(wood, 0.7f));
            if (pw > 20)
            {
                b.Box(gx, 58, gw, 26, M(wood, 0.8f));
                b.Box(gx, 88, gw, 26, M(wood, 0.8f));
                b.Rect(3 + pw - 6, 74, 3, 3, C("#c9a24a"));
                b.Set(3 + pw - 6, 74, C("#ead08a"));
                b.Rect(3, 116, pw, 7, C("#6d6e66"));
                b.HLine(3, 3 + pw - 1, 116, C("#8a8b82"));
            }

            // The OPEN / CLOSED sign hanging inside the glass (stage 694,250 → art 173,62).
            if (pw > 30) Sign(b, st, 13, 50, time);
        }

        static void Sign(PixelBuffer b, RoomState st, int x, int y, float time)
        {
            b.Line(x + 4, y - 3, x + 16, y - 6, C("#2a2420"));
            b.Line(x + 28, y - 3, x + 16, y - 6, C("#2a2420"));
            if (st.Open && st.Power)
            {
                // Neon: flickers now and then.
                bool flick = Mathf.Repeat(time, 7.3f) < 0.12f;
                Color32 neon = flick ? C("#8a3a5a") : C("#ff6f9a");
                b.Rect(x - 2, y, 37, 13, C("#1a1418"));
                b.Emissive = !flick;
                b.Box(x - 1, y + 1, 35, 11, flick ? C("#5a2a3a") : C("#c84a7a"));
                b.TextCentered("OPEN", x + 17, y + 3, neon, PixelFont.Body);
                b.Emissive = false;
            }
            else
            {
                // No power: the neon is dead, so the flip card does the job.
                b.Rect(x - 2, y, 37, 13, C("#f0e8d4"));
                b.Box(x - 2, y, 37, 13, st.Open ? C("#3a6a2a") : C("#a8342b"));
                b.TextCentered(st.Open ? "OPEN" : "CLOSED", x + 16, y + 3, st.Open ? C("#3a6a2a") : C("#a8342b"), PixelFont.Body);
            }
        }

        // ================================================================== clock

        /// <summary>Wall clock, 32x32 placed at (232, 15).</summary>
        public static void ClockLayer(PixelBuffer b, float minute, int decor)
        {
            b.Clear();
            float cx = 16f, cy = 16f;
            b.Ellipse(cx, cy, 15f, 15f, decor == 3 ? C("#6b5434") : C("#2a2723"));
            b.Ellipse(cx, cy, 12.5f, 12.5f, C("#e8e0cf"));
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                b.Set(Mathf.RoundToInt(cx - 0.5f + Mathf.Sin(a) * 10.5f), Mathf.RoundToInt(cy - 0.5f - Mathf.Cos(a) * 10.5f), i % 3 == 0 ? C("#2a241c") : C("#8a8070"));
            }
            float ha = (minute / 60f % 12f) / 12f * Mathf.PI * 2f;
            float ma = (minute % 60f) / 60f * Mathf.PI * 2f;
            b.Line(16, 16, 16 + Mathf.RoundToInt(Mathf.Sin(ha) * 6f), 16 - Mathf.RoundToInt(Mathf.Cos(ha) * 6f), C("#1f1a16"));
            b.Line(15, 16, 15 + Mathf.RoundToInt(Mathf.Sin(ha) * 6f), 16 - Mathf.RoundToInt(Mathf.Cos(ha) * 6f), C("#1f1a16"));
            b.Line(16, 16, 16 + Mathf.RoundToInt(Mathf.Sin(ma) * 9.5f), 16 - Mathf.RoundToInt(Mathf.Cos(ma) * 9.5f), C("#1f1a16"));
            b.Rect(15, 15, 2, 2, C("#b0442f"));
            if (decor == 1)
            {
                // Cracked glass.
                b.Line(9, 8, 23, 22, Pal.A(C("#9a9480"), 0.7f));
                b.Line(19, 6, 16, 13, Pal.A(C("#9a9480"), 0.6f));
            }
        }

        // ================================================================== counter, desk, props

        public static void Front(PixelBuffer b, RoomState st)
        {
            b.Clear();
            int d = st.Decor;
            Tripod(b, st);

            // Counter (stage 690..770 → art 172..192).
            Color32 top = d == 3 ? C("#3a2a20") : d == 2 ? C("#6a4e34") : C("#7a5a3b");
            Color32 front = d == 3 ? C("#2e2119") : d == 2 ? C("#4e3a28") : C("#55402c");
            b.Rect(0, 172, W, 5, top);
            b.HLine(0, W - 1, 172, Pal.Mix(top, C("#ffffff"), 0.15f));
            b.HLine(0, W - 1, 176, d == 3 ? C("#a88a4a") : M(top, 0.7f));
            b.Rect(0, 177, W, 16, front);
            for (int x = 6; x < W; x += 16) b.VLine(x, 178, 192, M(front, 0.82f));
            if (d == 1)
            {
                b.Speck(0, 172, W, 5, M(top, 0.82f), 0.12f, 33);
                b.Rect(206, 179, 44, 10, C("#d6cdb2"));
                b.Text("NO REFUNDS", 208, 181, C("#2a211b"), PixelFont.Tiny);
                b.Rect(205, 178, 3, 2, C("#c8c0a4"));
                b.Rect(248, 178, 3, 2, C("#c8c0a4"));
            }
            else if (d == 3)
            {
                b.Rect(212, 180, 56, 8, C("#a88a4a"));
                b.Text("NO REFUNDS", 220, 181, C("#3a2a14"), PixelFont.Tiny);
            }
            // Service bell.
            b.Ellipse(256f, 170f, 3f, 2.5f, C("#c9a24a"));
            b.HLine(253, 259, 172, C("#8a6a2a"));
            b.Set(255, 168, C("#f0d88a"));

            // Desk (stage 770.. → art 192..).
            Color32 desk = d == 3 ? C("#4a3424") : C("#5a4430");
            b.Rect(0, 193, W, 72, desk);
            b.HLine(0, W - 1, 193, Pal.Mix(desk, C("#ffffff"), 0.12f));
            b.Rect(0, 265, W, 5, M(desk, 0.62f));
            b.HLine(0, W - 1, 265, M(desk, 0.75f));
            if (d == 1)
            {
                b.Speck(0, 194, W, 70, M(desk, 0.9f), 0.12f, 21);
                b.Ring(272f, 262f, 4f, 2f, M(desk, 0.8f));
                b.Ring(196f, 258f, 4f, 2f, M(desk, 0.82f));
            }
            // Masking-tape labels for the paper piles.
            Tape(b, 176, 195, "SHEETS");
            Tape(b, 268, 195, "BOOKLETS");

            Computer(b, st);
            Binder(b, st);
            PrinterBody(b, st);
            Cashbox(b, st);
            Lamp(b, st);

            // Mug + pens.
            Color32 mug = d == 3 ? C("#2a3a4a") : C("#d8d0bc");
            b.Rect(412, 255, 7, 9, mug);
            b.HLine(412, 418, 255, C("#3a2416"));
            b.Ring(420f, 259f, 1.5f, 2f, mug);
            if (d == 1) b.Set(414, 259, C("#8a3a2a"));
            b.Line(170, 262, 173, 255, C("#2a3a6a"));
            b.Line(172, 263, 175, 257, C("#a8342b"));
        }

        static void Tape(PixelBuffer b, int x, int y, string text)
        {
            int w = PixelFont.Tiny.Measure(text) + 4;
            b.Rect(x, y, w, 7, C("#d8cca0"));
            b.Set(x, y, C("#c8bc90"));
            b.Set(x + w - 1, y + 6, C("#c8bc90"));
            b.Text(text, x + 2, y + 1, C("#3a3020"), PixelFont.Tiny);
        }

        internal static void Tripod(PixelBuffer b, RoomState st)
        {
            // Legs from the head (381,138) to the floor; the camera sits on top.
            Color32 leg = st.Camera == 3 ? C("#141414") : C("#2a2a28");
            b.Line(381, 138, 368, 175, leg);
            b.Line(381, 138, 394, 175, leg);
            b.Line(381, 138, 382, 175, M(leg, 0.8f));
            b.Line(380, 138, 367, 175, leg);
            b.Rect(378, 134, 7, 5, M(leg, 0.9f));
            switch (st.Camera)
            {
                case 1:
                    b.Rect(370, 122, 22, 13, C("#3a3a38"));
                    b.HLine(370, 391, 122, C("#5a5a56"));
                    b.Rect(388, 122, 4, 13, C("#2a2a28"));
                    b.Ellipse(380f, 129f, 5f, 5f, C("#1e1e1d"));
                    b.Ellipse(380f, 129f, 3f, 3f, C("#2e3e50"));
                    b.Set(379, 127, C("#9ab8d8"));
                    b.Rect(372, 118, 7, 4, C("#b9b9b0"));
                    b.HLine(370, 391, 131, C("#8a2a22"));
                    for (int i = 0; i < 6; i++) b.Rect(385 + (i % 2), 123 + i * 2, 5, 1, i % 2 == 0 ? C("#8e8f8a") : C("#a3a49e"));
                    break;
                case 2:
                    b.Rect(368, 121, 24, 15, C("#1a1a1c"));
                    b.HLine(368, 391, 121, C("#3a3a40"));
                    b.Rect(374, 117, 10, 4, C("#1a1a1c"));
                    b.Rect(376, 125, 14, 11, C("#141416"));
                    b.Ellipse(383f, 130f, 5f, 5f, C("#101012"));
                    b.Ellipse(383f, 130f, 3f, 3f, C("#3a5a8a"));
                    b.Set(382, 128, C("#a8c8f0"));
                    break;
                default:
                    b.Rect(366, 120, 26, 16, C("#18181a"));
                    b.HLine(366, 391, 120, C("#3a3a3e"));
                    b.Rect(371, 115, 12, 5, C("#18181a"));
                    b.Ellipse(382f, 129f, 7f, 7f, C("#0e0e10"));
                    b.Ring(382f, 129f, 6f, 6f, C("#a83a2a"));
                    b.Ellipse(382f, 129f, 3.5f, 3.5f, C("#2a4a7a"));
                    b.Set(380, 127, C("#a8c8f0"));
                    break;
            }
            // Record light.
            b.Emissive = st.Recording && st.Power;
            b.Set(389, 119, st.Recording && st.Power ? C("#ff4030") : C("#552222"));
            b.Emissive = false;
        }

        internal static void Computer(PixelBuffer b, RoomState st)
        {
            switch (st.Computer)
            {
                case 1:
                    // Beige CRT.
                    b.Rect(14, 197, 86, 60, C("#b3a888"));
                    b.HLine(15, 98, 197, C("#c8bea0"));
                    b.Rect(94, 197, 6, 60, C("#968c6f"));
                    b.Rect(14, 256, 86, 2, C("#7d7560"));
                    // The screen's frame ends a row higher so the badge fits the bezel lip (rows 251-255).
                    b.Rect(20, 201, 72, 49, C("#8a8269"));
                    b.Box(20, 201, 72, 49, C("#6e6753"));
                    b.Rect(38, 258, 38, 4, C("#8f866b"));
                    b.Rect(30, 262, 54, 3, C("#a39a7e"));
                    b.Text("BEIGE 98", 24, 251, C("#6e6753"), PixelFont.Tiny);
                    // Sticky note on the bezel.
                    b.Rect(86, 194, 10, 9, C("#e2cd48"));
                    b.HLine(87, 94, 197, C("#8a7a2a"));
                    b.HLine(87, 92, 199, C("#8a7a2a"));
                    break;
                case 2:
                    b.Rect(14, 199, 86, 52, C("#1a1a1c"));
                    b.HLine(14, 99, 199, C("#3a3a40"));
                    b.Rect(50, 251, 14, 10, C("#2a2a2e"));
                    b.Rect(38, 261, 38, 3, C("#2a2a2e"));
                    break;
                default:
                    b.Rect(6, 199, 50, 46, C("#18181b"));
                    b.Rect(58, 199, 50, 46, C("#18181b"));
                    b.HLine(6, 107, 199, C("#3a3a40"));
                    b.Rect(28, 245, 6, 14, C("#2a2a2e"));
                    b.Rect(80, 245, 6, 14, C("#2a2a2e"));
                    b.Rect(18, 259, 26, 3, C("#2a2a2e"));
                    b.Rect(70, 259, 26, 3, C("#2a2a2e"));
                    break;
            }
            // Keyboard.
            Color32 key = st.Computer == 1 ? C("#c8bfa2") : C("#3e3e44");
            Color32 kb = st.Computer == 1 ? C("#b8ae90") : C("#26262a");
            b.Rect(28, 264, 58, 6, kb);
            for (int x = 30; x < 84; x += 3) b.Rect(x, 265, 2, 2, key);
        }

        /// <summary>Where the screen layer goes for the current computer tier.</summary>
        public static RectInt ScreenRect(int tier) => tier switch
        {
            1 => new RectInt(24, 205, 64, 42),
            2 => new RectInt(17, 202, 80, 46),
            _ => new RectInt(9, 202, 96, 40),
        };

        /// <summary>The computer screen: status lines, messages blinking. Size from <see cref="ScreenRect"/>.</summary>
        public static void ScreenLayer(PixelBuffer b, RoomState st, string title, string line1, string line2, bool alert, bool blink)
        {
            b.Clear();
            int w = b.W, h = b.H;
            if (!st.Power)
            {
                b.Rect(0, 0, w, h, C("#050605"));
                return;
            }
            b.Emissive = true;
            Color32 bg = st.Computer == 1 ? C("#1d5656") : C("#20354a");
            b.Rect(0, 0, w, h, bg);
            if (st.Computer == 3)
            {
                b.Emissive = false;
                b.Rect(47, 0, 2, h, C("#18181b"));
                b.Emissive = true;
            }
            // Title bar.
            b.Rect(0, 0, st.Computer == 3 ? 47 : w, 7, C("#4b3f6e"));
            int tw = (st.Computer == 3 ? 47 : w) - 4;
            b.Text(PixelFont.Tiny.Fit(title, tw), 2, 1, C("#ece6d2"), PixelFont.Tiny);
            line1 = PixelFont.Tiny.Fit(line1, tw - 2);
            line2 = PixelFont.Tiny.Fit(line2, tw);
            int tx = 3, ty = 10;
            Color32 ink = C("#d8f0d0");
            if (alert)
            {
                if (blink)
                {
                    b.Rect(2, ty - 1, (st.Computer == 3 ? 44 : w - 4), 9, C("#f0b23a"));
                    b.Text(line1, tx + 1, ty + 1, C("#2a1c08"), PixelFont.Tiny);
                }
                else b.Text(line1, tx + 1, ty + 1, C("#f0b23a"), PixelFont.Tiny);
            }
            else b.Text(line1, tx, ty + 1, ink, PixelFont.Tiny);
            b.Text(line2, tx, ty + 10, ink, PixelFont.Tiny);
            if (st.Computer == 3)
            {
                b.Rect(51, 0, w - 51, 7, C("#2f5a4a"));
                b.Text("MSG", 53, 1, C("#ece6d2"), PixelFont.Tiny);
                for (int i = 0; i < 3; i++) b.HLine(53, w - 4, 11 + i * 6, C("#8aa0b8"));
            }
            // Taskbar.
            b.Rect(0, h - 5, w, 5, C("#bdb49a"));
            b.Rect(1, h - 4, 8, 3, C("#8a3a3a"));
            // Scanlines on the CRT.
            if (st.Computer == 1)
                for (int y = 0; y < h; y += 2)
                    for (int x = 0; x < w; x++)
                    {
                        var c = b.Get(x, y);
                        if (c.a > 0) b.Set(x, y, M(c, 0.86f));
                    }
            b.Emissive = false;
        }

        static void Binder(PixelBuffer b, RoomState st)
        {
            int x = 113, y = 203;
            int extra = Mathf.Min(6, st.Tabs.Count - 1);
            // Page block visible at the bottom grows as the binder fills up.
            b.Rect(x + 3, y + 58, 50, 2 + extra, C("#e6dcc4"));
            for (int i = 0; i < extra; i += 2) b.HLine(x + 3, x + 52, y + 59 + i, C("#c8bc9e"));
            b.Rect(x, y, 54, 58, C("#2d4a3a"));
            b.Rect(x, y, 5, 58, C("#1d3127"));
            b.HLine(x, x + 53, y, C("#3d5a48"));
            b.Rect(x + 12, y + 10, 36, 13, C("#d8cfb2"));
            b.Text("HANDBOOK", x + 14, y + 14, C("#2a241c"), PixelFont.Tiny);
            b.Text("RULES", x + 20, y + 30, C("#a8b4a0"), PixelFont.Tiny);
            // Sticky notes poking out of the top.
            b.Rect(x + 30, y - 3, 6, 4, C("#e2cd48"));
            b.Rect(x + 40, y - 2, 5, 3, C("#e08aa0"));
            // A tab per owned section on the right edge.
            int ty = y + 2;
            foreach (var (id, outdated) in st.Tabs)
            {
                if (ty > y + 52) break;
                Color32 col = outdated ? C("#d0503f") : TabColor(id);
                b.Rect(x + 54, ty, 4, 5, col);
                b.Set(x + 57, ty + 4, M(col, 0.8f));
                if (outdated) b.Set(x + 55, ty + 2, C("#ffffff"));
                ty += 6;
            }
        }

        public static Color32 TabColor(string id) => id switch
        {
            "BASICS" => C("#e8e2d0"),
            Countries.France => C("#4a7ac8"),
            Countries.Turkey => C("#c84a3a"),
            Countries.Germany => C("#e0c04a"),
            Countries.Italy => C("#5aa05a"),
            Countries.UnitedKingdom => C("#3a4a8a"),
            Countries.UnitedStates => C("#a05ac8"),
            Countries.Japan => C("#f0ece4"),
            Countries.SouthKorea => C("#5a8ac8"),
            Countries.Emirates => C("#3a8a4a"),
            Countries.Iran => C("#8ac85a"),
            _ => C("#e8d48a"),
        };

        internal static void PrinterBody(PixelBuffer b, RoomState st)
        {
            int x = 342, y = 196;
            switch (st.Printer)
            {
                case 1:
                    b.Rect(x + 6, y + 1, 56, 6, C("#8f8a78"));
                    b.Rect(x + 9, y - 2, 50, 4, C("#ddd5bd"));
                    b.Rect(x, y + 7, 70, 42, C("#a8a390"));
                    b.HLine(x, x + 69, y + 7, C("#bdb8a4"));
                    b.Rect(x + 62, y + 7, 8, 42, C("#8f8a78"));
                    b.HLine(x, x + 69, y + 48, C("#6f6b5d"));
                    b.Rect(x + 8, y + 26, 54, 3, C("#2a2824"));
                    // Duct tape across the corner.
                    for (int i = 0; i < 12; i++) b.Rect(x + 1 + i, y + 8 + i, 5, 2, i % 3 != 0 ? C("#8e8f8a") : C("#a3a49e"));
                    // HIT IT IF IT STOPS. And underneath, in different handwriting: BUT SOFTLY.
                    // 5-pixel letters on a 6-pixel pitch, everything above the paper slot (y + 26).
                    b.Rect(x + 44, y + 7, 25, 18, C("#e2cd48"));
                    b.HLine(x + 44, x + 68, y + 25, C("#b8a33a"));
                    b.Text("HIT IT", x + 46, y + 8, C("#2a2418"), PixelFont.Tiny);
                    b.Text("IF IT", x + 46, y + 14, C("#2a2418"), PixelFont.Tiny);
                    b.Text("STOPS", x + 46, y + 20, C("#2a2418"), PixelFont.Tiny);
                    b.Rect(x + 18, y + 12, 25, 12, C("#e08aa0"));
                    b.HLine(x + 18, x + 42, y + 24, C("#b8687a"));
                    b.Text("BUT", x + 25, y + 13, C("#3a1a22"), PixelFont.Tiny);
                    b.Text("SOFTLY", x + 19, y + 19, C("#3a1a22"), PixelFont.Tiny);
                    break;
                case 2:
                    b.Rect(x + 2, y + 2, 66, 47, C("#c9c6bd"));
                    b.HLine(x + 2, x + 67, y + 2, C("#dedbd2"));
                    b.Rect(x + 60, y + 2, 8, 47, C("#a8a59c"));
                    b.Rect(x + 8, y + 26, 54, 3, C("#2a2824"));
                    b.Rect(x + 6, y + 6, 22, 10, C("#3a3a3a"));
                    break;
                default:
                    b.Rect(x, y - 4, 72, 53, C("#2a2a2e"));
                    b.HLine(x, x + 71, y - 4, C("#4a4a50"));
                    b.Rect(x + 64, y - 4, 8, 53, C("#1e1e22"));
                    b.Rect(x + 8, y + 26, 56, 3, C("#0e0e10"));
                    b.Rect(x + 5, y, 26, 11, C("#101820"));
                    break;
            }
            // Output tray (stage 1370,1000 → art 342,250).
            b.Rect(x, 250, 70, 3, st.Printer == 3 ? C("#2a2a2e") : C("#8f8a78"));
            b.Rect(x + 2, 253, 66, 12, st.Printer == 3 ? C("#202024") : C("#6f6b5d"));
        }

        /// <summary>Printer status overlay: 72x45 placed at (342, 196), the printer body's origin.</summary>
        public static void PrinterLayer(PixelBuffer b, RoomState st, string status, string light, float progress, bool printing, float time)
        {
            b.Clear();
            // Paper sliding out of the slot (slot is at body y 26..28).
            if (printing)
            {
                int len = 2 + Mathf.RoundToInt(progress * 12f);
                b.Rect(16, 29, 38, len, C("#f3eedf"));
                for (int i = 1; i < len; i += 2) b.HLine(19, 46, 29 + i, C("#9a927c"));
            }
            Color32 led = light switch
            {
                "idle" => C("#6fe06a"),
                "busy" => Mathf.Repeat(time, 0.6f) < 0.3f ? C("#f0b040") : C("#7a5a20"),
                "jam" => Mathf.Repeat(time, 0.4f) < 0.2f ? C("#ff4030") : C("#7a2018"),
                _ => C("#2a2a2a"),
            };
            if (st.Printer == 1)
            {
                // The cheap printer has a paper label someone writes the status on.
                b.Emissive = light != "off";
                b.Rect(4, 31, 2, 2, led);
                b.Emissive = false;
                if (!printing)
                {
                    b.Rect(18, 33, 48, 7, C("#d6cdb2"));
                    b.Text(PixelFont.Tiny.Fit(status, 46), 19, 34, light == "jam" ? C("#a8342b") : C("#2a241c"), PixelFont.Tiny);
                }
                return;
            }
            int x = st.Printer == 2 ? 6 : 5, y = st.Printer == 2 ? 6 : 0, w = st.Printer == 2 ? 22 : 26, h = st.Printer == 2 ? 10 : 11;
            b.Emissive = light != "off";
            b.Rect(x, y, w, h, light == "off" ? C("#1a1e1a") : light == "jam" ? C("#5a1a14") : C("#1a2a3e"));
            b.Text(PixelFont.Tiny.Fit(status, w - 2), x + 1, y + 3, light == "jam" ? C("#ffb0a0") : C("#7fd0ff"), PixelFont.Tiny);
            b.Rect(st.Printer == 2 ? 30 : 36, st.Printer == 2 ? 8 : 4, 2, 2, led);
            b.Emissive = false;
        }

        static void Cashbox(PixelBuffer b, RoomState st)
        {
            int x = 421, y = 225;
            b.Rect(x, y, 50, 40, C("#46505a"));
            b.HLine(x, x + 49, y, C("#5f6a74"));
            b.Rect(x + 44, y, 6, 40, C("#353d45"));
            b.Rect(x, y + 37, 50, 3, C("#2f363d"));
            b.Text("CASH", x + 12, y + 3, C("#c8d0d8"), PixelFont.Tiny);
            b.Rect(x + 5, y + 11, 38, 12, C("#2a3238"));
            b.Rect(x + 18, y + 29, 12, 3, C("#2a3238"));
        }

        /// <summary>Cash LCD: 36x10 placed at (422, 237).</summary>
        public static void LcdLayer(PixelBuffer b, string text, bool power)
        {
            b.Clear();
            b.Emissive = power;
            b.Rect(0, 0, b.W, b.H, power ? C("#9bb59a") : C("#4a564a"));
            if (power) b.TextCentered(PixelFont.Tiny.Fit(text, b.W - 2), b.W / 2, 3, C("#1c2a1c"), PixelFont.Tiny);
            b.Emissive = false;
        }

        static void Lamp(PixelBuffer b, RoomState st)
        {
            Color32 lc = st.Decor == 3 ? C("#a88a4a") : C("#4a6a52");
            Color32 arm = st.Decor == 3 ? C("#7a6234") : C("#2c3a2e");
            b.Rect(442, 206, 16, 5, lc);
            b.HLine(442, 457, 206, Pal.Mix(lc, C("#ffffff"), 0.2f));
            b.Line(450, 206, 455, 168, arm);
            b.Line(451, 206, 456, 168, arm);
            b.Line(455, 168, 440, 156, arm);
            for (int i = 0; i < 9; i++) b.HLine(426 - i / 2, 440 + i / 3, 150 + i, i < 2 ? Pal.Mix(lc, C("#ffffff"), 0.2f) : lc);
            b.Emissive = st.Power;
            b.HLine(423, 440, 159, st.Power ? C("#fff2c0") : C("#7a7468"));
            b.HLine(425, 438, 160, st.Power ? C("#ffe39a") : C("#6a6458"));
            b.Emissive = false;
        }

        // ================================================================== lights

        /// <summary>Configures the room lights for the moment (time of day, weather, power, kit).</summary>
        public static void Lights(LightMap map, RoomState st)
        {
            var sky = st.Sky;
            float day = sky.Daylight;
            map.Lamps.Clear();
            var nightAmb = new Color(0.24f, 0.25f, 0.32f);
            var dayAmb = new Color(0.66f, 0.66f, 0.66f) * Color.Lerp(Color.white, sky.WindowLight, 0.35f);
            map.Ambient = Color.Lerp(nightAmb, dayAmb, day);
            if (!st.Power) map.Ambient *= 0.65f;

            // Daylight through the window and the door glass.
            map.Lamps.Add(new LightMap.Lamp(57f, 62f, 170f, 150f, sky.WindowLight, 0.25f + 0.85f * day, 1.1f));
            map.Lamps.Add(new LightMap.Lamp(190f, 60f, 60f, 90f, sky.WindowLight, 0.15f + 0.4f * day, 1.3f));
            if (sky.StreetLights) map.Lamps.Add(new LightMap.Lamp(70f, 40f, 70f, 60f, Ui.Hex("#c8904a"), 0.35f, 1.4f));
            if (!st.Power) return;

            if (st.Decor < 3)
                map.Lamps.Add(new LightMap.Lamp(240f, 6f, 270f, 200f, new Color(0.5f, 0.56f, 0.42f), 0.85f - day * 0.35f, 1.3f));
            else
            {
                map.Lamps.Add(new LightMap.Lamp(180f, 12f, 150f, 130f, new Color(0.95f, 0.72f, 0.45f), 0.8f - day * 0.3f, 1.4f));
                map.Lamps.Add(new LightMap.Lamp(300f, 12f, 150f, 130f, new Color(0.95f, 0.72f, 0.45f), 0.8f - day * 0.3f, 1.4f));
            }
            // The counter customer stands in this pool (warmer and stronger under the pendants).
            map.Lamps.Add(st.Decor < 3
                ? new LightMap.Lamp(240f, 80f, 80f, 70f, new Color(0.5f, 0.52f, 0.42f), 0.45f, 1.4f)
                : new LightMap.Lamp(240f, 80f, 90f, 80f, new Color(0.8f, 0.66f, 0.48f), 0.65f, 1.3f));
            // Desk lamp, warm, onto the right side of the desk.
            map.Lamps.Add(new LightMap.Lamp(432f, 200f, 110f, 60f, new Color(1.25f, 0.86f, 0.48f), 1f - day * 0.4f, 1.2f));
            map.Lamps.Add(new LightMap.Lamp(432f, 166f, 40f, 24f, new Color(1f, 0.75f, 0.4f), 0.5f, 1.4f));
            // Screen glow.
            var screen = st.Computer == 1 ? new Color(0.22f, 0.55f, 0.58f) : new Color(0.25f, 0.4f, 0.6f);
            map.Lamps.Add(new LightMap.Lamp(57f, 226f, 80f, 60f, screen, 0.85f - day * 0.4f, 1.5f));
            // Photo corner: cheap lamp, umbrella, or studio softboxes.
            float photoK = st.Station == 1 ? 0.8f : st.Station == 2 ? 0.95f : 1.15f;
            map.Lamps.Add(new LightMap.Lamp(456f, 40f, 100f, 120f, new Color(0.85f, 0.82f, 0.7f), photoK, 1.4f));
            if (st.Station == 3) map.Lamps.Add(new LightMap.Lamp(366f, 50f, 80f, 100f, new Color(0.9f, 0.9f, 0.86f), 0.6f, 1.4f));
            // Neon OPEN sign at night.
            if (st.Open && day < 0.6f) map.Lamps.Add(new LightMap.Lamp(190f, 68f, 40f, 30f, new Color(0.6f, 0.18f, 0.32f), 0.5f, 1.4f));
        }
    }
}
