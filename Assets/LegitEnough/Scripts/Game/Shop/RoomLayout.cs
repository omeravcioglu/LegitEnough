using LegitEnough.Sim;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>Where things are in the shop (stage pixels). Feet positions + depth scale per spot.</summary>
    public static class RoomLayout
    {
        public const float CounterTop = 690f;
        public const float DeskTop = 770f;
        public const float FloorLine = 540f;

        public readonly struct Anchor
        {
            public readonly Vector2 Feet;
            public readonly float Scale;

            public Anchor(float x, float y, float scale)
            {
                Feet = new Vector2(x, y);
                Scale = scale;
            }

            public static Anchor Lerp(Anchor a, Anchor b, float t) =>
                new Anchor(Mathf.Lerp(a.Feet.x, b.Feet.x, t), Mathf.Lerp(a.Feet.y, b.Feet.y, t), Mathf.Lerp(a.Scale, b.Scale, t));
        }

        public static readonly Anchor Outside = new Anchor(760f, 548f, 0.36f);
        public static readonly Anchor Door = new Anchor(760f, 552f, 0.38f);
        public static readonly Anchor Counter = new Anchor(960f, 1270f, 1f);
        public static readonly Anchor Stool = new Anchor(1650f, 642f, 0.5f);

        public static Anchor Bench(int seat) => new Anchor(130f + Mathf.Clamp(seat, 0, 2) * 120f, 614f, 0.44f);

        public static Anchor Of(Spot spot, int benchSeat) => spot switch
        {
            Spot.Outside => Outside,
            Spot.Door => Door,
            Spot.Counter => Counter,
            Spot.Stool => Stool,
            Spot.Bench => Bench(benchSeat),
            _ => Door,
        };

        /// <summary>Companions stand a little to the side and further back.</summary>
        public static Anchor CompanionOffset(Anchor a) =>
            new Anchor(a.Feet.x + 330f * a.Scale, a.Feet.y - 40f * a.Scale, a.Scale * 0.9f);
    }
}
