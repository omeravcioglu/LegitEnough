using System;
using System.Collections.Generic;
using LegitEnough.Sim;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// Developer tool (F9): a wall of generated customers. Page 0 shows every hairstyle and
    /// accessory combination; later pages show customers as the generator makes them.
    /// Useful for judging variety (design doc §7) and for spotting art bugs.
    /// </summary>
    public sealed class DevLineupView : Overlay
    {
        const int Columns = 8;
        const int Rows = 3;
        readonly VisualElement _grid;
        readonly Label _title;
        int _page;

        public DevLineupView(GameRoot game, int page = 0) : base(game, "le-overlay--black")
        {
            _page = page;
            _title = Ui.Text("", "le-h2", Root).At(40, 16, 1200, 50);
            var nav = Ui.El("le-row", Root).At(1300, 10, 600, 60);
            Ui.Btn("< PREV", () => Show(Math.Max(0, _page - 1)), "le-btn--small", nav);
            Ui.Btn("NEXT >", () => Show(_page + 1), "le-btn--small", nav);
            Ui.Btn("CLOSE", Close, "le-btn--small le-btn--ghost", nav);
            _grid = Ui.El("le-row le-row--wrap", Root).At(30, 80, 1860, 990);
            Show(page);
        }

        public override bool PausesGame => true;

        public void Show(int page)
        {
            _page = page;
            _grid.Clear();
            var people = new List<CustomerProfile>();
            if (page == 0)
            {
                _title.text = "LINEUP - every hairstyle (top), glasses/hats/facial hair (bottom)";
                int i = 0;
                foreach (HairStyle style in Enum.GetValues(typeof(HairStyle)))
                {
                    var p = CustomerGenerator.Generate(500 + i, 42);
                    p.Look.Hair = style;
                    p.Look.Glasses.Clear();
                    p.Look.Hat = HatStyle.None;
                    p.Look.Face = (FaceShape)(i % 5);
                    people.Add(p);
                    i++;
                }
                foreach (GlassesStyle g in new[] { GlassesStyle.Round, GlassesStyle.Square, GlassesStyle.Thick, GlassesStyle.Sunglasses })
                {
                    var p = CustomerGenerator.Generate(600 + i++, 42);
                    p.Look.Glasses.Clear();
                    p.Look.Glasses.Add(g);
                    p.Look.Hat = HatStyle.None;
                    people.Add(p);
                }
                foreach (HatStyle h in new[] { HatStyle.Beanie, HatStyle.Cap, HatStyle.Fedora })
                {
                    var p = CustomerGenerator.Generate(700 + i++, 42);
                    p.Look.Hat = h;
                    people.Add(p);
                }
                foreach (FacialHair f in new[] { FacialHair.Stubble, FacialHair.Mustache, FacialHair.Goatee, FacialHair.Beard })
                {
                    var p = CustomerGenerator.Generate(800 + i++, 42);
                    p.Look.FacialHair = f;
                    p.Look.Hair = HairStyle.Short;
                    people.Add(p);
                }
                var old = CustomerGenerator.Generate(900, 42);
                old.Look.Age = 68;
                old.Look.HairColor = 7;
                old.Look.Hair = HairStyle.Long;
                people.Add(old);
                var twoPairs = CustomerGenerator.Generate(901, 42);
                twoPairs.Look.Glasses.Clear();
                twoPairs.Look.Glasses.Add(GlassesStyle.Square);
                twoPairs.Look.Glasses.Add(GlassesStyle.Sunglasses);
                people.Add(twoPairs);
            }
            else
            {
                _title.text = $"LINEUP - generated customers, page {page}";
                for (int k = 0; k < Columns * Rows; k++) people.Add(CustomerGenerator.Generate((page - 1) * Columns * Rows + k + 1, S.Seed));
            }

            foreach (var p in people)
            {
                var cell = Ui.El("le-lineup__cell", _grid);
                var portrait = new PortraitView("le-lineup__portrait")
                {
                    Capture = new PhotoCapture
                    {
                        Id = p.Id,
                        CustomerId = p.Id,
                        Look = p.Look,
                        Pose = HeadPose.Center,
                        Expression = p.Behavior.Resting,
                        Glasses = new List<GlassesStyle>(p.Look.Glasses),
                        HatOn = p.Look.Hat != HatStyle.None,
                        Backdrop = Backdrop.LightGray,
                        Zoom = 2f,
                        PanY = 0.25f,
                        SensorHeightPx = 2000,
                    },
                    ShowGrain = false,
                };
                portrait.pickingMode = PickingMode.Ignore;
                cell.Add(portrait);
                Ui.Text($"{p.Identity.FirstName} {p.Identity.Surname}", "le-lineup__name", cell);
                Ui.Text($"{p.Primary}  ·  {p.Look.Hair}  ·  {p.Look.Age}", "le-lineup__meta", cell);
            }
        }
    }
}
