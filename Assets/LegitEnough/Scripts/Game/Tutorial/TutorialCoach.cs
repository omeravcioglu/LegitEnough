using System;
using System.Collections.Generic;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;
using Box = LegitEnough.Sim.Box;

namespace LegitEnough.Game
{
    /// <summary>
    /// The demo's guided Day 1: Samir's France passport, start to cash. A sticky note from R.
    /// gives one short order at a time and a bobbing arrow points at the thing to click.
    /// On Day 2 a short, skippable morning tour shows the computer's SHOP (R.'s note, the France
    /// update, the German section, the sale), and later notes say where to go when a visitor can't
    /// be served for want of a handbook section ("Click the COMPUTER, then SHOP").
    /// It is NOT an <see cref="Overlay"/>: it never touches the overlay stack, Top or ESC, and
    /// only its SKIP button takes clicks. The step is worked out from game state every frame
    /// (like the shop's hint line), so playing out of order never leaves it stuck.
    /// </summary>
    public sealed class TutorialCoach
    {
        const float NoteW = 440f;
        const float Gutter = 24f;
        const int ArrowScale = 4;
        const float ArrowSize = 13 * ArrowScale;
        /// <summary>Head height as a share of the viewfinder before the zoom step counts as done.</summary>
        const float MinHeadInFrame = 0.38f;
        const float StampSeconds = 0.9f;

        /// <summary>The job in order. Progress (the stamp, the counter) is a step's place in this list.</summary>
        static readonly string[] Flow =
        {
            "start", "mail", "read", "open", "wait", "reply", "seat",
            "camera", "rules", "backdrop", "glasses", "face", "relax", "zoom", "aim", "shoot",
            "review", "crop", "keep", "done",
            "computer", "template", "format", "data", "dates", "number", "photo", "print",
            "printing", "jam", "bind", "inspect", "hand", "checking", "cash", "close",
        };

        /// <summary>What the note's counter shows: the part of the job, by the last <see cref="Flow"/> slot in it.</summary>
        static readonly (string Name, string Last)[] Parts =
        {
            ("OPEN UP", "open"), ("THE DEAL", "reply"), ("THE PHOTO", "done"),
            ("THE PAPERS", "print"), ("PRINT", "bind"), ("HAND OVER", "close"),
        };

        /// <summary>Day 2's morning tour, in order (slots start with "d2-").</summary>
        static readonly string[] Flow2 = { "d2-mail", "d2-read", "d2-shop", "d2-update", "d2-extras", "d2-open" };

        static readonly (string Name, string Last)[] Parts2 =
        {
            ("THE MAIL", "d2-read"), ("THE SHOP", "d2-extras"), ("OPEN UP", "d2-open"),
        };

        /// <summary>A Day-2 note outside the tour (a visitor you can't serve yet): no counter, no stamp.</summary>
        const string NudgeSlot = "d2-nudge";

        // Not button labels (DONE, NEXT...), so a stamp never reads like an instruction.
        static readonly string[] Stamps = { "GOOD.", "RIGHT.", "NICE.", "OK." };

        /// <summary>Arrow sides in the order they're tried, with a small cost so ties keep the old look.</summary>
        static readonly (int Dir, float Cost)[] ArrowSides =
        {
            (DemoArt.PointerDown, 0f), (DemoArt.PointerRight, 300f), (DemoArt.PointerLeft, 350f), (DemoArt.PointerUp, 600f),
        };

        /// <summary>How far the arrow bobs, in stage pixels (three art pixels).</summary>
        const float BobTravel = 3 * ArrowScale;

        struct Step
        {
            /// <summary>Place in <see cref="Flow"/>.</summary>
            public string Slot;
            public string Text;
            /// <summary>Element name to point at (null: no arrow).</summary>
            public string Target;
            /// <summary>Tried when <see cref="Target"/> isn't on screen.</summary>
            public string Fallback;
            /// <summary>A second line, e.g. what's in the way.</summary>
            public string Extra;
            /// <summary>The closing "That's the job." note.</summary>
            public bool Final;
            /// <summary>Nothing to click, just look: a dim ring on the target and no arrow.</summary>
            public bool Watch;
            /// <summary>The arrow's direction when picking a side isn't good enough (null: pick).</summary>
            public int? Arrow;
            /// <summary>A quiet second line (a tip, not a warning); shown when there's no <see cref="Extra"/>.</summary>
            public string Aside;
        }

        readonly GameRoot _game;
        readonly VisualElement _note;
        readonly Label _title;
        readonly Label _count;
        readonly Label _text;
        readonly Label _extra;
        readonly Label _stamp;
        readonly VisualElement _ring;
        readonly PixelImage _arrow;
        readonly PixelBuffer[] _arrows = new PixelBuffer[4];
        readonly List<Rect> _keepClear = new List<Rect>();
        readonly List<Rect> _obstacles = new List<Rect>();

        GameSession _session;
        bool _skipped;
        bool _sawRules;
        bool _jamSeen;
        int _inspectedDoc = -1;
        int _handedDoc = -1;
        /// <summary>He sent the photo back: only photos newer than this one count (-1: any photo).</summary>
        int _retakeAfter = -1;
        int _retakeRedo;
        int _best;
        int _stamps;
        float _time;
        float _stampT;
        float _handbookFor;
        bool _preferBottom;
        /// <summary>The camera's empty right column, just above its bottom buttons, is a dock spot too.</summary>
        bool _sideSpot;
        int _arrowDir = -1;
        int _arrowSide = DemoArt.PointerDown;
        string _arrowKey = "";
        Rect _arrowFor;
        float _arrowAge;
        int _dock = -1;
        string _dockKey = "";
        /// <summary>A note is up this frame (Day 2 only shows one when there's something to say).</summary>
        bool _showing;
        int _best2;
        /// <summary>Day 2's morning tour is over (the shop opened and the update's bought, or visitors came).</summary>
        bool _day2Done;

        /// <summary>The coach of the running game (one per NewGame).</summary>
        public static TutorialCoach Current { get; private set; }

        /// <summary>True while the coach of <paramref name="game"/> is guiding (the shop hint line stays quiet).</summary>
        public static bool IsActiveFor(GameRoot game) => Current != null && Current._game == game && Current.Active;

        /// <summary>Full-stage layer; add it above the overlay layer. Only the SKIP button is pickable.</summary>
        public VisualElement Root { get; }

        GameSession S => _game.Session;

        /// <summary>
        /// Guiding: demo Day 1 (not skipped, hints on: the trailer and store harnesses turn hints off),
        /// or a Day-2 note is up. Skipping on Day 1 skips Day 2 too; hints off (F1) hides both.
        /// </summary>
        public bool Active => Eligible && (S.TutorialActive || _showing);

        /// <summary>Demo Day 2 of a guided demo: the SHOP tour and the "where to buy it" notes.</summary>
        bool DayTwo => S != null && S.IsDemo && S.Rules.Tutorial && S.Day == DemoCast.DayTwo;

        bool Eligible => S != null && !_skipped && _game.ShowHints && (S.TutorialActive || DayTwo);

