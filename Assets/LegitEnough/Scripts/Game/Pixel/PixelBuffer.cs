using System;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>
    /// A software pixel canvas (RGBA, origin top-left, y down). Every primitive is hard-edged,
    /// so anything drawn here reads as pixel art once it is shown with point filtering.
    /// While <see cref="Emissive"/> is on, written pixels are flagged as light sources: room
    /// lighting leaves them at full brightness (screens, bulbs, neon, the street outside).
    /// </summary>
    public sealed class PixelBuffer
    {
        public int W { get; private set; }
        public int H { get; private set; }
        public Color32[] Px = Array.Empty<Color32>();
        public byte[] Em = Array.Empty<byte>();
        public bool Emissive;

        public PixelBuffer(int w, int h) => Resize(w, h);

        public void Resize(int w, int h)
        {
            w = Mathf.Max(1, w);
            h = Mathf.Max(1, h);
            if (Px.Length < w * h)
            {
                Px = new Color32[w * h];
                Em = new byte[w * h];
            }
            W = w;
            H = h;
            Clear();
        }

        public void Clear()
        {
            Array.Clear(Px, 0, W * H);
            Array.Clear(Em, 0, W * H);
        }

        public void Fill(Color32 c)
        {
            byte e = Emissive ? (byte)1 : (byte)0;
            for (int i = 0; i < W * H; i++)
            {
                Px[i] = c;
                Em[i] = e;
            }
        }

        public bool In(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H;
        public Color32 Get(int x, int y) => In(x, y) ? Px[y * W + x] : default;
        public bool Has(int x, int y) => In(x, y) && Px[y * W + x].a > 0;

        public static Color32 Over(Color32 dst, Color32 src)
        {
            if (src.a == 255 || dst.a == 0) return src;
            int a = src.a, ia = 255 - a;
            int outA = a + dst.a * ia / 255;
            if (outA == 0) return default;
            return new Color32(
                (byte)((src.r * a + dst.r * dst.a * ia / 255) / outA),
                (byte)((src.g * a + dst.g * dst.a * ia / 255) / outA),
                (byte)((src.b * a + dst.b * dst.a * ia / 255) / outA),
                (byte)outA);
        }

        public void Set(int x, int y, Color32 c)
        {
            if (c.a == 0 || (uint)x >= (uint)W || (uint)y >= (uint)H) return;
            int i = y * W + x;
            Px[i] = c.a == 255 ? c : Over(Px[i], c);
            Em[i] = Emissive ? (byte)1 : (byte)0;
        }

        // ------------------------------------------------------------------ primitives

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            if (c.a == 0) return;
            int x0 = Mathf.Max(0, x), y0 = Mathf.Max(0, y), x1 = Mathf.Min(W, x + w), y1 = Mathf.Min(H, y + h);
            for (int yy = y0; yy < y1; yy++)
                for (int xx = x0; xx < x1; xx++)
                    Set(xx, yy, c);
        }

        public void HLine(int x0, int x1, int y, Color32 c) => Rect(Mathf.Min(x0, x1), y, Mathf.Abs(x1 - x0) + 1, 1, c);
        public void VLine(int x, int y0, int y1, Color32 c) => Rect(x, Mathf.Min(y0, y1), 1, Mathf.Abs(y1 - y0) + 1, c);

        public void Box(int x, int y, int w, int h, Color32 c)
        {
            HLine(x, x + w - 1, y, c);
            HLine(x, x + w - 1, y + h - 1, c);
            VLine(x, y, y + h - 1, c);
            VLine(x + w - 1, y, y + h - 1, c);
        }

        /// <summary>Filled ellipse; a pixel is in when its centre is.</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            if (rx <= 0f || ry <= 0f || c.a == 0) return;
            int y0 = Mathf.FloorToInt(cy - ry), y1 = Mathf.CeilToInt(cy + ry);
            int x0 = Mathf.FloorToInt(cx - rx), x1 = Mathf.CeilToInt(cx + rx);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        /// <summary>One-pixel ellipse outline.</summary>
        public void Ring(float cx, float cy, float rx, float ry, Color32 c)
        {
            if (rx <= 0f || ry <= 0f) return;
            int y0 = Mathf.FloorToInt(cy - ry) - 1, y1 = Mathf.CeilToInt(cy + ry) + 1;
            int x0 = Mathf.FloorToInt(cx - rx) - 1, x1 = Mathf.CeilToInt(cx + rx) + 1;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!Inside(x, y)) continue;
                    if (!Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1)) Set(x, y, c);
                }

            bool Inside(int x, int y)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                return dx * dx + dy * dy <= 1f;
            }
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            for (int guard = 0; guard < 4096; guard++)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Random pixels (seeded, so the same every frame).</summary>
        public void Speck(int x, int y, int w, int h, Color32 c, float density, int seed, bool onlyFilled = true)
        {
            var r = new PixelRandom(seed);
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    if (r.Next() < density && (!onlyFilled || Has(xx, yy))) Set(xx, yy, c);
        }

        public void Checker(int x, int y, int w, int h, Color32 c, int phase = 0)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    if (((xx + yy + phase) & 1) == 0) Set(xx, yy, c);
        }

        /// <summary>Multiplies what is already there (shading a region of a drawn object).</summary>
        public void Tint(int x, int y, int w, int h, float k)
        {
            int x0 = Mathf.Max(0, x), y0 = Mathf.Max(0, y), x1 = Mathf.Min(W, x + w), y1 = Mathf.Min(H, y + h);
            for (int yy = y0; yy < y1; yy++)
                for (int xx = x0; xx < x1; xx++)
                {
                    int i = yy * W + xx;
                    if (Px[i].a > 0) Px[i] = Pal.Mul(Px[i], k);
                }
        }

        /// <summary>Copies another buffer on top (alpha "over"), keeping its emissive flags.</summary>
        public void Blit(PixelBuffer src, int ox, int oy)
        {
            for (int y = 0; y < src.H; y++)
            {
                int ty = oy + y;
                if ((uint)ty >= (uint)H) continue;
                for (int x = 0; x < src.W; x++)
                {
                    int tx = ox + x;
                    if ((uint)tx >= (uint)W) continue;
                    var c = src.Px[y * src.W + x];
                    if (c.a == 0) continue;
                    int i = ty * W + tx;
                    Px[i] = c.a == 255 ? c : Over(Px[i], c);
                    Em[i] = src.Em[y * src.W + x];
                }
            }
        }

        /// <summary>Adds a 1px outline around everything opaque (sprite silhouettes).</summary>
        public void Outline(Color32 c, byte minAlpha = 128)
        {
            int n = W * H;
            var mark = new bool[n];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (Px[i].a >= minAlpha) continue;
                    if ((x > 0 && Px[i - 1].a >= minAlpha) || (x < W - 1 && Px[i + 1].a >= minAlpha) ||
                        (y > 0 && Px[i - W].a >= minAlpha) || (y < H - 1 && Px[i + W].a >= minAlpha)) mark[i] = true;
                }
            for (int i = 0; i < n; i++)
                if (mark[i])
                {
                    Px[i] = c;
                    Em[i] = 0;
                }
        }

        /// <summary>Text in one of the pixel fonts. Returns the width drawn.</summary>
        public int Text(string s, int x, int y, Color32 c, PixelFont font, bool mirror = false) => font.Draw(this, s, x, y, c, mirror);

        public int TextCentered(string s, int cx, int y, Color32 c, PixelFont font)
        {
            int w = font.Measure(s);
            return font.Draw(this, s, cx - w / 2, y, c, false);
        }
    }

    /// <summary>Small deterministic random source for art (xorshift).</summary>
    public struct PixelRandom
    {
        uint _s;

        public PixelRandom(int seed)
        {
            _s = (uint)seed * 2654435761u + 0x9E3779B9u;
            if (_s == 0) _s = 1;
        }

        public float Next()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return (_s & 0xFFFFFF) / 16777216f;
        }

        public int Range(int min, int maxExclusive) => min + (int)(Next() * (maxExclusive - min));
    }

    /// <summary>Colour helpers for the pixel art (Color32 throughout).</summary>
    public static class Pal
    {
        public static Color32 Hex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) c = Color.magenta;
            return c;
        }

        public static Color32 Mul(Color32 c, float k) =>
            new Color32(B(c.r * k), B(c.g * k), B(c.b * k), c.a);

        public static Color32 Mul(Color32 c, float r, float g, float b) =>
            new Color32(B(c.r * r), B(c.g * g), B(c.b * b), c.a);

        public static Color32 Mix(Color32 a, Color32 b, float t) =>
            new Color32(B(a.r + (b.r - a.r) * t), B(a.g + (b.g - a.g) * t), B(a.b + (b.b - a.b) * t), B(a.a + (b.a - a.a) * t));

        public static Color32 A(Color32 c, float alpha) => new Color32(c.r, c.g, c.b, B(alpha * 255f));

        public static Color32 Of(Color c) => c;

        static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);

        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        public static readonly Color32 Ink = Hex("#16120f");
        public static readonly Color32 Outline = Hex("#16120f");
    }
}
