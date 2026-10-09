using System.Collections.Generic;
using System.Globalization;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// Looking through the tripod camera at the customer on the stool (design doc §15-§22).
    /// Drag the head to pose it, drag the shoulders to slide them on the stool, drag the
    /// background to move the camera, scroll to zoom. Click glasses or a hat to remove it.
    /// </summary>
    public sealed class CameraView : Overlay
    {
        const float VfX = 150f, VfY = 90f, VfW = 660f, VfH = 880f;
        const float StepPixels = 38f;

        enum DragMode { None, Head, Body, Pan }

        readonly Visit _visit;
        readonly PhotoSession _session;
        readonly PortraitView _vf;
        readonly VisualElement _flash;
        readonly Label _zoomLabel;
        readonly Label _bgLabel;
        readonly Label _tableLabel;
        readonly Label _statusLabel;
        readonly VisualElement _bubble;
        readonly Label _bubbleText;
        readonly Button _faceBtn;
        readonly Button _eyesBtn;
        readonly Button _glassesBtn;
        readonly Button _hatBtn;
        readonly VisualElement _swatches;
        readonly VisualElement _kept;
        readonly Dictionary<Backdrop, Button> _swatchButtons = new Dictionary<Backdrop, Button>();
        string _keptKey = "";

        DragMode _mode;
        PortraitView.Part _pressPart;
        Vector2 _pressPos;
        HeadPose _poseAtPress;
        float _panXAtPress, _panYAtPress, _seatApplied;
        bool _moved;
        int _pointerId = -1;
        float _flashT;

        PhotoSubject Subject => _session.Subject;

        public CameraView(GameRoot game, Visit visit) : base(game, "le-overlay--camera")
        {
            _visit = visit;
            _session = S.ActivePhoto;

            // ---------------- viewfinder
            var vfBox = Ui.El("le-viewfinder", Root).At(VfX - 14f, VfY - 14f, VfW + 28f, VfH + 28f);
            _vf = new PortraitView("le-viewfinder__image") { Live = _session };
            _vf.At(14f, 14f, VfW, VfH);
            vfBox.Add(_vf);
            vfBox.Add(new Painted(DrawGuides, "le-viewfinder__guides").At(14f, 14f, VfW, VfH));
            _flash = Ui.Deco("le-flash", vfBox).At(14f, 14f, VfW, VfH);
            _flash.style.opacity = 0f;
            _zoomLabel = Ui.Text("", "le-vf-label le-vf-label--tl", vfBox);
            _zoomLabel.pickingMode = PickingMode.Ignore;
            _bgLabel = Ui.Text("", "le-vf-label le-vf-label--bl", vfBox);
            _bgLabel.pickingMode = PickingMode.Ignore;
            _tableLabel = Ui.Text("", "le-camera__table", Root).At(VfX, VfY + VfH + 20f, VfW, 40f);

            _vf.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _vf.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _vf.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _vf.RegisterCallback<WheelEvent>(e =>
            {
                _session.ZoomBy(e.delta.y > 0f ? -0.25f : 0.25f);
                e.StopPropagation();
            });

            _bubble = Ui.El("le-bubble le-bubble--camera", Root).At(880f, 10f, 940f, -1f);
            _bubbleText = Ui.Text("", "le-bubble__text", _bubble);
            _bubble.RegisterCallback<ClickEvent>(_ => S.SkipLine(_visit.Id));

            // ---------------- controls
            var panel = Ui.El("le-cam-panel", Root).At(880f, 104f, 960f, 970f);
            Ui.Text($"CAMERA  -  {S.Camera.Name}", "le-h2", panel);
            var order = _visit.Order;
            string job = order != null ? $"{_visit.Customer.DisplayName}  -  {S.Country(order.CountryId).Spec.Name} passport" : _visit.Customer.DisplayName;
            Ui.Text(job, "le-cam-panel__job", panel);
            _statusLabel = Ui.Text("", "le-muted", panel);

            Ui.Text("BACKDROP", "le-cam-panel__section", panel);
            _swatches = Ui.El("le-row", panel);
            foreach (Backdrop b in System.Enum.GetValues(typeof(Backdrop)))
            {
                var bb = b;
                var btn = Ui.Btn(Labels.Of(b), () => _session.PullBackdrop(bb), "le-btn--swatch", _swatches);
                btn.style.backgroundColor = Looks.Of(b);
                _swatchButtons[b] = btn;
            }

            Ui.Text("FRAMING", "le-cam-panel__section", panel);
            var framing = Ui.El("le-row", panel);
            Ui.Btn("ZOOM -", () => _session.ZoomBy(-0.25f), "le-btn--small", framing);
            Ui.Btn("ZOOM +", () => _session.ZoomBy(0.25f), "le-btn--small", framing);
            Ui.Btn("▲", () => _session.PanBy(0f, -0.08f), "le-btn--small le-btn--icon", framing);
            Ui.Btn("▼", () => _session.PanBy(0f, 0.08f), "le-btn--small le-btn--icon", framing);
            Ui.Btn("◀", () => _session.PanBy(-0.08f, 0f), "le-btn--small le-btn--icon", framing);
            Ui.Btn("▶", () => _session.PanBy(0.08f, 0f), "le-btn--small le-btn--icon", framing);
            Ui.Btn("RESET", () =>
            {
                _session.Rig.Zoom = 1f;
                _session.Rig.PanX = 0f;
                _session.Rig.PanY = CameraRig.DefaultPanY;
            }, "le-btn--small le-btn--ghost", framing);
            Ui.Text("Drag the HEAD to turn it  ·  drag the SHOULDERS to slide them  ·  drag the BACKGROUND to move the camera  ·  scroll to zoom  ·  click glasses/hat to take them off", "le-cam-panel__help", panel);

            Ui.Text("SAY", "le-cam-panel__section", panel);
            var say1 = Ui.El("le-row le-row--wrap", panel);
            Ui.Btn("LOOK AT THE CAMERA", () => Say(PhotoCommand.LookAtCamera), "le-btn--say", say1);
            _faceBtn = Ui.Btn("STOP SMILING", () => Say(PhotoCommand.NeutralFace), "le-btn--say", say1);
            _eyesBtn = Ui.Btn("OPEN YOUR EYES", () => Say(PhotoCommand.OpenEyes), "le-btn--say", say1);
            Ui.Btn("HOLD STILL", () => Say(PhotoCommand.HoldStill), "le-btn--say", say1);
            _glassesBtn = Ui.Btn("GLASSES OFF", () => Say(PhotoCommand.RemoveGlasses), "le-btn--say", say1);
            _hatBtn = Ui.Btn("HAT OFF", () => Say(PhotoCommand.RemoveHat), "le-btn--say", say1);

            var shutterRow = Ui.El("le-row le-row--center le-cam-panel__shutter-row", panel);
            Ui.Btn("SHUTTER", Shutter, "le-shutter", shutterRow);
            Ui.Text("(SPACE)", "le-muted", shutterRow);

            Ui.Text("KEPT PHOTOS", "le-cam-panel__section", panel);
            _kept = Ui.El("le-row le-cam-panel__kept", panel);

            var bottom = Ui.El("le-row le-row--end le-cam-panel__bottom", panel);
            Ui.Btn("HANDBOOK (H)", () => Game.ToggleHandbook(order?.CountryId), "le-btn--ghost", bottom);
            Ui.Btn("STEP AWAY (ESC)", Close, "le-btn--ghost", bottom);
            Ui.Btn("DONE - THANK YOU", Done, "le-btn--primary", bottom);
        }

        void Say(PhotoCommand cmd)
        {
            Subject.Command(cmd);
            Sfx.Play(SfxId.Pop, 0.5f);
        }

        public void Shutter()
        {
            if (Game.Top != this) return;
            if (_session.PressShutter()) Sfx.Play(SfxId.Shutter, 0.9f);
        }

        void Done()
        {
            if (S.FinishPhotoSitting(_visit.Id)) Close();
            else Game.Toasts.Show("Keep at least one photo first (shoot, then KEEP).", "bad");
        }

        public override void OnClosed()
        {
            if (S.ActivePhoto == _session) S.EndPhotoSession();
        }

        public override bool OnEscape()
        {
            if (Game.Top != this) return false;
            Close();
            return true;
        }

        public override void Tick(float dt)
        {
            if (S.ActivePhoto != _session || _visit.Phase == VisitPhase.Gone)
            {
                Close();
                return;
            }
            _vf.Refresh();

            var cap = _session.JustCaptured;
            if (cap != null)
            {
                _session.TakeJustCaptured();
                _flashT = 0.35f;
                Game.Open(new ReviewView(Game, cap));
            }
            if (_flashT > 0f)
            {
                _flashT -= dt;
                _flash.style.opacity = Mathf.Clamp01(_flashT / 0.35f);
            }

            var rig = _session.Rig;
            _zoomLabel.SetTextIfChanged($"● LIVE   {rig.Zoom.ToString("0.00", CultureInfo.InvariantCulture)}x");
            _bgLabel.SetTextIfChanged(rig.SwappingBackdrop ? "pulling down the backdrop..." : $"BACKDROP: {Labels.Of(rig.Backdrop)}");
            foreach (var kv in _swatchButtons) kv.Value.EnableInClassList("le-btn--selected", kv.Key == (rig.PendingBackdrop ?? rig.Backdrop));

            var subj = Subject;
            _faceBtn.text = subj.Expression == Expression.Smile || subj.Expression == Expression.Grin ? "STOP SMILING" : "RELAX YOUR FACE";
            _glassesBtn.SetEnabled(subj.HasGlasses);
            _hatBtn.SetEnabled(subj.HatOn && subj.Customer.Look.Hat != HatStyle.None);
            _eyesBtn.SetEnabled(subj.Customer.Behavior.LidDroop > 0f || subj.Expression == Expression.Tired || subj.Pose.Pitch < 0);

            var table = new List<string>();
            foreach (var g in subj.RemovedGlasses) table.Add(g == GlassesStyle.Sunglasses ? "sunglasses" : "glasses");
            if (subj.HatRemoved) table.Add(subj.Customer.Look.Hat.ToString().ToLowerInvariant());
            _tableLabel.SetTextIfChanged(table.Count > 0 ? "ON THE SIDE TABLE: " + string.Join(", ", table) : "");

            float mood = _visit.PatienceFraction;
            _statusLabel.SetTextIfChanged(mood > 0.6f ? "They seem fine." : mood > 0.35f ? "They're getting restless." : mood > 0.15f ? "They're annoyed." : "They're about to walk out.");

            var line = _visit.Line;
            _bubble.Show(line != null);
            if (line != null) _bubbleText.SetTextIfChanged(line.Text);

            string keptKey = "";
            var kept = S.Photos.ForCustomer(_visit.Customer.Id);
            foreach (var k in kept) keptKey += k.Id + ":" + k.Crop + ",";
            if (keptKey != _keptKey)
            {
                _keptKey = keptKey;
                _kept.Clear();
                if (kept.Count == 0) Ui.Text("none yet", "le-muted", _kept);
                foreach (var k in kept)
                {
                    var thumb = new PortraitView("le-thumb") { Capture = k };
                    thumb.pickingMode = PickingMode.Ignore;
                    _kept.Add(thumb);
                    thumb.View = k.Crop;
                }
            }
        }

        // ------------------------------------------------------------------ pointer

        float PxPerUnit => VfH / PortraitGeometry.FrameWorld(_session.Rig.Zoom, _session.Rig.PanX, _session.Rig.PanY).H;

        void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0 || Game.Top != this) return;
            var local = (Vector2)e.localPosition;
            _pressPart = _vf.HitTest(local);
            _pressPos = local;
            _moved = false;
            _pointerId = e.pointerId;
            _poseAtPress = Subject.Target;
            _panXAtPress = _session.Rig.PanX;
            _panYAtPress = _session.Rig.PanY;
            _seatApplied = 0f;
            switch (_pressPart)
            {
                case PortraitView.Part.Head:
                case PortraitView.Part.Glasses:
                case PortraitView.Part.Hat:
                    _mode = DragMode.Head;
                    break;
                case PortraitView.Part.Body:
                    _mode = DragMode.Body;
                    break;
                default:
                    _mode = DragMode.Pan;
                    break;
            }
            _vf.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            if (_mode == DragMode.None || e.pointerId != _pointerId) return;
            var delta = (Vector2)e.localPosition - _pressPos;
            if (!_moved && delta.sqrMagnitude > 36f)
            {
                _moved = true;
                if (_mode == DragMode.Head) Subject.BeginDrag();
            }
            if (!_moved) return;

            switch (_mode)
            {
                case DragMode.Head:
                {
                    int dy = Mathf.RoundToInt(delta.x / StepPixels);
                    int dp = Mathf.RoundToInt(-delta.y / StepPixels);
                    Subject.DragTo(_poseAtPress.Yaw + dy, _poseAtPress.Pitch + dp);
                    break;
                }
                case DragMode.Body:
                {
                    float want = delta.x / PxPerUnit;
                    Subject.NudgeSeat(want - _seatApplied);
                    _seatApplied = want;
                    break;
                }
                case DragMode.Pan:
                    _session.Rig.PanX = _panXAtPress - delta.x / PxPerUnit;
                    _session.Rig.PanY = _panYAtPress - delta.y / PxPerUnit;
                    _session.Rig.ClampTo(_session.Camera);
                    break;
            }
        }

        void OnPointerUp(PointerUpEvent e)
        {
            if (e.pointerId != _pointerId) return;
            if (_vf.HasPointerCapture(e.pointerId)) _vf.ReleasePointer(e.pointerId);
            if (_mode == DragMode.Head && _moved) Subject.EndDrag();
            if (_mode == DragMode.Body && _moved) Subject.EndNudge();
            if (!_moved)
            {
                if (_pressPart == PortraitView.Part.Glasses) Say(PhotoCommand.RemoveGlasses);
                else if (_pressPart == PortraitView.Part.Hat) Say(PhotoCommand.RemoveHat);
            }
            _mode = DragMode.None;
            _pointerId = -1;
        }

        static void DrawGuides(Painter2D p, Rect r)
        {
            var c = new Color(1f, 1f, 1f, 0.55f);
            float l = 40f;
            Draw.Line(p, new Vector2(10f, 10f), new Vector2(10f + l, 10f), c, 3f);
            Draw.Line(p, new Vector2(10f, 10f), new Vector2(10f, 10f + l), c, 3f);
            Draw.Line(p, new Vector2(r.width - 10f, 10f), new Vector2(r.width - 10f - l, 10f), c, 3f);
            Draw.Line(p, new Vector2(r.width - 10f, 10f), new Vector2(r.width - 10f, 10f + l), c, 3f);
            Draw.Line(p, new Vector2(10f, r.height - 10f), new Vector2(10f + l, r.height - 10f), c, 3f);
            Draw.Line(p, new Vector2(10f, r.height - 10f), new Vector2(10f, r.height - 10f - l), c, 3f);
            Draw.Line(p, new Vector2(r.width - 10f, r.height - 10f), new Vector2(r.width - 10f - l, r.height - 10f), c, 3f);
            Draw.Line(p, new Vector2(r.width - 10f, r.height - 10f), new Vector2(r.width - 10f, r.height - 10f - l), c, 3f);
            var faint = new Color(1f, 1f, 1f, 0.16f);
            for (int i = 1; i < 3; i++)
            {
                Draw.Line(p, new Vector2(r.width * i / 3f, 0f), new Vector2(r.width * i / 3f, r.height), faint, 1f);
                Draw.Line(p, new Vector2(0f, r.height * i / 3f), new Vector2(r.width, r.height * i / 3f), faint, 1f);
            }
            Draw.Line(p, new Vector2(r.width * 0.5f - 12f, r.height * 0.5f), new Vector2(r.width * 0.5f + 12f, r.height * 0.5f), c, 2f);
            Draw.Line(p, new Vector2(r.width * 0.5f, r.height * 0.5f - 12f), new Vector2(r.width * 0.5f, r.height * 0.5f + 12f), c, 2f);
        }
    }
}
