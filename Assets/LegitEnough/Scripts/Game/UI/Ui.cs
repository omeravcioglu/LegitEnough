using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// Tiny element factory. All look-and-feel lives in Resources/LegitEnough/UI/Main.uss;
    /// code only builds structure, assigns classes and sets positions.
    /// </summary>
    public static class Ui
    {
        public static VisualElement El(string classes = null, VisualElement parent = null)
        {
            var e = new VisualElement();
            AddClasses(e, classes);
            parent?.Add(e);
            return e;
        }

        /// <summary>Decorative element that never eats clicks.</summary>
        public static VisualElement Deco(string classes, VisualElement parent)
        {
            var e = El(classes, parent);
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        public static Label Text(string text, string classes = null, VisualElement parent = null)
        {
            var l = new Label(text);
            AddClasses(l, classes);
            parent?.Add(l);
            return l;
        }

        public static Button Btn(string text, Action onClick, string classes = "le-btn", VisualElement parent = null)
        {
            var b = new Button(() =>
            {
                Sfx.Play(SfxId.Click);
                onClick?.Invoke();
            }) { text = text };
            // Mouse-driven game: buttons never hold keyboard focus, so SPACE/ENTER can't re-trigger them.
            b.focusable = false;
            b.AddToClassList("le-btn");
            AddClasses(b, classes);
            parent?.Add(b);
            return b;
        }

        public static void AddClasses(VisualElement e, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (var c in classes.Split(' '))
                if (c.Length > 0) e.AddToClassList(c);
        }

        public static T At<T>(this T e, float x, float y, float w = -1f, float h = -1f) where T : VisualElement
        {
            e.style.position = Position.Absolute;
            e.style.left = x;
            e.style.top = y;
            if (w >= 0f) e.style.width = w;
            if (h >= 0f) e.style.height = h;
            return e;
        }

        public static T Show<T>(this T e, bool visible) where T : VisualElement
        {
            e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            return e;
        }

        public static bool IsShown(this VisualElement e) => e.resolvedStyle.display != DisplayStyle.None;

        public static T Bg<T>(this T e, Color c) where T : VisualElement
        {
            e.style.backgroundColor = c;
            return e;
        }

        public static T Toggle<T>(this T e, string cls, bool on) where T : VisualElement
        {
            e.EnableInClassList(cls, on);
            return e;
        }

        public static void SetTextIfChanged(this Label l, string text)
        {
            if (l.text != text) l.text = text;
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }
    }
}