        public TutorialCoach(GameRoot game)
        {
            _game = game;
            Current = this;
            Root = Ui.El("le-coach");
            Root.name = "tutorial-coach";
            Root.At(0, 0, GameRoot.StageWidth, GameRoot.StageHeight);
            Root.pickingMode = PickingMode.Ignore;

            _ring = Ui.Deco("le-coach__ring", Root);
            _ring.style.position = Position.Absolute;
            _arrow = new PixelImage("le-coach__pointer").At(0, 0, ArrowSize, ArrowSize);
            Root.Add(_arrow);

            _note = Ui.Deco("le-coach__note", Root).At(Gutter, Gutter, NoteW);
            _note.name = "coach-note";
            Ui.Deco("le-coach__tape", _note);
            var head = Ui.Deco("le-row le-coach__head", _note);
            _title = Txt("le-coach__from", head);
            _count = Txt("le-coach__count", head);
            _text = Txt("le-coach__text", _note);
            _extra = Txt("le-coach__extra", _note);
            var foot = Ui.Deco("le-row le-coach__foot", _note);
            Ui.Btn("SKIP TUTORIAL", Skip, "le-coach__skip", foot).name = "coach-skip";
            Txt("le-coach__sign", foot).text = "- R.";
            _stamp = Txt("le-coach__stamp", _note);
            _stamp.style.opacity = 0f;

            Root.Show(false);
        }

        static Label Txt(string classes, VisualElement parent)
        {
            var l = Ui.Text("", classes, parent);
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        /// <summary>Stop guiding for the rest of this game; the shop's hint line takes over.</summary>
        public void Skip()
        {
            if (_skipped) return;
            _skipped = true;
            Root.Show(false);
            _game.Toasts?.Show("Tutorial skipped. The hint line stays on (F1).");
        }

        public void OnSimEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.PrinterJammed:
                    _jamSeen = true;
                    break;
                case SimEventType.PagePrinted:
                    _jamSeen = false;
                    break;
                case SimEventType.DocumentHandedOver:
                    _handedDoc = e.Amount;
                    break;
            }
        }

        // ================================================================== per frame

        public void Tick(float dt)
        {
            _time += dt;
            if (_game.Session != _session) Reset();
            if (!Eligible || S.Phase == DayPhase.Closed || Hidden() || Root.panel == null)
            {
                Hide();
                return;
            }

            _handbookFor = _game.Find<HandbookView>() != null ? _handbookFor + dt : 0f;
            bool dayOne = S.TutorialActive;
            if (dayOne) NoteRulesRead();

            var step = Unblock(dayOne ? Derive() : DeriveDayTwo());
            if (step.Slot == null)
            {
                Hide();
                return;
            }
            _showing = true;
            Root.Show(true);
            Context(step.Slot);
            Render(step, dt);
        }

        void Hide()
        {
            Root.Show(false);
            _showing = false;
        }

        void Reset()
        {
            _session = _game.Session;
            _showing = false;
            _best2 = 0;
            _day2Done = false;
            _skipped = false;
            _sawRules = false;
            _jamSeen = false;
            _inspectedDoc = -1;
            _handedDoc = -1;
            _retakeAfter = -1;
            _retakeRedo = 0;
            _best = 0;
            _stampT = 0f;
            _dock = -1;
            _dockKey = "";
            _arrowKey = "";
        }

        /// <summary>Menus and cards (title, pause, settings, the day report) get the stage to themselves.</summary>
        bool Hidden()
        {
            if (_game.Find<TitleView>() != null) return true;
            var top = _game.Top;
            return top != null && top.PausesGame && !(top is DayStartView) && !(top is ConfirmView);
        }

        /// <summary>Reading the destination's handbook page counts once it's been open a moment.</summary>
        void NoteRulesRead()
        {
            if (_sawRules || _handbookFor <= 0f) return;
            var order = SamirOrder();
            string cid = order?.CountryId ?? Countries.France;
            var tab = _game.Stage?.Q("handbook-tab-" + cid);
            bool onPage = tab != null && tab.ClassListContains("le-tab--active");
            if ((onPage && _handbookFor > 0.6f) || (order != null && _handbookFor > 4f)) _sawRules = true;
        }

        // ================================================================== the steps

        static Step Make(string slot, string text, string target = null, string fallback = null) =>
            new Step { Slot = slot, Text = text, Target = target, Fallback = fallback };

        Visit SamirVisit() => S.Visits.Find(v => v.Phase != VisitPhase.Gone && v.Customer != null && v.Customer.Id == StoryCast.SamirId);

        Order SamirOrder()
        {
            Order found = null;
            foreach (var o in S.Orders.All)
                if (o.CustomerId == StoryCast.SamirId) found = o;
            return found;
        }

        Step Derive()
        {
            if (_game.Top is DayStartView)
                return Make("start", "Day one. One customer today. I'll talk you through him. START THE DAY.", "daycard-start");
            if (_game.Shop != null && _game.Shop.AskingRetake)
                return Make("seat", "Another photo? Fine. He won't love it, but he'll sit.", "confirm-ok");

            var samir = SamirVisit();
            var order = SamirOrder();

            if (order != null && order.Status == OrderStatus.Delivered)
            {
                if (S.CounterCash.Count > 0)
                    return Make("cash", "He paid. Click the cash. It goes in the box, not your pocket.", "cash-" + S.CounterCash[0].Id, "hit-cash");
                return CloseStep(true);
            }

            if (order == null)
            {
                if (samir != null)
                {
                    if (samir.Phase == VisitPhase.Choosing && samir.Choices.Count > 0) return ReplyStep(samir);
                    return Make("wait", "Your first customer. Let him talk.\n(Click a speech bubble to skip a line.)");
                }
                if (S.Phase == DayPhase.Morning) return MorningStep();
                if (S.Phase == DayPhase.AfterHours || !SamirDue()) return CloseStep(false);
                return Make("wait", "Someone's coming. Give it a minute. The clock on the wall is running.");
            }
            if (!order.IsOpen || samir == null) return CloseStep(false);
            NoteRetake(order);
            // Called back for another photo: only shots from now on count as "the new one".
            if (samir.Retake) _retakeAfter = Math.Max(_retakeAfter, samir.RetakeAfterPhoto);

            if (samir.Phase == VisitPhase.Choosing && samir.Choices.Count > 0)
            {
                if (samir.Choices.Exists(c => c.Id == GameSession.ChoiceIds.TakePhoto))
                    return Make("seat", "Photo first. LET'S TAKE YOUR PHOTO: he goes to the stool.", ChoiceTarget(GameSession.ChoiceIds.TakePhoto), "choices");
                if (samir.Choices.Exists(c => c.Id == GameSession.ChoiceIds.TrustMe))
                    return Make("checking", "He spotted something. Say you'll fix it and redo it, or tell him to trust you.", "choices");
                if (samir.Choices.Exists(c => c.Id == GameSession.ChoiceIds.FixIt))
                    return Make("checking", "He's not buying it. Say you'll fix it, then redo what he said.", ChoiceTarget(GameSession.ChoiceIds.FixIt), "choices");
                return ReplyStep(samir);
            }
            // The subject goes away when the sitting is over ("done"), before he gets up.
            if (samir.Phase == VisitPhase.PhotoSitting && samir.Subject != null) return PhotoStep(samir, order);
            if (samir.Moving && samir.To == Spot.Stool) return Make("seat", "He's heading for the stool. Give him a second.");
            if (order.Status == OrderStatus.AwaitingPhoto)
            {
                bool retake = order.Redos > 0;
                if (samir.Phase == VisitPhase.Waiting && !samir.Moving)
                    return Make("seat", retake
                        ? "He wants a new photo. Click the photo corner to call him back to the stool."
                        : "He's on the bench. Click the photo corner to call him to the stool.", "hit-photo");
                if (samir.Moving && samir.To == Spot.Stool) return Make("seat", "He's heading for the stool. Give him a second.");
                return Make("seat", retake ? "Let him sit down. Then call him back for a new photo." : "Let him finish. Then the photo.");
            }
            if (order.Status == OrderStatus.InProduction) return MakeStep(order);
            if (order.Status == OrderStatus.Ready)
            {
                var doc = DocToHand(order);
                // A new photo after the booklet was made: print it again with the new one.
                if (doc != null && doc.Id != _handedDoc && MadeWithOldPhoto(order, doc)) return MakeStep(order);
                if (doc != null) return HandStep(order, doc);
            }
            if (samir.Phase == VisitPhase.Inspecting) return Make("checking", "He's checking it. Look bored.");
            return Make("checking", "He's coming over. Let him look at it.");
        }

