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

        public GameSession Session { get; private set; }
        public VisualElement Stage { get; private set; }
        public ShopView Shop { get; private set; }
        public Toasts Toasts { get; private set; }
        public bool ShowHints = true;

        UIDocument _doc;
        VisualElement _root;
        VisualElement _overlayLayer;
        readonly List<Overlay> _overlays = new List<Overlay>();
        bool _reportShown;
        int _startCardDay;

        public Overlay Top => _overlays.Count > 0 ? _overlays[_overlays.Count - 1] : null;

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            gameObject.AddComponent<SfxPlayer>();
            CreateDocument();
        }

        void Start()
        {
            NewGame(FixedSeed != 0 ? FixedSeed : (ulong)DateTime.Now.Ticks);
            Open(new TitleView(this));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int((int)StageWidth, (int)StageHeight);
            panel.screenMatchMode = PanelScreenMatchMode.Shrink;
            panel.clearColor = true;
            panel.colorClearValue = new Color(0.035f, 0.03f, 0.028f, 1f);

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
            _root.AddToClassList("le-root");
            _root.RegisterCallback<GeometryChangedEvent>(_ => LayoutStage());
        }

        void LayoutStage()
        {
            if (Stage == null) return;
            var r = _root.layout;
            Stage.style.left = Mathf.Round((r.width - StageWidth) * 0.5f);
            Stage.style.top = Mathf.Round((r.height - StageHeight) * 0.5f);
        }

        public void NewGame(ulong seed)
        {
            foreach (var o in _overlays.ToArray()) CloseOverlay(o);
            _root.Clear();
            Session = new GameSession(seed);
            Stage = Ui.El("le-stage", _root).At(0, 0, StageWidth, StageHeight);
            Shop = new ShopView(this, Stage);
            _overlayLayer = Ui.El("le-overlay-layer", Stage).At(0, 0, StageWidth, StageHeight);
            _overlayLayer.pickingMode = PickingMode.Ignore;
            Toasts = new Toasts(Stage);
            _reportShown = false;
            _startCardDay = 0;
            LayoutStage();
            Debug.Log($"LEGIT ENOUGH: new game, seed {seed}");
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
            HandleKeys();

            bool paused = false;
            foreach (var o in _overlays) paused |= o.PausesGame;
            Session.Paused = paused;
            Session.Tick(dt);
            foreach (var e in Session.DrainEvents()) OnSimEvent(e);

            Shop.Tick(dt);
            foreach (var o in _overlays.ToArray()) o.Tick(dt);
            Toasts.Tick(dt);
            DayFlow();
        }

        void DayFlow()
        {
            if (Find<TitleView>() != null) return;
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

            if (kb.hKey.wasPressedThisFrame) ToggleHandbook();
            if (kb.spaceKey.wasPressedThisFrame && Top is CameraView cam) cam.Shutter();
            if (kb.f1Key.wasPressedThisFrame)
            {
                ShowHints = !ShowHints;
                Toasts.Show(ShowHints ? "Hints on (F1)" : "Hints off (F1)");
            }

            // Development shortcuts.
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
        }

        // ------------------------------------------------------------------ sim events

        void OnSimEvent(SimEvent e)
        {
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
                    Toasts.Show("Page printed. Click it in the printer tray to bind it.");
                    break;
                case SimEventType.PrinterJammed:
                    Sfx.Play(SfxId.Jam);
                    Toasts.Show("PRINTER JAM. Click the printer.", "bad");
                    break;
                case SimEventType.DocumentBound:
                    Sfx.Play(SfxId.Stamp);
                    Toasts.Show("Booklet bound. Check it, then drag it to the customer.");
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
                    Toasts.Show(e.Text);
                    break;
            }
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
