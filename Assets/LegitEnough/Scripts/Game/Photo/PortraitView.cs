using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;
using Box = LegitEnough.Sim.Box;

namespace LegitEnough.Game
{
    /// <summary>
    /// Draws what the camera sees: backdrop + seated customer, either live (a running
    /// <see cref="PhotoSession"/>) or frozen (a <see cref="PhotoCapture"/>). <see cref="View"/>
    /// selects the part of the frame to show (full frame, or a crop), and the element clips to it.
    /// Uses <see cref="PortraitGeometry"/>, the same math the evaluator scores with. The picture
    /// is pixel art whose resolution follows the camera: the cheap camera gives chunkier pixels.
    /// </summary>
    public sealed class PortraitView : VisualElement
    {
        readonly PixelImage _canvas = new PixelImage();
        readonly PixelBuffer _buf = new PixelBuffer(8, 8);
        readonly PixelBuffer _subject = new PixelBuffer(8, 8);
        readonly PixelPainter _painter;
        Box _view = new Box(0f, 0f, 1f, 1f);
        int _scale = 1;
        float _artPerUnitFrame;
        string _staticKey;

        public PhotoSession Live;
        public PhotoCapture Capture;
        public bool ShowGrain = true;

        public Box View
        {
            get => _view;
            set
            {
                _view = value;
                Relayout();
            }
        }

        public PortraitView(string classes = null)
        {
            AddToClassList("le-portrait");
            Ui.AddClasses(this, classes);
            style.overflow = Overflow.Hidden;
            _canvas.style.position = Position.Absolute;
            Add(_canvas);
            _painter = new PixelPainter(_subject);
            pickingMode = PickingMode.Position;
            RegisterCallback<GeometryChangedEvent>(_ => Relayout());
            RegisterCallback<AttachToPanelEvent>(_ => _staticKey = null);
        }

        public void Refresh() => Render();

        void Relayout()
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            float cw = r.width / Mathf.Max(0.01f, _view.W);
            float ch = r.height / Mathf.Max(0.01f, _view.H);
            _canvas.style.left = -_view.X * cw;
            _canvas.style.top = -_view.Y * ch;
            int sensor = Capture != null ? Capture.SensorHeightPx : Live != null ? Live.Camera.SensorHeightPx : 2400;
            float targetArtHeight = sensor <= 1600 ? 128f : sensor <= 2400 ? 168f : 212f;
            _scale = Mathf.Max(1, Mathf.RoundToInt(ch / targetArtHeight));
            int aw = Mathf.Max(4, Mathf.CeilToInt(cw / _scale));
            int ah = Mathf.Max(4, Mathf.CeilToInt(ch / _scale));
            _canvas.style.width = aw * _scale;
            _canvas.style.height = ah * _scale;
            _artPerUnitFrame = ch / _scale;
            if (_buf.W != aw || _buf.H != ah)
            {
                _buf.Resize(aw, ah);
                _subject.Resize(aw, ah);
            }
            _staticKey = null;
            Render();
        }

        /// <summary>Normalized frame point (0..1) → element-local pixels.</summary>
        public Vector2 FrameToLocal(Vector2 n)
        {
            var r = contentRect;
            return new Vector2((n.x - _view.X) / _view.W * r.width, (n.y - _view.Y) / _view.H * r.height);
        }

        public Vector2 LocalToFrame(Vector2 local)
        {
            var r = contentRect;
            return new Vector2(_view.X + local.x / r.width * _view.W, _view.Y + local.y / r.height * _view.H);
        }

        void Render()
        {
            if (_artPerUnitFrame <= 0f) return;
            if (Capture == null && Live == null)
            {
                _buf.Clear();
                _buf.Fill(new Color32(0, 0, 0, 255));
                _canvas.Upload(_buf);
                return;
            }
            // Frozen photos only redraw when something about them changes.
            if (Capture != null)
            {
                string key = Capture.Id + "|" + _buf.W + "x" + _buf.H + "|" + ShowGrain;
                if (key == _staticKey && _canvas.HasTexture) return;
                _staticKey = key;
            }
            DrawFrame();
            _canvas.Upload(_buf);
        }