        /// <summary>The reply button for one choice (ShopView names them "choice-&lt;id&gt;").</summary>
        static string ChoiceTarget(string choiceId) => "choice-" + choiceId;

        /// <summary>
        /// FIX IT over a photo problem sends the order back for a photo: from then on only photos
        /// taken after that count, so the coach asks for a new one instead of waving the old one through.
        /// </summary>
        void NoteRetake(Order order)
        {
            if (order.Status != OrderStatus.AwaitingPhoto || order.Redos <= 0 || order.Redos == _retakeRedo) return;
            _retakeRedo = order.Redos;
            foreach (var p in S.Photos.ForCustomer(order.CustomerId)) _retakeAfter = Math.Max(_retakeAfter, p.Id);
        }

        /// <summary>
        /// The booklet to talk about: the one he handed back (to shred) while it's on the desk, else the
        /// order's newest booklet (a reprint with a new photo), else any of his.
        /// </summary>
        FinishedDocument DocToHand(Order order)
        {
            if (order.Redos > 0)
            {
                var back = S.DeskDocuments.Find(d => d.Id == _handedDoc && d.OrderId == order.Id);
                if (back != null) return back;
            }
            if (order.Document != null && S.DeskDocuments.Contains(order.Document)) return order.Document;
            return S.DeskDocuments.Find(d => d.OrderId == order.Id);
        }

        /// <summary>He sat for a new photo after this booklet was bound (and a new photo was kept).</summary>
        bool MadeWithOldPhoto(Order order, FinishedDocument doc) =>
            _retakeAfter >= 0 && KeptPhotos(order.CustomerId) > 0 && (doc.Data?.PhotoId ?? -1) <= _retakeAfter;

        /// <summary>Photos of him that still count (after a retake request, only the new ones).</summary>
        int KeptPhotos(int customerId)
        {
            int n = 0;
            foreach (var p in S.Photos.ForCustomer(customerId)) if (p.Id > _retakeAfter) n++;
            return n;
        }

        /// <summary>Samir's arrival is still scheduled (the shop is open but he isn't in yet).</summary>
        bool SamirDue()
        {
            foreach (var a in S.Director.Upcoming)
                if (a.Beat != null && a.Beat.BeatId == StoryCast.SamirBeatId) return true;
            return false;
        }

        Step MorningStep()
        {
            bool unread = S.Inbox.Unread > 0;
            if (_game.Find<ComputerView>() != null)
            {
                if (unread) return Make("read", "MESSAGES. Mine's the one on top.", "nav-messages");
                return Make("read", "Read it twice. Then close the computer.", "os-close");
            }
            if (unread) return Make("mail", "I left you a message. It's on the computer.", "hit-crt");
            return Make("open", "Flip the sign. We're open.", "hit-sign");
        }

        Step ReplyStep(Visit samir)
        {
            bool final = samir.Choices.Count == 1 && samir.Choices[0].Id == GameSession.ChoiceIds.Accept;
            return final
                ? Make("reply", "He won't go higher. Take it.", "choices")
                : Make("reply", "Take the job. Or ask for more. Asking is free.", "choices");
        }

        Step CloseStep(bool paid)
        {
            if (_game.Top is ConfirmView)
                return new Step { Slot = "close", Text = "Yes. Close it.", Target = "confirm-ok", Final = paid };
            if (!paid)
                return Make("close", S.Phase == DayPhase.AfterHours ? "Closing time. Flip the sign." : "Nobody else is coming today. Flip the sign. Close early.", "hit-sign");
            string when = S.Phase == DayPhase.AfterHours ? "Closing time: flip the sign." : "Nobody else is coming today. Flip the sign. Close early.";
            return new Step
            {
                Slot = "close", Text = "Photo, papers, print, bind, paid. Tomorrow they come faster. " + when, Target = "hit-sign", Final = true,
                Aside = "Tomorrow's tools are in the computer's SHOP.",
            };
        }

        // ---------------- photo

