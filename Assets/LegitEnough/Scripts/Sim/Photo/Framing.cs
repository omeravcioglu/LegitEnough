using System;

namespace LegitEnough.Sim
{
    // ======================================================================================
    // FRAMING: the one shared "is this crop right?" check (crop readout, THE PHOTO summary,
    // tutorial coach). Pure: no Rng, no state, no allocation. It mirrors
    // PhotoEvaluator.AssessFraming (+ the LowResolution check in PhotoEvaluator.Assess)
    // exactly, using the same expressions, so the screen can never disagree with the scorer.
    // Tools/SimTests/CropTests.cs proves the agreement over a dense grid.
    //
    // Coordinates: Box values are normalized frame coordinates (0..1 of the photo frame,
    // x by width, y by height, y down), the same as PhotoCapture.Crop / HeadInFrame.
    // Every fraction below (margins, readings, coverage) is a fraction of CROP HEIGHT,
    // except Dx, which is a fraction of CROP WIDTH (the scorer measures centring that way).
    //
    // API
    //   Framing.GuideTop = 0.12f            dashed TOP OF HEAD line, fraction of crop height
    //                                        below the crop's top edge (middle of the scorer's
    //                                        2%..22% top margin).
    //   Framing.CrownTolerance = 0.10f      the crown may sit this far above/below the line
    //                                        (= GuideTop - TopMarginMin) and the top margin passes.
    //   Framing.CentreTolerance = 0.05f     |Dx| allowed (= PhotoEvaluator.CenterTolerance).
    //   Framing.LowResPx = 600, VeryLowResPx = 450   sensor rows the crop must keep.
    //
    //   FramingResult Framing.Check(Box head, Box crop, PhotoRules rulesOrNull, int sensorHeightPx)
    //   FramingResult Framing.Check(PhotoCapture photo, PhotoRules rulesOrNull)
    //       rulesOrNull = what the PLAYER'S handbook says (see HandbookPhotoRules); null means
    //       the rule is unknown: Size = Unknown, HasRule = false, FramingOk = false.
    //       sensorHeightPx <= 0 means "unknown camera": LowRes is never flagged.
    //
    //   PhotoRules Framing.HandbookPhotoRules(GameSession s, string countryId)
    //       The binder's photo rules for that country, or null when the section isn't owned
    //       (an OUTDATED section returns the old rule, on purpose).
    //
    //   Ruler helpers (zero on the dashed line, readings in fractions of crop height):
    //   float Framing.GuideY(Box crop)                       frame-space y of the dashed line
    //   float Framing.RulerY(Box crop, float reading)        frame-space y of a ruler reading
    //   float Framing.Reading(Box crop, float y)             ruler reading at frame-space y
    //       With the crown on the line, Reading(crop, head.Bottom) == coverage exactly.
    //       CAVEAT: coverage = ChinReading - CrownReading. A crown 3% below the line needs the
    //       chin 3% lower than the band says (CropTests: "crown within ±d of the line and chin in
    //       the band pulled in by d" always passes; "crown anywhere in ±10%, chin anywhere in
    //       the band" does not). Show the live HEAD SIZE (Coverage) so the player is never misled.
    //   float Framing.MinCropHeightForResolution(int sensorHeightPx)   crop.H below this = LowRes
    //
    //   Crop-control helpers (optional, pure):
    //   Box Framing.OpeningCrop(Box head, PhotoRules rulesOrNull, float insetX = OpeningInsetX,
    //                           float insetY = OpeningInsetY)
    //       Centred on the frame centre (NOT on the head), inset from every edge, sized for
    //       coverage = CoverageMin - 0.10 (80% height when the rule is unknown). If that does
    //       not fit it opens at the largest inset frame, unless that frame would already be the
    //       right size: then it opens too tight (CoverageMax + 0.10). It never opens passing.
    //   Box Framing.ResizeAboutCentre(Box crop, float newHeight, float insetX = 0, float insetY = 0)
    //       Keeps the centre fixed (a zoom); at the image edges the frame shrinks, never shifts.
    //   Box Framing.ResizeAboutGuide(Box crop, float newHeight)
    //       What CROP uses: keeps the guide point (dashed TOP OF HEAD line x NOSE line) still, so
    //       a crown placed on the line stays there while sizing; at an image edge the frame is slid
    //       back in by the least amount (growing never dead-ends).
    //   Box Framing.Move(Box crop, float dx, float dy)   move and clamp inside the frame.
    //   Framing.OnLineTolerance = 0.025f    "on the line" for the readout/coach (CrownOnLine).
    //
    //   Camera step (tutorial "SHUTTER" approval):
    //   bool Framing.ShotCroppable(Box head)  rough gate: head centre within ±ShotCentreTolerance
    //       (0.12) of the frame centre, crown >= ShotCrownMin (0.03), chin inside the frame.
    //   bool Framing.ShotCroppable(Box head, PhotoRules rulesOrNull, int sensorHeightPx = 0)
    //       rough gate AND CanCropToPass: USE THIS ONE when the rule is known. The rough gate
    //       alone lets through z2 shots that no crop can save (small-head rules, head near ±0.12).
    //   bool Framing.CanCropToPass(Box head, PhotoRules rules, int sensorHeightPx = 0)
    //       some crop inside the image passes every framing check with slack (top 3-21%,
    //       nose ±4%); sensorHeightPx > 0 also demands full resolution (no LOW RESOLUTION).
    //   bool Framing.TryPassingCrop(Box head, PhotoRules rules, int sensorHeightPx, out Box crop)
    //       that crop (mid-band, crown on the line, nose centred when possible), e.g. for a
    //       faint "pencil" hint after repeated failures.
    //
    // FramingResult fields
    //   Coverage      head.H / crop.H (the handbook's HEAD SIZE)
    //   TopMargin     (head.Y - crop.Y) / crop.H
    //   BottomMargin  (crop.Bottom - head.Bottom) / crop.H
    //   Dx            signed (head.CenterX - crop.CenterX) / crop.W; > 0 = head right of centre
    //   CrownReading  ruler reading of the crown (TopMargin - GuideTop); < 0 = above the line
    //   ChinReading   ruler reading of the chin (CrownReading + Coverage)
    //   CrownCut, ChinCut           scorer's HeadCutOff ("top of head cut" / "chin cut")
    //   HasRule, RuleMin, RuleMax   the rule used (RuleMin/Max are 0 when unknown)
    //   Size  : Unknown | TooSmall | Ok | TooLarge          (HeadTooSmall / HeadTooLarge)
    //   Top   : CutOff | TooClose | Ok | TooFar              (HeadCutOff top / "head touches top" /
    //                                                        "head too low")
    //   Side  : Centred | SlideLeft | SlideRight             (OffCenter "horizontal"; the frame
    //                                                        should move that way)
    //   CropSensorPx, LowRes (< 600 rows), VeryLowRes (< 450 rows)
    //   SizeOk, TopOk, Centred, NoCut, FramingOk (all framing checks pass, rule known),
    //   Clean (FramingOk and not LowRes), CrownOnLine (|CrownReading| <= OnLineTolerance),
    //   ChinInBand (ChinReading inside the rule: what the ruler shows)
    // ======================================================================================

