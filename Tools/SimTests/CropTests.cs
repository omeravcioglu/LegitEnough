using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LegitEnough.Sim;

namespace LegitEnough.SimTests
{
    /// <summary>
    /// Sim/Photo/Framing.cs: the shared crop check (crop readout, THE PHOTO summary, coach)
    /// against the scorer it mirrors (PhotoEvaluator, unchanged).
    /// </summary>
    public static class CropTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string F(float v, string fmt = "0.000") => v.ToString(fmt, Inv);

        static readonly int[] Sensors = { 1600, 2400, 3600 };
        static readonly float[] Zooms = { 1f, 1.5f, 2f };

        // ------------------------------------------------------------------ fixtures

        static List<(string Name, CustomerProfile P, bool Hat)> Subjects()
        {
            var story = StoryCast.CreateProfiles().ToDictionary(p => p.Id);
            var demo = DemoCast.CreateProfiles().ToDictionary(p => p.Id);
            var list = new List<(string, CustomerProfile, bool)>
            {
                ("Samir", story[StoryCast.SamirId], false),
                ("Elif", demo[DemoCast.ElifId], true),
                ("Kenji", demo[DemoCast.KenjiId], false),
                ("Viktor", demo[DemoCast.ViktorId], false),
                ("Anja", demo[DemoCast.AnjaId], false),
            };
            for (int i = 1; i <= 8; i++)
            {
                var c = CustomerGenerator.Generate(i, 12345UL);
                list.Add(("walkin" + i, c, c.Look.Hat != HatStyle.None));
            }
            return list;
        }

        /// <summary>Every head-size rule the game can show: all countries, the demo's France change, news extremes.</summary>
        static List<PhotoRules> AllRules()
        {
            var specs = new List<PhotoRules>();
            foreach (var c in Countries.CreateAll()) specs.Add(c.Photo);
            foreach (var (min, max) in new[] { (0.65f, 0.75f), (0.45f, 0.55f), (0.75f, 0.85f) })
            {
                var r = new PhotoRules { CoverageMin = min, CoverageMax = max };
                specs.Add(r);
            }
            var seen = new HashSet<string>();
            var unique = new List<PhotoRules>();
            foreach (var r in specs)
                if (seen.Add(F(r.CoverageMin) + "-" + F(r.CoverageMax))) unique.Add(r);
            return unique;
        }

        static string RuleName(PhotoRules r) => $"{F(r.CoverageMin * 100f, "0")}-{F(r.CoverageMax * 100f, "0")}";

        static PhotoCapture Cap(CustomerProfile c, bool hat, float zoom, float panY, float seat, float slouch, HeadPose pose, int sensor) =>
            new PhotoCapture
            {
                CustomerId = c.Id, Look = c.Look, Pose = pose, SeatOffset = seat, Slouch = slouch, HatOn = hat,
                Zoom = zoom, PanX = 0f, PanY = panY, SensorHeightPx = sensor, Noise = 0.35f,
                Backdrop = Backdrop.White, Expression = Expression.Neutral,
            };

        static readonly Issue[] FramingIssues = { Issue.HeadCutOff, Issue.HeadTooSmall, Issue.HeadTooLarge, Issue.OffCenter, Issue.LowResolution };

