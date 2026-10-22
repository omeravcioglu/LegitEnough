using System;
using System.Collections.Generic;
using System.Globalization;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;
using Calendar = LegitEnough.Sim.Calendar;

namespace LegitEnough.Game
{
    /// <summary>
    /// The shop computer (design doc §38, §55): ORDERS, MESSAGES, the OFFICIAL-ISH 98
    /// document editor, CLIENTS, NEWS and SHOP. The client sheet for the order being edited is
    /// pinned beside the monitor.
    /// </summary>
    public sealed class ComputerView : Overlay
    {
        enum App { Orders, Messages, Editor, Clients, News, Shop }

        readonly VisualElement _main;
        readonly VisualElement _pinned;
        readonly Label _barClock;
        readonly Label _barCash;
        readonly Dictionary<App, Button> _nav = new Dictionary<App, Button>();
        readonly Dictionary<App, Label> _navLabels = new Dictionary<App, Label>();
        App _app = App.Orders;
        int? _orderId;
        int? _messageId;
        int? _clientId;
        string _key = "";
        DataPageView _preview;
        Label _printerLine;
        float _refreshTimer;
        // The current app's main scroll list, kept in place when the app re-renders itself.
        ScrollView _scroll;
        string _scrollKey;

        public ComputerView(GameRoot game) : base(game, "le-overlay--dim")
        {
            // The monitor is whatever computer the shop owns (design doc §42: the desk evolves).
            var bezel = Ui.El("le-monitor le-monitor--t" + S.Computer.Tier, Root).At(330, 16, 1360, 980);
            var screen = Ui.El("le-monitor__screen", bezel);
            Ui.Text(S.Computer.Name.ToUpperInvariant(), "le-monitor__brand", bezel);

            var bar = Ui.El("le-os-bar", screen);
            Ui.Text("OFFICIAL-ISH OS", "le-os-bar__title", bar);
            _barClock = Ui.Text("", "le-os-bar__item", bar);
            _barCash = Ui.Text("", "le-os-bar__item", bar);
            Ui.Btn("X", Close, "le-os-close", bar).name = "os-close";

            var body = Ui.El("le-os-body", screen);
            var nav = Ui.El("le-os-nav", body);
            AddNav(nav, App.Orders, "ORDERS", "orders");
            AddNav(nav, App.Messages, "MESSAGES", "messages");
            AddNav(nav, App.Editor, "OFFICIAL-ISH 98", "editor");
            AddNav(nav, App.Clients, "CLIENTS", "clients");
            AddNav(nav, App.News, "NEWS", "news");
            AddNav(nav, App.Shop, "SHOP", "shop");
            _main = Ui.El("le-os-main", body);
            if (S.Computer.Tier == 1) screen.Add(new Painted(DrawScanlines, "le-monitor__scanlines"));

            _pinned = Ui.El("le-pinned", Root).At(14, 110, 310, 820);
            Sfx.Play(SfxId.Click, 0.6f, 0.6f);

            // Open where the work is: unread mail first, otherwise the editor if something needs a document.
            if (S.Inbox.Unread > 0) _app = App.Messages;
            else
            {
                foreach (var o in S.Orders.Open())
                {
                    if (o.Status == OrderStatus.InProduction)
                    {
                        _app = App.Editor;
                        _orderId = o.Id;
                        break;
                    }
                }
            }
            Render(true);
        }

        /// <summary>A desktop icon with its label underneath, named "nav-{icon}" for the tutorial.</summary>
        void AddNav(VisualElement nav, App app, string label, string icon)
        {
            var b = Ui.Btn("", () =>
            {
                _app = app;
                Render(true);
            }, "le-os-nav__item", nav);
            b.name = "nav-" + icon;
            var img = UiArt.Image(UiArt.Icon(icon), 3, "le-os-nav__icon");
            img.pickingMode = PickingMode.Ignore;
            b.Add(img);
            var l = Ui.Text(label, "le-os-nav__label", b);
            l.pickingMode = PickingMode.Ignore;
            _nav[app] = b;
            _navLabels[app] = l;
        }

        /// <summary>The app on screen: orders, messages, editor, clients, news or shop (read by the tutorial coach).</summary>
        public string CurrentApp => _app.ToString().ToLowerInvariant();

        /// <summary>The order open in the editor (0: none).</summary>
        public int EditorOrderId => _orderId ?? 0;

        public void OpenEditor(int orderId)
        {
            _orderId = orderId;
            _app = App.Editor;
            Render(true);
        }

        /// <summary>Switch app by name: orders, messages, editor, clients, news, shop.</summary>
        public void OpenApp(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "orders": _app = App.Orders; break;
                case "messages": _app = App.Messages; break;
                case "editor": _app = App.Editor; break;
                case "clients": _app = App.Clients; break;
                case "news": _app = App.News; break;
                case "shop": _app = App.Shop; break;
                default: return;
            }
            Render(true);
        }

