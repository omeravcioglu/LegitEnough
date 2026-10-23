using System.Collections.Generic;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// The shop seen from behind the counter: the room where customers physically walk and
    /// sit, and the desk that IS the interface (design doc §4, §55). The room and the props are
    /// pixel art from <see cref="ShopRenderer"/>; this class owns what you can click and drag.
    /// There is no floating HUD: the time is on the clock, the date on the calendar, the cash
    /// on the cash box and the mail on the computer screen.
    /// </summary>
    public sealed class ShopView
    {
        const int Art = ShopRenderer.Scale;

        readonly GameRoot _game;
        GameSession S => _game.Session;

        readonly ShopRenderer _renderer;
        readonly VisualElement _root;
        readonly VisualElement _actors;
        readonly VisualElement _bubbles;
        readonly VisualElement _choices;
        readonly VisualElement _counterCash;
        readonly VisualElement _sheets;
        readonly VisualElement _booklets;
        readonly VisualElement _tray;
        readonly VisualElement _dragLayer;
        readonly VisualElement _sign;
        readonly Label _hint;

        readonly Dictionary<int, CustomerFigure> _figures = new Dictionary<int, CustomerFigure>();
        readonly Dictionary<int, CustomerFigure> _companionFigures = new Dictionary<int, CustomerFigure>();
        readonly Dictionary<int, VisualElement> _bubbleEls = new Dictionary<int, VisualElement>();
        readonly Dictionary<int, Speech> _lastLine = new Dictionary<int, Speech>();
        readonly List<(PixelImage img, PixelBuffer buf, VisualElement el, Vector2Int art)> _sprites = new List<(PixelImage, PixelBuffer, VisualElement, Vector2Int)>();

        float _time;
        float _doorOpen;
        int _choicesVisit = -1;
        string _choicesKey = "";
        string _sheetsKey = "";
        string _bookletsKey = "";
        string _trayKey = "";
        string _cashKey = "";
        int _spriteLight = -1;
        Dragging _drag;

        sealed class Dragging
        {
            public FinishedDocument Doc;
            public VisualElement Source;
            public VisualElement Ghost;
            public Vector2 Start;
            public bool Moved;
            public int PointerId;
        }

        public ShopRenderer Renderer => _renderer;

        /// <summary>The "another photo?" question is up (the tutorial coach says yes to it).</summary>
        public bool AskingRetake => _retakeAsk != null && _game.Top == _retakeAsk;

        ConfirmView _retakeAsk;

        /// <summary>
        /// A desk booklet's element name: "booklet-{order}" for an order's first booklet on the desk,
        /// "booklet-{order}-{doc}" for any later copy (a reprint with a new photo), so each can be found.
        /// </summary>
        public static string BookletName(GameSession s, FinishedDocument d)
        {
            var first = s.DeskDocuments.Find(x => x.OrderId == d.OrderId);
            return first == null || first == d ? "booklet-" + d.OrderId : "booklet-" + d.OrderId + "-" + d.Id;
        }

        public ShopView(GameRoot game, VisualElement stage)
        {
            _game = game;
            _renderer = new ShopRenderer(game);
            _root = Ui.El("le-shop", stage).At(0, 0, GameRoot.StageWidth, GameRoot.StageHeight);

            // ---------------- room (behind the customers)
            var room = Ui.El("le-room", _root).At(0, 0, GameRoot.StageWidth, GameRoot.StageHeight);
            room.pickingMode = PickingMode.Ignore;
            _renderer.AddBackLayers(room);

            // Click targets carry names so the tutorial coach can point at them.
            _sign = Hit(room, 684, 248, 156, 56, "Door sign: OPEN / CLOSED", "hit-sign");
            _sign.RegisterCallback<ClickEvent>(_ => OnSignClicked());
            var photoHit = Hit(room, 1452, 90, 420, 560, "Photo station", "hit-photo");
            photoHit.RegisterCallback<ClickEvent>(_ => OnPhotoCornerClicked());

            // ---------------- actors
            _actors = Ui.El("le-actors", _root).At(0, 0, GameRoot.StageWidth, GameRoot.StageHeight);
            _actors.pickingMode = PickingMode.Ignore;

            // ---------------- counter, desk and props (in front of the customers)
            var front = Ui.El("le-front", _root).At(0, 0, GameRoot.StageWidth, GameRoot.StageHeight);
            front.pickingMode = PickingMode.Ignore;
            _renderer.AddFrontLayers(front);

            var tripod = Hit(_root, 1452, 464, 156, 236, "Camera", "hit-tripod");
            tripod.RegisterCallback<ClickEvent>(_ => OnPhotoCornerClicked());
            var crt = Hit(_root, 24, 776, 412, 300, "Computer (orders, messages, Official-ish editor)", "hit-crt");
            crt.RegisterCallback<ClickEvent>(_ => _game.OpenComputer());
            var binder = Hit(_root, 448, 800, 248, 268, "Country handbook (H)", "hit-binder");
            binder.RegisterCallback<ClickEvent>(_ => _game.ToggleHandbook());
            var printer = Hit(_root, 1364, 772, 292, 228, "Printer", "hit-printer");
            printer.RegisterCallback<ClickEvent>(_ => OnPrinterClicked());

            _counterCash = Ui.El("le-counter-cash", _root).At(1120, 640, 600, 70);
            _counterCash.pickingMode = PickingMode.Ignore;
            _counterCash.name = "hit-cash";
            _sheets = Ui.El("le-sheets", _root).At(700, 808, 368, 268);
            _sheets.pickingMode = PickingMode.Ignore;
            _sheets.name = "hit-sheets";
            _booklets = Ui.El("le-booklets", _root).At(1072, 808, 280, 268);
            _booklets.pickingMode = PickingMode.Ignore;
            _booklets.name = "hit-booklets";
            _tray = Ui.El("le-tray", _root).At(1368, 996, 288, 76);
            _tray.pickingMode = PickingMode.Ignore;
            _tray.name = "hit-tray";

            // ---------------- floating UI
            _bubbles = Ui.El("le-bubbles", _root).At(0, 0, GameRoot.StageWidth, GameRoot.StageHeight);
            _bubbles.pickingMode = PickingMode.Ignore;
            _choices = Ui.El("le-choices", _root).At(1150, 280, 520, -1);
            _choices.name = "choices";
            _choices.Show(false);

            _hint = Ui.Text("", "le-hint", _root).At(16, 700);
            _hint.pickingMode = PickingMode.Ignore;

            _root.Add(_renderer.Flicker);
            _dragLayer = Ui.El("le-drag-layer", _root).At(0, 0, GameRoot.StageWidth, GameRoot.StageHeight);
            _dragLayer.pickingMode = PickingMode.Ignore;
        }

        /// <summary>An invisible click target over something drawn in the pixel art.</summary>
        static VisualElement Hit(VisualElement parent, float x, float y, float w, float h, string tip, string name)
        {
            var e = Ui.El("le-hit", parent).At(x, y, w, h);
            e.name = name;
            if (tip.Length > 0) e.tooltip = tip;
            else e.pickingMode = PickingMode.Ignore;
            return e;
        }

        public void OnDoor() => _doorOpen = 1.4f;

        // ================================================================== per frame

        public void Tick(float dt)
        {
            _time += dt;
            if (_doorOpen > 0f) _doorOpen = Mathf.Max(0f, _doorOpen - dt);
            _renderer.Tick(dt, _doorOpen);

            SyncFigures(dt);
            SyncBubbles();
            SyncChoices();
            SyncDesk();
            SyncHint();
        }

        void SyncFigures(float dt)
        {
            var alive = new HashSet<int>();
            foreach (var v in S.Visits)
            {
                alive.Add(v.Id);
                if (!_figures.TryGetValue(v.Id, out var fig))
                {
                    fig = new CustomerFigure(v, false) { Room = _renderer };
                    fig.RegisterCallback<ClickEvent>(e => OnFigureClicked(v));
                    _figures[v.Id] = fig;
                    _actors.Add(fig);
                    if (v.Companion != null)
                    {
                        var comp = new CustomerFigure(v, true) { Room = _renderer };
                        comp.RegisterCallback<ClickEvent>(e => OnFigureClicked(v));
                        _companionFigures[v.Id] = comp;
                        _actors.Add(comp);
                    }
                }
                fig.Sync(dt);
                if (_companionFigures.TryGetValue(v.Id, out var c)) c.Sync(dt);

                // New line → voice blips.
                if (v.Line != null && (!_lastLine.TryGetValue(v.Id, out var last) || last != v.Line))
                {
                    var speaker = v.Line.ByCompanion && v.Companion != null ? v.Companion : v.Customer;
                    if (_game.Top == null || _game.Top is CameraView) Sfx.Voice(v.Line.Text, speaker.VoicePitch);
                }
                _lastLine[v.Id] = v.Line;
            }

            foreach (var id in new List<int>(_figures.Keys))
            {
                if (alive.Contains(id)) continue;
                _figures[id].RemoveFromHierarchy();
                _figures.Remove(id);
                if (_companionFigures.TryGetValue(id, out var c))
                {
                    c.RemoveFromHierarchy();
                    _companionFigures.Remove(id);
                }
                _lastLine.Remove(id);
            }

            // Depth sort: farther (smaller) figures first.
            var all = new List<CustomerFigure>();
            foreach (VisualElement child in _actors.Children()) if (child is CustomerFigure f) all.Add(f);
            all.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            for (int i = 0; i < all.Count; i++) all[i].BringToFront();
        }

        void SyncBubbles()
        {
            var alive = new HashSet<int>();
            foreach (var v in S.Visits)
            {
                if (v.Line == null) continue;
                bool comp = v.Line.ByCompanion && _companionFigures.ContainsKey(v.Id);
                var fig = comp ? _companionFigures[v.Id] : (_figures.TryGetValue(v.Id, out var f) ? f : null);
                if (fig == null || fig.resolvedStyle.opacity < 0.3f) continue;
                alive.Add(v.Id);

                if (!_bubbleEls.TryGetValue(v.Id, out var bubble))
                {
                    bubble = Ui.El("le-bubble le-clickable", _bubbles);
                    bubble.style.position = Position.Absolute;
                    Ui.Text("", "le-bubble__name", bubble);
                    Ui.Text("", "le-bubble__text", bubble);
                    Ui.Deco("le-bubble__tail", bubble);
                    int id = v.Id;
                    bubble.RegisterCallback<ClickEvent>(_ => S.SkipLine(id));
                    _bubbleEls[v.Id] = bubble;
                }
                var speaker = comp ? v.Companion : v.Customer;
                bubble.Q<Label>(className: "le-bubble__name").SetTextIfChanged(speaker.FirstName.ToUpperInvariant());
                bubble.Q<Label>(className: "le-bubble__text").SetTextIfChanged(v.Line.Text);

                var head = fig.HeadStage;
                float hs = fig.HeadSizeStage;
                bool leftSide = head.x > 1380f;
                float bw = 420f;
                float x = leftSide ? head.x - hs * 0.6f - bw : head.x + hs * 0.55f;
                float y = head.y - hs * 0.9f - 30f;
                x = Mathf.Clamp(x, 10f, 1910f - bw);
                y = Mathf.Clamp(y, 54f, 600f);
                bubble.style.left = Mathf.Round(x / Art) * Art;
                bubble.style.top = Mathf.Round(y / Art) * Art;
                bubble.style.width = bw;
                bubble.EnableInClassList("le-bubble--left", leftSide);
            }
            foreach (var id in new List<int>(_bubbleEls.Keys))
            {
                if (alive.Contains(id)) continue;
                _bubbleEls[id].RemoveFromHierarchy();
                _bubbleEls.Remove(id);
            }
        }

        void SyncChoices()
        {
            Visit chooser = null;
            foreach (var v in S.Visits)
                if (v.Phase == VisitPhase.Choosing && v.Choices.Count > 0) { chooser = v; break; }

            bool hide = chooser == null || (_game.Top != null && !(_game.Top is HandbookView));
            _choices.Show(!hide);
            if (chooser == null)
            {
                _choicesVisit = -1;
                return;
            }
            string key = "";
            foreach (var c in chooser.Choices) key += c.Id + (c.Enabled ? "+" : "-") + c.Text;
            if (_choicesVisit == chooser.Id && _choicesKey == key) return;
            _choicesVisit = chooser.Id;
            _choicesKey = key;
            _choices.Clear();
            Ui.Text("YOU:", "le-choices__label", _choices);
            foreach (var c in chooser.Choices)
            {
                var visitId = chooser.Id;
                var choiceId = c.Id;
                var b = Ui.Btn(c.Text, () => S.Choose(visitId, choiceId), "le-btn--choice", _choices);
                b.name = "choice-" + c.Id; // the tutorial coach points at single replies
                b.SetEnabled(c.Enabled);
            }
        }

        // ================================================================== desk items

        /// <summary>A clickable pixel sprite: an element holding a 4x-scaled image of a buffer.</summary>
        VisualElement Sprite(VisualElement parent, float x, float y, PixelBuffer buf, string classes)
        {
            var el = Ui.El("le-px-sprite " + classes, parent).At(x, y, buf.W * Art, buf.H * Art);
            var img = new PixelImage().At(0, 0, buf.W * Art, buf.H * Art);
            el.Add(img);
            // Containers are placed with inline styles, so the room position is known up front.
            var art = new Vector2Int(Mathf.RoundToInt((parent.style.left.value.value + x) / Art), Mathf.RoundToInt((parent.style.top.value.value + y) / Art));
            _sprites.Add((img, buf, el, art));
            return el;
        }

        void LightSprites(bool force)
        {
            if (!force && _spriteLight == _renderer.LightVersion) return;
            _spriteLight = _renderer.LightVersion;
            _sprites.RemoveAll(s => s.el.panel == null);
            foreach (var (img, buf, _, art) in _sprites)
            {
                var lit = new PixelBuffer(buf.W, buf.H);
                System.Array.Copy(buf.Px, lit.Px, buf.W * buf.H);
                _renderer.Light.Apply(lit, art.x, art.y);
                img.Upload(lit);
            }
        }

        void SyncDesk()
        {
            bool changed = false;

            // Client sheets: one per open order.
            string sheetsKey = "";
            foreach (var o in S.Orders.Open()) sheetsKey += o.Id + ",";
            if (sheetsKey != _sheetsKey)
            {
                _sheetsKey = sheetsKey;
                _sheets.Clear();
                changed = true;
                int i = 0;
                foreach (var o in S.Orders.Open())
                {
                    var customer = S.Customer(o.CustomerId);
                    var buf = new PixelBuffer(27, 31);
                    DeskSprites.Sheet(buf, o.Id, customer.Identity.Surname, DeskSprites.SheetCountry(S.Country(o.CountryId).Spec));
                    var sheet = Sprite(_sheets, (i % 3) * 120, (i / 3) * 132 + (i % 2) * 8, buf, "le-sheet");
                    sheet.name = "sheet-" + o.Id;
                    var orderId = o.Id;
                    sheet.RegisterCallback<ClickEvent>(_ =>
                    {
                        Sfx.Play(SfxId.Paper, 0.7f);
                        _game.Open(new SlipView(_game, orderId));
                    });
                    sheet.tooltip = $"Client sheet #{o.Id}: {customer.Identity.Surname} ({S.Country(o.CountryId).Spec.Name}) - click to read";
                    if (++i >= 6) break;
                }
            }

            // Booklets on the desk.
            string bKey = "";
            foreach (var d in S.DeskDocuments) bKey += d.Id + ",";
            if (bKey != _bookletsKey)
            {
                _bookletsKey = bKey;
                _booklets.Clear();
                changed = true;
                int i = 0;
                foreach (var d in S.DeskDocuments)
                {
                    var buf = new PixelBuffer(19, 28);
                    DeskSprites.Booklet(buf, Looks.Of(d.BookletCover), d.OrderId);
                    var el = Sprite(_booklets, (i % 3) * 84, (i / 3) * 124, buf, "le-booklet");
                    el.name = BookletName(S, d);
                    var owner = S.Orders.Get(d.OrderId);
                    string name = owner != null ? S.Customer(owner.CustomerId).Identity.Surname : "?";
                    el.tooltip = $"Booklet #{d.OrderId} ({name}): click to inspect, drag to a customer to hand it over";
                    var doc = d;
                    el.RegisterCallback<PointerDownEvent>(e => BeginDrag(e, el, doc));
                    el.RegisterCallback<PointerMoveEvent>(e => MoveDrag(e));
                    el.RegisterCallback<PointerUpEvent>(e => EndDrag(e));
                    if (++i >= 6) break;
                }
            }

            // Printed pages in the tray.
            var pr = S.Printer;
            Sfx.SetPrinterRunning(pr.Printing != null && !pr.Jammed && S.PowerOn && !S.Paused);
            string trayKey = "";
            foreach (var p in pr.Tray) trayKey += p.Id + ",";
            if (trayKey != _trayKey)
            {
                _trayKey = trayKey;
                _tray.Clear();
                changed = true;
                int i = 0;
                foreach (var page in pr.Tray)
                {
                    var buf = new PixelBuffer(13, 17);
                    DeskSprites.TrayPage(buf, page.OrderId);
                    var el = Sprite(_tray, 8 + i * 52, 0, buf, "le-tray-page");
                    el.name = "page-" + page.OrderId;
                    var pageId = page.Id;
                    el.RegisterCallback<ClickEvent>(_ => _game.Open(new PrintMenu(_game, pageId)));
                    el.tooltip = $"Printed data page #{page.OrderId} - click to bind, inspect or shred";
                    if (++i >= 5) break;
                }
            }

            // Cash on the counter.
            string cashKey = "";
            foreach (var c in S.CounterCash) cashKey += c.Id + ",";
            if (cashKey != _cashKey)
            {
                _cashKey = cashKey;
                _counterCash.Clear();
                changed = true;
                int i = 0;
                foreach (var c in S.CounterCash)
                {
                    var buf = new PixelBuffer(34, 14);
                    DeskSprites.Cash(buf, c.Amount, c.Id);
                    var el = Sprite(_counterCash, i * 152, 0, buf, "le-cash");
                    el.name = "cash-" + c.Id;
                    var id = c.Id;
                    el.RegisterCallback<ClickEvent>(_ => S.CollectCash(id));
                    el.tooltip = $"{Labels.Money(c.Amount)} - click to put it in the cash box";
                    i++;
                }
            }
            LightSprites(changed);
        }

        void SyncHint()
        {
            // The guided Day 1 of the demo talks for itself; the corner hint stays quiet meanwhile.
            // It also hides under any open screen (title, cards, computer, papers...): it's about the
            // shop, and only half of it would show past the screen's edge anyway.
            bool coaching = TutorialCoach.IsActiveFor(_game);
            string hint = _game.ShowHints && S.Day <= 3 && _game.Top == null && !coaching ? HintText() : "";
            _hint.SetTextIfChanged(hint);
            _hint.Show(hint.Length > 0);
        }

        string HintText()
        {
            if (S.Phase == DayPhase.Morning)
            {
                var stale = S.IsDemo && S.Inbox.Unread == 0 ? OutdatedHandbook() : null;
                if (stale != null)
                    return $"{stale.Spec.Name} changed its rules: your handbook is OUTDATED. Click the COMPUTER, then SHOP to update it. Then flip the door sign to OPEN.";
                return S.Inbox.Unread > 0 ? "Check the computer: you have messages. Then click the door sign to OPEN the shop." : "Click the door sign to OPEN the shop.";
            }
            if (S.CounterCash.Count > 0) return "Click the cash on the counter to put it away.";
            foreach (var v in S.Visits)
            {
                if (v.Phase == VisitPhase.Choosing) return S.IsDemo ? DemoChoiceHint(v) : "Pick a reply. (Click a speech bubble to skip a line.)";
                if (v.Phase == VisitPhase.PhotoSitting && !v.Moving)
                {
                    bool none = S.Photos.ForCustomer(v.Customer.Id).Count == 0;
                    var c = v.Order != null ? S.Country(v.Order.CountryId) : null;
                    if (S.IsDemo && none && c != null && c.HandbookOutdated)
                        return $"Careful: your {c.Spec.Name} handbook is OUTDATED. Click the COMPUTER, then SHOP: the update is {Labels.Money(Balance.HandbookUpdateCost)}. The NEWS says what changed.";
                    return none
                        ? "Click the camera to photograph them. Check the HANDBOOK (H) for the photo rules first."
                        : "Photo kept. Press DONE in the camera when you're finished. Not happy later? Click the photo corner to call them back.";
                }
                if (v.Phase == VisitPhase.AwaitingHandover) return "Drag their booklet from the desk onto them.";
                if (v.Phase == VisitPhase.Inspecting) return "They're looking it over...";
                if (S.IsDemo && v.Phase == VisitPhase.Waiting && !v.Moving && v.Order != null && v.Order.Status == OrderStatus.AwaitingPhoto)
                    return "They still need a photo. Click the photo corner to call them to the stool.";
            }
            if (S.IsDemo && S.Printer.Jammed) return "The printer jammed. Click it. Not too hard.";
            if (S.Printer.Tray.Count > 0) return "Click the printed page in the printer tray to bind it into a booklet.";
            if (S.DeskDocuments.Count > 0) return "Inspect the booklet (click it), then drag it onto the customer.";
            foreach (var o in S.Orders.Open())
                if (o.Status == OrderStatus.InProduction && S.Printer.Printing == null)
                {
                    if (S.IsDemo && o.Urgent && !S.Printer.Busy)
                        return $"Rush job, due at {o.Deadline.Clock}. Click the COMPUTER and open OFFICIAL-ISH. Keep the client sheet handy.";
                    return "Make the document: click the COMPUTER and open OFFICIAL-ISH. Keep the client sheet handy.";
                }
            if (S.IsDemo && S.Printer.Busy)
                return S.Equipment[EquipmentSlot.Printer].Tier <= 1 ? "Printing... (the old inkjet takes its time)" : "Printing...";
            if (S.Phase == DayPhase.AfterHours && S.Visits.Count == 0) return "Closing time. Click the door sign to CLOSE and end the day.";
            if (S.Visits.Count == 0 && S.Phase == DayPhase.Open)
                return S.IsDemo && NobodyElseToday()
                    ? "Nobody else is coming today. Click the door sign to close early."
                    : "Waiting for customers... (the clock on the wall is running)";
            return "";
        }

        /// <summary>Demo hints at the counter: why a reply is the only one, and what a rush job means.</summary>
        string DemoChoiceHint(Visit v)
        {
            var c = v.Request != null ? S.Country(v.Request.CountryId) : null;
            bool onlyDecline = v.Choices.Count == 1 && v.Choices[0].Id == GameSession.ChoiceIds.Decline;
            if (onlyDecline && c != null && c.FullGameOnly) return "Not this one. All you can do here is say no.";
            if (onlyDecline && c != null && !c.Owned)
                return c.Requestable(S.Standing.Reputation)
                    ? $"No {c.Spec.Name} section, so no {c.Spec.Adjective} jobs. Click the COMPUTER, then SHOP: it's {Labels.Money(c.Spec.HandbookPrice)}. Then you can take this one."
                    : $"No {c.Spec.Name} section, so no {c.Spec.Adjective} jobs. All you can do here is say no.";
            if (onlyDecline && c != null && c.Owned && !c.AcceptingJobs)
                return $"You switched {c.Spec.Name} off in Border Watch (computer, NEWS). Turn it back on and you can take this one.";
            if (v.Choices.Exists(ch => ch.Id == GameSession.ChoiceIds.TrustMe))
                return "They spotted a problem. Say you'll fix it and redo the work, or tell them to trust you.";
            if (v.Choices.Exists(ch => ch.Id == GameSession.ChoiceIds.FixIt))
                return "They're not buying it. Say you'll fix it, then redo what they said.";
            if (v.Order == null && c != null && c.Owned && c.HandbookOutdated)
                return $"Your {c.Spec.Name} handbook is OUTDATED. Click the COMPUTER, then SHOP: the update is {Labels.Money(Balance.HandbookUpdateCost)}. Then take the job.";
            if (v.Order == null && v.Request != null && v.Request.Urgent)
                return "A rush job: more money, a hard deadline. Check the clock before you say yes.";
            return "Pick a reply. (Click a speech bubble to skip a line.)";
        }

        CountryState OutdatedHandbook()
        {
            foreach (var c in S.CountryList)
                if (c.Owned && c.HandbookOutdated) return c;
            return null;
        }

        /// <summary>True when the day's visitors are done: nobody in the shop and nobody else due today.</summary>
        bool NobodyElseToday()
        {
            foreach (var a in S.Director.Upcoming)
                if (a.At.Day <= S.Day) return false;
            return true;
        }

        // ================================================================== interactions

        void OnSignClicked()
        {
            switch (S.Phase)
            {
                case DayPhase.Morning:
                    S.OpenShop();
                    Sfx.Play(SfxId.Pop);
                    break;
                case DayPhase.Open:
                case DayPhase.AfterHours:
                    if (!S.CanCloseShop(out string reason))
                    {
                        _game.Toasts.Show(reason, "bad");
                        Sfx.Play(SfxId.Error, 0.5f);
                        return;
                    }
                    if (S.Phase == DayPhase.Open)
                    {
                        bool lastDay = S.IsDemo && S.Day >= S.Rules.LastDay;
                        _game.Open(new ConfirmView(_game, "Close early?",
                            lastDay ? "It's not 18:00 yet. Closing now ends the day, and the demo." : "It's not 18:00 yet. Closing now ends the day.",
                            "CLOSE THE SHOP", () => S.CloseShop()));
                        return;
                    }
                    S.CloseShop();
                    break;
            }
        }

        void OnPhotoCornerClicked()
        {
            var stool = S.StoolVisit;
            if (stool != null && stool.Phase == VisitPhase.PhotoSitting && !stool.Moving)
            {
                var session = S.BeginPhotoSession(stool.Id);
                if (session != null) _game.Open(new CameraView(_game, stool));
                return;
            }
            var counter = S.CounterVisit;
            if (counter != null && counter.Phase == VisitPhase.Choosing && counter.Choices.Exists(c => c.Id == GameSession.ChoiceIds.TakePhoto && c.Enabled))
            {
                S.Choose(counter.Id, GameSession.ChoiceIds.TakePhoto);
                return;
            }
            foreach (var v in S.BenchVisits)
            {
                if (v.Order != null && v.Order.Status == OrderStatus.AwaitingPhoto && S.SendToStool(v.Id)) return;
            }
            var again = stool == null ? RetakeCandidate() : null;
            if (again != null)
            {
                AskRetake(again);
                return;
            }
            _game.Toasts.Show(stool != null ? "They're still getting settled." : "Nobody on the stool. Accept a job first.");
        }

        /// <summary>
        /// Who a click on the photo corner calls back for another photo: someone whose passport isn't
        /// handed over yet. With several, the one photographed last (the photo you're likely unhappy with).
        /// </summary>
        Visit RetakeCandidate()
        {
            Visit best = null;
            int newest = -1;
            foreach (var v in S.Visits)
            {
                if (!S.CanCallBackForPhoto(v.Id)) continue;
                int last = -1;
                foreach (var p in S.Photos.ForCustomer(v.Customer.Id)) last = Mathf.Max(last, p.Id);
                if (best == null || last > newest)
                {
                    best = v;
                    newest = last;
                }
            }
            return best;
        }

        void AskRetake(Visit v)
        {
            var o = v.Order;
            bool male = v.Customer.Identity.Sex == Sex.Male;
            string name = StreetName.Person(v.Customer.FirstName);
            bool made = o.Status == OrderStatus.Ready || S.Printer.Tray.Exists(p => p.OrderId == o.Id)
                || (S.Printer.Printing != null && S.Printer.Printing.OrderId == o.Id);
            string text = $"Call {name} back to the stool for a new photo? It costs a little of {(male ? "his" : "her")} patience. "
                + (made
                    ? "The passport you made keeps the old photo: pick the new one in the editor and print again."
                    : "Then pick the photo you want in the editor.");
            int id = v.Id;
            _retakeAsk = new ConfirmView(_game, "Another photo?", text, male ? "CALL HIM BACK" : "CALL HER BACK", () =>
            {
                _retakeAsk = null;
                if (S.CallBackForPhoto(id)) Sfx.Play(SfxId.Pop, 0.6f);
                else if (!S.Visits.Exists(x => x.Id == id && x.Line != null))
                    _game.Toasts.Show("Not now. Try again when the stool is free.");
            });
            _game.Open(_retakeAsk);
        }

        void OnFigureClicked(Visit v)
        {
            if (_drag != null) return;
            if (v.Line != null)
            {
                S.SkipLine(v.Id);
                return;
            }
            if (v.Phase == VisitPhase.PhotoSitting)
            {
                OnPhotoCornerClicked();
                return;
            }
            if (v.Phase == VisitPhase.Waiting || v.Phase == VisitPhase.Queueing)
            {
                S.CallVisit(v.Id);
                Sfx.Play(SfxId.Pop, 0.6f);
            }
        }

        void OnPrinterClicked()
        {
            if (S.Printer.Jammed)
            {
                S.ClearPrinterJam();
                Sfx.Play(SfxId.Stamp, 0.9f, 0.8f);
                _game.Toasts.Show(S.Equipment[EquipmentSlot.Printer].Tier == 1
                    ? "You hit it. Not too hard. It hums again."
                    : "You yank the paper out. It hums again.");
                return;
            }
            if (S.Printer.Tray.Count > 0) _game.Open(new PrintMenu(_game, S.Printer.Tray[0].Id));
            else if (S.Printer.Printing != null) _game.Toasts.Show($"Still printing. {Mathf.RoundToInt(S.Printer.Progress * 100f)}%.");
            else if (S.Printer.Busy) _game.Toasts.Show("Still printing.");
        }

        // ---------------- booklet drag & drop

        void BeginDrag(PointerDownEvent e, VisualElement source, FinishedDocument doc)
        {
            if (e.button != 0) return;
            _drag = new Dragging
            {
                Doc = doc,
                Source = source,
                Start = _root.WorldToLocal(e.position),
                PointerId = e.pointerId,
            };
            source.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void MoveDrag(PointerMoveEvent e)
        {
            if (_drag == null || !_drag.Source.HasPointerCapture(e.pointerId)) return;
            var pos = _root.WorldToLocal(e.position);
            if (!_drag.Moved && (pos - _drag.Start).sqrMagnitude > 64f)
            {
                _drag.Moved = true;
                var buf = new PixelBuffer(19, 28);
                DeskSprites.Booklet(buf, Looks.Of(_drag.Doc.BookletCover), _drag.Doc.OrderId);
                buf.Outline(Pal.Outline);
                var ghost = new PixelImage("le-booklet--ghost");
                ghost.style.position = Position.Absolute;
                ghost.style.width = buf.W * Art;
                ghost.style.height = buf.H * Art;
                ghost.Upload(buf);
                _dragLayer.Add(ghost);
                _drag.Ghost = ghost;
                _drag.Source.style.opacity = 0.3f;
            }
            if (_drag.Ghost != null)
            {
                _drag.Ghost.style.left = Mathf.Round((pos.x - 38f) / Art) * Art;
                _drag.Ghost.style.top = Mathf.Round((pos.y - 56f) / Art) * Art;
            }
        }

        void EndDrag(PointerUpEvent e)
        {
            if (_drag == null) return;
            var d = _drag;
            _drag = null;
            if (d.Source.HasPointerCapture(e.pointerId)) d.Source.ReleasePointer(e.pointerId);
            d.Ghost?.RemoveFromHierarchy();
            d.Source.style.opacity = 1f;
            if (!d.Moved)
            {
                _game.Open(new PassportView(_game, d.Doc));
                return;
            }
            var pos = _root.WorldToLocal(e.position);
            var target = FigureAt(pos);
            if (target == null && pos.y < RoomLayout.DeskTop && pos.y > RoomLayout.CounterTop - 120f) target = S.CounterVisit;
            if (target == null)
            {
                _game.Toasts.Show("Drop the booklet on a customer to hand it over.");
                return;
            }
            if (!S.GiveDocument(target.Id, d.Doc.Id))
            {
                _game.Toasts.Show(target.Order == null ? "They don't have an order with you." : "They're not ready to take it right now.", "bad");
                Sfx.Play(SfxId.Error, 0.4f);
            }
        }

        Visit FigureAt(Vector2 stagePos)
        {
            CustomerFigure best = null;
            foreach (var kv in _figures)
            {
                var fig = kv.Value;
                var local = fig.WorldToLocal(_root.LocalToWorld(stagePos));
                if (!fig.ContainsPoint(local)) continue;
                if (best == null || fig.Depth > best.Depth) best = fig;
            }
            return best?.Visit;
        }
    }
}
