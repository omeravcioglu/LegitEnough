using System.Collections.Generic;
using LegitEnough.Sim;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>Everything about a head that can change from moment to moment.</summary>
    public struct HeadState
    {
        /// <summary>-1..1, positive turns toward screen-right.</summary>
        public float Yaw;
        /// <summary>-1..1, positive lifts the chin.</summary>
        public float Pitch;
        public float GazeX;
        public float GazeY;
        /// <summary>0 open, 1 shut.</summary>
        public float Lid;
        public float MouthOpen;
        public Expression Expression;
        public IReadOnlyList<GlassesStyle> Glasses;
        public bool HatOn;
        /// <summary>Motion blur ghost (photos taken while moving).</summary>
        public bool Blurred;

        public static HeadState Rest(Appearance a, Expression e) => new HeadState
        {
            Expression = e,
            Glasses = a.Glasses,
            HatOn = a.Hat != HatStyle.None,
        };
    }

    public struct BodyPose
    {
        public bool Seated;
        public bool Walking;
        public float WalkPhase;
        public bool Holding;
        public Color HoldingColor;
        /// <summary>Idle breathing: lifts the shoulders and head by this many art pixels.</summary>
        public float Breath;
    }

    /// <summary>
    /// Pixel-art characters (design doc §7 modular customers). Draws any <see cref="Appearance"/>
    /// at any size, in art pixels, with pose, gaze, blink and expression. The geometry is the
    /// same as <see cref="PortraitGeometry"/> uses, so what the player crops is what the
    /// evaluator measures. Below <see cref="TinyHead"/> pixels the face switches to
    /// hand-placed pixel features so a 14px face still has readable eyes and a mouth.
    /// </summary>
    public static class FigurePainter
    {
        static readonly Color Sclera = Ui.Hex("#efe8dc");
        static readonly Color Lash = Ui.Hex("#21160f");
        static readonly Color Pupil = Ui.Hex("#120c09");
        static readonly Color Teeth = Ui.Hex("#f2ecdc");
        static readonly Color MouthDark = Ui.Hex("#3a1612");
        static readonly Color Shoe = Ui.Hex("#1e1916");
        const float K = 0.5522847f;

        /// <summary>Head height (art px) below which faces use hand-placed pixel features.</summary>
        public const float TinyHead = 24f;

        // ================================================================== head

        public static void DrawHead(PixelPainter p, Vector2 c, float h, Appearance a, HeadState s)
        {
            float w = h * a.FaceWidth;
            float yaw = Mathf.Clamp(s.Yaw, -1f, 1f);
            float pitch = Mathf.Clamp(s.Pitch, -1f, 1f);
            var hc = new Vector2(c.x + yaw * 0.03f * w, c.y);
            float fx = c.x + yaw * 0.17f * w;
            float fy = -pitch * 0.07f * h;
            Color skin = Looks.SkinOf(a);
            Color shade = Draw.Shade(skin, 0.86f);
            Color deep = Draw.Shade(skin, 0.68f);
            Color hair = Looks.HairOf(a);
            bool tiny = h < TinyHead;

            HairBack(p, hc, h, w, a, s, hair);
            Ears(p, hc, h, w, yaw, fy, skin, shade, a.Earrings);
            HeadPath(p, hc, h, w, a.Face, yaw);
            p.fillColor = skin;
            p.Fill();
            // Cel shadow on the side away from the light (light comes from the left).
            HeadPath(p, hc + new Vector2(w * 0.36f, h * 0.02f), h, w * 0.46f, a.Face, yaw);
            ClipToHeadAndFill(p, hc, h, w, a.Face, yaw, shade);
            // Jaw shadow under the chin line.
            p.Ellipse(new Vector2(hc.x + w * 0.05f, hc.y + h * 0.47f), w * 0.26f, h * 0.035f, Draw.Alpha(deep, 0.5f));

            if (a.Freckles && !tiny) Freckles(p, fx, hc.y + h * 0.08f + fy, w, h, deep);
            FacialHairUnder(p, hc, fx, fy, h, w, a, skin, hair);

            float eyeY = c.y - 0.02f * h + fy;
            if (tiny)
            {
                TinyFace(p, a, s, fx, eyeY, c.y + fy, w, h, skin, hair);
            }
            else
            {
                Eyes(p, a, s, fx, eyeY, w, h, skin);
                Brows(p, a, s, fx, eyeY, w, h, hair);
                Nose(p, a, yaw, pitch, fx, eyeY, c.y + fy, w, h, shade);
                Mouth(p, a, s, fx + yaw * 0.05f * w, c.y + 0.31f * h + fy * 0.8f, w, h, skin, hair);
                if (a.Age >= 45) Wrinkles(p, fx, eyeY, hc, w, h, deep, a.Age);
            }

            HairFront(p, hc, h, w, a, s, yaw, hair);
            if (s.HatOn && a.Hat != HatStyle.None) Hat(p, hc, fx, h, w, a);
            if (tiny) TinyGlasses(p, s, a, fx, eyeY, w, h, yaw);
            else Glasses(p, s, a, fx, eyeY, w, h, yaw);

            if (s.Blurred)
            {
                // Cheap "motion blur": a translucent ghost of the head shape, offset.
                HeadPath(p, new Vector2(hc.x + w * 0.08f, hc.y + h * 0.02f), h, w, a.Face, yaw);
                p.fillColor = Draw.Alpha(skin, 0.35f);
                p.Fill();
            }
        }

        /// <summary>Fills the current path, but only where it overlaps the head shape.</summary>
        static void ClipToHeadAndFill(PixelPainter p, Vector2 hc, float h, float w, FaceShape shape, float yaw, Color fill)
        {
            // Rasterize the shade shape into a scratch layer, then keep pixels the face covers.
            var target = p.Target;
            var scratch = Scratch(target.W, target.H);
            p.Target = scratch;
            p.fillColor = new Color32(255, 255, 255, 255);
            p.Fill();
            var shadeBounds = p.LastBounds;
            var faceMask = Scratch2(target.W, target.H);
            p.Target = faceMask;
            HeadPath(p, hc, h, w, shape, yaw);
            p.fillColor = new Color32(255, 255, 255, 255);
            p.Fill();
            p.Target = target;
            Color32 f = fill;
            int W = target.W;
            bool big = h >= 40f;
            for (int y = shadeBounds.yMin; y < shadeBounds.yMax; y++)
                for (int x = shadeBounds.xMin; x < shadeBounds.xMax; x++)
                {
                    int i = y * W + x;
                    if (scratch.Px[i].a == 0 || faceMask.Px[i].a == 0) continue;
                    // Dither the lit edge of the shadow on bigger faces so it doesn't read as a cut.
                    bool edge = x > 0 && scratch.Px[i - 1].a == 0 || (big && x > 1 && scratch.Px[i - 2].a == 0);
                    if (edge && ((x + y) & 1) == 0) continue;
                    target.Set(x, y, f);
                }
        }

        static PixelBuffer _scratch, _scratch2;

        /// <summary>Cleared scratch layers the size of the target (capacity is reused).</summary>
        static PixelBuffer Scratch(int w, int h)
        {
            if (_scratch == null) _scratch = new PixelBuffer(w, h);
            else _scratch.Resize(w, h);
            return _scratch;
        }

        static PixelBuffer Scratch2(int w, int h)
        {
            if (_scratch2 == null) _scratch2 = new PixelBuffer(w, h);
            else _scratch2.Resize(w, h);
            return _scratch2;
        }

        static void HeadPath(PixelPainter p, Vector2 hc, float h, float w, FaceShape shape, float yaw)
        {
            float jaw = shape switch
            {
                FaceShape.Round => 0.92f,
                FaceShape.Square => 0.97f,
                FaceShape.Long => 0.74f,
                FaceShape.Heart => 0.6f,
                _ => 0.8f,
            };
            float cheek = shape == FaceShape.Round ? 1.04f : shape == FaceShape.Heart ? 1.03f : 1f;
            float chinW = shape == FaceShape.Square ? 0.2f : shape == FaceShape.Heart ? 0.05f : 0.12f;
            float top = hc.y - h * 0.5f;
            float bot = hc.y + h * 0.5f;
            float chinX = hc.x + yaw * 0.08f * w;
            float hw = w * 0.5f * cheek;

            p.BeginPath();
            p.MoveTo(new Vector2(hc.x, top));
            p.BezierCurveTo(new Vector2(hc.x + hw * 0.62f, top), new Vector2(hc.x + hw, top + h * 0.16f), new Vector2(hc.x + hw, hc.y - h * 0.04f));
            p.BezierCurveTo(new Vector2(hc.x + hw, hc.y + h * 0.16f), new Vector2(hc.x + hw * jaw, hc.y + h * 0.24f), new Vector2(hc.x + hw * jaw * 0.93f, hc.y + h * 0.32f));
            p.BezierCurveTo(new Vector2(hc.x + hw * jaw * 0.78f, hc.y + h * 0.44f), new Vector2(chinX + w * chinW, bot), new Vector2(chinX, bot));
            p.BezierCurveTo(new Vector2(chinX - w * chinW, bot), new Vector2(hc.x - hw * jaw * 0.78f, hc.y + h * 0.44f), new Vector2(hc.x - hw * jaw * 0.93f, hc.y + h * 0.32f));
            p.BezierCurveTo(new Vector2(hc.x - hw * jaw, hc.y + h * 0.24f), new Vector2(hc.x - hw, hc.y + h * 0.16f), new Vector2(hc.x - hw, hc.y - h * 0.04f));
            p.BezierCurveTo(new Vector2(hc.x - hw, top + h * 0.16f), new Vector2(hc.x - hw * 0.62f, top), new Vector2(hc.x, top));
            p.ClosePath();
        }

        static void Ears(PixelPainter p, Vector2 hc, float h, float w, float yaw, float fy, Color skin, Color shade, bool earrings)
        {
            float earY = hc.y + 0.02f * h + fy * 0.4f;
            float earH = 0.2f * h;
            float earW = 0.1f * w;
            for (int side = -1; side <= 1; side += 2)
            {
                float vis = Mathf.Clamp(1f - side * yaw * 1.1f, 0.05f, 1.8f);
                float x = hc.x + side * (w * 0.5f - earW * 0.1f + earW * 0.35f * vis);
                p.Ellipse(new Vector2(x, earY), Mathf.Max(0.8f, earW * 0.5f * vis), earH * 0.5f, side > 0 ? shade : skin);
                if (h >= TinyHead) p.Ellipse(new Vector2(x + side * earW * 0.05f * vis, earY), earW * 0.22f * vis, earH * 0.28f, Draw.Shade(shade, 0.85f));
                if (earrings && vis > 0.4f)
                    p.Ellipse(new Vector2(x, earY + earH * 0.48f), Mathf.Max(0.6f, h * 0.022f), Mathf.Max(0.6f, h * 0.022f), Ui.Hex("#d9b44a"));
            }
        }

        // ------------------------------------------------------------------ tiny faces (shop floor)

        static void TinyFace(PixelPainter p, Appearance a, HeadState s, float fx, float eyeY, float cy, float w, float h, Color skin, Color hair)
        {
            var b = p.Target;
            float yaw = Mathf.Clamp(s.Yaw, -1f, 1f);
            float spacing = Mathf.Max(1.6f, 0.215f * w * (1f - Mathf.Abs(yaw) * 0.12f));
            int ey = Mathf.RoundToInt(eyeY + p.Offset.y);
            int gaze = Mathf.Abs(s.GazeX) > 0.45f ? (s.GazeX > 0 ? 1 : -1) : 0;
            Color32 dark = Pupil;
            Color32 lid = Draw.Shade(skin, 0.55f);
            Color32 white = Sclera;
            Color32 browC = a.HairColor >= 6 ? Draw.Shade(hair, 0.7f) : Draw.Shade(hair, 0.8f);
            bool tall = h >= 17f;

            for (int side = -1; side <= 1; side += 2)
            {
                int ex = Mathf.RoundToInt(fx + p.Offset.x + side * spacing - 0.5f);
                if (s.Lid >= 0.75f) b.HLine(ex - (side < 0 ? 1 : 0), ex + (side > 0 ? 1 : 0), ey + 1, lid);
                else if (s.Lid >= 0.4f) b.Set(ex + gaze, ey + 1, dark);
                else
                {
                    if (tall) b.Set(ex - gaze + (gaze == 0 ? -side : 0), ey, white);
                    b.Set(ex + (gaze > 0 && side < 0 ? 0 : gaze), ey, dark);
                    if (tall) b.Set(ex + (gaze > 0 && side < 0 ? 0 : gaze), ey + 1, dark);
                }
                // Brows lean with the mood.
                int inner = s.Expression == Expression.Angry ? 1 : s.Expression == Expression.Nervous || s.Expression == Expression.Surprised ? -1 : 0;
                int by = ey - Mathf.Max(2, Mathf.RoundToInt(h * 0.12f));
                b.Set(ex - side, by + (side > 0 ? 0 : inner), browC);
                b.Set(ex, by + (side > 0 ? inner : 0), browC);
                if (side > 0 && tall) b.Set(ex + 1, by, browC);
                if (side < 0 && tall) b.Set(ex - 2, by, browC);
            }

            // Nose: one shade pixel on the shadow side.
            b.Set(Mathf.RoundToInt(fx + p.Offset.x + 0.5f + yaw), ey + Mathf.Max(2, Mathf.RoundToInt(h * 0.16f)), Draw.Shade(skin, 0.7f));

            // Mouth.
            int my = Mathf.RoundToInt(cy + 0.31f * h + p.Offset.y);
            int mx = Mathf.RoundToInt(fx + p.Offset.x + yaw * 0.5f);
            int half = Mathf.Max(1, Mathf.RoundToInt(0.16f * w * a.MouthWidth));
            Color32 lip = Color.Lerp(Draw.Shade(skin, 0.62f), Ui.Hex("#7a2e2a"), 0.45f);
            switch (s.Expression)
            {
                case Expression.Smile:
                    b.HLine(mx - half + 1, mx + half - 1, my, lip);
                    b.Set(mx - half, my - 1, lip);
                    b.Set(mx + half, my - 1, lip);
                    break;
                case Expression.Grin:
                    b.HLine(mx - half, mx + half, my, Teeth);
                    b.HLine(mx - half, mx + half, my - 1, lip);
                    break;
                case Expression.Angry:
                    b.HLine(mx - half + 1, mx + half - 1, my, lip);
                    b.Set(mx - half, my + 1, lip);
                    b.Set(mx + half, my + 1, lip);
                    break;
                case Expression.Surprised:
                    b.Rect(mx - 1, my - 1, 2, 2, MouthDark);
                    break;
                default:
                    b.HLine(mx - half, mx + half, my, lip);
                    break;
            }
            if (s.MouthOpen > 0.35f && s.Expression != Expression.Surprised) b.Set(mx, my + 1, MouthDark);
            if (a.FacialHair == FacialHair.Mustache || a.FacialHair == FacialHair.Beard)
                b.HLine(mx - half - 1, mx + half + 1, my - 1, hair);
        }

        static void TinyGlasses(PixelPainter p, HeadState s, Appearance a, float fx, float eyeY, float w, float h, float yaw)
        {
            if (s.Glasses == null || s.Glasses.Count == 0) return;
            var b = p.Target;
            float spacing = Mathf.Max(1.6f, 0.215f * w * (1f - Mathf.Abs(yaw) * 0.12f));
            int ey = Mathf.RoundToInt(eyeY + p.Offset.y);
            foreach (var g in s.Glasses)
            {
                if (g == GlassesStyle.None) continue;
                Color32 frame = g == GlassesStyle.Round ? Ui.Hex("#8a6b3a") : Ui.Hex("#1a1412");
                int exL = Mathf.RoundToInt(fx + p.Offset.x - spacing - 0.5f);
                int exR = Mathf.RoundToInt(fx + p.Offset.x + spacing - 0.5f);
                if (g == GlassesStyle.Sunglasses)
                {
                    b.Rect(exL - 1, ey, 3, 2, Ui.Hex("#101014"));
                    b.Rect(exR - 1, ey, 3, 2, Ui.Hex("#101014"));
                    b.HLine(exL + 2, exR - 2, ey, Ui.Hex("#101014"));
                    b.Set(exL - 1, ey, Ui.Hex("#6a7a8a"));
                }
                else
                {
                    // Small faces: just the top of the frames, the bridge and the outer corners.
                    b.HLine(exL - 1, exR + 1, ey - 1, frame);
                    b.Set(exL - 1, ey, frame);
                    b.Set(exR + 1, ey, frame);
                    if (h >= 18f)
                    {
                        b.Set(exL + 1, ey + 1, frame);
                        b.Set(exR - 1, ey + 1, frame);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ full-size features

        static void Eyes(PixelPainter p, Appearance a, HeadState s, float fx, float eyeY, float w, float h, Color skin)
        {
            float yaw = Mathf.Clamp(s.Yaw, -1f, 1f);
            float spacing = 0.215f * w * (1f - Mathf.Abs(yaw) * 0.12f);
            float ew = Mathf.Max(3.2f, 0.17f * w * a.EyeSize);
            float eh = Mathf.Max(2.2f, 0.078f * h * a.EyeSize);
            float squint = s.Expression switch
            {
                Expression.Smile => 0.22f,
                Expression.Grin => 0.38f,
                Expression.Angry => 0.18f,
                _ => 0f,
            };
            bool wide = s.Expression == Expression.Surprised || s.Expression == Expression.Nervous;

            for (int side = -1; side <= 1; side += 2)
            {
                float far = (side == 1 && yaw > 0f) || (side == -1 && yaw < 0f) ? Mathf.Abs(yaw) : 0f;
                float rx = ew * 0.5f * (1f - 0.35f * far);
                float ry = eh * 0.5f * (wide ? 1.18f : 1f);
                var ec = new Vector2(fx + side * spacing * (1f - 0.1f * far), eyeY);

                p.Ellipse(ec, rx, ry, Sclera);
                float ir = Mathf.Max(0.9f, Mathf.Min(ry * 0.92f, rx * 0.55f));
                var ic = new Vector2(ec.x + Mathf.Clamp(s.GazeX, -1f, 1f) * rx * 0.45f, ec.y - Mathf.Clamp(s.GazeY, -1f, 1f) * ry * 0.35f);
                p.Ellipse(ic, ir, ir, Looks.Iris(a.Eyes));
                if (ir >= 1.4f) p.Ellipse(ic, ir * 0.5f, ir * 0.5f, Pupil);
                if (ir >= 2.2f) p.Ellipse(ic + new Vector2(-ir * 0.38f, -ir * 0.38f), ir * 0.24f, ir * 0.24f, Color.white);

                // Skin mask above and below so the iris never pokes out of the eye shape.
                EyeMask(p, ec, rx, ry, -1f, skin);
                EyeMask(p, ec, rx, ry, 1f, skin);

                // Upper lid: blink / droop. Lid edge is a quadratic whose apex slides from the top to the bottom.
                float lid = Mathf.Clamp01(s.Lid);
                var left = new Vector2(ec.x - rx, ec.y);
                var right = new Vector2(ec.x + rx, ec.y);
                var lidControl = new Vector2(ec.x, ec.y - 2f * ry + 4f * lid * ry);
                p.BeginPath();
                p.MoveTo(left);
                p.BezierCurveTo(new Vector2(ec.x - rx, ec.y - ry * K), new Vector2(ec.x - rx * K, ec.y - ry), new Vector2(ec.x, ec.y - ry));
                p.BezierCurveTo(new Vector2(ec.x + rx * K, ec.y - ry), new Vector2(ec.x + rx, ec.y - ry * K), right);
                p.QuadraticCurveTo(lidControl, left);
                p.ClosePath();
                p.fillColor = Draw.Shade(skin, 0.94f);
                p.Fill();

                // Lower lid: smiles push the cheeks up.
                if (squint > 0f)
                {
                    var lowControl = new Vector2(ec.x, ec.y + 2f * ry - 2f * squint * ry * 2f);
                    p.BeginPath();
                    p.MoveTo(left);
                    p.BezierCurveTo(new Vector2(ec.x - rx, ec.y + ry * K), new Vector2(ec.x - rx * K, ec.y + ry), new Vector2(ec.x, ec.y + ry));
                    p.BezierCurveTo(new Vector2(ec.x + rx * K, ec.y + ry), new Vector2(ec.x + rx, ec.y + ry * K), right);
                    p.QuadraticCurveTo(lowControl, left);
                    p.ClosePath();
                    p.fillColor = skin;
                    p.Fill();
                }

                // Lash line along the lid edge.
                p.Curve(left, lidControl, right, Lash, Mathf.Max(1.1f, h * 0.02f));
            }
        }

        static void EyeMask(PixelPainter p, Vector2 ec, float rx, float ry, float dir, Color skin)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(ec.x - rx * 1.3f, ec.y));
            p.LineTo(new Vector2(ec.x - rx, ec.y));
            p.BezierCurveTo(new Vector2(ec.x - rx, ec.y + dir * ry * K), new Vector2(ec.x - rx * K, ec.y + dir * ry), new Vector2(ec.x, ec.y + dir * ry));
            p.BezierCurveTo(new Vector2(ec.x + rx * K, ec.y + dir * ry), new Vector2(ec.x + rx, ec.y + dir * ry * K), new Vector2(ec.x + rx, ec.y));
            p.LineTo(new Vector2(ec.x + rx * 1.3f, ec.y));
            p.LineTo(new Vector2(ec.x + rx * 1.3f, ec.y + dir * ry * 2.4f));
            p.LineTo(new Vector2(ec.x - rx * 1.3f, ec.y + dir * ry * 2.4f));
            p.ClosePath();
            p.fillColor = skin;
            p.Fill();
        }

        static void Brows(PixelPainter p, Appearance a, HeadState s, float fx, float eyeY, float w, float h, Color hair)
        {
            float yaw = Mathf.Clamp(s.Yaw, -1f, 1f);
            float spacing = 0.215f * w * (1f - Mathf.Abs(yaw) * 0.12f);
            float bw = Mathf.Max(2.2f, 0.105f * w * a.EyeSize);
            float by = eyeY - 0.125f * h;
            float inner = 0f, outer = 0.008f * h;
            switch (s.Expression)
            {
                case Expression.Angry: inner = 0.035f * h; outer = -0.012f * h; break;
                case Expression.Nervous: inner = -0.03f * h; outer = 0.01f * h; break;
                case Expression.Surprised: inner = -0.045f * h; outer = -0.035f * h; break;
                case Expression.Tired: inner = -0.01f * h; outer = 0.022f * h; break;
                case Expression.Smile:
                case Expression.Grin: inner = -0.01f * h; outer = -0.006f * h; break;
            }
            Color brow = a.HairColor >= 6 ? Draw.Shade(hair, 0.72f) : Draw.Shade(hair, 0.82f);
            float thick = Mathf.Max(1.2f, 0.03f * h * a.BrowThickness);
            for (int side = -1; side <= 1; side += 2)
            {
                float far = (side == 1 && yaw > 0f) || (side == -1 && yaw < 0f) ? Mathf.Abs(yaw) : 0f;
                float cx = fx + side * spacing * (1f - 0.1f * far);
                float half = bw * (1f - 0.3f * far);
                var pin = new Vector2(cx - side * half, by + inner);
                var pout = new Vector2(cx + side * half, by + outer);
                p.Curve(pin, new Vector2(cx, by - 0.012f * h + (inner + outer) * 0.3f), pout, brow, thick);
            }
        }

        static void Nose(PixelPainter p, Appearance a, float yaw, float pitch, float fx, float eyeY, float cy, float w, float h, Color shade)
        {
            float ns = a.NoseSize;
            var top = new Vector2(fx + yaw * 0.03f * w, eyeY + 0.05f * h);
            var tip = new Vector2(fx + yaw * 0.12f * w, cy + 0.17f * h + 0.03f * h * ns);
            Color line = Draw.Shade(shade, 0.84f);
            // Shadow along the right side of the bridge, a lit tip, two nostrils.
            p.Curve(top + new Vector2(0.03f * w, 0f), new Vector2(tip.x + 0.07f * w * ns, tip.y - 0.06f * h), new Vector2(tip.x + 0.035f * w * ns, tip.y + 0.005f * h), line, Mathf.Max(1f, 0.016f * h));
            float nr = Mathf.Max(0.7f, 0.022f * w * ns * (1f + Mathf.Max(0f, pitch) * 0.6f));
            p.Ellipse(new Vector2(tip.x - 0.045f * w * ns, tip.y + 0.01f * h), nr, nr * 0.7f, Draw.Shade(line, 0.8f));
            p.Ellipse(new Vector2(tip.x + 0.045f * w * ns, tip.y + 0.01f * h), nr, nr * 0.7f, Draw.Shade(line, 0.8f));
            if (h > 50f) p.Ellipse(new Vector2(tip.x - 0.02f * w, tip.y - 0.03f * h), nr * 0.7f, nr * 0.5f, Draw.Mix(shade, Color.white, 0.5f));
        }

        static void Mouth(PixelPainter p, Appearance a, HeadState s, float mx, float my, float w, float h, Color skin, Color hair)
        {
            float mw = Mathf.Max(4f, 0.36f * w * a.MouthWidth * (1f - Mathf.Abs(s.Yaw) * 0.15f));
            Color lip = Color.Lerp(Draw.Shade(skin, 0.66f), Ui.Hex("#8a3430"), 0.4f);
            float lw = Mathf.Max(1.1f, 0.022f * h);
            var l = new Vector2(mx - mw * 0.5f, my);
            var r = new Vector2(mx + mw * 0.5f, my);
            float open = Mathf.Clamp01(s.MouthOpen);

            switch (s.Expression)
            {
                case Expression.Smile:
                    p.Curve(l + new Vector2(0f, -0.025f * h), new Vector2(mx, my + 0.07f * h), r + new Vector2(0f, -0.025f * h), lip, lw);
                    break;
                case Expression.Grin:
                {
                    var lu = l + new Vector2(-0.02f * w, -0.03f * h);
                    var ru = r + new Vector2(0.02f * w, -0.03f * h);
                    p.BeginPath();
                    p.MoveTo(lu);
                    p.QuadraticCurveTo(new Vector2(mx, my + 0.02f * h), ru);
                    p.QuadraticCurveTo(new Vector2(mx, my + 0.16f * h), lu);
                    p.ClosePath();
                    p.fillColor = MouthDark;
                    p.Fill();
                    p.BeginPath();
                    p.MoveTo(lu);
                    p.QuadraticCurveTo(new Vector2(mx, my + 0.02f * h), ru);
                    p.QuadraticCurveTo(new Vector2(mx, my + 0.07f * h), lu);
                    p.ClosePath();
                    p.fillColor = Teeth;
                    p.Fill();
                    break;
                }
                case Expression.Angry:
                    p.Curve(l + new Vector2(0f, 0.018f * h), new Vector2(mx, my - 0.035f * h), r + new Vector2(0f, 0.018f * h), lip, lw);
                    break;
                case Expression.Nervous:
                {
                    var m1 = new Vector2(mx - mw * 0.17f, my);
                    var m2 = new Vector2(mx + mw * 0.17f, my);
                    p.Curve(l + new Vector2(mw * 0.12f, 0f), new Vector2((l.x + m1.x) * 0.5f + mw * 0.06f, my - 0.02f * h), m1, lip, lw);
                    p.Curve(m1, new Vector2(mx, my + 0.02f * h), m2, lip, lw);
                    p.Curve(m2, new Vector2((m2.x + r.x) * 0.5f - mw * 0.06f, my - 0.02f * h), r - new Vector2(mw * 0.12f, 0f), lip, lw);
                    break;
                }
                case Expression.Surprised:
                    p.Ellipse(new Vector2(mx, my + 0.02f * h), Mathf.Max(1f, mw * 0.18f), Mathf.Max(1f, 0.05f * h), MouthDark);
                    break;
                case Expression.Tired:
                    p.Curve(l + new Vector2(mw * 0.05f, 0.008f * h), new Vector2(mx, my + 0.012f * h), r + new Vector2(-mw * 0.05f, 0.008f * h), lip, lw);
                    break;
                default:
                    p.Curve(l, new Vector2(mx, my + 0.012f * h), r, lip, lw);
                    break;
            }

            if (open > 0.05f && s.Expression != Expression.Grin && s.Expression != Expression.Surprised)
                p.Ellipse(new Vector2(mx, my + 0.015f * h), Mathf.Max(1f, mw * 0.22f), Mathf.Max(0.8f, 0.04f * h * open), MouthDark);

            // Lower lip.
            p.Curve(new Vector2(mx - mw * 0.2f, my + 0.045f * h), new Vector2(mx, my + 0.06f * h), new Vector2(mx + mw * 0.2f, my + 0.045f * h),
                Draw.Shade(skin, 0.86f), Mathf.Max(1f, 0.015f * h));

            if (a.FacialHair == FacialHair.Mustache || a.FacialHair == FacialHair.Beard)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(mx - mw * 0.62f, my + 0.005f * h));
                p.QuadraticCurveTo(new Vector2(mx, my - 0.1f * h), new Vector2(mx + mw * 0.62f, my + 0.005f * h));
                p.QuadraticCurveTo(new Vector2(mx, my - 0.035f * h), new Vector2(mx - mw * 0.62f, my + 0.005f * h));
                p.ClosePath();
                p.fillColor = hair;
                p.Fill();
            }
        }

        static void FacialHairUnder(PixelPainter p, Vector2 hc, float fx, float fy, float h, float w, Appearance a, Color skin, Color hair)
        {
            if (a.FacialHair == FacialHair.None || a.FacialHair == FacialHair.Mustache) return;
            float top = hc.y + 0.12f * h + fy * 0.5f;
            float bot = hc.y + 0.5f * h;
            float hw = w * 0.47f;
            if (a.FacialHair == FacialHair.Goatee)
            {
                p.Ellipse(new Vector2(fx, hc.y + 0.43f * h), w * 0.13f, h * 0.08f, hair);
                return;
            }
            float extra = a.FacialHair == FacialHair.Beard ? 0.06f * h : 0f;
            p.BeginPath();
            p.MoveTo(new Vector2(hc.x - hw, top));
            p.BezierCurveTo(new Vector2(hc.x - hw, top + 0.2f * h), new Vector2(fx - w * 0.25f, bot + extra), new Vector2(fx, bot + extra));
            p.BezierCurveTo(new Vector2(fx + w * 0.25f, bot + extra), new Vector2(hc.x + hw, top + 0.2f * h), new Vector2(hc.x + hw, top));
            p.QuadraticCurveTo(new Vector2(hc.x + hw * 0.6f, top + 0.12f * h), new Vector2(fx + w * 0.2f, top + 0.15f * h));
            p.QuadraticCurveTo(new Vector2(fx, top + 0.1f * h), new Vector2(fx - w * 0.2f, top + 0.15f * h));
            p.QuadraticCurveTo(new Vector2(hc.x - hw * 0.6f, top + 0.12f * h), new Vector2(hc.x - hw, top));
            p.ClosePath();
            if (a.FacialHair == FacialHair.Stubble)
            {
                // Stubble: a dither of darker skin over the jaw.
                var target = p.Target;
                var scratch = Scratch(target.W, target.H);
                p.Target = scratch;
                p.fillColor = new Color32(255, 255, 255, 255);
                p.Fill();
                var r = p.LastBounds;
                p.Target = target;
                Color32 st = Color.Lerp(Draw.Shade(skin, 0.8f), hair, 0.3f);
                for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++)
                    {
                        int i = y * target.W + x;
                        if (scratch.Px[i].a > 0 && ((x + y) & 1) == 0 && target.Px[i].a > 0) target.Set(x, y, st);
                    }
                return;
            }
            p.fillColor = hair;
            p.Fill();
        }

        static void Freckles(PixelPainter p, float fx, float y, float w, float h, Color shade)
        {
            var b = p.Target;
            Color32 c = Draw.Shade(shade, 0.92f);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Set(Mathf.RoundToInt(fx + p.Offset.x + side * w * 0.22f), Mathf.RoundToInt(y + p.Offset.y), c);
                b.Set(Mathf.RoundToInt(fx + p.Offset.x + side * w * 0.28f), Mathf.RoundToInt(y + p.Offset.y + h * 0.03f), c);
                b.Set(Mathf.RoundToInt(fx + p.Offset.x + side * w * 0.18f), Mathf.RoundToInt(y + p.Offset.y + h * 0.04f), c);
            }
        }

        static void Wrinkles(PixelPainter p, float fx, float eyeY, Vector2 hc, float w, float h, Color deep, int age)
        {
            var c = Draw.Alpha(deep, Mathf.Clamp01((age - 40) / 30f) * 0.7f);
            float lw = 1f;
            float fyTop = hc.y - h * 0.28f;
            p.Curve(new Vector2(fx - w * 0.2f, fyTop), new Vector2(fx, fyTop - h * 0.015f), new Vector2(fx + w * 0.2f, fyTop), c, lw);
            if (h > 40f) p.Curve(new Vector2(fx - w * 0.15f, fyTop + h * 0.04f), new Vector2(fx, fyTop + h * 0.03f), new Vector2(fx + w * 0.15f, fyTop + h * 0.04f), c, lw);
            for (int side = -1; side <= 1; side += 2)
            {
                p.Curve(new Vector2(fx + side * w * 0.15f, hc.y + h * 0.2f), new Vector2(fx + side * w * 0.2f, hc.y + h * 0.28f),
                    new Vector2(fx + side * w * 0.17f, hc.y + h * 0.34f), c, lw);
            }
        }

        // ================================================================== hair, hats, glasses

        static void HairBack(PixelPainter p, Vector2 hc, float h, float w, Appearance a, HeadState s, Color hair)
        {
            float top = hc.y - h * 0.5f;
            switch (a.Hair)
            {
                case HairStyle.Afro:
                {
                    float vol = PortraitGeometry.TopVolume(a, false) * h * (s.HatOn && a.Hat != HatStyle.None ? 0.4f : 1f);
                    float ry = 0.62f * h;
                    p.Ellipse(new Vector2(hc.x, top - vol + ry), 0.78f * h, ry, hair);
                    break;
                }
                case HairStyle.Long:
                {
                    // Behind the skull down to the jaw, then two curtains past the shoulders. The gap
                    // between them keeps the neck and collar visible (one slab read as a beard).
                    Color back = Draw.Shade(hair, 0.82f);
                    p.RoundRect(new Rect(hc.x - w * 0.66f, top - 0.04f * h, w * 1.32f, h * 0.98f), w * 0.5f, back);
                    p.RoundRect(new Rect(hc.x - w * 0.66f, hc.y + 0.1f * h, w * 0.42f, h * 0.78f), w * 0.15f, back);
                    p.RoundRect(new Rect(hc.x + w * 0.24f, hc.y + 0.1f * h, w * 0.42f, h * 0.78f), w * 0.15f, back);
                    break;
                }
                case HairStyle.Ponytail:
                    p.Ellipse(new Vector2(hc.x + w * 0.56f, hc.y + 0.18f * h), w * 0.12f, h * 0.3f, Draw.Shade(hair, 0.82f));
                    break;
                case HairStyle.Curly:
                    p.Ellipse(new Vector2(hc.x, hc.y - 0.05f * h), w * 0.62f, h * 0.55f, Draw.Shade(hair, 0.86f));
                    break;
            }
        }

        static void HairFront(PixelPainter p, Vector2 hc, float h, float w, Appearance a, HeadState s, float yaw, Color hair)
        {
            float top = hc.y - h * 0.5f;
            float vol = PortraitGeometry.TopVolume(a, false) * h * (s.HatOn && a.Hat != HatStyle.None ? 0.4f : 1f);
            float partX = hc.x + yaw * 0.15f * w;
            Color hi = Color.Lerp(hair, Color.white, 0.2f);
            Color lo = Draw.Shade(hair, 0.78f);
            float lw = Mathf.Max(1f, h * 0.02f);
            switch (a.Hair)
            {
                case HairStyle.Bald:
                    if (a.Age > 40)
                    {
                        p.Ellipse(new Vector2(hc.x - w * 0.47f, hc.y - 0.08f * h), w * 0.06f, h * 0.1f, hair);
                        p.Ellipse(new Vector2(hc.x + w * 0.47f, hc.y - 0.08f * h), w * 0.06f, h * 0.1f, lo);
                    }
                    p.Ellipse(new Vector2(hc.x - w * 0.14f, top + h * 0.1f), w * 0.1f, h * 0.035f, Draw.Mix(Looks.SkinOf(a), Color.white, 0.3f));
                    return;
                case HairStyle.Buzz:
                    Cap(p, hc, h, w, vol, top + 0.2f * h, hc.y - 0.08f * h, partX, w * 0.01f);
                    p.fillColor = Draw.Mix(hair, Looks.SkinOf(a), 0.35f);
                    p.Fill();
                    return;
                case HairStyle.Receding:
                    Cap(p, hc, h, w, vol, top + 0.28f * h, hc.y - 0.06f * h, partX, w * 0.02f);
                    p.fillColor = hair;
                    p.Fill();
                    CapShade(p, hc, h, w, vol, hair, lo);
                    return;
                case HairStyle.SidePart:
                    Cap(p, hc, h, w, vol, top + 0.17f * h, hc.y - 0.04f * h, hc.x - w * 0.18f + yaw * 0.1f * w, w * 0.04f);
                    p.fillColor = hair;
                    p.Fill();
                    p.BeginPath();
                    p.MoveTo(new Vector2(hc.x - w * 0.2f, top - vol * 0.6f));
                    p.QuadraticCurveTo(new Vector2(hc.x + w * 0.3f, top + 0.05f * h), new Vector2(hc.x + w * 0.5f, top + 0.24f * h));
                    p.QuadraticCurveTo(new Vector2(hc.x + w * 0.12f, top + 0.16f * h), new Vector2(hc.x - w * 0.2f, top + 0.12f * h));
                    p.LineTo(new Vector2(hc.x - w * 0.2f, top - vol * 0.6f));
                    p.ClosePath();
                    p.fillColor = hair;
                    p.Fill();
                    CapShade(p, hc, h, w, vol, hair, lo);
                    p.Curve(new Vector2(hc.x - w * 0.18f, top - vol * 0.5f), new Vector2(hc.x + w * 0.1f, top), new Vector2(hc.x + w * 0.3f, top + 0.08f * h), hi, lw);
                    return;
                case HairStyle.Curly:
                    Cap(p, hc, h, w, vol * 0.6f, top + 0.18f * h, hc.y - 0.02f * h, partX, w * 0.06f);
                    p.fillColor = hair;
                    p.Fill();
                    for (int i = 0; i < 9; i++)
                    {
                        float t = i / 8f;
                        float ang = Mathf.Lerp(200f, 340f, t) * Mathf.Deg2Rad;
                        var pos = new Vector2(hc.x + Mathf.Cos(ang) * w * 0.52f, hc.y - 0.05f * h + Mathf.Sin(ang) * (h * 0.5f + vol * 0.55f));
                        p.Ellipse(pos, h * 0.08f, h * 0.08f, i % 2 == 0 ? hair : lo);
                        if (h >= TinyHead) p.Ellipse(pos + new Vector2(-h * 0.02f, -h * 0.02f), h * 0.025f, h * 0.025f, hi);
                    }
                    return;
                case HairStyle.Afro:
                    Cap(p, hc, h, w, vol * 0.4f, top + 0.2f * h, hc.y - 0.02f * h, partX, w * 0.05f);
                    p.fillColor = hair;
                    p.Fill();
                    return;
                case HairStyle.Long:
                    Cap(p, hc, h, w, vol, top + 0.16f * h, hc.y + 0.05f * h, partX, w * 0.05f);
                    p.fillColor = hair;
                    p.Fill();
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x0 = hc.x + side * w * 0.5f;
                        p.BeginPath();
                        p.MoveTo(new Vector2(x0 + side * w * 0.06f, hc.y - 0.15f * h));
                        p.QuadraticCurveTo(new Vector2(x0 + side * w * 0.12f, hc.y + 0.4f * h), new Vector2(x0 + side * w * 0.08f, hc.y + 0.8f * h));
                        p.LineTo(new Vector2(x0 - side * w * 0.08f, hc.y + 0.7f * h));
                        p.QuadraticCurveTo(new Vector2(x0 - side * w * 0.06f, hc.y + 0.2f * h), new Vector2(x0 - side * w * 0.04f, hc.y - 0.1f * h));
                        p.LineTo(new Vector2(x0 + side * w * 0.06f, hc.y - 0.15f * h));
                        p.ClosePath();
                        p.fillColor = side > 0 ? lo : hair;
                        p.Fill();
                    }
                    p.Line(new Vector2(partX, top - vol), new Vector2(partX, top + 0.1f * h), lo, 1f);
                    return;
                case HairStyle.Ponytail:
                    Cap(p, hc, h, w, vol, top + 0.16f * h, hc.y - 0.06f * h, partX, w * 0.02f);
                    p.fillColor = hair;
                    p.Fill();
                    CapShade(p, hc, h, w, vol, hair, lo);
                    return;
                case HairStyle.Bun:
                {
                    Cap(p, hc, h, w, 0.05f * h, top + 0.17f * h, hc.y - 0.06f * h, partX, w * 0.02f);
                    p.fillColor = hair;
                    p.Fill();
                    float r = 0.11f * h;
                    p.Ellipse(new Vector2(hc.x, top - vol + r), r * 1.1f, r, hair);
                    if (h >= TinyHead) p.Curve(new Vector2(hc.x - r * 0.6f, top - vol + r * 0.7f), new Vector2(hc.x, top - vol + r * 0.3f), new Vector2(hc.x + r * 0.6f, top - vol + r * 0.7f), hi, lw);
                    return;
                }
                default: // Short
                    Cap(p, hc, h, w, vol, top + 0.2f * h, hc.y - 0.05f * h, partX, w * 0.03f);
                    p.fillColor = hair;
                    p.Fill();
                    CapShade(p, hc, h, w, vol, hair, lo);
                    if (h >= TinyHead) p.Curve(new Vector2(hc.x - w * 0.25f, top - vol * 0.3f), new Vector2(hc.x - w * 0.05f, top - vol * 0.8f), new Vector2(hc.x + w * 0.15f, top - vol * 0.45f), hi, lw);
                    return;
            }
        }

        /// <summary>Darkens the right edge of a hair cap (the shadow side), only where the hair is.</summary>
        static void CapShade(PixelPainter p, Vector2 hc, float h, float w, float vol, Color hair, Color lo)
        {
            float top = hc.y - h * 0.5f - vol;
            p.BeginPath();
            p.MoveTo(new Vector2(hc.x + w * 0.3f, top + h * 0.06f));
            p.QuadraticCurveTo(new Vector2(hc.x + w * 0.56f, top + h * 0.12f), new Vector2(hc.x + w * 0.54f, hc.y - h * 0.08f));
            p.LineTo(new Vector2(hc.x + w * 0.44f, hc.y - h * 0.08f));
            p.QuadraticCurveTo(new Vector2(hc.x + w * 0.46f, top + h * 0.2f), new Vector2(hc.x + w * 0.3f, top + h * 0.06f));
            p.ClosePath();
            var target = p.Target;
            var scratch = Scratch(target.W, target.H);
            p.Target = scratch;
            p.fillColor = new Color32(255, 255, 255, 255);
            p.Fill();
            var r = p.LastBounds;
            p.Target = target;
            Color32 hairC = hair, shadeC = lo;
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    int i = y * target.W + x;
                    var c = target.Px[i];
                    if (scratch.Px[i].a > 0 && c.r == hairC.r && c.g == hairC.g && c.b == hairC.b && c.a == 255) target.Px[i] = shadeC;
                }
        }

        /// <summary>A hair cap from the crown down to the hairline and temples.</summary>
        static void Cap(PixelPainter p, Vector2 hc, float h, float w, float vol, float hairlineY, float sideBottomY, float partX, float widen)
        {
            float top = hc.y - h * 0.5f - vol;
            float hw = w * 0.5f + widen;
            p.BeginPath();
            p.MoveTo(new Vector2(hc.x - hw, sideBottomY));
            p.BezierCurveTo(new Vector2(hc.x - hw - widen * 0.2f, hc.y - h * 0.34f), new Vector2(hc.x - hw * 0.72f, top), new Vector2(hc.x, top));
            p.BezierCurveTo(new Vector2(hc.x + hw * 0.72f, top), new Vector2(hc.x + hw + widen * 0.2f, hc.y - h * 0.34f), new Vector2(hc.x + hw, sideBottomY));
            p.LineTo(new Vector2(hc.x + w * 0.43f, sideBottomY));
            p.BezierCurveTo(new Vector2(hc.x + w * 0.43f, hairlineY), new Vector2(partX + w * 0.16f, hairlineY - h * 0.02f), new Vector2(partX, hairlineY));
            p.BezierCurveTo(new Vector2(partX - w * 0.16f, hairlineY - h * 0.02f), new Vector2(hc.x - w * 0.43f, hairlineY), new Vector2(hc.x - w * 0.43f, sideBottomY));
            p.ClosePath();
        }

        static void Hat(PixelPainter p, Vector2 hc, float fx, float h, float w, Appearance a)
        {
            float top = hc.y - h * 0.5f;
            Color c = Looks.ClothOf(a.TopColor + 3);
            Color dark = Draw.Shade(c, 0.7f);
            Color light = Color.Lerp(c, Color.white, 0.15f);
            switch (a.Hat)
            {
                case HatStyle.Beanie:
                {
                    float vol = 0.1f * h;
                    var r = new Rect(hc.x - w * 0.56f, top - vol, w * 1.12f, h * 0.42f + vol);
                    p.RoundRect(r, w * 0.5f, c);
                    p.Rect(new Rect(hc.x + w * 0.2f, top - vol * 0.5f, w * 0.36f, h * 0.3f), Draw.Alpha(dark, 0.6f));
                    p.Rect(new Rect(hc.x - w * 0.56f, top + h * 0.26f, w * 1.12f, h * 0.1f), dark);
                    if (h >= TinyHead)
                        for (float x = hc.x - w * 0.5f; x < hc.x + w * 0.5f; x += Mathf.Max(2f, w * 0.12f))
                            p.Line(new Vector2(x, top + h * 0.27f), new Vector2(x, top + h * 0.35f), Draw.Shade(dark, 0.85f), 1f);
                    break;
                }
                case HatStyle.Cap:
                {
                    float vol = 0.08f * h;
                    p.RoundRect(new Rect(hc.x - w * 0.54f, top - vol, w * 1.08f, h * 0.36f + vol), w * 0.5f, c);
                    p.Ellipse(new Vector2(hc.x - w * 0.15f, top + h * 0.02f), w * 0.18f, h * 0.05f, light);
                    p.Ellipse(new Vector2(fx, top + h * 0.28f), w * 0.5f, h * 0.06f, dark);
                    break;
                }
                case HatStyle.Fedora:
                {
                    float vol = 0.2f * h;
                    p.Ellipse(new Vector2(hc.x, top + h * 0.17f), w * 0.85f, h * 0.075f, dark);
                    p.Poly(c, new Vector2(hc.x - w * 0.42f, top + h * 0.17f), new Vector2(hc.x - w * 0.36f, top - vol),
                        new Vector2(hc.x + w * 0.36f, top - vol), new Vector2(hc.x + w * 0.42f, top + h * 0.17f));
                    p.Rect(new Rect(hc.x - w * 0.41f, top + h * 0.06f, w * 0.82f, h * 0.07f), Ui.Hex("#2a1d14"));
                    break;
                }
            }
        }

        static void Glasses(PixelPainter p, HeadState s, Appearance a, float fx, float eyeY, float w, float h, float yaw)
        {
            if (s.Glasses == null || s.Glasses.Count == 0) return;
            float spacing = 0.215f * w * (1f - Mathf.Abs(yaw) * 0.12f);
            float ew = Mathf.Max(3.2f, 0.17f * w * a.EyeSize);
            foreach (var g in s.Glasses)
            {
                if (g == GlassesStyle.None) continue;
                Color frame = g == GlassesStyle.Thick ? Ui.Hex("#1a1412") : g == GlassesStyle.Round ? Ui.Hex("#8a6b3a") : Ui.Hex("#2b2b30");
                float lw = Mathf.Max(1.1f, h * (g == GlassesStyle.Thick ? 0.03f : 0.016f));
                Vector2 innerL = default, innerR = default;
                for (int side = -1; side <= 1; side += 2)
                {
                    float far = (side == 1 && yaw > 0f) || (side == -1 && yaw < 0f) ? Mathf.Abs(yaw) : 0f;
                    float rx = ew * 0.72f * (1f - 0.35f * far);
                    float ry = h * (g == GlassesStyle.Sunglasses ? 0.075f : 0.065f);
                    var ec = new Vector2(fx + side * spacing * (1f - 0.1f * far), eyeY + h * 0.005f);
                    if (g == GlassesStyle.Sunglasses)
                    {
                        var rr = new Rect(ec.x - rx * 1.1f, ec.y - ry, rx * 2.2f, ry * 2.1f);
                        p.RoundRect(rr, ry * 0.6f, Ui.Hex("#0e0e12"));
                        p.Line(new Vector2(rr.xMin + rr.width * 0.22f, rr.yMin + rr.height * 0.32f), new Vector2(rr.xMin + rr.width * 0.42f, rr.yMin + rr.height * 0.2f), Ui.Hex("#5a6878"), 1f);
                    }
                    else if (g == GlassesStyle.Round)
                    {
                        p.Ellipse(ec, rx, ry * 1.1f, new Color(0.75f, 0.85f, 0.95f, 0.14f));
                        p.EllipseStroke(ec, rx, ry * 1.1f, frame, lw);
                    }
                    else
                    {
                        var rr = new Rect(ec.x - rx, ec.y - ry, rx * 2f, ry * 2f);
                        p.RoundRect(rr, ry * 0.35f, new Color(0.75f, 0.85f, 0.95f, 0.14f));
                        p.RoundRectStroke(rr, ry * 0.35f, frame, lw);
                        if (h >= 40f) p.Line(new Vector2(rr.xMin + rr.width * 0.25f, rr.yMin + rr.height * 0.3f), new Vector2(rr.xMin + rr.width * 0.4f, rr.yMin + rr.height * 0.2f), new Color(1f, 1f, 1f, 0.55f), 1f);
                    }
                    var inner = new Vector2(ec.x - side * rx, ec.y - ry * 0.3f);
                    var outer = new Vector2(ec.x + side * rx * (g == GlassesStyle.Sunglasses ? 1.1f : 1f), ec.y - ry * 0.3f);
                    if (side < 0) innerL = inner; else innerR = inner;
                    if (far < 0.6f) p.Line(outer, outer + new Vector2(side * w * 0.12f * (1f - far), -h * 0.01f), frame, lw * 0.8f);
                }
                p.Curve(innerL, new Vector2(fx, eyeY - h * 0.04f), innerR, g == GlassesStyle.Sunglasses ? Ui.Hex("#111111") : frame, lw);
            }
        }

        // ================================================================== portrait body

        /// <summary>Neck, shoulders and clothes below a head (photo framing).</summary>
        public static void DrawShoulders(PixelPainter p, Vector2 headCenter, float h, Appearance a, float bottomY, float yaw)
        {
            float w = h * a.FaceWidth;
            Color skin = Looks.SkinOf(a);
            Color neck = Draw.Shade(skin, 0.86f);
            Color top = Looks.ClothOf(a.TopColor);
            Color topDark = Draw.Shade(top, 0.78f);
            Color inner = Looks.ClothOf(a.InnerColor);
            float x = headCenter.x;
            float neckTop = headCenter.y + 0.3f * h;
            float shoulderY = headCenter.y + 0.78f * h;
            float sw = 1.3f * h * a.Build;

            p.Rect(new Rect(x - w * 0.2f, neckTop, w * 0.4f, shoulderY - neckTop + 0.1f * h), neck);
            p.Rect(new Rect(x + w * 0.06f, neckTop, w * 0.14f, shoulderY - neckTop + 0.1f * h), Draw.Shade(neck, 0.86f));
            p.Ellipse(new Vector2(x, neckTop + 0.12f * h), w * 0.2f, h * 0.05f, Draw.Shade(neck, 0.82f));

            ShoulderPath(p, x, shoulderY, h, sw, w, bottomY);
            p.fillColor = top;
            p.Fill();
            // Shadow side of the torso.
            ShoulderPath(p, x + sw * 0.62f, shoulderY + 0.02f * h, h, sw * 0.5f, w, bottomY);
            p.fillColor = Draw.Alpha(topDark, 0.85f);
            p.Fill();

            Neckline(p, a.Top, x, shoulderY, w, h, top, inner, skin);
            if (a.Scarf)
            {
                Color sc = Looks.ClothOf(a.ScarfColor);
                p.RoundRect(new Rect(x - w * 0.45f, shoulderY - 0.12f * h, w * 0.9f, h * 0.2f), h * 0.08f, sc);
                p.RoundRect(new Rect(x + w * 0.1f, shoulderY, w * 0.22f, h * 0.5f), h * 0.05f, Draw.Shade(sc, 0.85f));
            }
        }

        static void ShoulderPath(PixelPainter p, float x, float shoulderY, float h, float sw, float w, float bottomY)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x - w * 0.24f, shoulderY - 0.06f * h));
            p.BezierCurveTo(new Vector2(x - sw * 0.6f, shoulderY - 0.05f * h), new Vector2(x - sw, shoulderY + 0.05f * h), new Vector2(x - sw * 1.05f, shoulderY + 0.4f * h));
            p.LineTo(new Vector2(x - sw * 1.12f, bottomY));
            p.LineTo(new Vector2(x + sw * 1.12f, bottomY));
            p.LineTo(new Vector2(x + sw * 1.05f, shoulderY + 0.4f * h));
            p.BezierCurveTo(new Vector2(x + sw, shoulderY + 0.05f * h), new Vector2(x + sw * 0.6f, shoulderY - 0.05f * h), new Vector2(x + w * 0.24f, shoulderY - 0.06f * h));
            p.ClosePath();
        }

        static void Neckline(PixelPainter p, TopStyle style, float x, float y, float w, float h, Color top, Color inner, Color skin)
        {
            Color dark = Draw.Shade(top, 0.72f);
            Color light = Color.Lerp(top, Color.white, 0.14f);
            float lw = Mathf.Max(1f, h * 0.022f);
            switch (style)
            {
                case TopStyle.TShirt:
                    p.Ellipse(new Vector2(x, y - 0.04f * h), w * 0.24f, h * 0.07f, Draw.Shade(skin, 0.86f));
                    p.Curve(new Vector2(x - w * 0.25f, y - 0.05f * h), new Vector2(x, y + 0.08f * h), new Vector2(x + w * 0.25f, y - 0.05f * h), dark, lw);
                    break;
                case TopStyle.Shirt:
                    p.Poly(Color.Lerp(inner, Color.white, 0.6f), new Vector2(x - w * 0.24f, y - 0.06f * h), new Vector2(x, y + 0.14f * h), new Vector2(x - w * 0.34f, y + 0.08f * h));
                    p.Poly(Color.Lerp(inner, Color.white, 0.5f), new Vector2(x + w * 0.24f, y - 0.06f * h), new Vector2(x, y + 0.14f * h), new Vector2(x + w * 0.34f, y + 0.08f * h));
                    p.Line(new Vector2(x, y + 0.14f * h), new Vector2(x, y + 0.6f * h), dark, 1f);
                    break;
                case TopStyle.Hoodie:
                    p.RoundRect(new Rect(x - w * 0.55f, y - 0.14f * h, w * 1.1f, h * 0.2f), h * 0.1f, dark);
                    p.Line(new Vector2(x - w * 0.1f, y + 0.02f * h), new Vector2(x - w * 0.12f, y + 0.3f * h), Ui.Hex("#e6e0d0"), Mathf.Max(1f, h * 0.014f));
                    p.Line(new Vector2(x + w * 0.1f, y + 0.02f * h), new Vector2(x + w * 0.12f, y + 0.3f * h), Ui.Hex("#d0c8b8"), Mathf.Max(1f, h * 0.014f));
                    break;
                case TopStyle.Jacket:
                case TopStyle.Suit:
                case TopStyle.Coat:
                {
                    Color shirt = style == TopStyle.Suit ? Ui.Hex("#ece8df") : inner;
                    p.Poly(shirt, new Vector2(x - w * 0.24f, y - 0.06f * h), new Vector2(x + w * 0.24f, y - 0.06f * h), new Vector2(x, y + 0.5f * h));
                    if (style == TopStyle.Suit)
                        p.Poly(inner, new Vector2(x - w * 0.05f, y), new Vector2(x + w * 0.05f, y), new Vector2(x + w * 0.07f, y + 0.45f * h), new Vector2(x - w * 0.07f, y + 0.45f * h));
                    float lap = style == TopStyle.Coat ? 0.5f : 0.36f;
                    p.Poly(light, new Vector2(x - w * 0.26f, y - 0.07f * h), new Vector2(x - w * 0.02f, y + 0.5f * h), new Vector2(x - w * lap, y + 0.2f * h));
                    p.Poly(dark, new Vector2(x + w * 0.26f, y - 0.07f * h), new Vector2(x + w * 0.02f, y + 0.5f * h), new Vector2(x + w * lap, y + 0.2f * h));
                    if (style == TopStyle.Jacket) p.Line(new Vector2(x + w * 0.02f, y + 0.5f * h), new Vector2(x + w * 0.02f, y + 2f * h), dark, 1f);
                    break;
                }
                case TopStyle.Sweater:
                    p.RoundRect(new Rect(x - w * 0.3f, y - 0.1f * h, w * 0.6f, h * 0.14f), h * 0.06f, dark);
                    break;
            }
        }

        // ================================================================== full figure (shop floor)

        /// <summary>Head height in stage units at scale 1. The whole body is ~7.2 heads tall.</summary>
        public const float BaseHeadHeight = 150f;

        /// <summary>
        /// Draws a whole customer with their feet at <paramref name="feet"/> (art px). <paramref name="scale"/>
        /// is the depth scale; one head is <see cref="BaseHeadHeight"/> x scale / 4 art pixels.
        /// Returns the head centre.
        /// </summary>
        public static Vector2 DrawFigure(PixelPainter p, Vector2 feet, float scale, Appearance a, BodyPose pose, HeadState head)
        {
            float H = BaseHeadHeight * 0.25f * scale;
            float build = a.Build;
            Color top = Looks.ClothOf(a.TopColor);
            Color topDark = Draw.Shade(top, 0.74f);
            Color topLight = Color.Lerp(top, Color.white, 0.14f);
            Color pants = Draw.Shade(Looks.ClothOf(a.TopColor + 7), 0.66f);
            Color pantsDark = Draw.Shade(pants, 0.78f);
            Color skin = Looks.SkinOf(a);
            float bob = pose.Walking ? Mathf.Abs(Mathf.Sin(pose.WalkPhase)) * 0.06f * H : 0f;

            float hipY;
            if (!pose.Seated)
            {
                hipY = feet.y - 3.25f * H - bob;
                float legW = 0.4f * H * build;
                for (int side = -1; side <= 1; side += 2)
                {
                    float lift = pose.Walking ? Mathf.Max(0f, Mathf.Sin(pose.WalkPhase + (side > 0 ? Mathf.PI : 0f))) * 0.22f * H : 0f;
                    float lx = feet.x + side * (0.06f * H + legW * 0.5f);
                    p.RoundRect(new Rect(lx - legW * 0.5f, hipY, legW, feet.y - lift - hipY - 0.08f * H), legW * 0.25f, side > 0 ? pantsDark : pants);
                    p.Ellipse(new Vector2(lx + side * 0.05f * H, feet.y - lift - 0.06f * H), legW * 0.6f, Mathf.Max(1f, 0.1f * H), Shoe);
                }
            }
            else
            {
                // Seated, seen from the front: shins straight down from the knees, close together so
                // the seat's legs show beside them. The lap (thighs toward the viewer) goes over the
                // torso's hem further down, with the hands resting on it.
                hipY = feet.y - 1.15f * H;
                float shinW = 0.3f * H * build;
                for (int side = -1; side <= 1; side += 2)
                {
                    float lx = feet.x + side * (0.06f * H + shinW * 0.5f);
                    p.RoundRect(new Rect(lx - shinW * 0.5f, hipY + 0.3f * H, shinW, feet.y - hipY - 0.38f * H), shinW * 0.25f, side > 0 ? pantsDark : pants);
                    p.Ellipse(new Vector2(lx + side * 0.04f * H, feet.y - 0.07f * H), shinW * 0.62f, Mathf.Max(1f, 0.12f * H), Shoe);
                }
            }

            float shoulderY = hipY - 2.4f * H - pose.Breath;
            float sw = 0.95f * H * build;          // outer edge of the shoulders
            float tw = sw - 0.3f * H;              // torso half-width (arms hang outside it)
            float ww = tw - 0.08f * H;             // waist half-width
            bool shortSleeves = a.Top == TopStyle.TShirt;

            // Arms hang beside the torso (drawn first; the torso overlaps their inner edge). Seated,
            // they stop at the elbow: the forearms come forward onto the lap, drawn after it.
            bool lapHands = pose.Seated && !pose.Holding;
            float swing = pose.Walking ? Mathf.Sin(pose.WalkPhase) * 0.16f * H : 0f;
            for (int side = -1; side <= 1; side += 2)
            {
                float ax = feet.x + side * (sw - 0.17f * H);
                float armLen = pose.Holding ? 1.85f * H : lapHands ? LapElbow * H : 2.3f * H + side * swing;
                Color sleeve = side > 0 ? topDark : Color.Lerp(top, topDark, 0.35f);
                var arm = new Rect(ax - 0.17f * H, shoulderY + 0.14f * H, 0.34f * H, armLen);
                p.RoundRect(arm, 0.15f * H, sleeve);
                if (shortSleeves)
                    p.RoundRect(new Rect(arm.x, arm.y + 0.8f * H, arm.width, armLen - 0.8f * H), 0.15f * H, side > 0 ? Draw.Shade(skin, 0.86f) : skin);
                if (!lapHands)
                    p.Ellipse(new Vector2(ax, shoulderY + 0.16f * H + armLen), Mathf.Max(0.9f, 0.15f * H), Mathf.Max(0.9f, 0.18f * H), side > 0 ? Draw.Shade(skin, 0.86f) : skin);
            }

            // Hoodies wear their hood behind the neck.
            if (a.Top == TopStyle.Hoodie)
                p.RoundRect(new Rect(feet.x - 0.42f * H, shoulderY - 0.22f * H, 0.84f * H, 0.4f * H), 0.18f * H, topDark);

            // Torso, lit from the left.
            TorsoPath(p, feet.x, shoulderY, hipY, sw, tw, ww, H);
            p.fillColor = top;
            p.Fill();
            TorsoPath(p, feet.x + tw * 0.62f, shoulderY + 0.06f * H, hipY, sw * 0.4f, tw * 0.38f, ww * 0.38f, H);
            p.fillColor = Draw.Alpha(topDark, 0.85f);
            p.Fill();
            p.Line(new Vector2(feet.x - tw + 1.6f, shoulderY + 0.85f * H), new Vector2(feet.x - ww + 1.6f, hipY), topLight, 1f);
            // Seams where the arms meet the body, so a sleeve reads as a sleeve.
            Color seam = Draw.Shade(top, 0.55f);
            p.Line(new Vector2(feet.x - tw, shoulderY + 0.8f * H), new Vector2(feet.x - ww, hipY + 0.1f * H), seam, 1f);
            p.Line(new Vector2(feet.x + tw, shoulderY + 0.8f * H), new Vector2(feet.x + ww, hipY + 0.1f * H), seam, 1f);
            BodyDetails(p, a, feet.x, shoulderY, hipY, tw, ww, H, top, topDark, topLight);
            if (pose.Seated) Lap(p, feet.x, shoulderY, hipY, sw, ww, H, lapHands, shortSleeves, top, topDark, pants, pantsDark, skin);

            float headH = H;
            var headC = new Vector2(feet.x, shoulderY - 0.2f * H - headH * 0.5f);
            // Neck + collar, reusing the portrait neckline at figure scale.
            p.Rect(new Rect(feet.x - 0.15f * H, headC.y + 0.3f * H, 0.3f * H, 0.45f * H), Draw.Shade(skin, 0.84f));
            Neckline(p, a.Top, feet.x, shoulderY + 0.02f * H, headH * a.FaceWidth, headH, top, Looks.ClothOf(a.InnerColor), skin);
            if (a.Scarf) p.RoundRect(new Rect(feet.x - 0.4f * H, shoulderY - 0.1f * H, 0.8f * H, 0.2f * H), 0.08f * H, Looks.ClothOf(a.ScarfColor));

            if (pose.Holding)
            {
                var hr = new Rect(feet.x - 0.5f * H, shoulderY + 1.75f * H, 1f * H, 0.72f * H);
                p.RoundRect(hr, 0.05f * H, pose.HoldingColor);
                p.Rect(new Rect(hr.center.x - 0.12f * H, hr.y + 0.2f * H, 0.24f * H, 0.24f * H), Ui.Hex("#d8b45a"));
                p.Ellipse(new Vector2(hr.xMin + 0.05f * H, hr.center.y), 0.16f * H, 0.18f * H, skin);
                p.Ellipse(new Vector2(hr.xMax - 0.05f * H, hr.center.y), 0.16f * H, 0.18f * H, skin);
            }

            DrawHead(p, headC, headH, a, head);
            return headC;
        }

        /// <summary>
        /// A seated figure's lap seen from the front: the thighs come toward the viewer as a wide,
        /// lit band over the coat's hem, split down the middle, and the hands rest on the knees.
        /// This is what reads as "sitting" at shop-floor size, chair or no chair.
        /// </summary>
        /// <summary>Seated upper-arm length (heads): the elbows bend above the lap.</summary>
        const float LapElbow = 1.35f;

        static void Lap(PixelPainter p, float x, float shoulderY, float hipY, float sw, float ww, float H, bool hands,
            bool shortSleeves, Color top, Color topDark, Color pants, Color pantsDark, Color skin)
        {
            float lapW = ww + 0.4f * H;
            var lap = new Rect(x - lapW, hipY + 0.02f * H, lapW * 2f, 0.38f * H);
            p.RoundRect(lap, 0.12f * H, Draw.Shade(pants, 1.14f));
            // The knees' front faces, a step darker, and the gap between the thighs.
            p.Rect(new Rect(lap.x + 0.06f * H, lap.yMax - 0.12f * H, lap.width - 0.12f * H, 0.12f * H), pants);
            p.Line(new Vector2(x, lap.y + 0.08f * H), new Vector2(x, lap.yMax), pantsDark, 1f);
            if (!hands) return;
            for (int side = -1; side <= 1; side += 2)
            {
                var elbow = new Vector2(x + side * (sw - 0.17f * H), shoulderY + 0.14f * H + (LapElbow - 0.12f) * H);
                var hand = new Vector2(x + side * lapW * 0.6f, lap.y + 0.1f * H);
                Color sleeve = side > 0 ? topDark : Color.Lerp(top, topDark, 0.35f);
                Color hs = side > 0 ? Draw.Shade(skin, 0.86f) : skin;
                p.Line(elbow, hand, shortSleeves ? hs : sleeve, Mathf.Max(1f, 0.32f * H));
                p.Ellipse(hand, Mathf.Max(0.9f, 0.17f * H), Mathf.Max(0.9f, 0.14f * H), hs);
            }
        }

        static void TorsoPath(PixelPainter p, float x, float shoulderY, float hipY, float sw, float tw, float ww, float H)
        {
            // Shoulders run out over the tops of the arms, then the sides tuck in at the armpits.
            p.BeginPath();
            p.MoveTo(new Vector2(x - ww, hipY + 0.12f * H));
            p.LineTo(new Vector2(x - tw, shoulderY + 0.8f * H));
            p.LineTo(new Vector2(x - sw, shoulderY + 0.52f * H));
            p.BezierCurveTo(new Vector2(x - sw, shoulderY + 0.08f * H), new Vector2(x - sw * 0.6f, shoulderY - 0.04f * H), new Vector2(x, shoulderY - 0.04f * H));
            p.BezierCurveTo(new Vector2(x + sw * 0.6f, shoulderY - 0.04f * H), new Vector2(x + sw, shoulderY + 0.08f * H), new Vector2(x + sw, shoulderY + 0.52f * H));
            p.LineTo(new Vector2(x + tw, shoulderY + 0.8f * H));
            p.LineTo(new Vector2(x + ww, hipY + 0.12f * H));
            p.ClosePath();
        }

        /// <summary>Zips, buttons, pockets, belts: what tells a coat from a hoodie at a glance.</summary>
        static void BodyDetails(PixelPainter p, Appearance a, float x, float shoulderY, float hipY, float tw, float ww, float H, Color top, Color dark, Color light)
        {
            float mid = (shoulderY + hipY) * 0.5f;
            Color line = Draw.Shade(top, 0.62f);
            switch (a.Top)
            {
                case TopStyle.Jacket:
                    p.Line(new Vector2(x, shoulderY + 0.5f * H), new Vector2(x, hipY + 0.1f * H), line, 1f);
                    p.Line(new Vector2(x - tw * 0.6f, mid + 0.4f * H), new Vector2(x - tw * 0.2f, mid + 0.4f * H), line, 1f);
                    p.Line(new Vector2(x + tw * 0.2f, mid + 0.4f * H), new Vector2(x + tw * 0.6f, mid + 0.4f * H), line, 1f);
                    p.Rect(new Rect(x - ww, hipY - 0.12f * H, ww * 2f, 0.2f * H), dark);
                    break;
                case TopStyle.Coat:
                    p.Line(new Vector2(x + 0.04f * H, shoulderY + 0.5f * H), new Vector2(x + 0.04f * H, hipY + 0.1f * H), line, 1f);
                    p.Rect(new Rect(x - ww, mid + 0.25f * H, ww * 2f, 0.14f * H), dark);
                    p.Rect(new Rect(x - 0.08f * H, mid + 0.22f * H, 0.16f * H, 0.2f * H), Ui.Hex("#8a7a4a"));
                    for (int i = 0; i < 3; i++) p.Ellipse(new Vector2(x - tw * 0.4f, shoulderY + (0.8f + i * 0.5f) * H), 0.06f * H, 0.06f * H, line);
                    break;
                case TopStyle.Hoodie:
                    p.RoundRect(new Rect(x - tw * 0.6f, mid + 0.3f * H, tw * 1.2f, 0.45f * H), 0.1f * H, Draw.Shade(top, 0.86f));
                    p.Line(new Vector2(x - tw * 0.6f, mid + 0.3f * H), new Vector2(x + tw * 0.6f, mid + 0.3f * H), line, 1f);
                    break;
                case TopStyle.Suit:
                    for (int i = 0; i < 2; i++) p.Ellipse(new Vector2(x - 0.05f * H, mid + (0.15f + i * 0.4f) * H), 0.05f * H, 0.05f * H, line);
                    p.Line(new Vector2(x - tw * 0.75f, mid - 0.2f * H), new Vector2(x - tw * 0.4f, mid - 0.2f * H), light, 1f);
                    break;
                case TopStyle.Sweater:
                    p.Rect(new Rect(x - ww, hipY - 0.16f * H, ww * 2f, 0.24f * H), dark);
                    for (float xx = x - ww + 1f; xx < x + ww; xx += Mathf.Max(2f, H * 0.12f))
                        p.Line(new Vector2(xx, hipY - 0.14f * H), new Vector2(xx, hipY + 0.06f * H), Draw.Shade(dark, 0.85f), 1f);
                    break;
                case TopStyle.Shirt:
                    for (int i = 0; i < 4; i++) p.Ellipse(new Vector2(x, shoulderY + (0.6f + i * 0.45f) * H), 0.05f * H, 0.05f * H, Draw.Shade(top, 0.6f));
                    p.Line(new Vector2(x, shoulderY + 0.3f * H), new Vector2(x, hipY + 0.1f * H), line, 1f);
                    break;
                case TopStyle.TShirt:
                    if (H >= 20f) p.Rect(new Rect(x - tw * 0.35f, mid - 0.2f * H, tw * 0.7f, 0.45f * H), Draw.Mix(top, Ui.Hex("#e8e0cc"), 0.35f));
                    break;
            }
        }

        /// <summary>Approximate silhouette test in figure-local stage coordinates (for picking).</summary>
        public static bool HitFigure(Vector2 local, Vector2 feet, float scale, bool seated)
        {
            float H = BaseHeadHeight * scale;
            float hipY = seated ? feet.y - 1.15f * H : feet.y - 3.25f * H;
            float shoulderY = hipY - 2.4f * H;
            var headC = new Vector2(feet.x, shoulderY - 0.7f * H);
            if ((local - headC).sqrMagnitude < (0.6f * H) * (0.6f * H)) return true;
            return local.x > feet.x - 1.2f * H && local.x < feet.x + 1.2f * H && local.y > shoulderY - 0.1f * H && local.y < feet.y;
        }
    }
}
