using System.Collections.Generic;
using UnityEngine;

namespace LegitEnough.Game
{
    public enum Weather { Clear, Cloudy, Rain, Snow }

    /// <summary>
    /// Presentation-only atmosphere: weather (rolled per day, no gameplay effect) and the
    /// time-of-day light outside. Morning is cool, afternoon neutral, evening warm, night dark.
    /// </summary>
    public static class ShopMood
    {
        public static Weather WeatherFor(ulong seed, int day)
        {
            var r = new PixelRandom((int)(seed % 100000) * 31 + day * 7919);
            float roll = r.Next();
            var date = LegitEnough.Sim.Calendar.DateOf(day);
            bool winter = date.Month == 12 || date.Month <= 2;
            if (winter && roll < 0.25f) return Weather.Snow;
            if (roll < 0.38f) return Weather.Rain;
            if (roll < 0.64f) return Weather.Cloudy;
            return Weather.Clear;
        }

        public struct Outside
        {
            /// <summary>0 = night, 1 = full daylight.</summary>
            public float Daylight;
            public Color SkyTop;
            public Color SkyBottom;
            public Color WindowLight;
            public bool StreetLights;
        }

        struct Key
        {
            public float Minute, Day;
            public Color Top, Bottom, Light;
            public Key(float minute, float day, string top, string bottom, string light)
            {
                Minute = minute;
                Day = day;
                Top = Ui.Hex(top);
                Bottom = Ui.Hex(bottom);
                Light = Ui.Hex(light);
            }
        }

        static readonly Key[] Keys =
        {
            new Key(0f, 0.05f, "#0b1220", "#162238", "#26344e"),
            new Key(7f * 60f, 0.25f, "#3a4a66", "#a8a0a0", "#7c8aa6"),
            new Key(8.5f * 60f, 0.8f, "#93abc4", "#c9d3d8", "#b8c4d4"),
            new Key(11f * 60f, 1f, "#a6bccd", "#d6dcd8", "#d0d2cc"),
            new Key(15f * 60f, 0.9f, "#a8b4c0", "#dccab0", "#dcc8a8"),
            new Key(17f * 60f, 0.55f, "#5a4a70", "#e0905a", "#e0a070"),
            new Key(18.25f * 60f, 0.2f, "#1f2240", "#7a4a5a", "#6a4a5a"),
            new Key(19.25f * 60f, 0.05f, "#0d1424", "#18263b", "#26344e"),
            new Key(24f * 60f, 0.05f, "#0b1220", "#162238", "#26344e"),
        };

        public static Outside At(float minute, Weather w)
        {
            int i = 0;
            while (i < Keys.Length - 2 && Keys[i + 1].Minute <= minute) i++;
            var a = Keys[i];
            var b = Keys[i + 1];
            float t = Mathf.Clamp01((minute - a.Minute) / Mathf.Max(1f, b.Minute - a.Minute));
            var o = new Outside
            {
                Daylight = Mathf.Lerp(a.Day, b.Day, t),
                SkyTop = Color.Lerp(a.Top, b.Top, t),
                SkyBottom = Color.Lerp(a.Bottom, b.Bottom, t),
                WindowLight = Color.Lerp(a.Light, b.Light, t),
            };
            switch (w)
            {
                case Weather.Cloudy:
                    o.Daylight *= 0.78f;
                    o.SkyTop = Color.Lerp(o.SkyTop, Ui.Hex("#8a8e94") * o.Daylight * 1.2f, 0.6f);
                    o.SkyBottom = Color.Lerp(o.SkyBottom, Ui.Hex("#9a9a98") * Mathf.Max(0.2f, o.Daylight), 0.55f);
                    o.WindowLight = Color.Lerp(o.WindowLight, Ui.Hex("#9aa0a8"), 0.4f) * 0.9f;
                    break;
                case Weather.Rain:
                    o.Daylight *= 0.66f;
                    o.SkyTop = Color.Lerp(o.SkyTop, Ui.Hex("#4a5568") * Mathf.Max(0.25f, o.Daylight), 0.6f);
                    o.SkyBottom = Color.Lerp(o.SkyBottom, Ui.Hex("#6a7480") * Mathf.Max(0.25f, o.Daylight), 0.55f);
                    o.WindowLight = Color.Lerp(o.WindowLight, Ui.Hex("#7a8aa6"), 0.45f) * 0.85f;
                    break;
                case Weather.Snow:
                    o.Daylight *= 0.85f;
                    o.SkyTop = Color.Lerp(o.SkyTop, Ui.Hex("#b8c0c8") * Mathf.Max(0.3f, o.Daylight), 0.5f);
                    o.SkyBottom = Color.Lerp(o.SkyBottom, Ui.Hex("#d8dce0") * Mathf.Max(0.3f, o.Daylight), 0.5f);
                    o.WindowLight = Color.Lerp(o.WindowLight, Ui.Hex("#c8d4e0"), 0.4f);
                    break;
            }
            o.StreetLights = o.Daylight < 0.45f;
            return o;
        }
    }

