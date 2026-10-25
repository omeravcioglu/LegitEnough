using System;
using System.Collections.Generic;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// Owns the simulation and the UI Toolkit document. Ticks the sim, routes sim events to
    /// sound/toasts, manages the overlay stack and the day flow. Everything is built from code
    /// so the prototype runs from any scene (see <see cref="GameBootstrap"/>).
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public const float StageWidth = 1920f;
        public const float StageHeight = 1080f;

        [Tooltip("0 = a new random world every time.")]
        public ulong FixedSeed;

        public static GameRoot Instance { get; private set; }

        /// <summary>
        /// The player build is the demo. The trailer and store harnesses set this to false
        /// before the first session is made, so they film the full game as before.
        /// </summary>
        public static bool DemoBuild = true;

        public GameSession Session { get; private set; }
        public VisualElement Stage { get; private set; }
        public ShopView Shop { get; private set; }
        public Toasts Toasts { get; private set; }
        /// <summary>Achievements: the book, the backend for this edition and the tracker on the session.</summary>
        public AchievementService Achievements { get; private set; }
        /// <summary>The unlock banners (above the toasts).</summary>
        public AchievementToasts AchToasts { get; private set; }
        /// <summary>R.'s Day-1 notes (demo only; it stays hidden otherwise).</summary>
        public TutorialCoach Coach { get; private set; }
        public bool ShowHints = true;

        /// <summary>True while a menu screen owns the stage: the splash, the title or the demo end.</summary>
        public bool MenuUp => Find<SplashView>() != null || Find<TitleView>() != null || Find<DemoEndView>() != null;

        UIDocument _doc;
        VisualElement _root;
        VisualElement _overlayLayer;
        VisualElement _toastLayer;
        bool _toastsHidden;
        readonly List<Overlay> _overlays = new List<Overlay>();
        bool _reportShown;
        int _startCardDay;

        public Overlay Top => _overlays.Count > 0 ? _overlays[_overlays.Count - 1] : null;

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Instance = this;
#if UNITY_EDITOR
            // Developers play the full game in the Editor through an EditorPrefs switch, never by
            // editing DemoBuild. Batch harnesses (smoke test, trailer, store shots) set it themselves.
            if (!Application.isBatchMode && UnityEditor.EditorPrefs.GetBool("LegitEnough.PlayFullGameInEditor", false)) DemoBuild = false;