        Step PhotoStep(Visit samir, Order order)
        {
            if (samir.Moving) return Make("seat", "He's sitting down. Give him a second.");
            var rules = S.HandbookRules(order.CountryId);
            int kept = KeptPhotos(samir.Customer.Id);
            var camera = _game.Find<CameraView>();
            if (camera != null)
            {
                var crop = _game.Find<CropView>();
                if (crop != null) return CropStep(crop.Photo, crop.CurrentCrop, rules.Photo);
                var review = _game.Find<ReviewView>();
                if (review != null) return ReviewStep(review.Photo, rules.Photo);
            }
            if (kept == 0 && _game.Find<HandbookView>() != null) return RulesStep(order.CountryId, rules);
            if (camera == null)
                return kept > 0
                    ? Make("done", "Back to the camera. Tell him you're done.", "hit-tripod")
                    : Make("camera", "He's on the stool. Click the camera.", "hit-tripod");
            if (kept > 0)
            {
                var done = Make("done", "Got one. DONE - THANK YOU.", "cam-done");
                done.Aside = samir.Retake ? "Then pick the new one in the editor." : "Not happy with it later? Click the photo corner: he sits again.";
                return done;
            }
            if (!_sawRules) return Make("rules", $"{rules.Name} has rules about photos. Read them first. HANDBOOK.", "cam-handbook");

            var session = S.ActivePhoto;
            if (session == null) return Make("camera", "Click the camera.", "hit-tripod");
            var pr = rules.Photo;
            var rig = session.Rig;
            var subj = session.Subject;
            if (rig.SwappingBackdrop) return Make("backdrop", "It's rolling down. Wait for it.");
            if (rig.Backdrop != pr.Backdrop)
                return Make("backdrop", $"{rules.Name} wants {Labels.Of(pr.Backdrop)}. Pull it down.", "cam-backdrop-" + pr.Backdrop);
            if (subj.WearingSunglasses || (subj.HasGlasses && !pr.GlassesAllowed))
                return Make("glasses", $"No glasses for {rules.Name}. GLASSES OFF.", "cam-glasses");
            if (subj.HatOn && subj.Customer.Look.Hat != HatStyle.None && !pr.HeadwearAllowed)
                return Make("glasses", "No hats either. HAT OFF.", "cam-hat");
            if (!subj.Pose.IsCenter || Math.Abs(subj.GazeX) > 0.45f || Math.Abs(subj.GazeY) > 0.45f)
                return Make("face", "Face front. LOOK AT THE CAMERA. If he won't, drag his head.", "cam-look");
            if (!pr.ExpressionOk(subj.Expression))
                return Make("relax", $"Straight face. {rules.Name} doesn't do smiles. Tell him.", "cam-face");

            var head = PortraitGeometry.ToFrame(
                PortraitGeometry.HeadWorld(subj.Customer.Look, subj.Pose, subj.SeatOffset, subj.Slouch, subj.HatOn),
                PortraitGeometry.FrameWorld(rig.Zoom, rig.PanX, rig.PanY));
            if (head.H < MinHeadInFrame && rig.Zoom < session.Camera.MaxZoom - 0.01f)
                return Make("zoom", "Closer. ZOOM + till his head is big.", "cam-zoom-in");
            if (head.Y < 0.03f || head.Bottom > 0.97f || head.X < 0.02f || head.Right > 0.98f)
                return Make("aim", "His head's out of the frame. Drag the background to aim.", "cam-viewfinder");
            if (subj.Lid >= Balance.BlinkHalfThreshold) return Make("shoot", "Eyes! Wait for them...", "cam-shutter");
            return Make("shoot", "Now. SHUTTER. It fires a beat late, so watch his eyes.", "cam-shutter");
        }

        Step RulesStep(string countryId, CountrySpec rules)
        {
            var pr = rules.Photo;
            string text = $"{rules.Name}, photo page: {Labels.Of(pr.Backdrop)} backdrop. "
                + (pr.GlassesAllowed ? "Glasses OK. " : "No glasses. ")
                + (pr.HeadwearAllowed ? "Hats OK. " : "No hats. ")
                + (pr.SmileAllowed ? "Smiles OK. " : "No smiling. ")
                + $"Head fills {Range(pr)} of the photo. Got it? Press H.";
            return Make("rules", text, TabTarget(countryId));
        }

        Step ReviewStep(PhotoCapture photo, PhotoRules pr)
        {
            if (photo == null) return Make("review", "Look at it properly.");
            if (photo.EyesClosed || photo.EyesHalfClosed) return Make("review", "He blinked. RETAKE.", "review-retake");
            if (photo.Blurred) return Make("review", "He moved. It's blurry. RETAKE.", "review-retake");
            if (photo.BackdropMoving || photo.Backdrop != pr.Backdrop) return Make("review", "Wrong backdrop. RETAKE.", "review-retake");
            if (photo.Glasses.Count > 0 && (!pr.GlassesAllowed || photo.Glasses[photo.Glasses.Count - 1] == GlassesStyle.Sunglasses))
                return Make("review", "Glasses in the shot. RETAKE.", "review-retake");
            if (!pr.ExpressionOk(photo.Expression)) return Make("review", "That face won't pass. RETAKE.", "review-retake");
            if (!photo.Pose.IsCenter) return Make("review", "Head's turned. RETAKE.", "review-retake");
            if (photo.Crop.W >= 0.99f) return Make("review", "That's a keeper. CROP it to passport size first.", "review-crop");
            if (!FramingOk(photo.HeadInFrame, photo.Crop, pr, out _)) return Make("crop", "Not quite on the ruler. CROP it again.", "review-crop");
            return Make("keep", "That'll do. KEEP.", "review-keep");
        }

        Step CropStep(PhotoCapture photo, Box crop, PhotoRules pr)
        {
            if (photo == null) return Make("crop", "Drag a corner. Drag the frame.", "crop-frame");
            return FramingOk(photo.HeadInFrame, crop, pr, out string why)
                ? Make("crop", "On the ruler. Click DONE.", "crop-done")
                : Make("crop", why, "crop-frame");
        }

        /// <summary>The same framing test the photo is scored with, told in plain words.</summary>
        static bool FramingOk(Box head, Box crop, PhotoRules pr, out string why)
        {
            float coverage = head.H / crop.H;
            if (coverage < pr.CoverageMin)
            {
                why = $"Head's too small. Drag a corner in. Crown to chin: {Range(pr)} of the photo. Read the RULER.";
                return false;
            }
            if (coverage > pr.CoverageMax)
            {
                why = $"Too tight. Drag a corner out. Crown to chin: {Range(pr)}.";
                return false;
            }
            float top = (head.Y - crop.Y) / crop.H;
            float bottom = (crop.Bottom - head.Bottom) / crop.H;
            float dx = Math.Abs(head.CenterX - crop.CenterX) / crop.W;
            if (top < PhotoEvaluator.TopMarginMin || top > PhotoEvaluator.TopMarginMax || bottom < -0.01f || dx > PhotoEvaluator.CenterTolerance)
            {
                why = "Size is right. Now drag the frame: head in the middle, a little air on top.";
                return false;
            }
            why = "";
            return true;
        }

        static string Range(PhotoRules pr) => $"{Mathf.RoundToInt(pr.CoverageMin * 100f)}-{Mathf.RoundToInt(pr.CoverageMax * 100f)}%";

        // ---------------- the document

        Step MakeStep(Order order)
        {
            var page = S.Printer.Tray.Find(p => p.OrderId == order.Id);
            if (page != null)
            {
                if (_game.Find<PrintMenu>() != null) return Make("bind", "No smudges? BIND INTO BOOKLET.", "print-bind");
                return Make("bind", "It's out. Click the page in the tray.", "page-" + order.Id, "hit-tray");
            }
            if (S.Printer.Jammed) return Make("jam", "Jammed. Told you. Hit the printer. Not too hard.", "hit-printer");
            if (S.Printer.Busy)
            {
                var wait = _jamSeen
                    ? Make("jam", "There. Let it finish.", "hit-printer")
                    : Make("printing", "Printing. Go and watch the printer. It's not fast.", "hit-printer");
                wait.Watch = true;
                return wait;
            }
            return EditorStep(order);
        }

