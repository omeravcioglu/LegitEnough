#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegitEnough.Game;
using LegitEnough.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnoughCI
{
    /// <summary>
    /// Batch-mode play-through of the DEMO, used for verification on a throwaway copy of the
    /// project. Enters play mode and plays the demo end to end through public APIs and the
    /// named UI elements (buttons get a submit event, shop hit areas a click): title, settings,
    /// the studio splash, the coached Day 1 (Samir, France, the scripted jam), the scripted Day 2
    /// (Turkey, the Japan teaser, the locked FULL GAME screens, Germany and the France update, the
    /// rush job, the German client), the Day-2 report and the demo-end screen. Renders the UI into
    /// a RenderTexture and saves numbered PNGs, then exits with 0 (clean) or 1 (errors logged,
    /// failed checks or a timeout).
    /// Run: Unity -batchmode -projectPath COPY -executeMethod LegitEnoughCI.SmokeTest.Run
    /// </summary>
    public static class SmokeTest
    {
        sealed class Step
        {
            public string Name;
            public Func<bool> Until;
            public Action Do;
            public float Delay = 0.35f;
            public string Shot;
            public float Timeout = 40f;
            /// <summary>Checked when the step comes up: true skips it (no action, no picture).</summary>
            public Func<bool> Skip;
        }

        static readonly List<Step> Steps = new List<Step>();
        static readonly List<string> Errors = new List<string>();
        static readonly HashSet<string> ToastsSeen = new HashSet<string>();
        /// <summary>Every line a customer said (Watch records them; some are checked word for word).</summary>
        static readonly HashSet<string> LinesSeen = new HashSet<string>();
        /// <summary>The customer whose job the script is on: other visitors due meanwhile are moved back (see Watch).</summary>
        static int _busyId;
        /// <summary>Added to picture names while a job runs a second time (the FIX IT retake).</summary>
        static string _shotSuffix = "";
        static string _out;
        static int _index;
        static double _phaseStart;
        static int _phase; // 0 = delay, 1 = until, 2 = do, 3 = shot-wait
        static RenderTexture _rt;
        static PhotoCapture _capture;
        static int _frames;
        static int _jump = -1;
        static int _retakes;
        static int _reprints;
        static int _shots;
        static double _nudgeAt;
        static double _jamSeenAt = -1;
        static int _samirOrder = -1;
        static int _wishlistOpens;
        static int _markFrame;
        // Kenji (Japan) may walk in while another job is still on the bench: he's answered the
        // moment he asks, whatever step the script is on (see Watch).
        static string _kenjiShot;
        static double _kenjiAsked = -1;
        static bool _kenjiDone;

        static GameRoot G => GameRoot.Instance;
        static GameSession S => G.Session;

        public static void Run()
        {
            _out = Environment.GetEnvironmentVariable("LE_SMOKE_OUT");
            if (string.IsNullOrEmpty(_out)) _out = Path.GetFullPath("SmokeOut");
            Directory.CreateDirectory(_out);
            File.WriteAllText(Path.Combine(_out, "log.txt"), "smoke test start " + DateTime.Now + "\n");
            Application.logMessageReceived += OnLog;
            EditorApplication.playModeStateChanged += OnPlayMode;
            // The player build is the demo; say so explicitly in case the default ever changes.
            GameRoot.DemoBuild = true;
            BuildScript();
            // Keep this static driver alive across entering play mode (throwaway copy only).
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying = true;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            File.AppendAllText(Path.Combine(_out, "log.txt"), $"[{type}] {msg}\n");
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                if (msg.Contains("Curl error") || msg.Contains("licens")) return;
                Errors.Add(msg + "\n" + stack);
            }
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode)
            {
                _index = 0;
                _phase = 0;
                _phaseStart = EditorApplication.timeSinceStartup;
                EditorApplication.update += Drive;
            }
        }

        static void Note(string text) =>
            File.AppendAllText(Path.Combine(_out, "log.txt"), $"## [{EditorApplication.timeSinceStartup:0.0}s f{Time.frameCount}] {text}\n");

        /// <summary>A failed expectation: logged, counted, and the run carries on.</summary>
        static void Check(bool ok, string what)
        {
            if (ok) return;
            Errors.Add("CHECK FAILED: " + what + $"  (step '{(_index < Steps.Count ? Steps[_index].Name : "?")}', {S?.Now})");
            Note("CHECK FAILED: " + what);
        }

        static void Drive()
        {
            try
            {
                Watch();
                DriveInner();
            }
            catch (Exception e)
            {
                Errors.Add("DRIVER EXCEPTION in step " + (_index < Steps.Count ? Steps[_index].Name : "?") + ": " + e);
                Finish();
            }
        }

        /// <summary>
        /// Every frame: remember every toast that showed (some only live 3 s), and answer the
        /// "is it ready yet?" complaint a waiting customer may make while the script works.
        /// </summary>
        static void Watch()
        {
            if (G == null || G.Stage == null || S == null) return;
            G.Stage.Query<Label>(className: "le-toast").ForEach(l =>
            {
                if (!string.IsNullOrEmpty(l.text) && ToastsSeen.Add(l.text)) Note("toast: " + l.text);
            });
            foreach (var v in S.Visits.ToArray())
            {
                if (v.Line != null && !string.IsNullOrEmpty(v.Line.Text) && LinesSeen.Add(v.Line.Text))
                    Note($"{v.Customer.FirstName} says: {v.Line.Text}");
                if (v.Phase == VisitPhase.Choosing && v.Choices.Count == 1 && v.Choices[0].Id == GameSession.ChoiceIds.AlmostDone)
                {
                    Note($"{v.Customer.FirstName} asks if it's ready: almost done");
                    S.Choose(v.Id, GameSession.ChoiceIds.AlmostDone);
                }
            }
            HoldArrivals();
            WatchKenji();
        }

        /// <summary>
        /// The Day-2 visitors come close together (a player juggles them; this script does one job
        /// at a time). While a job runs, anyone else due within a minute is moved back 15 minutes,
        /// so the counter is never blocked by the next visitor waiting for an answer. Kenji is the
        /// exception: Watch answers him whenever he asks. Arrival times are checked at opening, before this.
        /// </summary>
        static void HoldArrivals()
        {
            if (_busyId == 0 || S.Phase != DayPhase.Open) return;
            var soon = S.Now.AddMinutes(1);
            foreach (var a in S.Director.Upcoming.ToArray())
            {
                if (a.Kind != ArrivalKind.Story || a.CustomerId == _busyId || a.CustomerId == DemoCast.KenjiId || a.At > soon) continue;
                var at = S.Now.AddMinutes(15);
                Note($"hold: customer {a.CustomerId} due {a.At.Clock} moved to {at.Clock} (busy with {_busyId})");
                S.Director.Reschedule(a, at);
            }
        }

        /// <summary>
        /// The Japan teaser: Kenji gets the decline line only and the sim toasts "Japan is in the
        /// full game.". He holds the counter until answered, so he's answered here, a moment after
        /// he asks (long enough for the toast to show in the picture).
        /// </summary>
        static void WatchKenji()
        {
            if (_kenjiDone || _kenjiShot == null) return;
            var v = VisitOf(DemoCast.KenjiId);
            if (v == null || v.Phase != VisitPhase.Choosing || v.Choices.Count == 0) return;
            double now = EditorApplication.timeSinceStartup;
            if (_kenjiAsked < 0) _kenjiAsked = now;
            if (now - _kenjiAsked < 0.8) return;
            _kenjiDone = true;
            Note($"kenji asks for {v.Request?.CountryId}: choices {ChoiceIds(v)}");
            Check(v.Request != null && v.Request.CountryId == Countries.Japan, "Kenji wants Japan");
            Check(ChoiceIds(v) == GameSession.ChoiceIds.Decline, "Japan: the decline line only, got " + ChoiceIds(v));
            // The sim's toast gets the demo's wishlist line added once (never twice).
            Check(ToastIs("Japan is in the full game. Wishlist us on Steam!"), "the Japan teaser toast with one wishlist line");
            Check(!ToastsSeen.Any(t => t.Contains("Steam! Wishlist") || t.Contains("Steam!  Wishlist")), "no doubled wishlist line");
            Shot(_kenjiShot);
            S.Choose(v.Id, GameSession.ChoiceIds.Decline);
        }

        static void DriveInner()
        {
            if (_index >= Steps.Count)
            {
                Finish();
                return;
            }
            var step = Steps[_index];
            double now = EditorApplication.timeSinceStartup;
            switch (_phase)
            {
                case 0:
                    if (now - _phaseStart < step.Delay) return;
                    if (step.Skip != null && step.Skip())
                    {
                        Note($"{step.Name}  (skipped)");
                        _index++;
                        _phaseStart = now;
                        return;
                    }
                    _phase = 1;
                    _phaseStart = now;
                    return;
                case 1:
                    if (step.Until != null && !step.Until())
                    {
                        if (now - _phaseStart > step.Timeout)
                        {
                            Errors.Add($"TIMEOUT waiting in step '{step.Name}' ({S?.Phase} {S?.Now}, top {G?.Top?.GetType().Name})");
                            Finish();
                        }
                        return;
                    }
                    _phase = 2;
                    return;
                case 2:
                    Note($"{step.Name}  (sim {S?.Now}, phase {S?.Phase}, top {G?.Top?.GetType().Name})");
                    step.Do?.Invoke();
                    _phase = 3;
                    _phaseStart = now;
                    _frames = 0;
                    return;
                case 3:
                    _frames++;
                    if (now - _phaseStart < 0.45 || _frames < 6) return;
                    if (step.Shot != null) Shot(step.Shot);
                    if (_jump >= 0)
                    {
                        _index = _jump;
                        _jump = -1;
                    }
                    else _index++;
                    _phase = 0;
                    _phaseStart = now;
                    return;
            }
        }

        static void Shot(string name)
        {
            if (_rt == null) return;
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(_out, name + _shotSuffix + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        /// <summary>An unplanned picture (a random jam, a smudge): numbered after the current step's own shot.</summary>
        static void ExtraShot(string name)
        {
            string own = _index < Steps.Count ? Steps[_index].Shot : null;
            Shot((own != null && own.Length > 3 ? own.Substring(0, 3) + "b_" : "x_") + name);
        }

        static void Finish()
        {
            EditorApplication.update -= Drive;
            File.AppendAllText(Path.Combine(_out, "log.txt"), $"\nDONE: {_index}/{Steps.Count} steps, {Errors.Count} errors\n");
            File.WriteAllText(Path.Combine(_out, "errors.txt"), string.Join("\n-----\n", Errors));
            EditorApplication.Exit(Errors.Count == 0 && _index >= Steps.Count ? 0 : 1);
        }

        /// <summary>Adds a step. Shots are numbered in script order: "007_samir_offer".</summary>
        static void Add(string name, Action action, string shot = null, Func<bool> until = null, float delay = 0.35f, float timeout = 40f, Func<bool> skip = null)
        {
            if (shot != null) shot = $"{++_shots:000}_{shot}";
            Steps.Add(new Step { Name = name, Do = action, Shot = shot, Until = until, Delay = delay, Timeout = timeout, Skip = skip });
        }

        static void Goto(string name) => _jump = Steps.FindIndex(s => s.Name == name);

        // ------------------------------------------------------------------ UI input

        /// <summary>Presses a named button the way a keyboard/gamepad submit does.</summary>
        static void Submit(string name)
        {
            var el = G.Stage.Q(name);
            Check(el != null, "element '" + name + "' exists");
            if (el == null) return;
            Check(el.enabledInHierarchy, "element '" + name + "' is enabled");
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = el;
                el.SendEvent(e);
            }
        }

        /// <summary>Clicks a named element that listens for clicks (shop hit areas, tabs, rows).</summary>
        static void Click(string name)
        {
            var el = G.Stage.Q(name);
            Check(el != null, "element '" + name + "' exists");
            if (el != null) Click(el);
        }

        static void Click(VisualElement el)
        {
            using (var e = ClickEvent.GetPooled())
            {
                e.target = el;
                el.SendEvent(e);
            }
        }

        static void Names(params string[] names)
        {
            foreach (var n in names) Check(G.Stage.Q(n) != null, "element '" + n + "' exists");
        }

        static bool HasText(VisualElement root, string text)
        {
            bool found = false;
            root?.Query<TextElement>().ForEach(l =>
            {
                if (l.text != null && l.text.Contains(text)) found = true;
            });
            return found;
        }

        static void Scroll(string className, float y)
        {
            var sv = G.Stage.Q<ScrollView>(className: className);
            if (sv != null) sv.scrollOffset = new Vector2(0f, y);
        }

        static bool CoachShown() =>
            G.Coach != null && G.Coach.Active && G.Coach.Root.resolvedStyle.display == DisplayStyle.Flex;

        // ------------------------------------------------------------------ sim helpers

        static Visit VisitOf(int customerId) => S.Visits.Find(v => v.Customer.Id == customerId && v.Phase != VisitPhase.Gone);

        static Visit Samir => VisitOf(StoryCast.SamirId);

        static Order OrderOf(int customerId)
        {
            Order found = null;
            foreach (var o in S.Orders.All)
                if (o.CustomerId == customerId) found = o;
            return found;
        }

        static string ChoiceIds(Visit v) => v == null ? "" : string.Join(",", v.Choices.ConvertAll(c => c.Id));

        /// <summary>
        /// The next job is this customer's: no one in the shop, so skip the clock to a minute before
        /// they're due (if they're still to come).
        /// </summary>
        static void SkipToArrival(int customerId)
        {
            _busyId = customerId;
            if (S.Visits.Exists(v => v.Phase != VisitPhase.Gone)) return;
            foreach (var a in S.Director.Upcoming)
            {
                if (a.CustomerId != customerId) continue;
                double minutes = GameTime.MinutesBetween(S.Now, a.At) - 1.0;
                if (minutes <= 0) return;
                Note($"skip {S.Now.Clock} -> {S.Now.AddMinutes(minutes).Clock} (customer {customerId} due {a.At.Clock})");
                S.DebugAdvanceMinutes((float)minutes);
                return;
            }
        }

        /// <summary>
        /// The demo's visitors count from when the sign flips (their beat times assume 09:00), never
        /// past the last arrival of the day. Checked right after opening.
        /// </summary>
        static void CheckArrivalsFromOpening(float openedAt, params int[] customerIds)
        {
            foreach (int id in customerIds)
            {
                ScheduledArrival found = null;
                foreach (var a in S.Director.Upcoming)
                    if (a.CustomerId == id) found = a;
                Check(found != null && found.Beat != null, $"customer {id} is due today");
                if (found == null || found.Beat == null) continue;
                float want = Math.Min(found.Beat.ArrivalMinute + (openedAt - 9 * 60), 17 * 60 + 15);
                Note($"customer {id} due {found.At.Clock} (beat {Calendar.ClockText(found.Beat.ArrivalMinute)}, opened {Calendar.ClockText(openedAt)})");
                Check(found.At.Day == S.Day && Math.Abs(found.At.Minute - want) < 0.05f,
                    $"customer {id} due {Calendar.ClockText(want)} (beat time counted from opening), got {found.At.Clock}");
            }
        }

        /// <summary>The button starting with <paramref name="label"/> in the row that says <paramref name="rowText"/> (shop and Border Watch rows).</summary>
        static Button RowButton(string rowText, string label)
        {
            Button found = null;
            G.Stage.Query<Button>().ForEach(b =>
            {
                if (found != null || b.text == null || !b.text.StartsWith(label)) return;
                for (var p = b.parent; p != null && p != G.Stage; p = p.parent)
                {
                    if (p.ClassListContains("le-os-row") || p.ClassListContains("le-os-shoprow"))
                    {
                        if (HasText(p, rowText)) found = b;
                        return;
                    }
                }
            });
            Check(found != null, $"a '{label}' button in the {rowText} row");
            return found;
        }

        /// <summary>Presses a button found by text, like <see cref="Submit"/>.</summary>
        static void Press(VisualElement el)
        {
            if (el == null) return;
            Check(el.enabledInHierarchy, "button '" + (el as Button)?.text + "' is enabled");
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = el;
                el.SendEvent(e);
            }
        }

        /// <summary>Waits like a player would: re-poses the subject when habits creep back in.</summary>
        static bool ReadyForShutter()
        {
            var sub = S.ActivePhoto.Subject;
            bool face = sub.Expression == Expression.Neutral;
            bool look = Math.Abs(sub.GazeX) < 0.3f && Math.Abs(sub.GazeY) < 0.3f;
            bool square = sub.Pose.Yaw == 0 && sub.Pose.Pitch == 0;
            if (face && look && square && !sub.InMotion && sub.Lid < 0.15f) return true;

            double now = EditorApplication.timeSinceStartup;
            if (now - _nudgeAt < 1.5) return false;
            _nudgeAt = now;
            if (!face) sub.Command(PhotoCommand.NeutralFace);
            else if (!square && !sub.InMotion)
            {
                sub.BeginDrag();
                sub.DragTo(0, 0);
                sub.EndDrag();
            }
            else if (!look) sub.Command(PhotoCommand.LookAtCamera);
            return false;
        }

        static bool BadTake(PhotoCapture c) =>
            c.EyesClosed || c.EyesHalfClosed || c.Blurred || c.Expression != Expression.Neutral ||
            Math.Abs(c.GazeX) > 0.3f || Math.Abs(c.GazeY) > 0.3f || c.Pose.Yaw != 0 || c.Pose.Pitch != 0;

        /// <summary>Glasses and hat as the country wants, zoomed in all the way, aimed at the head, held still.</summary>
        static void Pose(string countryId)
        {
            var ps = S.ActivePhoto;
            var sub = ps.Subject;
            var rules = S.Country(countryId).Spec.Photo;
            int guard = 0;
            while (sub.HasGlasses && (!rules.GlassesAllowed || sub.WearingSunglasses) && guard++ < 4) sub.Command(PhotoCommand.RemoveGlasses);
            if (sub.HatOn && !rules.HeadwearAllowed) sub.Command(PhotoCommand.RemoveHat);
            guard = 0;
            while (ps.Rig.Zoom < ps.Camera.MaxZoom - 0.01f && guard++ < 20) Submit("cam-zoom-in");
            var head = PortraitGeometry.HeadWorld(sub.Customer.Look, sub.Pose, sub.SeatOffset, sub.Slouch, sub.HatOn);
            ps.Rig.PanX = head.CenterX;
            ps.Rig.PanY = head.CenterY + 0.25f;
            ps.Rig.ClampTo(ps.Camera);
            sub.Command(PhotoCommand.HoldStill);
            sub.BeginDrag();
            sub.DragTo(0, 0);
            Note($"pose {countryId}: backdrop {ps.Rig.Backdrop}, glasses {sub.HasGlasses}, hat {sub.HatOn}, zoom {ps.Rig.Zoom:0.00}");
        }

        /// <summary>Everything the handbook asks for, typed in right (the GENERATE button and the photo pick come later).</summary>
        static void FillDraft(Order o)
        {
            var spec = S.Country(o.CountryId).Spec;
            var id = S.Customer(o.CustomerId).Identity;
            var d = o.Draft;
            d.Category = spec.Passport.Category;
            d.Cover = spec.Passport.Cover;
            d.Pages = spec.Passport.Pages;
            foreach (var f in spec.Passport.PersonalFields) d.Set(f, DocumentEvaluator.Expected(f, id, spec.Passport.DateFormat));
            var issue = Calendar.DateOf(S.Day);
            d.Set(DocField.DateOfIssue, Labels.FormatDate(issue, spec.Passport.DateFormat));
            d.Set(DocField.DateOfExpiry, Labels.FormatDate(issue.AddYears(spec.Passport.ValidityYears), spec.Passport.DateFormat));
        }

        /// <summary>Cheap printers jam at random: grab a picture of the jam, then hit the printer like a player would.</summary>
        static bool WaitForPrint(string tag, int orderId)
        {
            if (S.Printer.Jammed)
            {
                double now = EditorApplication.timeSinceStartup;
                if (_jamSeenAt < 0)
                {
                    _jamSeenAt = now;
                    Note("printer jammed");
                    return false;
                }
                if (now - _jamSeenAt < 0.6) return false;
                ExtraShot($"{tag}_printer_jam_random");
                Click("hit-printer");
                if (S.Printer.Jammed) S.ClearPrinterJam();
                _jamSeenAt = -1;
            }
            return S.Printer.Tray.Exists(p => p.OrderId == orderId);
        }

        /// <summary>The booklet goes to the customer once they can take it (at the counter, or from the bench when it's free).</summary>
        static bool TryHandOver(int customerId)
        {
            var v = VisitOf(customerId);
            var o = OrderOf(customerId);
            if (v == null || o == null || v.Moving) return false;
            var doc = S.DeskDocuments.Find(d => d.OrderId == o.Id);
            if (doc == null) return v.Phase == VisitPhase.Inspecting;
            if (v.Phase != VisitPhase.AwaitingHandover && v.Phase != VisitPhase.Waiting && v.Phase != VisitPhase.Choosing) return false;
            return S.GiveDocument(v.Id, doc.Id);
        }

        /// <summary>The editor thumbnail of a kept photo (a retake lists the old one too).</summary>
        static VisualElement PhotoThumb(int photoId)
        {
            VisualElement found = null;
            G.Stage.Query(className: "le-os-photo").ForEach(h =>
            {
                var pv = h.Q<PortraitView>();
                if (found == null && pv?.Capture != null && pv.Capture.Id == photoId) found = h;
            });
            return found;
        }

        /// <summary>Waits for the money; talks a doubting customer round if they find something.</summary>
        static bool Paid(int customerId)
        {
            var o = OrderOf(customerId);
            if (o != null && S.CounterCash.Exists(c => c.OrderId == o.Id)) return true;
            var v = VisitOf(customerId);
            if (v == null)
            {
                Check(false, $"customer {customerId} left without paying (order {o?.Status})");
                return true;
            }
            if (v.Phase == VisitPhase.Choosing && v.Choices.Exists(c => c.Id == GameSession.ChoiceIds.TrustMe))
            {
                Note($"{v.Customer.FirstName} found something ({ChoiceIds(v)}): trust me");
                S.Choose(v.Id, GameSession.ChoiceIds.TrustMe);
            }
            return false;
        }

        // ------------------------------------------------------------------ a whole job

        /// <summary>
        /// One passport from "deal" to cash: negotiation, photo, editor, print (and bind), check,
        /// hand over, get paid. <paramref name="tutorial"/> is Samir on Day 1: he haggles once, the
        /// coach must be up and the first page jams on purpose. <paramref name="wrongBackdropFirst"/>
        /// shoots the first photo on the old French background (light gray): the client spots it,
        /// FIX IT sends them back to the bench, the photo corner seats them again and the job runs a
        /// second time from the stool (pictures of that pass end in "_retake").
        /// </summary>
        static void Job(string tag, int customerId, bool tutorial, bool wrongBackdropFirst = false)
        {
            Func<Visit> who = () => VisitOf(customerId);
            Func<Order> order = () => OrderOf(customerId);
            string cid = null;
            bool retaken = !wrongBackdropFirst;
            Func<Backdrop> backdrop = () => retaken ? S.Country(cid).Spec.Photo.Backdrop : Backdrop.LightGray;

            Add($"{tag} begin", () => _busyId = customerId, null, null, 0f);
            Add($"{tag} talking", null, $"{tag}_walks_in", () => who() != null && who().Phase == VisitPhase.Talking && who().Line != null, 0.2f, 60f);
            Add($"{tag} offer", () =>
            {
                cid = who().Request.CountryId;
                Note($"{tag} wants {cid}: choices {ChoiceIds(who())}, arrived {who().ArrivedAt.Clock}, due {who().Request.Deadline}");
                if (who().Request.Urgent && who().Beat != null && who().Beat.DeadlineMinutes > 0)
                {
                    // The rush job is due a fixed time after he walks in (never past closing), and he says when.
                    var want = who().ArrivedAt.AddMinutes(who().Beat.DeadlineMinutes);
                    var closing = new GameTime(S.Day, 18 * 60);
                    if (want > closing) want = closing;
                    Check(Math.Abs(GameTime.MinutesBetween(want, who().Request.Deadline)) < 0.05,
                        $"{tag}: due {who().Beat.DeadlineMinutes} min after arriving ({want.Clock}), got {who().Request.Deadline.Clock}");
                    Check(LinesSeen.Any(l => l.Contains("Ready by " + who().Request.Deadline.Clock)), $"{tag} says the job is due at {who().Request.Deadline.Clock}");
                    Check(!LinesSeen.Any(l => l.Contains(DemoCast.DueToken)), "no {due} token left in what anyone says");
                }
                if (tutorial)
                {
                    Check(ChoiceIds(who()) == "accept,ask_small", "tutorial Samir: accept / ask small only, got " + ChoiceIds(who()));
                    Check(CoachShown(), "coach is up while Samir haggles");
                }
                else Check(who().Choices.Exists(c => c.Id == GameSession.ChoiceIds.Accept), tag + " can be accepted");
            }, $"{tag}_offer", () => who() != null && who().Phase == VisitPhase.Choosing && who().Choices.Count > 0, 0.35f, 60f);
            if (tutorial)
            {
                Add($"{tag} ask small", () => S.Choose(who().Id, GameSession.ChoiceIds.AskSmall), null);
                Add($"{tag} final offer", () => Check(who().Choices[0].Text == Lines.Player.AcceptFinal(450), "\"Fine. $450.\" is the only reply"),
                    $"{tag}_final_offer", () => who().Phase == VisitPhase.Choosing && ChoiceIds(who()) == "accept", 0.35f, 60f);
            }
            Add($"{tag} accept", () => S.Choose(who().Id, GameSession.ChoiceIds.Accept), null);
            Add($"{tag} photo choice", () => Note($"{tag} order #{order().Id}: {order().CountryId} {Labels.Money(order().Price)}{(order().Urgent ? " URGENT, due " + order().Deadline : "")}"),
                $"{tag}_after_accept", () => who().Phase == VisitPhase.Choosing && who().Choices.Exists(c => c.Id == GameSession.ChoiceIds.TakePhoto && c.Enabled), 0.35f, 60f);
            Add($"{tag} to stool", () => S.Choose(who().Id, GameSession.ChoiceIds.TakePhoto), null);
            Add($"{tag} seated", null, $"{tag}_on_stool", () => who().Phase == VisitPhase.PhotoSitting && !who().Moving && !who().IsTalking, 0.5f);
            Add($"{tag} open camera", () => Click("hit-tripod"), $"{tag}_camera_raw", null, 0.35f);
            Add($"{tag} camera up", () =>
            {
                _retakes = 0;
                _reprints = 0;
            }, null, () => G.Top is CameraView, 0.1f);
            if (tutorial)
            {
                // R. sends you to the handbook's photo page before the first shot.
                Add($"{tag} rules", () =>
                {
                    Submit("cam-handbook");
                    _markFrame = Time.frameCount;
                }, $"{tag}_handbook_photo_rules", null, 0.35f);
                // The coach counts the page as read after 0.6 s of game frames (dt is capped at 0.1 s).
                Add($"{tag} rules read", () =>
                {
                    Check(G.Top is HandbookView, "the handbook opened from the camera");
                    Check(G.Stage.Q("handbook-tab-" + cid)?.ClassListContains("le-tab--active") == true, "handbook opened on " + cid);
                    G.ToggleHandbook();
                }, null, () => Time.frameCount - _markFrame > 30, 1.2f);
            }
            Add($"{tag} backdrop", () => Submit("cam-backdrop-" + backdrop()), null);
            Add($"{tag} backdrop down", () => Check(S.ActivePhoto.Rig.Backdrop == backdrop(), "backdrop pulled: " + backdrop()),
                $"{tag}_backdrop", () => S.ActivePhoto != null && !S.ActivePhoto.Rig.SwappingBackdrop, 0.3f, 20f);
            Add($"{tag} pose", () => Pose(cid), null);
            Add($"{tag} posed", () =>
            {
                var sub = S.ActivePhoto.Subject;
                sub.EndDrag();
                if (sub.Expression != Expression.Neutral) sub.Command(PhotoCommand.NeutralFace);
                sub.Command(PhotoCommand.LookAtCamera);
            }, $"{tag}_camera_posed", null, 2.2f);
            Add($"{tag} shutter", () => (G.Top as CameraView)?.Shutter(), null, ReadyForShutter, 0.6f);
            Add($"{tag} review", () =>
            {
                _capture = (G.Top as ReviewView)?.Photo ?? S.ActivePhoto.Shots[S.ActivePhoto.Shots.Count - 1];
                if (BadTake(_capture) && _retakes++ < 6)
                {
                    Note($"retake: lid {_capture.Lid:0.00} blur {_capture.Blurred} {_capture.Expression} {_capture.Pose.Name} gaze {_capture.GazeX:0.0},{_capture.GazeY:0.0}");
                    Submit("review-retake");
                    Goto($"{tag} shutter");
                }
            }, $"{tag}_review", () => G.Top is ReviewView);
            Add($"{tag} crop", () =>
            {
                G.Top.Close();
                var rules = S.Country(cid).Spec.Photo;
                var head = _capture.HeadInFrame;
                float cropH = head.H / ((rules.CoverageMin + rules.CoverageMax) * 0.5f);
                _capture.Crop = PortraitGeometry.MakeCrop(head.CenterX, head.Y - 0.1f * cropH + cropH * 0.5f, cropH);
                G.Open(new CropView(G, _capture));
            }, $"{tag}_crop");
            Add($"{tag} keep", () =>
            {
                G.Top.Close();
                S.KeepPhoto(_capture);
                Note($"photo: {PhotoEvaluator.Score(_capture, S.Country(cid).Spec.Photo)} coverage {_capture.Coverage:0.00}");
            }, $"{tag}_camera_kept");
            Add($"{tag} done photos", () => Submit("cam-done"), null);
            Add($"{tag} bench", () => Check(!(G.Top is CameraView), "DONE closed the camera"), null,
                () => who().Phase == VisitPhase.Waiting && !who().Moving, 0.5f);

            // The client sheet, then the editor.
            Add($"{tag} slip", () => G.Open(new SlipView(G, order().Id)), $"{tag}_client_sheet");
            Add($"{tag} computer", () =>
            {
                Submit("slip-close");
                Click("hit-crt");
                G.Find<ComputerView>()?.OpenEditor(order().Id);
            }, $"{tag}_editor_template", () => true);
            if (tutorial)
            {
                // Day 1 is where the editor's FULL GAME locks show first.
                Add($"{tag} editor locks", () =>
                {
                    Names("editor-template", "tpl-FR", "tpl-TR", "tpl-JP", "tpl-IR", "editor-visa", "editor-format", "editor-data", "editor-photo", "editor-print");
                    Submit("tpl-JP");
                    Check(ToastIs("Japan is in the full game. Wishlist us on Steam!"), "locked template toast");
                }, $"{tag}_editor_locked_template");
                Add($"{tag} editor visa", () =>
                {
                    Submit("editor-visa");
                    Check(ToastIs("Visas are in the full game. Wishlist us on Steam!"), "locked VISA toast");
                }, $"{tag}_editor_locked_visa", null, 1.6f);
            }
            Add($"{tag} template", () =>
            {
                Submit("tpl-" + cid);
                Check(order().Draft.CountryId == cid, "template " + cid + " picked");
            }, $"{tag}_editor_template_picked");
            Add($"{tag} fill", () =>
            {
                FillDraft(order());
                G.Find<ComputerView>()?.OpenEditor(order().Id);
            }, null);
            Add($"{tag} number and photo", () =>
            {
                Submit("editor-docnumber");
                Check(Labels.Normalize(order().Draft.Get(DocField.DocumentNumber)).Length > 0, "GENERATE filled the number");
                var thumb = PhotoThumb(_capture.Id);
                Check(thumb != null, "kept photo listed in the editor");
                if (thumb != null) Click(thumb);
                Check(order().Draft.PhotoId == _capture.Id, "photo picked in the editor");
                if (order().Draft.PhotoId != _capture.Id) order().Draft.PhotoId = _capture.Id;
            }, $"{tag}_editor_filled");
            Add($"{tag} editor bottom", () => Scroll("le-os-form", 10000f), $"{tag}_editor_bottom");
            Add($"{tag} print", () =>
            {
                Submit("editor-print");
                Check(S.Printer.Busy || S.Printer.Jammed || S.Printer.Tray.Exists(p => p.OrderId == order().Id), "PRINT sent the page");
            }, $"{tag}_printing");
            Add($"{tag} close computer", () => Submit("os-close"), null);
            if (tutorial)
            {
                Add($"{tag} jam", () => Check(S.Printer.SafeJam, "the tutorial jam is the safe one"), $"{tag}_printer_jam",
                    () => S.Printer.Jammed, 0.3f, 90f);
                Add($"{tag} hit printer", () =>
                {
                    Click("hit-printer");
                    Check(!S.Printer.Jammed, "hitting the printer cleared the jam");
                }, $"{tag}_jam_cleared");
            }
            Add($"{tag} printed", () => _jamSeenAt = -1, $"{tag}_tray", () => WaitForPrint(tag, order().Id), 0.3f, 90f);
            Add($"{tag} print menu", () =>
            {
                var page = S.Printer.Tray.Find(p => p.OrderId == order().Id);
                if (S.Printer.Tray.Count == 1) Click("hit-printer");
                else G.Open(new PrintMenu(G, page.Id));
                Check(G.Top is PrintMenu, "the page opened in the print menu");
            }, $"{tag}_print_menu");
            Add($"{tag} bind", () =>
            {
                var page = S.Printer.Tray.Find(p => p.OrderId == order().Id);
                if (tutorial) Check(!page.Smudged, "the tutorial jam never smudges");
                if (page.Smudged && _reprints++ < 3)
                {
                    ExtraShot($"{tag}_print_menu_smudged");
                    Note("smudged page - shredding and reprinting");
                    Submit("print-leave");
                    S.ShredPage(page.Id);
                    S.PrintDraft(order().Id);
                    Goto($"{tag} printed");
                    return;
                }
                Submit("print-bind");
                Check(S.DeskDocuments.Exists(d => d.OrderId == order().Id), "booklet bound");
            }, $"{tag}_booklet_on_desk");
            Add($"{tag} inspect", () => G.Open(new PassportView(G, S.DeskDocuments.Find(d => d.OrderId == order().Id))), $"{tag}_passport");
            Add($"{tag} close passport", () => Submit("passport-close"), null);
            Add($"{tag} hand over", () => { }, null, () => TryHandOver(customerId), 0.3f, 60f);
            Add($"{tag} inspecting", null, $"{tag}_inspecting",
                () => who() == null || who().Phase == VisitPhase.Inspecting || S.CounterCash.Exists(c => c.OrderId == order().Id), 0.3f, 60f);
            if (wrongBackdropFirst)
            {
                Func<bool> skip = () => retaken;
                Add($"{tag} complaint", () =>
                {
                    var v = who();
                    Note($"{tag} complaint: {v?.Complaint.Issue} ({ChoiceIds(v)}) quality {order().Quality}");
                    bool fixIt = v != null && v.Choices.Exists(c => c.Id == GameSession.ChoiceIds.FixIt);
                    Check(fixIt && v.HasComplaint && v.Complaint.Issue == Issue.WrongBackdrop, $"{tag} spots the old background, got {v?.Complaint.Issue}");
                    Check(LinesSeen.Contains(DemoCast.UrgentBackdropComplaint), $"{tag} says France wants white now");
                    if (!fixIt) retaken = true; // nothing to fix: carry on to the cash
                }, $"{tag}_complaint_wrong_background",
                    () => who() == null || who().Phase == VisitPhase.Choosing && who().Choices.Exists(c => c.Id == GameSession.ChoiceIds.FixIt)
                          || S.CounterCash.Exists(c => c.OrderId == order().Id), 0.5f, 60f, skip);
                Add($"{tag} fix it", () => Submit("choice-" + GameSession.ChoiceIds.FixIt), null, null, 0.35f, 40f, skip);
                Add($"{tag} retake bench", () =>
                {
                    var o = order();
                    Check(LinesSeen.Contains(DemoCast.RetakeLine), $"{tag}: \"{DemoCast.RetakeLine}\"");
                    Check(o.NeedsRetake && o.Status == OrderStatus.AwaitingPhoto && o.Draft.PhotoId == null,
                        $"FIX IT over the photo: new photo needed (retake {o.NeedsRetake}, {o.Status}, photo {o.Draft.PhotoId})");
                    var old = S.DeskDocuments.Find(d => d.OrderId == o.Id);
                    Check(old != null, "the refused booklet is back on the desk");
                    if (old != null) S.ShredDocument(old.Id);
                    Check(o.Status == OrderStatus.AwaitingPhoto, "shredding the old booklet keeps the order waiting for a photo");
                }, $"{tag}_retake_bench", () => who() != null && who().Phase == VisitPhase.Waiting && !who().Moving && !who().IsTalking, 0.35f, 40f, skip);
                Add($"{tag} retake call", () =>
                {
                    Click("hit-photo");
                    Check(who().To == Spot.Stool, "the photo corner calls them back to the stool");
                    retaken = true;
                    _shotSuffix = "_retake";
                    Goto($"{tag} seated");
                }, null, null, 0.35f, 40f, skip);
            }
            Add($"{tag} paid", () =>
            {
                var o = order();
                Note($"{tag} quality: {o.Quality} :: {string.Join("; ", o.Quality.Findings)}");
                if (tutorial) Check(CoachShown(), "coach is up for the cash");
            }, $"{tag}_cash_on_counter", () => Paid(customerId), 0.5f, 90f);
            Add($"{tag} collect", () =>
            {
                var pile = S.CounterCash.Find(c => c.OrderId == order().Id);
                if (pile == null) return;
                Click("cash-" + pile.Id);
                Check(!S.CounterCash.Contains(pile), "clicking the cash collected it");
            }, null);
            Add($"{tag} gone", () =>
            {
                Check(order().Status == OrderStatus.Delivered, tag + " order delivered");
                _shotSuffix = "";
                _busyId = 0;
            }, null, () => who() == null, 0.5f, 60f);
        }

        static bool ToastIs(string text) => ToastsSeen.Contains(text) || HasText(G.Stage.Q(className: "le-toasts"), text);

        /// <summary>Flip the sign, confirm "Close early?", wait for the report.</summary>
        static void CloseEarly(string tag)
        {
            Add($"{tag} sign", () => Click("hit-sign"), $"{tag}_confirm_close_early", null, 0.5f);
            Add($"{tag} confirm", () =>
            {
                Check(G.Top is ConfirmView, "the sign asked to close early");
                bool lastDay = S.IsDemo && S.Day >= S.Rules.LastDay;
                Check(HasText(G.Top?.Root, "and the demo") == lastDay, lastDay ? "the last day says closing ends the demo" : "only the last day mentions the demo ending");
                Submit("confirm-ok");
            }, null);
            Add($"{tag} report", () => Note($"day {S.LastReport.Day}: revenue {S.LastReport.Revenue}, customers {S.LastReport.Customers}, cash {S.Ledger.Cash}, rep {S.Standing.Reputation:0}"),
                $"{tag}_report", () => G.Top is DayReportView, 0.8f, 30f);
        }

        // ------------------------------------------------------------------ the script

        static void BuildScript()
        {
            Add("boot", () =>
            {
                var doc = G.GetComponentInChildren<UIDocument>();
                _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _rt.Create();
                doc.panelSettings.targetTexture = _rt;
            }, null, () => G != null && G.Session != null && G.GetComponentInChildren<UIDocument>() != null, 1f);

            // ---------------- title, settings, splash
            Add("title", () =>
            {
                Check(S.IsDemo, "the boot session is the demo");
                Names("title-play", "title-settings", "title-credits", "title-wishlist", "title-quit", "title-version");
                Check(HasText(G.Top.Root, "DEMO"), "title says DEMO");
                Check(G.Stage.Q(className: "le-toasts")?.resolvedStyle.visibility == Visibility.Hidden, "no toasts over the title");
            }, "title_demo", () => G.Top is TitleView, 1.2f);
            Add("credits", () => Submit("title-credits"), "credits");
            Add("credits close", () =>
            {
                Check(HasText(G.Stage.Q("title-credits-card"), "McHunter Studio"), "credits name the studio");
                G.Top.OnEscape();
                Check(G.Stage.Q("title-credits-card") == null && G.Top is TitleView, "ESC closes the credits, title stays");
            }, null);
            Add("settings", () => Submit("title-settings"), "settings");
            Add("settings check", () =>
            {
                Check(G.Top is SettingsView, "settings opened from the title");
                Names("settings-volume-down", "settings-volume-up", "settings-fullscreen", "settings-hints", "settings-back");
                Submit("settings-back");
                Check(G.Top is TitleView, "BACK returns to the title");
            }, null);
            Add("wishlist", () =>
            {
                _wishlistOpens = StoreLinks.OpenCount;
                Submit("title-wishlist");
                Check(StoreLinks.OpenCount == _wishlistOpens + 1, "WISHLIST ON STEAM opens the store page");
            }, null);
            // GameRoot never shows the splash in batch mode: open it by hand and let it run out.
            Add("splash", () => G.Open(new SplashView(G)), "splash_a", null, 0.1f);
            Add("splash b", null, "splash_b", null, 0.45f);
            Add("splash c", null, "splash_c", null, 0.1f);
            Add("splash d", null, "splash_d", null, 0.05f);
            Add("splash closes", () => Check(!G.Stage.Q(className: "le-toasts").visible || G.Stage.Q(className: "le-toasts").resolvedStyle.visibility == Visibility.Hidden, "toasts still hidden"),
                null, () => G.Find<SplashView>() == null && G.Top is TitleView, 0.2f, 10f);

            // ---------------- Day 1: the coached job
            Add("play", () => Submit("title-play"), null);
            Add("day 1 card", () =>
            {
                Check(S.Day == 1 && S.TutorialActive, "Day 1 is the tutorial");
                Check(HasText(G.Top.Root, "DAY 1 OF 2"), "card says DEMO · DAY 1 OF 2");
                Names("daycard-start");
                Check(CoachShown(), "coach points at START THE DAY");
            }, "day1_card_coach", () => G.Top is DayStartView, 1.2f);
            Add("start day 1", () => Submit("daycard-start"), "day1_shop_morning", null, 0.3f);
            Add("read mail", () =>
            {
                Check(CoachShown(), "coach in the shop");
                Click("hit-crt");
                Check(G.Top is ComputerView, "the computer opened");
            }, "day1_messages");
            Add("close computer", () => Submit("os-close"), "day1_coach_open_sign");
            Add("open shop", () =>
            {
                _busyId = StoryCast.SamirId;
                Click("hit-sign");
                Check(S.Phase == DayPhase.Open, "the sign opened the shop");
                // Samir comes two minutes after the sign flips, whenever that is.
                CheckArrivalsFromOpening(S.Clock.Minute, StoryCast.SamirId);
            }, null);
            Add("close too soon", () =>
            {
                Click("hit-sign");
                Check(S.Phase == DayPhase.Open && !(G.Top is ConfirmView), "the sign stays OPEN while Samir is on his way");
                Check(ToastIs("Not yet. Your first customer is on his way."), "the sign says why it won't close");
            }, "day1_close_refused", null, 0.1f);
            Job("samir", StoryCast.SamirId, true);
            Add("samir order", () => _samirOrder = OrderOf(StoryCast.SamirId).Id, "day1_coach_close_early", null, 0.6f);
            CloseEarly("day1");
            Add("day 1 report", () =>
            {
                Names("report-next");
                Check(!S.DemoOver, "Day 1 is not the end of the demo");
            }, null);

            // ---------------- Day 2 morning
            Add("next day", () => Submit("report-next"), null);
            Add("day 2 card", () =>
            {
                Check(S.Day == 2 && !S.TutorialActive, "Day 2, no tutorial");
                Check(HasText(G.Top.Root, "LAST DAY OF THE DEMO"), "card says last day of the demo");
                Check(S.Inbox.Messages.Any(m => m.OrderId == _samirOrder), "Samir's outcome is in the mail");
                Check(S.Inbox.Messages.Any(m => m.Subject == DemoCast.DayTwoSubject), "R.'s Day-two note");
                Check(S.Country(Countries.Turkey).Owned, "Turkey is a gift");
                Check(S.Country(Countries.France).HandbookOutdated, "France changed its rules");
                Check(!CoachShown(), "no coach on Day 2");
                var po = S.PendingOutcomes.Find(p => p.OrderId == _samirOrder);
                var so = S.Orders.Get(_samirOrder);
                Note($"samir outcome: {po?.Kind} applied {po?.Applied}, quality {so?.Quality?.Final:0}, rep {S.Standing.Reputation:0}");
                Check(po != null && so?.Quality != null && po.Kind == DemoCast.SamirOutcome(so.Quality.Final),
                    $"Samir's verdict follows his passport's quality ({so?.Quality?.Final:0} -> {po?.Kind})");
                Check(po != null && po.Kind == OutcomeKind.Success, "a careful Samir job gets him through");
                Check(HasText(G.Top.Root, "Samir made it"), "the Day-2 card says Samir made it");
                Check(S.SaleActive && S.SaleDiscountPercent == 50, $"the Day-2 sale is 50% off (got {S.SaleDiscountPercent}%)");
            }, "day2_card", () => G.Top is DayStartView && S.Day == 2, 1.2f);
            Add("start day 2", () => Submit("daycard-start"), "day2_shop_morning", null, 0.3f);
            Add("day 2 mail", () => Click("hit-crt"), "day2_messages", null);
            Add("day 2 mail 2", () =>
            {
                var rows = G.Stage.Query(className: "le-os-msg").ToList();
                Check(rows.Count >= 2, "two new messages");
                if (rows.Count >= 2) Click(rows[1]);
            }, "day2_messages_second");
            Add("day 2 news", () => Submit("nav-news"), "day2_news_border_watch");
            Add("border watch lock", () =>
            {
                Names("watch-lock-JP", "watch-lock-UK", "watch-lock-IR");
                Click("watch-lock-UK");
                // Long names drop the wishlist line so the nudge stays on one line.
                Check(ToastIs("United Kingdom is in the full game."), "locked Border Watch flag toast");
            }, "day2_border_watch_toast");
            Add("border watch turkey off", () =>
            {
                // Not taking a country matters in the demo: its client gets turned away at the counter.
                Press(RowButton("TURKEY", "TAKING JOBS"));
                Check(!S.Country(Countries.Turkey).AcceptingJobs, "Border Watch: not taking Turkey jobs");
                Check(HasText(G.Top.Root, "turned away at the counter"), "the demo's Border Watch footnote");
            }, "day2_border_watch_turkey_off");
            Add("day 2 shop", () =>
            {
                Submit("nav-shop");
                Check(S.SaleActive, "equipment sale on");
                Check(HasText(G.Top.Root, "SALE: 50% OFF"), "the SHOP shows the sale at 50%");
                Check(HasText(G.Top.Root, "Customers are asking"), "Germany: a client is booked, so customers are asking");
                Names("shop-lock-cam3", "shop-lock-JP", "shop-lock-VISA");
                Check(S.EquipmentLocked(EquipmentCatalog.Get("cam3")) && !S.BuyEquipment("cam3"), "tier-3 camera is FULL GAME");
                Check(!S.BuyHandbook(Countries.Japan), "Japan handbook is FULL GAME");
            }, "day2_shop_sale_tier3_locks", null, 1.6f);
            Add("day 2 shop scrolled", () => Scroll("le-os-form--full", 700f), "day2_shop_scrolled");
            Add("day 2 shop bottom", () => Scroll("le-os-form--full", 10000f), "day2_shop_locked_handbooks_visa");
            Add("buy", () =>
            {
                // Germany waits until Anja is at the counter (buying it then gets you her job).
                Scroll("le-os-form--full", 0f);
                Press(RowButton("FRANCE", "UPDATE"));
                Check(!S.Country(Countries.France).HandbookOutdated, "bought the France update ($80)");
                Check(!S.Country(Countries.Germany).Owned, "Germany not bought yet");
            }, "day2_shop_bought");
            Add("day 2 close computer", () => Submit("os-close"), null);
            Add("handbook japan", () =>
            {
                G.ToggleHandbook(Countries.Japan);
                Names("handbook-tab-BASICS", "handbook-tab-JP", "handbook-tab-VISAS", "handbook-wishlist");
            }, "handbook_japan_full_game");
            Add("handbook korea tab", () =>
            {
                Click("handbook-tab-KR");
                Check(ToastIs("South Korea is in the full game.") || ToastIs("South Korea is in the full game. Wishlist us on Steam!"), "FULL GAME tab toast");
            }, "handbook_korea_tab_toast");
            Add("handbook visas", () =>
            {
                G.ToggleHandbook();
                G.ToggleHandbook(HandbookView.VisasTab);
            }, "handbook_visas");
            Add("handbook turkey", () =>
            {
                G.ToggleHandbook();
                G.ToggleHandbook(Countries.Turkey);
            }, "handbook_turkey_gift");
            Add("handbook germany", () =>
            {
                G.ToggleHandbook();
                G.ToggleHandbook(Countries.Germany);
                Names("handbook-tab-DE");
            }, "handbook_germany_not_owned");
            Add("handbook italy", () =>
            {
                G.ToggleHandbook();
                G.ToggleHandbook(Countries.Italy);
                Check(!HasText(G.Top.Root, "Customers are asking"), "Italy: no client booked, so nobody is promised");
            }, "handbook_italy_not_owned");
            Add("handbook france", () =>
            {
                G.ToggleHandbook();
                G.ToggleHandbook(Countries.France);
                Check(!S.Country(Countries.France).HandbookOutdated, "France handbook up to date");
            }, "handbook_france_updated");
            Add("handbook close", () => G.ToggleHandbook(), null);
            Add("pause", () =>
            {
                G.Open(new PauseView(G));
                Names("pause-resume", "pause-handbook", "pause-settings", "pause-newgame", "pause-quit");
                Check(HasText(G.Top.Root, "RESTART DEMO"), "pause offers RESTART DEMO");
            }, "pause");
            Add("pause settings", () => Submit("pause-settings"), "pause_settings");
            Add("pause back", () =>
            {
                Submit("settings-back");
                Submit("pause-resume");
                Check(G.Top == null, "back in the shop");
            }, null);
            Add("open shop day 2", () =>
            {
                _busyId = DemoCast.ElifId;
                Click("hit-sign");
                Check(S.Phase == DayPhase.Open, "the sign opened the shop");
                CheckArrivalsFromOpening(S.Clock.Minute, DemoCast.ElifId, DemoCast.KenjiId, DemoCast.ViktorId, DemoCast.AnjaId);
            }, null);
            Add("day 2 close too soon", () =>
            {
                Click("hit-sign");
                Check(S.Phase == DayPhase.Open && !(G.Top is ConfirmView), "the sign stays OPEN while visitors are due");
                Check(ToastIs("Not yet. Someone's still due today."), "the sign says someone's still due");
            }, "day2_close_refused", null, 0.1f);

            // ---------------- Day 2 visitors
            // Kenji's picture is taken by Watch whenever he asks; it gets its number here.
            _kenjiShot = $"{++_shots:000}_kenji_japan_teaser";
            // Elif: Turkey is switched off in Border Watch, so she can only be told no...
            Func<Visit> elif = () => VisitOf(DemoCast.ElifId);
            Add("elif not taking", () =>
            {
                string no = Lines.Player.NotTaking(S.Country(Countries.Turkey).Spec.Name);
                Check(ChoiceIds(elif()) == GameSession.ChoiceIds.Decline && elif().Choices[0].Text == no, $"not taking Turkey: only \"{no}\", got {ChoiceIds(elif())}");
            }, "elif_not_taking_turkey", () => elif() != null && elif().Phase == VisitPhase.Choosing && elif().Choices.Count > 0, 0.3f, 60f);
            // ...until the player takes Turkey jobs again: she gets the real offer.
            Add("elif turkey on", () =>
            {
                Click("hit-crt");
                G.Find<ComputerView>()?.OpenApp("news");
                Press(RowButton("TURKEY", "NOT TAKING"));
                Check(S.Country(Countries.Turkey).AcceptingJobs, "Border Watch: taking Turkey jobs again");
                Check(elif().Choices.Count == 0 && elif().IsTalking, "Elif is re-offered the job");
            }, "elif_border_watch_turkey_on");
            Add("elif close computer", () => Submit("os-close"), null);
            Job("elif", DemoCast.ElifId, false);
            Add("elif lines", () => Check(LinesSeen.Contains(DemoCast.TakingJobsAgainLine), $"Elif: \"{DemoCast.TakingJobsAgainLine}\""), null, null, 0f);
            Add("skip to kenji", () =>
            {
                if (!_kenjiDone) SkipToArrival(DemoCast.KenjiId);
                _busyId = 0;
            }, null);
            Add("kenji gone", () => Check(OrderOf(DemoCast.KenjiId) == null, "no Japan order"), null,
                () => _kenjiDone && VisitOf(DemoCast.KenjiId) == null, 0.5f, 90f);
            // Viktor: shot on the old light-gray background first; he reads the paper (France wants white now).
            Add("skip to viktor", () => SkipToArrival(DemoCast.ViktorId), null);
            Job("viktor", DemoCast.ViktorId, false, true);
            Add("viktor check", () =>
            {
                var o = OrderOf(DemoCast.ViktorId);
                Check(o.Urgent && o.CountryId == Countries.France && o.Price >= 1200, $"rush job: France, urgent, $1,200+ (got {o.CountryId} {o.Urgent} {o.Price})");
                Check(o.Redos == 1, $"one FIX IT (got {o.Redos})");
                Note($"viktor: due {o.Deadline.Clock}, delivered {o.DeliveredAt?.Clock}, quality {o.Quality}");
            }, null);
            // Losing focus mid-shift opens the pause menu (GameRoot.PauseShift), once.
            Add("pause on focus loss", () =>
            {
                Check(G.Top == null && S.Phase == DayPhase.Open, "in the shop, clock running");
                G.PauseShift();
                Check(G.Top is PauseView, "PauseShift opened the pause menu");
                G.PauseShift();
            }, "pause_focus_lost");
            Add("pause on focus loss resume", () =>
            {
                Submit("pause-resume");
                Check(G.Top == null, "one pause menu only (a second PauseShift is a no-op)");
            }, null);
            // Anja: Germany isn't owned, so only the "no"... until the section is bought while she waits.
            Func<Visit> anja = () => VisitOf(DemoCast.AnjaId);
            Add("skip to anja", () => SkipToArrival(DemoCast.AnjaId), null);
            Add("anja no handbook", () =>
            {
                Check(ChoiceIds(anja()) == GameSession.ChoiceIds.Decline, "no Germany section: the decline line only, got " + ChoiceIds(anja()));
                Check(!S.Country(Countries.Germany).Owned, "Germany not owned yet");
            }, "anja_no_germany_handbook", () => anja() != null && anja().Phase == VisitPhase.Choosing && anja().Choices.Count > 0, 0.3f, 90f);
            Add("anja buy germany", () =>
            {
                Click("hit-crt");
                G.Find<ComputerView>()?.OpenApp("shop");
                Scroll("le-os-form--full", 0f);
                Press(RowButton("GERMANY", "BUY"));
                Check(S.Country(Countries.Germany).Owned, "bought the Germany section ($250) with Anja at the counter");
                Check(anja().Choices.Count == 0 && anja().IsTalking, "Anja is re-offered the job");
            }, "anja_shop_bought_germany");
            Add("anja close computer", () => Submit("os-close"), null);
            Job("anja", DemoCast.AnjaId, false);
            Add("anja lines", () => Check(LinesSeen.Contains(DemoCast.HandbookBoughtLine), $"Anja: \"{DemoCast.HandbookBoughtLine}\""), null, null, 0f);
            Add("quiet afternoon", () => Check(S.Inbox.Messages.Any(m => m.Body == DemoCast.QuietBody && m.From == "R."), "R. says close up"),
                "day2_quiet_afternoon", () => S.Inbox.Messages.Any(m => m.Body == DemoCast.QuietBody), 0.5f, 30f);
            CloseEarly("day2");

            // ---------------- the end
            Add("demo over", () =>
            {
                Check(S.DemoOver, "the demo is over after the Day-2 report");
                Names("report-finish");
                Check(G.Stage.Q("report-next") == null, "no NEXT DAY on the last report");
            }, null);
            Add("finish demo", () => Submit("report-finish"), null);
            Add("demo end", () =>
            {
                Names("demo-end", "demoend-wishlist", "demoend-again", "demoend-quit");
                Check(HasText(G.Top.Root, "A FEW DAYS LATER"), "the demo end has the epilogue");
                Check(HasText(G.Top.Root, "Samir made it"), "the epilogue has Samir's word");
                Check(HasText(G.Top.Root, "haven't written yet"), "the epilogue names the clients still to write");
                Check(G.Stage.Q(className: "le-toasts")?.resolvedStyle.visibility == Visibility.Hidden, "no toasts over the demo end");
                G.Top.OnEscape();
                Check(G.Top is DemoEndView, "ESC doesn't leave the demo end");
            }, "demo_end", () => G.Top is DemoEndView, 1.2f);
            Add("demo end wishlist", () =>
            {
                _wishlistOpens = StoreLinks.OpenCount;
                Submit("demoend-wishlist");
                Check(StoreLinks.OpenCount == _wishlistOpens + 1, "demo-end WISHLIST opens the store page");
            }, "demo_end_wishlist");
            Add("still the end", () => Check(G.Top is DemoEndView && S.Day == 2, "nothing moves on behind the demo end"), null, null, 1f);
            Add("play again", () => Submit("demoend-again"), null);
            Add("title again", () => Check(S.IsDemo && S.Day == 1 && S.Phase == DayPhase.Morning, "PLAY AGAIN starts a fresh demo"),
                "title_again", () => G.Top is TitleView, 1f);
        }
    }
}
#endif
