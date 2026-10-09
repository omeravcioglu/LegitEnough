using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// A passport data page drawn from a <see cref="DocumentDraft"/>: used live in the editor
    /// preview, on printed pages and inside finished booklets. Deliberately fictional design —
    /// no real security features, just a page that reads as "official-ish".
    /// </summary>
    public sealed class DataPageView : VisualElement
    {
        readonly GameSession _s;
        readonly Painted _pattern;
        readonly Label _header;
        readonly Label _sub;
        readonly Label _category;
        readonly PortraitView _photo;
        readonly VisualElement _noPhoto;
        readonly VisualElement _fields;
        readonly Painted _codeStrip;
        readonly VisualElement _smudge;
        string _key = "";

        public DocumentDraft Data;
        public bool Smudged;

        public DataPageView(GameSession s)
        {
            _s = s;
            AddToClassList("le-datapage");
            pickingMode = PickingMode.Ignore;
            _pattern = new Painted(DrawPattern, "le-datapage__pattern");
            Add(_pattern);
            var head = Ui.El("le-datapage__head", this);
            _header = Ui.Text("", "le-datapage__header", head);
            _sub = Ui.Text("", "le-datapage__sub", head);
            _category = Ui.Text("", "le-datapage__category", head);
            var body = Ui.El("le-datapage__body", this);
            var photoBox = Ui.El("le-datapage__photo", body);
            _photo = new PortraitView("le-datapage__portrait") { ShowGrain = true };
            _photo.pickingMode = PickingMode.Ignore;
            photoBox.Add(_photo);
            _noPhoto = Ui.Text("NO PHOTO", "le-datapage__nophoto", photoBox);
            _fields = Ui.El("le-datapage__fields", body);
            _codeStrip = new Painted(DrawCode, "le-datapage__code");
            Add(_codeStrip);
            _smudge = Ui.Deco("le-datapage__smudge", this);
        }

        /// <summary>Re-read the draft. Cheap when nothing changed.</summary>
        public void Refresh()
        {
            var d = Data;
            if (d == null) return;
            var country = _s.Country(d.CountryId);
            string key = d.CountryId + "|" + d.Category + "|" + d.Cover + "|" + d.Pages + "|" + d.PhotoId + "|" + Smudged;
            foreach (var kv in d.Fields) key += "|" + kv.Key + "=" + kv.Value;
            if (key == _key) return;
            _key = key;

            _header.text = country != null ? country.Spec.DocumentHeader : "- NO TEMPLATE -";
            _sub.text = "PASSPORT  /  " + (country != null ? country.Spec.Name.ToUpperInvariant() : "");
            _category.text = d.Category.HasValue ? Labels.Of(d.Category.Value) : "TYPE ?";
            var tint = d.Cover.HasValue ? Looks.Of(d.Cover.Value) : new Color(0.4f, 0.4f, 0.4f);
            style.backgroundColor = Color.Lerp(new Color(0.95f, 0.93f, 0.86f), tint, 0.12f);
            _pattern.MarkDirtyRepaint();

            var photo = _s.Photos.Get(d.PhotoId);
            _photo.Capture = photo;
            _photo.Show(photo != null);
            _noPhoto.Show(photo == null);
            if (photo != null)
            {
                _photo.View = photo.Crop;
                _photo.Refresh();
            }

            _fields.Clear();
            var spec = country?.Spec.Passport;
            if (spec != null)
            {
                foreach (var f in spec.AllFields()) AddField(Labels.Of(f), d.Get(f));
            }
            else
            {
                foreach (var kv in d.Fields) AddField(Labels.Of(kv.Key), kv.Value);
            }
            _smudge.Show(Smudged);
            _codeStrip.MarkDirtyRepaint();
        }

        void AddField(string label, string value)
        {
            var row = Ui.El("le-datapage__field", _fields);
            Ui.Text(label, "le-datapage__label", row);
            Ui.Text(string.IsNullOrEmpty(value) ? " " : Labels.Normalize(value), "le-datapage__value", row);
        }

        void DrawPattern(Painter2D p, Rect r)
        {
            var tint = Data?.Cover != null ? Looks.Of(Data.Cover.Value) : new Color(0.4f, 0.4f, 0.4f);
            var c = new Color(tint.r, tint.g, tint.b, 0.12f);
            for (int k = 0; k < 7; k++)
            {
                p.BeginPath();
                float baseY = r.height * (0.15f + k * 0.12f);
                p.MoveTo(new Vector2(0f, baseY));
                for (int i = 1; i <= 40; i++)
                {
                    float x = r.width * i / 40f;
                    float y = baseY + Mathf.Sin(i * 0.6f + k) * 10f + Mathf.Sin(i * 0.23f + k * 2f) * 6f;
                    p.LineTo(new Vector2(x, y));
                }
                p.strokeColor = c;
                p.lineWidth = 1.2f;
                p.Stroke();
            }
            Draw.EllipseStroke(p, new Vector2(r.width * 0.78f, r.height * 0.55f), r.height * 0.22f, r.height * 0.22f, new Color(tint.r, tint.g, tint.b, 0.1f), 3f);
        }

        void DrawCode(Painter2D p, Rect r)
        {
            int seed = Data != null ? (Data.OrderId * 131 + Data.Get(DocField.DocumentNumber).Length * 17) : 1;
            var rng = new System.Random(seed);
            var c = new Color(0.1f, 0.1f, 0.12f, 0.75f);
            float x = 6f;
            while (x < r.width - 6f)
            {
                float w = 1f + (float)rng.NextDouble() * 3f;
                Draw.Rect(p, new Rect(x, 4f, w, r.height - 8f), c);
                x += w + 1.5f + (float)rng.NextDouble() * 3f;
            }
        }
    }
}
