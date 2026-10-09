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

        public Toasts(VisualElement parent)
        {
            _layer = Ui.El("le-toasts", parent);
            _layer.pickingMode = PickingMode.Ignore;
        }

        public void Show(string text, string kind = "")
        {
            if (string.IsNullOrEmpty(text)) return;
            var l = Ui.Text(text, "le-toast" + (kind.Length > 0 ? " le-toast--" + kind : ""), _layer);
            l.pickingMode = PickingMode.Ignore;
            _items.Add(new Item { Label = l, Life = 3.2f });
            while (_items.Count > 5)
            {
                _items[0].Label.RemoveFromHierarchy();
                _items.RemoveAt(0);
            }
        }

        public void Tick(float dt)
        {
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