        /// <summary>The scorer's framing (+ resolution) findings, as comparable strings.</summary>
        static List<string> ScorerFindings(PhotoCapture p, PhotoRules rules)
        {
            var report = new QualityReport();
            PhotoEvaluator.Assess(p, rules, p.CustomerId, report);
            var list = new List<string>();
            foreach (var f in report.Findings)
                if (Array.IndexOf(FramingIssues, f.Issue) >= 0)
                    list.Add(Key(f.Issue, f.Detail, f.Issue == Issue.LowResolution ? f.Penalty : 0f));
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        static string Key(Issue i, string detail, float penalty) =>
            i + "|" + (i == Issue.LowResolution ? F(penalty, "0") : detail);

        /// <summary>The same findings predicted from Framing.Check alone.</summary>
        static List<string> FramingFindings(FramingResult r)
        {
            var list = new List<string>();
            if (r.CrownCut || r.ChinCut) list.Add(Key(Issue.HeadCutOff, r.CrownCut ? "top of head cut" : "chin cut", 0f));
            if (r.Size == FramingSize.TooSmall) list.Add(Key(Issue.HeadTooSmall, Labels.Percent(r.Coverage), 0f));
            if (r.Size == FramingSize.TooLarge) list.Add(Key(Issue.HeadTooLarge, Labels.Percent(r.Coverage), 0f));
            if (r.Side != FramingSide.Centred) list.Add(Key(Issue.OffCenter, "horizontal", 0f));
            if (r.Top == FramingTop.TooFar) list.Add(Key(Issue.OffCenter, "head too low", 0f));
            if (r.Top == FramingTop.TooClose) list.Add(Key(Issue.OffCenter, "head touches top", 0f));
            if (r.LowRes) list.Add(Key(Issue.LowResolution, "", r.VeryLowRes ? 25f : 12f));
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        static bool HasFramingFinding(PhotoCapture p, PhotoRules rules, bool countLowRes)
        {
            foreach (var s in ScorerFindings(p, rules))
                if (countLowRes || !s.StartsWith(nameof(Issue.LowResolution), StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>A crop (or null if it doesn't fit the image) from ruler readings and a sideways offset.</summary>
        static Box? CropFromReadings(Box head, float crownReading, float chinReading, float dxOfWidth)
        {
            float cov = chinReading - crownReading;
            if (cov <= 0.01f) return null;
            float h = head.H / cov;
            float w = PortraitGeometry.CropWidthFor(h);
            float y = head.Y - (Framing.GuideTop + crownReading) * h;
            float cx = head.CenterX - dxOfWidth * w;
            var crop = Box.FromCenter(cx, y + h * 0.5f, w, h);
            if (h > PortraitGeometry.FullCrop().H + 1e-5f) return null;
            if (!crop.Inside(new Box(0f, 0f, 1f, 1f), 0f)) return null;
            return crop;
        }

        // ------------------------------------------------------------------ (1) agreement

        public static void Crop_FramingAgreesWithScorer()
        {
            var subjects = Subjects();
            var rules = AllRules();
            var poses = new[] { HeadPose.Center, new HeadPose(1, 0), new HeadPose(-2, 1) };
            int checks = 0, boundary = 0;
            var verdicts = new Dictionary<string, int>();
            var rng = Rng.For(4242UL, 77UL);
            foreach (var (name, prof, hat) in subjects)
                foreach (var zoom in Zooms)
                    foreach (var panY in new[] { CameraRig.DefaultPanY, 0.3f })
                        foreach (var seat in new[] { -0.35f, 0f, 0.3f })
                        {
                            var pose = poses[(int)(rng.NextFloat() * poses.Length) % poses.Length];
                            int sensor = Sensors[(checks / 7) % Sensors.Length];
                            var p = Cap(prof, hat, zoom, panY, seat, 0.075f, pose, sensor);
                            Box head = p.HeadInFrame;
                            var crops = new List<Box>();
                            // Dense grid over the whole image.
                            for (float ch = 0.15f; ch <= 0.97f; ch += 0.07f)
                                for (float cx = 0f; cx <= 1.0001f; cx += 0.125f)
                                    for (float cy = 0f; cy <= 1.0001f; cy += 0.125f)
                                        crops.Add(PortraitGeometry.MakeCrop(cx, cy, ch));
                            // Right on (and a hair either side of) every threshold.
                            foreach (var r in rules)
                                foreach (var cov in new[] { r.CoverageMin, r.CoverageMax, (r.CoverageMin + r.CoverageMax) * 0.5f })
                                    foreach (var top in new[] { -0.01f, PhotoEvaluator.TopMarginMin, Framing.GuideTop, PhotoEvaluator.TopMarginMax })
                                        foreach (var dx in new[] { 0f, PhotoEvaluator.CenterTolerance, -PhotoEvaluator.CenterTolerance })
                                            foreach (var eps in new[] { -1e-4f, 0f, 1e-4f })
                                            {
                                                float h = head.H / (cov + eps);
                                                float w = PortraitGeometry.CropWidthFor(h);
                                                crops.Add(new Box(head.CenterX - (0.5f + dx + eps) * w, head.Y - (top + eps) * h, w, h));
                                                boundary++;
                                            }
                            // Chin-cut side too.
                            foreach (var bottom in new[] { -0.02f, -0.01f, 0f })
                            {
                                float h = head.H / 0.7f;
                                crops.Add(new Box(head.CenterX - PortraitGeometry.CropWidthFor(h) * 0.5f, head.Bottom + bottom * h - h, PortraitGeometry.CropWidthFor(h), h));
                            }

                            foreach (var crop in crops)
                            {
                                p.Crop = crop;
                                foreach (var r in rules)
                                {
                                    var f = Framing.Check(p, r);
                                    var want = ScorerFindings(p, r);
                                    var got = FramingFindings(f);
                                    if (!want.SequenceEqual(got))
                                        throw new AssertException($"{name} z{zoom} seat {seat} rule {RuleName(r)} crop {crop} head {head}: scorer [{string.Join("; ", want)}] vs framing [{string.Join("; ", got)}]");
                                    Assert.True(f.Coverage == p.Coverage, "coverage identical to PhotoCapture.Coverage");
                                    Assert.True(f.FramingOk == (got.Count == 0 || (got.Count == 1 && f.LowRes)), "FramingOk == no framing finding");
                                    Assert.True(f.Clean == (got.Count == 0), "Clean == no finding at all");
                                    Assert.Near(f.ChinReading, Framing.Reading(crop, head.Bottom), 1e-4f, "chin reading = ruler at the chin");
                                    Assert.Near(f.CrownReading, Framing.Reading(crop, head.Y), 1e-4f, "crown reading = ruler at the crown");
                                    checks++;
                                    string v = f.Size + "/" + f.Top + "/" + f.Side;
                                    verdicts.TryGetValue(v, out int n);
                                    verdicts[v] = n + 1;
                                }
                                // Rule unknown: size never judged, never "ok".
                                var u = Framing.Check(p, null);
                                Assert.True(u.Size == FramingSize.Unknown && !u.HasRule && !u.FramingOk, "unknown rule");
                                // Unknown camera: never low-res.
                                Assert.True(!Framing.Check(head, crop, rules[0], 0).LowRes, "unknown sensor");
                            }
                        }
            Assert.True(verdicts.Count >= 30, "grid covers many verdict combinations");
            foreach (FramingTop t in Enum.GetValues(typeof(FramingTop)))
                Assert.True(verdicts.Keys.Any(k => k.Contains("/" + t + "/")), $"grid hits Top={t}");
            foreach (FramingSide sd in Enum.GetValues(typeof(FramingSide)))
                Assert.True(verdicts.Keys.Any(k => k.EndsWith("/" + sd)), $"grid hits Side={sd}");
            Console.WriteLine($"    {checks} crop x rule checks ({boundary} on thresholds), {rules.Count} rules [{string.Join(" ", rules.Select(RuleName))}], {verdicts.Count} verdict combos: all agree with PhotoEvaluator");
        }

        // ------------------------------------------------------------------ (2) literal player

        public static void Crop_LiteralPlayerPasses()
        {
            var subjects = Subjects();
            var rules = AllRules();
            float tol = Framing.CrownTolerance;
            Assert.Near(0.10f, tol, 1e-6f, "crown tolerance is 10% of the frame");
            const float e = 1e-4f; // stay a hair inside drawn edges (float rounding at the exact edge)
            int shots = 0, fits = 0, naiveFail = 0, naiveTotal = 0, slackOnly = 0;
            var naiveExample = "";
            var lowRes = new SortedDictionary<string, (int lo, int all)>(StringComparer.Ordinal);
            var unreachable = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var gated = new SortedDictionary<string, int>(StringComparer.Ordinal);

            foreach (var (name, prof, hat) in subjects)
                foreach (var zoom in new[] { 1f, 1.25f, 1.5f, 1.75f, 2f })
                    foreach (var panY in new[] { CameraRig.DefaultPanY, 0.45f, 0.3f })
                        foreach (var seat in new[] { -0.35f, -0.15f, 0f, 0.15f, 0.35f })
                        {
                            var p = Cap(prof, hat, zoom, panY, seat, 0.075f, HeadPose.Center, 1600);
                            Box head = p.HeadInFrame;
                            // Only shots the camera coach would approve (head centred-ish, crown in).
                            if (!Framing.ShotCroppable(head)) continue;
                            shots++;
                            foreach (var r in rules)
                            {
                                string rz = $"{RuleName(r)} z{F(zoom, "0.00")}";
                                gated.TryGetValue(rz, out int g);
                                gated[rz] = g + 1;
                                bool anyPass = false;
                                // (a) The guarantee: crown within ±d of the dashed line and the chin
                                // inside the band pulled in by d at both ends (d = 0 .. half the band),
                                // nose within the drawn centre tolerance  =>  no framing finding.
                                float half = (r.CoverageMax - r.CoverageMin) * 0.5f;
                                foreach (var d in new[] { 0f, 0.01f, 0.02f, half - 2f * e })
                                    foreach (var cs in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
                                        foreach (var t in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                                            foreach (var dx in new[] { -1f, 0f, 1f })
                                            {
                                                float c = cs * d;
                                                float lo = r.CoverageMin + d + e, hi = r.CoverageMax - d - e;
                                                if (lo > hi) { lo = hi = (r.CoverageMin + r.CoverageMax) * 0.5f; }
                                                float chin = lo + (hi - lo) * t;
                                                var crop = CropFromReadings(head, c, chin, dx * (Framing.CentreTolerance - e));
                                                if (crop == null) continue;
                                                fits++;
                                                p.Crop = crop.Value;
                                                if (HasFramingFinding(p, r, false))
                                                    throw new AssertException($"{name} {rz} seat {F(seat)} pan {F(panY)}: crown {F(c)} off the line, chin {F(chin)} on the ruler, dx {F(dx)}: [{string.Join("; ", ScorerFindings(p, r))}]");
                                                anyPass = true;
                                                var fr = Framing.Check(p, r);
                                                Assert.True(fr.FramingOk, "Framing agrees: ok");
                                                if (d == 0f && cs == 0f)
                                                {
                                                    Assert.Near(fr.Coverage, fr.ChinReading, 2e-4f, "crown on the line: chin reading == coverage");
                                                    lowRes.TryGetValue(rz, out var lr);
                                                    lowRes[rz] = (lr.lo + (fr.LowRes ? 1 : 0), lr.all + 1);
                                                }
                                            }
                                // (b) Parts that hold on their own: crown anywhere within ±10% of the
                                // line never trips the top margin or the crown cut; a chin inside the
                                // band never gets cut; nose within tolerance is centred.
                                foreach (var c in new[] { -tol + e, -0.05f, 0f, 0.05f, tol - e })
                                    foreach (var chin in new[] { r.CoverageMin + e, r.CoverageMax - e })
                                    {
                                        var crop = CropFromReadings(head, c, chin, 0f);
                                        if (crop == null) continue;
                                        p.Crop = crop.Value;
                                        var fr = Framing.Check(p, r);
                                        Assert.True(fr.TopOk && fr.NoCut && fr.Centred, $"{rz}: crown {F(c)} chin {F(chin)} -> {fr.Top} cut {fr.CrownCut}/{fr.ChinCut}");
                                        // The naive reading (crown anywhere in ±10%, chin anywhere in the band)
                                        // is NOT a guarantee: size = chin reading - crown reading.
                                        naiveTotal++;
                                        if (!fr.SizeOk)
                                        {
                                            naiveFail++;
                                            if (naiveExample.Length == 0)
                                                naiveExample = $"{RuleName(r)}: crown {F(c * 100f, "0")}% off the line, chin on {F(chin * 100f, "0")} -> head size {Labels.Percent(fr.Coverage)} ({fr.Size}); the readout says so";
                                        }
                                    }
                                // (c) Reachability. The rule-aware camera gate (ShotCroppable(head, rule))
                                // only approves shots that some crop inside the image can save; its
                                // witness crop must really pass the scorer.
                                if (Framing.TryPassingCrop(head, r, 0, out Box witness))
                                {
                                    Assert.True(witness.Inside(new Box(0f, 0f, 1f, 1f), 1e-4f), $"{rz}: witness inside the image {witness}");
                                    p.Crop = witness;
                                    if (HasFramingFinding(p, r, false))
                                        throw new AssertException($"{name} {rz}: witness crop {witness} fails [{string.Join("; ", ScorerFindings(p, r))}]");
                                    Assert.True(Framing.ShotCroppable(head, r), "gate agrees");
                                    if (Framing.TryPassingCrop(head, r, 1600, out Box clean))
                                    {
                                        p.Crop = clean;
                                        if (HasFramingFinding(p, r, true))
                                            throw new AssertException($"{name} {rz}: clean witness {clean} fails [{string.Join("; ", ScorerFindings(p, r))}]");
                                    }
                                }
                                else
                                {
                                    Assert.True(!Framing.ShotCroppable(head, r), "gate rejects");
                                    unreachable.TryGetValue(rz, out int u);
                                    unreachable[rz] = u + 1;
                                    if (anyPass || AnyPassingCrop(p, r)) slackOnly++;
                                }
                            }
                        }
            Assert.True(shots > 200 && fits > 10000, $"enough approved shots/crops ({shots}/{fits})");
            Console.WriteLine($"    {shots} camera-approved shots x {rules.Count} rules, {fits} literal crops inside the image: crown within ±d of the line + chin in the band pulled in by d + nose within ±5% => no framing finding");
            Console.WriteLine($"    naive reading (crown anywhere ±10%, chin anywhere in band) fails size {naiveFail}/{naiveTotal}, e.g. {naiveExample}");
            var lrLines = lowRes.Where(kv => kv.Value.lo > 0).Select(kv => $"{kv.Key} {kv.Value.lo}/{kv.Value.all}").ToList();
            Console.WriteLine("    cam1 (1600 px) LOW RESOLUTION with crown on the line + chin in band: " + (lrLines.Count == 0 ? "never" : string.Join(", ", lrLines)) + " (z >= 1.5: never)");
            Assert.True(lowRes.Where(kv => kv.Key.EndsWith("z1.50") || kv.Key.EndsWith("z1.75") || kv.Key.EndsWith("z2.00")).All(kv => kv.Value.lo == 0), "no low-res at zoom >= 1.5 on cam1");
            var unr = unreachable.Select(kv => $"{kv.Key} {kv.Value}/{gated[kv.Key]}").ToList();
            Console.WriteLine("    rough gate (±0.12, crown >= 3%) passes but no crop can save it, so ShotCroppable(head, rule) says no: " + (unr.Count == 0 ? "none" : string.Join(", ", unr)) + $" ({slackOnly} of those pass only with zero slack)");
            Assert.True(unreachable.Keys.All(k => k.EndsWith("z1.75") || k.EndsWith("z2.00")), "the rough gate is enough below zoom 1.75");
        }

        static bool AnyPassingCrop(PhotoCapture p, PhotoRules r)
        {
            Box head = p.HeadInFrame;
            for (float cov = r.CoverageMin; cov <= r.CoverageMax + 1e-6f; cov += 0.005f)
                for (float top = 0.021f; top <= 0.219f; top += 0.006f)
                    for (float dx = -0.049f; dx <= 0.049f; dx += 0.007f)
                    {
                        var crop = CropFromReadings(head, top - Framing.GuideTop, top - Framing.GuideTop + cov, dx);
                        if (crop == null) continue;
                        p.Crop = crop.Value;
                        if (!HasFramingFinding(p, r, false)) return true;
                    }
            return false;
        }

        // ------------------------------------------------------------------ (3) opening frame + controls

        public static void Crop_OpeningFrameAndControls()
        {
            const float AreaW = 600f, AreaH = 800f;
            var subjects = Subjects();
            var rules = AllRules();
            var withUnknown = new List<PhotoRules>(rules) { null };
            int n = 0, tooTight = 0;
            float maxDrag = 0f, minDrag = float.MaxValue;
            foreach (var (name, prof, hat) in subjects)
                foreach (var zoom in new[] { 1f, 1.5f, 2f, 2.5f, 3f })
                    foreach (var seat in new[] { -0.35f, 0f, 0.35f })
                    {
                        var p = Cap(prof, hat, zoom, CameraRig.DefaultPanY, seat, 0.075f, HeadPose.Center, 1600);
                        Box head = p.HeadInFrame;
                        foreach (var r in withUnknown)
                        {
                            Box o = Framing.OpeningCrop(head, r);
                            n++;
                            string at = $"{name} z{F(zoom, "0.0")} seat {F(seat)} rule {(r == null ? "unknown" : RuleName(r))} crop {o}";
                            Assert.True(o.X * AreaW >= 26f - 0.01f && (1f - o.Right) * AreaW >= 26f - 0.01f, "inset 26 px left/right: " + at);
                            Assert.True(o.Y * AreaH >= 26f - 0.01f && (1f - o.Bottom) * AreaH >= 26f - 0.01f, "inset 26 px top/bottom: " + at);
                            Assert.Near(0.5f, o.CenterX, 1e-5f, "centred x: " + at);
                            Assert.Near(0.5f, o.CenterY, 1e-5f, "centred y: " + at);
                            Assert.Near(PortraitGeometry.CropWidthFor(o.H), o.W, 1e-5f, "35:45: " + at);
                            p.Crop = o;
                            var f = Framing.Check(p, r);
                            Assert.True(!f.FramingOk, "opening frame is not already passing: " + at);
                            if (r == null) { Assert.Near(Framing.OpeningUnknownHeight, o.H, 1e-5f, "unknown rule: 80% height"); continue; }
                            Assert.True(!f.SizeOk, "opening frame is never the right size: " + at);
                            if (f.Size == FramingSize.TooLarge) tooTight++;
                            else if (Math.Abs(f.Coverage - (r.CoverageMin - Framing.OpeningCoverageOffset)) > 1e-3f)
                                Assert.True(o.H >= Framing.MaxCropHeight(26f / AreaW, 26f / AreaH) - 1e-5f, "sized for min - 10% unless it hits the inset: " + at);
                            float target = head.H / ((r.CoverageMin + r.CoverageMax) * 0.5f);
                            if (zoom > 2f || target > PortraitGeometry.FullCrop().H) continue;
                            float need = Math.Abs(o.H - target) * AreaH;
                            maxDrag = Math.Max(maxDrag, need);
                            minDrag = Math.Min(minDrag, need);
                        }
                    }

            // Resizing keeps the centre; at an edge it shrinks instead of shifting.
            var c0 = Box.FromCenter(0.5f, 0.5f, PortraitGeometry.CropWidthFor(0.6f), 0.6f);
            var big = Framing.ResizeAboutCentre(c0, 2f);
            Assert.Near(0.5f, big.CenterX, 1e-5f, "resize keeps centre x");
            Assert.Near(0.5f, big.CenterY, 1e-5f, "resize keeps centre y");
            Assert.True(big.Inside(new Box(0f, 0f, 1f, 1f)), "resize stays inside");
            Assert.Near(Framing.MaxCropHeight(), big.H, 1e-4f, "grows to the largest frame");
            var off = Box.FromCenter(0.3f, 0.6f, PortraitGeometry.CropWidthFor(0.4f), 0.4f);
            var grown = Framing.ResizeAboutCentre(off, 0.9f);
            Assert.Near(0.3f, grown.CenterX, 1e-5f, "off-centre resize keeps centre x");
            Assert.Near(0.6f, grown.CenterY, 1e-5f, "off-centre resize keeps centre y");
            Assert.True(grown.Inside(new Box(0f, 0f, 1f, 1f)) && grown.H < 0.9f && grown.H > 0.4f, "shrinks at the edge instead of shifting: " + grown);
            var inset = Framing.ResizeAboutCentre(c0, 2f, 26f / AreaW, 26f / AreaH);
            Assert.True(inset.X * AreaW >= 25.99f && inset.Y * AreaH >= 25.99f, "resize honours the inset");
            var small = Framing.ResizeAboutCentre(c0, 0.01f);
            Assert.Near(Framing.MinCropHeight, small.H, 1e-5f, "minimum size");
            var moved = Framing.Move(c0, 2f, -2f);
            Assert.True(moved.Inside(new Box(0f, 0f, 1f, 1f)) && Math.Abs(moved.H - c0.H) < 1e-6f, "move clamps, keeps size");

            Console.WriteLine($"    {n} opening frames: centred, inset >= 26 px, never passing ({tooTight} open too tight because min - 10% won't fit); at zoom 1-2 the frame is {F(minDrag, "0")}-{F(maxDrag, "0")} px (of 800) taller or shorter than a mid-band crop");
        }

        // ------------------------------------------------------------------ (4) sizing about the guide point

        /// <summary>
        /// CROP sizes about the guide point (dashed TOP OF HEAD line x NOSE line): a crown put on
        /// the line stays on it, so "place it, then size it till the chin is in the band" works;
        /// the height asked for is always given, and an edge slides the frame instead of stopping it.
        /// </summary>
        public static void Crop_ResizeAboutGuide()
        {
            var image = new Box(0f, 0f, 1f, 1f);
            float full = PortraitGeometry.FullCrop().H;
            int n = 0, kept = 0, slid = 0;
            foreach (var h0 in new[] { 0.2f, 0.45f, 0.7f, full })
                foreach (var cx in new[] { 0f, 0.15f, 0.5f, 0.85f, 1f })
                    foreach (var cy in new[] { 0f, 0.2f, 0.5f, 0.8f, 1f })
                    {
                        var c = Box.FromCenter(cx, cy, PortraitGeometry.CropWidthFor(h0), h0).ClampInside(image);
                        foreach (var want in new[] { 0.01f, 0.12f, 0.3f, 0.55f, 0.8f, 0.95f, 2f })
                        {
                            var r = Framing.ResizeAboutGuide(c, want);
                            n++;
                            string at = $"{c} -> {F(want)}: {r}";
                            float expect = Math.Max(Math.Min(Framing.MinCropHeight, c.H), Math.Min(want, full));
                            Assert.Near(expect, r.H, 1e-5f, "the height asked for (clamped to min/full): " + at);
                            Assert.Near(PortraitGeometry.CropWidthFor(r.H), r.W, 1e-5f, "35:45: " + at);
                            Assert.True(r.Inside(image), "inside the image: " + at);
                            var anchored = new Box(c.CenterX - r.W * 0.5f, Framing.GuideY(c) - Framing.GuideTop * r.H, r.W, r.H);
                            if (anchored.Inside(image, 0f))
                            {
                                Assert.Near(Framing.GuideY(c), Framing.GuideY(r), 1e-5f, "dashed line stays: " + at);
                                Assert.Near(c.CenterX, r.CenterX, 1e-5f, "centre line stays: " + at);
                                kept++;
                            }
                            else slid++;
                            if (want > c.H + 1e-4f && c.H < full - 1e-4f)
                                Assert.True(r.H > c.H + 1e-5f, "growing never dead-ends at an edge: " + at);
                        }
                    }

            // The player's way: place the crown on the dashed line and the nose on the centre line,
            // then size it so the chin is mid-band. Every camera-approved shot that some crop can
            // pass (ShotCroppable with the rule) passes that way, or needs the edge slide (counted).
            var subjects = Subjects();
            var rules = AllRules();
            int shots = 0, passed = 0, edge = 0;
            foreach (var (name, prof, hat) in subjects)
                foreach (var zoom in new[] { 1f, 1.25f, 1.5f, 1.75f, 2f })
                    foreach (var panY in new[] { CameraRig.DefaultPanY, 0.45f, 0.3f })
                        foreach (var seat in new[] { -0.35f, 0f, 0.35f })
                        {
                            var p = Cap(prof, hat, zoom, panY, seat, 0.075f, HeadPose.Center, 0);
                            Box head = p.HeadInFrame;
                            foreach (var r in rules)
                            {
                                if (!Framing.ShotCroppable(head, r)) continue;
                                shots++;
                                var c = Framing.OpeningCrop(head, r);
                                c = Framing.Move(c, head.CenterX - c.CenterX, head.Y - Framing.GuideY(c));
                                float mid = (r.CoverageMin + r.CoverageMax) * 0.5f;
                                var sized = Framing.ResizeAboutGuide(c, head.H / mid);
                                p.Crop = sized;
                                var f = Framing.Check(p, r);
                                bool onLine = Math.Abs(Framing.GuideY(sized) - head.Y) < 1e-4f && Math.Abs(sized.CenterX - head.CenterX) < 1e-4f;
                                string at = $"{name} z{F(zoom, "0.00")} pan {F(panY)} seat {F(seat)} rule {RuleName(r)}: {sized} ({f.Size}/{f.Top}/{f.Side})";
                                if (onLine)
                                {
                                    Assert.True(f.FramingOk && f.CrownOnLine && f.ChinInBand, "placed then sized: passes, crown on the line, chin in the band: " + at);
                                    Assert.Near(f.Coverage, f.ChinReading, 2e-4f, "chin reading == head size: " + at);
                                }
                                else edge++;
                                if (f.FramingOk) passed++;
                            }
                        }
            Console.WriteLine($"    {n} resizes: the height asked for every time, inside the image; dashed lines still in {kept}, slid off an edge in {slid}; growing never stops at an edge");
            Console.WriteLine($"    place-then-size on {shots} croppable shot x rule pairs: {passed} pass as placed; {edge} needed an edge slide (crown kept off the line by the image edge)");
        }
    }
}
