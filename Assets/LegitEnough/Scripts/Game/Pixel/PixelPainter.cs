using System.Collections.Generic;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>
    /// A software stand-in for UI Toolkit's Painter2D that rasterizes into a
    /// <see cref="PixelBuffer"/> with hard edges: paths (lines, quadratic and cubic curves) fill
    /// with the non-zero rule and stroke with round joins. Coordinates are art pixels after
    /// <c>p * Scale + Offset</c>. Character art keeps its vector geometry (so photo scoring
    /// still matches) and still comes out as crisp pixels.
    /// </summary>
    public sealed class PixelPainter
    {
        public PixelBuffer Target;
        public float Scale = 1f;
        public Vector2 Offset;
        public Color32 fillColor = new Color32(0, 0, 0, 255);
        public Color32 strokeColor = new Color32(0, 0, 0, 255);
        public float lineWidth = 1f;

        readonly List<List<Vector2>> _subpaths = new List<List<Vector2>>();
        readonly List<bool> _closed = new List<bool>();
        List<Vector2> _cur;
        Vector2 _pen;
        int[] _mark = new int[0];
        int _gen;
        int _minX, _minY, _maxX, _maxY;

        public PixelPainter(PixelBuffer target) => Target = target;

        /// <summary>Pixel bounds of the last Fill/Stroke (empty when nothing was painted).</summary>
        public RectInt LastBounds => _maxX < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(_minX, _minY, _maxX - _minX + 1, _maxY - _minY + 1);

        Vector2 T(Vector2 p) => p * Scale + Offset;

        // ------------------------------------------------------------------ path API (Painter2D-like)

        public void BeginPath()
        {
            _subpaths.Clear();
            _closed.Clear();
            _cur = null;
        }

        public void MoveTo(Vector2 p)
        {
            _cur = new List<Vector2> { T(p) };
            _subpaths.Add(_cur);
            _closed.Add(false);
            _pen = p;
        }

        public void LineTo(Vector2 p)
        {
            if (_cur == null) MoveTo(_pen);
            _cur.Add(T(p));
            _pen = p;
        }

        public void QuadraticCurveTo(Vector2 c, Vector2 p)
        {
            if (_cur == null) MoveTo(_pen);
            var a = _pen;
            int n = Segments(a, c, c, p);
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n, u = 1f - t;
                _cur.Add(T(u * u * a + 2f * u * t * c + t * t * p));
            }
            _pen = p;
        }

        public void BezierCurveTo(Vector2 c1, Vector2 c2, Vector2 p)
        {
            if (_cur == null) MoveTo(_pen);
            var a = _pen;
            int n = Segments(a, c1, c2, p);
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n, u = 1f - t;
                _cur.Add(T(u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * p));
            }
            _pen = p;
        }

        public void ClosePath()
        {
            if (_cur == null) return;
            _closed[_closed.Count - 1] = true;
            if (_cur.Count > 0) _pen = InverseT(_cur[0]);
            _cur = null;
        }

        Vector2 InverseT(Vector2 art) => Scale != 0f ? (art - Offset) / Scale : art;

        int Segments(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float len = (Vector2.Distance(a, b) + Vector2.Distance(b, c) + Vector2.Distance(c, d)) * Scale;
            return Mathf.Clamp(Mathf.CeilToInt(len / 1.5f), 3, 48);
        }

        /// <summary>Fills every subpath with the non-zero rule.</summary>
        public void Fill()
        {
            if (fillColor.a == 0 || _subpaths.Count == 0) return;
            BeginMask();
            ScanFill(_subpaths);
            PaintMask(fillColor);
        }

        /// <summary>Strokes every subpath with round joins and caps.</summary>
        public void Stroke()
        {
            if (strokeColor.a == 0 || _subpaths.Count == 0) return;
            BeginMask();
            float hw = lineWidth * Scale * 0.5f;
            for (int s = 0; s < _subpaths.Count; s++)
            {
                var pts = _subpaths[s];
                int n = pts.Count;
                if (n == 0) continue;
                int segs = _closed[s] ? n : n - 1;
                for (int i = 0; i < segs; i++) MarkSegment(pts[i], pts[(i + 1) % n], hw);
                if (n == 1) MarkDisc(pts[0], hw);
            }
            PaintMask(strokeColor);
        }

        // ------------------------------------------------------------------ helpers (Draw.* equivalents)

        public void Ellipse(Vector2 c, float rx, float ry, Color32 fill)
        {
            if (rx <= 0f || ry <= 0f) return;
            var a = T(c);
            Target.Ellipse(a.x, a.y, rx * Scale, ry * Scale, fill);
        }

        public void EllipseStroke(Vector2 c, float rx, float ry, Color32 stroke, float width)
        {
            if (rx <= 0f || ry <= 0f) return;
            BeginPath();
            const float k = 0.5522847f;
            MoveTo(new Vector2(c.x + rx, c.y));
            BezierCurveTo(new Vector2(c.x + rx, c.y + ry * k), new Vector2(c.x + rx * k, c.y + ry), new Vector2(c.x, c.y + ry));
            BezierCurveTo(new Vector2(c.x - rx * k, c.y + ry), new Vector2(c.x - rx, c.y + ry * k), new Vector2(c.x - rx, c.y));
            BezierCurveTo(new Vector2(c.x - rx, c.y - ry * k), new Vector2(c.x - rx * k, c.y - ry), new Vector2(c.x, c.y - ry));
            BezierCurveTo(new Vector2(c.x + rx * k, c.y - ry), new Vector2(c.x + rx, c.y - ry * k), new Vector2(c.x + rx, c.y));
            ClosePath();
            strokeColor = stroke;
            lineWidth = width;
            Stroke();
        }

        public void RoundRectPath(Rect r, float radius)
        {
            const float K = 0.5522847f;
            float rad = Mathf.Min(radius, r.width * 0.5f, r.height * 0.5f);
            float k = rad * (1f - K);
            BeginPath();
            MoveTo(new Vector2(r.xMin + rad, r.yMin));
            LineTo(new Vector2(r.xMax - rad, r.yMin));
            BezierCurveTo(new Vector2(r.xMax - k, r.yMin), new Vector2(r.xMax, r.yMin + k), new Vector2(r.xMax, r.yMin + rad));
            LineTo(new Vector2(r.xMax, r.yMax - rad));
            BezierCurveTo(new Vector2(r.xMax, r.yMax - k), new Vector2(r.xMax - k, r.yMax), new Vector2(r.xMax - rad, r.yMax));
            LineTo(new Vector2(r.xMin + rad, r.yMax));
            BezierCurveTo(new Vector2(r.xMin + k, r.yMax), new Vector2(r.xMin, r.yMax - k), new Vector2(r.xMin, r.yMax - rad));
            LineTo(new Vector2(r.xMin, r.yMin + rad));
            BezierCurveTo(new Vector2(r.xMin, r.yMin + k), new Vector2(r.xMin + k, r.yMin), new Vector2(r.xMin + rad, r.yMin));
            ClosePath();
        }

        public void RoundRect(Rect r, float radius, Color32 fill)
        {
            if (r.width <= 0f || r.height <= 0f) return;
            RoundRectPath(r, radius);
            fillColor = fill;
            Fill();
        }

        public void RoundRectStroke(Rect r, float radius, Color32 stroke, float width)
        {
            RoundRectPath(r, radius);
            strokeColor = stroke;
            lineWidth = width;
            Stroke();
        }

        public void Poly(Color32 fill, params Vector2[] pts)
        {
            if (pts.Length < 3) return;
            BeginPath();
            MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) LineTo(pts[i]);
            ClosePath();
            fillColor = fill;
            Fill();
        }

        public void Line(Vector2 a, Vector2 b, Color32 c, float width)
        {
            BeginPath();
            MoveTo(a);
            LineTo(b);
            strokeColor = c;
            lineWidth = width;
            Stroke();
        }

        public void Curve(Vector2 a, Vector2 control, Vector2 b, Color32 c, float width)
        {
            BeginPath();
            MoveTo(a);
            QuadraticCurveTo(control, b);
            strokeColor = c;
            lineWidth = width;
            Stroke();
        }

        public void Rect(Rect r, Color32 fill) =>
            Poly(fill, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax));

        // ------------------------------------------------------------------ rasterization

        readonly List<List<Vector2>> _quad = new List<List<Vector2>> { new List<Vector2>(4) };
        readonly List<int> _order = new List<int>();

        void BeginMask()
        {
            int n = Target.W * Target.H;
            if (_mark.Length < n)
            {
                _mark = new int[n];
                _gen = 0;
            }
            _gen++;
            if (_gen == int.MaxValue)
            {
                System.Array.Clear(_mark, 0, _mark.Length);
                _gen = 1;
            }
            _minX = int.MaxValue;
            _minY = int.MaxValue;
            _maxX = -1;
            _maxY = -1;
        }

        void Mark(int x, int y)
        {
            if ((uint)x >= (uint)Target.W || (uint)y >= (uint)Target.H) return;
            _mark[y * Target.W + x] = _gen;
            if (x < _minX) _minX = x;
            if (y < _minY) _minY = y;
            if (x > _maxX) _maxX = x;
            if (y > _maxY) _maxY = y;
        }

        readonly List<float> _xs = new List<float>();
        readonly List<int> _dirs = new List<int>();

        /// <summary>Marks pixels whose centres are inside the polygons (non-zero winding).</summary>
        void ScanFill(List<List<Vector2>> polys)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var poly in polys)
                if (poly.Count >= 3)
                    foreach (var p in poly)
                {
                    if (p.y < minY) minY = p.y;
                    if (p.y > maxY) maxY = p.y;
                }
            if (minY > maxY) return;
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY)), y1 = Mathf.Min(Target.H - 1, Mathf.CeilToInt(maxY));
            var order = _order;
            for (int y = y0; y <= y1; y++)
            {
                float yc = y + 0.5f;
                _xs.Clear();
                _dirs.Clear();
                foreach (var poly in polys)
                {
                    int n = poly.Count;
                    if (n < 3) continue;
                    for (int i = 0; i < n; i++)
                    {
                        var a = poly[i];
                        var b = poly[(i + 1) % n];
                        if (a.y == b.y) continue;
                        bool down = a.y < b.y;
                        float lo = down ? a.y : b.y, hi = down ? b.y : a.y;
                        if (yc < lo || yc >= hi) continue;
                        float t = (yc - a.y) / (b.y - a.y);
                        _xs.Add(a.x + (b.x - a.x) * t);
                        _dirs.Add(down ? 1 : -1);
                    }
                }
                if (_xs.Count < 2) continue;
                order.Clear();
                for (int i = 0; i < _xs.Count; i++) order.Add(i);
                order.Sort((i, j) => _xs[i].CompareTo(_xs[j]));
                int wind = 0;
                for (int k = 0; k < order.Count - 1; k++)
                {
                    wind += _dirs[order[k]];
                    if (wind == 0) continue;
                    float xa = _xs[order[k]], xb = _xs[order[k + 1]];
                    int px0 = Mathf.CeilToInt(xa - 0.5f), px1 = Mathf.CeilToInt(xb - 0.5f) - 1;
                    for (int x = px0; x <= px1; x++) Mark(x, y);
                }
            }
        }

        void MarkSegment(Vector2 a, Vector2 b, float hw)
        {
            if (hw < 0.6f)
            {
                // Hairline: a one-pixel Bresenham line.
                int x0 = Mathf.FloorToInt(a.x), y0 = Mathf.FloorToInt(a.y), x1 = Mathf.FloorToInt(b.x), y1 = Mathf.FloorToInt(b.y);
                int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
                for (int guard = 0; guard < 4096; guard++)
                {
                    Mark(x0, y0);
                    if (x0 == x1 && y0 == y1) break;
                    int e2 = 2 * err;
                    if (e2 >= dy) { err += dy; x0 += sx; }
                    if (e2 <= dx) { err += dx; y0 += sy; }
                }
                return;
            }
            var d = b - a;
            float len = d.magnitude;
            if (len > 0.0001f)
            {
                var nrm = new Vector2(-d.y, d.x) / len * hw;
                var q = _quad[0];
                q.Clear();
                q.Add(a + nrm);
                q.Add(b + nrm);
                q.Add(b - nrm);
                q.Add(a - nrm);
                ScanFill(_quad);
            }
            MarkDisc(a, hw);
            MarkDisc(b, hw);
        }

        void MarkDisc(Vector2 c, float r)
        {
            if (r < 0.6f)
            {
                Mark(Mathf.FloorToInt(c.x), Mathf.FloorToInt(c.y));
                return;
            }
            int x0 = Mathf.FloorToInt(c.x - r), x1 = Mathf.CeilToInt(c.x + r), y0 = Mathf.FloorToInt(c.y - r), y1 = Mathf.CeilToInt(c.y + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - c.x, dy = y + 0.5f - c.y;
                    if (dx * dx + dy * dy <= r * r) Mark(x, y);
                }
        }

        void PaintMask(Color32 c)
        {
            if (_maxX < 0) return;
            for (int y = _minY; y <= _maxY; y++)
            {
                int row = y * Target.W;
                for (int x = _minX; x <= _maxX; x++)
                    if (_mark[row + x] == _gen) Target.Set(x, y, c);
            }
        }
    }
}
