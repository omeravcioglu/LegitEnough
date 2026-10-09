using System;

namespace LegitEnough.Sim
{
    /// <summary>Scores a photograph against a country's photo rules (hidden from the player).</summary>
    public static class PhotoEvaluator
    {
        /// <summary>Head top should sit this far below the crop's top edge (fraction of crop height).</summary>
        public const float TopMarginMin = 0.02f;
        public const float TopMarginMax = 0.22f;
        public const float CenterTolerance = 0.05f;

        public static void Assess(PhotoCapture photo, PhotoRules rules, int expectedCustomerId, QualityReport report)
        {
            const ScoreArea A = ScoreArea.Photo;
            if (photo == null)
            {
                report.Add(new Finding(Issue.NoPhoto, A, 100f, 0.1f));
                return;
            }
            if (photo.CustomerId != expectedCustomerId)
                report.Add(new Finding(Issue.WrongPerson, A, 60f, 0.1f, null, "photo shows someone else"));

            if (photo.Backdrop != rules.Backdrop)
                report.Add(new Finding(Issue.WrongBackdrop, A, 15f, 0.68f, null, $"{Labels.Of(photo.Backdrop)} instead of {Labels.Of(rules.Backdrop)}"));
            if (photo.BackdropMoving)
                report.Add(new Finding(Issue.BackdropMoving, A, 20f, 0.85f, null, "backdrop was still rolling"));

            if (photo.EyesClosed)
                report.Add(new Finding(Issue.EyesClosed, A, 20f, 0.6f));
            else if (photo.EyesHalfClosed)
                report.Add(new Finding(Issue.EyesHalfClosed, A, 15f));

            if (Math.Abs(photo.GazeX) > 0.45f || Math.Abs(photo.GazeY) > 0.45f)
                report.Add(new Finding(Issue.LookingAway, A, 8f, 0.8f));

            int yaw = Math.Abs(photo.Pose.Yaw);
            if (yaw == 1) report.Add(new Finding(Issue.HeadTurned, A, 8f));
            else if (yaw >= 2) report.Add(new Finding(Issue.HeadTurned, A, 10f, 0.75f, null, photo.Pose.Name));

            int pitch = Math.Abs(photo.Pose.Pitch);
            if (pitch == 1) report.Add(new Finding(Issue.HeadTilted, A, 7f));
            else if (pitch >= 2) report.Add(new Finding(Issue.HeadTilted, A, 10f, 0.8f, null, photo.Pose.Name));

            if (!rules.ExpressionOk(photo.Expression))
            {
                float m = photo.Expression == Expression.Smile ? 0.8f : 0.72f;
                report.Add(new Finding(Issue.WrongExpression, A, 10f, m, null, Labels.Of(photo.Expression)));
            }

            if (photo.Glasses.Count > 0)
            {
                bool sun = photo.Glasses[photo.Glasses.Count - 1] == GlassesStyle.Sunglasses;
                if (sun) report.Add(new Finding(Issue.SunglassesWorn, A, 25f, 0.55f));
                else if (!rules.GlassesAllowed) report.Add(new Finding(Issue.GlassesWorn, A, 12f, 0.72f));
            }

            if (photo.HatOn && photo.Look.Hat != HatStyle.None && !rules.HeadwearAllowed)
                report.Add(new Finding(Issue.HeadwearWorn, A, 12f, 0.72f));

            AssessFraming(photo, rules, report);

            if (photo.Blurred)
                report.Add(new Finding(Issue.Blurry, A, 22f, 0.9f));

            float cropPx = photo.Crop.H * photo.SensorHeightPx;
            if (cropPx < 450f) report.Add(new Finding(Issue.LowResolution, A, 25f, 1f, null, $"{cropPx:0}px"));
            else if (cropPx < 600f) report.Add(new Finding(Issue.LowResolution, A, 12f, 1f, null, $"{cropPx:0}px"));

            if (photo.Noise > 0f)
            {
                float grain = photo.Noise * 10f * (cropPx < 800f ? 1.5f : 1f);
                report.Add(new Finding(Issue.Grainy, A, grain));
            }

            report.PhotoBonus += photo.LightingBonus;
        }

        public static void AssessFraming(PhotoCapture photo, PhotoRules rules, QualityReport report)
        {
            const ScoreArea A = ScoreArea.Photo;
            Box head = photo.HeadInFrame;
            Box crop = photo.Crop;

            float top = (head.Y - crop.Y) / crop.H;
            float bottom = (crop.Bottom - head.Bottom) / crop.H;
            if (top < -0.01f || bottom < -0.01f)
            {
                report.Add(new Finding(Issue.HeadCutOff, A, 15f, 0.6f, null, top < -0.01f ? "top of head cut" : "chin cut"));
            }

            float coverage = head.H / crop.H;
            if (coverage < rules.CoverageMin)
            {
                float p = Math.Min(35f, (rules.CoverageMin - coverage) * 300f);
                report.Add(new Finding(Issue.HeadTooSmall, A, p, 1f, null, Labels.Percent(coverage)));
            }
            else if (coverage > rules.CoverageMax)
            {
                float p = Math.Min(35f, (coverage - rules.CoverageMax) * 300f);
                report.Add(new Finding(Issue.HeadTooLarge, A, p, 1f, null, Labels.Percent(coverage)));
            }

            float dx = Math.Abs(head.CenterX - crop.CenterX) / crop.W;
            if (dx > CenterTolerance)
                report.Add(new Finding(Issue.OffCenter, A, Math.Min(30f, (dx - CenterTolerance) * 200f), 1f, null, "horizontal"));

            if (top > TopMarginMax)
                report.Add(new Finding(Issue.OffCenter, A, Math.Min(25f, (top - TopMarginMax) * 150f), 1f, null, "head too low"));
            else if (top >= -0.01f && top < TopMarginMin)
                report.Add(new Finding(Issue.OffCenter, A, 5f, 1f, null, "head touches top"));
        }

        /// <summary>Convenience for tools/tests: score a photo on its own.</summary>
        public static QualityReport Score(PhotoCapture photo, PhotoRules rules)
        {
            var r = new QualityReport();
            Assess(photo, rules, photo?.CustomerId ?? -1, r);
            r.Finish();
            return r;
        }
    }
}