        public override void Tick(float dt)
        {
            if (!S.PowerOn)
            {
                Game.Toasts.Show("The power cut out. The screen goes black.", "bad");
                Close();
                return;
            }
            _barClock.SetTextIfChanged($"{Calendar.ShortDate(S.Day).ToUpperInvariant()}  {S.Now.Clock}");
            _barCash.SetTextIfChanged(Labels.Money(S.Ledger.Cash));
            foreach (var kv in _nav) kv.Value.EnableInClassList("le-os-nav__item--active", kv.Key == _app);
            _navLabels[App.Messages].SetTextIfChanged(S.Inbox.Unread > 0 ? $"MESSAGES ({S.Inbox.Unread})" : "MESSAGES");
            // Demo: the Day-2 sale shows on the desktop, not only inside the SHOP.
            if (S.IsDemo) _navLabels[App.Shop].SetTextIfChanged(S.SaleActive ? "SHOP: SALE" : "SHOP");

            _preview?.Refresh();
            if (_printerLine != null) _printerLine.SetTextIfChanged(PrinterLine());

            _refreshTimer -= dt;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 0.5f;
                Render(false);
            }
        }

        string PrinterLine()
        {
            var p = S.Printer;
            if (p.Jammed) return "PRINTER: JAMMED";
            if (p.Printing != null) return $"PRINTER: printing #{p.Printing.OrderId} ({Mathf.RoundToInt(p.Progress * 100f)}%)" + (p.Queued > 0 ? $", {p.Queued} queued" : "");
            if (p.Tray.Count > 0) return $"PRINTER: {p.Tray.Count} page(s) in the tray";
            return S.PrinterBroken ? "PRINTER: ready (making a grinding noise)" : "PRINTER: ready";
        }

        /// <summary>Rebuilds the current app when its data changed (or when forced).</summary>
        void Render(bool force)
        {
            string key = _app + "|" + StateKey();
            if (!force && key == _key) return;
            _key = key;
            // Re-rendering the same view (a click, a payment) keeps its scroll position.
            string scrollKey = _app + ":" + (_app == App.Editor ? _orderId : null);
            Vector2? keep = _scroll != null && scrollKey == _scrollKey ? _scroll.scrollOffset : (Vector2?)null;
            _main.Clear();
            _preview = null;
            _printerLine = null;
            _scroll = null;
            switch (_app)
            {
                case App.Orders: RenderOrders(); break;
                case App.Messages: RenderMessages(); break;
                case App.Editor: RenderEditor(); break;
                case App.Clients: RenderClients(); break;
                case App.News: RenderNews(); break;
                case App.Shop: RenderShop(); break;
            }
            _scrollKey = _scroll != null ? _app + ":" + (_app == App.Editor ? _orderId : null) : null;
            if (keep.HasValue && _scroll != null && keep.Value != Vector2.zero) KeepScroll(_scroll, keep.Value);
            RenderPinned();
        }

        /// <summary>A vertical list for the monitor: no horizontal scroller (a USS rule can't hide it).</summary>
        static ScrollView VScroll(string classes, VisualElement parent)
        {
            var sv = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            Ui.AddClasses(sv, classes);
            parent.Add(sv);
            return sv;
        }

        /// <summary>Restores a scroll offset once the rebuilt content has been laid out.</summary>
        static void KeepScroll(ScrollView sv, Vector2 offset)
        {
            EventCallback<GeometryChangedEvent> cb = null;
            cb = _ =>
            {
                sv.contentContainer.UnregisterCallback(cb);
                sv.scrollOffset = offset;
            };
            sv.contentContainer.RegisterCallback(cb);
        }

        string StateKey()
        {
            switch (_app)
            {
                case App.Orders:
                {
                    string k = S.Day + ":";
                    foreach (var o in S.Orders.All) k += o.Id + "" + (int)o.Status + o.Outcome + ",";
                    return k;
                }
                case App.Messages: return S.Inbox.Messages.Count + ":" + S.Inbox.Unread + ":" + _messageId;
                case App.Editor:
                {
                    string k = _orderId + ":";
                    foreach (var o in S.Orders.Open()) k += o.Id + ",";
                    var order = _orderId.HasValue ? S.Orders.Get(_orderId.Value) : null;
                    if (order != null)
                    {
                        k += order.Draft.CountryId + order.Draft.PhotoId + S.Photos.ForCustomer(order.CustomerId).Count;
                        k += order.Draft.Category + "" + order.Draft.Cover + order.Draft.Pages;
                    }
                    return k;
                }
                case App.Clients: return S.Clients.Count + ":" + _clientId + ":" + S.Day;
                case App.News: return S.News.Count + ":" + S.Day;
                case App.Shop:
                {
                    string k = S.Ledger.Cash + ":" + S.PrinterBroken + S.SaleActive;
                    foreach (var kv in S.Equipment) k += kv.Value.Id;
                    foreach (var c in S.CountryList) k += c.Owned + "" + c.HandbookOutdated;
                    return k + (int)S.Standing.Reputation / 5;
                }
            }
            return "";
        }

        void RenderPinned()
        {
            _pinned.Clear();
            var order = _orderId.HasValue ? S.Orders.Get(_orderId.Value) : null;
            if (_app != App.Editor || order == null)
            {
                _pinned.Show(false);
                return;
            }
            _pinned.Show(true);
            var paper = Ui.El("le-slip le-slip--pinned", _pinned);
            SlipView.BuildSheet(paper, S, order, S.Customer(order.CustomerId));
            Ui.Deco("le-tape", _pinned);
        }

        // ================================================================== ORDERS

        void RenderOrders()
        {
            Ui.Text("ORDERS", "le-os-h1", _main);
            var open = new List<Order>(S.Orders.Open());
            if (open.Count == 0) Ui.Text("No open orders.", "le-os-muted", _main);
            else
            {
                var head = Ui.El("le-os-row le-os-row--head", _main);
                foreach (var h in new[] { "#", "CLIENT", "COUNTRY", "STATUS", "DUE", "PRICE" }) Ui.Text(h, "le-os-cell", head);
                foreach (var o in open)
                {
                    var row = Ui.El("le-os-row le-clickable", _main);
                    var c = S.Customer(o.CustomerId);
                    Ui.Text("#" + o.Id, "le-os-cell", row);
                    Ui.Text(c.Identity.Surname + ", " + CustomerGenerator.TitleCase(c.Identity.GivenNames), "le-os-cell", row);
                    Ui.Text(S.Country(o.CountryId).Spec.Name.ToUpperInvariant(), "le-os-cell", row);
                    var statusCell = Ui.El("le-os-cell", row);
                    Ui.Text(StatusText(o.Status), "le-os-chip le-os-chip--" + ChipKind(o.Status), statusCell);
                    bool late = S.Now > o.Deadline;
                    string due = $"{Calendar.Relative(o.Deadline.Day, S.Day)} {o.Deadline.Clock}" + (o.WaitsInShop ? " (waiting)" : " (pickup)");
                    Ui.Text(late ? "LATE - " + due : due, "le-os-cell" + (late ? " le-os-bad" : ""), row);
                    Ui.Text(Labels.Money(o.Price) + (o.Urgent ? " URGENT" : ""), "le-os-cell", row);
                    int id = o.Id;
                    row.RegisterCallback<ClickEvent>(_ => OpenEditor(id));
                }
            }

            Ui.Text("DELIVERED", "le-os-h2", _main);
            var delivered = new List<Order>();
            foreach (var o in S.Orders.All) if (o.Status == OrderStatus.Delivered) delivered.Add(o);
            delivered.Reverse();
            if (delivered.Count == 0) Ui.Text("Nothing delivered yet.", "le-os-muted", _main);
            for (int i = 0; i < Math.Min(8, delivered.Count); i++)
            {
                var o = delivered[i];
                var c = S.Customer(o.CustomerId);
                string outcome = o.Outcome == OutcomeKind.Pending ? "no news yet" : o.Outcome.ToString().ToUpperInvariant();
                var row = Ui.El("le-os-row", _main);
                Ui.Text("#" + o.Id, "le-os-cell", row);
                Ui.Text(c.Identity.Surname, "le-os-cell", row);
                Ui.Text(S.Country(o.CountryId).Spec.Name.ToUpperInvariant(), "le-os-cell", row);
                Ui.Text("paid " + Labels.Money(o.Paid), "le-os-cell", row);
                Ui.Text(o.DeliveredAt.HasValue ? "day " + o.DeliveredAt.Value.Day : "", "le-os-cell", row);
                Ui.Text(outcome, "le-os-cell" + (o.Outcome == OutcomeKind.Failed ? " le-os-bad" : o.Outcome == OutcomeKind.Success ? " le-os-good" : ""), row);
            }
        }

        static string ChipKind(OrderStatus s) => s switch
        {
            OrderStatus.AwaitingPhoto => "photo",
            OrderStatus.InProduction => "make",
            OrderStatus.Ready => "ready",
            _ => "other",
        };

        static string StatusText(OrderStatus s) => s switch
        {
            OrderStatus.AwaitingPhoto => "NEEDS PHOTO",
            OrderStatus.InProduction => "MAKE DOCUMENT",
            OrderStatus.Ready => "READY",
            _ => s.ToString().ToUpperInvariant(),
        };

        // ================================================================== MESSAGES

        void RenderMessages()
        {
            Ui.Text("MESSAGES", "le-os-h1", _main);
            if (S.InternetDown) Ui.Text("(no connection - new messages will arrive later)", "le-os-bad", _main);
            var cols = Ui.El("le-os-cols", _main);
            var list = _scroll = VScroll("le-os-list", cols);
            var reader = Ui.El("le-os-reader", cols);
            if (S.Inbox.Messages.Count == 0) list.Add(Ui.Text("Inbox empty.", "le-os-muted"));
            if (_messageId == null && S.Inbox.Messages.Count > 0) _messageId = S.Inbox.Messages[0].Id;
            foreach (var m in S.Inbox.Messages)
            {
                var row = Ui.El("le-os-msg le-clickable");
                row.name = "msg-" + m.Id; // the tutorial coach points at R.'s notes
                row.EnableInClassList("le-os-msg--unread", !m.Read);
                row.EnableInClassList("le-os-msg--selected", m.Id == _messageId);
                Ui.Text(m.From, "le-os-msg__from", row);
                Ui.Text(m.Subject, "le-os-msg__subject", row);
                Ui.Text($"Day {m.ArrivesAt.Day}, {m.ArrivesAt.Clock}", "le-os-msg__time", row);
                int id = m.Id;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    _messageId = id;
                    Sfx.Play(SfxId.Click, 0.5f);
                    Render(true);
                });
                list.Add(row);
            }
            var sel = _messageId.HasValue ? S.Inbox.Get(_messageId.Value) : null;
            if (sel != null)
            {
                sel.Read = true;
                Ui.Text("FROM: " + sel.From, "le-os-h2", reader);
                Ui.Text($"Subject: {sel.Subject}   -   Day {sel.ArrivesAt.Day}, {sel.ArrivesAt.Clock}", "le-os-muted", reader);
                Ui.Text(sel.Body, "le-os-body-text", reader);
                if (sel.CustomerId.HasValue && S.Clients.TryGetRecord(sel.CustomerId.Value, out _))
                {
                    int cid = sel.CustomerId.Value;
                    Ui.Btn("OPEN CLIENT FILE", () =>
                    {
                        _clientId = cid;
                        _app = App.Clients;
                        Render(true);
                    }, "le-os-btn", reader);
                }
            }
        }

        // ================================================================== EDITOR

        void RenderEditor()
        {
            Ui.Text("OFFICIAL-ISH 98  -  document editor", "le-os-h1", _main);
            var picker = Ui.El("le-row le-row--wrap le-os-picker", _main);
            var open = new List<Order>(S.Orders.Open());
            if (open.Count == 0)
            {
                Ui.Text("No open orders. Accept a job at the counter first.", "le-os-muted", _main);
                return;
            }
            if (!_orderId.HasValue || S.Orders.Get(_orderId.Value) == null || !S.Orders.Get(_orderId.Value).IsOpen) _orderId = open[0].Id;
            foreach (var o in open)
            {
                int id = o.Id;
                var b = Ui.Btn($"#{o.Id} {S.Customer(o.CustomerId).Identity.Surname}", () =>
                {
                    _orderId = id;
                    Render(true);
                }, "le-os-btn le-os-btn--tab", picker);
                b.EnableInClassList("le-os-btn--selected", id == _orderId);
            }

            var order = S.Orders.Get(_orderId.Value);
            var draft = order.Draft;
            var customer = S.Customer(order.CustomerId);
            var cols = Ui.El("le-os-cols", _main);
            var form = _scroll = VScroll("le-os-form", cols);
            var right = Ui.El("le-os-preview", cols);

            // 1. Template. The blocks below carry names ("editor-...") the tutorial points at.
            Section(form, "1. TEMPLATE");
            if (S.IsDemo) DemoTemplates(form, draft);
            else
            {
                var tpl = Ui.El("le-row le-row--wrap", form);
                tpl.name = "editor-template";
                foreach (var c in S.CountryList)
                    if (c.Owned) TemplateButton(tpl, draft, c);
            }
            var typeRow = Ui.El("le-row", form);
            Ui.Btn("PASSPORT", () => { }, "le-os-btn le-os-btn--selected", typeRow);
            if (S.IsDemo) LockedButton(typeRow, "VISA", "Visas are").name = "editor-visa";
            else Ui.Btn("VISA (not licensed)", () => { }, "le-os-btn", typeRow).SetEnabled(false);

            // 2. Format.
            var format = Ui.El(null, form);
            format.name = "editor-format";
            Section(format, "2. FORMAT");
            var catRow = Ui.El("le-row le-row--wrap", format);
            Ui.Text("CATEGORY", "le-os-label", catRow);
            foreach (DocCategory cat in Enum.GetValues(typeof(DocCategory)))
            {
                var cc = cat;
                var b = Ui.Btn(Labels.Of(cat), () =>
                {
                    draft.Category = cc;
                    Render(true);
                }, "le-os-btn le-os-btn--small", catRow);
                b.EnableInClassList("le-os-btn--selected", draft.Category == cat);
            }
            var coverRow = Ui.El("le-row le-row--wrap", format);
            Ui.Text("COVER", "le-os-label", coverRow);
            foreach (CoverColor cov in Enum.GetValues(typeof(CoverColor)))
            {
                var cv = cov;
                var b = Ui.Btn(Labels.Of(cov), () =>
                {
                    draft.Cover = cv;
                    Render(true);
                }, "le-os-btn le-os-btn--small le-os-btn--cover", coverRow);
                b.style.borderLeftColor = Looks.Of(cov);
                b.EnableInClassList("le-os-btn--selected", draft.Cover == cov);
            }
            var pageRow = Ui.El("le-row le-row--wrap", format);
            Ui.Text("PAGES", "le-os-label", pageRow);
            foreach (int pages in new[] { 24, 32, 48, 64 })
            {
                int pg = pages;
                var b = Ui.Btn(pages.ToString(CultureInfo.InvariantCulture), () =>
                {
                    draft.Pages = pg;
                    Render(true);
                }, "le-os-btn le-os-btn--small", pageRow);
                b.EnableInClassList("le-os-btn--selected", draft.Pages == pages);
            }

            // 3. Data.
            var data = Ui.El(null, form);
            data.name = "editor-data";
            Section(data, "3. DATA");
            var spec = S.Country(draft.CountryId)?.Spec;
            if (spec == null)
            {
                Ui.Text("Pick a template first: the fields depend on it.", "le-os-muted", data);
            }
            else
            {
                if (S.Computer.ImportsNames || S.Computer.AutoFormatsDates)
                {
                    var tools = Ui.El("le-row", data);
                    if (S.Computer.ImportsNames)
                        Ui.Btn("IMPORT NAMES", () =>
                        {
                            S.ImportNames(order.Id);
                            Render(true);
                        }, "le-os-btn le-os-btn--small", tools);
                    if (S.Computer.AutoFormatsDates)
                        Ui.Btn("FORMAT DATES", () =>
                        {
                            S.AutoFormatDates(order.Id);
                            Render(true);
                        }, "le-os-btn le-os-btn--small", tools);
                }
                foreach (var f in spec.Passport.PersonalFields) FieldEditor(data, draft, f);
                Ui.Text($"Dates: use the template's format. Today is on the calendar (and up in the corner).", "le-os-muted", data);
                FieldEditor(data, draft, DocField.DateOfIssue);
                FieldEditor(data, draft, DocField.DateOfExpiry);
                var numRow = Ui.El("le-row", data);
                var numField = TextFor(draft, DocField.DocumentNumber, numRow);
                numField.AddToClassList("le-os-input--num");
                Ui.Btn("GENERATE", () =>
                {
                    numField.value = S.GenerateDocumentNumber(order.Id);
                }, "le-os-btn le-os-btn--small le-os-btn--fixed", numRow).name = "editor-docnumber";
            }

            // 4. Photo.
            var photoBlock = Ui.El(null, form);
            photoBlock.name = "editor-photo";
            Section(photoBlock, "4. PHOTO");
            var photos = S.Photos.ForCustomer(order.CustomerId);
            if (photos.Count == 0) Ui.Text("No kept photos of this client yet. Photograph them at the stool.", "le-os-muted", photoBlock);
            else if (photos.Count > 1) Ui.Text("Newest first. Click the one that goes on the passport.", "le-os-muted", photoBlock);
            var photoRow = Ui.El("le-row le-row--wrap", photoBlock);
            // Newest first: after a retake the new shot is the first one you see.
            for (int pi = photos.Count - 1; pi >= 0; pi--)
            {
                var ph = photos[pi];
                var holder = Ui.El("le-os-photo le-clickable", photoRow);
                holder.name = "editor-photo-" + ph.Id;
                bool inUse = draft.PhotoId == ph.Id;
                holder.EnableInClassList("le-os-photo--selected", inUse);
                var thumb = new PortraitView("le-thumb") { Capture = ph };
                thumb.pickingMode = PickingMode.Ignore;
                holder.Add(thumb);
                thumb.View = ph.Crop;
                if (photos.Count > 1 && (inUse || pi == photos.Count - 1))
                {
                    var tag = Ui.Text(inUse ? "IN USE" : "NEWEST", "le-os-photo__tag", holder);
                    tag.pickingMode = PickingMode.Ignore;
                }
                int pid = ph.Id;
                holder.RegisterCallback<ClickEvent>(_ =>
                {
                    draft.PhotoId = pid;
                    Sfx.Play(SfxId.Click, 0.6f);
                    Render(true);
                });
            }

            if (photos.Count > 0)
                Ui.Text("Want a better one? Close the computer and click the photo corner: they sit again.", "le-os-muted", photoBlock);

            // Preview + print.
            Ui.Text("PREVIEW", "le-os-label", right);
            _preview = new DataPageView(S) { Data = draft };
            right.Add(_preview);
            _preview.Refresh();
            var printRow = Ui.El("le-row", right);
            Ui.Btn($"PRINT  ({Labels.Money(Balance.PrintCost)})", () =>
            {
                if (S.PrintDraft(order.Id) != null) Sfx.Play(SfxId.Key);
            }, "le-os-btn le-os-btn--primary", printRow).name = "editor-print";
            _printerLine = Ui.Text(PrinterLine(), "le-os-muted", right);
            Ui.Text("Nothing is checked for you. Read it over before you print.", "le-os-muted", right);
        }

        static void Section(VisualElement parent, string title) => Ui.Text(title, "le-os-h2", parent);

        /// <summary>An owned country's template button, named "tpl-{id}".</summary>
        void TemplateButton(VisualElement parent, DocumentDraft draft, CountryState c)
        {
            string cid = c.Id;
            var b = Ui.Btn(c.Spec.Name.ToUpperInvariant(), () =>
            {
                draft.CountryId = cid;
                Render(true);
            }, "le-os-btn", parent);
            b.name = "tpl-" + cid;
            b.EnableInClassList("le-os-btn--selected", draft.CountryId == cid);
        }

        /// <summary>
        /// The demo's template picker: owned templates as usual, then the playable countries still to
        /// get (greyed flags with what they take: a price in the SHOP or the reputation they need), then
        /// the full-game countries as padlocked flags under a stamp. A click on a greyed flag says why.
        /// Every country keeps its "tpl-{id}" name.
        /// </summary>
        void DemoTemplates(VisualElement form, DocumentDraft draft)
        {
            var box = Ui.El(null, form);
            box.name = "editor-template";
            var owned = Ui.El("le-row le-row--wrap", box);
            var shop = Ui.El("le-row le-os-tplrow", box);
            Ui.Text("BUY IN SHOP", "le-os-label le-os-tplrow__head", shop);
            var rep = Ui.El("le-row le-os-tplrow", box);
            Ui.Text("NEEDS REP", "le-os-label le-os-tplrow__head", rep);
            var locked = Ui.El("le-row le-os-tplrow", box);
            var head = Ui.El("le-os-tplrow__head", locked);
            head.Add(FullGameLocks.Badge(2, null));
            bool toBuy = false, toEarn = false;
            foreach (var c in S.CountryList)
            {
                if (c.Owned)
                {
                    TemplateButton(owned, draft, c);
                    continue;
                }
                if (c.FullGameOnly)
                {
                    string subject = c.Spec.Name + " is";
                    var lockTile = Ui.Btn("", () => FullGameLocks.Nudge(Game, subject), "le-os-btn le-os-btn--tile le-os-btn--locked", locked);
                    lockTile.name = "tpl-" + c.Id;
                    lockTile.Add(FullGameLocks.Flag(c.Id, 2, null));
                    continue;
                }
                bool buyable = c.Requestable(S.Standing.Reputation);
                string tip = buyable
                    ? $"{c.Spec.Name}: buy the section in the SHOP."
                    : $"{c.Spec.Name} needs {c.Spec.UnlockReputation} reputation.";
                var tile = Ui.Btn("", () => Game.Toasts.Show(tip, ""), "le-os-btn le-os-btn--tile le-os-btn--tile-off", buyable ? shop : rep);
                tile.name = "tpl-" + c.Id;
                var flag = UiArt.Image(UiArt.Flag(c.Id), 2);
                flag.pickingMode = PickingMode.Ignore;
                tile.Add(flag);
                Ui.Text(buyable ? Labels.Money(c.Spec.HandbookPrice) : c.Spec.UnlockReputation + " REP", "le-os-tile__cap", tile).pickingMode = PickingMode.Ignore;
                if (buyable) toBuy = true;
                else toEarn = true;
            }
            shop.Show(toBuy);
            rep.Show(toEarn);
        }

        /// <summary>
        /// A FULL GAME button: optional label, padlock and the red stamp. It stays enabled (a disabled
        /// button would fade the stamp) and answers a click with the wishlist nudge.
        /// </summary>
        Button LockedButton(VisualElement parent, string label, string subject)
        {
            var b = Ui.Btn("", () => FullGameLocks.Nudge(Game, subject), "le-os-btn le-os-btn--locked", parent);
            if (label != null) Ui.Text(label, "le-os-lock__label", b).pickingMode = PickingMode.Ignore;
            b.Add(FullGameLocks.Padlock(2, "le-os-lock__padlock"));
            b.Add(FullGameLocks.Badge(2, null));
            return b;
        }

        void FieldEditor(VisualElement form, DocumentDraft draft, DocField f)
        {
            if (f == DocField.Sex)
            {
                var row = Ui.El("le-row", form);
                Ui.Text(Labels.Of(f), "le-os-label", row);
                foreach (var s in new[] { "M", "F" })
                {
                    string v = s;
                    var b = Ui.Btn(s, () =>
                    {
                        draft.Set(DocField.Sex, v);
                        Render(true);
                    }, "le-os-btn le-os-btn--small", row);
                    b.EnableInClassList("le-os-btn--selected", draft.Get(DocField.Sex) == s);
                }
                return;
            }
            if (f == DocField.EyeColor)
            {
                var row = Ui.El("le-row le-row--wrap", form);
                Ui.Text(Labels.Of(f), "le-os-label", row);
                foreach (EyeColor e in Enum.GetValues(typeof(EyeColor)))
                {
                    string v = Labels.Of(e);
                    var b = Ui.Btn(v, () =>
                    {
                        draft.Set(DocField.EyeColor, v);
                        Render(true);
                    }, "le-os-btn le-os-btn--small", row);
                    b.EnableInClassList("le-os-btn--selected", draft.Get(DocField.EyeColor) == v);
                }
                return;
            }
            var r = Ui.El("le-row", form);
            TextFor(draft, f, r);
        }

        TextField TextFor(DocumentDraft draft, DocField f, VisualElement parent)
        {
            var tf = new TextField(Labels.Of(f)) { value = draft.Get(f), maxLength = 40 };
            tf.AddToClassList("le-os-input");
            tf.RegisterValueChangedCallback(evt => draft.Set(f, evt.newValue));
            tf.RegisterCallback<KeyDownEvent>(_ => Sfx.Play(SfxId.Key, 0.35f, UnityEngine.Random.Range(0.9f, 1.15f)));
            parent.Add(tf);
            return tf;
        }

        // ================================================================== CLIENTS

        void RenderClients()
        {
            Ui.Text("CLIENTS", "le-os-h1", _main);
            Ui.Text("Your name on the street: " + StreetName.Of(S.Standing.Tier), "le-os-muted", _main);
            var cols = Ui.El("le-os-cols", _main);
            var list = _scroll = VScroll("le-os-list", cols);
            var detail = Ui.El("le-os-reader", cols);
            var records = new List<ClientRecord>(S.Clients.Records);
            records.Sort((a, b) => b.LastVisitDay.CompareTo(a.LastVisitDay));
            if (records.Count == 0) list.Add(Ui.Text("No clients yet.", "le-os-muted"));
            foreach (var r in records)
            {
                var c = S.Customer(r.CustomerId);
                var row = Ui.El("le-os-msg le-clickable");
                row.EnableInClassList("le-os-msg--selected", _clientId == r.CustomerId);
                Ui.Text(c.Identity.Surname + ", " + CustomerGenerator.TitleCase(c.Identity.GivenNames), "le-os-msg__from", row);
                var st = Ui.Text(r.Status.ToString().ToUpperInvariant(), "le-os-msg__subject", row);
                st.style.color = Looks.Of(r.Status);
                int id = r.CustomerId;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    _clientId = id;
                    Render(true);
                });
                list.Add(row);
            }
            if (_clientId.HasValue && S.Clients.TryGetRecord(_clientId.Value, out var rec))
            {
                var c = S.Customer(rec.CustomerId);
                var top = Ui.El("le-row", detail);
                var face = new PortraitView("le-os-face") { Capture = MugShot(c), ShowGrain = false };
                face.pickingMode = PickingMode.Ignore;
                top.Add(face);
                var info = Ui.El("le-col", top);
                Ui.Text(c.DisplayName, "le-os-h2", info);
                var stl = Ui.Text("STATUS: " + rec.Status.ToString().ToUpperInvariant(), "le-os-text", info);
                stl.style.color = Looks.Of(rec.Status);
                Ui.Text($"First visit: day {rec.FirstVisitDay}   Last visit: day {rec.LastVisitDay}", "le-os-text", info);
                Ui.Text($"Visits: {rec.Visits}   Jobs: {rec.Jobs}   Total paid: {Labels.Money(rec.TotalPaid)}", "le-os-text", info);
                Ui.Text("HISTORY", "le-os-label", detail);
                foreach (var line in rec.Log) Ui.Text(line, "le-os-text", detail);
            }
            else Ui.Text("Select a client.", "le-os-muted", detail);
        }

        static PhotoCapture MugShot(CustomerProfile c) => new PhotoCapture
        {
            Id = c.Id,
            CustomerId = c.Id,
            Look = c.Look,
            Pose = HeadPose.Center,
            Expression = c.Behavior.Resting,
            Glasses = new List<GlassesStyle>(c.Look.Glasses),
            HatOn = c.Look.Hat != HatStyle.None,
            Backdrop = Backdrop.LightGray,
            Zoom = 2f,
            PanY = 0.3f,
            SensorHeightPx = 2000,
        };

        // ================================================================== NEWS

        void RenderNews()
        {
            Ui.Text("NEWS", "le-os-h1", _main);
            var cols = Ui.El("le-os-cols", _main);
            var feed = _scroll = VScroll("le-os-list le-os-list--news", cols);
            // The watch scrolls too: rows keep their height instead of being squeezed under each other.
            var watch = VScroll("le-os-reader le-os-watch", cols);

            if (S.News.Count == 0) feed.Add(Ui.Text("Quiet day. No headlines yet.", "le-os-muted"));
            for (int i = S.News.Count - 1; i >= 0 && i >= S.News.Count - 14; i--)
            {
                var n = S.News[i];
                var item = Ui.El("le-os-news");
                Ui.Text($"DAY {n.Day}  ·  {n.Section}", "le-os-msg__time", item);
                Ui.Text(n.Headline, "le-os-news__headline", item);
                Ui.Text(n.Body, "le-os-text", item);
                feed.Add(item);
            }

            Ui.Text("BORDER WATCH", "le-os-h2", watch);
            Ui.Text("Attention on the shop: " + Standing.Name(S.Standing.HeatLevel), "le-os-text", watch);
            var head = Ui.El("le-os-row le-os-row--head", watch);
            Ui.Text("COUNTRY", "le-os-cell le-os-cell--country", head);
            Ui.Text("RISK", "le-os-cell le-os-cell--risk", head);
            Ui.Text("DEMAND", "le-os-cell le-os-cell--demand", head);
            Ui.Text("JOBS", "le-os-cell le-os-cell--jobs", head);
            var fullGame = new List<CountryState>();
            foreach (var c in S.CountryList)
            {
                if (c.FullGameOnly)
                {
                    fullGame.Add(c);
                    continue;
                }
                bool requestable = c.Requestable(S.Standing.Reputation);
                if (!requestable && !S.IsDemo) continue;
                var row = Ui.El("le-os-row", watch);
                Ui.Text(c.Spec.Name.ToUpperInvariant(), "le-os-cell le-os-cell--country", row);
                if (!requestable)
                {
                    // Demo: a playable country you can't reach yet, greyed with what it takes.
                    row.AddToClassList("le-os-row--locked");
                    Ui.Text($"NEEDS {c.Spec.UnlockReputation} REP", "le-os-cell le-os-cell--needs", row);
                    continue;
                }
                var lvl = c.RiskLevel(S.Day);
                var risk = Ui.Text(lvl.ToString().ToUpperInvariant(), "le-os-cell le-os-cell--risk", row);
                risk.style.color = Looks.Of(lvl);
                float demand = c.Demand(S.Day);
                Ui.Text(demand > 1.01f ? "HIGH" : "normal", "le-os-cell le-os-cell--demand", row);
                var jobs = Ui.El("le-os-cell le-os-cell--jobs", row);
                if (c.Owned)
                {
                    string cid = c.Id;
                    Ui.Btn(c.AcceptingJobs ? "TAKING JOBS" : "NOT TAKING", () =>
                    {
                        S.SetAcceptingJobs(cid, !S.Country(cid).AcceptingJobs);
                        Render(true);
                    }, "le-os-btn le-os-btn--small" + (c.AcceptingJobs ? "" : " le-os-btn--off"), jobs);
                }
                else Ui.Text("no handbook", "le-os-muted", jobs);
            }
            // Demo: the full-game countries share one greyed row (each flag answers for itself).
            if (fullGame.Count > 0)
            {
                var row = Ui.El("le-os-row le-os-row--locked le-os-row--fold le-clickable", watch);
                row.name = "watch-lock-all";
                var more = Ui.El("le-os-cell le-os-watch__more", row);
                var flags = Ui.El("le-row le-row--wrap", more);
                var names = new List<string>();
                foreach (var c in fullGame)
                {
                    var flag = FullGameLocks.Flag(c.Id, 2, "le-os-watch__flag");
                    flag.name = "watch-lock-" + c.Id;
                    flag.pickingMode = PickingMode.Position;
                    string subject = c.Spec.Name + " is";
                    flag.RegisterCallback<ClickEvent>(e =>
                    {
                        e.StopPropagation();
                        FullGameLocks.Nudge(Game, subject);
                    });
                    flags.Add(flag);
                    names.Add(c.Spec.Name.ToUpperInvariant());
                }
                Ui.Text(string.Join(", ", names), "le-os-watch__names", more);
                var span = Ui.El("le-os-cell le-os-cell--locked le-os-watch__stamp", row);
                span.Add(FullGameLocks.Padlock(2, "le-os-lock__padlock"));
                span.Add(FullGameLocks.Badge(2, null));
                row.RegisterCallback<ClickEvent>(_ => FullGameLocks.Nudge(Game, "These are"));
            }
            Ui.Text(S.IsDemo
                ? "Not taking a country? Its clients get turned away at the counter."
                : "Turning a country off means fewer of those customers walk in.", "le-os-muted", watch);
        }

        // ================================================================== SHOP

        void RenderShop()
        {
            Ui.Text("SHOP", "le-os-h1", _main).name = "shop-title";
            int sale = SalePercent();
            string saleText = !S.SaleActive ? "" : sale > 0 ? $"    SALE: {sale}% OFF EQUIPMENT" : "    SALE ON EQUIPMENT";
            Ui.Text("Cash: " + Labels.Money(S.Ledger.Cash) + saleText, "le-os-text", _main);
            var scroll = _scroll = VScroll("le-os-form le-os-form--full", _main);

            // The demo puts the handbook sections first: they're what R. sends you here for.
            var fullGame = new List<CountryState>();
            if (S.IsDemo) HandbookSections(scroll, fullGame);
            EquipmentSection(scroll);
            if (!S.IsDemo) HandbookSections(scroll, fullGame);

            // Demo: the full-game sections after everything you can buy, then the visa license.
            if (fullGame.Count > 0) Section(scroll, "IN THE FULL GAME");
            foreach (var c in fullGame)
            {
                var row = Ui.El("le-os-shoprow le-os-shoprow--locked", scroll);
                row.name = "shop-lock-" + c.Id;
                var col = Ui.El("le-col", row);
                Ui.Text($"{c.Spec.Name.ToUpperInvariant()}  (tier {c.Spec.Tier})", "le-os-text le-os-strong", col);
                Ui.Text($"{c.Spec.Adjective} jobs are in the full game.", "le-os-muted", col);
                LockedButton(row, null, c.Spec.Name + " is");
            }
            if (S.IsDemo)
            {
                Section(scroll, "LICENSES");
                var row = Ui.El("le-os-shoprow le-os-shoprow--locked", scroll);
                row.name = "shop-lock-VISA";
                var col = Ui.El("le-col", row);
                Ui.Text("VISA LICENSE", "le-os-text le-os-strong", col);
                Ui.Text("Visas as well as passports. More paper, more rules, more money.", "le-os-muted", col);
                LockedButton(row, null, "Visas are");
            }

            if (S.PrinterBroken)
            {
                Section(scroll, "REPAIRS");
                var row = Ui.El("le-os-shoprow", scroll);
                Ui.Text("Printer repair - it'll stop grinding and print at full speed.", "le-os-text", row);
                var b = Ui.Btn("REPAIR " + Labels.Money(Balance.PrinterRepairCost), () =>
                {
                    S.RepairPrinter();
                    Render(true);
                }, "le-os-btn", row);
                b.SetEnabled(S.Ledger.Cash >= Balance.PrinterRepairCost);
            }
        }

        /// <summary>The running sale as a percentage, read off the dearest item (prices round to $25).</summary>
        int SalePercent() => SalePercentOf(S);

        /// <summary>The running equipment sale in percent (0: none). Also read by the tutorial coach.</summary>
        public static int SalePercentOf(GameSession s)
        {
            if (s == null || !s.SaleActive) return 0;
            EquipmentDef top = null;
            foreach (var e in EquipmentCatalog.All)
                if (top == null || e.Price > top.Price) top = e;
            if (top == null || top.Price <= 0) return 0;
            float pct = 100f * (1f - s.PriceOf(top) / (float)top.Price);
            return Mathf.Max(0, Mathf.RoundToInt(pct / 5f) * 5);
        }

        void EquipmentSection(VisualElement scroll)
        {
            Section(scroll, "EQUIPMENT");
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                var current = S.Equipment[slot];
                var next = EquipmentCatalog.All.Find(e => e.Slot == slot && e.Tier == current.Tier + 1);
                if (next != null && S.EquipmentLocked(next))
                {
                    LockedEquipmentRow(scroll, current, next, false);
                    continue;
                }
                var row = Ui.El("le-os-shoprow", scroll);
                // What you have → what you'd buy, drawn exactly as they stand in the shop.
                var pics = Ui.El("le-os-shoppics", row);
                pics.Add(UiArt.Image(UiArt.Equipment(slot, current.Tier), 2, "le-os-product le-os-product--owned"));
                if (next != null)
                {
                    Ui.Text("→", "le-os-product__arrow", pics);
                    pics.Add(UiArt.Image(UiArt.Equipment(slot, next.Tier), 2, "le-os-product"));
                }
                var col = Ui.El("le-col", row);
                Ui.Text($"{slot.ToString().ToUpperInvariant()}: {current.Name}", "le-os-text le-os-strong", col);
                if (next == null)
                {
                    Ui.Text("Top of the line.", "le-os-muted", col);
                    continue;
                }
                Ui.Text($"Next: {next.Name} - {next.Blurb}", "le-os-muted", col);
                int price = S.PriceOf(next);
                if (price < next.Price) Ui.Text($"On sale. Usually {Labels.Money(next.Price)}.", "le-os-muted le-os-good", col);
                string id = next.Id;
                var b = Ui.Btn("BUY " + Labels.Money(price), () =>
                {
                    if (!S.BuyEquipment(id)) Game.Toasts.Show("Can't afford it.", "bad");
                    Render(true);
                }, "le-os-btn", row);
                b.name = "shop-equip-" + slot.ToString().ToLowerInvariant();
                b.SetEnabled(S.Ledger.Cash >= price);
                // The demo stops a tier short: show what comes after, locked.
                var after = EquipmentCatalog.All.Find(e => e.Slot == slot && e.Tier == next.Tier + 1);
                if (after != null && S.EquipmentLocked(after)) LockedEquipmentRow(scroll, next, after, true);
            }
        }

        /// <summary>
        /// Rule updates and sections to buy. The demo lists updates first, then what you can buy, then
        /// what needs more reputation, and only says customers are asking when one is really coming.
        /// Full-game countries are collected into <paramref name="fullGame"/> for the end of the page.
        /// </summary>
        void HandbookSections(VisualElement scroll, List<CountryState> fullGame)
        {
            Section(scroll, "HANDBOOK SECTIONS");
            var list = new List<CountryState>();
            foreach (var c in S.CountryList)
            {
                if (c.Owned && !c.HandbookOutdated) continue;
                if (c.FullGameOnly) fullGame.Add(c);
                else list.Add(c);
            }
            if (S.IsDemo)
            {
                float rep = S.Standing.Reputation;
                // Updates, then what you can buy, then the rest, each in catalogue order.
                var sorted = new List<CountryState>();
                foreach (var c in list) if (c.Owned) sorted.Add(c);
                foreach (var c in list) if (!c.Owned && c.Requestable(rep)) sorted.Add(c);
                foreach (var c in list) if (!c.Owned && !c.Requestable(rep)) sorted.Add(c);
                list = sorted;
            }
            foreach (var c in list)
            {
                var row = Ui.El("le-os-shoprow", scroll);
                var col = Ui.El("le-col", row);
                string cid = c.Id;
                if (c.Owned)
                {
                    Ui.Text($"{c.Spec.Name.ToUpperInvariant()} - rules changed (day {c.LastRuleChangeDay})", "le-os-text le-os-strong", col);
                    Ui.Text("Your binder is out of date.", "le-os-bad", col);
                    var b = Ui.Btn("UPDATE " + Labels.Money(Balance.HandbookUpdateCost), () =>
                    {
                        S.BuyHandbookUpdate(cid);
                        Render(true);
                    }, "le-os-btn", row);
                    b.name = "shop-update-" + cid;
                    b.SetEnabled(S.Ledger.Cash >= Balance.HandbookUpdateCost);
                    continue;
                }
                bool unlocked = c.Requestable(S.Standing.Reputation);
                Ui.Text($"{c.Spec.Name.ToUpperInvariant()}  (tier {c.Spec.Tier})", "le-os-text le-os-strong", col);
                string line = !unlocked ? "Nobody asks for this yet. Build your name."
                    : !S.IsDemo || FullGameLocks.ClientBooked(S, cid) ? $"Customers are asking. Base price around {Labels.Money(c.Spec.BasePassportPrice)}."
                    : $"No {c.Spec.Adjective} clients booked in the demo. Buy it to read the rules.";
                Ui.Text(line, "le-os-muted", col);
                if (unlocked)
                {
                    var b = Ui.Btn("BUY " + Labels.Money(c.Spec.HandbookPrice), () =>
                    {
                        if (!S.BuyHandbook(cid)) Game.Toasts.Show("Can't afford it.", "bad");
                        Render(true);
                    }, "le-os-btn", row);
                    b.name = "shop-buy-" + cid;
                    b.SetEnabled(S.Ledger.Cash >= c.Spec.HandbookPrice);
                }
                else if (S.IsDemo) Ui.Text($"NEEDS {c.Spec.UnlockReputation} REP", "le-os-text le-os-strong le-os-needs", row);
            }
        }

        /// <summary>
        /// Demo: equipment past the demo's top tier, greyed and padlocked, drawn like an upgrade row
        /// (<paramref name="from"/> → locked). Either it stands in for the upgrade from what you own,
        /// or (<paramref name="then"/>) it follows the buyable upgrade as the step after it.
        /// </summary>
        void LockedEquipmentRow(VisualElement parent, EquipmentDef from, EquipmentDef locked, bool then)
        {
            var row = Ui.El("le-os-shoprow le-os-shoprow--locked", parent);
            row.name = "shop-lock-" + locked.Id;
            var pics = Ui.El("le-os-shoppics", row);
            pics.Add(FullGameLocks.Plain(from.Slot, from.Tier, 2, then ? "le-os-product" : "le-os-product le-os-product--owned"));
            Ui.Text("→", "le-os-product__arrow", pics);
            pics.Add(FullGameLocks.Product(locked.Slot, locked.Tier, 2));
            var col = Ui.El("le-col", row);
            if (then)
            {
                Ui.Text($"THEN: {locked.Name}", "le-os-text le-os-strong", col);
                Ui.Text(locked.Blurb, "le-os-muted", col);
            }
            else
            {
                Ui.Text($"{from.Slot.ToString().ToUpperInvariant()}: {from.Name}", "le-os-text le-os-strong", col);
                Ui.Text($"Next: {locked.Name} - {locked.Blurb}", "le-os-muted", col);
            }
            LockedButton(row, null, locked.Name + " is");
        }

        static void DrawScanlines(Painter2D p, Rect r)
        {
            var c = new Color(0f, 0f, 0f, 0.13f);
            for (float y = 0f; y < r.height; y += 4f) Draw.Rect(p, new Rect(0f, y, r.width, 1.5f), c);
        }
    }

    /// <summary>
    /// The demo's FULL GAME look, shared by the computer and the handbook: padlocks, greyed flags
    /// and products, the red stamp, and the wishlist nudge when something locked is clicked.
    /// Buffers are drawn once and reused (each image still gets its own texture).
    /// </summary>
    internal static class FullGameLocks
    {
        static PixelBuffer _padlock;
        static PixelBuffer _badge;
        static readonly Dictionary<string, PixelBuffer> Flags = new Dictionary<string, PixelBuffer>();
        static readonly Dictionary<int, PixelBuffer> Products = new Dictionary<int, PixelBuffer>();
        static readonly Dictionary<int, PixelBuffer> Plains = new Dictionary<int, PixelBuffer>();
        static string _lastNudge;
        static float _lastNudgeAt = -10f;

        /// <summary>The longest toast that stays on one line (LegitPixel 24px in the 560px toast box).</summary>
        const int MaxToastChars = 49;

        /// <summary>
        /// Demo: is a client for this country really coming (still scheduled, or in the shop now)? Copy
        /// only says customers are asking when one is. Before Day 2 the Day-2 cast counts as coming.
        /// </summary>
        public static bool ClientBooked(GameSession s, string countryId)
        {
            if (s.Day < DemoCast.DayTwo)
                foreach (var b in DemoCast.DayTwoBeats())
                    if (b.CountryId == countryId) return true;
            foreach (var a in s.Director.Upcoming)
                if (a.Kind == ArrivalKind.Story && DemoCast.IsDemoBeat(a.Beat) && a.Beat.CountryId == countryId) return true;
            foreach (var v in s.Visits)
                if (v.Phase != VisitPhase.Gone && DemoCast.IsDemoBeat(v.Beat) && v.Beat.CountryId == countryId) return true;
            return false;
        }

        public static PixelImage Padlock(int scale, string classes) => UiArt.Image(_padlock ??= DemoArt.Padlock(), scale, classes);

        public static PixelImage Badge(int scale, string classes) => UiArt.Image(_badge ??= DemoArt.FullGameBadge(), scale, classes);

        /// <summary>The country's flag, greyed with a padlock on its corner (16x12: it overhangs a 14x10 flag).</summary>
        public static PixelImage Flag(string id, int scale, string classes)
        {
            if (!Flags.TryGetValue(id, out var b)) Flags[id] = b = DemoArt.LockedFlag(id);
            return UiArt.Image(b, scale, classes);
        }

        /// <summary>A piece of equipment as the shop shows it (cached: each one draws a whole room).</summary>
        public static PixelImage Plain(EquipmentSlot slot, int tier, int scale, string classes)
        {
            int key = (int)slot * 10 + tier;
            if (!Plains.TryGetValue(key, out var b)) Plains[key] = b = UiArt.Equipment(slot, tier);
            return UiArt.Image(b, scale, classes);
        }

        /// <summary>A piece of equipment as the shop shows it, drained of colour, padlock in the corner.</summary>
        public static PixelImage Product(EquipmentSlot slot, int tier, int scale)
        {
            int key = (int)slot * 10 + tier;
            if (!Products.TryGetValue(key, out var b))
            {
                b = UiArt.Equipment(slot, tier);
                var dust = new Color32(122, 116, 106, 255);
                for (int i = 0; i < b.Px.Length; i++)
                {
                    var c = b.Px[i];
                    if (c.a == 0) continue;
                    byte l = (byte)((c.r * 30 + c.g * 59 + c.b * 11) / 100);
                    var grey = Pal.Mix(new Color32(l, l, l, c.a), dust, 0.3f);
                    b.Px[i] = new Color32(grey.r, grey.g, grey.b, c.a);
                }
                var lockArt = DemoArt.Padlock();
                b.Blit(lockArt, b.W - lockArt.W - 1, b.H - lockArt.H - 1);
                Products[key] = b;
            }
            return UiArt.Image(b, scale, "le-os-product le-os-product--locked");
        }

        /// <summary>
        /// "Japan is" → "Japan is in the full game. Wishlist us on Steam!" Once per burst of clicks. A toast
        /// fits one line of about 50 characters: longer subjects ("United Kingdom is") drop the wishlist
        /// line rather than wrap (the locked pages and the demo end still carry it).
        /// </summary>
        public static void Nudge(GameRoot game, string subject)
        {
            string text = subject + " in the full game. Wishlist us on Steam!";
            if (text.Length > MaxToastChars) text = subject + " in the full game.";
            if (text == _lastNudge && Time.unscaledTime - _lastNudgeAt < 1.5f) return;
            _lastNudge = text;
            _lastNudgeAt = Time.unscaledTime;
            game.Toasts.Show(text, "");
        }
    }
}
