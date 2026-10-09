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
            Ui.Btn("X", Close, "le-os-close", bar);

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

        /// <summary>A desktop icon with its label underneath.</summary>
        void AddNav(VisualElement nav, App app, string label, string icon)
        {
            var b = Ui.Btn("", () =>
            {
                _app = app;
                Render(true);
            }, "le-os-nav__item", nav);
            var img = UiArt.Image(UiArt.Icon(icon), 3, "le-os-nav__icon");
            img.pickingMode = PickingMode.Ignore;
            b.Add(img);
            var l = Ui.Text(label, "le-os-nav__label", b);
            l.pickingMode = PickingMode.Ignore;
            _nav[app] = b;
            _navLabels[app] = l;
        }

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
            _main.Clear();
            _preview = null;
            _printerLine = null;
            switch (_app)
            {
                case App.Orders: RenderOrders(); break;
                case App.Messages: RenderMessages(); break;
                case App.Editor: RenderEditor(); break;
                case App.Clients: RenderClients(); break;
                case App.News: RenderNews(); break;
                case App.Shop: RenderShop(); break;
            }
            RenderPinned();
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
            var list = new ScrollView();
            list.AddToClassList("le-os-list");
            cols.Add(list);
            var reader = Ui.El("le-os-reader", cols);
            if (S.Inbox.Messages.Count == 0) list.Add(Ui.Text("Inbox empty.", "le-os-muted"));
            if (_messageId == null && S.Inbox.Messages.Count > 0) _messageId = S.Inbox.Messages[0].Id;
            foreach (var m in S.Inbox.Messages)
            {
                var row = Ui.El("le-os-msg le-clickable");
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
            var form = new ScrollView();
            form.AddToClassList("le-os-form");
            cols.Add(form);
            var right = Ui.El("le-os-preview", cols);

            // 1. Template.
            Section(form, "1. TEMPLATE");
            var tpl = Ui.El("le-row le-row--wrap", form);
            foreach (var c in S.CountryList)
            {
                if (!c.Owned) continue;
                string cid = c.Id;
                var b = Ui.Btn(c.Spec.Name.ToUpperInvariant(), () =>
                {
                    draft.CountryId = cid;
                    Render(true);
                }, "le-os-btn", tpl);
                b.EnableInClassList("le-os-btn--selected", draft.CountryId == cid);
            }
            var typeRow = Ui.El("le-row", form);
            Ui.Btn("PASSPORT", () => { }, "le-os-btn le-os-btn--selected", typeRow);
            Ui.Btn("VISA (not licensed)", () => { }, "le-os-btn", typeRow).SetEnabled(false);

            // 2. Format.
            Section(form, "2. FORMAT");
            var catRow = Ui.El("le-row le-row--wrap", form);
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
            var coverRow = Ui.El("le-row le-row--wrap", form);
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
            var pageRow = Ui.El("le-row le-row--wrap", form);
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
            Section(form, "3. DATA");
            var spec = S.Country(draft.CountryId)?.Spec;
            if (spec == null)
            {
                Ui.Text("Pick a template first: the fields depend on it.", "le-os-muted", form);
            }
            else
            {
                if (S.Computer.ImportsNames || S.Computer.AutoFormatsDates)
                {
                    var tools = Ui.El("le-row", form);
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
                foreach (var f in spec.Passport.PersonalFields) FieldEditor(form, draft, f);
                Ui.Text($"Dates: use the template's format. Today is on the calendar (and up in the corner).", "le-os-muted", form);
                FieldEditor(form, draft, DocField.DateOfIssue);
                FieldEditor(form, draft, DocField.DateOfExpiry);
                var numRow = Ui.El("le-row", form);
                var numField = TextFor(draft, DocField.DocumentNumber, numRow);
                Ui.Btn("GENERATE", () =>
                {
                    numField.value = S.GenerateDocumentNumber(order.Id);
                }, "le-os-btn le-os-btn--small", numRow);
            }

            // 4. Photo.
            Section(form, "4. PHOTO");
            var photos = S.Photos.ForCustomer(order.CustomerId);
            if (photos.Count == 0) Ui.Text("No kept photos of this client yet. Photograph them at the stool.", "le-os-muted", form);
            var photoRow = Ui.El("le-row le-row--wrap", form);
            foreach (var ph in photos)
            {
                var holder = Ui.El("le-os-photo le-clickable", photoRow);
                holder.EnableInClassList("le-os-photo--selected", draft.PhotoId == ph.Id);
                var thumb = new PortraitView("le-thumb") { Capture = ph };
                thumb.pickingMode = PickingMode.Ignore;
                holder.Add(thumb);
                thumb.View = ph.Crop;
                int pid = ph.Id;
                holder.RegisterCallback<ClickEvent>(_ =>
                {
                    draft.PhotoId = pid;
                    Sfx.Play(SfxId.Click, 0.6f);
                    Render(true);
                });
            }

            // Preview + print.
            Ui.Text("PREVIEW", "le-os-label", right);
            _preview = new DataPageView(S) { Data = draft };
            right.Add(_preview);
            _preview.Refresh();
            var printRow = Ui.El("le-row", right);
            Ui.Btn($"PRINT  ({Labels.Money(Balance.PrintCost)})", () =>
            {
                if (S.PrintDraft(order.Id) != null) Sfx.Play(SfxId.Key);
            }, "le-os-btn le-os-btn--primary", printRow);
            _printerLine = Ui.Text(PrinterLine(), "le-os-muted", right);
            Ui.Text("Nothing is checked for you. Read it over before you print.", "le-os-muted", right);
        }

        static void Section(VisualElement parent, string title) => Ui.Text(title, "le-os-h2", parent);

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
            Ui.Text("Your name on the street: " + Standing.Name(S.Standing.Tier), "le-os-muted", _main);
            var cols = Ui.El("le-os-cols", _main);
            var list = new ScrollView();
            list.AddToClassList("le-os-list");
            cols.Add(list);
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
            var feed = new ScrollView();
            Ui.AddClasses(feed, "le-os-list le-os-list--news");
            cols.Add(feed);
            var watch = Ui.El("le-os-reader", cols);

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
            foreach (var h in new[] { "COUNTRY", "RISK", "DEMAND", "JOBS" }) Ui.Text(h, "le-os-cell", head);
            foreach (var c in S.CountryList)
            {
                if (!c.Owned && S.Standing.Reputation < c.Spec.UnlockReputation) continue;
                var row = Ui.El("le-os-row", watch);
                Ui.Text(c.Spec.Name.ToUpperInvariant() + (c.Owned ? "" : " (no handbook)"), "le-os-cell", row);
                var lvl = c.RiskLevel(S.Day);
                var risk = Ui.Text(lvl.ToString().ToUpperInvariant(), "le-os-cell", row);
                risk.style.color = Looks.Of(lvl);
                float demand = c.Demand(S.Day);
                Ui.Text(demand > 1.01f ? "HIGH" : "normal", "le-os-cell", row);
                if (c.Owned)
                {
                    string cid = c.Id;
                    var toggle = Ui.Btn(c.AcceptingJobs ? "TAKING JOBS" : "NOT TAKING", () =>
                    {
                        S.SetAcceptingJobs(cid, !S.Country(cid).AcceptingJobs);
                        Render(true);
                    }, "le-os-btn le-os-btn--small" + (c.AcceptingJobs ? "" : " le-os-btn--off"), row);
                }
                else Ui.Text("-", "le-os-cell", row);
            }
            Ui.Text("Turning a country off means fewer of those customers walk in.", "le-os-muted", watch);
        }

        // ================================================================== SHOP

        void RenderShop()
        {
            Ui.Text("SHOP", "le-os-h1", _main);
            Ui.Text("Cash: " + Labels.Money(S.Ledger.Cash) + (S.SaleActive ? "    SALE: 25% OFF EQUIPMENT" : ""), "le-os-text", _main);
            var scroll = new ScrollView();
            Ui.AddClasses(scroll, "le-os-form le-os-form--full");
            _main.Add(scroll);

            Section(scroll, "EQUIPMENT");
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                var current = S.Equipment[slot];
                var next = EquipmentCatalog.All.Find(e => e.Slot == slot && e.Tier == current.Tier + 1);
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
                string id = next.Id;
                var b = Ui.Btn("BUY " + Labels.Money(price), () =>
                {
                    if (!S.BuyEquipment(id)) Game.Toasts.Show("Can't afford it.", "bad");
                    Render(true);
                }, "le-os-btn", row);
                b.SetEnabled(S.Ledger.Cash >= price);
            }

            Section(scroll, "HANDBOOK SECTIONS");
            foreach (var c in S.CountryList)
            {
                if (c.Owned && !c.HandbookOutdated) continue;
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
                    b.SetEnabled(S.Ledger.Cash >= Balance.HandbookUpdateCost);
                    continue;
                }
                bool unlocked = S.Standing.Reputation >= c.Spec.UnlockReputation;
                Ui.Text($"{c.Spec.Name.ToUpperInvariant()}  (tier {c.Spec.Tier})", "le-os-text le-os-strong", col);
                Ui.Text(unlocked ? $"Customers are asking. Base price around {Labels.Money(c.Spec.BasePassportPrice)}." : "Nobody asks for this yet. Build your name.", "le-os-muted", col);
                if (unlocked)
                {
                    var b = Ui.Btn("BUY " + Labels.Money(c.Spec.HandbookPrice), () =>
                    {
                        if (!S.BuyHandbook(cid)) Game.Toasts.Show("Can't afford it.", "bad");
                        Render(true);
                    }, "le-os-btn", row);
                    b.SetEnabled(S.Ledger.Cash >= c.Spec.HandbookPrice);
                }
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

        static void DrawScanlines(Painter2D p, Rect r)
        {
            var c = new Color(0f, 0f, 0f, 0.13f);
            for (float y = 0f; y < r.height; y += 4f) Draw.Rect(p, new Rect(0f, y, r.width, 1.5f), c);
        }
    }
}
