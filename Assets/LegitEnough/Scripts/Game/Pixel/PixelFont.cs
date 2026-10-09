using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>
    /// Bitmap fonts for text painted into the pixel art (calendar, LCDs, sticky notes, labels).
    /// The glyphs are generated from the same tables as the UI's TTF fonts.
    /// </summary>
    public sealed class PixelFont
    {
        /// <summary>3x5 capitals: tiny labels on props.</summary>
        public static readonly PixelFont Tiny = new PixelFont(PixelGlyphs.Tiny, 5, true, false, 2);
        /// <summary>5x7 with lowercase (the body font).</summary>
        public static readonly PixelFont Body = new PixelFont(PixelGlyphs.Body, 7, false, true, 3);
        /// <summary>Bold 8px capitals (the heading font).</summary>
        public static readonly PixelFont Stamp = new PixelFont(PixelGlyphs.Stamp, 8, true, true, 3);

        struct Glyph
        {
            public int W;
            public int[] Cells; // packed x | (y << 8), y may be below the cap box (descenders)
        }

        readonly Dictionary<char, Glyph> _glyphs = new Dictionary<char, Glyph>();
        readonly bool _upperOnly;
        readonly int _space;

        /// <summary>Cap height in pixels.</summary>
        public readonly int Height;

        PixelFont(string[] table, int height, bool upperOnly, bool proportional, int space)
        {
            Height = height;
            _upperOnly = upperOnly;
            _space = space;
            for (int i = 0; i + 1 < table.Length; i += 2)
            {
                char ch = table[i][0];
                string[] rows = table[i + 1].Split('|');
                var cells = new List<int>();
                int minX = int.MaxValue, maxX = -1, width = 0;
                for (int y = 0; y < rows.Length; y++)
                {
                    width = Mathf.Max(width, rows[y].Length);
                    for (int x = 0; x < rows[y].Length; x++)
                    {
                        if (rows[y][x] != '#') continue;
                        cells.Add(x | (y << 8));
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                    }
                }
                bool fixedWidth = !proportional || char.IsDigit(ch) || maxX < 0;
                if (!fixedWidth && minX > 0)
                    for (int k = 0; k < cells.Count; k++) cells[k] -= minX;
                int w = fixedWidth ? width : maxX - minX + 1;
                _glyphs[ch] = new Glyph { W = ch == ' ' ? _space : w, Cells = cells.ToArray() };
            }
        }

        Glyph Find(char ch)
        {
            if (_upperOnly) ch = char.ToUpperInvariant(ch);
            if (_glyphs.TryGetValue(ch, out var g)) return g;
            string d = ch.ToString().Normalize(NormalizationForm.FormD);
            if (d.Length > 0 && _glyphs.TryGetValue(_upperOnly ? char.ToUpperInvariant(d[0]) : d[0], out g)) return g;
            if (_glyphs.TryGetValue(char.ToUpperInvariant(ch), out g)) return g;
            return _glyphs.TryGetValue('?', out g) ? g : default;
        }

        public int Measure(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int w = 0;
            foreach (char ch in s) w += Find(ch).W + 1;
            return w - 1;
        }

        /// <summary>Draws with the cap top at y. Returns the width.</summary>
        public int Draw(PixelBuffer b, string s, int x, int y, Color32 c, bool mirror)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            if (mirror)
            {
                var arr = s.ToCharArray();
                System.Array.Reverse(arr);
                s = new string(arr);
            }
            int cx = x;
            foreach (char ch in s)
            {
                var g = Find(ch);
                if (g.Cells != null)
                    foreach (int cell in g.Cells)
                    {
                        int gx = cell & 0xFF, gy = cell >> 8;
                        b.Set(cx + (mirror ? g.W - 1 - gx : gx), y + gy, c);
                    }
                cx += g.W + 1;
            }
            return cx - x - 1;
        }

        /// <summary>Upper-cases, strips accents the font lacks and trims to a pixel width.</summary>
        public string Fit(string s, int maxWidth)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (_upperOnly) s = s.ToUpper(CultureInfo.InvariantCulture);
            while (s.Length > 0 && Measure(s) > maxWidth) s = s.Substring(0, s.Length - 1);
            return s;
        }
    }
}
