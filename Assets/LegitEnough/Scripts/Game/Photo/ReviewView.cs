using System.Globalization;
using System.Runtime.CompilerServices;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Box = LegitEnough.Sim.Box;

namespace LegitEnough.Game
{
    /// <summary>
    /// Which rule a photo is cropped against: the job's country, as the PLAYER'S handbook has it
    /// (an outdated section shows the old rule; no section means the rule is unknown). Shared by
    /// THE PHOTO, CROP and the tutorial coach, so all three say the same thing.
    /// </summary>
    public static class CropWords
    {
        /// <summary>The country of the job this photo is for, or null.</summary>
        public static string CountryOf(GameSession s, PhotoCapture photo)
        {
            if (s == null || photo == null) return null;
            Order order = null;
            var session = s.ActivePhoto;
            if (session != null) order = s.Orders.Get(session.OrderId);
            if (order == null || order.CustomerId != photo.CustomerId)
            {
                order = null;
                foreach (var o in s.Orders.All)
                    if (o.CustomerId == photo.CustomerId) order = o; // the newest job for this face
            }
            return order?.CountryId;
        }

        public static string NameOf(GameSession s, string countryId)
        {
            var c = s?.Country(countryId);
            return c != null ? c.Spec.Name : "";
        }

        public static int Pct(float fraction) => Mathf.RoundToInt(fraction * 100f);

        /// <summary>The head size as shown: rounded, but never rounded across the rule's edge.</summary>
        public static int ShownCoverage(in FramingResult f)
        {
            int pct = Pct(f.Coverage);
            if (!f.HasRule) return pct;
            int min = Pct(f.RuleMin), max = Pct(f.RuleMax);
            if (f.Size == FramingSize.TooSmall && pct >= min) pct = min - 1;
            else if (f.Size == FramingSize.TooLarge && pct <= max) pct = max + 1;
            else if (f.Size == FramingSize.Ok) pct = Mathf.Clamp(pct, min, max);
            return pct;
        }

        public static string Range(in FramingResult f) => $"{Pct(f.RuleMin)}-{Pct(f.RuleMax)}%";

        /// <summary>The pinned tag: "FRANCE - HEAD 60-70%".</summary>
        public static string TagTitle(in FramingResult f, string country)
        {
            string name = string.IsNullOrEmpty(country) ? "THIS JOB" : country.ToUpperInvariant();
            return f.HasRule ? $"{name} - HEAD {Range(f)}" : $"{name} - RULE UNKNOWN";
        }

        public static string TagNote(in FramingResult f, string country) =>
            f.HasRule ? "top of hair or hat to chin"
            : string.IsNullOrEmpty(country) ? "no job on file for this face" : $"no {country} section in the handbook";

        public static string SizeLine(in FramingResult f, string country)
        {
            string name = string.IsNullOrEmpty(country) ? "THE RULE" : country.ToUpperInvariant();
            string head = $"HEAD SIZE {ShownCoverage(f)}%";
            if (!f.HasRule) return $"{head}  ·  {name}: RULE UNKNOWN";
            // The fix, not just the verdict: a smaller frame makes a bigger head.
            string verdict = f.Size == FramingSize.TooSmall ? " - too small: drag a corner IN"
                : f.Size == FramingSize.TooLarge ? " - too big: drag a corner OUT" : "";
            return $"{head}  ·  {name} NEEDS {Range(f)}{verdict}";
        }

        /// <summary>How far the crown sits off the dashed line, as the ruler shows it: "4% above".</summary>
        public static string OffLine(in FramingResult f) =>
            $"{Mathf.Max(1, Pct(Mathf.Abs(f.CrownReading)))}% {(f.CrownReading < 0f ? "above" : "below")} the line";

        /// <summary>
        /// "On the line" only when it visibly is; inside the scorer's slack it says how far off it is,
        /// so the dashed line and the readout never disagree. Failing rows say which way to slide.
        /// </summary>
        public static string TopLine(in FramingResult f) => f.Top switch
        {
            FramingTop.CutOff => "TOP OF HEAD CUT OFF - slide the frame UP",
            FramingTop.TooClose => "TOP OF HEAD: too high - slide the frame UP",
            FramingTop.TooFar => "TOP OF HEAD: too low - slide the frame DOWN",
            _ => f.CrownOnLine ? "TOP OF HEAD: on the line" : $"TOP OF HEAD: close enough ({OffLine(f)})",
        };

        public static string SideLine(in FramingResult f) => f.Side switch
        {
            FramingSide.SlideLeft => "NOT CENTRED: slide the frame left",
            FramingSide.SlideRight => "NOT CENTRED: slide the frame right",
            _ => "CENTRED",
        };

        public const string ChinCutLine = "CHIN CUT OFF";
        public const string LowResLine = "LOW RESOLUTION - zoom in on the camera next time";