        Step EditorStep(Order order)
        {
            string cid = order.CountryId;
            var spec = S.HandbookRules(cid);
            var pass = spec.Passport;
            var draft = order.Draft;
            if (_game.Find<ComputerView>() == null)
            {
                if (_game.Find<SlipView>() != null) return Make("computer", "That's his sheet. OPEN IN EDITOR.", "slip-open-editor");
                bool again = S.DeskDocuments.Exists(d => d.OrderId == order.Id);
                return Make("computer", again ? "New photo, new print. Click the computer." : "Now his passport. Click the computer.", "hit-crt");
            }
            if (_game.Find<HandbookView>() != null)
                return Make("format", $"{spec.Name}, left page: category, cover, pages, date format. Got it? Press H.", TabTarget(cid));
            if (!OnScreen("editor-template")) return Make("computer", "OFFICIAL-ISH 98. Open it.", "nav-editor");
            if (draft.CountryId != cid) return Make("template", $"Template: {spec.Name.ToUpperInvariant()}.", "tpl-" + cid);
            if (draft.Category != pass.Category || draft.Cover != pass.Cover || draft.Pages != pass.Pages)
            {
                // From the left, over the empty frame: on the right it would sit next to PRINT.
                var format = Make("format", "Category, cover, pages. The handbook says which. Press H.", "editor-format");
                format.Arrow = DemoArt.PointerRight;
                return format;
            }

            string pattern = Labels.Pattern(pass.DateFormat);
            foreach (var f in pass.PersonalFields)
                if (Labels.Normalize(draft.Get(f)).Length == 0)
                    return Make("data", "Copy him off the client sheet, pinned on the left. Every field.", "editor-data");
            if (!Labels.TryParse(Labels.Normalize(draft.Get(DocField.DateOfBirth)), pass.DateFormat, out _))
                return Make("data", $"Dates go the template's way: {pattern}. Not his way.", "editor-data");
            var issued = Calendar.DateOf(S.Day);
            if (!DateIs(draft.Get(DocField.DateOfIssue), issued, pass.DateFormat) || !DateIs(draft.Get(DocField.DateOfExpiry), issued.AddYears(pass.ValidityYears), pass.DateFormat))
                return Make("dates", $"Issued today. Expires in {pass.ValidityYears} years. {pattern}. Today's date is up in the corner.", "editor-data");
            if (Labels.Normalize(draft.Get(DocField.DocumentNumber)).Length == 0)
                return Make("number", "Document number. GENERATE. Nobody checks. Probably.", "editor-docnumber");
            if (!draft.PhotoId.HasValue) return Make("photo", "His photo. Click it in.", "editor-photo");
            if (draft.PhotoId.Value <= _retakeAfter && KeptPhotos(order.CustomerId) > 0)
                return Make("photo", "Not the old photo. Click the new one in.", "editor-photo");
            // From the right: above PRINT the arrow would sit on the preview's barcode and number.
            var print = Make("print", "Read it over. Nothing gets checked for you. Then PRINT.", "editor-print");
            print.Arrow = DemoArt.PointerLeft;
            return print;
        }

        static bool DateIs(string typed, DateTime expected, DateFormat format) =>
            Labels.TryParse(Labels.Normalize(typed), format, out var d) && d == expected.Date;

        Step HandStep(Order order, FinishedDocument doc)
        {
            // He handed it back ("fix it"): shred it and make it again.
            if (order.Redos > 0 && doc.Id == _handedDoc)
            {
                // After a retake the new booklet is already on the desk: this one is just the old copy.
                bool remade = S.DeskDocuments.Exists(d => d.OrderId == order.Id && d.Id != doc.Id);
                if (_game.Find<PassportView>() != null)
                    return Make("checking", remade ? "That's the old one. SHRED IT." : "SHRED IT. Then fix what he said and print a new one.", "passport-shred");
                return Make("checking", remade ? "The old booklet is still on the desk. Click it and SHRED IT." : "He wants it fixed. Click the booklet and SHRED IT.", ShopView.BookletName(S, doc), "hit-booklets");
            }
            // A reprint with a new photo leaves the old copy on the desk: talk about the new one.
            bool twin = S.DeskDocuments.Exists(d => d.OrderId == order.Id && d.Id != doc.Id);
            if (_game.Find<PassportView>() != null)
            {
                _inspectedDoc = doc.Id;
                return Make("inspect", "Cover, pages, photo, dates. Legit enough? Close it.", "passport-close");
            }
            if (_inspectedDoc != doc.Id)
                return Make("inspect", twin ? "The new booklet. Check it before he does: click it." : "Check it before he does. Click the booklet.", ShopView.BookletName(S, doc), "hit-booklets");
            return Make("hand", twin ? "Drag the new booklet onto him. The old copy can stay." : "Drag the booklet onto him. He's on the bench.", ShopView.BookletName(S, doc), "hit-booklets");
        }

        // ================================================================== Day 2: the SHOP

        /// <summary>
        /// Day 2: first a note for a visitor who can't be served yet (no section, an outdated binder),
        /// then the short morning tour of the SHOP. Nothing here waits for the player: the tour ends
        /// once the shop is open and the update's bought, or as soon as the first visitor walks in.
        /// </summary>
        Step DeriveDayTwo()
        {
            if (_game.Top is DayStartView) return default;
            var nudge = DayTwoNudge();
            if (nudge.Slot != null) return nudge;
            if (_day2Done) return default;

            var france = S.Country(Countries.France);
            bool outdated = france != null && france.Owned && france.HandbookOutdated;
            if (S.Phase != DayPhase.Morning && (!outdated || S.Visits.Count > 0))
            {
                _day2Done = true;
                return default;
            }

            var pc = _game.Find<ComputerView>();
            string app = pc?.CurrentApp;
            if (app == "shop") return outdated ? UpdateStep(france) : ExtrasStep();

            var note = DayTwoNote();
            if (note != null && !note.Read)
            {
                if (pc == null) return Make("d2-mail", "Morning. Another note from me. It's on the computer.", "hit-crt");
                if (app != "messages") return Make("d2-read", "MESSAGES. Mine says \"day two\".", "nav-messages");
                return Make("d2-read", "Mine says \"day two\". Click it.", "msg-" + note.Id);
            }
            if (outdated)
            {
                if (pc == null) return Make("d2-shop", $"{france.Spec.Name} changed its rules. Your binder's out of date. Click the COMPUTER, then SHOP.", "hit-crt");
                return Make("d2-shop", $"SHOP. The {france.Spec.Name} update is in there.", "nav-shop");
            }
            if (pc != null) return Make("d2-open", "Close the computer. Then flip the sign.", "os-close");
            return Make("d2-open", "Flip the sign. Customers today.", "hit-sign");
        }

        /// <summary>R.'s Day-2 message ("day two").</summary>
        Message DayTwoNote()
        {
            foreach (var m in S.Inbox.Messages)
                if (m.Kind == MessageKind.System && m.Subject == DemoCast.DayTwoSubject && m.ArrivesAt.Day == S.Day) return m;
            return null;
        }

        Step UpdateStep(CountryState c)
        {
            var step = Make("d2-update", $"{c.Spec.Name} changed the rules. UPDATE your binder: {Labels.Money(Balance.HandbookUpdateCost)}.", "shop-update-" + c.Id);
            if (S.Ledger.Cash < Balance.HandbookUpdateCost) step.Extra = "Not enough cash yet. Take a job first.";
            return step;
        }

