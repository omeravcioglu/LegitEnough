#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using LegitEnough.Game;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;
using Box = LegitEnough.Sim.Box;
using Calendar = LegitEnough.Sim.Calendar;

namespace LegitEnoughCI
{
    /// <summary>
    /// Plays a scripted session for the trailer at a fixed 1/60 s step and records "takes":
    /// every rendered frame goes straight into ffmpeg (lossless RGB, 1080p60) and every sound
    /// the game asks for, plus footsteps, flashes and clicks, is logged with its frame number
    /// so the trailer audio can be built in sync. A pixel cursor drives the real UI through
    /// synthetic pointer events; when the UI doesn't react, the step falls back to the session
    /// API and the log says FALLBACK.
    /// Only ever runs inside the throwaway copy made by Tools/Trailer/trailer.ps1.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class TrailerRig : MonoBehaviour
    {
        const int W = 1920, H = 1080, Art = 4;
        const float VfStep = 38f;
        const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static string Out;
        public static string Ffmpeg = "ffmpeg";
        public static string Only;
        public static Action<int> Exit;

        static GameRoot G => GameRoot.Instance;
        static GameSession S => G.Session;

        readonly List<IEnumerator> _stack = new List<IEnumerator>();
        readonly List<string> _errors = new List<string>();
        readonly System.Random _rand = new System.Random(2008);
        string _log;
        int _frame;
        bool _finished;

        // ------------------------------------------------------------ capture
        RenderTexture _rt;
        Texture2D _tex;
        byte[] _buf;
        System.Diagnostics.Process _ff;
        Stream _ffIn;
        StreamWriter _events;
        string _take;
        int _takeStart;
        int _captured;
        readonly StringBuilder _takes = new StringBuilder();

        // world state behind the event log
        readonly Dictionary<int, float> _walkPhase = new Dictionary<int, float>();
        readonly Dictionary<int, string> _lines = new Dictionary<int, string>();
        bool _printerOn, _power = true, _swapping, _toasts;
        string _top = "";
        int _shots, _sfxCount;
        HeadPose _pose;
        float _zoom;

        // ------------------------------------------------------------ cursor
        PixelImage _cursorImg, _burstImg;
        PixelBuffer _arrowUp, _arrowDown;
        readonly PixelBuffer _burst = new PixelBuffer(25, 25);
        Vector2 _pos = new Vector2(1500f, 940f);
        Vector2 _lastSent;
        bool _cursorOn, _down;
        bool? _shownDown;
        int _burstAge = -1;
        Vector2 _burstPos;
        float _arc = 1f;

        // ============================================================ lifecycle

        void Start()
        {
            _log = Path.Combine(Out, "log.txt");
            File.WriteAllText(_log, "trailer capture " + DateTime.Now + "\n");
            Application.logMessageReceived += OnLog;
            Sfx.Hook = OnSfx;
            _stack.Add(Script());
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            Sfx.Hook = null;
        }

        void Update()
        {
            if (_finished) return;
            _frame++;
            try
            {
                // The render texture now holds the previous frame: that frame belongs to the take.
                if (_take != null && _frame > _takeStart) Capture();
                Clean();
                Step();
                TickCursor();
                if (_frame > 60 * 60 * 40) Fail("global timeout");
            }
            catch (Exception e)
            {
                Fail("exception: " + e);
            }
        }

        void LateUpdate()
        {
            if (_finished || _events == null || G == null || G.Session == null) return;
            try
            {
                LogWorld();
            }
            catch (Exception e)
            {
                Note("log world: " + e.Message);
            }
        }

        void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (msg.Contains("Curl error") || msg.Contains("licens")) return;
            _errors.Add(msg + "\n" + stack);
            Note("[" + type + "] " + msg);
        }

        void Note(string text)
        {
            if (_log != null) File.AppendAllText(_log, $"[{_frame}] {text}\n");
        }

        void Fail(string why)
        {
            _errors.Add(why);
            Note("FAIL " + why);
            Finish();
        }

        void Finish()
        {
            if (_finished) return;
            _finished = true;
            _stack.Clear();
            try { StopRec(); } catch (Exception e) { _errors.Add("stop: " + e.Message); }
            Time.captureDeltaTime = 0f;
            Note($"DONE: {_frame} frames, {_errors.Count} errors");
            File.WriteAllText(Path.Combine(Out, "errors.txt"), string.Join("\n-----\n", _errors));
            Exit?.Invoke(_errors.Count == 0 ? 0 : 1);
        }

        // ============================================================ coroutine runner

        void Step()
        {
            int guard = 0;
            while (_stack.Count > 0 && !_finished)
            {
                if (++guard > 100000) throw new Exception("script spins without yielding");
                var top = _stack[_stack.Count - 1];
                if (!top.MoveNext())
                {
                    _stack.RemoveAt(_stack.Count - 1);
                    continue;
                }
                if (top.Current is IEnumerator nested)
                {
                    _stack.Add(nested);
                    continue;
                }
                return;
            }
            if (!_finished && _stack.Count == 0) Finish();
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Sec(float seconds) => Frames(Mathf.Max(1, Mathf.RoundToInt(seconds * 60f)));

        IEnumerator Until(Func<bool> cond, float timeout, string what, bool fatal = true)
        {
            int limit = Mathf.RoundToInt(timeout * 60f);
            for (int i = 0; !cond(); i++)
            {
                if (i >= limit)
                {
                    if (fatal) Fail("timeout: " + what + $" (day {S?.Day} {S?.Clock.Minute:0.0} {S?.Phase})");
                    else Note("gave up waiting: " + what);
                    yield break;
                }
                yield return null;
            }
        }

        // ============================================================ capture

        void SetupCapture()
        {
            var doc = G.GetComponentInChildren<UIDocument>();
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _rt.Create();
            doc.panelSettings.targetTexture = _rt;
            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            _buf = new byte[W * H * 4];
        }

        void Rec(string name)
        {
            StopRec();
            if (!string.IsNullOrEmpty(Only) && Array.IndexOf(Only.Split(','), name) < 0) return;
            string dir = Path.Combine(Out, "takes");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, name + ".mkv");
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Ffmpeg,
                Arguments = $"-y -hide_banner -loglevel error -f rawvideo -pix_fmt rgba -s {W}x{H} -framerate 60 -i - -vf vflip -c:v libx264rgb -preset ultrafast -crf 0 \"{file}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            _ff = System.Diagnostics.Process.Start(psi);
            _ffIn = _ff.StandardInput.BaseStream;
            _events = new StreamWriter(Path.Combine(dir, name + ".jsonl"), false, new UTF8Encoding(false));
            _take = name;
            _takeStart = _frame;
            _captured = 0;
            _printerOn = false;
            _power = S.PowerOn;
            _top = "";
            _lines.Clear();
            Ev("take", $"\"name\":{Q(name)},\"day\":{S.Day},\"minute\":{F(S.Clock.Minute)},\"tier\":{S.Camera.Tier}");
            Note($"rec {name} (day {S.Day} {Calendar.ClockText(S.Clock.Minute)})");
        }