    /// <summary>
    /// Banded room lighting (visual direction: "never evenly lit"). Lights add up per pixel,
    /// then quantize into a few bands with a little ordered dither at the band edges.
    /// The baked map multiplies everything that isn't emissive: room, props and people alike.
    /// </summary>
    public sealed class LightMap
    {
        public struct Lamp
        {
            public float X, Y, Rx, Ry, K, Falloff;
            public Color C;

            public Lamp(float x, float y, float rx, float ry, Color c, float k, float falloff = 1.5f)
            {
                X = x;
                Y = y;
                Rx = rx;
                Ry = ry;
                C = c;
                K = k;
                Falloff = falloff;
            }
        }

        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        public readonly int W, H;
        public Color Ambient = new Color(0.6f, 0.6f, 0.6f);
        public readonly List<Lamp> Lamps = new List<Lamp>();
        public int Bands = 8;
        /// <summary>Darkest an unlit pixel may get, so night never crushes to pure black.</summary>
        public Color Floor = new Color(0.035f, 0.035f, 0.05f);

        readonly float[] _r, _g, _b;

        public LightMap(int w, int h)
        {
            W = w;
            H = h;
            _r = new float[w * h];
            _g = new float[w * h];
            _b = new float[w * h];
        }

        public void Bake()
        {
            for (int i = 0; i < W * H; i++)
            {
                _r[i] = Ambient.r;
                _g[i] = Ambient.g;
                _b[i] = Ambient.b;
            }
            foreach (var l in Lamps)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(l.X - l.Rx)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(l.X + l.Rx));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(l.Y - l.Ry)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(l.Y + l.Ry));
                for (int y = y0; y <= y1; y++)
                {
                    float dy = (y - l.Y) / l.Ry;
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = (x - l.X) / l.Rx;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d >= 1f) continue;
                        float f = Mathf.Pow(1f - d, l.Falloff) * l.K;
                        int i = y * W + x;
                        _r[i] += l.C.r * f;
                        _g[i] += l.C.g * f;
                        _b[i] += l.C.b * f;
                    }
                }
            }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    float t = 0.5f + ((Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f - 0.5f) * 0.34f;
                    _r[i] = Band(_r[i], t);
                    _g[i] = Band(_g[i], t);
                    _b[i] = Band(_b[i], t);
                }
        }

        float Band(float v, float t)
        {
            float s = v * Bands;
            float fl = Mathf.Floor(s);
            return ((s - fl) > t ? fl + 1f : fl) / Bands;
        }

        /// <summary>Lights a buffer whose top-left pixel sits at (ox, oy) in the room.</summary>
        public void Apply(PixelBuffer b, int ox, int oy, float extra = 1f)
        {
            int fr = (int)(Floor.r * 255f), fg = (int)(Floor.g * 255f), fb = (int)(Floor.b * 255f);
            for (int y = 0; y < b.H; y++)
            {
                int ry = Mathf.Clamp(oy + y, 0, H - 1);
                for (int x = 0; x < b.W; x++)
                {
                    int i = y * b.W + x;
                    var c = b.Px[i];
                    if (c.a == 0 || b.Em[i] != 0) continue;
                    int rx = Mathf.Clamp(ox + x, 0, W - 1);
                    int j = ry * W + rx;
                    c.r = (byte)Mathf.Clamp(Mathf.Max(fr, (int)(c.r * _r[j] * extra)), 0, 255);
                    c.g = (byte)Mathf.Clamp(Mathf.Max(fg, (int)(c.g * _g[j] * extra)), 0, 255);
                    c.b = (byte)Mathf.Clamp(Mathf.Max(fb, (int)(c.b * _b[j] * extra)), 0, 255);
                    b.Px[i] = c;
                }
            }
        }

        /// <summary>The light at one room pixel (for tinting UI sprites placed in the room).</summary>
        public Color At(int x, int y)
        {
            int i = Mathf.Clamp(y, 0, H - 1) * W + Mathf.Clamp(x, 0, W - 1);
            return new Color(_r[i], _g[i], _b[i]);
        }
    }
}