    public enum FramingSize { Unknown, TooSmall, Ok, TooLarge }

    public enum FramingTop { Ok, CutOff, TooClose, TooFar }

    public enum FramingSide { Centred, SlideLeft, SlideRight }

    public struct FramingResult
    {
        public float Coverage;
        public float TopMargin;
        public float BottomMargin;
        public float Dx;
        public float CrownReading;
        public float ChinReading;
        public bool CrownCut;
        public bool ChinCut;
        public bool HasRule;
        public float RuleMin;
        public float RuleMax;
        public FramingSize Size;
        public FramingTop Top;
        public FramingSide Side;
        public float CropSensorPx;
        public bool LowRes;
        public bool VeryLowRes;

        public bool SizeOk => Size == FramingSize.Ok;
        public bool TopOk => Top == FramingTop.Ok;
        public bool Centred => Side == FramingSide.Centred;
        public bool NoCut => !CrownCut && !ChinCut;
        /// <summary>Every framing check the scorer makes passes (false while the rule is unknown).</summary>
        public bool FramingOk => SizeOk && TopOk && Centred && NoCut;
        /// <summary>Framing passes and the crop keeps enough camera resolution.</summary>
        public bool Clean => FramingOk && !LowRes;
        /// <summary>The crown sits visibly on the dashed line (within Framing.OnLineTolerance).</summary>
        public bool CrownOnLine => !CrownCut && Math.Abs(CrownReading) <= Framing.OnLineTolerance;
        /// <summary>The chin's ruler reading is inside the rule's band (what the player sees).</summary>
        public bool ChinInBand => HasRule && ChinReading >= RuleMin && ChinReading <= RuleMax;
    }

