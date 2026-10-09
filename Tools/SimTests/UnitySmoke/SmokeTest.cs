#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using LegitEnough.Game;
using LegitEnough.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnoughCI
{
    /// <summary>
    /// Batch-mode play-through used for verification on a throwaway copy of the project.
    /// Enters play mode, drives Day 1 through public APIs, renders the UI into a
    /// RenderTexture and saves PNGs, then exits with 0 (clean) or 1 (errors logged).
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
        }

        static readonly List<Step> Steps = new List<Step>();
        static readonly List<string> Errors = new List<string>();
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
        static double _nudgeAt;
        static double _jamSeenAt = -1;

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

        static void Note(string text) => File.AppendAllText(Path.Combine(_out, "log.txt"), "## " + text + "\n");

        static void Drive()
        {
            try
            {
                DriveInner();
            }
            catch (Exception e)
            {
                Errors.Add("DRIVER EXCEPTION in step " + (_index < Steps.Count ? Steps[_index].Name : "?") + ": " + e);
                Finish();
            }
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
                    _phase = 1;
                    _phaseStart = now;
                    return;
                case 1:
                    if (step.Until != null && !step.Until())
                    {
                        if (now - _phaseStart > step.Timeout)
                        {
                            Errors.Add($"TIMEOUT waiting in step '{step.Name}' ({S?.Phase} {S?.Now})");
                            Finish();
                        }
                        return;
                    }
                    _phase = 2;
                    return;
                case 2:
                    Note($"{step.Name}  (sim {S?.Now}, phase {S?.Phase})");
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
            File.WriteAllBytes(Path.Combine(_out, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static void Finish()
        {
            EditorApplication.update -= Drive;
            File.AppendAllText(Path.Combine(_out, "log.txt"), $"\nDONE: {_index}/{Steps.Count} steps, {Errors.Count} errors\n");
            File.WriteAllText(Path.Combine(_out, "errors.txt"), string.Join("\n-----\n", Errors));
            EditorApplication.Exit(Errors.Count == 0 && _index >= Steps.Count ? 0 : 1);
        }

        static void Add(string name, Action action, string shot = null, Func<bool> until = null, float delay = 0.35f, float timeout = 40f) =>
            Steps.Add(new Step { Name = name, Do = action, Shot = shot, Until = until, Delay = delay, Timeout = timeout });

        static Visit Samir => S.Visits.Find(v => v.Customer.Id == StoryCast.SamirId);

        static void Goto(string name) => _jump = Steps.FindIndex(s => s.Name == name);

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

        /// <summary>Cheap printers jam at random: grab a picture of the jam, then clear it like a player would.</summary>
        static bool WaitForPrint()
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
                Shot("15b_printer_jam");
                S.ClearPrinterJam();
                _jamSeenAt = -1;
            }
            return S.Printer.Tray.Count > 0;
        }

        static bool BadTake(PhotoCapture c) =>
            c.EyesClosed || c.EyesHalfClosed || c.Blurred || c.Expression != Expression.Neutral ||
            Math.Abs(c.GazeX) > 0.3f || Math.Abs(c.GazeY) > 0.3f || c.Pose.Yaw != 0 || c.Pose.Pitch != 0;

        static void BuildScript()
        {
            Add("boot", () =>
            {
                var doc = G.GetComponentInChildren<UIDocument>();
                _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _rt.Create();
                doc.panelSettings.targetTexture = _rt;
            }, null, () => G != null && G.Session != null && G.GetComponentInChildren<UIDocument>() != null, 1f);
            Add("title", null, "00_title", () => G.Top is TitleView, 1.2f);
            Add("close title", () => G.Top?.Close(), null);
            Add("day start card", null, "01_day_start", null, 1.2f);
            Add("close card", () => G.Top?.Close(), "02_shop_morning");
            Add("open shop", () =>
            {
                S.OpenShop();
                S.DebugAdvanceMinutes(9 * 60 + 8 - S.Clock.Minute);
            }, null);
            Add("samir talking", null, "03_samir_walks_in", () => Samir != null && Samir.Phase == VisitPhase.Talking && Samir.Line != null, 0.2f);
            Add("samir offer", null, "04_negotiation", () => Samir != null && Samir.Phase == VisitPhase.Choosing);
            Add("accept", () => S.Choose(Samir.Id, GameSession.ChoiceIds.Accept), null);
            Add("photo choice", null, "05_after_accept", () => Samir.Phase == VisitPhase.Choosing && Samir.Choices.Exists(c => c.Id == GameSession.ChoiceIds.TakePhoto));
            Add("to stool", () => S.Choose(Samir.Id, GameSession.ChoiceIds.TakePhoto), null);
            Add("seated", null, "06_on_stool", () => Samir.Phase == VisitPhase.PhotoSitting && !Samir.Moving, 0.5f);
            Add("open camera", () =>
            {
                S.BeginPhotoSession(Samir.Id);
                G.Open(new CameraView(G, Samir));
            }, "07_camera_raw");
            Add("pose", () =>
            {
                var ps = S.ActivePhoto;
                var sub = ps.Subject;
                ps.PullBackdrop(Backdrop.LightGray);
                while (sub.HasGlasses) sub.Command(PhotoCommand.RemoveGlasses);
                ps.Rig.Zoom = 2f;
                var head = PortraitGeometry.HeadWorld(sub.Customer.Look, sub.Pose, sub.SeatOffset, sub.Slouch, sub.HatOn);
                ps.Rig.PanX = head.CenterX;
                ps.Rig.PanY = head.CenterY + 0.25f;
                sub.Command(PhotoCommand.HoldStill);
                sub.BeginDrag();
                sub.DragTo(0, 0);
            }, null);
            Add("posed", () =>
            {
                var sub = S.ActivePhoto.Subject;
                sub.EndDrag();
                if (sub.Expression != Expression.Neutral) sub.Command(PhotoCommand.NeutralFace);
                sub.Command(PhotoCommand.LookAtCamera);
            }, "08_camera_posed", null, 2.2f);
            Add("shutter", () => (G.Top as CameraView)?.Shutter(), null, ReadyForShutter, 0.6f);
            Add("review", () =>
            {
                _capture = S.ActivePhoto.Shots[S.ActivePhoto.Shots.Count - 1];
                if (BadTake(_capture) && _retakes++ < 6)
                {
                    Note($"retake: lid {_capture.Lid:0.00} blur {_capture.Blurred} {_capture.Expression} {_capture.Pose.Name} gaze {_capture.GazeX:0.0},{_capture.GazeY:0.0}");
                    G.Top.Close();
                    Goto("shutter");
                }
            }, "09_review", () => G.Top is ReviewView);
            Add("crop", () =>
            {
                G.Top.Close();
                var head = _capture.HeadInFrame;
                float cropH = head.H / 0.65f;
                _capture.Crop = PortraitGeometry.MakeCrop(head.CenterX, head.Y - 0.1f * cropH + cropH * 0.5f, cropH);
                G.Open(new CropView(G, _capture));
            }, "10_crop");
            Add("keep", () =>
            {
                G.Top.Close();
                S.KeepPhoto(_capture);
                Note($"photo: {PhotoEvaluator.Score(_capture, S.Country(Countries.France).Spec.Photo)} coverage {_capture.Coverage:0.00}");
            }, "11_camera_kept");
            Add("done photos", () =>
            {
                if (!S.FinishPhotoSitting(Samir.Id)) Errors.Add("FinishPhotoSitting failed");
                if (G.Top is CameraView) G.Top.Close();
            }, null);
            Add("bench", null, "12_bench_waiting", () => Samir.Phase == VisitPhase.Waiting, 0.5f);
            Add("computer", () => G.OpenComputer(), "13_computer_messages");
            Add("editor", () =>
            {
                var o = Samir.Order;
                var spec = S.Country(o.CountryId).Spec;
                var d = o.Draft;
                d.CountryId = o.CountryId;
                d.Category = spec.Passport.Category;
                d.Cover = spec.Passport.Cover;
                d.Pages = spec.Passport.Pages;
                d.PhotoId = _capture.Id;
                foreach (var f in spec.Passport.PersonalFields) d.Set(f, DocumentEvaluator.Expected(f, Samir.Customer.Identity, spec.Passport.DateFormat));
                var issue = Calendar.DateOf(S.Day);
                d.Set(DocField.DateOfIssue, Labels.FormatDate(issue, spec.Passport.DateFormat));
                d.Set(DocField.DateOfExpiry, Labels.FormatDate(issue.AddYears(spec.Passport.ValidityYears), spec.Passport.DateFormat));
                S.GenerateDocumentNumber(o.Id);
                G.Find<ComputerView>()?.OpenEditor(o.Id);
            }, "14_editor");
            Add("print", () => S.PrintDraft(Samir.Order.Id), "15_printing");
            Add("printed", () => G.Find<ComputerView>()?.Close(), "16_tray", WaitForPrint, 0.3f, 60f);
            Add("print menu", () => G.Open(new PrintMenu(G, S.Printer.Tray[0].Id)), "17_print_menu");
            Add("bind", () =>
            {
                var page = S.Printer.Tray[0];
                if (page.Smudged) Shot("17b_print_menu_smudged");
                G.Top.Close();
                if (page.Smudged && _reprints++ < 3)
                {
                    Note("smudged page - shredding and reprinting");
                    S.ShredPage(page.Id);
                    S.PrintDraft(Samir.Order.Id);
                    Goto("printed");
                    return;
                }
                S.BindPage(page.Id);
            }, "18_booklet_on_desk");
            Add("inspect", () => G.Open(new PassportView(G, S.DeskDocuments[0])), "19_passport");
            Add("handbook", () =>
            {
                G.Top.Close();
                G.ToggleHandbook(Countries.France);
            }, "20_handbook_france");
            Add("handbook basics", () =>
            {
                G.Top.Close();
                G.ToggleHandbook("BASICS");
            }, "21_handbook_basics");
            Add("slip", () =>
            {
                G.Top.Close();
                G.Open(new SlipView(G, Samir.Order.Id));
            }, "22_client_sheet");
            Add("hand over", () =>
            {
                G.Top.Close();
                if (!S.GiveDocument(Samir.Id, S.DeskDocuments[0].Id)) Errors.Add("GiveDocument failed");
            }, null);
            Add("inspecting", null, "23_inspecting", () => Samir.Phase == VisitPhase.Inspecting, 0.5f);
            Add("paid", () =>
            {
                Note("quality: " + Samir.Order.Quality + " :: " + string.Join("; ", Samir.Order.Quality.Findings));
            }, "24_cash_on_counter", () => S.CounterCash.Count > 0);
            Add("collect", () => S.CollectCash(S.CounterCash[0].Id), null);
            Add("gone", null, "25_empty_shop", () => S.Visits.Count == 0, 0.5f, 60f);
            Add("walk-in", () => S.DebugSpawnWalkIn(), null);
            Add("walk-in talks", null, "26_walk_in", () => S.Visits.Count > 0 && S.Visits[0].Phase == VisitPhase.Choosing, 0.5f, 60f);
            Add("decline", () => S.Choose(S.Visits[0].Id, GameSession.ChoiceIds.Decline), null);
            Add("walk-in gone", () => { }, null, () => S.Visits.Count == 0, 0.5f, 60f);
            Add("orders app", () =>
            {
                G.OpenComputer();
                G.Find<ComputerView>()?.OpenApp("orders");
            }, "27a_orders");
            Add("clients app", () => G.Find<ComputerView>()?.OpenApp("clients"), "27b_clients");
            Add("news app", () => G.Find<ComputerView>()?.OpenApp("news"), "27c_news");
            Add("shop app", () => G.Find<ComputerView>()?.OpenApp("shop"), "27d_shop");
            Add("evening", () =>
            {
                G.Find<ComputerView>()?.Close();
                S.DebugAdvanceMinutes(Balance.CloseMinute + 1 - S.Clock.Minute);
            }, null);
            Add("close shop", () =>
            {
                if (!S.CloseShop()) Errors.Add("CloseShop failed: " + (S.CanCloseShop(out var r) ? "" : r));
            }, null, () => S.Phase == DayPhase.AfterHours, 0.5f);
            Add("report", null, "27_day_report", () => G.Top is DayReportView, 0.5f);
            Add("next day", () =>
            {
                G.Top.Close();
                S.StartNextDay();
            }, "28_day2_card", null, 0.3f);
            Add("day 2 shop", () => G.Top?.Close(), "29_day2_shop", () => G.Top is DayStartView, 0.8f);
            Add("pause", () => G.Open(new PauseView(G)), "33_pause");
            Add("confirm", () =>
            {
                G.Top?.Close();
                S.OpenShop();
                G.Open(new ConfirmView(G, "Close early?", "It's not 18:00 yet. Closing now ends the day.", "CLOSE THE SHOP", () => { }));
            }, "34_confirm");
            Add("rule change", () =>
            {
                G.Top?.Close();
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
                G.ToggleHandbook(Countries.France);
            }, "36_handbook_outdated");
            Add("news app", () =>
            {
                G.Top?.Close();
                G.OpenComputer();
                G.Find<ComputerView>()?.OpenApp("news");
            }, "37_news_day2");
            Add("shop app update", () => G.Find<ComputerView>()?.OpenApp("shop"), "38_shop_update");
            Add("power cut", () =>
            {
                G.Find<ComputerView>()?.Close();
                S.InjectNews(new NewsItem { Effect = NewsEffect.PowerOutage, Magnitude = S.Clock.Minute - 1f, Days = 120 });
            }, "35_power_cut");
            Add("lineup styles", () => G.Open(new DevLineupView(G, 0)), "30_lineup_styles");
            Add("lineup generated", () => (G.Top as DevLineupView)?.Show(1), "31_lineup_generated");
            Add("lineup generated 2", () => (G.Top as DevLineupView)?.Show(2), "32_lineup_generated2");
            // Day 1 → Day 100: buy the kit and look at the shop again, then at night.
            Add("tier 2", () =>
            {
                G.Top?.Close();
                S.Ledger.Record(S.Now, LedgerKind.Other, 30000, "smoke: kit money");
                foreach (var id in new[] { "cam2", "prn2", "pc2", "stn2" })
                    if (!S.BuyEquipment(id)) Errors.Add("buy failed: " + id);
                S.DebugAdvanceMinutes(130f);
            }, "39_tier2_shop");
            Add("tier 3 night", () =>
            {
                foreach (var id in new[] { "cam3", "prn3", "pc3", "stn3" })
                    if (!S.BuyEquipment(id)) Errors.Add("buy failed: " + id);
                S.DebugAdvanceMinutes(Mathf.Max(0f, 19.5f * 60f - S.Clock.Minute));
            }, "40_day100_night", null, 0.5f);
            Add("night customer", () => S.DebugSpawnWalkIn(), null);
            Add("night customer talks", null, "41_day100_customer", () => S.Visits.Exists(v => v.Phase == VisitPhase.Choosing), 0.5f, 60f);
            Add("computer t3", () => G.OpenComputer(), "42_computer_t3");
            Add("shop products", () => G.Find<ComputerView>()?.OpenApp("shop"), "43_shop_products");
        }
    }
}
#endif