        /// <summary>One line for THE PHOTO: "Head 65% (France 60-70%) · centred · top of head OK".</summary>
        public static string Summary(in FramingResult f, string country)
        {
            string name = string.IsNullOrEmpty(country) ? "rule" : country;
            string size = f.HasRule
                ? $"Head {ShownCoverage(f)}% ({name} {Range(f)})"
                    + (f.Size == FramingSize.TooSmall ? ": too small" : f.Size == FramingSize.TooLarge ? ": too big" : "")
                : $"Head {ShownCoverage(f)}% ({name} rule unknown)";
            string side = f.Side == FramingSide.Centred ? "centred" : f.Side == FramingSide.SlideLeft ? "off centre (head too far left)" : "off centre (head too far right)";
            string top = f.Top switch
            {
                FramingTop.CutOff => "TOP OF HEAD CUT OFF",
                FramingTop.TooClose => "top of head too high",
                FramingTop.TooFar => "top of head too low",
                _ => "top of head OK",
            };
            string s = $"{size} · {side} · {top}";
            if (f.ChinCut) s += " · CHIN CUT OFF";
            if (f.LowRes) s += " · low resolution";
            return s;
        }

        /// <summary>
        /// THE PHOTO's stamp: the one thing that matters most about the framing, in the order a
        /// fix works (cut first, then size, then placing). Null when there's no verdict to give.
        /// </summary>
        public static string Verdict(in FramingResult f)
        {
            if (f.CrownCut) return "TOP OF HEAD CUT OFF";
            if (f.ChinCut) return "CHIN CUT OFF";
            if (f.Size == FramingSize.TooSmall) return "HEAD TOO SMALL";
            if (f.Size == FramingSize.TooLarge) return "HEAD TOO BIG";
            if (f.Top == FramingTop.TooClose) return "TOP OF HEAD TOO HIGH";
            if (f.Top == FramingTop.TooFar) return "TOP OF HEAD TOO LOW";
            if (!f.Centred) return "NOT CENTRED";
            if (!f.HasRule) return null;
            return f.LowRes ? "FRAMING OK - LOW RES" : "FRAMING OK";
        }
    }

    /// <summary>
    /// The photo just taken (design doc §23): KEEP, RETAKE or CROP. No score — the player
    /// has to look: eyes open? expression? background? head straight? glasses? head big enough?
    /// </summary>
    public sealed class ReviewView : Overlay
    {
        readonly PhotoCapture _photo;
        readonly PortraitView _image;
        readonly Painted _cropFrame;
        readonly string _countryId;
        readonly PhotoRules _rules;
        readonly FramingResult _check;

        /// <summary>The shot under review (read by the tutorial coach).</summary>
        public PhotoCapture Photo => _photo;
        /// <summary>True while the photo still has the whole frame (no crop yet).</summary>
        public bool Uncropped => IsUncropped(_photo);
        /// <summary>The shared framing check of the photo's crop against the handbook's rule.</summary>
        public FramingResult Check => _check;
        /// <summary>The handbook's photo rules for this job, or null when the section isn't owned.</summary>
        public PhotoRules Rules => _rules;
        public string CountryId => _countryId;

        /// <summary>
        /// The photo still has the camera's whole frame: never through CROP's DONE and its crop
        /// is exactly the full frame. (A near-full crop the player chose is a crop.)
        /// </summary>
        public static bool IsUncropped(PhotoCapture photo)
        {
            if (photo == null || CropView.WasCropped(photo)) return false;
            var c = photo.Crop;
            var full = PortraitGeometry.FullCrop();
            return Mathf.Abs(c.X - full.X) < 1e-4f && Mathf.Abs(c.Y - full.Y) < 1e-4f
                && Mathf.Abs(c.W - full.W) < 1e-4f && Mathf.Abs(c.H - full.H) < 1e-4f;
        }

