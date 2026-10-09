using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;
using Box = LegitEnough.Sim.Box;

namespace LegitEnough.Game
{
    /// <summary>
    /// The photo just taken (design doc §23): KEEP, RETAKE or CROP. No score — the player
    /// has to look: eyes open? expression? background? head straight? glasses?
    /// </summary>
    public sealed class ReviewView : Overlay
    {
        readonly PhotoCapture _photo;
        readonly PortraitView _image;
        readonly Painted _cropFrame;

        public ReviewView(GameRoot game, PhotoCapture photo) : base(game, "le-overlay--dark")
        {
            _photo = photo;
            Ui.Text("THE PHOTO", "le-h1", Root).At(140, 40, 800, 60);
            Ui.Text("Look at it properly. Eyes open? Right expression? Right background? Head straight? Glasses?", "le-text", Root).At(140, 100, 900, 60);

            var frame = Ui.El("le-review__frame", Root).At(140, 170, 600, 800);
            _image = new PortraitView("le-review__image") { Capture = photo };
            _image.At(0, 0, 600, 800);
            frame.Add(_image);
            _cropFrame = new Painted(DrawCrop, "le-review__crop").At(0, 0, 600, 800);
            frame.Add(_cropFrame);

            var side = Ui.El("le-review__side", Root).At(820, 170, 900, 800);
            Ui.Text("PASSPORT CROP (35 x 45)", "le-cam-panel__section", side);
            var preview = new PortraitView("le-review__preview") { Capture = photo };
            preview.At(0, 40, 280, 360);
            side.Add(preview);
            preview.View = photo.Crop;
            Ui.Text(photo.Crop.W >= 0.99f ? "Not cropped yet - the whole frame will be used." : "Cropped.", "le-muted", side).At(0, 410, 600, 30);

            var row = Ui.El("le-row", side).At(0, 520, 900, 90);
            Ui.Btn("RETAKE", Close, "le-btn--ghost le-btn--big", row);
            Ui.Btn("CROP", () =>
            {
                Close();
                Game.Open(new CropView(Game, photo));
            }, "le-btn--big", row);
            Ui.Btn("KEEP", () =>
            {
                S.KeepPhoto(photo);
                Close();
            }, "le-btn--primary le-btn--big", row);
            Ui.Text("Kept photos can be inserted into the document on the computer.", "le-muted", side).At(0, 620, 800, 40);
        }

        void DrawCrop(Painter2D p, Rect r)
        {
            var c = _photo.Crop;
            var rect = new Rect(c.X * r.width, c.Y * r.height, c.W * r.width, c.H * r.height);
            var dim = new Color(0f, 0f, 0f, 0.45f);
            Draw.Rect(p, new Rect(0f, 0f, r.width, rect.yMin), dim);
            Draw.Rect(p, new Rect(0f, rect.yMax, r.width, r.height - rect.yMax), dim);
            Draw.Rect(p, new Rect(0f, rect.yMin, rect.xMin, rect.height), dim);
            Draw.Rect(p, new Rect(rect.xMax, rect.yMin, r.width - rect.xMax, rect.height), dim);
            Draw.RoundRectStroke(p, rect, 1f, new Color(1f, 1f, 1f, 0.8f), 2f);
        }
    }

    /// <summary>
    /// Manual crop with a fixed 35:45 frame (design doc §22). A ruler on the crop edge
    /// measures 0-100% of the photo height; nothing snaps into place.
    /// </summary>
    public sealed class CropView : Overlay
    {
        const float W = 600f, H = 800f;

        readonly PhotoCapture _photo;
        readonly VisualElement _area;
        readonly VisualElement _rect;
        readonly Painted _shade;
        readonly Painted _ruler;
        readonly PortraitView _preview;
        readonly Label[] _rulerLabels = new Label[4];
        Box _crop;
        bool _showRuler = true;

        enum Mode { None, Move, Resize }
        Mode _mode;
        Vector2 _press;
        Box _cropAtPress;
        int _corner;

        public CropView(GameRoot game, PhotoCapture photo) : base(game, "le-overlay--dark")
        {
            _photo = photo;
            _crop = photo.Crop;
            Ui.Text("CROP", "le-h1", Root).At(140, 40, 800, 60);
            Ui.Text("Drag the frame to move it. Drag a corner to resize. The handbook says how much of the photo the head should fill.", "le-text", Root).At(140, 100, 1200, 60);

            _area = Ui.El("le-crop__area", Root).At(140, 170, W, H);
            var img = new PortraitView("le-crop__image") { Capture = photo };
            img.pickingMode = PickingMode.Ignore;
            img.At(0, 0, W, H);
            _area.Add(img);
            _shade = new Painted(DrawShade).At(0, 0, W, H);
            _area.Add(_shade);
            _rect = Ui.El("le-crop__rect", _area);
            _rect.style.position = Position.Absolute;
            _rect.pickingMode = PickingMode.Ignore;
            _ruler = new Painted(DrawRuler, "le-crop__ruler");
            _ruler.style.position = Position.Absolute;
            _area.Add(_ruler);
            foreach (var corner in new[] { "tl", "tr", "bl", "br" }) Ui.Deco("le-crop__handle le-crop__handle--" + corner, _rect);
            for (int i = 0; i < 4; i++)
            {
                _rulerLabels[i] = Ui.Text((50 + i * 10) + "%", "le-crop__ruler-label", _area);
                _rulerLabels[i].style.position = Position.Absolute;
                _rulerLabels[i].pickingMode = PickingMode.Ignore;
            }

            _area.RegisterCallback<PointerDownEvent>(OnDown);
            _area.RegisterCallback<PointerMoveEvent>(OnMove);
            _area.RegisterCallback<PointerUpEvent>(OnUp);

            var side = Ui.El("le-review__side", Root).At(860, 170, 900, 800);
            Ui.Text("RESULT", "le-cam-panel__section", side);
            _preview = new PortraitView("le-review__preview") { Capture = photo };
            _preview.At(0, 40, 280, 360);
            side.Add(_preview);

            var tools = Ui.El("le-row", side).At(0, 430, 900, 70);
            Ui.Btn("RULER", () =>
            {
                _showRuler = !_showRuler;
                Apply();
            }, "le-btn--small", tools);
            Ui.Btn("RESET", () =>
            {
                _crop = PortraitGeometry.FullCrop();
                Apply();
            }, "le-btn--small le-btn--ghost", tools);

            var row = Ui.El("le-row", side).At(0, 540, 900, 90);
            Ui.Btn("CANCEL", Close, "le-btn--ghost le-btn--big", row);
            Ui.Btn("DONE", () =>
            {
                _photo.Crop = _crop;
                Close();
                Game.Open(new ReviewView(Game, _photo));
            }, "le-btn--primary le-btn--big", row);
            Apply();
        }

