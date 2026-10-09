using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>An element that draws itself with a Painter2D callback (placeholder art props).</summary>
    public sealed class Painted : VisualElement
    {
        Action<Painter2D, Rect> _draw;

        public Painted(Action<Painter2D, Rect> draw, string classes = null)
        {
            _draw = draw;
            Ui.AddClasses(this, classes);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += ctx =>
            {
                var r = contentRect;
                if (r.width <= 0f || r.height <= 0f) return;
                _draw?.Invoke(ctx.painter2D, r);
            };
        }

        public void SetDrawer(Action<Painter2D, Rect> draw)
        {
            _draw = draw;
            MarkDirtyRepaint();
        }
    }
}