#endif
            GameSettings.ApplyFrameCap();
            if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            gameObject.AddComponent<SfxPlayer>();
            // Background music follows the game state on its own (it plays nothing in batch runs).
            gameObject.AddComponent<MusicPlayer>();
            GameSettings.ApplySaved(this);
            Achievements = AchievementService.Create();
            CreateDocument();
        }

        void Start()
        {
            NewGame(FixedSeed != 0 ? FixedSeed : (ulong)DateTime.Now.Ticks);
            Open(new TitleView(this));
            // The studio ident plays over the title, then fades into it. Batch runs (smoke test,
            // store shots, trailer) go straight to the title they wait for.
            if (!Application.isBatchMode) Open(new SplashView(this));
        }

        void OnApplicationQuit()
        {
            Achievements?.Shutdown();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Alt-tab (or the store page opening) during a shift stops the clock behind PAUSED.</summary>
        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus || Application.isBatchMode || Application.isEditor) return;
            PauseShift();
        }

        /// <summary>
        /// Opens the pause menu when the shop clock is running. Menu screens and cards that already
        /// stop the clock (day cards, settings, confirms, papers) are left as they are.
        /// </summary>
        public void PauseShift()
        {
            if (Session == null || _overlayLayer == null || MenuUp) return;
            foreach (var o in _overlays)
                if (o.PausesGame) return;
            Open(new PauseView(this));
        }

        void CreateDocument()
        {
            // PanelSettings warns about a missing theme the instant it's created, before we can assign one.
            bool logs = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            Debug.unityLogger.logEnabled = logs;
            panel.name = "LegitEnough Panel";
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("LegitEnough/UI/LegitEnoughTheme");
            // The whole 1920x1080 stage is always on screen: Expand scales by min(w/1920, h/1080),
            // so the panel is never smaller than the stage in either direction. LayoutStage centres
            // the stage and the rest of the panel stays the dark root (bars at the sides on 21:9,
            // above and below on 16:10, 4:3 and the Steam Deck). Shrink cropped instead.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int((int)StageWidth, (int)StageHeight);
            panel.screenMatchMode = PanelScreenMatchMode.Expand;
            panel.clearColor = true;
            panel.colorClearValue = new Color(6f / 255f, 5f / 255f, 4f / 255f, 1f); // the bars' colour (.le-root)

            var go = new GameObject("LegitEnough UI");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            _doc = go.AddComponent<UIDocument>();
            _doc.panelSettings = panel;
            go.SetActive(true);

            _root = _doc.rootVisualElement;
            var sheet = Resources.Load<StyleSheet>("LegitEnough/UI/Main");
            if (sheet != null) _root.styleSheets.Add(sheet);
            else Debug.LogWarning("LegitEnough: Main.uss not found in Resources/LegitEnough/UI.");
            // The demo's screens, locks and tutorial notes each keep their own sheet.
            foreach (var extra in new[] { "DemoBoot", "DemoLocks", "DemoTutorial", "Achievements", "Crop" })
            {
                var more = Resources.Load<StyleSheet>("LegitEnough/UI/" + extra);
                if (more != null) _root.styleSheets.Add(more);
            }
            _root.AddToClassList("le-root");
            _root.RegisterCallback<GeometryChangedEvent>(_ => LayoutStage());
        }

        /// <summary>
        /// Centres the stage in the panel. With Expand the panel is at least 1920x1080, so the
        /// offsets are never negative: whatever is left over is letterbox/pillarbox (the root's
        /// dark background; the stage clips its own children, so nothing reaches the bars).
        /// The offset lands on a whole screen pixel so the pixel art keeps one grid everywhere.
        /// </summary>
        void LayoutStage()
        {
            if (Stage == null) return;
            var r = _root.layout;
            if (float.IsNaN(r.width) || float.IsNaN(r.height) || r.width <= 0f) return;
            var target = _doc != null && _doc.panelSettings != null ? _doc.panelSettings.targetTexture : null;
            float screenW = target != null ? target.width : Screen.width;
            float px = screenW > 0f ? screenW / r.width : 1f; // screen pixels per panel unit
            Stage.style.left = Mathf.Round(Mathf.Max(0f, r.width - StageWidth) * 0.5f * px) / px;
            Stage.style.top = Mathf.Round(Mathf.Max(0f, r.height - StageHeight) * 0.5f * px) / px;
        }

        public void NewGame(ulong seed)
        {
            foreach (var o in _overlays.ToArray()) CloseOverlay(o);
            _root.Clear();
            Session = DemoBuild ? new GameSession(seed, GameRules.Demo()) : new GameSession(seed);
            Achievements?.Attach(Session);
            Stage = Ui.El("le-stage", _root).At(0, 0, StageWidth, StageHeight);
            Shop = new ShopView(this, Stage);
            _overlayLayer = Ui.El("le-overlay-layer", Stage).At(0, 0, StageWidth, StageHeight);
            _overlayLayer.pickingMode = PickingMode.Ignore;
            // Above every screen (it points into the computer and the camera), below the toasts.
            Coach = new TutorialCoach(this);
            Stage.Add(Coach.Root);
            Toasts = new Toasts(Stage);
            AchToasts = new AchievementToasts(this, Stage);
            _toastLayer = Stage.Q(className: "le-toasts");
            _toastsHidden = false;
            _reportShown = false;
            _startCardDay = 0;
            LayoutStage();
            Debug.Log($"LEGIT ENOUGH: new game ({(Session.IsDemo ? "demo" : "full")}), seed {seed}");
        }

        // ------------------------------------------------------------------ overlays

        public T Open<T>(T overlay) where T : Overlay
        {
            _overlayLayer.Add(overlay.Root);
            _overlays.Add(overlay);
            overlay.OnOpened();
            return overlay;
        }

        public void CloseOverlay(Overlay overlay)
        {
            if (!_overlays.Remove(overlay)) return;
            overlay.Root.RemoveFromHierarchy();
            overlay.OnClosed();
        }

        public T Find<T>() where T : Overlay
        {
            foreach (var o in _overlays)
                if (o is T t) return t;
            return null;
        }

        public void ToggleHandbook(string countryId = null)
        {
            var hb = Find<HandbookView>();
            if (hb != null)
            {
                CloseOverlay(hb);
                return;
            }
            Open(new HandbookView(this, countryId));
            Sfx.Play(SfxId.Paper, 0.7f);
        }

        public void OpenComputer(int? orderId = null)
        {
            if (!Session.PowerOn)
            {
                Toasts.Show("No power. The computer is dead until the power comes back.", "bad");
                Sfx.Play(SfxId.Error, 0.5f);
                return;
            }
            var pc = Find<ComputerView>() ?? Open(new ComputerView(this));
            if (orderId.HasValue) pc.OpenEditor(orderId.Value);
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (Session == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            GameSettings.Poll();
            HandleKeys();

            bool paused = false;
            foreach (var o in _overlays) paused |= o.PausesGame;
            Session.Paused = paused;
            Session.Tick(dt);
            foreach (var e in Session.DrainEvents()) OnSimEvent(e);
            Achievements?.Tick(this, dt);

            Shop.Tick(dt);
            foreach (var o in _overlays.ToArray()) o.Tick(dt);
            Coach.Tick(dt);
            SyncToasts();
            Toasts.Tick(dt);
            DayFlow();
            // After DayFlow: a day card opened this frame is already on top when the banner decides.
            AchToasts?.Tick(dt);
        }

        /// <summary>
        /// Toasts belong to the shop: the splash, title and demo-end screens never show them. Over the
        /// computer and the handbook they drop to the strip under the window, clear of the OS title
        /// bar (date, clock, cash, X) and the handbook's tab column, and fewer stack there.
        /// </summary>
        void SyncToasts()
        {
            if (_toastLayer == null) return;
            bool pc = Find<ComputerView>() != null, binder = Find<HandbookView>() != null;
            _toastLayer.EnableInClassList("le-toasts--low", pc || binder);
            // Under the computer two fit (strip + bezel); under the binder only the strip is free.
            Toasts.MaxVisible = binder ? 1 : pc ? 2 : 5;
            bool hide = MenuUp;
            if (hide == _toastsHidden) return;
            _toastsHidden = hide;
            _toastLayer.style.visibility = hide ? Visibility.Hidden : Visibility.Visible;
        }

        void DayFlow()
        {
            // The title and the demo-end screen own the stage: no day cards over them.
            if (Find<TitleView>() != null || Find<DemoEndView>() != null) return;
            if (Session.Phase == DayPhase.Closed && !_reportShown)
            {
                _reportShown = true;
                foreach (var o in _overlays.ToArray()) CloseOverlay(o);
                Open(new DayReportView(this));
            }
            if (Session.Phase == DayPhase.Morning && _startCardDay != Session.Day)
            {
                _startCardDay = Session.Day;
                _reportShown = false;
                Open(new DayStartView(this));
            }
        }

        bool IsTyping()
        {
            var focused = _root?.panel?.focusController?.focusedElement as VisualElement;
            if (focused == null) return false;
            return focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null;
        }

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (IsTyping()) _root.panel.focusController.focusedElement?.Blur();
                else if (Top != null) Top.OnEscape();
                else Open(new PauseView(this));
                return;
            }
            if (IsTyping()) return;
            // Menu screens (and the settings and achievements cards) take no shop shortcuts.
            if (MenuUp || Find<SettingsView>() != null || Find<AchievementsView>() != null) return;

            if (kb.hKey.wasPressedThisFrame) ToggleHandbook();
            if (kb.spaceKey.wasPressedThisFrame && Top is CameraView cam) cam.Shutter();
            if (kb.f1Key.wasPressedThisFrame)
            {
                // F1 is where players look for help: say what went away and how to get it back.
                bool coached = TutorialCoach.IsActiveFor(this);
                GameSettings.SetHints(this, !ShowHints);
                if (coached) Toasts.Show("R.'s notes hidden. F1 brings them back.");
                else if (TutorialCoach.IsActiveFor(this)) Toasts.Show("R.'s notes are back. (F1 hides them.)");
                else Toasts.Show(ShowHints ? "Hints on. (F1 hides them.)" : "Hints off. F1 or SETTINGS brings them back.");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Development shortcuts (never in a release build).
            if (kb.f5Key.wasPressedThisFrame)
            {
                Session.DebugSpawnWalkIn();
                Toasts.Show("[dev] walk-in customer");
            }
            if (kb.f6Key.wasPressedThisFrame)
            {
                Session.DebugAdvanceMinutes(30f);
                Toasts.Show("[dev] +30 minutes");
            }
            if (kb.f9Key.wasPressedThisFrame && Find<DevLineupView>() == null) Open(new DevLineupView(this));
#endif
        }

        // ------------------------------------------------------------------ sim events

        void OnSimEvent(SimEvent e)
        {
            Coach.OnSimEvent(e);
            // Mail lands while the title is up (R.'s welcome note at boot): no chime and no toast
            // over the menu screens. The day card counts the unread mail instead.
            if (MenuUp) return;
            // R.'s notes (demo Day 1) or the hint line (days 1-3) already say what to do next: the
            // toast then just reports what happened instead of repeating the instruction.
            bool guided = ShowHints && Session.Day <= 3;
            switch (e.Type)
            {
                case SimEventType.DoorOpened:
                    Sfx.Play(SfxId.DoorBell, 0.55f);
                    Sfx.Play(SfxId.DoorOpen, 0.6f);
                    Shop.OnDoor();
                    break;
                case SimEventType.FolderPlaced:
                    Sfx.Play(SfxId.Paper);
                    Toasts.Show("Client sheet on the desk.");
                    break;
                case SimEventType.PrintQueued:
                    Sfx.Play(SfxId.Key, 0.8f);
                    Toasts.Show("Sent to the printer.");
                    break;
                case SimEventType.PagePrinted:
                    Sfx.Play(SfxId.PrinterDone);
                    Toasts.Show(guided ? "Page printed." : "Page printed. Click it in the printer tray to bind it.");
                    break;
                case SimEventType.PrinterJammed:
                    Sfx.Play(SfxId.Jam);
                    Toasts.Show("PRINTER JAM. Click the printer.", "bad");
                    break;
                case SimEventType.DocumentBound:
                    Sfx.Play(SfxId.Stamp);
                    Toasts.Show(guided ? "Booklet bound." : "Booklet bound. Check it, then drag it to the customer.");
                    break;
                case SimEventType.DocumentShredded:
                    Sfx.Play(SfxId.Paper, 0.8f, 0.7f);
                    break;
                case SimEventType.DocumentHandedOver:
                    Sfx.Play(SfxId.Paper, 0.8f, 1.2f);
                    break;
                case SimEventType.CashPlaced:
                    Sfx.Play(SfxId.Paper, 0.6f, 1.4f);
                    break;
                case SimEventType.CashCollected:
                    Sfx.Play(SfxId.Cash, 0.8f);
                    Toasts.Show("+" + Labels.Money(e.Amount), "good");
                    break;
                case SimEventType.MessageArrived:
                    // The morning card lists the overnight mail itself.
                    if (Find<DayStartView>() != null || (Session.Phase == DayPhase.Morning && _startCardDay != Session.Day)) break;
                    Sfx.Play(SfxId.Notify, 0.7f);
                    Toasts.Show("1 NEW MESSAGE");
                    break;
                case SimEventType.Purchase:
                    Sfx.Play(SfxId.Cash, 0.6f, 0.8f);
                    Toasts.Show("Bought: " + e.Text);
                    break;
                case SimEventType.ClosingTime:
                    Sfx.Play(SfxId.Notify, 0.4f, 0.7f);
                    Toasts.Show("18:00 - closing time. Finish up, then flip the sign to CLOSED.");
                    break;
                case SimEventType.OrderCancelled:
                    Sfx.Play(SfxId.Error, 0.5f);
                    Toasts.Show($"Order #{e.OrderId} cancelled ({e.Text}).", "bad");
                    break;
                case SimEventType.PhotoKept:
                    Toasts.Show("Photo kept.");
                    break;
                case SimEventType.Toast:
                    Toasts.Show(DemoToast(e.Text));
                    break;
            }
        }

        /// <summary>
        /// The demo's locked-country visitor ("Japan is in the full game.") gets the same store line as
        /// every other FULL GAME lock, so the toast says it once and points somewhere.
        /// </summary>
        string DemoToast(string text)
        {
            if (!Session.IsDemo || string.IsNullOrEmpty(text)) return text;
            if (text.EndsWith(" is in the full game.", StringComparison.Ordinal) && text.IndexOf("Wishlist", StringComparison.Ordinal) < 0)
                return text + " Wishlist us on Steam!";
            return text;
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>Starts the game in whatever scene is open: press Play and it runs.</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (UnityEngine.Object.FindFirstObjectByType<GameRoot>() != null) return;
            var go = new GameObject("LEGIT ENOUGH");
            go.AddComponent<GameRoot>();
        }
    }
}