        /// <summary>After the update: what else is worth a look today. The player decides; the note just points.</summary>
        Step ExtrasStep()
        {
            var de = S.Country(Countries.Germany);
            bool germany = de != null && !de.Owned && !de.FullGameOnly && de.Requestable(S.Standing.Reputation) && S.DemoClientDue(Countries.Germany);
            int sale = ComputerView.SalePercentOf(S);
            string text = "Updated. ";
            if (germany) text += $"{de.Spec.Name}'s section is here too ({Labels.Money(de.Spec.HandbookPrice)}): the German client needs it. ";
            if (sale > 0) text += sale == 50 ? "And gear is half off today. " : $"And gear is {sale}% off today. ";
            text += germany || sale > 0 ? "Your call. Then close the computer." : "Close the computer.";
            if (!germany && sale <= 0) return Make("d2-extras", text, "os-close");
            var step = Make("d2-extras", text, germany ? "shop-buy-" + de.Id : FirstEquipmentButton() ?? "os-close");
            step.Watch = true; // a look, not an order
            step.Aside = "Close it with the X, top right.";
            return step;
        }

        string FirstEquipmentButton()
        {
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                string name = "shop-equip-" + slot.ToString().ToLowerInvariant();
                if (OnScreen(name)) return name;
            }
            return null;
        }

        /// <summary>
        /// Someone at the counter you can't serve yet: they want a country you have no section for
        /// (all you can say is no), or one whose pages went out of date. Says where: COMPUTER, then SHOP.
        /// </summary>
        Step DayTwoNudge()
        {
            float rep = S.Standing.Reputation;
            foreach (var v in S.Visits)
            {
                if (v.Phase == VisitPhase.Gone || v.Moving || v.Leaving || v.Customer == null) continue;
                var c = v.Request != null ? S.Country(v.Request.CountryId) : v.Order != null ? S.Country(v.Order.CountryId) : null;
                if (c == null || c.FullGameOnly) continue;
                string pron = v.Customer.Identity.Sex == Sex.Male ? "him" : "her";
                bool choosing = v.Phase == VisitPhase.Choosing && v.Order == null && v.Choices.Count > 0;
                bool onlyNo = choosing && v.Choices.Count == 1 && v.Choices[0].Id == GameSession.ChoiceIds.Decline;
                if (onlyNo && !c.Owned && c.Requestable(rep))
                    return ShopNudge(c, $"{v.Customer.FirstName} wants {c.Spec.Name}. No section, no job. Click the COMPUTER, then SHOP: it's {Labels.Money(c.Spec.HandbookPrice)}. Or tell {pron} no.",
                        $"{c.Spec.Name}: BUY. Then {v.Customer.FirstName} gets a real answer.", "shop-buy-" + c.Id);
                bool beforePhoto = (choosing && !onlyNo) || (v.Phase == VisitPhase.PhotoSitting && v.Order != null && S.Photos.ForCustomer(v.Customer.Id).Count == 0);
                if (beforePhoto && c.Owned && c.HandbookOutdated)
                    return ShopNudge(c, $"Your {c.Spec.Name} pages are OUT OF DATE and this is a {c.Spec.Adjective} job. Click the COMPUTER, then SHOP: the update is {Labels.Money(Balance.HandbookUpdateCost)}.",
                        $"{c.Spec.Name}: UPDATE. The NEWS says what changed.", "shop-update-" + c.Id);
            }
            return default;
        }

        Step ShopNudge(CountryState c, string text, string inShop, string button)
        {
            var pc = _game.Find<ComputerView>();
            if (pc == null) return Make(NudgeSlot, text, "hit-crt");
            if (pc.CurrentApp != "shop") return Make(NudgeSlot, $"SHOP. {c.Spec.Name}'s pages are in there.", "nav-shop");
            return Make(NudgeSlot, inShop, button);
        }

        /// <summary>The destination's handbook tab, unless it's already open.</summary>
        string TabTarget(string countryId)
        {
            var tab = _game.Stage?.Q("handbook-tab-" + countryId);
            return tab != null && tab.ClassListContains("le-tab--active") ? null : "handbook-tab-" + countryId;
        }

        // ---------------- what's in the way

        static bool IsChoiceTarget(string name) => name == "choices" || name.StartsWith("choice-", StringComparison.Ordinal);

        static bool IsShopTarget(string name) =>
            name.StartsWith("hit-", StringComparison.Ordinal) || IsChoiceTarget(name)
            || name.StartsWith("booklet-", StringComparison.Ordinal) || name.StartsWith("page-", StringComparison.Ordinal)
            || name.StartsWith("cash-", StringComparison.Ordinal) || name.StartsWith("sheet-", StringComparison.Ordinal);

        /// <summary>A target in the shop under an open screen: point at that screen's way out instead.</summary>
        Step Unblock(Step step)
        {
            var top = _game.Top;
            if (step.Target == null || top == null) return step;
            if (top is ConfirmView)
            {
                if (step.Target != "confirm-ok") step.Target = step.Fallback = null;
                return step;
            }
            if (!IsShopTarget(step.Target) || (IsChoiceTarget(step.Target) && top is HandbookView)) return step;
            step.Fallback = null;
            step.Watch = false; // the way out is a real click
            step.Arrow = null;
            switch (top)
            {
                case ComputerView _:
                    step.Target = "os-close";
                    step.Extra = "Close the computer first.";
                    break;
                case HandbookView _:
                    step.Target = null;
                    step.Extra = "Close the handbook first (H).";
                    break;
                case PassportView _:
                    step.Target = "passport-close";
                    step.Extra = "Close the booklet first.";
                    break;
                case SlipView _:
                    step.Target = "slip-close";
                    step.Extra = "Put the sheet down first.";
                    break;
                case PrintMenu _:
                    step.Target = "print-leave";
                    step.Extra = "Close this first.";
                    break;
                case CameraView _:
                    step.Target = "cam-away";
                    step.Extra = "Step away from the camera first.";
                    break;
                default:
                    step.Target = null;
                    step.Extra = "Close this first.";
                    break;
            }
            return step;
        }

        /// <summary>What the note must not cover on the current screen, and where it'd rather sit.</summary>
        void Context(string slot)
        {
            _keepClear.Clear();
            var top = _game.Top;
            _preferBottom = top is CameraView || top is ReviewView || top is CropView;
            _sideSpot = top is CameraView;
            switch (top)
            {
                case CameraView _:
                    _keepClear.Add(new Rect(136f, 76f, 688f, 908f));   // the viewfinder
                    _keepClear.Add(new Rect(136f, 984f, 688f, 56f));   // what's on the side table
                    _keepClear.Add(new Rect(880f, 10f, 940f, 70f));    // what he's saying
                    _keepClear.Add(new Rect(880f, 100f, 940f, 600f));  // backdrop, framing, SAY
                    _keepClear.Add(new Rect(1100f, 1004f, 730f, 70f)); // HANDBOOK / STEP AWAY / DONE
                    break;
                case ReviewView _:
                case CropView _:
                    _keepClear.Add(new Rect(140f, 170f, 680f, 800f));  // the photo
                    _keepClear.Add(new Rect(140f, 36f, 1220f, 130f));  // the heading and how-to line
                    break;
                case HandbookView _:
                    if (slot == "rules")
                    {
                        _keepClear.Add(new Rect(840f, 50f, 730f, 980f));
                        _keepClear.Add(new Rect(110f, 50f, 730f, 260f)); // the left page's header rows
                    }
                    else _keepClear.Add(new Rect(110f, 50f, 730f, 980f));
                    break;
                case ComputerView _:
                    _keepClear.Add(new Rect(14f, 110f, 310f, 820f));   // the pinned client sheet
                    _keepClear.Add(new Rect(376f, 56f, 1268f, 56f));   // the OS title bar: date, cash, X
                    _keepClear.Add(new Rect(380f, 110f, 200f, 810f));  // the OS nav
                    break;
            }
        }

        // ================================================================== drawing

        void Render(Step step, float dt)
        {
            // Day 2's tour has its own short counter; its one-off notes have none.
            bool two = step.Slot.StartsWith("d2-", StringComparison.Ordinal);
            var flow = two ? Flow2 : Flow;
            var parts = two ? Parts2 : Parts;
            ref int best = ref (two ? ref _best2 : ref _best);
            int index = Array.IndexOf(flow, step.Slot) + 1;
            if (index > best)
            {
                if (best > 0)
                {
                    _stampT = StampSeconds;
                    _stamp.text = Stamps[_stamps++ % Stamps.Length];
                    Sfx.Play(SfxId.Pop, 0.3f, 1.5f);
                }
                best = index;
            }

            // The counter counts parts of the job, from the furthest step reached, so it never
            // skips numbers or runs backwards when a redo sends him back to the camera.
            int part = PartOf(flow, parts, best - 1);
            bool counted = !step.Final && index > 0;
            _title.SetTextIfChanged(step.Final ? "THAT'S THE JOB." : "NOTE FROM R.");
            _count.SetTextIfChanged(counted ? $"{parts[part].Name}  {part + 1}/{parts.Length}" : "");
            _text.SetTextIfChanged(step.Text ?? "");

            var target = Locate(step.Target, out string scroll, out var targetEl);
            if (!target.HasValue && step.Fallback != null) target = Locate(step.Fallback, out scroll, out targetEl);
            string warn = step.Extra ?? scroll;
            string extra = warn ?? step.Aside ?? "";
            _extra.SetTextIfChanged(extra);
            _extra.Show(extra.Length > 0);
            _extra.EnableInClassList("le-coach__extra--aside", warn == null && extra.Length > 0);
            _note.EnableInClassList("le-coach__note--final", step.Final);

            // The stamp sits over the counter (never the instructions); the counter comes back as it fades.
            if (_stampT > 0f) _stampT = Mathf.Max(0f, _stampT - dt);
            float stamp = Mathf.Clamp01(_stampT / 0.3f);
            _stamp.style.opacity = stamp;
            _count.style.opacity = 1f - stamp;
            _note.EnableInClassList("le-coach__note--done", _stampT > StampSeconds - 0.25f);

            string key = step.Slot + "|" + step.Target;
            Rect? avoid = null;
            if (target.HasValue)
            {
                var r = target.Value;
                var ring = new Rect(r.x - 8f, r.y - 8f, r.width + 16f, r.height + 16f);
                _ring.style.left = Snap(ring.x);
                _ring.style.top = Snap(ring.y);
                _ring.style.width = Snap(ring.width);
                _ring.style.height = Snap(ring.height);
                _ring.style.opacity = step.Watch
                    ? 0.3f + 0.15f * (0.5f + 0.5f * Mathf.Sin(_time * 2.5f))
                    : 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(_time * 6f));
                _ring.Show(true);
                // Watching (the printer at work): a ring says "look here", an arrow would say "click".
                _arrow.Show(!step.Watch);
                avoid = step.Watch ? ring : Union(ring, PlaceArrow(r, targetEl, key, step.Arrow, dt));
            }
            else
            {
                _ring.Show(false);
                _arrow.Show(false);
            }
            // A target that isn't laid out yet (a screen that was just rebuilt) docks again next frame.
            Dock(avoid, target.HasValue || step.Target == null ? key : null);
        }

        static int PartOf(string[] flow, (string Name, string Last)[] parts, int flowIndex)
        {
            for (int p = 0; p < parts.Length; p++)
                if (flowIndex <= Array.IndexOf(flow, parts[p].Last)) return p;
            return parts.Length - 1;
        }

        /// <summary>
        /// Puts the arrow next to the target, tip first, bobbing; returns where it went. The side is
        /// picked once per step: above if that's clear, else whichever side covers the least of
        /// the labels and buttons around it (never the neighbouring control's text).
        /// </summary>
        Rect PlaceArrow(Rect r, VisualElement targetEl, string key, int? fixedDir, float dt)
        {
            bool moved = Mathf.Abs(r.x - _arrowFor.x) > 24f || Mathf.Abs(r.y - _arrowFor.y) > 24f
                || Mathf.Abs(r.width - _arrowFor.width) > 24f || Mathf.Abs(r.height - _arrowFor.height) > 24f;
            if (key != _arrowKey || moved)
            {
                _arrowKey = key;
                _arrowAge = 0f;
            }
            // Pick during the first moments of a step, while the screen settles; then hold still.
            if (_arrowAge < 0.3f)
            {
                _arrowFor = r;
                _arrowSide = fixedDir ?? PickSide(r, targetEl);
            }
            _arrowAge += dt;

            int dir = _arrowSide;
            if (dir != _arrowDir)
            {
                _arrows[dir] ??= DemoArt.Pointer(dir);
                _arrow.Upload(_arrows[dir]);
                _arrowDir = dir;
            }
            float bob = Mathf.Round((0.5f + 0.5f * Mathf.Sin(_time * 5f)) * 3f) * ArrowScale;
            var a = ArrowRect(r, dir, bob);
            float x = Snap(Mathf.Clamp(a.x, 0f, GameRoot.StageWidth - ArrowSize));
            float y = Snap(Mathf.Clamp(a.y, 0f, GameRoot.StageHeight - ArrowSize));
            _arrow.style.left = x;
            _arrow.style.top = y;
            return new Rect(x, y, ArrowSize, ArrowSize);
        }

        /// <summary>Where the arrow goes for a side, tip on the target's frame, pushed out by <paramref name="bob"/>.</summary>
        static Rect ArrowRect(Rect r, int dir, float bob)
        {
            var tip = DemoArt.PointerTip(dir);
            float x, y;
            switch (dir)
            {
                case DemoArt.PointerUp: // below the target
                    x = r.center.x - (tip.x + 0.5f) * ArrowScale;
                    y = r.yMax + 6f - tip.y * ArrowScale + bob;
                    break;
                case DemoArt.PointerLeft: // right of the target
                    x = r.xMax + 6f - tip.x * ArrowScale + bob;
                    y = r.center.y - (tip.y + 0.5f) * ArrowScale;
                    break;
                case DemoArt.PointerRight: // left of the target
                    x = r.xMin - 6f - (tip.x + 1) * ArrowScale - bob;
                    y = r.center.y - (tip.y + 0.5f) * ArrowScale;
                    break;
                default: // above the target
                    x = r.center.x - (tip.x + 0.5f) * ArrowScale;
                    y = r.yMin - 6f - (tip.y + 1) * ArrowScale - bob;
                    break;
            }
            return new Rect(x, y, ArrowSize, ArrowSize);
        }

        int PickSide(Rect r, VisualElement targetEl)
        {
            CollectObstacles(targetEl);
            int best = DemoArt.PointerDown;
            float bestScore = float.MaxValue;
            foreach (var (dir, cost) in ArrowSides)
            {
                var swept = Union(ArrowRect(r, dir, 0f), ArrowRect(r, dir, BobTravel));
                float s = cost;
                if (swept.xMin < 0f || swept.yMin < 0f || swept.xMax > GameRoot.StageWidth || swept.yMax > GameRoot.StageHeight) s += 1e6f;
                foreach (var o in _obstacles) s += OverlapArea(swept, o);
                if (s < bestScore)
                {
                    bestScore = s;
                    best = dir;
                }
            }
            return best;
        }

        /// <summary>Stage boxes of the text and buttons on the screen in front, except the target's own.</summary>
        void CollectObstacles(VisualElement target)
        {
            _obstacles.Clear();
            var scope = _game.Top?.Root ?? _game.Stage;
            if (scope == null || _game.Stage == null) return;
            scope.Query<TextElement>().ForEach(e =>
            {
                if (!(e is Button) && string.IsNullOrEmpty(e.text)) return;
                if (!(e is Button) && e.GetFirstAncestorOfType<Button>() != null) return; // counted with its button
                if (target != null && (IsInside(e, target) || IsInside(target, e))) return;
                if (IsInside(e, Root) || HasAncestorClass(e, "le-toasts") || !Visible(e)) return;
                var wb = e.worldBound;
                if (float.IsNaN(wb.x) || float.IsNaN(wb.y) || !(wb.width >= 1f) || !(wb.height >= 1f)) return;
                _obstacles.Add(_game.Stage.WorldToLocal(wb));
            });
        }

        static bool IsInside(VisualElement e, VisualElement ancestor)
        {
            for (var p = e; p != null; p = p.hierarchy.parent)
                if (p == ancestor) return true;
            return false;
        }

        static bool HasAncestorClass(VisualElement e, string className)
        {
            for (var p = e; p != null; p = p.hierarchy.parent)
                if (p.ClassListContains(className)) return true;
            return false;
        }

        /// <summary>
        /// Docks the note in a stage corner (or the camera's empty right column): never over its own
        /// target or the arrow, clear of what the player needs to see, ideally on the other side.
        /// It only moves when the step changes or the target would end up underneath it.
        /// </summary>
        void Dock(Rect? avoid, string key)
        {
            float h = _note.resolvedStyle.height;
            if (float.IsNaN(h) || h < 40f) h = 240f;
            float right = GameRoot.StageWidth - Gutter - NoteW;
            float bottom = GameRoot.StageHeight - Gutter - h;
            var spots = new List<Vector2> { new Vector2(Gutter, Gutter), new Vector2(right, Gutter), new Vector2(Gutter, bottom), new Vector2(right, bottom) };
            if (_sideSpot) spots.Add(new Vector2(right, Mathf.Max(Gutter, 996f - h)));
            if (_dock >= spots.Count) _dock = -1;

            bool keep = _dock >= 0 && key != null && key == _dockKey
                && !(avoid.HasValue && new Rect(spots[_dock], new Vector2(NoteW, h)).Overlaps(avoid.Value));
            if (!keep)
            {
                float best = float.MaxValue;
                int pick = 0;
                for (int i = 0; i < spots.Count; i++)
                {
                    var r = new Rect(spots[i], new Vector2(NoteW, h));
                    float s = 0f;
                    if (avoid.HasValue && r.Overlaps(avoid.Value)) s += 1e6f;
                    // Covering what the player reads always costs more than the side preference below.
                    foreach (var k in _keepClear) s += OverlapArea(r, k) * 0.5f;
                    bool left = spots[i].x < GameRoot.StageWidth * 0.5f;
                    if (avoid.HasValue && left == avoid.Value.center.x < GameRoot.StageWidth * 0.5f) s += 3000f;
                    bool top = i < 2;
                    if (top == _preferBottom) s += 600f;
                    if (i == 1) s += 700f; // the toasts live up there
                    if (i == _dock) s -= 1f;
                    if (s < best)
                    {
                        best = s;
                        pick = i;
                    }
                }
                _dock = pick;
                if (key != null) _dockKey = key;
            }
            _note.style.left = Snap(spots[_dock].x);
            _note.style.top = Snap(spots[_dock].y);
        }

        // ---------------- finding things on the stage

        bool OnScreen(string name)
        {
            var e = _game.Stage?.Q(name);
            return e != null && Visible(e);
        }

        /// <summary>
        /// The named element's box in stage space, or null when it isn't on screen. Inside a scroll
        /// view the box is clipped to what's visible; scrolled out of sight, it hugs the edge it's past.
        /// </summary>
        Rect? Locate(string name, out string scroll, out VisualElement element)
        {
            scroll = null;
            element = null;
            if (string.IsNullOrEmpty(name)) return null;
            var e = _game.Stage?.Q(name);
            if (e == null || !Visible(e)) return null;
            var wb = e.worldBound;
            // Not laid out yet: NaN fails every comparison, so test for the good case.
            if (float.IsNaN(wb.x) || float.IsNaN(wb.y) || !(wb.width >= 1f) || !(wb.height >= 1f)) return null;
            element = e;
            for (var p = e.hierarchy.parent; p != null; p = p.hierarchy.parent)
            {
                if (!(p is ScrollView sv)) continue;
                var vp = sv.contentViewport.worldBound;
                if (wb.yMin >= vp.yMax - 4f)
                {
                    wb = new Rect(wb.x, vp.yMax - 12f, wb.width, 12f);
                    scroll = "Scroll down.";
                }
                else if (wb.yMax <= vp.yMin + 4f)
                {
                    wb = new Rect(wb.x, vp.yMin, wb.width, 12f);
                    scroll = "Scroll up.";
                }
                else
                {
                    float y0 = Mathf.Max(wb.yMin, vp.yMin), y1 = Mathf.Min(wb.yMax, vp.yMax);
                    wb = new Rect(wb.x, y0, wb.width, y1 - y0);
                }
            }
            return _game.Stage.WorldToLocal(wb);
        }

        bool Visible(VisualElement e)
        {
            if (e.panel == null) return false;
            for (var p = e; p != null && p != _game.Stage; p = p.hierarchy.parent)
            {
                var st = p.resolvedStyle;
                if (st.display == DisplayStyle.None || st.visibility == Visibility.Hidden || st.opacity < 0.05f) return false;
            }
            return true;
        }

        static float Snap(float v) => Mathf.Round(v / ArrowScale) * ArrowScale;

        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        static float OverlapArea(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0f && h > 0f ? w * h : 0f;
        }
    }
}