        void StopRec()
        {
            if (_take == null) return;
            try
            {
                _ffIn.Flush();
                _ffIn.Close();
            }
            catch (Exception e)
            {
                _errors.Add("ffmpeg pipe: " + e.Message);
            }
            if (!_ff.WaitForExit(300000))
            {
                _errors.Add("ffmpeg hung on " + _take);
                try { _ff.Kill(); } catch { }
            }
            else if (_ff.ExitCode != 0)
            {
                _errors.Add($"ffmpeg exit {_ff.ExitCode} on {_take}");
            }
            _events.Close();
            _takes.AppendLine($"{_take}\t{_captured}");
            File.WriteAllText(Path.Combine(Out, "takes", "index.tsv"), _takes.ToString());
            Note($"stop {_take}: {_captured} frames");
            _take = null;
            _ff = null;
            _ffIn = null;
            _events = null;
        }

        void Capture()
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, W, H), 0, 0, false);
            RenderTexture.active = prev;
            _tex.GetRawTextureData<byte>().CopyTo(_buf);
            _ffIn.Write(_buf, 0, _buf.Length);
            _captured++;
        }

        // ============================================================ event log

        void Ev(string type, string extra = null)
        {
            if (_events == null) return;
            _events.Write("{\"f\":");
            _events.Write((_frame - _takeStart).ToString(CultureInfo.InvariantCulture));
            _events.Write(",\"t\":\"");
            _events.Write(type);
            _events.Write('"');
            if (!string.IsNullOrEmpty(extra))
            {
                _events.Write(',');
                _events.Write(extra);
            }
            _events.WriteLine("}");
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ") + "\"";

        static string Pos(Vector2 p) => $"\"x\":{F(p.x)},\"y\":{F(p.y)}";

        void OnSfx(string id, float v, float p)
        {
            if (id == "Printer")
            {
                bool on = v > 0.5f;
                if (on != _printerOn)
                {
                    _printerOn = on;
                    Ev("printer", "\"on\":" + (on ? 1 : 0));
                }
                return;
            }
            _sfxCount++;
            Ev("sfx", $"\"id\":{Q(id)},\"v\":{F(v)},\"p\":{F(p)}");
        }

        void LogWorld()
        {
            // Footfalls: the walk cycle crosses a half-turn.
            if (Field(G.Shop, "_figures") is System.Collections.IDictionary figs)
            {
                foreach (System.Collections.DictionaryEntry kv in figs)
                {
                    if (!(kv.Value is CustomerFigure fig)) continue;
                    int id = (int)kv.Key;
                    float ph = (float)Field(fig, "_phase");
                    if (_walkPhase.TryGetValue(id, out var last) && Mathf.FloorToInt(ph / Mathf.PI) > Mathf.FloorToInt(last / Mathf.PI))
                        Ev("step", $"\"x\":{F(fig.HeadStage.x)},\"s\":{F(fig.Depth)}");
                    _walkPhase[id] = ph;
                }
            }
            foreach (var v in S.Visits)
            {
                string text = v.Line?.Text;
                _lines.TryGetValue(v.Id, out var last);
                if (text != null && text != last)
                {
                    var who = v.Line.ByCompanion && v.Companion != null ? v.Companion : v.Customer;
                    Ev("line", $"\"who\":{Q(who.FirstName)},\"pitch\":{F(who.VoicePitch)},\"text\":{Q(text)}");
                }
                _lines[v.Id] = text;
            }
            if (S.PowerOn != _power)
            {
                _power = S.PowerOn;
                Ev("power", "\"on\":" + (_power ? 1 : 0));
            }
            string top = G.Top?.GetType().Name ?? "";
            if (top != _top)
            {
                _top = top;
                Ev("top", $"\"name\":{Q(top)}");
            }
            var ps = S.ActivePhoto;
            if (ps == null)
            {
                _shots = 0;
                return;
            }
            if (ps.Shots.Count > _shots) Ev("flash");
            _shots = ps.Shots.Count;
            if (ps.Subject.Pose != _pose)
            {
                _pose = ps.Subject.Pose;
                Ev("pose", $"\"yaw\":{_pose.Yaw},\"pitch\":{_pose.Pitch}");
            }
            if (Mathf.Abs(ps.Rig.Zoom - _zoom) > 0.01f)
            {
                _zoom = ps.Rig.Zoom;
                Ev("zoom", $"\"z\":{F(_zoom)}");
            }
            if (ps.Rig.SwappingBackdrop && !_swapping) Ev("backdrop");
            _swapping = ps.Rig.SwappingBackdrop;
        }

        // ============================================================ store-look cleanup

        void Clean()
        {
            if (G == null || G.Stage == null) return;
            G.ShowHints = false;
            var toasts = _toasts ? DisplayStyle.Flex : DisplayStyle.None;
            G.Stage.Query(className: "le-toasts").ForEach(e => e.style.display = toasts);
            G.Stage.Query(className: "le-hint").ForEach(e => e.style.display = DisplayStyle.None);
            G.Stage.Query(className: "le-coach").ForEach(e => e.style.display = DisplayStyle.None);
        }

        // ============================================================ cursor

        static PixelBuffer Arrow(bool pressed)
        {
            string[] rows =
            {
                "X.......",
                "XX......",
                "XoX.....",
                "XooX....",
                "XoooX...",
                "XooooX..",
                "XoooooX.",
                "XooooooX",
                "XoooXXXX",
                "XooX....",
                "XoX.....",
                "XX......",
            };
            var b = new PixelBuffer(9, 13);
            var ink = new Color32(22, 18, 15, 255);
            var fill = pressed ? new Color32(206, 196, 170, 255) : new Color32(246, 238, 218, 255);
            var shadow = new Color32(0, 0, 0, 120);
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] != '.') b.Px[(y + 1) * b.W + x + 1] = shadow;
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char ch = rows[y][x];
                    if (ch == 'X') b.Px[y * b.W + x] = ink;
                    else if (ch == 'o') b.Px[y * b.W + x] = fill;
                }
            return b;
        }

        void DrawBurst(int age)
        {
            for (int i = 0; i < _burst.Px.Length; i++) _burst.Px[i] = new Color32(0, 0, 0, 0);
            float r = 3f + age * 0.75f;
            byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(1f - age / 11f));
            var c = new Color32(246, 238, 218, a);
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI / 4f;
                for (float d = r; d <= r + 1.6f; d += 0.4f)
                {
                    int x = 12 + Mathf.RoundToInt(Mathf.Cos(ang) * d);
                    int y = 12 + Mathf.RoundToInt(Mathf.Sin(ang) * d);
                    if (x >= 0 && y >= 0 && x < 25 && y < 25) _burst.Px[y * 25 + x] = c;
                }
            }
            _burstImg.Upload(_burst);
        }

        void TickCursor()
        {
            if (G == null || G.Stage == null) return;
            if (_cursorImg == null || _cursorImg.parent != G.Stage)
            {
                _arrowUp = _arrowUp ?? Arrow(false);
                _arrowDown = _arrowDown ?? Arrow(true);
                _burstImg = new PixelImage { pickingMode = PickingMode.Ignore };
                _burstImg.style.position = Position.Absolute;
                _burstImg.style.width = 25 * Art;
                _burstImg.style.height = 25 * Art;
                _cursorImg = new PixelImage { pickingMode = PickingMode.Ignore };
                _cursorImg.style.position = Position.Absolute;
                _cursorImg.style.width = 9 * Art;
                _cursorImg.style.height = 13 * Art;
                G.Stage.Add(_burstImg);
                G.Stage.Add(_cursorImg);
                _shownDown = null;
            }
            _burstImg.BringToFront();
            _cursorImg.BringToFront();
            if (_shownDown != _down)
            {
                _cursorImg.Upload(_down ? _arrowDown : _arrowUp);
                _shownDown = _down;
            }
            _cursorImg.style.display = _cursorOn ? DisplayStyle.Flex : DisplayStyle.None;
            _cursorImg.style.left = Mathf.Round(_pos.x);
            _cursorImg.style.top = Mathf.Round(_pos.y);
            bool burst = _cursorOn && _burstAge >= 0 && _burstAge <= 11;
            _burstImg.style.display = burst ? DisplayStyle.Flex : DisplayStyle.None;
            if (burst)
            {
                DrawBurst(_burstAge);
                _burstImg.style.left = Mathf.Round(_burstPos.x) - 12 * Art - Art / 2;
                _burstImg.style.top = Mathf.Round(_burstPos.y) - 12 * Art - Art / 2;
            }
            if (_burstAge >= 0 && ++_burstAge > 11) _burstAge = -1;
        }

        void SendPointer(EventType type, Vector2 p)
        {
            var panel = G != null && G.Stage != null ? G.Stage.panel : null;
            if (panel == null) return;
            var e = new Event { type = type, mousePosition = p, delta = p - _lastSent, button = 0, clickCount = 1 };
            _lastSent = p;
            EventBase pe;
            if (type == EventType.MouseDown) pe = PointerDownEvent.GetPooled(e);
            else if (type == EventType.MouseUp) pe = PointerUpEvent.GetPooled(e);
            else pe = PointerMoveEvent.GetPooled(e);
            using (pe) panel.visualTree.SendEvent(pe);
        }

        void SetPos(Vector2 p)
        {
            _pos = p;
            SendPointer(_down ? EventType.MouseDrag : EventType.MouseMove, p);
        }

        void Press()
        {
            _down = true;
            SendPointer(EventType.MouseDown, _pos);
            _burstAge = 0;
            _burstPos = _pos;
            Ev("down", Pos(_pos));
        }

        void Release()
        {
            _down = false;
            SendPointer(EventType.MouseUp, _pos);
            Ev("up", Pos(_pos));
        }

        /// <summary>Minimum-jerk move along a slight arc, like a hand on a mouse.</summary>
        IEnumerator MoveTo(Vector2 to, float seconds = -1f)
        {
            var from = _pos;
            float dist = Vector2.Distance(from, to);
            if (dist < 1f)
            {
                SetPos(to);
                yield break;
            }
            if (seconds < 0f) seconds = Mathf.Clamp(0.2f + dist / 2400f, 0.24f, 0.62f);
            int n = Mathf.Max(2, Mathf.RoundToInt(seconds * 60f));
            _arc = -_arc;
            var dir = (to - from) / dist;
            var perp = new Vector2(-dir.y, dir.x) * dist * 0.07f * _arc;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                float e = t * t * t * (t * (t * 6f - 15f) + 10f);
                SetPos(Vector2.Lerp(from, to, e) + perp * (4f * e * (1f - e)));
                yield return null;
            }
        }

        /// <summary>Move, press, release. If <paramref name="done"/> never comes true (or, without it, the game makes no sound), run the fallback.</summary>
        IEnumerator Click(Vector2 p, Func<bool> done, Action fallback, string what)
        {
            yield return MoveTo(p);
            yield return Frames(3);
            int sfx = _sfxCount;
            Press();
            yield return Frames(5);
            Release();
            Func<bool> ok = done ?? (() => _sfxCount > sfx);
            for (int i = 0; i < 14 && !ok(); i++) yield return null;
            if (!ok())
            {
                Note("FALLBACK " + what);
                fallback?.Invoke();
            }
            yield return Frames(4);
        }

        IEnumerator ClickBtn(string text, Func<bool> done = null, Action fallback = null)
        {
            Button b = null;
            for (int i = 0; i < 20 && (b = FindBtn(text)) == null; i++) yield return null;
            if (b == null)
            {
                Note("FALLBACK (no button) " + text);
                fallback?.Invoke();
                yield break;
            }
            var btn = b;
            yield return Click(Center(b), done, fallback ?? (() => Submit(btn)), "button " + text);
        }

        IEnumerator ClickEl(VisualElement el, Func<bool> done, Action fallback, string what)
        {
            if (el == null)
            {
                Note("FALLBACK (no element) " + what);
                fallback?.Invoke();
                yield break;
            }
            yield return Click(Center(el), done, fallback, what);
        }

        IEnumerator Drag(Vector2 from, Vector2 to, float seconds, float hold = 0.12f)
        {
            yield return MoveTo(from);
            yield return Frames(4);
            Press();
            yield return Frames(6);
            yield return MoveTo(to, seconds);
            yield return Sec(hold);
            Release();
            yield return Frames(4);
        }

        static void Submit(Button b)
        {
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                b.SendEvent(e);
            }
        }

        IEnumerator TypeInto(TextField tf, string text)
        {
            yield return Click(Center(tf), () => true, null, "field " + tf.label);
            tf.value = "";
            for (int i = 1; i <= text.Length; i++)
            {
                tf.value = text.Substring(0, i);
                if (text[i - 1] != ' ') Ev("key", Pos(_pos));
                double r = _rand.NextDouble();
                yield return Frames(text[i - 1] == ' ' ? 6 : 3 + (int)(r * 4));
            }
            yield return Frames(6);
        }

        // ============================================================ finding things

        static Vector2 Center(VisualElement e) => e.worldBound.center;

        static bool Visible(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden) return false;
            return e.worldBound.width > 0f;
        }

        static Button FindBtn(string text, VisualElement scope = null)
        {
            Button found = null;
            (scope ?? G.Stage).Query<Button>().ForEach(b =>
            {
                if (found == null && b.text == text && b.enabledInHierarchy && Visible(b)) found = b;
            });
            return found;
        }

        static Button FindBtnStarting(string prefix)
        {
            Button found = null;
            G.Stage.Query<Button>().ForEach(b =>
            {
                if (found == null && b.text != null && b.text.StartsWith(prefix, StringComparison.Ordinal) && b.enabledInHierarchy && Visible(b)) found = b;
            });
            return found;
        }

        /// <summary>A button in the editor row whose label reads <paramref name="rowLabel"/>.</summary>
        static Button FindInRow(string rowLabel, string text)
        {
            Button found = null;
            G.Stage.Query<Label>(className: "le-os-label").ForEach(l =>
            {
                if (found == null && l.text == rowLabel && l.parent != null) found = FindBtn(text, l.parent);
            });
            return found;
        }

        static VisualElement FindNav(string label)
        {
            VisualElement found = null;
            G.Stage.Query<Label>(className: "le-os-nav__label").ForEach(l =>
            {
                if (found == null && l.text == label) found = l.parent;
            });
            return found;
        }

        static TextField FindField(DocField f)
        {
            TextField found = null;
            string label = Labels.Of(f);
            G.Stage.Query<TextField>().ForEach(t =>
            {
                if (found == null && t.label == label) found = t;
            });
            return found;
        }

        static object Field(object o, string name)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, AnyField);
                if (f != null) return f.GetValue(o);
            }
            throw new MissingFieldException(o.GetType().Name, name);
        }

        static void SetField(object o, string name, object value)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, AnyField);
                if (f == null) continue;
                f.SetValue(o, value);
                return;
            }
            throw new MissingFieldException(o.GetType().Name, name);
        }

        static Visit Samir => S.Visits.Find(v => v.Customer.Id == StoryCast.SamirId);

        static CustomerFigure FigureOf(Visit v)
        {
            if (v == null || !(Field(G.Shop, "_figures") is System.Collections.IDictionary figs)) return null;
            return figs.Contains(v.Id) ? figs[v.Id] as CustomerFigure : null;
        }

        static void DropArrivals(bool all)
        {
            var list = (List<ScheduledArrival>)Field(S.Director, "_arrivals");
            list.RemoveAll(a => all || a.Kind != ArrivalKind.Story);
        }

        static ulong PickSeed()
        {
            for (ulong s = 20081006; s < 20081006 + 50000; s++)
                if (ShopMood.WeatherFor(s, 1) == Weather.Rain && ShopMood.WeatherFor(s, 100) == Weather.Snow) return s;
            return 20081006;
        }

        void SaveBuffer(PixelBuffer b, string name)
        {
            var tex = new Texture2D(b.W, b.H, TextureFormat.RGBA32, false);
            var px = new Color32[b.W * b.H];
            for (int y = 0; y < b.H; y++)
                for (int x = 0; x < b.W; x++)
                    px[(b.H - 1 - y) * b.W + x] = b.Px[y * b.W + x];
            tex.SetPixels32(px);
            tex.Apply();
            Directory.CreateDirectory(Path.Combine(Out, "art"));
            File.WriteAllBytes(Path.Combine(Out, "art", name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }

        // ============================================================ camera helpers

        static PortraitView Viewfinder => G.Top?.Root.Q<PortraitView>(className: "le-viewfinder__image");

        static Vector2 WorldToScreen(float wx, float wy)
        {
            var ps = S.ActivePhoto;
            var vf = Viewfinder;
            var fr = PortraitGeometry.FrameWorld(ps.Rig.Zoom, ps.Rig.PanX, ps.Rig.PanY);
            var local = vf.FrameToLocal(new Vector2((wx - fr.X) / fr.W, (wy - fr.Y) / fr.H));
            return vf.LocalToWorld(local);
        }

        static Vector2 HeadScreen()
        {
            var sub = S.ActivePhoto.Subject;
            var skull = PortraitGeometry.SkullWorld(sub.Customer.Look, sub.Pose, sub.SeatOffset, sub.Slouch);
            return WorldToScreen(skull.CenterX, skull.CenterY + skull.H * 0.12f);
        }

        static Vector2 GlassesScreen()
        {
            var sub = S.ActivePhoto.Subject;
            var pose = sub.Pose;
            var skull = PortraitGeometry.SkullWorld(sub.Customer.Look, pose, sub.SeatOffset, sub.Slouch);
            float eyeY = skull.CenterY - 0.02f * skull.H - pose.PitchF * 0.07f * skull.H;
            float fx = skull.CenterX + pose.YawF * 0.14f * skull.W;
            return WorldToScreen(fx + skull.W * 0.18f, eyeY);
        }

        /// <summary>Grab the head and walk it through poses (yaw, pitch), holding each a moment.</summary>
        IEnumerator HeadWiggle(Vector2Int[] poses, float hold)
        {
            var sub = S.ActivePhoto.Subject;
            var start = HeadScreen();
            yield return MoveTo(start);
            yield return Frames(4);
            var p0 = sub.Target;
            Press();
            yield return Frames(6);
            foreach (var t in poses)
            {
                var to = start + new Vector2((t.x - p0.Yaw) * VfStep, -(t.y - p0.Pitch) * VfStep);
                yield return MoveTo(to, 0.26f);
                yield return Sec(hold);
            }
            Release();
            yield return Frames(6);
            var last = poses[poses.Length - 1];
            if (sub.Target.Yaw != last.x || sub.Target.Pitch != last.y)
            {
                Note($"FALLBACK head drag (target {sub.Target.Yaw},{sub.Target.Pitch})");
                sub.BeginDrag();
                sub.DragTo(last.x, last.y);
                sub.EndDrag();
            }
        }

        /// <summary>Player-style corrections until the subject is square, neutral and looking in.</summary>
        IEnumerator GetReady()
        {
            var sub = S.ActivePhoto.Subject;
            for (int tries = 0; tries < 14; tries++)
            {
                bool face = sub.Expression == Expression.Neutral;
                bool look = Mathf.Abs(sub.GazeX) < 0.3f && Mathf.Abs(sub.GazeY) < 0.3f;
                bool square = sub.Pose.Yaw == 0 && sub.Pose.Pitch == 0 && sub.Target.Yaw == 0 && sub.Target.Pitch == 0;
                if (face && look && square && !sub.InMotion && sub.Lid < 0.2f) yield break;
                if (!square && !sub.InMotion) yield return HeadWiggle(new[] { new Vector2Int(0, 0) }, 0.15f);
                else if (!face) yield return ClickBtn(FindBtn("STOP SMILING") != null ? "STOP SMILING" : "RELAX YOUR FACE");
                else if (!look) yield return ClickBtn("LOOK AT THE CAMERA");
                else if (sub.Lid >= 0.2f && FindBtn("OPEN YOUR EYES") != null) yield return ClickBtn("OPEN YOUR EYES");
                yield return Sec(0.35f);
            }
            Note("subject never fully ready");
        }

        /// <summary>Times the subject's next blink against the shutter lag: shut for the bad take, open for the good one.</summary>
        void TimeBlink(bool shut)
        {
            var ps = S.ActivePhoto;
            var sub = ps.Subject;
            float pending = (float)Field(ps, "_pendingShot");
            float dur = sub.Customer.Behavior.BlinkDuration;
            SetField(sub, "_blinkT", -1f);
            SetField(sub, "_blinkIn", shut ? Mathf.Max(0.0001f, pending - 0.5f * dur - 1f / 60f) : pending + 0.8f);
        }

        IEnumerator Shutter(bool blink)
        {
            var ps = S.ActivePhoto;
            int before = ps.Shots.Count;
            yield return ClickBtn("SHUTTER", () => ps.ShotPending || ps.Shots.Count > before, () => (G.Top as CameraView)?.Shutter());
            if (ps.ShotPending) TimeBlink(blink);
            yield return Until(() => G.Top is ReviewView, 6f, "review");
        }

        IEnumerator FrameFace(float zoom)
        {
            var ps = S.ActivePhoto;
            var zin = FindBtn("ZOOM +");
            if (zin != null)
            {
                yield return MoveTo(Center(zin));
                while (ps.Rig.Zoom < zoom - 0.01f)
                {
                    float z = ps.Rig.Zoom;
                    yield return Click(Center(zin), () => ps.Rig.Zoom > z, () => ps.ZoomBy(0.25f), "zoom");
                    yield return Frames(5);
                }
            }
            else
            {
                ps.Rig.Zoom = zoom;
            }
            yield return Sec(0.25f);
            // Drag the background so the face sits a little above the middle.
            var sub = ps.Subject;
            var head = PortraitGeometry.HeadWorld(sub.Customer.Look, sub.Pose, sub.SeatOffset, sub.Slouch, sub.HatOn);
            float wantX = head.CenterX, wantY = head.CenterY + 0.25f * 2f / ps.Rig.Zoom;
            var fr = PortraitGeometry.FrameWorld(ps.Rig.Zoom, ps.Rig.PanX, ps.Rig.PanY);
            float pxPerUnit = 880f / fr.H;
            var delta = new Vector2((ps.Rig.PanX - wantX) * pxPerUnit, (ps.Rig.PanY - wantY) * pxPerUnit);
            if (delta.magnitude < 8f) yield break;
            var vf = Viewfinder;
            Vector2 grab = vf.LocalToWorld(new Vector2(90f, 130f));
            foreach (var cand in new[] { new Vector2(90f, 130f), new Vector2(570f, 130f), new Vector2(90f, 420f), new Vector2(570f, 420f) })
            {
                if (vf.HitTest(cand) != PortraitView.Part.Background) continue;
                grab = vf.LocalToWorld(cand);
                break;
            }
            yield return Drag(grab, grab + delta, 0.55f);
            if (Mathf.Abs(ps.Rig.PanX - wantX) > 0.05f && Mathf.Abs(delta.x) > 8f)
            {
                Note("FALLBACK pan");
                ps.Rig.PanX = wantX;
                ps.Rig.PanY = wantY;
                ps.Rig.ClampTo(ps.Camera);
            }
        }

        // ============================================================ the script

        IEnumerator Script()
        {
            yield return Until(() => G != null && G.Session != null && G.Stage != null && G.Top is TitleView, 180f, "boot");
            SetupCapture();
            Time.captureDeltaTime = 1f / 60f;
            ulong seed = PickSeed();
            GameRoot.DemoBuild = false;
            G.NewGame(seed);
            var title = G.Open(new TitleView(G));
            title.Root.style.display = DisplayStyle.None;
            DropArrivals(false);
            Note($"seed {seed}: day 1 {ShopMood.WeatherFor(seed, 1)}, day 100 {ShopMood.WeatherFor(seed, 100)}");
            yield return Sec(1.5f);

            yield return NightOpen(title);
            yield return SamirArrives();
            yield return PhotoSession();
            yield return MakeDocument();
            yield return PrintAndBind();
            yield return HandOver();
            yield return ToDay100();
            yield return NewsAndHandbook();
            yield return Upgrades();
            yield return BusyNight();
            yield return PowerCut();
            yield return StudioCamera();
            yield return OsTier3();

            SaveBuffer(UiArt.Logo(true), "logo_dark");
            SaveBuffer(UiArt.Logo(false), "logo_light");
            SaveBuffer(UiArt.Mark(), "mark");
            SaveBuffer(UiArt.Emblem(), "emblem");
            yield return Frames(2);
        }

        IEnumerator NightOpen(TitleView title)
        {
            _cursorOn = false;
            Rec("night");
            yield return Sec(10f);
            StopRec();
            title.Root.style.display = DisplayStyle.Flex;
            yield return Sec(0.5f);
            Rec("title");
            yield return Sec(4f);
            StopRec();
            title.Close();
        }

        IEnumerator SamirArrives()
        {
            yield return Until(() => G.Top is DayStartView, 5f, "day 1 card");
            yield return Sec(0.6f);
            Rec("day1card");
            yield return Sec(3f);
            StopRec();
            G.Top.Close();
            yield return Sec(0.2f);
            S.DebugAdvanceMinutes(9f * 60f + 8.6f - S.Clock.Minute);
            yield return Sec(0.3f);
            _pos = new Vector2(1180f, 1010f);
            _cursorOn = true;
            Rec("samir");
            yield return Sec(0.6f);
            yield return Click(new Vector2(762f, 276f), () => S.Phase == DayPhase.Open, () => S.OpenShop(), "sign");
            yield return MoveTo(new Vector2(1260f, 900f), 0.6f);
            yield return Until(() => Samir != null, 30f, "samir arrives");
            yield return Until(() => Samir.Phase == VisitPhase.Choosing && Samir.Choices.Count > 0, 90f, "samir offer");
            yield return Sec(1.3f);
            if (Samir.Choices.Exists(c => c.Id == GameSession.ChoiceIds.AskSmall && c.Enabled))
            {
                yield return Choose(Samir, GameSession.ChoiceIds.AskSmall);
                yield return Until(() => Samir == null || (Samir.Phase == VisitPhase.Choosing && Samir.Choices.Count > 0), 90f, "samir answers");
                if (Samir == null) Fail("Samir walked out after the counter-offer");
                yield return Sec(1.1f);
            }
            if (Samir.Choices.Exists(c => c.Id == GameSession.ChoiceIds.Accept)) yield return Choose(Samir, GameSession.ChoiceIds.Accept);
            yield return Until(() => Samir.Phase == VisitPhase.Choosing && Samir.Choices.Exists(c => c.Id == GameSession.ChoiceIds.TakePhoto), 90f, "photo choice");
            yield return Sec(1f);
            yield return Choose(Samir, GameSession.ChoiceIds.TakePhoto);
            yield return Until(() => Samir.Phase == VisitPhase.PhotoSitting && !Samir.Moving, 60f, "samir seated");
            yield return Sec(0.7f);
            StopRec();
        }

        IEnumerator Choose(Visit v, string id)
        {
            var choice = v.Choices.Find(c => c.Id == id);
            if (choice == null)
            {
                Note("missing choice " + id);
                yield break;
            }
            string text = choice.Text;
            int vid = v.Id;
            Button b = null;
            for (int i = 0; i < 30 && (b = FindBtn(text)) == null; i++) yield return null;
            if (b == null)
            {
                Note("FALLBACK (no choice button) " + id);
                S.Choose(vid, id);
                yield break;
            }
            yield return Click(Center(b), () => !(v.Phase == VisitPhase.Choosing && v.Choices.Exists(c => c.Id == id && c.Text == text)), () => S.Choose(vid, id), "choice " + id);
        }

        IEnumerator PhotoSession()
        {
            Rec("photo");
            yield return Click(new Vector2(1530f, 560f), () => G.Top is CameraView, () =>
            {
                if (S.BeginPhotoSession(Samir.Id) != null) G.Open(new CameraView(G, Samir));
            }, "tripod");
            yield return Until(() => G.Top is CameraView, 5f, "camera view");
            var ps = S.ActivePhoto;
            var sub = ps.Subject;
            var spec = S.Country(Samir.Order.CountryId).Spec;
            yield return Sec(0.8f);

            if (ps.Rig.Backdrop != spec.Photo.Backdrop)
            {
                var want = spec.Photo.Backdrop;
                yield return ClickBtn(Labels.Of(want), () => ps.Rig.SwappingBackdrop || ps.Rig.Backdrop == want, () => ps.PullBackdrop(want));
                yield return Until(() => !ps.Rig.SwappingBackdrop, 12f, "backdrop swap", false);
                yield return Sec(0.3f);
            }

            // Grab the head and turn it around: the fun part.
            var p0 = sub.Target;
            yield return HeadWiggle(new[]
            {
                new Vector2Int(Mathf.Clamp(p0.Yaw + 2, -2, 2), p0.Pitch),
                new Vector2Int(-1, 0),
                new Vector2Int(1, 1),
                new Vector2Int(0, -1),
                new Vector2Int(0, 0),
            }, 0.38f);
            yield return Sec(0.3f);

            if (sub.HasGlasses && !spec.Photo.GlassesAllowed)
            {
                yield return Click(GlassesScreen(), () => !sub.HasGlasses, () => sub.Command(PhotoCommand.RemoveGlasses), "glasses");
                yield return Sec(0.6f);
            }
            if (sub.HatOn && sub.Customer.Look.Hat != HatStyle.None && !spec.Photo.HeadwearAllowed)
            {
                yield return ClickBtn("HAT OFF");
                yield return Sec(0.5f);
            }

            yield return FrameFace(2f);
            yield return Sec(0.2f);
            yield return ClickBtn("LOOK AT THE CAMERA");
            yield return ClickBtn("HOLD STILL");
            yield return GetReady();

            // First shot: he blinks.
            yield return Shutter(true);
            var bad = ps.Shots[ps.Shots.Count - 1];
            Note($"bad take: lid {bad.Lid:0.00} closed {bad.EyesClosed}");
            yield return Sec(1.7f);
            yield return ClickBtn("RETAKE", () => G.Top is CameraView, () => G.Top?.Close());
            yield return Sec(0.3f);
            yield return GetReady();
            yield return Shutter(false);
            var good = ps.Shots[ps.Shots.Count - 1];
            Note($"good take: lid {good.Lid:0.00} blur {good.Blurred} {good.Expression} {good.Pose.Name}");
            yield return Sec(1.2f);

            yield return ClickBtn("CROP", () => G.Top is CropView, () =>
            {
                G.Top?.Close();
                G.Open(new CropView(G, good));
            });
            yield return Sec(0.5f);
            yield return CropTo(good);
            yield return Sec(0.4f);
            yield return ClickBtn("DONE", () => G.Top is ReviewView, () =>
            {
                SetPhotoCrop(good);
                G.Top?.Close();
                G.Open(new ReviewView(G, good));
            });
            yield return Sec(0.8f);
            yield return ClickBtn("KEEP", () => G.Top is CameraView, () =>
            {
                S.KeepPhoto(good);
                G.Top?.Close();
            });
            yield return Sec(0.8f);
            yield return ClickBtn("DONE - THANK YOU", () => !(G.Top is CameraView), () =>
            {
                S.FinishPhotoSitting(Samir.Id);
                if (G.Top is CameraView) G.Top.Close();
            });
            yield return Until(() => Samir.Phase == VisitPhase.Waiting && !Samir.Moving, 30f, "samir on the bench");
            yield return Sec(0.6f);
            StopRec();
        }

        static Box TargetCrop(PhotoCapture photo)
        {
            var head = photo.HeadInFrame;
            float cropH = head.H / 0.65f;
            return PortraitGeometry.MakeCrop(head.CenterX, head.Y - 0.1f * cropH + cropH * 0.5f, cropH);
        }

        void SetPhotoCrop(PhotoCapture photo)
        {
            if (G.Top is CropView cv) photo.Crop = (Box)Field(cv, "_crop");
            else photo.Crop = TargetCrop(photo);
        }

        IEnumerator CropTo(PhotoCapture photo)
        {
            // CROP measures in its 600 x 800 image (inside the area's border).
            var area = G.Top?.Root.Q(className: "le-crop__image");
            if (area == null) yield break;
            var target = TargetCrop(photo);
            var c = (Box)Field(G.Top, "_crop");
            const float cw = 600f, ch = 800f;
            // Resize from the bottom-right corner, straight down/up: the frame zooms about the guide
            // point (dashed TOP OF HEAD line x NOSE line), so the corner travels 88% of the change in
            // height. Grab just inside the corner.
            var corner = area.LocalToWorld(new Vector2(c.Right * cw - 10f, c.Bottom * ch - 10f));
            var cornerTo = area.LocalToWorld(new Vector2(c.Right * cw - 10f, c.Bottom * ch - 10f + (target.H - c.H) * ch * (1f - Framing.GuideTop)));
            yield return Drag(corner, cornerTo, 0.6f);
            yield return Sec(0.2f);
            // Then slide it over the face.
            c = (Box)Field(G.Top, "_crop");
            var mid = area.LocalToWorld(new Vector2(c.CenterX * cw, c.CenterY * ch));
            var midTo = area.LocalToWorld(new Vector2(target.CenterX * cw, target.CenterY * ch));
            yield return Drag(mid, midTo, 0.55f);
            c = (Box)Field(G.Top, "_crop");
            if (Mathf.Abs(c.H - target.H) > 0.03f || Mathf.Abs(c.CenterX - target.CenterX) > 0.03f)
            {
                Note($"FALLBACK crop ({c.H:0.00} vs {target.H:0.00})");
                SetField(G.Top, "_crop", target);
                G.Top.GetType().GetMethod("Apply", AnyField)?.Invoke(G.Top, null);
            }
        }

        IEnumerator MakeDocument()
        {
            Rec("editor");
            yield return Click(new Vector2(230f, 900f), () => G.Find<ComputerView>() != null, () => G.OpenComputer(), "crt");
            yield return Sec(0.7f);
            var order = Samir.Order;
            var pc = G.Find<ComputerView>();
            var nav = FindNav("OFFICIAL-ISH 98");
            yield return ClickEl(nav, () => G.Stage.Q(className: "le-os-form") != null, () => pc.OpenEditor(order.Id), "editor icon");
            yield return Sec(0.5f);
            if (G.Stage.Q(className: "le-os-form") == null) pc.OpenEditor(order.Id);
            var spec = S.Country(order.CountryId).Spec;
            var draft = order.Draft;

            yield return ClickBtn(spec.Name.ToUpperInvariant(), () => draft.CountryId == order.CountryId, () =>
            {
                draft.CountryId = order.CountryId;
                pc.OpenEditor(order.Id);
            });
            yield return Frames(3);
            yield return RowClick("CATEGORY", Labels.Of(spec.Passport.Category), () => draft.Category == spec.Passport.Category, () => draft.Category = spec.Passport.Category);
            yield return RowClick("COVER", Labels.Of(spec.Passport.Cover), () => draft.Cover == spec.Passport.Cover, () => draft.Cover = spec.Passport.Cover);
            yield return RowClick("PAGES", spec.Passport.Pages.ToString(CultureInfo.InvariantCulture), () => draft.Pages == spec.Passport.Pages, () => draft.Pages = spec.Passport.Pages);

            var who = Samir.Customer.Identity;
            foreach (var f in spec.Passport.PersonalFields)
            {
                string value = DocumentEvaluator.Expected(f, who, spec.Passport.DateFormat);
                if (f == DocField.Sex || f == DocField.EyeColor)
                {
                    var ff = f;
                    yield return RowClick(Labels.Of(f), value, () => draft.Get(ff) == value, () => draft.Set(ff, value));
                    continue;
                }
                yield return TypeField(f, value, draft);
            }
            var issue = Calendar.DateOf(S.Day);
            yield return TypeField(DocField.DateOfIssue, Labels.FormatDate(issue, spec.Passport.DateFormat), draft);
            yield return TypeField(DocField.DateOfExpiry, Labels.FormatDate(issue.AddYears(spec.Passport.ValidityYears), spec.Passport.DateFormat), draft);
            var gen = FindBtn("GENERATE");
            if (gen != null)
            {
                gen.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(gen);
                yield return Frames(2);
            }
            yield return ClickBtn("GENERATE", () => !string.IsNullOrEmpty(draft.Get(DocField.DocumentNumber)), () => S.GenerateDocumentNumber(order.Id));
            yield return Frames(3);

            var photo = G.Stage.Q(className: "le-os-photo");
            if (photo != null)
            {
                photo.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(photo);
                yield return Frames(2);
            }
            var kept = S.Photos.ForCustomer(Samir.Customer.Id);
            yield return ClickEl(photo, () => draft.PhotoId.HasValue, () =>
            {
                if (kept.Count > 0) draft.PhotoId = kept[kept.Count - 1].Id;
                pc.OpenEditor(order.Id);
            }, "photo");
            yield return Sec(0.8f);
            var print = FindBtnStarting("PRINT");
            yield return ClickEl(print, () => S.Printer.Busy || S.Printer.Tray.Count > 0, () => S.PrintDraft(order.Id), "print");
            yield return Sec(0.9f);
            StopRec();
        }

        IEnumerator RowClick(string row, string text, Func<bool> done, Action set)
        {
            var pc = G.Find<ComputerView>();
            Button b = null;
            for (int i = 0; i < 20 && (b = FindInRow(row, text)) == null; i++) yield return null;
            if (b == null)
            {
                Note($"FALLBACK (no row button) {row}/{text}");
                set();
                pc?.OpenEditor(Samir.Order.Id);
                yield break;
            }
            b.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(b);
            yield return Frames(2);
            b = FindInRow(row, text) ?? b;
            yield return Click(Center(b), done, () =>
            {
                set();
                pc?.OpenEditor(Samir.Order.Id);
            }, $"{row}/{text}");
            yield return Frames(3);
        }

        IEnumerator TypeField(DocField f, string value, DocumentDraft draft)
        {
            var tf = FindField(f);
            if (tf == null)
            {
                Note("FALLBACK (no field) " + f);
                draft.Set(f, value);
                yield break;
            }
            tf.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(tf);
            yield return Frames(2);
            yield return TypeInto(tf, value);
            if (draft.Get(f) != value)
            {
                Note("FALLBACK typing " + f);
                draft.Set(f, value);
            }
        }

        IEnumerator PrintAndBind()
        {
            _toasts = true;
            Rec("print");
            var close = G.Stage.Q<Button>(className: "le-os-close");
            yield return ClickEl(close, () => G.Find<ComputerView>() == null, () => G.Find<ComputerView>()?.Close(), "close computer");
            yield return MoveTo(new Vector2(1540f, 720f), 0.5f);
            yield return Until(() => S.Printer.Printing != null || S.Printer.Tray.Count > 0, 15f, "printing");
            if (S.Printer.Printing != null && !S.Printer.Jammed)
            {
                SetField(S.Printer, "_jamAt", Mathf.Max(S.Printer.Progress + 0.1f, 0.45f));
                yield return Until(() => S.Printer.Jammed, 60f, "jam");
                yield return Sec(1.2f);
                // You hit it. Not too hard.
                yield return Click(new Vector2(1500f, 860f), () => !S.Printer.Jammed, () => S.ClearPrinterJam(), "whack the printer");
                if (S.Printer.Printing != null) S.Printer.Printing.Smudged = false;
            }
            yield return Until(() => S.Printer.Tray.Count > 0, 90f, "page in the tray");
            yield return Sec(0.7f);
            var page = G.Stage.Q(className: "le-tray-page");
            yield return ClickEl(page, () => G.Top is PrintMenu, () => G.Open(new PrintMenu(G, S.Printer.Tray[0].Id)), "tray page");
            yield return Sec(1f);
            yield return ClickBtn("BIND INTO BOOKLET", () => S.DeskDocuments.Count > 0, () =>
            {
                var pg = S.Printer.Tray[0];
                G.Top?.Close();
                S.BindPage(pg.Id);
            });
            yield return Sec(0.9f);
            var booklet = G.Stage.Q(className: "le-booklet");
            yield return ClickEl(booklet, () => G.Top is PassportView, () => G.Open(new PassportView(G, S.DeskDocuments[0])), "booklet");
            yield return Sec(2.6f);
            yield return Click(new Vector2(1760f, 960f), () => G.Top == null, () => G.Top?.Close(), "close passport");
            yield return Sec(0.3f);
            StopRec();
            _toasts = false;
        }

        IEnumerator HandOver()
        {
            Rec("handover");
            var booklet = G.Stage.Q(className: "le-booklet");
            var fig = FigureOf(Samir);
            var to = fig != null ? fig.HeadStage + new Vector2(0f, fig.HeadSizeStage * 1.2f) : new Vector2(300f, 520f);
            if (booklet != null) yield return Drag(Center(booklet), to, 0.8f, 0.2f);
            yield return Frames(8);
            if (S.DeskDocuments.Count > 0)
            {
                Note("FALLBACK hand over");
                S.GiveDocument(Samir.Id, S.DeskDocuments[0].Id);
            }
            yield return MoveTo(new Vector2(1300f, 880f), 0.6f);
            yield return Until(() => S.CounterCash.Count > 0, 120f, "cash on the counter");
            yield return Sec(1f);
            var cash = G.Stage.Q(className: "le-cash");
            int pile = S.CounterCash[0].Id;
            yield return ClickEl(cash, () => S.CounterCash.Count == 0, () => S.CollectCash(pile), "cash");
            yield return MoveTo(new Vector2(1400f, 940f), 0.5f);
            yield return Until(() => S.Visits.Count == 0, 60f, "samir leaves", false);
            yield return Sec(0.8f);
            StopRec();
        }

        IEnumerator ToDay100()
        {
            _cursorOn = false;
            yield return Until(() => S.Visits.Count == 0, 60f, "shop empty");
            S.DebugAdvanceMinutes(Mathf.Max(0f, Balance.CloseMinute + 1f - S.Clock.Minute));
            yield return Until(() => S.Phase == DayPhase.AfterHours, 5f, "after hours");
            yield return Frames(2);
            if (!S.CloseShop()) Fail("CloseShop failed: " + (S.CanCloseShop(out var reason) ? "?" : reason));
            yield return Until(() => G.Top is DayReportView, 10f, "day report");
            yield return Sec(0.5f);
            Rec("report");
            yield return Sec(3f);
            StopRec();
            G.Top.Close();
            yield return Frames(2);
            S.Clock.Day = 99;
            S.StartNextDay();
            DropArrivals(true);
            yield return Until(() => G.Top is DayStartView, 10f, "day 100 card");
            yield return Sec(0.5f);
            Rec("day100card");
            yield return Sec(3f);
            StopRec();
            G.Top.Close();
            yield return Sec(0.3f);
        }

        IEnumerator NewsAndHandbook()
        {
            S.InjectNews(new NewsItem
            {
                Effect = NewsEffect.RuleChange, CountryId = Countries.France,
                Headline = "FRANCE INTRODUCES NEW TRAVEL REGULATIONS",
                Body = "Updated document requirements take effect immediately. Handbook update available in the SHOP.",
            });
            S.InjectNews(new NewsItem
            {
                Effect = NewsEffect.RiskUp, CountryId = Countries.France, Days = 3, Magnitude = 0.12f,
                Headline = "FRANCE TIGHTENS AIRPORT SECURITY",
                Body = "Longer lines, sharper eyes.",
            });
            yield return Sec(0.4f);
            _pos = new Vector2(900f, 1000f);
            _cursorOn = true;
            Rec("news");
            yield return Click(new Vector2(230f, 900f), () => G.Find<ComputerView>() != null, () => G.OpenComputer(), "crt");
            yield return Sec(0.6f);
            var pc = G.Find<ComputerView>();
            yield return ClickEl(FindNav("NEWS"), null, () => pc.OpenApp("news"), "news icon");
            yield return Sec(2.4f);
            yield return ClickEl(FindNav("SHOP"), null, () => pc.OpenApp("shop"), "shop icon");
            yield return Sec(1.8f);
            var close = G.Stage.Q<Button>(className: "le-os-close");
            yield return ClickEl(close, () => G.Find<ComputerView>() == null, () => G.Find<ComputerView>()?.Close(), "close computer");
            yield return Click(new Vector2(572f, 934f), () => G.Find<HandbookView>() != null, () => G.ToggleHandbook(), "binder");
            yield return Sec(0.8f);
            VisualElement tab = null;
            G.Stage.Query<Label>(className: "le-tab__label").ForEach(l =>
            {
                if (tab == null && string.Equals(l.text, S.Country(Countries.France).Spec.Name, StringComparison.OrdinalIgnoreCase)) tab = l.GetFirstAncestorOfType<VisualElement>();
            });
            while (tab != null && !tab.ClassListContains("le-tab")) tab = tab.parent;
            yield return ClickEl(tab, null, () => { G.ToggleHandbook(); G.ToggleHandbook(Countries.France); }, "france tab");
            yield return Sec(2.2f);
            StopRec();
            while (G.Top != null) G.Top.Close();
            _cursorOn = false;
        }

        IEnumerator Upgrades()
        {
            yield return Sec(0.5f);
            Rec("tier1");
            yield return Sec(3.5f);
            StopRec();
            S.Ledger.Record(S.Now, LedgerKind.Other, 80000, "trailer: savings");
            foreach (var id in new[] { "cam2", "prn2", "pc2", "stn2" })
                if (!S.BuyEquipment(id)) Note("buy failed: " + id);
            yield return Sec(1f);
            Rec("tier2");
            yield return Sec(3.5f);
            StopRec();
            foreach (var id in new[] { "cam3", "prn3", "pc3", "stn3" })
                if (!S.BuyEquipment(id)) Note("buy failed: " + id);
            yield return Sec(1f);
            Rec("tier3");
            yield return Sec(3.5f);
            StopRec();
            S.OpenShop();
            S.DebugAdvanceMinutes(16.6f * 60f - S.Clock.Minute);
            yield return Sec(1f);
            Rec("dusk");
            yield return Sec(3.5f);
            StopRec();
        }

        IEnumerator BusyNight()
        {
            S.DebugAdvanceMinutes(Mathf.Max(0f, Balance.CloseMinute + 2f - S.Clock.Minute));
            yield return Until(() => S.Visits.Count == 0, 60f, "evening empty", false);
            S.DebugAdvanceMinutes(Mathf.Max(0f, 19.2f * 60f - S.Clock.Minute));
            DropArrivals(true);
            yield return Sec(1f);
            _cursorOn = false;
            Rec("night_busy");
            yield return Sec(1.5f);
            for (int i = 0; i < 4; i++)
            {
                S.DebugSpawnWalkIn();
                yield return Sec(2.8f);
            }
            yield return Until(() => S.Visits.Count > 0 && !S.Visits.Exists(v => v.Moving), 40f, "queue settled", false);
            yield return Sec(5f);
            StopRec();
        }

        IEnumerator PowerCut()
        {
            Rec("power");
            yield return Sec(2f);
            S.InjectNews(new NewsItem { Effect = NewsEffect.PowerOutage, Magnitude = S.Clock.Minute + 0.05f, Days = 6 });
            yield return Until(() => !S.PowerOn, 5f, "power off");
            yield return Until(() => S.PowerOn, 30f, "power back");
            yield return Sec(3f);
            StopRec();
        }

        IEnumerator StudioCamera()
        {
            Visit v = null;
            yield return Until(() => (v = S.Visits.Find(x => x.Phase == VisitPhase.Choosing && x.Choices.Exists(c => c.Id == GameSession.ChoiceIds.Accept))) != null, 60f, "a night customer to serve", false);
            if (v == null) yield break;
            S.Choose(v.Id, GameSession.ChoiceIds.Accept);
            yield return Until(() => v.Phase == VisitPhase.Gone || (v.Phase == VisitPhase.Choosing && v.Choices.Exists(c => c.Id == GameSession.ChoiceIds.TakePhoto && c.Enabled)), 60f, "night photo choice", false);
            if (v.Phase != VisitPhase.Choosing) yield break;
            S.Choose(v.Id, GameSession.ChoiceIds.TakePhoto);
            yield return Until(() => v.Phase == VisitPhase.PhotoSitting && !v.Moving, 60f, "night customer seated", false);
            if (v.Phase != VisitPhase.PhotoSitting) yield break;
            if (S.BeginPhotoSession(v.Id) == null) yield break;
            G.Open(new CameraView(G, v));
            yield return Sec(0.6f);
            _pos = new Vector2(1500f, 980f);
            _cursorOn = true;
            Rec("camera_t3");
            var ps = S.ActivePhoto;
            var spec = v.Order != null ? S.Country(v.Order.CountryId)?.Spec : null;
            yield return Sec(0.4f);
            if (spec != null && ps.Rig.Backdrop != spec.Photo.Backdrop)
            {
                var want = spec.Photo.Backdrop;
                yield return ClickBtn(Labels.Of(want), () => ps.Rig.SwappingBackdrop || ps.Rig.Backdrop == want, () => ps.PullBackdrop(want));
                yield return Until(() => !ps.Rig.SwappingBackdrop, 12f, "backdrop", false);
            }
            var p0 = ps.Subject.Target;
            yield return HeadWiggle(new[] { new Vector2Int(Mathf.Clamp(p0.Yaw - 1, -2, 2), 0), new Vector2Int(1, 0), new Vector2Int(0, 0) }, 0.32f);
            yield return FrameFace(1.75f);
            yield return ClickBtn("LOOK AT THE CAMERA");
            yield return GetReady();
            yield return Shutter(false);
            yield return Sec(1.6f);
            StopRec();
            while (G.Top != null) G.Top.Close();
            _cursorOn = false;
        }

        IEnumerator OsTier3()
        {
            if (!S.PowerOn) yield return Until(() => S.PowerOn, 30f, "power for the computer", false);
            G.OpenComputer();
            var pc = G.Find<ComputerView>();
            if (pc == null) yield break;
            pc.OpenApp("orders");
            yield return Sec(0.6f);
            _pos = new Vector2(1100f, 700f);
            _cursorOn = true;
            Rec("os_t3");
            yield return Sec(0.8f);
            yield return ClickEl(FindNav("SHOP"), null, () => pc.OpenApp("shop"), "shop icon");
            yield return Sec(1.6f);
            yield return ClickEl(FindNav("CLIENTS"), null, () => pc.OpenApp("clients"), "clients icon");
            yield return Sec(1.4f);
            StopRec();
            _cursorOn = false;
            pc.Close();
        }
    }
}
#endif
