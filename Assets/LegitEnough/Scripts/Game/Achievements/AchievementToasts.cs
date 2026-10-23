using System.Collections.Generic;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// The unlock banner: one at a time, top centre over the shop or compact at the bottom over the
    /// computer, held back while any other screen is up (camera, review, binder, cards, menus) and
    /// dropped when a menu screen opens (they stay NEW on the ACHIEVEMENTS screen). It steps aside
    /// for R.'s note, its pointer, the hint line, speech bubbles and the toasts, picking its spot when
    /// it appears and moving only if something comes to cover it. During the demo's guided Day 1 it
    /// is quiet: smaller, shorter, and the soft chime instead of the stamp.
    /// Batch runs show none (trailer and store-shot footage) unless the smoke test asks.
    /// </summary>
    public sealed class AchievementToasts
    {
        /// <summary>Only the smoke test sets this: banners in batch mode.</summary>
        public static bool ShowInBatch;

        const float Life = 4.0f, QuietLife = 2.6f, FadeTime = 0.5f, GapTime = 0.4f;
        const float ShopX = 680f, ShopY = 16f, ShopW = 560f;
        const float LowW = 360f, LowMargin = 16f, LowBottom = 8f;

        enum Spot { Hold, Shop, Low }

        readonly GameRoot _game;
        readonly VisualElement _layer;
        readonly Queue<AchievementUnlock> _queue = new Queue<AchievementUnlock>();
        readonly List<Rect> _avoid = new List<Rect>();

        VisualElement _item;
        AchievementUnlock _current;
        bool _quiet;
        Spot _builtFor;
        /// <summary>The spot's second choice (a step lower / bottom-right) is in use; _placed: chosen.</summary>
        bool _alt, _placed;
        float _life, _gap;

        public VisualElement Layer => _layer;
        /// <summary>The banner on screen (or held), if any.</summary>
        public AchievementDef Current => _item != null ? _current.Def : null;
        public int Queued => _queue.Count;

        public AchievementToasts(GameRoot game, VisualElement stage)
        {
            _game = game;
            _layer = Ui.El("le-achtoasts", stage);
            _layer.name = "ach-toasts";
            _layer.pickingMode = PickingMode.Ignore;
            _layer.style.position = Position.Absolute;
            _layer.style.visibility = Visibility.Hidden;
        }

        bool Allowed => !Application.isBatchMode || ShowInBatch;

        public void Enqueue(AchievementUnlock u)
        {
            if (u.Def == null || !Allowed || _game.MenuUp) return;
            _queue.Enqueue(u);
        }

        public void Clear()
        {
            _queue.Clear();
            Remove();
            _gap = 0f;
        }

        public void Tick(float dt)
        {
            if (!Allowed || _game.MenuUp)
            {
                // A menu screen took over: what's still queued waits on the ACHIEVEMENTS screen as NEW.
                Clear();
                return;
            }
            var spot = Where();
            if (spot == Spot.Hold)
            {
                _layer.style.visibility = Visibility.Hidden;
                return;
            }
            if (_item == null)
            {
                if (_gap > 0f)
                {
                    _gap -= dt;
                    return;
                }
                if (_queue.Count == 0) return;
                _current = _queue.Dequeue();
                // Quiet is for the guided Day 1 only: Day 2's notes don't make SAMIR_MADE_IT a whisper.
                _quiet = TutorialCoach.IsActiveFor(_game) && _game.Session?.Day == 1;
                _life = _quiet ? QuietLife : Life;
                Build(spot);
                if (_quiet) Sfx.Play(SfxId.Notify, 0.3f);
                else Sfx.Play(SfxId.Stamp, 0.6f);
            }
            else if (spot != _builtFor)
            {
                Build(spot);
            }
            Place(spot);
            _layer.style.visibility = Visibility.Visible;
            _life -= dt;
            _item.style.opacity = _life < FadeTime ? Mathf.Max(0f, _life / FadeTime) : 1f;
            if (_life <= 0f)
            {
                Remove();
                _gap = GapTime;
            }
        }

        /// <summary>The shop floor (nothing open) or the computer; anything else holds the banner back.</summary>
        Spot Where()
        {
            // The day is over: the report is about to take the stage.
            if (_game.Session != null && _game.Session.Phase == DayPhase.Closed) return Spot.Hold;
            var top = _game.Top;
            if (top == null) return Spot.Shop;
            return top is ComputerView ? Spot.Low : Spot.Hold;
        }

        void Remove()
        {
            _item?.RemoveFromHierarchy();
            _item = null;
            _layer.style.visibility = Visibility.Hidden;
        }

        void Build(Spot spot)
        {
            _item?.RemoveFromHierarchy();
            _builtFor = spot;
            _placed = false;
            var d = _current.Def;
            bool compact = spot == Spot.Low;
            string cls = "le-achtoast";
            if (_quiet) cls += " le-achtoast--quiet";
            if (compact) cls += " le-achtoast--compact";
            if (_current.Banked) cls += " le-achtoast--banked";
            _item = Ui.El(cls, _layer);
            _item.name = "ach-toast-" + d.Id;
            _item.pickingMode = PickingMode.Ignore;
            _layer.EnableInClassList("le-achtoasts--low", compact);
            // The quiet banner holds just the badge and the name: sized to them, still centred.
            _layer.EnableInClassList("le-achtoasts--fit", _quiet && !compact);
            _layer.style.width = compact ? LowW : ShopW;

            var badge = UiArt.Image(AchievementArt.Badge(d, BadgeState.Achieved), _quiet || compact ? 2 : 3, "le-achtoast__badge");
            _item.Add(badge);
            var words = Ui.El("le-achtoast__words", _item);
            words.pickingMode = PickingMode.Ignore;
            if (!_quiet && !compact)
            {
                string head = _current.Banked ? "EARNED FOR THE FULL GAME" : d.Hidden ? "SECRET ACHIEVEMENT" : "ACHIEVEMENT";
                Ui.Text(head, "le-achtoast__head", words).pickingMode = PickingMode.Ignore;
            }
            var row = Ui.El("le-achtoast__row", words);
            row.pickingMode = PickingMode.Ignore;
            Ui.Text(d.Name, "le-achtoast__name", row).pickingMode = PickingMode.Ignore;
            if (_current.Banked) row.Add(UiArt.Image(DemoArt.FullGameBadge(), 2, "le-achtoast__fullgame"));
        }

        /// <summary>
        /// Top centre over the shop (else a step lower), bottom-left over the computer (else
        /// bottom-right): whichever covers the least of R.'s note, its pointer, the hint line, the
        /// speech bubbles and the toasts. Chosen once the banner has its size; after that it only
        /// moves when something covers it and the other spot is clearer.
        /// </summary>
        void Place(Spot spot)
        {
            float h = _item.resolvedStyle.height, iw = _item.resolvedStyle.width;
            bool measured = !float.IsNaN(h) && h >= 1f;
            if (!measured) h = spot == Spot.Low ? 72f : 128f;
            CollectAvoid();
            Vector2 a, b;
            float w, dx = 0f;
            if (spot == Spot.Low)
            {
                w = LowW;
                float y = GameRoot.StageHeight - LowBottom - h;
                a = new Vector2(LowMargin, y);
                b = new Vector2(GameRoot.StageWidth - LowMargin - LowW, y);
            }
            else
            {
                w = ShopW;
                a = new Vector2(ShopX, ShopY);
                b = new Vector2(ShopX, ShopY + 136f);
                // A banner narrower than the layer sits in its middle: weigh what it really covers.
                if (!float.IsNaN(iw) && iw >= 1f && iw < ShopW) dx = (ShopW - iw) * 0.5f;
            }
            var size = new Vector2(w - 2f * dx, h);
            float ca = Cost(new Rect(a + new Vector2(dx, 0f), size)), cb = Cost(new Rect(b + new Vector2(dx, 0f), size));
            if (!_placed)
            {
                _alt = cb < ca;
                _placed = measured;
            }
            else
            {
                float here = _alt ? cb : ca, there = _alt ? ca : cb;
                if (here > 0f && there < here) _alt = !_alt;
            }
            var p = _alt ? b : a;
            _layer.style.left = p.x;
            _layer.style.top = p.y;
        }

        float Cost(Rect r)
        {
            float c = 0f;
            foreach (var o in _avoid)
                if (r.Overlaps(o))
                {
                    float w = Mathf.Min(r.xMax, o.xMax) - Mathf.Max(r.xMin, o.xMin);
                    float h = Mathf.Min(r.yMax, o.yMax) - Mathf.Max(r.yMin, o.yMin);
                    c += Mathf.Max(0f, w) * Mathf.Max(0f, h);
                }
            return c;
        }

        void CollectAvoid()
        {
            _avoid.Clear();
            var stage = _game.Stage;
            if (stage == null) return;
            var origin = stage.worldBound.position;
            void Add(VisualElement e)
            {
                if (e == null || !Shown(e)) return;
                var wb = e.worldBound;
                if (float.IsNaN(wb.width) || wb.width < 1f || wb.height < 1f) return;
                _avoid.Add(new Rect(wb.position - origin, wb.size));
            }
            var coach = _game.Coach?.Root;
            if (coach != null)
            {
                Add(coach.Q(className: "le-coach__note"));
                Add(coach.Q(className: "le-coach__pointer"));
                Add(coach.Q(className: "le-coach__ring"));
            }
            stage.Query(className: "le-hint").ForEach(Add);
            stage.Query(className: "le-bubble").ForEach(Add);
            stage.Query(className: "le-toast").ForEach(Add);
        }

        static bool Shown(VisualElement e)
        {
            for (var p = e; p != null; p = p.hierarchy.parent)
            {
                var s = p.resolvedStyle;
                if (s.display == DisplayStyle.None || s.visibility == Visibility.Hidden || s.opacity <= 0.01f) return false;
            }
            return true;
        }
    }
}