        public ReviewView(GameRoot game, PhotoCapture photo) : base(game, "le-overlay--camera")
        {
            _photo = photo;
            _countryId = CropWords.CountryOf(S, photo);
            _rules = Framing.HandbookPhotoRules(S, _countryId);
            _check = Framing.Check(photo, _rules);
            bool uncropped = IsUncropped(photo);
            string country = CropWords.NameOf(S, _countryId);

            Ui.Text("THE PHOTO", "le-h1", Root).At(140, 40, 800, 60);
            Ui.Text("Look at it properly. Eyes open? Right expression? Right background? Head straight? Glasses? Head big enough?", "le-text", Root).At(140, 100, 1400, 60);

            // The border sits outside the 600 x 800 photo, as on CROP.
            var frame = Ui.El("le-review__frame", Root).At(140, 170, 608, 808);
            _image = new PortraitView("le-review__image") { Capture = photo };
            _image.At(0, 0, 600, 800);
            frame.Add(_image);
            _cropFrame = new Painted(DrawCrop, "le-review__crop").At(0, 0, 600, 800);
            frame.Add(_cropFrame);

            var side = Ui.El("le-review__side", Root).At(820, 170, 1000, 800);
            Ui.Text("ON THE PASSPORT (35 x 45)", "le-cam-panel__section", side);
            var preview = new PortraitView("le-review__preview") { Capture = photo };
            preview.At(0, 40, 280, 360);
            side.Add(preview);
            preview.View = photo.Crop;
            var hb = Ui.Btn("HANDBOOK (H)", () => Game.ToggleHandbook(_countryId), "le-btn--small le-btn--ghost", side).At(316, 40);
            hb.name = "review-handbook";

            // What the framing check says, as a stamp beside the preview and one line under it. The
            // primary button is the next right move: CROP while the framing is wrong, KEEP once it's
            // right (or the handbook has no rule to judge it by), RETAKE if no crop can save it.
            string summaryText, cls, verdict = null, verdictCls = null, next = null;
            string primary = "keep";
            if (uncropped && (!_check.HasRule || _check.Size == FramingSize.TooSmall))
            {
                summaryText = "NOT CROPPED. The whole frame goes on the passport - the head will be too small. CROP it.";
                cls = "le-review__summary--warn";
                verdict = "NOT CROPPED";
                verdictCls = "warn";
                primary = "crop";
            }
            else if (uncropped && _check.Size == FramingSize.TooLarge)
            {
                // Cropping only ever makes the head bigger.
                summaryText = $"NOT CROPPED, and the head is already too big: {CropWords.Summary(_check, country)}. Zoom out and RETAKE.";
                cls = "le-review__summary--bad";
                verdict = "HEAD TOO BIG";
                verdictCls = "bad";
                next = "Even uncropped. RETAKE it.";
                primary = "retake";
            }
            else
            {
                summaryText = (uncropped ? "NOT CROPPED, but the whole frame fits: " : "") + CropWords.Summary(_check, country);
                bool ok = _check.FramingOk && !_check.LowRes;
                cls = !_check.NoCut ? "le-review__summary--bad" : ok ? "le-review__summary--ok" : "le-review__summary--warn";
                verdict = CropWords.Verdict(_check);
                verdictCls = !_check.NoCut || (_check.HasRule && !_check.FramingOk) ? "bad" : ok ? "ok" : "warn";
                if (_check.HasRule && !_check.FramingOk)
                {
                    primary = "crop";
                    next = "CROP it again.";
                }
            }
            var summary = Ui.Text(summaryText, "le-review__summary " + cls, side);
            summary.At(0, 416, 960, 90);
            summary.name = "review-summary";
            if (verdict != null)
            {
                var stamp = Ui.El("le-review__verdict le-review__verdict--" + verdictCls, side).At(316, 120, -1, -1);
                stamp.name = "review-verdict";
                Ui.Text(verdict, "le-review__verdict-title", stamp);
                if (next != null) Ui.Text(next, "le-review__verdict-next", stamp);
            }

            var row = Ui.El("le-row", side).At(0, 520, 960, 90);
            Ui.Btn("RETAKE", Close, primary == "retake" ? "le-btn--primary le-btn--big" : "le-btn--ghost le-btn--big", row).name = "review-retake";
            Ui.Btn("CROP", () =>
            {
                Close();
                Game.Open(new CropView(Game, photo));
            }, primary == "crop" ? "le-btn--primary le-btn--big" : "le-btn--big", row).name = "review-crop";
            Ui.Btn("KEEP", () =>
            {
                S.KeepPhoto(photo);
                Close();
            }, primary == "keep" ? "le-btn--primary le-btn--big" : "le-btn--big", row).name = "review-keep";
            Ui.Text("Kept photos can be inserted into the document on the computer. Not happy with it later? Click the photo corner to call them back for another.", "le-muted le-review__retake", side).At(0, 620, 800, 80);
        }