    public static class Framing
    {
        /// <summary>Dashed TOP OF HEAD line: fraction of crop height below the crop's top edge.</summary>
        public const float GuideTop = 0.12f;
        /// <summary>How far (fraction of crop height) the crown may sit off the line and still pass.</summary>
        public const float CrownTolerance = GuideTop - PhotoEvaluator.TopMarginMin;
        public const float CentreTolerance = PhotoEvaluator.CenterTolerance;
        /// <summary>The scorer's "cut" slack: a head this little outside the crop is not cut.</summary>
        public const float CutSlack = 0.01f;
        public const float LowResPx = 600f;
        public const float VeryLowResPx = 450f;

        /// <summary>Default opening inset, as fractions of the 600 x 800 crop area (26 px each side).</summary>
        public const float OpeningInsetX = 26f / 600f;
        public const float OpeningInsetY = 26f / 800f;
        public const float OpeningUnknownHeight = 0.8f;
        public const float OpeningCoverageOffset = 0.10f;
        public const float MinCropHeight = 0.12f;

        public const float ShotCentreTolerance = 0.12f;
        public const float ShotCrownMin = 0.03f;

        public static FramingResult Check(PhotoCapture photo, PhotoRules rulesOrNull) =>
            Check(photo.HeadInFrame, photo.Crop, rulesOrNull, photo.SensorHeightPx);

        public static FramingResult Check(Box head, Box crop, PhotoRules rulesOrNull, int sensorHeightPx)
        {
            var r = new FramingResult();
            // Same expressions as PhotoEvaluator.AssessFraming, in the same order.
            float top = (head.Y - crop.Y) / crop.H;
            float bottom = (crop.Bottom - head.Bottom) / crop.H;
            r.TopMargin = top;
            r.BottomMargin = bottom;
            r.CrownCut = top < -CutSlack;
            r.ChinCut = bottom < -CutSlack;

            float coverage = head.H / crop.H;
            r.Coverage = coverage;
            r.CrownReading = top - GuideTop;
            r.ChinReading = r.CrownReading + coverage;

            if (rulesOrNull != null)
            {
                r.HasRule = true;
                r.RuleMin = rulesOrNull.CoverageMin;
                r.RuleMax = rulesOrNull.CoverageMax;
                if (coverage < rulesOrNull.CoverageMin) r.Size = FramingSize.TooSmall;
                else if (coverage > rulesOrNull.CoverageMax) r.Size = FramingSize.TooLarge;
                else r.Size = FramingSize.Ok;
            }
            else r.Size = FramingSize.Unknown;

            float dx = Math.Abs(head.CenterX - crop.CenterX) / crop.W;
            r.Dx = (head.CenterX - crop.CenterX) / crop.W;
            if (dx > PhotoEvaluator.CenterTolerance)
                r.Side = head.CenterX > crop.CenterX ? FramingSide.SlideRight : FramingSide.SlideLeft;
            else r.Side = FramingSide.Centred;

            if (top < -CutSlack) r.Top = FramingTop.CutOff;
            else if (top > PhotoEvaluator.TopMarginMax) r.Top = FramingTop.TooFar;
            else if (top < PhotoEvaluator.TopMarginMin) r.Top = FramingTop.TooClose;
            else r.Top = FramingTop.Ok;

            // Same expression as PhotoEvaluator.Assess.
            float cropPx = crop.H * sensorHeightPx;
            r.CropSensorPx = cropPx;
            if (sensorHeightPx > 0)
            {
                r.VeryLowRes = cropPx < VeryLowResPx;
                r.LowRes = cropPx < LowResPx;
            }
            return r;
        }

        /// <summary>
        /// The photo rules the player's binder shows for this country, or null if the section
        /// isn't owned (an outdated section returns the old rule, on purpose).
        /// </summary>
        public static PhotoRules HandbookPhotoRules(GameSession s, string countryId)
        {
            if (s == null || string.IsNullOrEmpty(countryId)) return null;
            var c = s.Country(countryId);
            if (c == null || !c.Owned) return null;
            return s.HandbookRules(countryId)?.Photo;
        }

