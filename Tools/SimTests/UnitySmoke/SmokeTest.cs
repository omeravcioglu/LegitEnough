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
    /// failed checks or a timeout). At six points (title, the Day-1 shop with R.'s note, a toast
    /// over the computer, the computer SHOP with R.'s note, the open Day-2 shop with its hint line
    /// and a toast, the demo end) the same moment is also
    /// rendered and checked at 3440x1440 and 1280x800 (see AspectPass); on the Day-2 SHOP screens
    /// R.'s note must cover no button (see CoachClearOfButtons).
    /// Achievements (memory store, banners switched on for batch): the ACHIEVEMENTS screen from the
    /// title and the pause menu, every unlock banner on the way (held under any screen but the shop
    /// floor and the computer, quiet on the coached Day 1, clear of R.'s note and the monitor bezel)
    /// and the demo-end line (see WatchAch).
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
            /// <summary>Runs right after the step's picture (checks on what the picture shows).</summary>
            public Action After;
            /// <summary>Set: after the picture, the same moment is re-rendered at the other screen sizes (see AspectPass).</summary>
            public string AspectPick;
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
        // Background music: MusicPlayer picks a cue from the game state every frame (batch mode
        // tracks the cue but plays nothing). Watch logs every change.
        static MusicCue _lastCue = (MusicCue)(-1);
        static readonly HashSet<MusicCue> CuesSeen = new HashSet<MusicCue>();
        // The voluntary retake (photo corner -> "Another photo?"): the first kept photo and booklet.
        static int _firstPhotoId = -1;
        static int _firstDocId = -1;
        static float _patienceBefore;
        static int _settingsMusic;
        // The aspect pass (phase 4): the screen sizes it re-renders, the one in progress, frames waited.
        static readonly Vector2Int[] AspectSizes = { new Vector2Int(3440, 1440), new Vector2Int(1280, 800) };
        static readonly Dictionary<Vector2Int, RenderTexture> AspectRts = new Dictionary<Vector2Int, RenderTexture>();
        static string _aspectShot;
        static string _aspectPick;
        static int _aspectI;
        static int _aspectFrame;
        static int _aspectShots;
        static int _coachShopChecks;
        static readonly HashSet<string> AspectSeen = new HashSet<string>();
        // Achievements: every banner seen on screen (WatchAch), and a banner showing where it must
        // be held (the screen it showed over, the frame it was first seen there).
        static readonly HashSet<string> AchSeen = new HashSet<string>();
        static readonly HashSet<string> AchReported = new HashSet<string>();
        static string _achBad;
        static int _achBadFrame;

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
            // Batch runs hide the achievement banners (trailer, store shots): this run checks them.
            AchievementToasts.ShowInBatch = true;
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
            // A missing music file is only a warning in the game; here it's a failure.
            else if (type == LogType.Warning && msg.Contains("LegitEnough: music")) Errors.Add("MUSIC: " + msg);
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
                // The aspect pass holds the game still: nothing to watch until it's done.
                if (_phase != 4) Watch();
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
            WatchAch();
            var mp = MusicPlayer.Instance;
            if (mp != null && mp.Cue != _lastCue)
            {
                _lastCue = mp.Cue;
                CuesSeen.Add(mp.Cue);
                Note($"music: {mp.Cue} (top {G.Top?.GetType().Name}, {S.Phase} {S.Now})");
            }
        }

        static void MusicIs(MusicCue want, string when)
        {
            var mp = MusicPlayer.Instance;
            Check(mp != null, "the MusicPlayer is running");
            if (mp != null) Check(mp.Cue == want, $"music {when}: {want}, got {mp.Cue}");
        }

        // ------------------------------------------------------------------ achievements

        static AchievementBook Book => G.Achievements?.Book;

        static bool Unlocked(string id)
        {
            var d = AchievementCatalog.Get(id);
            return Book != null && d != null && Book.Unlocked(d);
        }

        /// <summary>The banner layer is up (its own inline visibility, set every frame by AchievementToasts.Tick).</summary>
        static bool AchLayerUp() =>
            G.AchToasts != null && G.AchToasts.Layer.style.visibility.value == Visibility.Visible && G.AchToasts.Current != null;

        /// <summary>The banner for <paramref name="id"/> is on screen and laid out.</summary>
        static bool AchShowing(string id)
        {
            if (!AchLayerUp() || G.AchToasts.Current.Id != id) return false;
            var item = G.AchToasts.Layer.Q("ach-toast-" + id);
            return item != null && item.worldBound.width >= 1f && item.worldBound.height >= 1f && item.resolvedStyle.opacity > 0.9f;
        }

        /// <summary>
        /// Every frame: log each banner the first time it's on screen (where, how, over what), and
        /// fail if one stays up over a screen that should hold it back (anything but the shop floor
        /// and the computer). A screen opened this frame gets a few frames' grace.
        /// </summary>
        static void WatchAch()
        {
            var t = G.AchToasts;
            if (t == null) return;
            bool up = AchLayerUp();
            if (up)
            {
                string id = t.Current.Id;
                var item = t.Layer.Q("ach-toast-" + id);
                if (item != null && item.worldBound.width >= 1f && !AchSeen.Contains(id))
                {
                    AchSeen.Add(id);
                    var r = item.worldBound;
                    var st = G.Stage.worldBound;
                    Note($"achievement banner: {id} [{string.Join(" ", item.GetClasses())}{(t.Layer.ClassListContains("le-achtoasts--low") ? " / layer low" : "")}] " +
                         $"at ({r.x - st.x:0},{r.y - st.y:0} {r.width:0}x{r.height:0}) over {G.Top?.GetType().Name ?? "the shop"}, {t.Queued} queued, {S.Now}");
                }
            }
            var top = G.Top;
            string bad = up && top != null && !(top is ComputerView) ? $"{t.Current.Id} over {top.GetType().Name}" : null;
            if (bad == null || bad != _achBad)
            {
                _achBad = bad;
                _achBadFrame = Time.frameCount;
                return;
            }
            if (Time.frameCount - _achBadFrame >= 3 && AchReported.Add(bad))
                Check(false, "achievement banners wait while a screen is up, but " + bad + " is showing");
        }

        /// <summary>Earned, and waiting (not on screen) while <paramref name="where"/> is up.</summary>
        static void AchHeld(string id, string where)
        {
            var t = G.AchToasts;
            Check(Unlocked(id), $"{id} is unlocked ({where})");
            Check(t != null && !AchLayerUp(), $"no achievement banner over {where}");
            if (t == null) return;
            bool waiting = t.Current != null ? t.Current.Id == id || t.Queued > 0 : t.Queued > 0;
            Check(waiting || AchSeen.Contains(id), $"the {id} banner waits for {where} to close (current {t.Current?.Id ?? "-"}, {t.Queued} queued)");
            Note($"achievements held over {where}: current {t.Current?.Id ?? "-"}, {t.Queued} queued");
        }

        /// <summary>Checks the banner on screen now: its look, its spot, and what it must stay clear of.</summary>
        static void AchBanner(string id, bool? quiet, bool low)
        {
            Check(AchShowing(id), $"the {id} banner is on screen");
            var item = G.AchToasts.Layer.Q("ach-toast-" + id);
            if (item == null) return;
            var r = item.worldBound;
            var st = G.Stage.worldBound;
            if (quiet.HasValue)
                Check(item.ClassListContains("le-achtoast--quiet") == quiet.Value, $"{id}: {(quiet.Value ? "quiet" : "full")} banner, classes [{string.Join(" ", item.GetClasses())}]");
            Check(G.AchToasts.Layer.ClassListContains("le-achtoasts--low") == low, $"{id}: banner {(low ? "low, over the computer" : "top centre, over the shop")}");
            Check(r.xMin >= st.xMin - 0.5f && r.yMin >= st.yMin - 0.5f && r.xMax <= st.xMax + 0.5f && r.yMax <= st.yMax + 0.5f,
                $"{id}: the banner is inside the stage ({r.x - st.x:0},{r.y - st.y:0} {r.width:0}x{r.height:0})");
            if (CoachShown())
            {
                var note = G.Coach.Root.Q(className: "le-coach__note");
                var o = note != null && Shown(note) ? Clip(r, note.worldBound) : null;
                Check(o == null, $"{id}: the banner is clear of R.'s note (overlap {o?.width:0}x{o?.height:0})");
            }
            if (low && G.Top is ComputerView pc)
            {
                var bezel = pc.Root.Q(className: "le-monitor");
                Check(bezel != null, "the monitor bezel");
                var o = bezel != null ? Clip(r, bezel.worldBound) : null;
                Check(o == null, $"{id}: the banner is clear of the monitor bezel (overlap {o?.width:0}x{o?.height:0})");
            }
            Note($"banner {id}: ({r.x - st.x:0},{r.y - st.y:0} {r.width:0}x{r.height:0}) [{string.Join(" ", item.GetClasses())}], coach {CoachShown()}, top {G.Top?.GetType().Name ?? "-"}");
        }

        /// <summary>The ACHIEVEMENTS screen's count line, and that it agrees with the book.</summary>
        static void AchCount(string where)
        {
            var count = G.Stage.Q<Label>("ach-count")?.text;
            int n = Book?.CountUnlocked(true) ?? -1;
            string want = $"DEMO {n} / {AchievementCatalog.DemoCount}";
            Note($"achievements count ({where}): \"{count}\", more: \"{G.Stage.Q<Label>("ach-more")?.text}\"");
            Check(count == want, $"{where}: the count reads \"{want}\", got \"{count}\"");
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
                    step.After?.Invoke();
                    if (step.AspectPick != null && _rt != null)
                    {
                        BeginAspect((step.Shot ?? step.Name) + _shotSuffix, step.AspectPick);
                        _phase = 4;
                        return;
                    }
                    NextStep(now);
                    return;
                case 4:
                    if (!TickAspect()) return;
                    NextStep(EditorApplication.timeSinceStartup);
                    return;
            }
        }

        static void NextStep(double now)
        {
            if (_jump >= 0)
            {
                _index = _jump;
                _jump = -1;
            }
            else _index++;
            _phase = 0;
            _phaseStart = now;
        }

        static void Shot(string name)
        {
            if (_rt == null) return;
            var tex = ReadBack(_rt);
            File.WriteAllBytes(Path.Combine(_out, name + _shotSuffix + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static Texture2D ReadBack(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        // ------------------------------------------------------------------ other screen shapes

        /// <summary>
        /// Marks the step just added: after its picture the game is held still (GameRoot disabled,
        /// so no sim time passes) and the same moment is re-rendered at 3440x1440 (21:9) and
        /// 1280x800 (Steam Deck). The panel takes the render texture's size as the screen size, so
        /// this is what those screens show. At each size: the whole 1920x1080 stage is inside the
        /// panel, centred, touching two opposite edges (bars on the other two, in the bar colour);
        /// R.'s note, the toasts and the shop's hint line sit inside the stage; the named element
        /// is picked at its on-screen spot and a bar picks nothing. Pictures: "NNN_name_WxH.png".
        /// Then 1920x1080 comes back and the run carries on as before.
        /// </summary>
        static void AspectPass(string pick) => Steps[Steps.Count - 1].AspectPick = pick;

        static void BeginAspect(string shot, string pick)
        {
            _aspectShot = shot;
            _aspectPick = pick;
            _aspectI = 0;
            G.enabled = false;
            SetTarget(AspectSizes[0]);
        }

        static void SetTarget(Vector2Int size)
        {
            if (!AspectRts.TryGetValue(size, out var rt))
            {
                rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                AspectRts[size] = rt;
            }
            G.GetComponentInChildren<UIDocument>().panelSettings.targetTexture = rt;
            _aspectFrame = Time.frameCount;
        }

        /// <summary>True once every size is checked and the game runs again at 1920x1080.</summary>
        static bool TickAspect()
        {
            // A few rendered frames (editor updates come faster) for the panel to take the new
            // size, the stage to re-centre and the texture to be drawn at that size.
            if (Time.frameCount - _aspectFrame < 4) return false;
            var size = AspectSizes[_aspectI];
            CheckAspect(size);
            _aspectShots++;
            if (++_aspectI < AspectSizes.Length)
            {
                SetTarget(AspectSizes[_aspectI]);
                return false;
            }
            G.GetComponentInChildren<UIDocument>().panelSettings.targetTexture = _rt;
            G.enabled = true;
            return true;
        }

        static void CheckAspect(Vector2Int size)
        {
            string at = $"{_aspectShot} @ {size.x}x{size.y}";
            var root = G.GetComponentInChildren<UIDocument>().rootVisualElement;
            var panel = root.panel;
            Rect pr = root.layout, sb = G.Stage.worldBound;
            // Expand: one scale for both axes, the smaller of the two ratios.
            float k = Mathf.Min(size.x / GameRoot.StageWidth, size.y / GameRoot.StageHeight);
            float left = sb.xMin, right = pr.width - sb.xMax, top = sb.yMin, bottom = pr.height - sb.yMax;
            Note($"aspect {at}: panel {pr.width:0.##}x{pr.height:0.##} (scale {k:0.####}), stage ({sb.x:0.##},{sb.y:0.##}) {sb.width:0.##}x{sb.height:0.##}, bars L {left * k:0.#} R {right * k:0.#} T {top * k:0.#} B {bottom * k:0.#} px");
            Check(Mathf.Abs(pr.width - size.x / k) < 1f && Mathf.Abs(pr.height - size.y / k) < 1f,
                $"{at}: the panel is the screen at one scale ({size.x / k:0.#}x{size.y / k:0.#}), got {pr.width:0.#}x{pr.height:0.#}");
            Check(Mathf.Abs(sb.width - GameRoot.StageWidth) < 0.5f && Mathf.Abs(sb.height - GameRoot.StageHeight) < 0.5f,
                $"{at}: the stage is 1920x1080, got {sb.width:0.#}x{sb.height:0.#}");
            Check(left >= -0.5f && top >= -0.5f && right >= -0.5f && bottom >= -0.5f, $"{at}: the whole stage is inside the panel (L {left:0.#} R {right:0.#} T {top:0.#} B {bottom:0.#})");
            Check(Mathf.Abs(left - right) * k <= 1.01f && Mathf.Abs(top - bottom) * k <= 1.01f, $"{at}: the stage is centred (L {left * k:0.#} R {right * k:0.#} T {top * k:0.#} B {bottom * k:0.#} px)");
            Check(Mathf.Min(left + right, top + bottom) * k <= 1.01f, $"{at}: the stage touches two opposite edges (bars only on the other two)");

            // What must never reach the bars: R.'s note, the toasts, the hint line.
            int inside = 0;
            Action<VisualElement, string, string> inStage = (e, what, kind) =>
            {
                if (e == null || !Shown(e)) return;
                var wb = e.worldBound;
                if (!(wb.width >= 1f) || !(wb.height >= 1f)) return;
                inside++;
                AspectSeen.Add($"{kind} @ {size.x}x{size.y}");
                Check(wb.xMin >= sb.xMin - 0.5f && wb.yMin >= sb.yMin - 0.5f && wb.xMax <= sb.xMax + 0.5f && wb.yMax <= sb.yMax + 0.5f,
                    $"{at}: {what} is inside the stage ({wb.x - sb.x:0.#},{wb.y - sb.y:0.#} {wb.width:0.#}x{wb.height:0.#})");
            };
            if (CoachShown()) inStage(G.Coach.Root.Q(className: "le-coach__note"), "R.'s note", "note");
            G.Stage.Query<Label>(className: "le-toast").ForEach(l => { if (!string.IsNullOrEmpty(l.text)) inStage(l, "the toast \"" + l.text + "\"", "toast"); });
            G.Stage.Query<Label>(className: "le-hint").ForEach(l => { if (!string.IsNullOrEmpty(l.text)) inStage(l, "the hint line", "hint"); });
            if (AchLayerUp()) inStage(G.AchToasts.Layer.Q("ach-toast-" + G.AchToasts.Current.Id), "the achievement banner", "ach");
            Note($"aspect {at}: {inside} note/toast/hint boxes inside the stage");

            // The named element is hit at its on-screen spot (worked out from the bar layout).
            float ox = (size.x - GameRoot.StageWidth * k) * 0.5f, oy = (size.y - GameRoot.StageHeight * k) * 0.5f;
            var el = G.Stage.Q(_aspectPick);
            Check(el != null && Shown(el), $"{at}: '{_aspectPick}' is on screen");
            if (el != null && Shown(el))
            {
                var local = G.Stage.WorldToLocal(el.worldBound.center);
                var screen = new Vector2(ox + local.x * k, oy + local.y * k);
                var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, screen));
                Check(picked != null && (picked == el || el.Contains(picked)), $"{at}: a click at ({screen.x:0},{screen.y:0}) hits '{_aspectPick}', got {picked?.GetType().Name}#{picked?.name}");
            }
            var bar = ox > 4f ? new Vector2(ox * 0.5f, size.y * 0.5f) : new Vector2(size.x * 0.5f, oy * 0.5f);
            var barPick = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, bar));
            Check(barPick == null || barPick == root, $"{at}: a click in the bar hits nothing, got {barPick?.GetType().Name}#{barPick?.name}");

            // The picture, and the bars in it: plain rgb(6,5,4) both sides.
            var tex = ReadBack(AspectRts[size]);
            File.WriteAllBytes(Path.Combine(_out, $"{_aspectShot}_{size.x}x{size.y}.png"), tex.EncodeToPNG());
            int bad = 0, seen = 0;
            for (int i = 0; i < 9; i++)
            {
                float f = (i + 0.5f) / 9f;
                var spots = ox >= 2f
                    ? new[] { new Vector2(ox * 0.5f, size.y * f), new Vector2(size.x - ox * 0.5f, size.y * f) }
                    : new[] { new Vector2(size.x * f, oy * 0.5f), new Vector2(size.x * f, size.y - oy * 0.5f) };
                foreach (var p in spots)
                {
                    Color32 c = tex.GetPixel((int)p.x, (int)p.y);
                    seen++;
                    if (Math.Abs(c.r - 6) > 2 || Math.Abs(c.g - 5) > 2 || Math.Abs(c.b - 4) > 2) bad++;
                }
            }
            UnityEngine.Object.DestroyImmediate(tex);
            Check(bad == 0, $"{at}: the bars are rgb(6,5,4) ({bad} of {seen} sampled pixels are not)");
        }

        /// <summary>Displayed (no hidden ancestor) and not invisible.</summary>
        static bool Shown(VisualElement e)
        {
            if (e.panel == null || e.resolvedStyle.visibility != Visibility.Visible || e.resolvedStyle.opacity < 0.01f) return false;
            for (var p = e; p != null; p = p.hierarchy.parent)
                if (p.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        /// <summary>
        /// The computer SHOP with R.'s note up: the note covers no button you can see and press
        /// (each button's box clipped to its scroll view and to the stage).
        /// </summary>
        static void CoachClearOfButtons(string what)
        {
            if (!CoachShown() || !(G.Top is ComputerView pc) || pc.CurrentApp != "shop")
            {
                Note($"coach vs buttons, {what}: skipped (coach {CoachShown()}, top {G.Top?.GetType().Name})");
                return;
            }
            _coachShopChecks++;
            var note = G.Coach.Root.Q(className: "le-coach__note").worldBound;
            var stage = G.Stage.worldBound;
            int buttons = 0;
            var hits = new List<string>();
            G.Top.Root.Query<Button>().ForEach(b =>
            {
                if (!b.enabledInHierarchy || !Shown(b)) return;
                var wb = Clip(b.worldBound, stage);
                for (var p = b.hierarchy.parent; p != null && wb.HasValue; p = p.hierarchy.parent)
                    if (p is ScrollView sv && sv.contentContainer.Contains(b)) wb = Clip(wb.Value, sv.contentViewport.worldBound);
                if (!wb.HasValue) return;
                buttons++;
                var o = Clip(wb.Value, note);
                if (o.HasValue && o.Value.width > 0.5f && o.Value.height > 0.5f)
                    hits.Add($"'{(string.IsNullOrEmpty(b.name) ? b.text : b.name)}' ({o.Value.width:0}x{o.Value.height:0})");
            });
            Note($"coach vs buttons, {what}: note ({note.x - stage.x:0},{note.y - stage.y:0} {note.width:0}x{note.height:0}), {buttons} buttons on screen, {hits.Count} covered");
            Check(hits.Count == 0, $"{what}: R.'s note covers no button in the SHOP, but covers {string.Join(", ", hits)}");
        }

        static Rect? Clip(Rect a, Rect b)
        {
            if (float.IsNaN(a.x) || float.IsNaN(a.y) || float.IsNaN(b.x) || float.IsNaN(b.y)) return null;
            float x0 = Mathf.Max(a.xMin, b.xMin), x1 = Mathf.Min(a.xMax, b.xMax);
            float y0 = Mathf.Max(a.yMin, b.yMin), y1 = Mathf.Min(a.yMax, b.yMax);
            if (x1 <= x0 || y1 <= y0) return null;
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        static void AfterShot(Action check) => Steps[Steps.Count - 1].After = check;

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

        /// <summary>What R.'s sticky note says right now (its main line and the second line).</summary>
        static string CoachText()
        {
            if (G.Coach == null) return "";
            var main = G.Coach.Root.Q<Label>(className: "le-coach__text")?.text ?? "";
            var extra = G.Coach.Root.Q<Label>(className: "le-coach__extra");
            return extra != null && extra.resolvedStyle.display == DisplayStyle.Flex ? main + " | " + extra.text : main;
        }

        static string CoachCount() => G.Coach?.Root.Q<Label>(className: "le-coach__count")?.text ?? "";

        /// <summary>
        /// The coach is up and its ring sits on the named element (ring = the element's box plus a
        /// margin). <paramref name="watch"/>: a "just look" ring with no arrow; otherwise the arrow shows too.
        /// </summary>
        static bool CoachOn(string target, bool watch = false)
        {
            if (!CoachShown()) return false;
            var el = G.Stage.Q(target);
            var ring = G.Coach.Root.Q(className: "le-coach__ring");
            var arrow = G.Coach.Root.Q(className: "le-coach__pointer");
            if (el == null || ring == null || arrow == null) return false;
            if (ring.resolvedStyle.display != DisplayStyle.Flex) return false;
            bool arrowUp = arrow.resolvedStyle.display == DisplayStyle.Flex;
            if (arrowUp == watch) return false;
            Rect r = ring.worldBound, t = el.worldBound;
            return r.Overlaps(t) && r.xMin <= t.xMin + 1f && r.xMax >= t.xMax - 1f && r.width <= t.width * 1.5f + 60f;
        }

        static void CoachCheck(string target, string says, string what, bool watch = false)
        {
            bool on = CoachOn(target, watch);
            string text = CoachText();
            Note($"coach [{CoachCount()}] on {target}: {on} :: {text}");
            Check(on, $"{what}: the coach {(watch ? "rings" : "points at")} '{target}'");
            if (says != null) Check(text.Contains(says), $"{what}: the note says \"{says}\", got \"{text}\"");
        }

        /// <summary>An Until that waits for <paramref name="cond"/> but gives up quietly after a few seconds (the step's Check reports it).</summary>
        static Func<bool> Soon(Func<bool> cond, float seconds = 3f) =>
            () => cond() || EditorApplication.timeSinceStartup - _phaseStart > seconds;

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
            var doc = BookletOf(o);
            if (doc == null) return v.Phase == VisitPhase.Inspecting;
            if (v.Phase != VisitPhase.AwaitingHandover && v.Phase != VisitPhase.Waiting && v.Phase != VisitPhase.Choosing) return false;
            return S.GiveDocument(v.Id, doc.Id);
        }

        /// <summary>The order's booklet on the desk: the newest one when a reprint left two.</summary>
        static FinishedDocument BookletOf(Order o)
        {
            if (o == null) return null;
            if (o.Document != null && S.DeskDocuments.Contains(o.Document)) return o.Document;
            return S.DeskDocuments.Find(d => d.OrderId == o.Id);
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
        /// <paramref name="callBack"/> is the player's own retake: with the booklet already bound and
        /// the client on the bench, the photo corner asks "Another photo?", the client sits again,
        /// the new photo is kept and picked in the editor, and the job is printed again (pictures of
        /// that pass end in "_again"); the new booklet is the one handed over.
        /// </summary>
        static void Job(string tag, int customerId, bool tutorial, bool wrongBackdropFirst = false, bool callBack = false)
        {
            Func<Visit> who = () => VisitOf(customerId);
            Func<Order> order = () => OrderOf(customerId);
            string cid = null;
            bool retaken = !wrongBackdropFirst;
            bool calledBack = !callBack;
            bool secondPass = false;
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
            Add($"{tag} done photos", () =>
            {
                // The kept row says where a better photo comes from (the photo corner; in a retake, the editor).
                var hint = G.Stage.Q<Label>("cam-retake-note");
                Check(hint != null, "the camera's kept row has the retake line");
                if (hint != null)
                {
                    string want = secondPass ? "editor" : "photo corner";
                    Check(hint.text.Contains(want), $"the kept row mentions the {want}, got \"{hint.text}\"");
                }
                // The first kept photo is an achievement, but its banner waits for the camera to close.
                if (tutorial) AchHeld(AchievementCatalog.FirstPhoto, "the camera");
                Submit("cam-done");
            }, null);
            if (tutorial)
            {
                // Back in the shop: the quiet banner (R.'s guided Day 1), clear of R.'s note.
                Add($"{tag} first photo banner", () => AchBanner(AchievementCatalog.FirstPhoto, true, false),
                    "ach_toast_tutorial", Soon(() => AchShowing(AchievementCatalog.FirstPhoto), 12f), 0f);
            }
            Add($"{tag} bench", () => Check(!(G.Top is CameraView), "DONE closed the camera"), null,
                () => who().Phase == VisitPhase.Waiting && !who().Moving, 0.5f);

            // The client sheet, then the editor.
            Add($"{tag} slip", () =>
            {
                if (secondPass)
                {
                    Check(LinesSeen.Contains(Lines.RetakeDone), $"{tag}: \"{Lines.RetakeDone}\"");
                    Check(!who().Retake && who().Retakes == 1, $"{tag}: the retake is done (retakes {who().Retakes})");
                    Check(S.Photos.ForCustomer(customerId).Count >= 2, $"{tag}: both photos kept");
                    Check(order().Status == OrderStatus.Ready && order().Draft.PhotoId == _firstPhotoId,
                        $"{tag}: still ready, the draft keeps the old photo until it's picked ({order().Status}, photo {order().Draft.PhotoId})");
                    Check(S.DeskDocuments.Exists(d => d.Id == _firstDocId), $"{tag}: the first booklet is still on the desk");
                }
                G.Open(new SlipView(G, order().Id));
            }, $"{tag}_client_sheet");
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
            // A retake: the picker with both photos (NEWEST first, the old one still IN USE).
            Add($"{tag} editor photo picker", () =>
            {
                var sv = G.Stage.Q<ScrollView>(className: "le-os-form");
                var block = G.Stage.Q("editor-photo");
                Check(sv != null && block != null, "the editor's photo block");
                if (sv != null && block != null) sv.ScrollTo(block);
            }, $"{tag}_editor_photo_picker", null, 0.35f, 40f, () => !secondPass);
            Add($"{tag} number and photo", () =>
            {
                Submit("editor-docnumber");
                Check(Labels.Normalize(order().Draft.Get(DocField.DocumentNumber)).Length > 0, "GENERATE filled the number");
                if (secondPass)
                {
                    // Newest first: the new photo leads, the old one is still IN USE until clicked.
                    var thumbs = G.Stage.Query(className: "le-os-photo").ToList();
                    Check(thumbs.Count >= 2, $"the editor lists both photos (got {thumbs.Count})");
                    Check(thumbs.Count > 0 && thumbs[0].name == "editor-photo-" + _capture.Id, $"the newest photo comes first (got {(thumbs.Count > 0 ? thumbs[0].name : "-")})");
                    Check(HasText(G.Stage.Q("editor-photo"), "NEWEST") && HasText(G.Stage.Q("editor-photo"), "IN USE"), "NEWEST and IN USE tags");
                    Check(HasText(G.Stage.Q("editor-photo"), "Newest first"), "the 'Newest first' line");
                    Check(G.Stage.Q("editor-photo-" + _firstPhotoId)?.ClassListContains("le-os-photo--selected") == true, "the old photo is framed as IN USE");
                }
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
                // Clearing the jam is an achievement: its banner follows FIRST_PHOTO's if that one's still up.
                Add($"{tag} jam banner", () =>
                {
                    Check(Unlocked(AchievementCatalog.PercussiveMaintenance), "clearing the jam unlocked PERCUSSIVE_MAINTENANCE");
                    AchBanner(AchievementCatalog.PercussiveMaintenance, true, false);
                }, null, Soon(() => AchShowing(AchievementCatalog.PercussiveMaintenance), 12f), 0f);
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
                if (secondPass)
                {
                    var o = order();
                    Check(S.DeskDocuments.Count(d => d.OrderId == o.Id) == 2, $"{tag}: two booklets on the desk (got {S.DeskDocuments.Count(d => d.OrderId == o.Id)})");
                    Check(o.Document != null && o.Document.Id != _firstDocId && o.Document.Data.PhotoId == _capture.Id,
                        $"{tag}: the order's booklet is the new one, with the new photo (doc {o.Document?.Id}, photo {o.Document?.Data.PhotoId})");
                    if (o.Document != null)
                    {
                        string name = ShopView.BookletName(S, o.Document);
                        Check(name == $"booklet-{o.Id}-{o.Document.Id}", $"{tag}: the reprint is named '{name}'");
                    }
                }
            }, $"{tag}_booklet_on_desk");
            if (callBack)
            {
                // The player isn't happy with the photo after all: the photo corner calls her back.
                Func<bool> skip = () => calledBack;
                Add($"{tag} call back", () =>
                {
                    var v = who();
                    _firstPhotoId = _capture.Id;
                    _firstDocId = order().Document?.Id ?? -1;
                    Note($"{tag} call back: patience {v.PatienceFraction:0.00}, photo {_firstPhotoId}, booklet {_firstDocId}");
                    Click("hit-photo");
                    Check(G.Top is ConfirmView && HasText(G.Top.Root, "Another photo?"), "the photo corner asks \"Another photo?\"");
                    Check(HasText(G.Top?.Root, "print again"), "the question says the booklet needs printing again");
                }, $"{tag}_photo_corner_another_photo", () => who() != null && S.CanCallBackForPhoto(who().Id), 0.5f, 40f, skip);
                Add($"{tag} call back yes", () =>
                {
                    var v = who();
                    _patienceBefore = v.Patience;
                    Submit("confirm-ok");
                    Check(!(G.Top is ConfirmView), "the question closed");
                    Check(v.Retake && v.To == Spot.Stool, $"{tag} walks back to the stool (retake {v.Retake}, to {v.To})");
                    Check(Math.Abs(_patienceBefore - v.PatienceMax * Balance.RetakePatienceCost - v.Patience) < 0.05f, $"{tag}: a call-back costs {Balance.RetakePatienceCost:P0} patience");
                    Check(!S.CanCallBackForPhoto(v.Id), "no second call-back while she's on her way");
                    calledBack = true;
                    secondPass = true;
                    _shotSuffix = "_again";
                    Goto($"{tag} seated");
                }, $"{tag}_called_back", null, 0.6f, 40f, skip);
            }
            Add($"{tag} inspect", () =>
            {
                if (callBack)
                {
                    Check(LinesSeen.Contains(Lines.RetakeCalled(S.Customer(customerId))), $"{tag}: \"{Lines.RetakeCalled(S.Customer(customerId))}\"");
                    // The desk draws both copies, each under its own name (the shop redraws the desk a frame after binding).
                    var o = order();
                    var old = S.DeskDocuments.Find(d => d.Id == _firstDocId);
                    Check(old != null && G.Stage.Q(ShopView.BookletName(S, old)) != null, $"{tag}: the first booklet is on the desk as 'booklet-{o.Id}'");
                    Check(o.Document != null && G.Stage.Q(ShopView.BookletName(S, o.Document)) != null, $"{tag}: the reprint is on the desk as 'booklet-{o.Id}-{o.Document?.Id}'");
                }
                G.Open(new PassportView(G, BookletOf(order())));
            }, $"{tag}_passport");
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
                if (callBack)
                {
                    var o = order();
                    Check(o.Document != null && o.Document.Id != _firstDocId && o.Document.Data.PhotoId != _firstPhotoId,
                        $"{tag}: delivered the reprint with the new photo (doc {o.Document?.Id}, photo {o.Document?.Data.PhotoId})");
                    // The first booklet stays behind (no auto-shred): tidy it away like a player would.
                    var old = S.DeskDocuments.Find(d => d.Id == _firstDocId);
                    Check(old != null, $"{tag}: the first booklet stayed on the desk");
                    if (old != null) S.ShredDocument(old.Id);
                    Check(!S.DeskDocuments.Exists(d => d.OrderId == o.Id), $"{tag}: desk clear after shredding the old copy");
                }
                _shotSuffix = "";
                _busyId = 0;
            }, null, () => who() == null, 0.5f, 60f);
        }

        /// <summary>
        /// The four music files import as MusicImport.cs says (streamed Vorbis, not preloaded) and
        /// load from Resources at the right length. Batch mode plays nothing, so this is the check
        /// that the player build will find them.
        /// </summary>
        static void CheckMusicFiles()
        {
            var want = new (string File, float Seconds)[] { ("title_night", 80f), ("shop_day", 240f), ("shop_busy", 240f), ("demo_end", 47.75f) };
            foreach (var (file, seconds) in want)
            {
                string path = "Assets/LegitEnough/Resources/LegitEnough/Music/" + file + ".ogg";
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                Check(imp != null, "music imported: " + path);
                if (imp != null)
                {
                    var ss = imp.defaultSampleSettings;
                    Note($"music {file}: {ss.loadType}, {ss.compressionFormat} q{ss.quality:0.00}, preload {ss.preloadAudioData}, background {imp.loadInBackground}");
                    Check(ss.loadType == AudioClipLoadType.Streaming && ss.compressionFormat == AudioCompressionFormat.Vorbis && !ss.preloadAudioData,
                        $"music {file} streams Vorbis, not preloaded (got {ss.loadType} {ss.compressionFormat} preload {ss.preloadAudioData})");
                }
                var clip = Resources.Load<AudioClip>("LegitEnough/Music/" + file);
                Check(clip != null, "music loads from Resources: " + file);
                if (clip == null) continue;
                Note($"music {file}: {clip.length:0.00} s, {clip.frequency} Hz, {clip.channels} ch, loadType {clip.loadType}");
                Check(Math.Abs(clip.length - seconds) < 0.1f && clip.channels == 2, $"music {file}: {seconds} s stereo, got {clip.length:0.00} s {clip.channels} ch");
            }
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
                Names("title-achievements");
                Check(HasText(G.Top.Root, "DEMO"), "title says DEMO");
                Check(G.Stage.Q(className: "le-toasts")?.resolvedStyle.visibility == Visibility.Hidden, "no toasts over the title");
                MusicIs(MusicCue.Title, "on the title");
                CheckMusicFiles();
            }, "title_demo", () => G.Top is TitleView, 1.2f);
            AspectPass("title-play");
            // ACHIEVEMENTS from the title: nothing earned yet, the demo's 12 first, the other 24 FULL GAME.
            Add("achievements from the title", () =>
            {
                Submit("title-achievements");
                Check(G.Top is AchievementsView, "ACHIEVEMENTS opened from the title");
                Check(Book != null && Book.Demo, "the achievements book is the demo's");
                Check(G.Achievements?.Backend is MemoryAchievementBackend, $"batch runs keep achievements in memory (got {G.Achievements?.Backend?.Name})");
                Names("ach-view", "ach-count", "ach-grid", "ach-detail", "ach-detail-name", "ach-detail-desc", "ach-detail-progress",
                    "ach-detail-status", "ach-detail-wishlist", "ach-back");
                AchCount("from the title");
                Check(G.Stage.Q<Label>("ach-count")?.text == "DEMO 0 / 12", "the title shows DEMO 0 / 12");
                int full = 0, demo = 0;
                foreach (var d in AchievementCatalog.All)
                {
                    var cell = G.Stage.Q("ach-cell-" + d.Id);
                    Check(cell != null, "element 'ach-cell-" + d.Id + "' exists");
                    if (cell == null) continue;
                    bool fg = cell.ClassListContains("le-ach__cell--fullgame");
                    if (fg) full++;
                    else demo++;
                    Check(fg != d.Demo, $"{d.Id}: {(d.Demo ? "a demo cell" : "a FULL GAME cell")}");
                    Check((G.Stage.Q("ach-cell-" + d.Id + "-bar") != null) == (d.IsProgress && d.Demo), $"{d.Id}: a progress bar only on a progress achievement the demo can earn");
                }
                Check(demo == 12 && full == 24, $"12 demo cells and 24 FULL GAME cells (got {demo} and {full})");
                Check(G.Stage.Q<Label>("ach-detail-name")?.text == AchievementCatalog.All[0].Name, "the first cell is picked when it opens");
            }, "achievements_title", () => G.Top is TitleView);
            Add("achievements secrets", () =>
            {
                // The demo's two secrets stay "???" with their teaser; a FULL GAME secret too.
                foreach (var id in new[] { AchievementCatalog.SayCheese, AchievementCatalog.NightShift, AchievementCatalog.MixUp })
                {
                    var d = AchievementCatalog.Get(id);
                    Submit("ach-cell-" + id);
                    string name = G.Stage.Q<Label>("ach-detail-name")?.text, desc = G.Stage.Q<Label>("ach-detail-desc")?.text;
                    string status = G.Stage.Q<Label>("ach-detail-status")?.text;
                    Note($"achievements {id}: \"{name}\" / \"{desc}\" / {status}");
                    Check(name == "???" && desc == d.Teaser, $"{id}: \"???\" and the teaser in the detail, got \"{name}\" / \"{desc}\"");
                    Check(status == (d.Demo ? "SECRET" : "FULL GAME"), $"{id}: status {(d.Demo ? "SECRET" : "FULL GAME")}, got {status}");
                }
                // A FULL GAME one: its real text, the badge and WISHLIST.
                Submit("ach-cell-" + AchievementCatalog.Jobs25);
                Check(G.Stage.Q<Label>("ach-detail-name")?.text == AchievementCatalog.Get(AchievementCatalog.Jobs25).Name, "a FULL GAME cell shows its real name");
                Check(G.Stage.Q<Label>("ach-detail-status")?.text == "FULL GAME", "a FULL GAME cell says FULL GAME");
            }, "achievements_fullgame");
            AfterShot(() =>
            {
                var wish = G.Stage.Q("ach-detail-wishlist");
                Check(wish != null && Shown(wish), "a FULL GAME cell shows WISHLIST");
                Check(!Shown(G.Stage.Q("ach-detail-progress")), "no progress line on a FULL GAME cell");
                Submit("ach-cell-" + AchievementCatalog.FirstPhoto);
            });
            Add("achievements back", () =>
            {
                Check(G.Stage.Q("ach-detail-wishlist") == null || !Shown(G.Stage.Q("ach-detail-wishlist")), "no WISHLIST on a demo cell");
                Submit("ach-back");
                Check(G.Top is TitleView && G.Top.Root.visible, "BACK returns to the title, shown again");
                Check(G.Stage.Q("ach-view") == null, "the ACHIEVEMENTS screen closed");
            }, null);
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
                // The MUSIC row: ten pips and -/+, saved like the master volume (restored here).
                Names("settings-music-down", "settings-music-up", "settings-music-1", "settings-music-10");
                Check(HasText(G.Top.Root, "MUSIC"), "settings has a MUSIC row");
                _settingsMusic = GameSettings.MusicVolume;
                Note($"music setting {_settingsMusic * 10}%");
                if (_settingsMusic > 0)
                {
                    Submit("settings-music-down");
                    Check(GameSettings.MusicVolume == _settingsMusic - 1, $"MUSIC - lowers it ({GameSettings.MusicVolume})");
                    Submit("settings-music-up");
                }
                else
                {
                    Submit("settings-music-up");
                    Check(GameSettings.MusicVolume == 1, $"MUSIC + raises it ({GameSettings.MusicVolume})");
                    Submit("settings-music-down");
                }
                Check(GameSettings.MusicVolume == _settingsMusic, "MUSIC back where it was");
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
            Add("splash music", () => MusicIs(MusicCue.Title, "under the splash"), null, null, 0f);
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
                MusicIs(MusicCue.Shop, "in the shop on Day 1");
                Click("hit-crt");
                Check(G.Top is ComputerView, "the computer opened");
            }, "day1_messages");
            Add("close computer", () => Submit("os-close"), "day1_coach_open_sign");
            AspectPass("hit-sign");
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
            // Paid: FIRST_JOB's banner (and QUALITY_90's after it, when the passport was that good).
            Add("samir paid banners", () =>
            {
                var o = OrderOf(StoryCast.SamirId);
                bool q90 = Unlocked(AchievementCatalog.Quality90);
                Note($"samir quality {o?.Quality?.Final:0}: QUALITY_90 {(q90 ? "unlocked" : "not unlocked")}; banners seen: {string.Join(", ", AchSeen)}");
                Check(Unlocked(AchievementCatalog.FirstJob), "Samir's cash unlocked FIRST_JOB");
                Check(AchSeen.Contains(AchievementCatalog.FirstJob), "the FIRST_JOB banner showed");
                Check(q90 == (o?.Quality != null && o.Quality.Final >= 90), $"QUALITY_90 follows the passport's quality ({o?.Quality?.Final:0})");
                if (q90) Check(AchSeen.Contains(AchievementCatalog.Quality90), "the QUALITY_90 banner showed");
            }, null, Soon(() => AchSeen.Contains(AchievementCatalog.FirstJob)
                                && (!Unlocked(AchievementCatalog.Quality90) || AchSeen.Contains(AchievementCatalog.Quality90))
                                && !AchLayerUp() && G.AchToasts.Queued == 0, 20f), 0f);
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
                // Samir's word came with the morning mail: SAMIR_MADE_IT, held while the card is up.
                if (po != null && po.Kind == OutcomeKind.Success) AchHeld(AchievementCatalog.SamirMadeIt, "the Day-2 card");
            }, "day2_card", () => G.Top is DayStartView && S.Day == 2, 1.2f);
            Add("start day 2", () => Submit("daycard-start"), "day2_shop_morning", null, 0.3f);
            Add("samir made it banner", () =>
            {
                Check(AchSeen.Contains(AchievementCatalog.SamirMadeIt), "the SAMIR_MADE_IT banner showed in the shop");
                if (AchShowing(AchievementCatalog.SamirMadeIt)) AchBanner(AchievementCatalog.SamirMadeIt, null, false);
            }, "ach_toast_samir", Soon(() => AchShowing(AchievementCatalog.SamirMadeIt), 10f), 0f,
                skip: () => !Unlocked(AchievementCatalog.SamirMadeIt));
            // R.'s Day-2 tour: THE MAIL -> THE SHOP (the France update, a look at Germany and the
            // sale) -> OPEN UP. It never blocks: the script follows it, then does its own rounds.
            Add("day 2 coach mail", () =>
            {
                CoachCheck("hit-crt", "on the computer", "Day 2 morning");
                Check(CoachCount().StartsWith("THE MAIL"), "Day-2 counter: THE MAIL, got " + CoachCount());
                MusicIs(MusicCue.Shop, "in the shop on Day 2");
            }, "day2_coach_mail", Soon(() => CoachOn("hit-crt")));
            Add("day 2 mail", () => Click("hit-crt"), "day2_messages", null);
            Message dayTwo = null;
            Add("day 2 coach messages", () =>
            {
                dayTwo = S.Inbox.Messages.FirstOrDefault(m => m.Subject == DemoCast.DayTwoSubject && m.ArrivesAt.Day == 2);
                Check(dayTwo != null && !dayTwo.Read, "R.'s day-two note is waiting, unread");
                Check(dayTwo != null && dayTwo.Body.Contains("$80") && dayTwo.Body.Contains("half off"), "R.'s note mentions the $80 update and the half-off gear");
                var pc = G.Find<ComputerView>();
                if (pc != null && pc.CurrentApp != "messages")
                {
                    CoachCheck("nav-messages", "MESSAGES", "Day 2 computer");
                    Submit("nav-messages");
                }
            }, null, Soon(() => CoachOn("nav-messages") || dayTwo != null && CoachOn("msg-" + dayTwo.Id)));
            Add("day 2 coach note", () =>
            {
                CoachCheck("msg-" + dayTwo.Id, "day two", "Day 2 messages");
                Click("msg-" + dayTwo.Id);
                Check(dayTwo.Read, "clicking R.'s note reads it");
            }, null, Soon(() => dayTwo != null && CoachOn("msg-" + dayTwo.Id)));
            Add("day 2 coach to shop", () =>
            {
                CoachCheck("nav-shop", "SHOP", "Day 2 after the mail");
                Check(CoachCount().StartsWith("THE SHOP"), "Day-2 counter: THE SHOP, got " + CoachCount());
            }, "day2_coach_nav_shop", Soon(() => CoachOn("nav-shop")));
            Add("day 2 news", () => Submit("nav-news"), "day2_news_border_watch");
            Add("border watch lock", () =>
            {
                Names("watch-lock-JP", "watch-lock-UK", "watch-lock-IR");
                Click("watch-lock-UK");
                // Long names drop the wishlist line so the nudge stays on one line.
                Check(ToastIs("United Kingdom is in the full game."), "locked Border Watch flag toast");
            }, "day2_border_watch_toast");
            AspectPass("os-close");
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
            AfterShot(() => CoachClearOfButtons("Day-2 SHOP"));
            Add("day 2 coach update", () =>
                {
                    CoachCheck("shop-update-FR", "UPDATE your binder: $80", "Day 2 SHOP");
                    Names("shop-update-FR", "shop-buy-DE");
                }, "day2_coach_shop_update", Soon(() => CoachOn("shop-update-FR")));
            AfterShot(() => CoachClearOfButtons("Day-2 SHOP, the France update"));
            AspectPass("os-close");
            Add("day 2 shop scrolled", () => Scroll("le-os-form--full", 700f), "day2_shop_scrolled");
            AfterShot(() => CoachClearOfButtons("Day-2 SHOP, scrolled"));
            Add("day 2 shop bottom", () => Scroll("le-os-form--full", 10000f), "day2_shop_locked_handbooks_visa");
            AfterShot(() => CoachClearOfButtons("Day-2 SHOP, at the bottom"));
            Add("buy", () =>
            {
                // Germany waits until Anja is at the counter (buying it then gets you her job).
                Scroll("le-os-form--full", 0f);
                Submit("shop-update-FR");
                Check(!S.Country(Countries.France).HandbookOutdated, "bought the France update ($80)");
                Check(!S.Country(Countries.Germany).Owned, "Germany not bought yet");
            }, "day2_shop_bought");
            AfterShot(() => CoachClearOfButtons("Day-2 SHOP, update bought"));
            // After the update: a quiet ring (no arrow) on Germany's BUY and a word on the sale. Your call.
            Add("day 2 coach extras", () =>
            {
                CoachCheck("shop-buy-DE", "half off", "Day 2 after the update", true);
                Check(CoachText().Contains("Your call"), "the extras note leaves it to the player");
            }, "day2_coach_shop_extras", Soon(() => CoachOn("shop-buy-DE", true)));
            AfterShot(() => CoachClearOfButtons("Day-2 SHOP, the extras"));
            Add("day 2 close computer", () => Submit("os-close"), null);
            Add("day 2 coach open", () =>
            {
                CoachCheck("hit-sign", "Flip the sign", "Day 2 after the shop");
                Check(CoachCount().StartsWith("OPEN UP"), "Day-2 counter: OPEN UP, got " + CoachCount());
            }, "day2_coach_open_sign", Soon(() => CoachOn("hit-sign")));
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
                // Open with the update bought: the morning tour is over.
                Check(!CoachShown(), "no Day-2 note once the shop is open and up to date, got: " + CoachText());
            }, "day2_close_refused", null, 0.1f);
            AspectPass("hit-sign");

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
            // Elif's job also runs the player's own retake: booklet made, then "Another photo?".
            Job("elif", DemoCast.ElifId, false, callBack: true);
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
                Check(CuesSeen.Contains(MusicCue.Busy), "the rush job played the busy music");
                MusicIs(MusicCue.Shop, "after the rush job is delivered");
                // White backdrop on the day France changed it, and before his deadline.
                Check(Unlocked(AchievementCatalog.ReadThePaper), "Viktor's redo unlocked READ_THE_PAPER");
                Check(Unlocked(AchievementCatalog.RushJob), "Viktor's rush job unlocked RUSH_JOB");
            }, null);
            // The sale: the photo station's upgrade for $350, with Germany's $250 still affordable.
            Add("upgrade shop", () =>
            {
                Click("hit-crt");
                G.Find<ComputerView>()?.OpenApp("shop");
                Check(G.Top is ComputerView pc && pc.CurrentApp == "shop", "the computer's SHOP");
            }, null);
            Add("upgrade buy", () =>
            {
                var stn2 = EquipmentCatalog.Get("stn2");
                int price = S.PriceOf(stn2), cash = S.Ledger.Cash;
                Note($"upgrade: stn2 {Labels.Money(price)} on sale {S.SaleActive}, cash {Labels.Money(cash)}");
                Check(S.SaleActive && price == 350, $"the photo station upgrade is $350 on sale (got {Labels.Money(price)})");
                Check(cash - price >= 250, $"cash for the upgrade and Germany after it ({Labels.Money(cash)})");
                Submit("shop-equip-photostation");
                Check(S.Station.Id == "stn2", $"bought the Adjustable Backdrop Stand (station {S.Station.Id})");
                Check(Unlocked(AchievementCatalog.FirstUpgrade) || S.Station.Tier >= 2, "the upgrade counts");
            }, null);
            Add("upgrade banner", () =>
            {
                Check(Unlocked(AchievementCatalog.FirstUpgrade), "the upgrade unlocked FIRST_UPGRADE");
                AchBanner(AchievementCatalog.FirstUpgrade, null, true);
            }, "ach_toast_computer", Soon(() => AchShowing(AchievementCatalog.FirstUpgrade), 30f), 0f);
            Add("upgrade close computer", () => Submit("os-close"), null);
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
            // R. says where the section is: COMPUTER, then SHOP, then Germany's BUY.
            Add("anja coach computer", () => CoachCheck("hit-crt", "Click the COMPUTER, then SHOP: it's $250", "Anja without a section"),
                "anja_coach_computer", Soon(() => CoachOn("hit-crt")));
            Add("anja open computer", () => Click("hit-crt"), null);
            Add("anja coach shop", () =>
            {
                CoachCheck("nav-shop", "SHOP", "Anja, computer open");
                Submit("nav-shop");
                Scroll("le-os-form--full", 0f);
            }, "anja_coach_nav_shop", Soon(() => CoachOn("nav-shop")));
            AfterShot(() => CoachClearOfButtons("Anja, SHOP open"));
            Add("anja buy germany", () =>
            {
                CoachCheck("shop-buy-DE", "Germany: BUY", "Anja, in the SHOP");
                Submit("shop-buy-DE");
                Check(S.Country(Countries.Germany).Owned, "bought the Germany section ($250) with Anja at the counter");
                Check(anja().Choices.Count == 0 && anja().IsTalking, "Anja is re-offered the job");
            }, "anja_shop_bought_germany");
            AfterShot(() => CoachClearOfButtons("Anja, Germany bought"));
            Add("anja close computer", () => Submit("os-close"), null);
            Job("anja", DemoCast.AnjaId, false);
            Add("anja lines", () => Check(LinesSeen.Contains(DemoCast.HandbookBoughtLine), $"Anja: \"{DemoCast.HandbookBoughtLine}\""), null, null, 0f);
            // Germany bought while she waited, then her job taken.
            Add("anja just in time", () =>
            {
                Check(Unlocked(AchievementCatalog.JustInTime), "buying Germany with Anja at the counter unlocked JUST_IN_TIME");
                Check(AchSeen.Contains(AchievementCatalog.JustInTime), "the JUST_IN_TIME banner showed");
            }, null, Soon(() => AchSeen.Contains(AchievementCatalog.JustInTime), 10f), 0f);
            // Pause -> ACHIEVEMENTS: the count agrees with the book (all demo runs).
            Add("pause achievements", () =>
            {
                G.Open(new PauseView(G));
                Names("pause-achievements");
                Submit("pause-achievements");
                Check(G.Top is AchievementsView, "ACHIEVEMENTS opened from the pause menu");
                AchCount("from the pause menu");
                int n = Book?.CountUnlocked(true) ?? 0;
                Check(n >= 7, $"at least 7 demo achievements by now (got {n})");
                foreach (var id in new[]
                         {
                             AchievementCatalog.FirstPhoto, AchievementCatalog.PercussiveMaintenance, AchievementCatalog.FirstJob,
                             AchievementCatalog.SamirMadeIt, AchievementCatalog.ReadThePaper, AchievementCatalog.RushJob,
                             AchievementCatalog.FirstUpgrade, AchievementCatalog.JustInTime,
                         })
                    Check(G.Stage.Q("ach-cell-" + id)?.ClassListContains("le-ach__cell--unlocked") == true, $"the {id} cell is unlocked");
                var all = AchievementCatalog.All.Where(d => Book != null && (Book.Unlocked(d) || Book.Banked(d)))
                    .Select(d => d.Id + (Book.Banked(d) ? " (banked)" : ""));
                Note("achievements so far: " + string.Join(", ", all));
            }, "achievements_pause");
            Add("pause achievements back", () =>
            {
                Submit("ach-back");
                Check(G.Top is PauseView && G.Top.Root.visible, "BACK returns to the pause menu, shown again");
                Submit("pause-resume");
                Check(G.Top == null, "back in the shop");
            }, null);
            Add("quiet afternoon", () => Check(S.Inbox.Messages.Any(m => m.Body == DemoCast.QuietBody && m.From == "R."), "R. says close up"),
                "day2_quiet_afternoon", () => S.Inbox.Messages.Any(m => m.Body == DemoCast.QuietBody), 0.5f, 30f);
            CloseEarly("day2");

            // ---------------- the end
            Add("demo over", () =>
            {
                Check(S.DemoOver, "the demo is over after the Day-2 report");
                Names("report-finish");
                Check(G.Stage.Q("report-next") == null, "no NEXT DAY on the last report");
                // The SHOP screens with R.'s note up were checked for covered buttons (see CoachClearOfButtons).
                Check(_coachShopChecks >= 3, $"R.'s note was checked against the SHOP's buttons at least 3 times (got {_coachShopChecks})");
                // Every demo achievement the run earned before the last report showed its banner.
                foreach (var id in new[]
                         {
                             AchievementCatalog.FirstPhoto, AchievementCatalog.PercussiveMaintenance, AchievementCatalog.FirstJob,
                             AchievementCatalog.SamirMadeIt, AchievementCatalog.ReadThePaper, AchievementCatalog.RushJob,
                             AchievementCatalog.FirstUpgrade, AchievementCatalog.JustInTime,
                         })
                    Check(AchSeen.Contains(id), $"the {id} banner showed during the run");
                Note("achievement banners seen: " + string.Join(", ", AchSeen));
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
                MusicIs(MusicCue.End, "on the demo end");
                // How many of the demo's twelve (all runs), plus what's waiting in the full game.
                int n = Book?.CountUnlocked(true) ?? -1, banked = Book?.CountBanked() ?? 0;
                string want = $"You unlocked {n} of {AchievementCatalog.DemoCount} demo achievements.";
                if (banked > 0) want += $" {banked} more {(banked == 1 ? "is" : "are")} waiting in the full game.";
                string line = G.Stage.Q<Label>("demoend-achievements")?.text;
                Note($"demo end achievements: \"{line}\" (banked {banked})");
                Check(line == want, $"the demo end says \"{want}\", got \"{line}\"");
                Check(G.AchToasts != null && !AchLayerUp() && G.AchToasts.Queued == 0, "no achievement banner over the demo end, none queued");
            }, "demo_end", () => G.Top is DemoEndView, 1.2f);
            AspectPass("demoend-wishlist");
            Add("demo end wishlist", () =>
            {
                _wishlistOpens = StoreLinks.OpenCount;
                Submit("demoend-wishlist");
                Check(StoreLinks.OpenCount == _wishlistOpens + 1, "demo-end WISHLIST opens the store page");
            }, "demo_end_wishlist");
            Add("still the end", () => Check(G.Top is DemoEndView && S.Day == 2, "nothing moves on behind the demo end"), null, null, 1f);
            Add("play again", () => Submit("demoend-again"), null);
            Add("title again", () =>
            {
                Check(S.IsDemo && S.Day == 1 && S.Phase == DayPhase.Morning, "PLAY AGAIN starts a fresh demo");
                MusicIs(MusicCue.Title, "back on the title");
                foreach (var cue in new[] { MusicCue.Title, MusicCue.Shop, MusicCue.Busy, MusicCue.End })
                    Check(CuesSeen.Contains(cue), "music cue played at some point: " + cue);
                Note("music cues seen: " + string.Join(", ", CuesSeen));
                Check(_aspectShots == 6 * AspectSizes.Length, $"the aspect pass ran at 6 points x {AspectSizes.Length} sizes (got {_aspectShots})");
                foreach (var size in AspectSizes)
                    foreach (var kind in new[] { "note", "toast", "hint" })
                        Check(AspectSeen.Contains($"{kind} @ {size.x}x{size.y}"), $"the aspect pass saw a {kind} at {size.x}x{size.y}");
            }, "title_again", () => G.Top is TitleView, 1f);
        }
    }
}
#endif