        void Apply()
        {
            _rect.style.left = _crop.X * W;
            _rect.style.top = _crop.Y * H;
            _rect.style.width = _crop.W * W;
            _rect.style.height = _crop.H * H;
            _ruler.Show(_showRuler);
            _ruler.style.left = _crop.Right * W + 4f;
            _ruler.style.top = _crop.Y * H;
            _ruler.style.width = 56f;
            _ruler.style.height = _crop.H * H;
            _ruler.MarkDirtyRepaint();
            for (int i = 0; i < 4; i++)
            {
                _rulerLabels[i].Show(_showRuler);
                _rulerLabels[i].style.left = _crop.Right * W + 38f;
                _rulerLabels[i].style.top = _crop.Y * H + _crop.H * H * (0.5f + i * 0.1f) - 12f;
            }
            _shade.MarkDirtyRepaint();
            _preview.View = _crop;
            _preview.Refresh();
        }

        void OnDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            var local = (Vector2)e.localPosition;
            var r = new Rect(_crop.X * W, _crop.Y * H, _crop.W * W, _crop.H * H);
            _press = local;
            _cropAtPress = _crop;
            _corner = -1;
            var corners = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) };
            for (int i = 0; i < 4; i++)
                if ((corners[i] - local).sqrMagnitude < 26f * 26f) _corner = i;
            if (_corner >= 0) _mode = Mode.Resize;
            else if (r.Contains(local)) _mode = Mode.Move;
            else return;
            _area.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            if (_mode == Mode.None) return;
            var local = (Vector2)e.localPosition;
            var d = local - _press;
            var c = _cropAtPress;
            if (_mode == Mode.Move)
            {
                _crop = new Box(c.X + d.x / W, c.Y + d.y / H, c.W, c.H).ClampInside(new Box(0f, 0f, 1f, 1f));
            }
            else
            {
                // Anchor the opposite corner; height follows the vertical drag, width keeps 35:45.
                bool top = _corner < 2;
                bool left = _corner % 2 == 0;
                float anchorX = left ? c.Right : c.X;
                float anchorY = top ? c.Bottom : c.Y;
                float hNew = Mathf.Clamp(top ? c.H - d.y / H : c.H + d.y / H, 0.12f, PortraitGeometry.FullCrop().H);
                float wNew = PortraitGeometry.CropWidthFor(hNew);
                float x = left ? anchorX - wNew : anchorX;
                float y = top ? anchorY - hNew : anchorY;
                _crop = new Box(x, y, wNew, hNew).ClampInside(new Box(0f, 0f, 1f, 1f));
            }
            Apply();
        }

        void OnUp(PointerUpEvent e)
        {
            if (_area.HasPointerCapture(e.pointerId)) _area.ReleasePointer(e.pointerId);
            _mode = Mode.None;
        }

        void DrawShade(Painter2D p, Rect r)
        {
            var rect = new Rect(_crop.X * r.width, _crop.Y * r.height, _crop.W * r.width, _crop.H * r.height);
            var dim = new Color(0f, 0f, 0f, 0.5f);
            Draw.Rect(p, new Rect(0f, 0f, r.width, rect.yMin), dim);
            Draw.Rect(p, new Rect(0f, rect.yMax, r.width, r.height - rect.yMax), dim);
            Draw.Rect(p, new Rect(0f, rect.yMin, rect.xMin, rect.height), dim);
            Draw.Rect(p, new Rect(rect.xMax, rect.yMin, r.width - rect.xMax, rect.height), dim);
        }

        static void DrawRuler(Painter2D p, Rect r)
        {
            Draw.Rect(p, new Rect(0f, 0f, 30f, r.height), new Color(0.95f, 0.9f, 0.6f, 0.92f));
            var ink = new Color(0.15f, 0.12f, 0.1f);
            for (int i = 0; i <= 20; i++)
            {
                float y = r.height * i / 20f;
                float len = i % 2 == 0 ? 16f : 8f;
                Draw.Line(p, new Vector2(0f, y), new Vector2(len, y), ink, i % 2 == 0 ? 2f : 1f);
            }
            // Bold marks at 50/60/70/80% where most rules live.
            for (int pct = 50; pct <= 80; pct += 10)
            {
                float y = r.height * pct / 100f;
                Draw.Line(p, new Vector2(0f, y), new Vector2(30f, y), new Color(0.7f, 0.15f, 0.1f), 2.5f);
            }
        }
    }
}
