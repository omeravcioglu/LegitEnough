using System;

namespace LegitEnough.Sim
{
    /// <summary>
    /// Discrete head direction (design doc §16). Yaw and pitch are stored in half-steps
    /// from -2 to 2: even values are the nine named poses (UP-LEFT ... DOWN-RIGHT),
    /// odd values are the in-between transition frames.
    /// Yaw &gt; 0 turns the face toward screen-right; pitch &gt; 0 lifts the chin.
    /// </summary>
    public readonly struct HeadPose : IEquatable<HeadPose>
    {
        public const int Min = -2;
        public const int Max = 2;

        public readonly int Yaw;
        public readonly int Pitch;

        public HeadPose(int yaw, int pitch)
        {
            Yaw = Math.Max(Min, Math.Min(Max, yaw));
            Pitch = Math.Max(Min, Math.Min(Max, pitch));
        }

        public static readonly HeadPose Center = new HeadPose(0, 0);

        /// <summary>-1..1 continuous values for renderers.</summary>
        public float YawF => Yaw * 0.5f;
        public float PitchF => Pitch * 0.5f;

        public bool IsCenter => Yaw == 0 && Pitch == 0;
        public bool IsNamedPose => Yaw % 2 == 0 && Pitch % 2 == 0;

        /// <summary>One half-step toward <paramref name="target"/> on each axis that differs.</summary>
        public HeadPose StepToward(HeadPose target) =>
            new HeadPose(Yaw + Math.Sign(target.Yaw - Yaw), Pitch + Math.Sign(target.Pitch - Pitch));

        public string Name
        {
            get
            {
                string v = Pitch > 0 ? "UP" : Pitch < 0 ? "DOWN" : "";
                string h = Yaw < 0 ? "LEFT" : Yaw > 0 ? "RIGHT" : "";
                if (v.Length == 0 && h.Length == 0) return "CENTER";
                if (v.Length == 0) return h;
                if (h.Length == 0) return v;
                return v + "-" + h;
            }
        }

        public bool Equals(HeadPose other) => Yaw == other.Yaw && Pitch == other.Pitch;
        public override bool Equals(object obj) => obj is HeadPose p && Equals(p);
        public override int GetHashCode() => Yaw * 31 + Pitch;
        public static bool operator ==(HeadPose a, HeadPose b) => a.Equals(b);
        public static bool operator !=(HeadPose a, HeadPose b) => !a.Equals(b);
        public override string ToString() => $"{Name}({Yaw},{Pitch})";
    }
}