        // ------------------------------------------------------------------ ruler

        public static float GuideY(Box crop) => crop.Y + GuideTop * crop.H;

        public static float RulerY(Box crop, float reading) => crop.Y + (GuideTop + reading) * crop.H;

        public static float Reading(Box crop, float y) => (y - crop.Y) / crop.H - GuideTop;

        /// <summary>Crop heights (normalized) below this keep fewer than 600 sensor rows: LowResolution.</summary>
        public static float MinCropHeightForResolution(int sensorHeightPx) =>
            sensorHeightPx > 0 ? LowResPx / sensorHeightPx : 0f;

        // ------------------------------------------------------------------ crop controls

        /// <summary>Largest crop height that fits the frame with these insets on every side.</summary>
        public static float MaxCropHeight(float insetX = 0f, float insetY = 0f)
        {
            Box full = PortraitGeometry.FullCrop();
            float byH = Math.Min(full.H, 1f - 2f * insetY);
            float byW = (1f - 2f * insetX) / PortraitGeometry.CropWidthFor(1f);
            return Math.Max(MinCropHeight, Math.Min(byH, byW));
        }

        public static Box OpeningCrop(Box head, PhotoRules rulesOrNull, float insetX = OpeningInsetX, float insetY = OpeningInsetY)
        {
            float maxH = MaxCropHeight(insetX, insetY);
            float h;
            if (rulesOrNull == null) h = OpeningUnknownHeight;
            else
            {
                float wantCov = rulesOrNull.CoverageMin - OpeningCoverageOffset;
                h = wantCov > 0.05f ? head.H / wantCov : maxH;
                if (h > maxH)
                {
                    // Too big to fit: if the largest frame would already be the right size,
                    // open too tight instead, so the player still has to size it.
                    float cov = head.H / maxH;
                    h = cov >= rulesOrNull.CoverageMin && cov <= rulesOrNull.CoverageMax
                        ? head.H / (rulesOrNull.CoverageMax + OpeningCoverageOffset)
                        : maxH;
                }
            }
            h = Math.Max(MinCropHeight, Math.Min(maxH, h));
            return Box.FromCenter(0.5f, 0.5f, PortraitGeometry.CropWidthFor(h), h);
        }

        /// <summary>
        /// Resize keeping the centre fixed. The height is limited so the frame stays inside the
        /// image (with the insets) around that centre: at an edge it shrinks, it never shifts.
        /// </summary>
        public static Box ResizeAboutCentre(Box crop, float newHeight, float insetX = 0f, float insetY = 0f)
        {
            float cx = crop.CenterX, cy = crop.CenterY;
            float k = PortraitGeometry.CropWidthFor(1f);
            float roomY = 2f * Math.Min(cy - insetY, 1f - insetY - cy);
            float roomX = 2f * Math.Min(cx - insetX, 1f - insetX - cx) / k;
            float h = Math.Min(newHeight, Math.Min(PortraitGeometry.FullCrop().H, Math.Min(roomY, roomX)));
            h = Math.Max(Math.Min(MinCropHeight, crop.H), h);
            return Box.FromCenter(cx, cy, PortraitGeometry.CropWidthFor(h), h);
        }

        /// <summary>
        /// Resize keeping the guide point still: where the dashed TOP OF HEAD line crosses the
        /// NOSE line. So a crown placed on the line stays on it, the nose stays on the centre line,
        /// and sizing only moves the chin against the band. The height asked for is always given
        /// (between MinCropHeight and the full crop); when that frame would leave the image it is
        /// slid back in by the least amount, so growing never dead-ends at an edge.
        /// </summary>
        public static Box ResizeAboutGuide(Box crop, float newHeight)
        {
            float ax = crop.CenterX, ay = GuideY(crop);
            float h = Math.Min(newHeight, PortraitGeometry.FullCrop().H);
            h = Math.Max(Math.Min(MinCropHeight, crop.H), h);
            float w = PortraitGeometry.CropWidthFor(h);
            return new Box(ax - 0.5f * w, ay - GuideTop * h, w, h).ClampInside(new Box(0f, 0f, 1f, 1f));
        }

