using System;
using System.Collections.Generic;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>
    /// Pixel art for the demo build: the McHunter Studio logo, padlocks and FULL GAME stamps for
    /// what the demo keeps locked, the DEMO tag under the title logo and the tutorial's pointer.
    /// Same language as <see cref="UiArt"/>: hard pixels, #16120f outlines, stamps stepped rather
    /// than rotated. Every call returns a fresh buffer.
    /// </summary>
    public static class DemoArt
    {
        // Colours are built once: Pal.Hex parses its string on every call.
        static readonly Color32 Ink = new Color32(22, 18, 15, 255);             // #16120f
        static readonly Color32 Cream = new Color32(236, 227, 200, 255);        // #ece3c8 (ENOUGH)
        static readonly Color32 Red = new Color32(192, 57, 43, 255);            // #c0392b
        static readonly Color32 RedLight = new Color32(214, 88, 64, 255);       // #d65840
        static readonly Color32 RedDark = new Color32(142, 40, 30, 255);        // #8e281e
        static readonly Color32 RedFaded = new Color32(204, 96, 76, 255);       // #cc604c
        static readonly Color32 Steel = new Color32(168, 170, 166, 255);        // #a8aaa6
        static readonly Color32 SteelLight = new Color32(214, 216, 208, 255);   // #d6d8d0
        static readonly Color32 Brass = new Color32(201, 162, 74, 255);         // #c9a24a
        static readonly Color32 BrassLight = new Color32(232, 207, 126, 255);   // #e8cf7e
        static readonly Color32 BrassDark = new Color32(154, 122, 50, 255);     // #9a7a32
        static readonly Color32 Accent = new Color32(224, 168, 58, 255);        // #e0a83a
        static readonly Color32 AccentLight = new Color32(240, 200, 106, 255);  // #f0c86a
        static readonly Color32 Dust = new Color32(122, 116, 106, 255);         // #7a746a

        // ------------------------------------------------------------------ studio logo

        public const int StudioW = 91, StudioH = 32;
        internal const byte LogoMark = 1, LogoDot = 2, LogoWord = 3, LogoSub = 4;

        /// <summary>Colour per logo index: empty, orange mark, blue dot, wordmark, STUDIO.</summary>
        internal static readonly Color32[] LogoColors =
        {
            new Color32(0, 0, 0, 0),
            new Color32(238, 116, 23, 255),   // #ee7417
            new Color32(59, 99, 172, 255),    // #3b63ac
            new Color32(231, 231, 231, 255),  // #e7e7e7
            new Color32(196, 196, 196, 255),  // #c4c4c4
        };

        // The trailer's pixel redraw of the logo, copied pixel for pixel from
        // Tools/Trailer/art/mchunter_pixel_idx.npy (`python Tools/Trailer/studio_logo.py` prints
        // these rows): '#' orange mark and small dot, 'o' blue dot, 'H' McHunter, 's' STUDIO.
        static readonly string[] LogoRows =
        {
            "..............####.........................................................................",
            "............########.......................................................................",
            "...........##########......................................................................",
            "...........##########......................................................................",
            "...........##########......................................................................",
            "...........##########......................................................................",
            "..........###########......................................................................",
            "..........###########......................................................................",
            "..........##########.......................................................................",
            ".........##########.............HHH....HHH........HH...HH.................HH...............",
            "......###########...............HHHH..HHHH........HH...HH.................HH...............",
            "..###########...................HHHH..HHHH..HHHH..HH...HH..HH..HH.HHHHH..HHHHH..HHHH..HH.HH",
            ".###########....oooo............HH.HHHH.HH.HH..HH.HH...HH..HH..HH.HHHHHH.HHHHH.HH..HH.HHHHH",
            ".##########....oooooo....####...HH.HHHH.HH.HH.....HHHHHHH..HH..HH.HH..HH..HH...HH..HH.HHH..",
            "###########...oooooooo..######..HH..HH..HH.HH.....HHHHHHH..HH..HH.HH..HH..HH...HHHHHH.HH...",
            "###########...oooooooo..######..HH..HH..HH.HH.....HH...HH..HH..HH.HH..HH..HH...HH.....HH...",
            "###########...oooooooo..######..HH......HH.HH.....HH...HH..HH..HH.HH..HH..HH...HH.....HH...",
            "###########...oooooooo..######..HH......HH.HH..HH.HH...HH..HHHHHH.HH..HH..HH...HH..HH.HH...",
            ".##########....oooooo....####...HH......HH..HHHH..HH...HH...HHHHH.HH..HH...HHH..HHHH..HH...",
            ".###########....oooo.......................................................................",
            "..###########..............................................................................",
            "......###########..........................................................................",
            ".........##########..............sss..sss..s.s..ss...sss..sss..............................",
            "..........##########.............s.....s...s.s..s.s...s...s.s..............................",
            "..........###########............sss...s...s.s..s.s...s...s.s..............................",
            "..........###########..............s...s...s.s..s.s...s...s.s..............................",
            "...........##########............sss...s...sss..ss...sss..sss..............................",
            "...........##########......................................................................",
            "...........##########......................................................................",
            "...........##########......................................................................",
            "............########.......................................................................",
            "..............####.........................................................................",
        };

        static byte[] _logo;

        /// <summary>The logo as one palette index per pixel (row-major, 91x32), parsed once.</summary>
        internal static byte[] LogoIndex()
        {
            if (_logo != null) return _logo;
            var idx = new byte[StudioW * StudioH];
            for (int y = 0; y < StudioH; y++)
                for (int x = 0; x < StudioW; x++)
                    idx[y * StudioW + x] = LogoRows[y][x] switch
                    {
                        '#' => LogoMark,
                        'o' => LogoDot,
                        'H' => LogoWord,
                        's' => LogoSub,
                        _ => (byte)0,
                    };
            return _logo = idx;
        }

        /// <summary>The McHunter Studio logo, exactly as in the trailer. 91x32, no outline (made for dark).</summary>
        public static PixelBuffer StudioLogo()
        {
            var idx = LogoIndex();
            var b = new PixelBuffer(StudioW, StudioH);
            for (int i = 0; i < idx.Length; i++) b.Px[i] = LogoColors[idx[i]];
            return b;
        }

        // ------------------------------------------------------------------ locks

        // X ink, S/s steel shackle (light/base), h/b/d brass body (light/base/dark), '.' clear.
        static readonly string[] PadlockRows =
        {
            "..XXXXX..",
            ".XSSSssX.",
            ".XSXXXsX.",
            ".XSX.XsX.",
            "XXXXXXXXX",
            "XhhhhhhhX",
            "XhbbXbbdX",
            "XhbbXbbdX",
            "XhbbbbbdX",
            "XdddddddX",
            "XXXXXXXXX",
        };

        // The same padlock shrunk for a flag's corner.
        static readonly string[] MiniLockRows =
        {
            "..XXX..",
            ".XSSsX.",
            ".XSXsX.",
            "XXXXXXX",
            "XhhhhhX",
            "XbbXbdX",
            "XdddddX",
            "XXXXXXX",
        };

        /// <summary>A brass padlock with a steel shackle, outlined. 9x11.</summary>
        public static PixelBuffer Padlock()
        {
            var b = new PixelBuffer(9, 11);
            Paint(b, PadlockRows, 0, 0);
            return b;
        }

        /// <summary>
        /// A country flag greyed out with a small padlock on its corner: the country is in the
        /// full game. The flag sits at (0,0) exactly where <see cref="UiArt.Flag"/> draws it
        /// (14x10); the padlock overhangs it, so the buffer is 16x12.
        /// </summary>
        public static PixelBuffer LockedFlag(string id)
        {
            var flag = UiArt.Flag(id);
            var b = new PixelBuffer(16, 12);
            for (int y = 0; y < flag.H; y++)
                for (int x = 0; x < flag.W; x++)
                {
                    var c = flag.Px[y * flag.W + x];
                    if (c.a == 0) continue;
                    if (c.r != Ink.r || c.g != Ink.g || c.b != Ink.b)
                    {
                        // Drained of colour and dusted over, darker than a live flag.
                        byte l = (byte)((c.r * 30 + c.g * 59 + c.b * 11) / 100);
                        c = Pal.Mix(Pal.Mix(c, new Color32(l, l, l, 255), 0.85f), Dust, 0.35f);
                        c = Pal.Mul(c, 0.85f);
                    }
                    b.Px[y * b.W + x] = c;
                }
            Paint(b, MiniLockRows, 9, 4);
            return b;
        }

        /// <summary>A small red FULL GAME stamp for locked tabs, rows and buttons. 40x13.</summary>
        public static PixelBuffer FullGameBadge() =>
            // Steps before the second L and before the A: three even runs, every letter whole.
            Stamp("FULL GAME", PixelFont.Tiny, 2, 2, 11, true, 3, 6);

        /// <summary>
        /// A red DEMO stamp to sit under the title logo, drawn at the logo's scale. 36x16.
        /// It belongs at (<see cref="DemoTagX"/>, <see cref="DemoTagY"/>) in the logo's pixels:
        /// centred under ENOUGH, tucked up into the logo's empty bottom rows (34-41), so it hangs
        /// 7 pixels below the 70x42 logo.
        /// </summary>
        public static PixelBuffer DemoTag() =>
            Stamp("DEMO", PixelFont.Stamp, 3, 2, 5, true, 1, 3);

        /// <summary>Where <see cref="DemoTag"/> goes, in <see cref="UiArt.Logo"/> pixels.</summary>
        public const int DemoTagX = 22, DemoTagY = 33;

        /// <summary>
        /// Cream letters on a bevelled red plate, outlined in ink, with a little worn-off ink.
        /// Stamped crooked like <see cref="UiArt.Logo"/>, but the plate only steps a pixel
        /// right before the letters at <paramref name="stepBefore"/>: at this size a shear's steps
        /// would break the letters' one-pixel strokes.
        /// </summary>
        static PixelBuffer Stamp(string text, PixelFont font, int padX, int padY, int seed, bool rise, params int[] stepBefore)
        {
            int steps = stepBefore.Length;
            int pw = font.Measure(text) + padX * 2, ph = font.Height + padY * 2;
            // One pixel all round for the outline, plus the height the steps climb.
            var plate = new PixelBuffer(pw + 2, ph + 2 + steps);
            int left = 1, top = 1 + (rise ? steps : 0);
            plate.Rect(left, top, pw, ph, Red);
            plate.HLine(left, left + pw - 1, top, RedLight);
            plate.VLine(left, top, top + ph - 1, RedLight);
            plate.HLine(left, left + pw - 1, top + ph - 1, RedDark);
            plate.VLine(left + pw - 1, top + 1, top + ph - 1, RedDark);

            var cuts = new int[steps];
            int x = left + padX;
            for (int i = 0; i < text.Length; i++)
            {
                for (int k = 0; k < steps; k++)
                    if (stepBefore[k] == i) cuts[k] = x - 1; // the gap column goes with the letter
                x += font.Draw(plate, text[i].ToString(), x, top + padY, Cream, false) + 1;
            }

            // Worn: the plate's ink is patchy (the letters stay clean so they still read).
            var r = new PixelRandom(seed);
            for (int i = 0; i < plate.W * plate.H; i++)
            {
                if (plate.Px[i].a == 0) continue;
                float n = r.Next();
                if (plate.Px[i].r == Cream.r && plate.Px[i].g == Cream.g) continue;
                if (n < 0.07f) plate.Px[i] = RedFaded;
                else if (n < 0.10f) plate.Px[i] = RedDark;
            }

            var b = Step(plate, cuts, rise);
            b.Outline(Ink);
            return b;
        }

        /// <summary>Moves every column at or right of each cut one pixel up (or down).</summary>
        static PixelBuffer Step(PixelBuffer src, int[] cuts, bool up)
        {
            var o = new PixelBuffer(src.W, src.H);
            for (int x = 0; x < src.W; x++)
            {
                int dy = 0;
                foreach (int cut in cuts)
                    if (x >= cut) dy += up ? -1 : 1;
                for (int y = 0; y < src.H; y++)
                {
                    var c = src.Px[y * src.W + x];
                    int ty = y + dy;
                    if (c.a > 0 && (uint)ty < (uint)o.H) o.Px[ty * o.W + x] = c;
                }
            }
            return o;
        }

        // ------------------------------------------------------------------ tutorial pointer

        // The arrow pointing down; the other directions are this, turned. Shading and the shadow
        // are added after turning, so the light always comes from the top left.
        static readonly string[] PointerRows =
        {
            "..ooooo..",
            "..ooooo..",
            "..ooooo..",
            "..ooooo..",
            "ooooooooo",
            ".ooooooo.",
            "..ooooo..",
            "...ooo...",
            "....o....",
        };

        public const int PointerDown = 0, PointerUp = 1, PointerLeft = 2, PointerRight = 3;

        /// <summary>
        /// The tutorial's chunky pointing arrow in the accent gold, ink-outlined with a soft drop
        /// shadow. 13x13. <paramref name="dir"/>: 0 down, 1 up, 2 left, 3 right (where it points).
        /// </summary>
        public static PixelBuffer Pointer(int dir)
        {
            const int n = 9;
            var shape = new PixelBuffer(13, 13);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int sx = x, sy = y; // the down arrow's pixel that lands on (x, y)
                    switch (dir)
                    {
                        case PointerUp: sy = n - 1 - y; break;
                        case PointerLeft: sx = y; sy = n - 1 - x; break;
                        case PointerRight: sx = y; sy = x; break;
                    }
                    if (PointerRows[sy][sx] == 'o') shape.Px[(y + 1) * shape.W + x + 1] = Accent;
                }
            // Lit from the top left: edges facing it catch the light.
            var lit = new PixelBuffer(13, 13);
            for (int y = 1; y <= n; y++)
                for (int x = 1; x <= n; x++)
                    if (shape.Has(x, y) && (!shape.Has(x - 1, y) || !shape.Has(x, y - 1)))
                        lit.Px[y * lit.W + x] = AccentLight;
            shape.Blit(lit, 0, 0);
            shape.Outline(Ink);

            var b = new PixelBuffer(13, 13);
            for (int y = 0; y < 13; y++)
                for (int x = 0; x < 13; x++)
                    if (shape.Has(x, y))
                    {
                        b.Set(x + 2, y + 2, new Color32(0, 0, 0, 50));
                        b.Set(x + 1, y + 1, new Color32(0, 0, 0, 80));
                    }
            b.Blit(shape, 0, 0);
            return b;
        }

        /// <summary>Where a <see cref="Pointer"/> points: its tip pixel, in buffer pixels.</summary>
        public static Vector2Int PointerTip(int dir) => dir switch
        {
            PointerUp => new Vector2Int(5, 0),
            PointerLeft => new Vector2Int(0, 5),
            PointerRight => new Vector2Int(10, 5),
            _ => new Vector2Int(5, 10),
        };

        // ------------------------------------------------------------------ helpers

        static void Paint(PixelBuffer b, string[] rows, int ox, int oy)
        {
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    Color32 c;
                    switch (rows[y][x])
                    {
                        case 'X': c = Ink; break;
                        case 'S': c = SteelLight; break;
                        case 's': c = Steel; break;
                        case 'h': c = BrassLight; break;
                        case 'b': c = Brass; break;
                        case 'd': c = BrassDark; break;
                        default: continue;
                    }
                    b.Set(ox + x, oy + y, c);
                }
        }
    }

    /// <summary>
    /// The McHunter Studio ident: the logo building itself in at 60 fps, ported from the trailer
    /// (Tools/Trailer/make_trailer.py, class Studio). The mark dissolves in, the blue dot pops on
    /// the beat with a pixel of overshoot, the small dot on the off-beat, the wordmark types
    /// itself letter by letter, then STUDIO sparkles in. Each pixel flashes cream for two frames
    /// as it lands. There is no fade here: the view fades the image itself.
    /// </summary>
    public sealed class StudioIdent
    {
        public const int Fps = 60;
        public const int Frames = 144;
        /// <summary>The mark's pixels land over these frames (the trailer plays a shimmer at MarkFrom).</summary>
        public const int MarkFrom = 15, MarkTo = 27;
        public const int DotFrame = 36, SmallDotFrame = 54;
        /// <summary>When each of the 8 letters of McHunter types in (the trailer blips each one).</summary>
        public static readonly int[] LetterFrames = { 72, 76, 80, 84, 88, 92, 96, 100 };
        /// <summary>STUDIO lands over frames 106-115.</summary>
        public const int SubFrame = 106;
        /// <summary>The first frame that shows the finished logo (the last flash is over).</summary>
        public const int SettledFrame = 117;

        const int Never = int.MaxValue;
        static readonly Color32 Flash = new Color32(255, 246, 226, 255);

        readonly byte[] _idx;
        readonly int[] _appear;
        readonly int[] _dotRing, _smallRing;
        readonly PixelBuffer _buf = new PixelBuffer(DemoArt.StudioW, DemoArt.StudioH);
        int _shown = int.MinValue;

        public StudioIdent()
        {
            const int w = DemoArt.StudioW, h = DemoArt.StudioH;
            _idx = DemoArt.LogoIndex();
            _appear = new int[w * h];
            for (int i = 0; i < _appear.Length; i++) _appear[i] = Never;
            var rng = new PixelRandom(9);

            // The mark (the orange shape left of column 23) dissolves in: its pixels shuffled,
            // then dealt out evenly over the frames.
            var mark = new List<int>();
            for (int i = 0; i < _idx.Length; i++)
                if (_idx[i] == DemoArt.LogoMark && i % w < 23) mark.Add(i);
            for (int i = mark.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (mark[i], mark[j]) = (mark[j], mark[i]);
            }
            for (int k = 0; k < mark.Count; k++)
                _appear[mark[k]] = MarkFrom + k * (MarkTo - MarkFrom + 1) / mark.Count;

            for (int i = 0; i < _idx.Length; i++)
            {
                if (_idx[i] == DemoArt.LogoDot) _appear[i] = DotFrame;
                else if (_idx[i] == DemoArt.LogoMark && i % w >= 23) _appear[i] = SmallDotFrame;
            }

            // The wordmark's letters are the column runs between its empty columns.
            int letter = -1;
            bool inside = false;
            for (int x = 0; x < w; x++)
            {
                bool used = false;
                for (int y = 0; y < h; y++) used |= _idx[y * w + x] == DemoArt.LogoWord;
                if (!used)
                {
                    inside = false;
                    continue;
                }
                if (!inside) letter++;
                inside = true;
                int at = LetterFrames[Mathf.Min(letter, LetterFrames.Length - 1)];
                for (int y = 0; y < h; y++)
                    if (_idx[y * w + x] == DemoArt.LogoWord) _appear[y * w + x] = at;
            }

            for (int i = 0; i < _idx.Length; i++)
                if (_idx[i] == DemoArt.LogoSub) _appear[i] = SubFrame + rng.Range(0, 10);

            _dotRing = Ring(i => _idx[i] == DemoArt.LogoDot);
            _smallRing = Ring(i => _idx[i] == DemoArt.LogoMark && i % w >= 23);
        }

        /// <summary>The pixels just outside a shape (4-neighbours): the dots' overshoot.</summary>
        int[] Ring(Func<int, bool> inShape)
        {
            const int w = DemoArt.StudioW, h = DemoArt.StudioH;
            var ring = new List<int>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (inShape(i)) continue;
                    if ((x > 0 && inShape(i - 1)) || (x < w - 1 && inShape(i + 1)) ||
                        (y > 0 && inShape(i - w)) || (y < h - 1 && inShape(i + w))) ring.Add(i);
                }
            return ring.ToArray();
        }

        /// <summary>
        /// The logo as it stands at <paramref name="frame"/> (60 fps; empty before MarkFrom,
        /// finished from SettledFrame on). 91x32. The same buffer is returned every call and
        /// redrawn only when the frame changes.
        /// </summary>
        public PixelBuffer Frame(int frame)
        {
            if (frame == _shown) return _buf;
            _shown = frame;
            _buf.Clear();
            var px = _buf.Px;
            for (int i = 0; i < _appear.Length; i++)
            {
                int at = _appear[i];
                if (at > frame) continue;
                px[i] = at >= frame - 1 ? Flash : DemoArt.LogoColors[_idx[i]];
            }
            // The dots overshoot by a pixel ring for the two frames they pop in on.
            Overshoot(_dotRing, DotFrame, DemoArt.LogoColors[DemoArt.LogoDot], frame);
            Overshoot(_smallRing, SmallDotFrame, DemoArt.LogoColors[DemoArt.LogoMark], frame);
            return _buf;
        }

        void Overshoot(int[] ring, int at, Color32 c, int frame)
        {
            if (frame < at || frame >= at + 2) return;
            foreach (int i in ring)
                if (_appear[i] > frame) _buf.Px[i] = c;
        }
    }
}
