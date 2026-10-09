using LegitEnough.Sim;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>Pixel art used by menus and screens: the logo stamp, country flags, app icons.</summary>
    public static class UiArt
    {
        static Color32 C(string hex) => Pal.Hex(hex);

        /// <summary>
        /// LEGIT as a crooked rubber stamp with ink drop-outs; ENOUGH typed underneath,
        /// offset, one letter a pixel low. 70x42.
        /// </summary>
        public static PixelBuffer Logo(bool onDark = true)
        {
            var stamp = new PixelBuffer(70, 42);
            var red = C("#c0392b");
            stamp.Box(9, 3, 47, 18, red);
            stamp.Box(10, 4, 45, 16, red);
            PixelFont.Stamp.Draw(stamp, "LEGIT", 15, 8, red, false);
            Wear(stamp, 3, 0.04f);
            // Stamped crooked: a pixel shear keeps the letter strokes whole (a rotation breaks them).
            var b = Shear(stamp, -0.07f);

            var word = new PixelBuffer(70, 42);
            var ink = onDark ? C("#ece3c8") : C("#1e1a16");
            int x = 20;
            string s = "ENOUGH";
            for (int i = 0; i < s.Length; i++)
            {
                int dy = i == 2 ? 1 : i == 5 ? -1 : 0;
                x += PixelFont.Stamp.Draw(word, s[i].ToString(), x, 24 + dy, ink, false) + 1;
            }
            if (onDark)
            {
                var shadow = new PixelBuffer(70, 42);
                shadow.Blit(word, 1, 1);
                for (int i = 0; i < shadow.Px.Length; i++)
                    if (shadow.Px[i].a > 0) shadow.Px[i] = C("#16120f");
                b.Blit(shadow, 0, 0);
            }
            b.Blit(word, 0, 0);
            return b;
        }

        /// <summary>Small square "LE" mark for icons. 20x20.</summary>
        public static PixelBuffer Mark()
        {
            var b = new PixelBuffer(20, 20);
            b.Rect(1, 1, 18, 18, C("#c0392b"));
            b.Rect(3, 3, 14, 14, C("#16120f"));
            PixelFont.Stamp.Draw(b, "L", 4, 5, C("#ece3c8"), false);
            PixelFont.Stamp.Draw(b, "E", 10, 7, C("#c0392b"), false);
            Wear(b, 4, 0.03f);
            return b;
        }

        static void Wear(PixelBuffer b, int seed, float amount)
        {
            var r = new PixelRandom(seed);
            for (int i = 0; i < b.W * b.H; i++)
                if (b.Px[i].a > 0 && r.Next() < amount) b.Px[i] = default;
        }

        static PixelBuffer Shear(PixelBuffer src, float slope)
        {
            var o = new PixelBuffer(src.W, src.H);
            float cx = src.W / 2f;
            for (int y = 0; y < src.H; y++)
                for (int x = 0; x < src.W; x++)
                {
                    var c = src.Px[y * src.W + x];
                    if (c.a == 0) continue;
                    int ty = y + Mathf.RoundToInt((x - cx) * slope);
                    if ((uint)ty < (uint)o.H) o.Px[ty * o.W + x] = c;
                }
            return o;
        }

        /// <summary>A country's tab flag, 12x8 with a dark outline (14x10).</summary>
        public static PixelBuffer Flag(string id)
        {
            var b = new PixelBuffer(14, 10);
            int x = 1, y = 1;
            var white = C("#ece8de");
            var red = C("#c83a3a");
            var blue = C("#2a4a9a");
            var green = C("#3a8a4a");
            switch (id)
            {
                case Countries.France: V3(b, x, y, blue, white, red); break;
                case Countries.Italy: V3(b, x, y, green, white, red); break;
                case Countries.Germany: H3(b, x, y, C("#1a1a1a"), C("#c83a2a"), C("#e0b030")); break;
                case Countries.Iran:
                    H3(b, x, y, green, white, red);
                    b.Set(x + 6, y + 4, red);
                    b.Set(x + 5, y + 3, red);
                    break;
                case Countries.Turkey:
                    b.Rect(x, y, 12, 8, red);
                    b.Ellipse(x + 5f, y + 4f, 2.6f, 2.6f, white);
                    b.Ellipse(x + 6f, y + 4f, 2.2f, 2.2f, red);
                    b.Set(x + 8, y + 4, white);
                    break;
                case Countries.UnitedKingdom:
                    b.Rect(x, y, 12, 8, C("#2a3a7a"));
                    b.Line(x, y, x + 11, y + 7, white);
                    b.Line(x + 11, y, x, y + 7, white);
                    b.Rect(x, y + 3, 12, 2, white);
                    b.Rect(x + 5, y, 2, 8, white);
                    b.HLine(x, x + 11, y + 4, red);
                    b.VLine(x + 5, y, y + 7, red);
                    break;
                case Countries.UnitedStates:
                    for (int j = 0; j < 8; j++) b.HLine(x, x + 11, y + j, (j & 1) == 0 ? red : white);
                    b.Rect(x, y, 5, 4, C("#2a3a7a"));
                    b.Set(x + 1, y + 1, white);
                    b.Set(x + 3, y + 2, white);
                    break;
                case Countries.Japan:
                    b.Rect(x, y, 12, 8, white);
                    b.Ellipse(x + 6f, y + 4f, 2.5f, 2.5f, red);
                    break;
                case Countries.SouthKorea:
                    b.Rect(x, y, 12, 8, white);
                    b.Ellipse(x + 6f, y + 4f, 2.5f, 2.5f, red);
                    b.HLine(x + 4, x + 8, y + 5, blue);
                    b.HLine(x + 5, x + 7, y + 6, blue);
                    b.Set(x + 1, y + 1, C("#1a1a1a"));
                    b.Set(x + 10, y + 1, C("#1a1a1a"));
                    b.Set(x + 1, y + 6, C("#1a1a1a"));
                    b.Set(x + 10, y + 6, C("#1a1a1a"));
                    break;
                case Countries.Emirates:
                    b.Rect(x, y, 3, 8, red);
                    b.Rect(x + 3, y, 9, 3, green);
                    b.Rect(x + 3, y + 3, 9, 2, white);
                    b.Rect(x + 3, y + 5, 9, 3, C("#1a1a1a"));
                    break;
                default:
                    b.Rect(x, y, 12, 8, C("#e8d48a"));
                    PixelFont.Tiny.Draw(b, "?", x + 4, y + 1, C("#2a2420"), false);
                    break;
            }
            b.Outline(C("#16120f"));
            return b;
        }

        static void V3(PixelBuffer b, int x, int y, Color32 a, Color32 c, Color32 d)
        {
            b.Rect(x, y, 4, 8, a);
            b.Rect(x + 4, y, 4, 8, c);
            b.Rect(x + 8, y, 4, 8, d);
        }

        static void H3(PixelBuffer b, int x, int y, Color32 a, Color32 c, Color32 d)
        {
            b.Rect(x, y, 12, 3, a);
            b.Rect(x, y + 3, 12, 2, c);
            b.Rect(x, y + 5, 12, 3, d);
        }

        /// <summary>Desktop icons for the shop computer's apps, 18x18.</summary>
        public static PixelBuffer Icon(string app)
        {
            var b = new PixelBuffer(18, 18);
            int x = 1;
            switch (app)
            {
                case "orders":
                    b.Rect(x + 2, 2, 12, 14, C("#8a6a42"));
                    b.Rect(x + 3, 4, 10, 11, C("#e8e0c8"));
                    b.Rect(x + 5, 1, 6, 3, C("#9a9a92"));
                    for (int i = 0; i < 4; i++)
                    {
                        b.Set(x + 4, 6 + i * 2, i == 2 ? C("#c84a3a") : C("#5a9a4a"));
                        b.HLine(x + 6, x + 11, 6 + i * 2, C("#6a6252"));
                    }
                    break;
                case "messages":
                    b.Rect(x + 1, 4, 15, 10, C("#e8e0c8"));
                    b.Line(x + 1, 4, x + 8, 10, C("#8a826a"));
                    b.Line(x + 15, 4, x + 8, 10, C("#8a826a"));
                    b.Rect(x + 12, 2, 4, 4, C("#e0a83a"));
                    break;
                case "editor":
                    b.Rect(x + 3, 2, 11, 14, C("#6e1e28"));
                    b.HLine(x + 3, x + 13, 2, C("#8a2a34"));
                    b.Ring(x + 8.5f, 8f, 3f, 3f, C("#c9a24a"));
                    b.HLine(x + 5, x + 12, 13, C("#c9a24a"));
                    break;
                case "clients":
                    b.Rect(x + 1, 3, 15, 11, C("#cfc8b2"));
                    b.Ellipse(x + 5f, 7f, 2f, 2f, C("#6a5a4a"));
                    b.Rect(x + 3, 9, 5, 3, C("#6a5a4a"));
                    b.HLine(x + 9, x + 14, 6, C("#6a6252"));
                    b.HLine(x + 9, x + 13, 8, C("#8a826a"));
                    b.HLine(x + 9, x + 14, 10, C("#8a826a"));
                    break;
                case "news":
                    b.Rect(x + 1, 2, 14, 14, C("#e8e0c8"));
                    b.Rect(x + 2, 3, 12, 3, C("#3a3a3a"));
                    b.Rect(x + 2, 7, 5, 5, C("#8fa6b8"));
                    for (int i = 0; i < 3; i++) b.HLine(x + 8, x + 13, 7 + i * 2, C("#6a6252"));
                    b.HLine(x + 2, x + 13, 14, C("#6a6252"));
                    break;
                default: // shop
                    b.Rect(x + 2, 6, 13, 10, C("#a8804a"));
                    b.HLine(x + 2, x + 14, 6, C("#c8a06a"));
                    b.VLine(x + 8, 6, 15, C("#7a5a32"));
                    b.HLine(x + 2, x + 14, 10, C("#7a5a32"));
                    b.Rect(x + 4, 2, 9, 4, C("#5a9a4a"));
                    PixelFont.Tiny.Draw(b, "$", x + 7, 1, C("#e8e0c8"), false);
                    break;
            }
            b.Outline(C("#16120f"));
            return b;
        }

        /// <summary>
        /// A piece of equipment exactly as it stands in the shop (drawn by <see cref="RoomArt"/>),
        /// cut out for the SHOP app: what you buy is what appears in the room.
        /// </summary>
        public static PixelBuffer Equipment(EquipmentSlot slot, int tier)
        {
            var st = new RoomState { Camera = tier, Printer = tier, Computer = tier, Station = tier, Decor = 1, Power = true };
            st.Sky = ShopMood.At(12f * 60f, Weather.Clear);
            st.Tabs.Add(("BASICS", false));
            var room = new PixelBuffer(RoomArt.W, RoomArt.H);
            RectInt crop;
            switch (slot)
            {
                case EquipmentSlot.Camera:
                    RoomArt.Tripod(room, st);
                    crop = new RectInt(362, 112, 36, 64);
                    break;
                case EquipmentSlot.Printer:
                    RoomArt.PrinterBody(room, st);
                    var face = new PixelBuffer(72, 45);
                    RoomArt.PrinterLayer(face, st, "READY", "idle", 0f, false, 0f);
                    room.Blit(face, 342, 196);
                    crop = new RectInt(340, 190, 76, 80);
                    break;
                case EquipmentSlot.Computer:
                    RoomArt.Computer(room, st);
                    var sr = RoomArt.ScreenRect(tier);
                    var screen = new PixelBuffer(sr.width, sr.height);
                    RoomArt.ScreenLayer(screen, st, "OFFICIAL-ISH", "READY", "", false, false);
                    room.Blit(screen, sr.x, sr.y);
                    crop = new RectInt(4, 192, 106, 78);
                    break;
                default:
                    RoomArt.PhotoCorner(room, st);
                    var backdrop = new PixelBuffer(96, 112);
                    st.Backdrop = Backdrop.LightGray;
                    RoomArt.BackdropLayer(backdrop, st);
                    room.Blit(backdrop, 366, 24);
                    crop = new RectInt(362, 20, 102, 120);
                    break;
            }
            var o = new PixelBuffer(crop.width + 2, crop.height + 2);
            for (int y = 0; y < crop.height; y++)
                for (int x = 0; x < crop.width; x++)
                {
                    var c = room.Get(crop.x + x, crop.y + y);
                    if (c.a > 0) o.Set(x + 1, y + 1, c);
                }
            o.Outline(C("#16120f"));
            return o;
        }

        /// <summary>The gold emblem on a passport cover. 24x24.</summary>
        public static PixelBuffer Emblem()
        {
            var b = new PixelBuffer(24, 24);
            var gold = C("#e8cf7e");
            b.Ring(12f, 12f, 10.5f, 10.5f, gold);
            b.Ring(12f, 12f, 8.5f, 8.5f, gold);
            // A stylised bird over a shield: official enough.
            b.Rect(10, 8, 4, 8, gold);
            b.HLine(6, 17, 9, gold);
            b.HLine(7, 16, 10, gold);
            b.Set(5, 8, gold);
            b.Set(18, 8, gold);
            b.Rect(11, 6, 2, 2, gold);
            b.HLine(9, 14, 16, gold);
            b.HLine(10, 13, 17, gold);
            return b;
        }

        /// <summary>A 4x-scaled image of a buffer, ready to drop into a layout.</summary>
        public static PixelImage Image(PixelBuffer b, int scale, string classes = null)
        {
            var img = new PixelImage(classes);
            img.style.width = b.W * scale;
            img.style.height = b.H * scale;
            img.style.flexShrink = 0;
            img.Upload(b);
            return img;
        }
    }
}