        /// <summary>ESC never throws a shot away without asking.</summary>
        public override bool OnEscape()
        {
            Game.Open(new ConfirmView(Game, "Throw this photo away?",
                "It isn't kept yet. KEEP keeps it, CROP fixes the size.", "THROW IT AWAY", Close));
            return true;
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
    /// Manual crop with a fixed 35:45 frame (design doc §22), laid out like a passport-photo
    /// template: a dashed TOP OF HEAD line, a NOSE centre line, and a ruler whose zero sits on
    /// the dashed line, so with the crown on the line the chin reading IS the head size. The
    /// handbook's band for the job's country is shaded on it. Nothing snaps into place.
    /// </summary>
    public sealed class CropView : Overlay
    {
        const float W = 600f, H = 800f;
        /// <summary>Handle art (inside the frame's corners) and how far outside a corner still grabs it.</summary>
        const float HandlePx = 32f, GrabIn = 44f, GrabOut = 26f;
        const float RulerGap = 6f, RulerW = 64f;
        /// <summary>Width-to-height of the frame on screen (35:45).</summary>
        const float Aspect = 35f / 45f;

        static readonly ConditionalWeakTable<PhotoCapture, StrongBox<int>> FailedDone = new ConditionalWeakTable<PhotoCapture, StrongBox<int>>();
        static readonly ConditionalWeakTable<PhotoCapture, object> CroppedDone = new ConditionalWeakTable<PhotoCapture, object>();

        /// <summary>DONE was pressed on CROP for this photo at least once (its crop is the player's).</summary>
        public static bool WasCropped(PhotoCapture photo) => photo != null && CroppedDone.TryGetValue(photo, out _);

        readonly PhotoCapture _photo;
        readonly string _countryId;
        readonly string _country;
        readonly PhotoRules _rules;
        readonly Box _opening;
        readonly VisualElement _area;
        readonly VisualElement _img;
        readonly VisualElement _rect;
        readonly VisualElement[] _handles = new VisualElement[4];
        readonly Painted _shade;
        readonly Painted _guides;
        readonly Painted _ruler;
        readonly Label[] _rulerLabels = new Label[9];
        readonly Label _topTag;
        readonly Label _chinTag;
        readonly Label _noseTag;
        readonly PortraitView _preview;
        readonly Button _guidesBtn;
        readonly Button _doneBtn;
        readonly Label _sizeLine, _topLine, _sideLine, _cutLine, _resLine, _okLine;
        readonly VisualElement _sizeChip, _topChip, _sideChip;
        readonly VisualElement _cutRow, _resRow, _okRow;
        Box _crop;
        FramingResult _check;
        bool _showGuides = true;

        /// <summary>The shot being cropped and the frame as it stands (read by the tutorial coach).</summary>
        public PhotoCapture Photo => _photo;
        public Box CurrentCrop => _crop;
        /// <summary>The shared framing check of the frame as it stands (what the readout shows).</summary>
        public FramingResult Check => _check;
        /// <summary>The handbook's photo rules for this job, or null when the section isn't owned.</summary>
        public PhotoRules Rules => _rules;
        public string CountryId => _countryId;
        /// <summary>The frame CROP opened with (RESET goes back to it).</summary>
        public Box OpeningCrop => _opening;

        /// <summary>DONE presses on this photo that left the framing wrong (for the coach's way out).</summary>
        public static int FailedCrops(PhotoCapture photo) =>
            photo != null && FailedDone.TryGetValue(photo, out var n) ? n.Value : 0;

        enum Mode { None, Move, Resize }
        Mode _mode;
        Vector2 _press;
        Box _cropAtPress;
        int _corner;
        int _hover = -2;
        float _nudgeRepeat;
        Keyboard _keyboard;

        public CropView(GameRoot game, PhotoCapture photo) : base(game, "le-overlay--camera")
        {
            _photo = photo;
            _countryId = CropWords.CountryOf(S, photo);
            _country = CropWords.NameOf(S, _countryId);
            _rules = Framing.HandbookPhotoRules(S, _countryId);
            _opening = Framing.OpeningCrop(photo.HeadInFrame, _rules);
            // A fresh shot opens on the practice frame; a cropped one keeps its crop.
            _crop = ReviewView.IsUncropped(photo) ? _opening : photo.Crop;

            Ui.Text("CROP", "le-h1", Root).At(140, 40, 800, 60);
            Ui.Text(_rules != null
                    ? "Top of the head on the dashed line. Nose on the centre line. Then size it: the chin goes in the red band."
                    : "Top of the head on the dashed line. Nose on the centre line. Head size: your handbook has no rule for this country.",
                "le-text", Root).At(140, 100, 1640, 40);
            // Sizing keeps the dashed lines where they are, so placing first and sizing second works.
            Ui.Text("Drag inside: move  ·  Corner IN: bigger head, OUT: smaller (the dashed lines stay put)  ·  Scroll or + / -  ·  Arrows: nudge", "le-muted le-crop__help", Root).At(140, 134, 1640, 34)
                .pickingMode = PickingMode.Ignore;

            _area = Ui.El("le-crop__area", Root).At(140, 170, W + 8f, H + 8f);
            _area.pickingMode = PickingMode.Ignore;
            // Everything is measured in the image's own 600 x 800 box (inside the area's border).
            _img = new PortraitView("le-crop__image") { Capture = photo };
            _img.pickingMode = PickingMode.Ignore;
            _img.At(0, 0, W, H);
            _area.Add(_img);
            _shade = new Painted(DrawShade).At(0, 0, W, H);
            _area.Add(_shade);
            _guides = new Painted(DrawGuides, "le-crop__guides");
            _guides.style.position = Position.Absolute;
            _area.Add(_guides);
            _rect = Ui.El("le-crop__rect", _area);
            _rect.name = "crop-frame";
            _rect.style.position = Position.Absolute;
            _rect.pickingMode = PickingMode.Ignore;
            string[] corners = { "tl", "tr", "bl", "br" };
            for (int i = 0; i < 4; i++)
            {
                _handles[i] = Ui.Deco("le-crop__handle le-crop__handle--" + corners[i], _rect);
                _handles[i].name = "crop-handle-" + corners[i];
            }
            _ruler = new Painted(DrawRuler, "le-crop__ruler");
            _ruler.style.position = Position.Absolute;
            _area.Add(_ruler);
            for (int i = 0; i < _rulerLabels.Length; i++)
            {
                _rulerLabels[i] = Ui.Text((i * 10).ToString(CultureInfo.InvariantCulture), "le-crop__ruler-label", _area);
                _rulerLabels[i].style.position = Position.Absolute;
                _rulerLabels[i].pickingMode = PickingMode.Ignore;
            }
            _topTag = Tag("TOP OF HEAD", "le-crop__tag le-crop__tag--top");
            _chinTag = Tag("CHIN", "le-crop__tag le-crop__tag--chin");
            _noseTag = Tag("NOSE", "le-crop__tag le-crop__tag--nose");

            // The whole screen takes the drag, so a corner at the image's edge still has its full grab circle.
            Root.RegisterCallback<PointerDownEvent>(OnDown);
            Root.RegisterCallback<PointerMoveEvent>(OnMove);
            Root.RegisterCallback<PointerUpEvent>(OnUp);
            Root.RegisterCallback<PointerCaptureOutEvent>(_ => _mode = Mode.None);
            Root.RegisterCallback<WheelEvent>(OnWheel);

            var side = Ui.El("le-review__side le-crop__side", Root).At(1000, 170, 880, 800);
            Ui.Text("RESULT", "le-cam-panel__section", side);
            _preview = new PortraitView("le-review__preview") { Capture = photo };
            _preview.At(0, 40, 252, 324);
            side.Add(_preview);

            var tag = Ui.El("le-crop-rule", side).At(284, 26, 580, -1);
            tag.name = "crop-rule-tag";
            Ui.Deco("le-crop-rule__pin", tag);
            var first = Framing.Check(photo.HeadInFrame, _crop, _rules, photo.SensorHeightPx);
            Ui.Text(CropWords.TagTitle(first, _country), "le-crop-rule__title", tag);
            Ui.Text(CropWords.TagNote(first, _country), "le-crop-rule__note", tag);

            var read = Ui.El("le-crop-read", side).At(284, 156, 580, 230);
            read.name = "crop-readout";
            _sizeLine = ReadRow(read, out _sizeChip, out _);
            _topLine = ReadRow(read, out _topChip, out _);
            _sideLine = ReadRow(read, out _sideChip, out _);
            _cutLine = ReadRow(read, out _, out _cutRow);
            _resLine = ReadRow(read, out _, out _resRow);
            _okLine = ReadRow(read, out _, out _okRow);
            _cutLine.text = CropWords.ChinCutLine;
            _resLine.text = CropWords.LowResLine;
            _okLine.text = "FRAMING OK. DONE.";
            _cutRow.AddToClassList("le-crop-read__row--bad");
            _resRow.AddToClassList("le-crop-read__row--warn");
            _okRow.AddToClassList("le-crop-read__row--ok");

            var tools = Ui.El("le-row", side).At(0, 400, 880, 64);
            _guidesBtn = Ui.Btn("GUIDES", () =>
            {
                _showGuides = !_showGuides;
                Apply();
            }, "le-btn--small", tools);
            _guidesBtn.name = "crop-guides";
            Ui.Btn("RESET", () =>
            {
                _crop = _opening;
                Apply();
            }, "le-btn--small le-btn--ghost", tools).name = "crop-reset";

            var row = Ui.El("le-row", side).At(0, 476, 880, 90);
            Ui.Btn("BACK", Back, "le-btn--ghost le-btn--big", row).name = "crop-back";
            _doneBtn = Ui.Btn("DONE", () =>
            {
                _photo.Crop = _crop;
                if (!CroppedDone.TryGetValue(_photo, out _)) CroppedDone.Add(_photo, null);
                if (!_check.FramingOk)
                {
                    if (FailedDone.TryGetValue(_photo, out var n)) n.Value++;
                    else FailedDone.Add(_photo, new StrongBox<int>(1));
                }
                Close();
                Game.Open(new ReviewView(Game, _photo));
            }, "le-btn--big", row);
            _doneBtn.name = "crop-done";
            // + and - by the character typed, so they work on any keyboard layout.
            _keyboard = Keyboard.current;
            if (_keyboard != null) _keyboard.onTextInput += OnChar;
            Apply();
        }

        public override void OnClosed()
        {
            if (_keyboard != null) _keyboard.onTextInput -= OnChar;
        }

        Label Tag(string text, string classes)
        {
            var l = Ui.Text(text, classes, _area);
            l.style.position = Position.Absolute;
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        static Label ReadRow(VisualElement parent, out VisualElement chip, out VisualElement row)
        {
            row = Ui.El("le-row le-crop-read__row", parent);
            row.pickingMode = PickingMode.Ignore;
            chip = Ui.Deco("le-crop-read__chip", row);
            var l = Ui.Text("", "le-crop-read__text", row);
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        /// <summary>Back to THE PHOTO with the crop it had: nothing is thrown away.</summary>
        void Back()
        {
            Close();
            Game.Open(new ReviewView(Game, _photo));
        }

        public override bool OnEscape()
        {
            Back();
            return true;
        }

        void Apply()
        {
            float x = _crop.X * W, y = _crop.Y * H, w = _crop.W * W, h = _crop.H * H;
            _rect.style.left = x;
            _rect.style.top = y;
            _rect.style.width = w;
            _rect.style.height = h;
            _guides.style.left = x;
            _guides.style.top = y;
            _guides.style.width = w;
            _guides.style.height = h;
            _guides.Show(_showGuides);
            _guides.MarkDirtyRepaint();

            _check = Framing.Check(_photo.HeadInFrame, _crop, _rules, _photo.SensorHeightPx);

            float rx = x + w + RulerGap;
            _ruler.Show(_showGuides);
            _ruler.style.left = rx;
            _ruler.style.top = y;
            _ruler.style.width = RulerW;
            _ruler.style.height = h;
            _ruler.MarkDirtyRepaint();
            // Numbers every 10% (every 20% when the frame is too small for them all).
            bool sparse = h * 0.1f < 30f;
            for (int i = 0; i < _rulerLabels.Length; i++)
            {
                float ly = (Framing.RulerY(_crop, i * 0.1f) - _crop.Y) * H;
                bool fits = ly <= h - 10f;
                _rulerLabels[i].Show(_showGuides && fits && (!sparse || i % 2 == 0));
                _rulerLabels[i].style.left = rx + 22f;
                _rulerLabels[i].style.width = RulerW - 24f;
                _rulerLabels[i].style.top = y + ly - 15f;
            }
            float guideY = (Framing.GuideY(_crop) - _crop.Y) * H;
            _topTag.Show(_showGuides);
            _topTag.style.left = rx + RulerW + 6f;
            _topTag.style.top = y + guideY - 18f;
            _chinTag.Show(_showGuides && _check.HasRule);
            if (_check.HasRule)
            {
                _chinTag.SetTextIfChanged($"CHIN {CropWords.Pct(_check.RuleMin)}-{CropWords.Pct(_check.RuleMax)}");
                float mid = (Framing.RulerY(_crop, (_check.RuleMin + _check.RuleMax) * 0.5f) - _crop.Y) * H;
                _chinTag.style.left = rx + RulerW + 6f;
                _chinTag.style.top = y + mid - 18f;
            }
            // NOSE labels the centre line from outside the frame (below it, or above), so it never covers the chin.
            _noseTag.Show(_showGuides);
            _noseTag.style.left = x + w * 0.5f - 34f;
            _noseTag.style.top = y + h + 40f <= H ? y + h + 4f : y >= 40f ? y - 38f : y + 4f;
            _guidesBtn.EnableInClassList("le-btn--selected", _showGuides);
            _guidesBtn.text = _showGuides ? "GUIDES: ON" : "GUIDES: OFF";

            _shade.MarkDirtyRepaint();
            _preview.View = _crop;
            _preview.Refresh();
            UpdateReadout();
        }

        void UpdateReadout()
        {
            var f = _check;
            _sizeLine.SetTextIfChanged(CropWords.SizeLine(f, _country));
            Chip(_sizeChip, !f.HasRule ? 0 : f.SizeOk ? 1 : 2);
            _topLine.SetTextIfChanged(CropWords.TopLine(f));
            Chip(_topChip, f.Top == FramingTop.CutOff ? 3 : f.TopOk ? 1 : 2);
            _sideLine.SetTextIfChanged(CropWords.SideLine(f));
            Chip(_sideChip, f.Centred ? 1 : 2);
            _cutRow.Show(f.ChinCut);
            _resRow.Show(f.LowRes);
            _okRow.Show(f.FramingOk);
            // DONE only lights up once the frame is right (with no rule to size by: once it's placed).
            _doneBtn.EnableInClassList("le-btn--primary", f.HasRule ? f.FramingOk : f.TopOk && f.Centred && f.NoCut);
        }

        /// <summary>0 unknown, 1 ok, 2 fix it, 3 cut off.</summary>
        static void Chip(VisualElement chip, int state)
        {
            var row = chip.parent;
            row.EnableInClassList("le-crop-read__row--unknown", state == 0);
            row.EnableInClassList("le-crop-read__row--ok", state == 1);
            row.EnableInClassList("le-crop-read__row--warn", state == 2);
            row.EnableInClassList("le-crop-read__row--bad", state == 3);
        }

        // ------------------------------------------------------------------ input

        Vector2 ImagePos(IPointerEvent e) => _img.WorldToLocal(e.position);
        Vector2 ImagePos(Vector2 panelPos) => _img.WorldToLocal(panelPos);

        /// <summary>Which corner (0 tl, 1 tr, 2 bl, 3 br) grabs this point, or -1.</summary>
        int CornerAt(Vector2 p)
        {
            var r = new Rect(_crop.X * W, _crop.Y * H, _crop.W * W, _crop.H * H);
            // Small frames: the grab zones would overlap, so they stop at the middle.
            float inX = Mathf.Min(GrabIn, r.width * 0.5f), inY = Mathf.Min(GrabIn, r.height * 0.5f);
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                bool left = i % 2 == 0, top = i < 2;
                float cx = left ? r.xMin : r.xMax, cy = top ? r.yMin : r.yMax;
                float u = (p.x - cx) * (left ? 1f : -1f); // inward distance
                float v = (p.y - cy) * (top ? 1f : -1f);
                if (u < -GrabOut || v < -GrabOut || u > inX || v > inY) continue;
                float d = (p - new Vector2(cx, cy)).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        bool InFrame(Vector2 p) => new Rect(_crop.X * W, _crop.Y * H, _crop.W * W, _crop.H * H).Contains(p);

        void OnDown(PointerDownEvent e)
        {
            if (e.button != 0 || Game.Top != this) return;
            var local = ImagePos(e);
            _press = local;
            _cropAtPress = _crop;
            _corner = CornerAt(local);
            if (_corner >= 0) _mode = Mode.Resize;
            else if (InFrame(local)) _mode = Mode.Move;
            else return;
            Root.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            var local = ImagePos(e);
            if (_mode == Mode.None)
            {
                int corner = Game.Top == this ? CornerAt(local) : -1;
                Hover(corner >= 0 ? corner : Game.Top == this && InFrame(local) ? 4 : -1);
                return;
            }
            var d = local - _press;
            var c0 = _cropAtPress;
            if (_mode == Mode.Move)
            {
                _crop = Framing.Move(c0, d.x / W, d.y / H);
            }
            else
            {
                // A zoom about the guide point (dashed TOP OF HEAD line x NOSE line). The corner
                // follows the cursor along whichever axis asks for the bigger change, so any drag
                // direction works. A bottom corner sits 88% of the height below the guide point and
                // follows exactly; a top corner sits only 12% above it, so its up/down drag is
                // geared as if from the middle instead of being eight times as touchy.
                bool left = _corner % 2 == 0, top = _corner < 2;
                float h0 = c0.H * H;
                float reachX = Mathf.Max(1f, c0.W * W * 0.5f);
                float reachY = Mathf.Max(1f, top ? Mathf.Max(Framing.GuideTop, 0.5f) * h0 : (1f - Framing.GuideTop) * h0);
                float gx = (left ? -d.x : d.x) / reachX;
                float gy = (top ? -d.y : d.y) / reachY;
                float g = Mathf.Abs(gx) > Mathf.Abs(gy) ? gx : gy;
                SetHeight(c0, c0.H * Mathf.Max(0.05f, 1f + g));
            }
            Apply();
        }

        void OnUp(PointerUpEvent e)
        {
            if (Root.HasPointerCapture(e.pointerId)) Root.ReleasePointer(e.pointerId);
            _mode = Mode.None;
        }

        void OnWheel(WheelEvent e)
        {
            if (Game.Top != this) return;
            var p = ImagePos(e.mousePosition);
            if (p.x < -40f || p.y < -40f || p.x > W + 160f || p.y > H + 40f) return;
            // A sideways swipe or tilt isn't a zoom.
            if (Mathf.Abs(e.delta.y) < 0.01f || Mathf.Abs(e.delta.x) > Mathf.Abs(e.delta.y)) return;
            // Like the camera: scroll up zooms in (smaller frame, bigger head).
            Zoom(e.delta.y < 0f ? 1f : -1f);
            e.StopPropagation();
        }

        /// <summary>+ (or =) zooms in, - zooms out: by the character, whatever the keyboard layout.</summary>
        void OnChar(char ch)
        {
            if (Game.Top != this || _mode != Mode.None) return;
            if (ch == '+' || ch == '=') Zoom(1f);
            else if (ch == '-') Zoom(-1f);
        }

        void Zoom(float steps)
        {
            SetHeight(_crop, _crop.H * (1f - 0.02f * steps));
            Apply();
        }

        /// <summary>Every resize keeps the dashed lines still (and never dead-ends at an edge).</summary>
        void SetHeight(Box from, float h) => _crop = Framing.ResizeAboutGuide(from, h);

        void Hover(int which)
        {
            if (which == _hover) return;
            _hover = which;
            for (int i = 0; i < 4; i++) _handles[i].EnableInClassList("le-crop__handle--hot", i == which);
            _rect.EnableInClassList("le-crop__rect--hot", which == 4);
        }

        public override void Tick(float dt)
        {
            if (Game.Top != this || _mode != Mode.None) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            int nx = (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0);
            int ny = (kb.downArrowKey.isPressed ? 1 : 0) - (kb.upArrowKey.isPressed ? 1 : 0);
            bool nudgeNow = kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame
                || kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame;
            if (Repeat(nx != 0 || ny != 0, nudgeNow, ref _nudgeRepeat, dt))
            {
                float px = kb.shiftKey.isPressed ? 10f : 1f;
                _crop = Framing.Move(_crop, nx * px / W, ny * px / H);
                Apply();
            }
            // + and - (main row or numpad) arrive as typed characters, with the system's key repeat: OnChar.
        }

        /// <summary>Key repeat: once on press, then after a short wait, steadily while held.</summary>
        static bool Repeat(bool held, bool pressedNow, ref float wait, float dt)
        {
            if (!held) { wait = 0f; return false; }
            if (pressedNow) { wait = 0.35f; return true; }
            wait -= dt;
            if (wait > 0f) return false;
            wait = 0.04f;
            return true;
        }

        // ------------------------------------------------------------------ drawing

        void DrawShade(Painter2D p, Rect r)
        {
            var rect = new Rect(_crop.X * r.width, _crop.Y * r.height, _crop.W * r.width, _crop.H * r.height);
            var dim = new Color(0f, 0f, 0f, 0.62f);
            Draw.Rect(p, new Rect(0f, 0f, r.width, rect.yMin), dim);
            Draw.Rect(p, new Rect(0f, rect.yMax, r.width, r.height - rect.yMax), dim);
            Draw.Rect(p, new Rect(0f, rect.yMin, rect.xMin, rect.height), dim);
            Draw.Rect(p, new Rect(rect.xMax, rect.yMin, r.width - rect.xMax, rect.height), dim);
        }

        static readonly Color GuideInk = new Color(1f, 1f, 1f, 0.95f);
        static readonly Color GuideShadow = new Color(0.05f, 0.04f, 0.03f, 0.75f);
        static readonly Color BandRed = new Color(0.86f, 0.2f, 0.16f);
        static readonly Color BandRim = new Color(1f, 0.86f, 0.8f, 0.9f);

        /// <summary>Inside the frame: the TOP OF HEAD line, the NOSE line and the chin band's edges.</summary>
        void DrawGuides(Painter2D p, Rect r)
        {
            float gy = Framing.GuideTop * r.height;
            if (_check.HasRule)
            {
                float y0 = (Framing.GuideTop + _check.RuleMin) * r.height;
                float y1 = Mathf.Min(r.height, (Framing.GuideTop + _check.RuleMax) * r.height);
                Draw.Rect(p, new Rect(0f, y0, r.width, y1 - y0), Draw.Alpha(BandRed, 0.16f));
                // Solid edges with a pale rim, so the band reads over dark hair and red clothes alike.
                foreach (var y in new[] { y0, y1 })
                {
                    Draw.Line(p, new Vector2(0f, y), new Vector2(r.width, y), BandRim, 4f);
                    Draw.Line(p, new Vector2(0f, y), new Vector2(r.width, y), BandRed, 2f);
                }
            }
            Dashed(p, new Vector2(0f, gy), new Vector2(r.width, gy), GuideInk, 3f, 14f, 8f, true);
            Dashed(p, new Vector2(r.width * 0.5f, 0f), new Vector2(r.width * 0.5f, r.height), GuideInk, 3f, 14f, 8f, true);
        }

        static void Dashed(Painter2D p, Vector2 a, Vector2 b, Color c, float width, float dash, float gap, bool shadow)
        {
            float len = Vector2.Distance(a, b);
            if (len < 1f) return;
            var dir = (b - a) / len;
            for (int pass = shadow ? 0 : 1; pass < 2; pass++)
            {
                var col = pass == 0 ? GuideShadow : c;
                float wpx = pass == 0 ? width + 3f : width;
                for (float t = 0f; t < len; t += dash + gap)
                {
                    float e = Mathf.Min(len, t + dash);
                    Draw.Line(p, a + dir * t, a + dir * e, col, wpx);
                }
            }
        }

        /// <summary>
        /// The ruler: zero on the TOP OF HEAD line, a tick every 5% of the frame's height, numbered
        /// every 10%; the handbook's chin band shaded red. Readings are in % of the frame height.
        /// </summary>
        void DrawRuler(Painter2D p, Rect r)
        {
            var paper = new Color(0.95f, 0.91f, 0.74f, 0.96f);
            var ink = new Color(0.15f, 0.12f, 0.1f);
            Draw.Rect(p, new Rect(0f, 0f, r.width, r.height), paper);
            float zero = Framing.GuideTop * r.height;
            if (_check.HasRule)
            {
                float y0 = (Framing.GuideTop + _check.RuleMin) * r.height;
                float y1 = Mathf.Min(r.height, (Framing.GuideTop + _check.RuleMax) * r.height);
                Draw.Rect(p, new Rect(0f, y0, r.width, y1 - y0), Draw.Alpha(BandRed, 0.55f));
                Draw.Line(p, new Vector2(0f, y0), new Vector2(20f, y0), BandRed, 3f);
                Draw.Line(p, new Vector2(0f, y1), new Vector2(20f, y1), BandRed, 3f);
            }
            // Above the line: unnumbered 5% ticks, so "too close / too far" can be read too.
            for (int i = -2; ; i++)
            {
                float y = zero + i * 0.05f * r.height;
                if (y > r.height - 1f) break;
                if (y < 1f) continue;
                bool ten = i % 2 == 0;
                Draw.Line(p, new Vector2(0f, y), new Vector2(ten ? 20f : 11f, y), ink, ten ? 2f : 1.5f);
            }
            // The zero: a bold tick, continuing the dashed TOP OF HEAD line.
            Draw.Line(p, new Vector2(0f, zero), new Vector2(22f, zero), ink, 3f);
            Draw.Poly(p, ink, new Vector2(0f, zero - 7f), new Vector2(8f, zero), new Vector2(0f, zero + 7f));
            Draw.RoundRectStroke(p, new Rect(0f, 0f, r.width, r.height), 0f, new Color(0.35f, 0.28f, 0.16f), 2f);
        }
    }
}
