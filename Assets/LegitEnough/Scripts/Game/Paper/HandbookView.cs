using System.Collections.Generic;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// The country handbook binder (design doc §13). Pages are generated from the rules
    /// the player owns — which can be out of date after a rule change.
    /// </summary>
    public sealed class HandbookView : Overlay
    {
        /// <summary>The demo's extra tab: visas, a full-game feature.</summary>
        public const string VisasTab = "VISAS";

        readonly VisualElement _left;
        readonly VisualElement _right;
        readonly VisualElement _tabs;
        string _selected;

        public HandbookView(GameRoot game, string countryId) : base(game, "le-overlay--dim")
        {
            Root.RegisterCallback<ClickEvent>(_ => Close());
            var binder = Ui.El("le-binder", Root).At(110, 50, 1460, 980);
            binder.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            _left = Ui.El("le-binder__page le-binder__page--left", binder);
            var rings = Ui.Deco("le-binder__rings", binder);
            for (int i = 0; i < 3; i++) Ui.Deco("le-binder__ring", rings);
            _right = Ui.El("le-binder__page le-binder__page--right", binder);
            // The column hugs its tabs (its backing fills the gaps between them) and squeezes past 960px.
            _tabs = Ui.El("le-binder__tabs", Root).At(1570, 70, 340);
            _tabs.style.maxHeight = 960;
            // The demo adds FULL GAME stamps and a VISAS tab: tighter tabs keep all twelve in the column.
            _tabs.EnableInClassList("le-binder__tabs--demo", S.IsDemo);
            _tabs.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            Ui.Text("H / ESC to close", "le-binder__hint", Root).At(150, 1036, 600, 30).pickingMode = PickingMode.Ignore;

            _selected = countryId;
            if (_selected == null)
            {
                // Open on the job at the counter if there is one.
                var v = S.CounterVisit ?? S.StoolVisit;
                _selected = v?.Request?.CountryId ?? v?.Order?.CountryId ?? "BASICS";
            }
            Rebuild();
        }

        void Rebuild()
        {
            _tabs.Clear();
            AddTab("BASICS", "BASICS", true, null);
            foreach (var c in S.CountryList)
            {
                if (c.FullGameOnly)
                {
                    AddTab(c.Id, c.Spec.Name.ToUpperInvariant(), false, null, c.Spec.Name + " is");
                    continue;
                }
                bool unlocked = c.Requestable(S.Standing.Reputation);
                // The demo says what a closed section takes; "locked" belongs to the FULL GAME stamp.
                string state = c.Owned ? (c.HandbookOutdated ? "OUTDATED" : null)
                    : unlocked ? $"BUY {Labels.Money(c.Spec.HandbookPrice)}"
                    : S.IsDemo ? $"NEEDS {c.Spec.UnlockReputation} REP" : "LOCKED";
                AddTab(c.Id, c.Spec.Name.ToUpperInvariant(), c.Owned, state);
            }
            if (S.IsDemo) AddTab(VisasTab, "VISAS", false, null, "Visas are");

            _left.Clear();
            _right.Clear();
            if (S.IsDemo && _selected == VisasTab)
            {
                RenderFullGame("VISAS", FullGameLocks.Padlock(6, "le-hb-lock__art"),
                    "Some trips need more than a passport. Different paper, different rules.",
                    "Visa work is in the full game, with all ten countries and the top-of-the-line kit.");
                RenderFullGameList();
                return;
            }
            if (_selected == "BASICS" || S.Country(_selected) == null)
            {
                RenderPage(_left, Handbook.General());
                var tips = new HandbookPage { Title = "SCRIBBLED ON THE BACK" };
                tips.Note("Customers lie about their height. Write what they say.");
                tips.Note("Dates come in every format under the sun. The handbook says which one to use. Use that one.");
                tips.Note("The cheap camera fires a beat AFTER you press. People blink. Wait for it.");
                tips.Note("Glasses off? Look again. Some people carry backups.");
                tips.Note("Nobody tells you if it worked. Not right away.");
                RenderPage(_right, tips);
                return;
            }

            var state2 = S.Country(_selected);
            if (state2.FullGameOnly)
            {
                var spec = state2.Spec;
                RenderFullGame(spec.Name.ToUpperInvariant(), FullGameLocks.Flag(spec.Id, 6, "le-hb-lock__art"), Scribble(spec),
                    $"{spec.Adjective} jobs are in the full game, with all ten countries, visas and the top-of-the-line kit.");
                // The facing page is the real photo page with its answers blacked out.
                RenderPage(_right, Handbook.Photo(spec), true);
                Ui.Text("– Sealed. These rules come with the full game.", "le-hb__note", _right);
                var stamp = FullGameLocks.Badge(4, "le-hb-teaser__stamp");
                stamp.pickingMode = PickingMode.Ignore;
                _right.Add(stamp);
                return;
            }
            if (!state2.Owned)
            {
                var locked = new HandbookPage { Title = state2.Spec.Name.ToUpperInvariant() };
                bool unlocked = state2.Requestable(S.Standing.Reputation);
                if (!S.IsDemo)
                    locked.Note(unlocked
                        ? $"Section not purchased. Buy it in the computer's SHOP ({Labels.Money(state2.Spec.HandbookPrice)}) to take {state2.Spec.Adjective} jobs."
                        : "Nobody's asking for this one yet. Build your reputation.");
                // Demo: only promise clients who are really coming.
                else if (!unlocked)
                    locked.Note($"Nobody's asking for this one yet. It opens at reputation {state2.Spec.UnlockReputation}.");
                else if (FullGameLocks.ClientBooked(S, state2.Id))
                    locked.Note($"Section not purchased. Click the COMPUTER, then SHOP: it's {Labels.Money(state2.Spec.HandbookPrice)}. Then you can take {state2.Spec.Adjective} jobs.");
                else
                    locked.Note($"Section not purchased ({Labels.Money(state2.Spec.HandbookPrice)} in the computer's SHOP). No {state2.Spec.Adjective} clients are booked in the demo; buy it to read the rules anyway.");
                RenderPage(_left, locked);
                if (S.IsDemo)
                {
                    // What the section holds, blacked out until it's bought.
                    RenderPage(_right, Handbook.Photo(state2.Spec), true);
                    Ui.Text(unlocked ? "– Buy the section to read it." : "– Not for sale yet.", "le-hb__note", _right);
                }
                return;
            }

            var rules = S.HandbookRules(_selected);
            RenderPage(_left, Handbook.Passport(rules));
            var photo = Handbook.Photo(rules);
            RenderPage(_right, photo);
            _right.Add(CoverageDiagram(rules.Photo));
            RenderPage(_right, Handbook.Notes(rules));
            if (state2.HandbookOutdated)
            {
                var sticky = Ui.El("le-sticky le-sticky--alert", _left);
                Ui.Text($"OUTDATED! {state2.Spec.Name} changed its rules on day {state2.LastRuleChangeDay}. Click the COMPUTER, then SHOP: the update is {Labels.Money(Balance.HandbookUpdateCost)}.", "le-sticky__text", sticky);
            }
        }

        /// <summary>
        /// One binder tab, named "handbook-tab-{id}" for the tutorial. <paramref name="fullGame"/>
        /// ("Japan is") marks a demo tab for something only the full game has: a greyed, padlocked
        /// flag and the FULL GAME stamp on the state line. The label stays the plain name.
        /// </summary>
        void AddTab(string id, string label, bool owned, string state, string fullGame = null)
        {
            var tab = Ui.El("le-tab le-clickable", _tabs);
            tab.name = "handbook-tab-" + id;
            tab.EnableInClassList("le-tab--active", id == _selected);
            tab.EnableInClassList("le-tab--locked", !owned);
            tab.EnableInClassList("le-tab--fullgame", fullGame != null);
            if (fullGame != null)
                tab.Add(id == VisasTab ? FullGameLocks.Padlock(3, "le-tab__lock") : FullGameLocks.Flag(id, 3, "le-tab__flag le-tab__flag--locked"));
            else if (id != "BASICS")
            {
                var flag = UiArt.Image(UiArt.Flag(id), 3, "le-tab__flag");
                flag.pickingMode = PickingMode.Ignore;
                tab.Add(flag);
            }
            var text = Ui.El("le-tab__text", tab);
            text.pickingMode = PickingMode.Ignore;
            Ui.Text(label, "le-tab__label", text).pickingMode = PickingMode.Ignore;
            if (fullGame != null) text.Add(FullGameLocks.Badge(2, "le-tab__badge"));
            else if (state != null) Ui.Text(state, "le-tab__state", text).pickingMode = PickingMode.Ignore;
            tab.RegisterCallback<ClickEvent>(e =>
            {
                e.StopPropagation();
                _selected = id;
                Sfx.Play(SfxId.Paper, 0.5f, 1.3f);
                if (fullGame != null) FullGameLocks.Nudge(Game, fullGame);
                Rebuild();
            });
        }

        /// <summary>The page behind a FULL GAME tab: what it is, a scribble, and where to get it.</summary>
        void RenderFullGame(string title, VisualElement art, string scribble, string pitch)
        {
            Ui.Text(title, "le-hb__title", _left);
            var head = Ui.El("le-hb-lock", _left);
            head.Add(art);
            head.Add(FullGameLocks.Badge(3, "le-hb-lock__badge"));
            if (scribble != null) Ui.Text("– " + scribble, "le-hb__note", _left);
            Ui.Text(pitch, "le-hb-lock__pitch", _left);
            Ui.Btn("WISHLIST ON STEAM", StoreLinks.OpenWishlist, "le-btn--primary le-hb-lock__wishlist", _left).name = "handbook-wishlist";
        }

        /// <summary>The right-hand page behind the VISAS tab: what the full game adds, from the game's own data.</summary>
        void RenderFullGameList()
        {
            var names = new List<string>();
            var flags = Ui.El("le-row le-row--wrap le-hb-flags");
            foreach (var c in S.CountryList)
            {
                if (!c.FullGameOnly) continue;
                names.Add(c.Spec.Name);
                flags.Add(FullGameLocks.Flag(c.Id, 3, "le-hb-flags__flag"));
            }
            var kit = new List<string>();
            foreach (var e in EquipmentCatalog.All)
                if (S.EquipmentLocked(e)) kit.Add(e.Name);
            var page = new HandbookPage { Title = "IN THE FULL GAME" };
            if (names.Count > 1)
                page.Add("COUNTRIES", "All ten, adding " + string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1] + ".");
            page.Add("VISAS", "On top of passports. More paper, more rules, more money.");
            if (kit.Count > 0) page.Add("KIT", string.Join(", ", kit) + ".");
            page.Add("CLIENTS", "More stories, regulars and trouble at the door.");
            page.Add("DAYS", "No two-day limit. The shop stays open.");
            page.Note("The good money is in the paperwork you can't do yet.");
            RenderPage(_right, page);
            if (flags.childCount > 0) _right.Add(flags);
        }

        /// <summary>R.'s note on a full-game country: the twist that makes it worth the wait.</summary>
        static string Scribble(CountrySpec spec) => spec.Id switch
        {
            Countries.UnitedKingdom => "Same gray as France. Different everything else.",
            Countries.Japan => "Year first, dots in between. No notes to lean on.",
            Countries.SouthKorea => "Sixty-four pages. Dates like 14 MAR 1987. No place of birth.",
            Countries.Emirates => "Red backdrop. Headwear is fine. Five years, not ten.",
            Countries.Iran => "Brown cover, five-year passports. The best money in town, and the most trouble.",
            _ => spec.Notes != null && spec.Notes.Length > 0 ? spec.Notes[0] : null,
        };

        /// <summary>
        /// Prints a handbook page. <paramref name="redacted"/> keeps the labels but blacks out every
        /// answer (bars sized to the hidden text) and leaves out the margin notes.
        /// </summary>
        static void RenderPage(VisualElement page, HandbookPage content, bool redacted = false)
        {
            if (!string.IsNullOrEmpty(content.Title)) Ui.Text(content.Title, "le-hb__title", page);
            foreach (var line in content.Lines)
            {
                if (line.Handwritten)
                {
                    if (!redacted) Ui.Text("– " + line.Value, "le-hb__note", page);
                    continue;
                }
                if (redacted && line.Label.Length == 0) continue;
                var row = Ui.El("le-hb__row", page);
                if (line.Label.Length > 0) Ui.Text(line.Label, "le-hb__label", row);
                if (redacted)
                {
                    var bar = Ui.El("le-hb__redact", row);
                    bar.style.width = Mathf.Clamp(line.Value.Length * 12, 72, 330);
                    continue;
                }
                Ui.Text(line.Value, line.Label.Length > 0 ? "le-hb__value" : "le-hb__value le-hb__value--wide", row);
            }
        }

        // The coverage diagram: a small copy of the CROP screen's guides, so the binder and the
        // crop ruler read the same way (Sim Framing: ruler zero on the dashed TOP OF HEAD line).
        // The page squeezes it, so everything is laid out from the height it actually gets.
        const float DiagramFrameTop = 4f;
        const float DiagramFrameX = 30f;
        const float DiagramNoseRoom = 28f;
        static readonly Color DiagramInk = new Color(0.2f, 0.18f, 0.16f);
        static readonly Color DiagramRed = new Color(0.65f, 0.15f, 0.12f);

        static Rect DiagramFrame(Rect r)
        {
            float h = Mathf.Max(60f, r.height - DiagramFrameTop - DiagramNoseRoom);
            return new Rect(DiagramFrameX, DiagramFrameTop, h * 35f / 45f, h);
        }

        static float DiagramRulerX(Rect frame) => frame.xMax + 16f;

        static VisualElement CoverageDiagram(PhotoRules rules)
        {
            var d = new Painted((p, r) => DrawCoverageDiagram(p, r, rules), "le-coverage-diagram");
            d.style.marginTop = 4f;
            d.style.marginBottom = 8f;
            var top = DiagramLabel(d, "TOP OF HEAD", null);
            var chin = DiagramLabel(d, $"CHIN {Labels.Percent(rules.CoverageMin)} - {Labels.Percent(rules.CoverageMax)}", DiagramRed);
            var nose = DiagramLabel(d, "NOSE", null);
            nose.style.unityTextAlign = TextAnchor.MiddleCenter;
            d.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var frame = DiagramFrame(d.contentRect);
                var crop = new LegitEnough.Sim.Box(0f, frame.y, 1f, frame.height);
                float x = DiagramRulerX(frame) + 22f;
                Place(top, x, Framing.GuideY(crop));
                Place(chin, x, Framing.RulerY(crop, (rules.CoverageMin + rules.CoverageMax) * 0.5f));
                Place(nose, frame.x, frame.yMax + 14f);
                nose.style.width = frame.width;
            });
            return d;
        }

        static void Place(Label l, float x, float centerY)
        {
            l.style.left = x;
            l.style.top = centerY - 12f;
        }

        static Label DiagramLabel(VisualElement parent, string text, Color? color)
        {
            var l = Ui.Text(text, "le-hb__label", parent);
            l.pickingMode = PickingMode.Ignore;
            l.style.position = Position.Absolute;
            l.style.height = 24f;
            l.style.width = StyleKeyword.Auto;
            l.style.marginLeft = 0f; l.style.marginTop = 0f; l.style.marginRight = 0f; l.style.marginBottom = 0f;
            l.style.paddingLeft = 0f; l.style.paddingTop = 0f; l.style.paddingRight = 0f; l.style.paddingBottom = 0f;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            if (color.HasValue) l.style.color = color.Value;
            return l;
        }

        static void DrawCoverageDiagram(Painter2D p, Rect r, PhotoRules rules)
        {
            var frame = DiagramFrame(r);
            float h = frame.height;
            var crop = new LegitEnough.Sim.Box(0f, frame.y, 1f, h);
            Draw.Rect(p, frame, Looks.Of(rules.Backdrop));

            // A head with its crown (top of the hair) on the line and its chin mid-band.
            float crown = Framing.GuideY(crop);
            float chin = Framing.RulerY(crop, (rules.CoverageMin + rules.CoverageMax) * 0.5f);
            float headH = chin - crown;
            float cx = frame.center.x;
            var body = new Color(0.45f, 0.42f, 0.4f);
            Draw.Rect(p, new Rect(cx - frame.width * 0.4f, chin - headH * 0.06f, frame.width * 0.8f, frame.yMax - chin + headH * 0.06f), body);
            Draw.Ellipse(p, new Vector2(cx, crown + headH * 0.5f), headH * 0.34f, headH * 0.5f, new Color(0.25f, 0.2f, 0.17f));
            Draw.Ellipse(p, new Vector2(cx, crown + headH * 0.56f), headH * 0.3f, headH * 0.44f, new Color(0.55f, 0.5f, 0.45f));
            Draw.RoundRectStroke(p, frame, 2f, DiagramInk, 2f);

            // Guides: dashed TOP OF HEAD line (run on to the ruler) and the dashed NOSE line.
            float rx = DiagramRulerX(frame);
            Dashed(p, new Vector2(frame.x + 2f, crown), new Vector2(rx + 16f, crown), DiagramInk);
            Dashed(p, new Vector2(cx, frame.y + 2f), new Vector2(cx, frame.yMax - 2f), DiagramInk);

            // Ruler: zero on the line, a tick every 5%, longer every 10%; the chin band shaded.
            float bandTop = Framing.RulerY(crop, rules.CoverageMin);
            float bandBottom = Framing.RulerY(crop, rules.CoverageMax);
            Draw.Rect(p, new Rect(rx - 6f, bandTop, 22f, bandBottom - bandTop), Draw.Alpha(DiagramRed, 0.35f));
            Draw.Line(p, new Vector2(rx, crown), new Vector2(rx, frame.yMax), DiagramInk, 2f);
            for (int i = 0; ; i++)
            {
                float y = Framing.RulerY(crop, i * 0.05f);
                if (y > frame.yMax + 0.5f) break;
                Draw.Line(p, new Vector2(rx, y), new Vector2(rx + (i % 2 == 0 ? 12f : 6f), y), DiagramInk, 2f);
            }
            Draw.Line(p, new Vector2(rx - 6f, bandTop), new Vector2(rx + 16f, bandTop), DiagramRed, 2f);
            Draw.Line(p, new Vector2(rx - 6f, bandBottom), new Vector2(rx + 16f, bandBottom), DiagramRed, 2f);
        }

        static void Dashed(Painter2D p, Vector2 a, Vector2 b, Color c)
        {
            float len = Vector2.Distance(a, b);
            if (len <= 0f) return;
            var dir = (b - a) / len;
            for (float t = 0f; t < len; t += 10f)
                Draw.Line(p, a + dir * t, a + dir * Mathf.Min(len, t + 6f), c, 2f);
        }

        public override void Tick(float dt)
        {
            if (Root.panel == null) return;
        }
    }
}