        void DrawFrame()
        {
            Appearance look;
            HeadPose pose;
            float gazeX, gazeY, lid, seat, slouch, zoom, panX, panY, noise;
            Expression expr;
            System.Collections.Generic.IReadOnlyList<GlassesStyle> glasses;
            bool hatOn, blurred = false, backdropMoving = false;
            Backdrop backdrop;
            Backdrop? pending = null;
            float swapProgress = 0f;
            int grainSeed, station;

            if (Capture != null)
            {
                var c = Capture;
                look = c.Look;
                pose = c.Pose;
                gazeX = c.GazeX;
                gazeY = c.GazeY;
                lid = c.Lid;
                expr = c.Expression;
                glasses = c.Glasses;
                hatOn = c.HatOn;
                seat = c.SeatOffset;
                slouch = c.Slouch;
                zoom = c.Zoom;
                panX = c.PanX;
                panY = c.PanY;
                backdrop = c.Backdrop;
                noise = c.Noise;
                blurred = c.Blurred;
                backdropMoving = c.BackdropMoving;
                grainSeed = c.Id * 7919;
                station = c.LightingBonus < 0f ? 1 : c.LightingBonus > 0f ? 3 : 2;
            }
            else
            {
                var s = Live.Subject;
                var rig = Live.Rig;
                look = s.Customer.Look;
                pose = s.Pose;
                gazeX = s.GazeX;
                gazeY = s.GazeY;
                lid = s.Lid;
                expr = s.Expression;
                glasses = s.Glasses;
                hatOn = s.HatOn;
                seat = s.SeatOffset;
                slouch = s.Slouch;
                zoom = rig.Zoom;
                panX = rig.PanX;
                panY = rig.PanY;
                backdrop = rig.Backdrop;
                pending = rig.PendingBackdrop;
                swapProgress = rig.SwappingBackdrop ? 1f - rig.BackdropSwapRemaining / Mathf.Max(0.01f, Live.Station.BackdropSwapSeconds) : 0f;
                noise = Live.Camera.Noise;
                grainSeed = (int)(Time.unscaledTime * 12f);
                station = Live.Station.Tier;
            }

            int W = _buf.W, H = _buf.H;
            float frameH = _artPerUnitFrame;

            // Backdrop: wrinkled bedsheet, smooth paper, or a lit studio sweep.
            Color32 bg = Looks.Of(backdrop);
            DrawBackdrop(_buf, bg, station);
            if (pending.HasValue)
            {
                int y = Mathf.RoundToInt(frameH * swapProgress);
                var tmp = new PixelBuffer(W, Mathf.Max(1, y));
                DrawBackdrop(tmp, Looks.Of(pending.Value), station);
                _buf.Blit(tmp, 0, 0);
                _buf.Rect(0, y - 1, W, 2, new Color32(30, 28, 26, 255));
            }
            if (backdropMoving)
                for (int i = 0; i < 6; i++)
                    _buf.Rect(0, Mathf.RoundToInt(frameH * (0.1f + i * 0.15f)), W, Mathf.Max(1, Mathf.RoundToInt(frameH * 0.04f)), new Color32(0, 0, 0, 30));

            var frame = PortraitGeometry.FrameWorld(zoom, panX, panY);
            var skull = PortraitGeometry.SkullWorld(look, pose, seat, slouch);
            float pxPerUnit = frameH / frame.H;
            var center = new Vector2((skull.CenterX - frame.X) * pxPerUnit, (skull.CenterY - frame.Y) * pxPerUnit);
            float h = skull.H * pxPerUnit;

            // Subject shadow on the backdrop (light from the left).
            _buf.Ellipse(center.x + h * 0.45f, center.y + h * 0.2f, h * 0.62f, h * 0.8f, new Color32(0, 0, 0, 28));

            var head = new HeadState
            {
                Yaw = pose.YawF,
                Pitch = pose.PitchF,
                GazeX = gazeX,
                GazeY = gazeY,
                Lid = lid,
                Expression = expr,
                Glasses = glasses,
                HatOn = hatOn,
                Blurred = blurred,
            };
            _subject.Clear();
            _painter.Target = _subject;
            FigurePainter.DrawShoulders(_painter, center, h, look, H + h, pose.YawF);
            FigurePainter.DrawHead(_painter, center, h, look, head);
            _subject.Outline(Pal.Outline);
            _buf.Blit(_subject, 0, 0);

            // The cheap kit lights unevenly: the far side of the frame falls off a little.
            if (station == 1)
                for (int y = 0; y < H; y++)
                    for (int x = W * 3 / 4; x < W; x++)
                        if (((x + y) & 1) == 0 || x > W * 7 / 8) _buf.Px[y * W + x] = Pal.Mul(_buf.Px[y * W + x], 0.93f);

            if (ShowGrain && noise > 0f)
            {
                var rng = new PixelRandom(grainSeed);
                int dots = Mathf.RoundToInt(noise * W * H * 0.08f);
                var dark = new Color32(0, 0, 0, 40);
                var light = new Color32(255, 255, 255, 34);
                for (int i = 0; i < dots; i++)
                    _buf.Set(rng.Range(0, W), rng.Range(0, H), (i & 1) == 0 ? dark : light);
            }
        }

