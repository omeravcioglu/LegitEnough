using System;

namespace LegitEnough.Sim
{
    /// <summary>Axis-aligned rectangle (x, y = top-left; y grows downward).</summary>
    public readonly struct Box
    {
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;

        public Box(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public float Right => X + W;
        public float Bottom => Y + H;
        public float CenterX => X + W * 0.5f;
        public float CenterY => Y + H * 0.5f;

        public static Box FromCenter(float cx, float cy, float w, float h) => new Box(cx - w * 0.5f, cy - h * 0.5f, w, h);

        public bool Contains(float px, float py) => px >= X && px <= Right && py >= Y && py <= Bottom;

        public bool Inside(Box outer, float epsilon = 1e-4f) =>
            X >= outer.X - epsilon && Y >= outer.Y - epsilon && Right <= outer.Right + epsilon && Bottom <= outer.Bottom + epsilon;

        /// <summary>Clamp position (not size) so the box lies within <paramref name="outer"/>.</summary>
        public Box ClampInside(Box outer)
        {
            float w = Math.Min(W, outer.W);
            float h = Math.Min(H, outer.H);
            float x = Math.Max(outer.X, Math.Min(X, outer.Right - w));
            float y = Math.Max(outer.Y, Math.Min(Y, outer.Bottom - h));
            return new Box(x, y, w, h);
        }

        public override string ToString() => $"({X:0.###},{Y:0.###} {W:0.###}x{H:0.###})";
    }
}
