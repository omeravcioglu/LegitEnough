using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>
    /// A photograph: a frozen copy of subject and camera state at the instant the shutter
    /// fired. Renderers redraw it from this data; the evaluator scores it from the same data.
    /// </summary>
    public sealed class PhotoCapture
    {
        public int Id;
        public int CustomerId;
        public GameTime TakenAt;
        public Appearance Look;

        // Subject
        public HeadPose Pose;
        public float GazeX;
        public float GazeY;
        public Expression Expression;
        public float Lid;
        public bool Blurred;
        public List<GlassesStyle> Glasses = new List<GlassesStyle>();
        public bool HatOn;
        public float SeatOffset;
        public float Slouch;

        // Camera
        public Backdrop Backdrop;
        public float Zoom;
        public float PanX;
        public float PanY;
        public int SensorHeightPx;
        public float Noise;
        public float LightingBonus;
        /// <summary>The backdrop roll was still moving when the shutter fired.</summary>
        public bool BackdropMoving;

        /// <summary>Crop in normalized frame coordinates. Defaults to the full (uncropped) frame.</summary>
        public Box Crop = PortraitGeometry.FullCrop();
        public bool Kept;

        public Box FrameWorld => PortraitGeometry.FrameWorld(Zoom, PanX, PanY);

        /// <summary>Measured head (hair/hat top to chin) in normalized frame coordinates.</summary>
        public Box HeadInFrame =>
            PortraitGeometry.ToFrame(PortraitGeometry.HeadWorld(Look, Pose, SeatOffset, Slouch, HatOn), FrameWorld);

        public bool EyesClosed => Lid >= Balance.BlinkClosedThreshold;
        public bool EyesHalfClosed => Lid >= Balance.BlinkHalfThreshold && Lid < Balance.BlinkClosedThreshold;

        public float Coverage => HeadInFrame.H / Crop.H;
    }
}