        static void DrawBackdrop(PixelBuffer b, Color32 bg, int station)
        {
            int W = b.W, H = b.H;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color32 c = bg;
                    if (station == 1)
                    {
                        float f = Mathf.Sin(x * 0.33f * 120f / W + Mathf.Sin(y * 0.05f * 160f / H + x * 0.04f) * 2.2f);
                        c = f > 0.7f ? Pal.Mix(bg, new Color32(255, 255, 255, 255), 0.1f) : f < -0.6f ? Pal.Mul(bg, 0.87f) : bg;
                    }
                    else if (station == 3)
                    {
                        float dx = (x - W * 0.42f) / (W * 0.7f), dy = (y - H * 0.4f) / (H * 0.8f);
                        c = dx * dx + dy * dy < 0.35f ? Pal.Mix(bg, new Color32(255, 255, 255, 255), 0.07f) : bg;
                    }
                    b.Px[y * W + x] = c;
                }
            // Floor of the frame is a touch darker.
            for (int y = (int)(H * 0.78f); y < H; y++)
                for (int x = 0; x < W; x++) b.Px[y * W + x] = Pal.Mul(b.Px[y * W + x], 0.95f);
        }

        // ------------------------------------------------------------------ hit testing (camera view)

        public enum Part { None, Head, Glasses, Hat, Body, Background }

        /// <summary>What's under a point (element-local).</summary>
        public Part HitTest(Vector2 local)
        {
            Appearance look;
            HeadPose pose;
            float seat, slouch, zoom, panX, panY;
            bool hasGlasses, hatOn;
            if (Live != null)
            {
                var s = Live.Subject;
                look = s.Customer.Look;
                pose = s.Pose;
                seat = s.SeatOffset;
                slouch = s.Slouch;
                zoom = Live.Rig.Zoom;
                panX = Live.Rig.PanX;
                panY = Live.Rig.PanY;
                hasGlasses = s.HasGlasses;
                hatOn = s.HatOn && look.Hat != HatStyle.None;
            }
            else return Part.None;

            var n = LocalToFrame(local);
            var frame = PortraitGeometry.FrameWorld(zoom, panX, panY);
            var world = new Vector2(frame.X + n.x * frame.W, frame.Y + n.y * frame.H);
            var skull = PortraitGeometry.SkullWorld(look, pose, seat, slouch);
            float eyeY = skull.CenterY - 0.02f * skull.H - pose.PitchF * 0.07f * skull.H;
            float fx = skull.CenterX + pose.YawF * 0.14f * skull.W;

            if (hasGlasses && Mathf.Abs(world.y - eyeY) < 0.09f && Mathf.Abs(world.x - fx) < skull.W * 0.42f) return Part.Glasses;
            if (hatOn && world.y < skull.Y + skull.H * 0.3f && world.y > skull.Y - 0.25f && Mathf.Abs(world.x - skull.CenterX) < skull.W * 0.6f) return Part.Hat;
            var d = new Vector2((world.x - skull.CenterX) / (skull.W * 0.55f), (world.y - skull.CenterY) / (skull.H * 0.55f));
            if (d.sqrMagnitude <= 1f) return Part.Head;
            if (world.y > skull.Bottom && Mathf.Abs(world.x - skull.CenterX) < 1.3f) return Part.Body;
            return Part.Background;
        }
    }
}
