using System.Collections.Generic;
using LegitEnough.Sim;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>How a badge is drawn: earned, not yet earned, a secret not yet earned, or kept for the full game.</summary>
    public enum BadgeState { Achieved, Locked, Mystery, FullGame }

    /// <summary>
    /// The achievement badges: a perforated postage stamp (the forger's motif) on a plate in the
    /// category's colour, a paper field and a 20x20 glyph drawn in the same palette mini-language as
    /// <see cref="DemoArt"/>. Badges are 32x32; Steam icons are the badge at 8x on a 256x256 panel.
    /// Every call returns a fresh buffer.
    /// </summary>
    public static class AchievementArt
    {
        public const int Size = 32;
        public const int SteamSize = 256;

        static readonly Color32 Ink = new Color32(22, 18, 15, 255);         // #16120f
        static readonly Color32 Paper = new Color32(230, 220, 196, 255);    // #e6dcc4
        static readonly Color32 Dust = new Color32(122, 116, 106, 255);     // #7a746a
        static readonly Color32 Panel = new Color32(27, 22, 18, 255);       // #1b1612 (--le-panel)
        static readonly Color32 MysteryField = new Color32(58, 49, 40, 255);       // #3a3128
        static readonly Color32 MysteryShadow = new Color32(42, 36, 28, 255);      // #2a241c
        static readonly Color32 Accent = new Color32(224, 168, 58, 255);    // #e0a83a
        static readonly Color32 AccentLight = new Color32(240, 200, 106, 255); // #f0c86a

        /// <summary>Plate colours per category: base, light (top/left), dark (bottom/right).</summary>
        static readonly Color32[][] Plates =
        {
            new[] { C(0xc9a24a), C(0xe8cf7e), C(0x9a7a32) }, // Progress: brass
            new[] { C(0xc0392b), C(0xd65840), C(0x8e281e) }, // Skill: stamp red
            new[] { C(0x23407a), C(0x4a6aa8), C(0x1b2f5c) }, // Story: pen navy
            // Secret: ink, lifted a step from the panel so the perforations still read on it.
            new[] { C(0x4a4036), C(0x6e6354), C(0x2a241c) },
        };

        static Color32 C(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// The 32x32 badge, transparent outside the stamp. Locked keeps the ink and dusts the rest
        /// (the <see cref="DemoArt.LockedFlag"/> treatment); FullGame adds the brass padlock;
        /// Mystery is the secret plate with a "?" whatever <paramref name="d"/> is.
        /// </summary>
        public static PixelBuffer Badge(AchievementDef d, BadgeState s)
        {
            if (s == BadgeState.Mystery || d == null) return Mystery(false);
            var b = Frame(d.Id, Plates[(int)d.Category], false);
            if (Glyphs.TryGetValue(d.Id, out var rows))
            {
                int o = rows.Length == 22 ? 5 : 6; // a 22-row glyph uses the whole paper field
                Paint(b, rows, o, o);
            }
            b.Outline(Ink);
            if (s == BadgeState.Locked || s == BadgeState.FullGame) Dusty(b);
            if (s == BadgeState.FullGame) b.Blit(DemoArt.Padlock(), 22, 20);
            return b;
        }

        /// <summary>The secret plate with a stamped "?"; with a padlock when it waits in the full game.</summary>
        public static PixelBuffer Mystery(bool padlock)
        {
            var b = Frame("MYSTERY", Plates[(int)AchCategory.Secret], true);
            // The stamp font's "?" at 2x, with a soft shadow and lit top edges.
            var q = MysteryMark;
            const int ox = 10, oy = 8;
            for (int j = 0; j < q.Length; j++)
                for (int i = 0; i < q[j].Length; i++)
                    if (q[j][i] == '#') b.Rect(ox + i * 2 + 1, oy + j * 2 + 1, 2, 2, MysteryShadow);
            for (int j = 0; j < q.Length; j++)
                for (int i = 0; i < q[j].Length; i++)
                {
                    if (q[j][i] != '#') continue;
                    b.Rect(ox + i * 2, oy + j * 2, 2, 2, Accent);
                    bool topEdge = j == 0 || q[j - 1][i] != '#';
                    bool leftEdge = i == 0 || q[j][i - 1] != '#';
                    if (topEdge) b.HLine(ox + i * 2, ox + i * 2 + 1, oy + j * 2, AccentLight);
                    if (leftEdge) b.VLine(ox + i * 2, oy + j * 2, oy + j * 2 + 1, AccentLight);
                }
            b.Outline(Ink);
            if (padlock) b.Blit(DemoArt.Padlock(), 22, 20);
            return b;
        }

        /// <summary>
        /// What the ACHIEVEMENTS screen shows for one cell: colour when earned (with the padlock when the
        /// demo banked it for the full game), dusty when not, the "?" for a secret not yet earned.
        /// </summary>
        public static PixelBuffer ForCell(AchievementDef d, bool unlocked, bool banked, bool fullGameOnly)
        {
            if (unlocked) return Badge(d, BadgeState.Achieved);
            if (banked)
            {
                var b = Badge(d, BadgeState.Achieved);
                b.Blit(DemoArt.Padlock(), 22, 20);
                return b;
            }
            if (d.Hidden) return Mystery(fullGameOnly);
            return Badge(d, fullGameOnly ? BadgeState.FullGame : BadgeState.Locked);
        }

        /// <summary>
        /// The 256x256 Steam icon: the badge at 8x on the panel colour, so the perforations read.
        /// Unachieved is plain grayscale (the badge at luma x 0.8 on the panel's own luma, #171717), as
        /// Valve recommends. Hidden achievements upload their real art: Steam keeps spoilers hidden itself.
        /// </summary>
        public static PixelBuffer SteamIcon(AchievementDef d, bool achieved)
        {
            var badge = Badge(d, BadgeState.Achieved);
            var b = new PixelBuffer(SteamSize, SteamSize);
            byte pl = (byte)((Panel.r * 30 + Panel.g * 59 + Panel.b * 11) / 100);
            b.Fill(achieved ? Panel : new Color32(pl, pl, pl, 255));
            const int k = SteamSize / Size;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var c = badge.Px[y * Size + x];
                    if (c.a == 0) continue;
                    if (!achieved)
                    {
                        byte l = (byte)Mathf.Clamp((c.r * 30 + c.g * 59 + c.b * 11) / 100 * 0.8f, 0f, 255f);
                        c = new Color32(l, l, l, 255);
                    }
                    b.Rect(x * k, y * k, k, k, c);
                }
            return b;
        }

        /// <summary>A little red "NEW" plate (Tiny font, cream on red, ink outline) for a cell not yet looked at. 17x11.</summary>
        public static PixelBuffer NewTag()
        {
            var b = new PixelBuffer(17, 11);
            b.Rect(1, 1, 15, 9, C(0xc0392b));
            b.HLine(1, 15, 1, C(0xd65840));
            b.HLine(1, 15, 9, C(0x8e281e));
            b.Text("NEW", 3, 3, C(0xece3c8), PixelFont.Tiny);
            b.Outline(Ink);
            return b;
        }

        // ------------------------------------------------------------------ the stamp

        /// <summary>
        /// Plate (1,1,30,30) in the category colour with a bevel, worn with 3% speckle, every third
        /// pixel of each edge bitten out (the outline then runs into the bites: perforations), and
        /// the paper field inside a 1px ink border at (4,4,24,24). Not outlined yet.
        /// </summary>
        static PixelBuffer Frame(string id, Color32[] plate, bool mystery)
        {
            var b = new PixelBuffer(Size, Size);
            Color32 baseC = plate[0], light = plate[1], dark = plate[2];
            b.Rect(1, 1, 30, 30, baseC);
            b.HLine(1, 30, 1, light);
            b.VLine(1, 1, 30, light);
            b.HLine(1, 30, 30, dark);
            b.VLine(30, 1, 30, dark);
            var r = new PixelRandom(StableHash(id));
            for (int y = 1; y <= 30; y++)
                for (int x = 1; x <= 30; x++)
                {
                    float n = r.Next();
                    if (x >= 4 && x <= 27 && y >= 4 && y <= 27) continue;
                    if (n < 0.02f) b.Px[y * Size + x] = dark;
                    else if (n < 0.03f) b.Px[y * Size + x] = light;
                }
            for (int i = 1; i <= 30; i++)
            {
                if (i % 3 != 2) continue;
                b.Px[1 * Size + i] = default;
                b.Px[30 * Size + i] = default;
                b.Px[i * Size + 1] = default;
                b.Px[i * Size + 30] = default;
            }
            b.Box(4, 4, 24, 24, Ink);
            b.Rect(5, 5, 22, 22, mystery ? MysteryField : Paper);
            return b;
        }

        /// <summary>The LockedFlag treatment over the whole badge: drained, dusted, darker; ink stays ink.</summary>
        static void Dusty(PixelBuffer b)
        {
            for (int i = 0; i < b.W * b.H; i++)
            {
                var c = b.Px[i];
                if (c.a == 0 || (c.r == Ink.r && c.g == Ink.g && c.b == Ink.b)) continue;
                byte l = (byte)((c.r * 30 + c.g * 59 + c.b * 11) / 100);
                c = Pal.Mix(Pal.Mix(c, new Color32(l, l, l, 255), 0.85f), Dust, 0.35f);
                b.Px[i] = Pal.Mul(c, 0.85f);
            }
        }

        /// <summary>FNV-1a over the id: the same speckle on every machine and every run.</summary>
        static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char ch in s)
                {
                    h ^= ch;
                    h *= 16777619u;
                }
                return (int)h;
            }
        }

        // ------------------------------------------------------------------ glyph palette

        static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
        {
            ['X'] = C(0x16120f),                                              // ink
            ['r'] = C(0xc0392b), ['R'] = C(0xd65840), ['D'] = C(0x8e281e),    // stamp red / light / dark
            ['b'] = C(0xc9a24a), ['B'] = C(0xe8cf7e), ['d'] = C(0x9a7a32),    // brass / light / dark
            ['n'] = C(0x23407a), ['N'] = C(0x4a6aa8), ['v'] = C(0x1b2f5c),    // pen navy / light / dark
            ['g'] = C(0x7fae5a), ['G'] = C(0x4f7a3a), ['L'] = C(0xb5d68f),    // green / dark / light
            ['w'] = C(0xece3c8), ['p'] = C(0xe6dcc4),                         // cream, paper
            ['m'] = C(0xc9bda2), ['M'] = C(0xa39679),                         // paper shade, deeper
            ['s'] = C(0xa8aaa6), ['S'] = C(0xd6d8d0), ['q'] = C(0x6e706c), ['Q'] = C(0x46484a), // steel
            ['a'] = C(0xe0a83a), ['A'] = C(0xf0c86a),                         // accent / light
            ['k'] = C(0xc8946a), ['K'] = C(0x9a6a48),                         // skin / shadow
            ['h'] = C(0x3a2a1e),                                              // hair
            ['c'] = C(0x9cc4e4), ['C'] = C(0x4f86c0),                         // light blue / blue
            ['y'] = C(0xf6e08a), ['o'] = C(0xe07a2a),                         // flame yellow / orange
            ['t'] = C(0x7a5236), ['T'] = C(0xa8764a),                         // wood / light wood
            ['z'] = C(0x2e3a5e),                                              // night sky
        };

        static void Paint(PixelBuffer b, string[] rows, int ox, int oy)
        {
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (Palette.TryGetValue(rows[y][x], out var c)) b.Set(ox + x, oy + y, c);
        }

        /// <summary>The stamp font's "?" (6x8), drawn at 2x on the mystery badge.</summary>
        static readonly string[] MysteryMark =
        {
            ".####.",
            "##..##",
            "....##",
            "...##.",
            "..##..",
            "......",
            "..##..",
            "..##..",
        };

        // ------------------------------------------------------------------ the 36 glyphs

        // 20 rows of 20 (drawn at 6,6), or 22 of 22 for one that fills the paper field (at 5,5).
        // Authored with a scratch preview tool; the palette letters are listed above.
        static readonly Dictionary<string, string[]> Glyphs = new Dictionary<string, string[]>
        {
            ["FIRST_PHOTO"] = new[]
            {
                "....................",
                "...XXXXXXXXXXXXXX...",
                "...XwwwwwwwwwwwwXm..",
                "...XwSSSSSSSSSSwXm..",
                "...XwSSShhhhSSSwXm..",
                "...XwSShhhhhhSSwXm..",
                "...XwSShkkkkhSSwXm..",
                "...XwSSkXkkXkSSwXm..",
                "...XwSSkkkkkkSSwXm..",
                "...XwSSkkKKkkSSwXm..",
                "...XwSSSkkkkSSSwXm..",
                "...XwSSSSKKSSSSwXm..",
                "...XwSSnnkknnSSwXm..",
                "...XwSnnnNNnnnSwXm..",
                "...XwnnnnNNnnnnwXm..",
                "...XwnnnnnnnnnnwXm..",
                "...XwwwwwwwwwwwwXm..",
                "...XXXXXXXXXXXXXXm..",
                "....mmmmmmmmmmmmmm..",
                "....................",
            },
            ["PERCUSSIVE_MAINTENANCE"] = new[]
            {
                "............X.XaX.X.",
                "...........XaXXaXXaX",
                "............XaaAaaX.",
                ".X.........XaAAyAAaX",
                ".X........XaaAyyyAaa",
                "...........XaAAyAAaX",
                "....XXXXXXX.XaaAaaX.",
                "....XwwwwwXXaXXaXXaX",
                "...XwwmwwwX.X.XaX.X.",
                "...XwwwmwX.....X....",
                ".XXXXXXXXXXXXXXX....",
                ".XSSSSSSSSSSSSSX....",
                ".XsssssssssssssX.X..",
                ".XsXXXXXXXXXXXsX..X.",
                ".XsssssssssssssX....",
                ".XsssssssssssrRX....",
                ".XqqqqqqqqqqqqqX....",
                ".XXXXXXXXXXXXXXX....",
                "..XX.........XX.....",
                "....................",
            },
            ["FIRST_JOB"] = new[]
            {
                "....................",
                "....................",
                "....................",
                "...XXXXXXXXXXXXXXXX.",
                "...XGGGGGGGGGGGGGGX.",
                ".XXXXXXXXXXXXXXXXXX.",
                ".XLLLLLLLLLLLLLLLLX.",
                ".XLGggggggXgggggGLX.",
                ".XLggggggXXXXggggLX.",
                ".XLgggggXgXggggggLX.",
                ".XLggggggXXXgggggLX.",
                ".XLgggggggXgXggggLX.",
                ".XLgggggXXXXgggggLX.",
                ".XLGggggggXgggggGLX.",
                ".XLLLLLLLLLLLLLLLLX.",
                ".XXXXXXXXXXXXXXXXXX.",
                "..XGGGGGGGGGGGGGGGX.",
                "..XXXXXXXXXXXXXXXXX.",
                "....................",
                "....................",
            },
            ["QUALITY_90"] = new[]
            {
                "....................",
                "..XXXXXXXXXXXX......",
                "..XvnnnnnnnnnX......",
                "..XvnbbbbbbnnX......",
                "..XvnnnnnnnnnX......",
                "..XvnnnnnnnnnX......",
                "..XvnnnbbnnnnX......",
                "..XvnnbBBbnnnX......",
                "..XvnbbBBbbnnX......",
                "..XvnnbbbbnnnX...XX.",
                "..XvnnnbbnnnnX..XggX",
                "..XvnnnnnnnnnX.XgGgX",
                "..XvnnnnnXXnnXXgGgX.",
                "..XvnNNNXLgXnXgGgX..",
                "..XvnnnnXgGgXgGgX...",
                "..XvnnnnnXgGgGgX....",
                "..XXXXXXXXXgGgX.....",
                "...........XgX......",
                "............X.......",
                "....................",
            },
            ["SAY_CHEESE"] = new[]
            {
                "XXXXXXXXXX..........",
                "XwwwwwwwwX..........",
                "XwSXXXXXXXXXX.......",
                "XwSXwwwwwwwwX.......",
                "XwSXwSXXXXXXXXXX....",
                "XwSXwSXwwwwwwwwX....",
                "XwSXwSXwSShhSSwX....",
                "XwSXwSXwShhhhSwX....",
                "XwnXwSXwSkkkkSwX....",
                "XwwXwSXwSkXXkSwX....",
                "XXXXwnXwSSkkSSwX....",
                "...XwwXwSnXXXXXXXXXX",
                "...XXXXwnnXAaaaaaaAX",
                "......XwwwXaaaaXXaaX",
                "......XXXXXaaaaaaXaX",
                "..........XXaXaaXaaX",
                "..........XaXaaaaXaX",
                "..........XXaXaXXaaX",
                "..........XaaaaaaaaX",
                "..........XXXXXXXXXX",
            },
            ["SAMIR_MADE_IT"] = new[]
            {
                "....................",
                "....................",
                "....................",
                "XXXXXXXXXXXXXXXXXXXX",
                "XcccccccccXrrXrrXrrX",
                "XccccccaccXrRrrrXrrX",
                "XcccccaAacXrrrrrXRrX",
                "XccccccaccmXrrrXrrrX",
                "XcccccccccmwXrXwwwwX",
                "XGGcccccccmwwXwwwwwX",
                "XGGGGgggggmwmmmmmmmX",
                "XgGGGGGgggmwwwwwwwwX",
                "XgggggggggmwmmmmmmmX",
                "XCCCCCCCCCmwwwwwwwwX",
                "XgggggggggmwmmmmmwwX",
                "XgggggggggmwwwwwwwwX",
                "XXXXXXXXXXXXXXXXXXXX",
                "....................",
                "....................",
                "....................",
            },
            ["HAGGLER"] = new[]
            {
                "................X...",
                "...............XgX..",
                "..............XgLgX.",
                ".............XgLgggX",
                "..X.........XgLgggGg",
                "..X..........XXLgGXX",
                "...X..........XLgGX.",
                "...X..........XLgGX.",
                "...XXXXXXXXXXXXLgGX.",
                "...XBBBBBBBBBX.XXX..",
                "..XBBBBBXBBBbX......",
                ".XBBBBBXXXXBbX......",
                "XBBXBBXBXBBBbX......",
                "XBXpXBBXXXBBbX......",
                "XBBXBBBBXBXBbX......",
                ".XBBBBXXXXBBbX......",
                "..XBBBBBXBBBbX......",
                "...XbbbbbbbbbX......",
                "....XXXXXXXXXX......",
                "....................",
            },
            ["READ_THE_PAPER"] = new[]
            {
                "....................",
                ".XXXXXXXXXXXXXXXXX..",
                ".XwwwwwwwwpppppppXm.",
                ".XwXXXXXXXXXXXXXpXm.",
                ".XwXXXXXXXXXXXXXpXm.",
                ".XwwwwwwwwpppppppXm.",
                ".XwMMMMMMMMMMMMMpXm.",
                ".XwwwwwwwwpppppppXm.",
                ".XwqqqqqqqqqqqqqpXm.",
                ".XwqqqqqqqqqqqqqpXm.",
                ".XwwwwwwwwprrrrrpXm.",
                ".XwssssswwrppppprXm.",
                ".Xwssssswrpqqqqqprm.",
                ".Xwssssswrppppppprm.",
                ".Xwwwwwwwrpqqqqqprm.",
                ".XwMMMMMwrppppppprm.",
                ".XwwwwwwwwrqqqqqrXm.",
                ".XwMMMMMwwprrrrrpXm.",
                ".XXXXXXXXXXXXXXXXX..",
                "..mmmmmmmmmmmmmmmmm.",
            },
            ["RUSH_JOB"] = new[]
            {
                "..........XXXX......",
                "..........XssX......",
                "..........XXXX......",
                ".........XXXXXX..XX.",
                ".......XXssssssXXXs.",
                "......XssssssssssX..",
                ".....XssswwwXwwsssX.",
                "XXXXX.sswwwwXwwwss.X",
                "....XsswwwwwXwwwwssX",
                "....XswwwwwwXwwwwwsX",
                "XXX.XswwwwwwXwwwwwsX",
                "...XssXwwwwwXwwwwXss",
                "....XswwwwwwwrwwwwsX",
                ".XXXXswwwwwwwwrwwwsX",
                "....XsswwwwwwwwrwssX",
                "....X.sswwwwwwwwss.X",
                "XXX..XssswwwwwwsssX.",
                "......XsssssXssssX..",
                ".......XXssssssXX...",
                ".........XXXXXX.....",
            },
            ["JUST_IN_TIME"] = new[]
            {
                "....................",
                "XXXXXXXXXXXX........",
                "XGGggggggggX........",
                "XGsggggggggX........",
                "XGSgXXXXXXgX........",
                "XGGgXwwwwXgX........",
                "XGGgXXXXXXgXXXXXXXXX",
                "XGGgggggggXdbbbbbbbd",
                "XGsggggggggXBBBBBBBX",
                "XGSggggggggXBaaaaaBX",
                "XGGggggggggXXBaaaBX.",
                "XGGggggggggX.XBaBX..",
                "XGGggggggggX..XBX...",
                "XGsggggggggX.XBaBX..",
                "XGSggggggggXXBXaXBX.",
                "XGGggggggggXBXaaaXBX",
                "XGGggggggggXBaaaaaBX",
                "XXXXXXXXXXXdbbbbbbbd",
                "....wwwwwwwXXXXXXXXX",
                "....................",
            },
            ["FIRST_UPGRADE"] = new[]
            {
                "..............XgX...",
                ".............XgggX..",
                "............XgggggX.",
                "...........XgggggggX",
                "............XXLggXX.",
                ".............XLggX..",
                ".............XLggX..",
                "...XXXXX......XXX...",
                "..XsSSSsX...........",
                "XXXXXXXXXXXXXXXX....",
                "XqqqqqqqqqqqqqqX....",
                "XqqqXXXXXXqqqrqX....",
                "XqqXsSSSSsXqqqqX....",
                "XQqXSXXXXSXqqqqX....",
                "XQqXSXNcXSXqqqqX....",
                "XQqXSXCNXSXqqqqX....",
                "XQqXsXXXXsXqqqqX....",
                "XQqqXssssXqqqqqX....",
                "XQQQQXXXXQQQQQQX....",
                "XXXXXXXXXXXXXXXX....",
            },
            ["NIGHT_SHIFT"] = new[]
            {
                "XXXXXXXXXXXXXXXXXXXX",
                "XzzzzzzzzzzzzzzzzzzX",
                "XzzzzzzzzzzzzzzzBzzX",
                "XzzBbbbbzzzzzzzzzzzX",
                "XzzbbbbzzzzzzzzzzzzX",
                "XzbbbbbzzzzzzzzzzzzX",
                "XzbbbbbzzzzzzzzzSSSX",
                "XzbbbbzzzzzzzzzzzzSX",
                "XzbbbbbzzzzzzzzzzSzX",
                "XzbbbbbzzzzzzzzzSzzX",
                "XzbbbbbzzzzzzzzzSSSX",
                "XzzbbbbbzzzzwwwzzzzX",
                "XzzBbbbbbzzzzzwzzzzX",
                "XzzzBBBBBBBBBwzzzzzX",
                "XzzzzBBBBBBBwzzzzzzX",
                "XzzzzzzzBzzzwwwzzzzX",
                "XzzzBzzzzzzzzzzzzBzX",
                "XzzzzzzzzzBzzzzzzzzX",
                "XzzzzzzzzzzzzzzzzzzX",
                "XXXXXXXXXXXXXXXXXXXX",
            },
            ["JOBS_25"] = new[]
            {
                "...XXXX...XXXXXX....",
                "...XXXX...XXXXXX....",
                ".......XX.XX........",
                ".......XX.XX........",
                ".....XX...XXXX......",
                ".....XX...XXXX......",
                "...XX.........XX....",
                "...XX.........XX....",
                "...XXXXXX.XXXX......",
                "...XXXXXX.XXXX......",
                "....................",
                "..XXXXXXXXXXXXXXX...",
                "..XvvvvvvvvvvwwwX...",
                "..XXXXXXXXXXXXXXX...",
                "...XXXXXXXXXXXXXXX..",
                "...XDDDDDDDDDDwwwX..",
                "...XXXXXXXXXXXXXXX..",
                "..XXXXXXXXXXXXXXX...",
                "..XGGGGGGGGGGwwwX...",
                "..XXXXXXXXXXXXXXX...",
            },
            ["JOBS_100"] = new[]
            {
                "......XXXX..........",
                "......XDrX....XXXX..",
                "..XXXXXDrX....XvnX..",
                "..XvnXXDrXXXXXXvnX..",
                "..XvnXXDrXXGgXXvnX..",
                "..XvnXXDrXXGgXXvnX..",
                "..XvnXXDrXXGgXXvnX..",
                "..XvnXXDrXXGgXXvnX..",
                "XXXXXXXXXXXXXXXXXXXX",
                "XttttttttttttttttttX",
                "XtTTTTTTTTTTTTTTTTtX",
                "XtTTdddddddddddddTtX",
                "XtTTdBXBBXXXBXXXdTtX",
                "XtttdXXBBXBXBXBXdttX",
                "XtTTdBXBBXBXBXBXdTtX",
                "XtTTdBXBBXBXBXBXdTtX",
                "XtTTdXXXBXXXBXXXdTtX",
                "XtTTdddddddddddddTtX",
                "XttttttttttttttttttX",
                "XXXXXXXXXXXXXXXXXXXX",
            },
            ["REP_WORD"] = new[]
            {
                ".........XnnnnnnnnnX",
                ".........XnnnnnnnnnX",
                ".........XnnnnnnnnnX",
                ".........XnnwnwnwnnX",
                ".XXXXXXXXXXXXXXnnnnX",
                "XwwwwwwwwwwwwwwXnnnX",
                "wwwwwwwwwwwwwwwwXnnX",
                "wwwwwwwwwwwwwwwwXXX.",
                "wwwwwwwwwwwwwwwwXX..",
                "wwwwXXwXXwXXwwwwX...",
                "wwwwXXwXXwXXwwwwX...",
                "wwwwwwwwwwwwwwwwX...",
                "wwwwwwwwwwwwwwwwX...",
                "XwwwwwwwwwwwwwwX....",
                ".XXwwwXXXXXXXXX.....",
                "..XwwX..............",
                "..XwX...............",
                "...X................",
                "....................",
                "....................",
            },
            ["REP_PRO"] = new[]
            {
                "....XrrrX.XRRRX.....",
                "....XrrrrXRRRRX.....",
                ".....XrrrXRRRX......",
                ".....XrrrrRRRX......",
                "......XrrrRRX.......",
                "......XrrRRRX.......",
                ".......XXXXX........",
                "........XbX.........",
                ".......XbBbX........",
                "....XXXXbBbXXXX.....",
                "...XbbbbbBbbbbbX....",
                "....XbBBBBBBbdX.....",
                ".....XbbBBBbdX......",
                "......XbbBbdX.......",
                ".....XbbbdbbdX......",
                ".....XbbdXdbdX......",
                "....XbbdX.XdbdX.....",
                "....XbdX...XddX.....",
                ".....XX.....XX......",
                "....................",
            },
            ["REP_LEGEND"] = new[]
            {
                "....................",
                ".........X..........",
                "........XbX.........",
                ".......XbBbX........",
                "....XXXXbBbXXXX.....",
                "...XbbbbbBbbbbbX....",
                "....XbBBBBBBbdX.....",
                ".GG..XbbBBBbdX...GG.",
                "gGG...XbbBbdX....GGg",
                "gGg..XbbbdbbdX...gGg",
                "gGG..XbbdXdbdX...GGg",
                ".gGgXbbdX.XdbdX.gGg.",
                "gGGgXbdX...XddX.gGGg",
                ".gGGgXX.....XX.gGGg.",
                "..gGGg........gGGg..",
                "...gGGGg....gGGGg...",
                ".....gGGgddgGGg.....",
                ".......gdbbdg.......",
                "........dbbd........",
                "....................",
            },
            ["CASH_10K"] = new[]
            {
                "....................",
                "....XXXXXXXXXXXXXXX.",
                "...XLLLLLBBBBLLLLLLX",
                "...XLLLLLBBBBLLLLLX.",
                "...XLLLLLBBBBLLLLLX.",
                "...XgggggbbbbgggggX.",
                "...XGGGGGbbbbGGGGGX.",
                "...XgggggbrrbgggggX.",
                ".XXXXXXXXXXXXXXXGGX.",
                "XLLLLLBBBBLLLLLLXgX.",
                "XLLLLLBBBBLLLLLXGGX.",
                "XLLLLLBBBBLLLLLXXX..",
                "XgggggbbbbgggggX....",
                "XGGGGGbbbbGGGGGX....",
                "XgggggbrrbgggggX....",
                "XGGGGGbbbbGGGGGX....",
                "XgggggbbbbgggggX....",
                "XGGGGGbbbbGGGGGX....",
                ".XXXXXXXXXXXXXX.....",
                "....................",
            },
            ["SECTIONS_5"] = new[]
            {
                ".XXXXXXXXXXXXXXXX...",
                ".XhhGGGGGGGGGGXrrX..",
                ".XhhGGGGGGGGGGXrrX..",
                ".XhsGGGGGGGGGGXrrX..",
                ".XhSGXXXXXXXXGXXX...",
                ".XhhGXqqqqqqXGXaaX..",
                ".XhhGXmmmmwwXGXaaX..",
                ".XhhGXXXXXXXXGXaaX..",
                ".XhhGGGGGGGGGGXXX...",
                ".XhsGGGGGGGGGGXNNX..",
                ".XhSGGGGGGGGGGXNNX..",
                ".XhhGggggggggGXNNX..",
                ".XhhGGGGGGGGGGXXX...",
                ".XhhGGGGGGGGGGXLLX..",
                ".XhhGGGGGGGGGGXLLX..",
                ".XhsGGGGGGGGGGXLLX..",
                ".XhSGGGGGGGGGGXXX...",
                ".XhhGGGGGGGGGGXSSX..",
                ".XhhGGGGGGGGGGXSSX..",
                ".XXXXXXXXXXXXXXSSX..",
            },
            ["SECTIONS_ALL"] = new[]
            {
                ".......XXXXXX.......",
                ".....XXCCCCCCXX.....",
                "....XCCCCCCCCCCX....",
                "...XCccCCggCCCCCX...",
                "..X.CcgggggCCCgg.Xb.",
                "..XCcggLggggCCgggX..",
                "..XCCggggggCCCCggX.b",
                "..XCCCggggCCCCCgCX..",
                ".XCCCCCgggCCCCCCCCXb",
                "..XCCCCCggCCCCggCX..",
                "..XCCCCCCgCCCggggX.b",
                "..XCCCCCCCCCgggggX..",
                "..X.CCCCCCCCCggg.Xb.",
                "...XCCCCCCCCCCgCX...",
                "....XCCCCCCCCCCX.b..",
                ".....XXCCCCCCXX.....",
                ".......XXXXXX..b....",
                ".........dd.........",
                ".....XBBBBBBBBX.....",
                ".....XXXXXXXXXX.....",
            },
            ["TOP_SHELF"] = new[]
            {
                "....................",
                "XXXXXXXXXXXX........",
                "XwwwwwwwwwwXX.......",
                "XwAAAAAAAAwXQX......",
                "XwAyyyyyyAwXQQX.....",
                "XwAyyyyyyAwXQQQX....",
                "XwAyyyyyyAwXQQQXXX..",
                "XwAyyyyyyAwXQQQXqX..",
                "XwAyyyyyyAwXQQQXXX..",
                "XwAAAAAAAAwXQQX..X..",
                "XwwwwwwwwwwXQX..XsX.",
                "XXXXXXXXXXXXX...XsX.",
                "................XsX.",
                ".a...a...a......XsX.",
                "...a............XsX.",
                ".......a.......XsXX.",
                "..............XsXsX.",
                ".............XsX.XsX",
                ".............XX..XsX",
                "..................XX",
            },
            ["FULL_KIT"] = new[]
            {
                "..........m.........",
                ".XXX......m..XXXXX..",
                "XXXXXXXXX.m..XwwwX..",
                "XqqXXXqqX.mXXXXXXXXX",
                "XqXsSXqrX.mXSSSSSSSX",
                "XqXSNXqqX.mXsXXXXXsX",
                "XqqXXXqqX.mXsssssgsX",
                "XXXXXXXXX.mXXXXXXXXX",
                "..........m.........",
                "mmmmmmmmmmmmmmmmmmmm",
                "..........m.........",
                "XXXXXXXXX.mXXXXXXXXX",
                "XSSSSSSSX.mXwwwywwwX",
                "XSXXXXXSX.mXwwyyywwX",
                "XSXCcCXSX.mXXXXXXXXX",
                "XSXXXXXSX.m....X....",
                "XXXXXXXXX.m....X....",
                "...XsX....m...X.X...",
                ".XXXXXXX..m..X...X..",
                "..........m.........",
            },
            ["WEEK_ONE"] = new[]
            {
                ".....X........X.....",
                ".....s........s.....",
                ".XXXXXXXXXXXXXXXXXX.",
                ".XrrrXrrrrrrrrXrrrXm",
                ".XrrrrrrrrrrrrrrrrXm",
                ".XrrrrrrrrrrrrrrrrXm",
                ".XDDDDDDDDDDDDDDDDXm",
                ".XwwwwwwwwwwwwwwwwXm",
                ".XwwwwwXXXXXXwwwwwXm",
                ".XwwwwwXXXXXXwwwwwXm",
                ".XwwwwwwwwwXXwwwwwXm",
                ".XwwwwwwwwwXXwwwwwXm",
                ".XwwwwwwwXXwwwwwwwXm",
                ".XwwwwwwwXXwwwwwwwXm",
                ".XwwwwwwwXXwwwwwwwXm",
                ".XwwwwwwwXXwwwwwwwXm",
                ".XwwwwwwwXXwwwwwwwXm",
                ".XwmmmmmmmmmmmmmmwXm",
                ".XXXXXXXXXXXXXXXXXXm",
                "..mmmmmmmmmmmmmmmmmm",
            },
            ["FLAWLESS"] = new[]
            {
                ".................w..",
                ".................w..",
                "...............wwSww",
                "....XXXXXXXXXXX..w..",
                "...XcwcccNcccccX.w..",
                "..XcwccccNccccNNX...",
                ".XcwcccccNcccccNNX..",
                "XXXXXXXXXXXXXXXXXXX.",
                "XcwccNNcccNcccNNNCX.",
                ".XcwccNNccNccNNNCX..",
                "..XcccNNccNcNNNCX...",
                "...XccNNNcNNNNCX....",
                "....XccNNNNNNCX.....",
                ".....XcNNNNNCX......",
                "......XcNNNCX.......",
                ".......XcNCX........",
                "........XCX.........",
                ".........X..........",
                "....................",
                "....................",
            },
            ["CLEAN_PRINT_DAY"] = new[]
            {
                "....................",
                ".......XXXXXXXXXXXX.",
                ".....XXXXXXXXXXXXpX.",
                "...XXXXXXXXXXXXpXpX.",
                ".XXXXXXXXXXXXpXpXpX.",
                ".XwwwwwwwwwwXpXpXpX.",
                ".XwqqqqqqwwwXpXpXpX.",
                ".XwwwwwwwwwwXpXpXpX.",
                ".XwMMMMMMMMwXpXpXpX.",
                ".XwwwwwwwwwwXpXpXpX.",
                ".XwMMMMMMMMwXpXpXXX.",
                ".XwwwwwwwwwwXpXpXggX",
                ".XwMMMMMMMMwXpXXgGgX",
                ".XwwwwwwwXXwXpXgGgX.",
                ".XwMMMMMXLgXXXgGgXX.",
                ".XwwwwwwXgGgXgGgXXX.",
                ".XwMMMMMwXgGgGgXX...",
                ".XwwwwwwwwXgGgX.....",
                ".XXXXXXXXXXXgX......",
                "............X.......",
            },
            ["SMOOTH_TALKER"] = new[]
            {
                ".....dAAAAAAAAd.....",
                "...AA..........AA...",
                "...dd..........dd...",
                ".....dddddddddd.....",
                "....................",
                "..XXXXXXXXXXXXXXXX..",
                ".XwwwwwwwwwwwwwwwwX.",
                "XwwwwwXwwwwwwXwwwwwX",
                "XwwwwwwwwwwwwwwwwwwX",
                "XwwwwXwwwwwwwwXwwwwX",
                "XwwwwwXwwwwwwXwwwwwX",
                "XwwwwwwXXXXXXwwwwwwX",
                "XwwwwwwwwwwwwwwwwwwX",
                "XwwwwwwwwwwwwwwwwwwX",
                ".XwwwwwwwwwwwwwwwwX.",
                "..XXwwwXXXXXXXXXXX..",
                "...XwwX.............",
                "...XwX..............",
                "....X...............",
                "....................",
            },
            ["CLEAN_SHEET"] = new[]
            {
                ".......XXXXXX.......",
                "......XbBBBBbX......",
                "..XXXXXddddddXXXXX..",
                "..XttttXXXXXXttttX..",
                "..XtXXXXXXXXXXXXtX..",
                "..XtXwwwwwwwwwwXtX..",
                "..XtXwwwgwwwwwwXtX..",
                "..XtXgwgwwMMMMwXtX..",
                "..XtXwgwwwwwwwwXtX..",
                "..XtXwwwwwwwwwwXtX..",
                "..XtXwwwgwwwwwwXtX..",
                "..XtXgwgwwMMMMwXtX..",
                "..XtXwgwwwwwwwwXtX..",
                "..XtXwwwwwwwwwwXtX..",
                "..XtXwwwgwwwwwwXtX..",
                "..XtXgwgwwMMMMwXtX..",
                "..XtXwgwwwwwwwwXtX..",
                "..XtXwwwwwwwwwwXtX..",
                "..XtXXXXXXXXXXXXtX..",
                "..XXXXXXXXXXXXXXXX..",
            },
            ["LYING_LOW"] = new[]
            {
                "....................",
                "....XXX.............",
                "...XwwwX............",
                "...XwwwXX...........",
                "...XwwwX......XNX...",
                "...XwwwXX.....XNX...",
                "...XwwwX......XNX...",
                "...XwwwXX.....XNX...",
                "...XwwwX......XNX...",
                "...XwwwXX....XXNXX..",
                "...XwCwX....XNNNNNX.",
                "...XwCwXX....XNNNX..",
                "...XwCwX......XNX...",
                "..XXCCCXX......X....",
                ".XcCCCCCCX..........",
                ".XcCCCCCCX..........",
                ".XCCCCCCCX..........",
                "..XCCCCCX...........",
                "...XXXXX............",
                "....................",
            },
            ["CANDLELIGHT"] = new[]
            {
                "XXXXXXXKKKKKKXXXXXXX",
                "XhhhhKKKKKyKKKKhhhhX",
                "XhhhhKKTTyyyTKKhhhhX",
                "XhhhKKTTTyAyTTKKhhhX",
                "XhhhKKTToyAyoTKKhhhX",
                "XhhhKKTTToaoTTKKhhhX",
                "XhhhKKTTTTXTTTKKhhhX",
                "XhhhKKXXXXXXXXKKhhhX",
                "XhhhKKXSwwwwwXKKhhhX",
                "XhhhhKXSwwSwwXKhhhhX",
                "XhhhhKXwwwwwSXKhhhhX",
                "XhhhhhXwwwwwSXhhhhhX",
                "XhhhhhXwwwwwSXhhhhhX",
                "XhhhhhXwwwwwSXhhhhhX",
                "XhhhhhXwwwwwSXhhhhhX",
                "XhhhhhXwwwwwSXhhhhhX",
                "XhhXXXXXXXXXXXXXXhhX",
                "XhhXBBBBBBBBBBBBXhhX",
                "XhhXXXXXXXXXXXXXXhhX",
                "XXXXXXXXXXXXXXXXXXXX",
            },
            ["WORD_OF_MOUTH"] = new[]
            {
                "....................",
                "...XXXXX......X.....",
                "..XrRRrrX......X....",
                ".XrRrrrrX..X....X...",
                ".XrrrrrX....X...X...",
                ".XrrrrX.....X...X...",
                ".XrrrX.....X...X....",
                ".XrrrX........X.....",
                ".XrrrX..............",
                "..XrrrX.............",
                "..XrrrrX.......XXX..",
                "...XrrrrX.....XrrrX.",
                "....XrrrrXX..XrrrrX.",
                ".....XXrrrrXXrrrrX..",
                ".......XXrrrrrrDX...",
                ".........XXDDDDX....",
                "...........XXXX.....",
                "....................",
                "....................",
                "....................",
            },
            ["YOUSSEF_MADE_IT"] = new[]
            {
                "....................",
                "....................",
                "....................",
                "....................",
                "............XXXXX...",
                "...........XhhhhhX..",
                "...........XhkkkhX..",
                "...........XkXkXkX..",
                "...........XkkkkkX..",
                "....XXXXX..XkKKkkX..",
                "...XhhhhhX..XkkkX...",
                "...XhkkkhX.XXnkkXX..",
                "...XcXcXXXXnnnnnnnX.",
                "...XkkkkkXnnnnnnnnnX",
                "...XkkKkkXnnnnnnnnnX",
                "....XkkkX.nnnnnnnnnX",
                "...XXrkkrXnnnnnnnnnX",
                "..XrrrrrrrnnnnnnnnnX",
                ".XrrrrRrrrnnnnnnnnnX",
                ".XrrrrRrrrnnnnnnnnnX",
            },
            ["NOT_TODAY"] = new[]
            {
                "..........XX..........",
                "..........ss..........",
                ".........X..X.........",
                "........X....X........",
                ".......X......X.......",
                "XXXXXXXXXXXXXXXXXXXXXX",
                "RRRRRRRRRRRRRRRRRRRRRR",
                "rrrrrrrrrrrrrrrrrrrrrr",
                "rrrrrrrrrrrrrrrrrrrrrr",
                "rwwrwrrwwwrwwwrwwrwwrr",
                "rwrrwrrwrwrwrrrwrrwrwr",
                "rwrrwrrwrwrwwwrwwrwrwr",
                "rwrrwrrwrwrrrwrwrrwrwr",
                "rwwrwwrwwwrwwwrwwrwwrr",
                "rrrrrrrrrrrrrrrrrrrrrr",
                "DDDDDDDDDDDDDDDDDDDDDD",
                "XXXXXXXXXXXXXXXXXXXXXX",
                "mmmmmmmmmmmmmmmmmmmmmm",
                "......................",
                "......................",
                "......................",
                "......................",
            },
            ["BACKUP_PAIR"] = new[]
            {
                "....................",
                "XXXXXXXXXXXXXXXXXXXX",
                "XQQQQQQXXXXQQQQQQQX.",
                "XQSQQQQX..XQSQQQQQX.",
                "XQQSQQQX..XQQSQQQQX.",
                ".XQQQQX....XQQQQQX..",
                "..XXXX......XXXXX...",
                "..........a.........",
                ".........aaa........",
                "........aaaaa.......",
                ".........aaa........",
                ".........aaa........",
                "....................",
                "...XXXX......XXXX...",
                "..XccwcX....XccwcX..",
                ".XXcccccXXXXcccccXX.",
                "..XcccccX..XcccccX..",
                "...XXXXX....XXXXX...",
                "....................",
                "....................",
            },
            ["INCOGNITO"] = new[]
            {
                "......XXXXXXXX......",
                ".....XhhhhhhhhX.....",
                "....XhhhhhhhhhhX....",
                "....XhhkkkkkkhhX....",
                "...XXXXXXXXXXXXXX...",
                "...XQSQQQXXQSQQQX...",
                "...XQQSQQXXQQSQQX...",
                "....XQQQX..XQQQX....",
                "....XkXXkkkkXXkX....",
                "....XkkkkKkkkkkX....",
                ".....XkkkkkkkkX.....",
                ".....XkkXXXXkkX.....",
                "......XkkkkkkX......",
                "....XXXXkkkkXXXX....",
                "...XtttTXXXXTtttX...",
                "..XttttTTttTTttttX..",
                ".XttttttTttTttttttX.",
                ".XttttttTttTttttttX.",
                ".XttttttTttTttttttX.",
                ".XttttttTttTttttttX.",
            },
            ["MIX_UP"] = new[]
            {
                "......aaaaaaaa......",
                ".....a........a.....",
                ".....a.......aaa....",
                "..............a.....",
                "....................",
                ".XXXXXXXX..XXXXXXXX.",
                ".XvnnnnnX..XDrrrrrX.",
                ".XvnnnnnX..XDrrrrrX.",
                ".XvnnnnnX..XDrrrrrX.",
                ".XvnnbnnX..XDrrbrrX.",
                ".XvnbBbnX..XDrbBbrX.",
                ".XvnnbnnX..XDrrbrrX.",
                ".XvnnnnnX..XDrrrrrX.",
                ".XvnnnnnX..XDrrrrrX.",
                ".XvnnnnnX..XDrrrrrX.",
                ".XXXXXXXX..XXXXXXXX.",
                ".....a..............",
                "....aaa.......a.....",
                ".....a........a.....",
                "......aaaaaaaa......",
            },
            ["SAMIR_CAUGHT"] = new[]
            {
                "....................",
                "...XXXXXXXXXXXXXX...",
                "..XXXnnnnnnnnnnXXX..",
                ".XrrrXnbbbbbbnXrrrX.",
                "..XrXrXnnnnnnXrrrX..",
                "...XrRrXnnnnXrrrX...",
                "...XXrrrXnnXrrRXX...",
                "...XvXrrrXXrrrXnX...",
                "...XvnXrrRrrrXnnX...",
                "...XvnnXrrrrXnnnX...",
                "...XvnnXrrrRXnnnX...",
                "...XvnXrrrrrrXnnX...",
                "...XvXrrrXXrrRXnX...",
                "...XXrrRXnnXrrrXX...",
                "...XrrXXnnnnXrrrX...",
                "..XrrrXnnnnnnXrXrX..",
                ".XrrrXnnnnnnnnXrrrX.",
                "..XXXnnnnnnnnnnXXX..",
                "...XXXXXXXXXXXXXX...",
                "....................",
            },
        };
    }
}
