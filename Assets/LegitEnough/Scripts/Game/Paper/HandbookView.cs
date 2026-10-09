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
            _tabs = Ui.El("le-binder__tabs", Root).At(1570, 70, 340, 960);
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
                bool unlocked = S.Standing.Reputation >= c.Spec.UnlockReputation;
                string state = c.Owned ? (c.HandbookOutdated ? "OUTDATED" : null) : unlocked ? $"BUY {Labels.Money(c.Spec.HandbookPrice)}" : "LOCKED";
                AddTab(c.Id, c.Spec.Name.ToUpperInvariant(), c.Owned, state);
            }

            _left.Clear();
            _right.Clear();
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
            if (!state2.Owned)
            {
                var locked = new HandbookPage { Title = state2.Spec.Name.ToUpperInvariant() };
                bool unlocked = S.Standing.Reputation >= state2.Spec.UnlockReputation;
                locked.Note(unlocked
                    ? $"Section not purchased. Buy it in the computer's SHOP ({Labels.Money(state2.Spec.HandbookPrice)}) to take {state2.Spec.Adjective} jobs."
                    : "Nobody's asking for this one yet. Build your reputation.");
                RenderPage(_left, locked);
                return;
            }

            var rules = S.HandbookRules(_selected);
            RenderPage(_left, Handbook.Passport(rules));
            var photo = Handbook.Photo(rules);
            RenderPage(_right, photo);
            _right.Add(new Painted((p, r) => DrawCoverageDiagram(p, r, rules.Photo), "le-coverage-diagram"));
            RenderPage(_right, Handbook.Notes(rules));
            if (state2.HandbookOutdated)
            {
                var sticky = Ui.El("le-sticky le-sticky--alert", _left);
                Ui.Text($"OUTDATED! {state2.Spec.Name} changed its rules on day {state2.LastRuleChangeDay}. Buy the update in the SHOP ({Labels.Money(Balance.HandbookUpdateCost)}).", "le-sticky__text", sticky);
            }
        }

        void AddTab(string id, string label, bool owned, string state)
        {
            var tab = Ui.El("le-tab le-clickable", _tabs);
            tab.EnableInClassList("le-tab--active", id == _selected);
            tab.EnableInClassList("le-tab--locked", !owned);
            if (id != "BASICS")
            {
                var flag = UiArt.Image(UiArt.Flag(id), 3, "le-tab__flag");
                flag.pickingMode = PickingMode.Ignore;
                tab.Add(flag);
            }
            var text = Ui.El("le-tab__text", tab);
            text.pickingMode = PickingMode.Ignore;
            Ui.Text(label, "le-tab__label", text).pickingMode = PickingMode.Ignore;
            if (state != null) Ui.Text(state, "le-tab__state", text).pickingMode = PickingMode.Ignore;
            tab.RegisterCallback<ClickEvent>(e =>
            {
                e.StopPropagation();
                _selected = id;
                Sfx.Play(SfxId.Paper, 0.5f, 1.3f);
                Rebuild();
            });
        }

        static void RenderPage(VisualElement page, HandbookPage content)
        {
            if (!string.IsNullOrEmpty(content.Title)) Ui.Text(content.Title, "le-hb__title", page);
            foreach (var line in content.Lines)
            {
                if (line.Handwritten)
                {
                    Ui.Text("– " + line.Value, "le-hb__note", page);
                    continue;
                }
                var row = Ui.El("le-hb__row", page);
                if (line.Label.Length > 0) Ui.Text(line.Label, "le-hb__label", row);
                Ui.Text(line.Value, line.Label.Length > 0 ? "le-hb__value" : "le-hb__value le-hb__value--wide", row);
            }
        }

        static void DrawCoverageDiagram(Painter2D p, Rect r, PhotoRules rules)
        {
            float h = r.height - 20f;
            float w = h * 35f / 45f;
            var frame = new Rect(30f, 10f, w, h);
            Draw.Rect(p, frame, Looks.Of(rules.Backdrop));
            Draw.RoundRectStroke(p, frame, 2f, new Color(0.2f, 0.18f, 0.16f), 2f);
            float mid = (rules.CoverageMin + rules.CoverageMax) * 0.5f;
            float headH = h * mid;
            float top = frame.y + h * 0.1f;
            Draw.Ellipse(p, new Vector2(frame.center.x, top + headH * 0.5f), headH * 0.36f, headH * 0.5f, new Color(0.55f, 0.5f, 0.45f));
            Draw.Rect(p, new Rect(frame.center.x - w * 0.4f, top + headH * 0.95f, w * 0.8f, frame.yMax - top - headH * 0.95f), new Color(0.45f, 0.42f, 0.4f));
            var ink = new Color(0.65f, 0.15f, 0.12f);
            float x = frame.xMax + 16f;
            Draw.Line(p, new Vector2(x, top), new Vector2(x, top + h * rules.CoverageMax), ink, 2f);
            Draw.Line(p, new Vector2(x - 8f, top), new Vector2(x + 8f, top), ink, 2f);
            Draw.Line(p, new Vector2(x - 8f, top + h * rules.CoverageMin), new Vector2(x + 8f, top + h * rules.CoverageMin), ink, 2f);
            Draw.Line(p, new Vector2(x - 8f, top + h * rules.CoverageMax), new Vector2(x + 8f, top + h * rules.CoverageMax), ink, 2f);
        }

        public override void Tick(float dt)
        {
            if (Root.panel == null) return;
        }
    }
}