        /// <summary>
        /// How far the crown may sit off the dashed line and still be called "on the line" by the
        /// readout and the coach (the scorer allows <see cref="CrownTolerance"/>).
        /// </summary>
        public const float OnLineTolerance = 0.025f;

        public static Box Move(Box crop, float dx, float dy) =>
            new Box(crop.X + dx, crop.Y + dy, crop.W, crop.H).ClampInside(new Box(0f, 0f, 1f, 1f));

        // ------------------------------------------------------------------ camera step

        /// <summary>
        /// Rough camera-step gate: head centre within ±0.12 of the frame centre, crown at least 3%
        /// below the top, chin inside. Not enough on its own at high zoom with small-head rules;
        /// use the overload with the rule.
        /// </summary>
        public static bool ShotCroppable(Box head) =>
            Math.Abs(head.CenterX - 0.5f) <= ShotCentreTolerance && head.Y >= ShotCrownMin && head.Bottom <= 1f;

        /// <summary>
        /// The camera-step gate for a known rule: the rough gate AND some crop inside the image
        /// passes every framing check with a little slack (see CanCropToPass). With
        /// <paramref name="sensorHeightPx"/> &gt; 0 that crop must also keep full resolution.
        /// A null rule falls back to the rough gate.
        /// </summary>
        public static bool ShotCroppable(Box head, PhotoRules rulesOrNull, int sensorHeightPx = 0) =>
            ShotCroppable(head) && (rulesOrNull == null || CanCropToPass(head, rulesOrNull, sensorHeightPx));

        /// <summary>Slack kept inside the scorer's windows when asking "can this still pass?".</summary>
        public const float PassSlackTop = 0.01f;
        public const float PassSlackSide = 0.01f;

        /// <summary>
        /// True when some crop inside the image (no taller than the full crop) passes every framing
        /// check with a little slack: top margin 3%..21%, nose within ±4% of the crop width, chin in.
        /// With <paramref name="sensorHeightPx"/> &gt; 0 the crop must also avoid LOW RESOLUTION.
        /// </summary>
        public static bool CanCropToPass(Box head, PhotoRules rules, int sensorHeightPx = 0) =>
            TryPassingCrop(head, rules, sensorHeightPx, out _);

        /// <summary>
        /// A crop that passes every framing check with slack, preferring the middle of the band,
        /// the crown on the dashed line and the nose on the centre line (e.g. for a pencil hint).
        /// </summary>
        public static bool TryPassingCrop(Box head, PhotoRules rules, int sensorHeightPx, out Box crop)
        {
            crop = default;
            if (rules == null || head.H <= 0f) return false;
            float maxH = PortraitGeometry.FullCrop().H;
            float minH = sensorHeightPx > 0 ? LowResPx * 1.002f / sensorHeightPx : 0f;
            float k = PortraitGeometry.CropWidthFor(1f);
            const int Steps = 24;
            for (int n = 0; n <= Steps; n++)
            {
                // Middle of the band first, then alternately outward.
                int i = Steps / 2 + ((n & 1) == 0 ? n / 2 : -(n + 1) / 2);
                float cov = rules.CoverageMin + (rules.CoverageMax - rules.CoverageMin) * (0.02f + 0.96f * i / Steps);
                float h = head.H / cov;
                if (h > maxH || h < minH) continue;
                // Vertical: top margin t with the crop inside 0..1 and the chin not cut.
                float tLo = Math.Max(PhotoEvaluator.TopMarginMin + PassSlackTop, (head.Y - (1f - h)) / h);
                float tHi = Math.Min(PhotoEvaluator.TopMarginMax - PassSlackTop, Math.Min(head.Y / h, 1f - cov));
                if (tLo > tHi) continue;
                // Sideways: crop centre within ±(tol - slack) of the head, crop inside 0..1.
                float w = k * h;
                float d = (PhotoEvaluator.CenterTolerance - PassSlackSide) * w;
                float xLo = Math.Max(head.CenterX - d, w * 0.5f);
                float xHi = Math.Min(head.CenterX + d, 1f - w * 0.5f);
                if (xLo > xHi) continue;
                float t = Math.Max(tLo, Math.Min(tHi, GuideTop));
                float cx = Math.Max(xLo, Math.Min(xHi, head.CenterX));
                crop = new Box(cx - w * 0.5f, head.Y - t * h, w, h);
                return true;
            }
            return false;
        }
    }
}
