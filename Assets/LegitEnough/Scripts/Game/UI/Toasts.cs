using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>Short-lived notifications in the corner ("Page printed", "+$450").</summary>
    public sealed class Toasts
    {
        sealed class Item
        {
            public Label Label;
            public float Life;
        }

        readonly VisualElement _layer;
        readonly List<Item> _items = new List<Item>();

        /// <summary>How many toasts may stack at once (GameRoot lowers it where space is tight).</summary>
        public int MaxVisible = 5;

        public Toasts(VisualElement parent)
        {
            _layer = Ui.El("le-toasts", parent);
            _layer.pickingMode = PickingMode.Ignore;
        }

        public void Show(string text, string kind = "")
        {
            if (string.IsNullOrEmpty(text)) return;
            // The same line again (a locked item clicked twice) refreshes the live toast, no stack.
            foreach (var it in _items)
            {
                if (it.Label.text != text || it.Life <= 0.5f) continue;
                it.Life = 3.2f;
                it.Label.style.opacity = 1f;
                return;
            }
            var l = Ui.Text(text, "le-toast" + (kind.Length > 0 ? " le-toast--" + kind : ""), _layer);
            l.pickingMode = PickingMode.Ignore;
            _items.Add(new Item { Label = l, Life = 3.2f });
            Trim();
        }

        void Trim()
        {
            while (_items.Count > System.Math.Max(1, MaxVisible))
            {
                _items[0].Label.RemoveFromHierarchy();
                _items.RemoveAt(0);
            }
        }

        public void Tick(float dt)
        {
            Trim();
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var it = _items[i];
                it.Life -= dt;
                if (it.Life < 0.5f) it.Label.style.opacity = it.Life / 0.5f;
                if (it.Life <= 0f)
                {
                    it.Label.RemoveFromHierarchy();
                    _items.RemoveAt(i);
                }
            }
        }
    }
}
