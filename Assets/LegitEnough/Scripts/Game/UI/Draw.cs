using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>Painter2D shape helpers for the placeholder vector art.</summary>
    public static class Draw
    {
        const float K = 0.5522847f; // circle bezier constant

        public static void EllipsePath(Painter2D p, Vector2 c, float rx, float ry)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(c.x + rx, c.y));
            p.BezierCurveTo(new Vector2(c.x + rx, c.y + ry * K), new Vector2(c.x + rx * K, c.y + ry), new Vector2(c.x, c.y + ry));
            p.BezierCurveTo(new Vector2(c.x - rx * K, c.y + ry), new Vector2(c.x - rx, c.y + ry * K), new Vector2(c.x - rx, c.y));
            p.BezierCurveTo(new Vector2(c.x - rx, c.y - ry * K), new Vector2(c.x - rx * K, c.y - ry), new Vector2(c.x, c.y - ry));
            p.BezierCurveTo(new Vector2(c.x + rx * K, c.y - ry), new Vector2(c.x + rx, c.y - ry * K), new Vector2(c.x + rx, c.y));
            p.ClosePath();
        }

        public static void Ellipse(Painter2D p, Vector2 c, float rx, float ry, Color fill)
        {
            if (rx <= 0f || ry <= 0f) return;
            EllipsePath(p, c, rx, ry);
            p.fillColor = fill;
            p.Fill();
        }

        public static void EllipseStroke(Painter2D p, Vector2 c, float rx, float ry, Color stroke, float width)
        {
            if (rx <= 0f || ry <= 0f) return;
            EllipsePath(p, c, rx, ry);
            p.strokeColor = stroke;
            p.lineWidth = width;
            p.Stroke();
        }

        public static void RoundRectPath(Painter2D p, Rect r, float radius)
        {
            float rad = Mathf.Min(radius, r.width * 0.5f, r.height * 0.5f);
            float k = rad * (1f - K);
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin + rad, r.yMin));
            p.LineTo(new Vector2(r.xMax - rad, r.yMin));
            p.BezierCurveTo(new Vector2(r.xMax - k, r.yMin), new Vector2(r.xMax, r.yMin + k), new Vector2(r.xMax, r.yMin + rad));
            p.LineTo(new Vector2(r.xMax, r.yMax - rad));
            p.BezierCurveTo(new Vector2(r.xMax, r.yMax - k), new Vector2(r.xMax - k, r.yMax), new Vector2(r.xMax - rad, r.yMax));
            p.LineTo(new Vector2(r.xMin + rad, r.yMax));
            p.BezierCurveTo(new Vector2(r.xMin + k, r.yMax), new Vector2(r.xMin, r.yMax - k), new Vector2(r.xMin, r.yMax - rad));
            p.LineTo(new Vector2(r.xMin, r.yMin + rad));
            p.BezierCurveTo(new Vector2(r.xMin, r.yMin + k), new Vector2(r.xMin + k, r.yMin), new Vector2(r.xMin + rad, r.yMin));
            p.ClosePath();
        }

        public static void RoundRect(Painter2D p, Rect r, float radius, Color fill)
        {
            if (r.width <= 0f || r.height <= 0f) return;
            RoundRectPath(p, r, radius);
            p.fillColor = fill;
            p.Fill();
        }

        public static void RoundRectStroke(Painter2D p, Rect r, float radius, Color stroke, float width)
        {
            RoundRectPath(p, r, radius);
            p.strokeColor = stroke;
            p.lineWidth = width;
            p.Stroke();
        }

        public static void Poly(Painter2D p, Color fill, params Vector2[] pts)
        {
            if (pts.Length < 3) return;
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath();
            p.fillColor = fill;
            p.Fill();
        }

        public static void Line(Painter2D p, Vector2 a, Vector2 b, Color c, float width)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.strokeColor = c;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.Stroke();
        }

        public static void Curve(Painter2D p, Vector2 a, Vector2 control, Vector2 b, Color c, float width)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.QuadraticCurveTo(control, b);
            p.strokeColor = c;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.Stroke();
        }

        public static void Rect(Painter2D p, Rect r, Color fill)
        {
            Poly(p, fill, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax));
        }

        public static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        public static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);

        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
