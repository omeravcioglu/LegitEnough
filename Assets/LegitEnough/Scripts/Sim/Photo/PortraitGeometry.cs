using System;

namespace LegitEnough.Sim
{
    /// <summary>
    /// The camera on the tripod. Zoom/pan state persists between sessions, like a real
    /// tripod someone forgot to reset; the backdrop roll also stays where it was left.
    /// </summary>
    public sealed class CameraRig
    {
        public const float DefaultPanY = 0.6f;

        public Backdrop Backdrop = Backdrop.White;
        public float Zoom = 1f;
        public float PanX;
        public float PanY = DefaultPanY;

        /// <summary>Remaining seconds of a backdrop swap in progress (the roll is moving).</summary>
        public float BackdropSwapRemaining;
        public Backdrop? PendingBackdrop;

        public bool SwappingBackdrop => BackdropSwapRemaining > 0f;

        public void ClampTo(EquipmentDef camera)
        {
            Zoom = Math.Max(1f, Math.Min(camera.MaxZoom, Zoom));
            PanX = Math.Max(-1.5f, Math.Min(1.5f, PanX));
            PanY = Math.Max(-1.5f, Math.Min(2.2f, PanY));
        }

        public void Tick(float dt)
        {
            if (BackdropSwapRemaining <= 0f) return;
            BackdropSwapRemaining -= dt;
            if (BackdropSwapRemaining <= 0f && PendingBackdrop.HasValue)
            {
                Backdrop = PendingBackdrop.Value;
                PendingBackdrop = null;
                BackdropSwapRemaining = 0f;
            }
        }

        public void PullBackdrop(Backdrop b, float swapSeconds)
        {
            if (b == Backdrop && !PendingBackdrop.HasValue) return;
            PendingBackdrop = b;
            BackdropSwapRemaining = Math.Max(0.01f, swapSeconds);
        }
    }

    /// <summary>
    /// Shared photo-space math. World units are head heights; x grows right, y grows down,
    /// and (0,0) is the subject's head center when sitting straight. Renderers and the
    /// evaluator both use these functions, so what you see is exactly what gets scored.
    /// </summary>
    public static class PortraitGeometry
    {
        /// <summary>Extra height above the skull from hair or a hat, in head heights.</summary>
        public static float TopVolume(Appearance look, bool hatOn)
        {
            float hair = look.Hair switch
            {
                HairStyle.Bald => 0f,
                HairStyle.Buzz => 0.02f,
                HairStyle.Receding => 0.03f,
                HairStyle.Short => 0.06f,
                HairStyle.SidePart => 0.08f,
                HairStyle.Long => 0.07f,
                HairStyle.Ponytail => 0.05f,
                HairStyle.Curly => 0.12f,
                HairStyle.Bun => 0.16f,
                HairStyle.Afro => 0.30f,
                _ => 0.05f,
            };
            if (!hatOn || look.Hat == HatStyle.None) return hair;
            float hat = look.Hat switch
            {
                HatStyle.Beanie => 0.1f,
                HatStyle.Cap => 0.08f,
                HatStyle.Fedora => 0.2f,
                _ => 0f,
            };
            return Math.Max(hair * 0.4f, hat);
        }

        /// <summary>Skull (crown-to-chin) box in world units.</summary>
        public static Box SkullWorld(Appearance look, HeadPose pose, float seatOffset, float slouch)
        {
            float h = 1f - 0.05f * Math.Abs(pose.PitchF);
            float cx = seatOffset + pose.YawF * 0.05f;
            float cy = slouch - pose.PitchF * 0.05f;
            float w = look.FaceWidth;
            return Box.FromCenter(cx, cy, w, h);
        }

        /// <summary>Measured head box (top of hair/hat to chin) in world units.</summary>
        public static Box HeadWorld(Appearance look, HeadPose pose, float seatOffset, float slouch, bool hatOn)
        {
            Box skull = SkullWorld(look, pose, seatOffset, slouch);
            float extra = TopVolume(look, hatOn);
            return new Box(skull.X, skull.Y - extra, skull.W, skull.H + extra);
        }

        public static Box FrameWorld(float zoom, float panX, float panY)
        {
            float h = Balance.FrameHeadHeightsAtZoom1 / Math.Max(0.1f, zoom);
            float w = h * Balance.SensorAspect;
            return Box.FromCenter(panX, panY, w, h);
        }

        /// <summary>World box → normalized frame coordinates (0..1 inside the frame).</summary>
        public static Box ToFrame(Box world, Box frame) =>
            new Box((world.X - frame.X) / frame.W, (world.Y - frame.Y) / frame.H, world.W / frame.W, world.H / frame.H);

        /// <summary>Normalized crop width for a given normalized crop height (35:45 output on a 3:4 sensor).</summary>
        public static float CropWidthFor(float cropHeight) => cropHeight * (Balance.PhotoAspect / Balance.SensorAspect);

        /// <summary>The largest crop that fits the frame, centered. This is an "uncropped" photo.</summary>
        public static Box FullCrop()
        {
            float h = 1f;
            float w = CropWidthFor(h);
            if (w > 1f)
            {
                w = 1f;
                h = w / (Balance.PhotoAspect / Balance.SensorAspect);
            }
            return Box.FromCenter(0.5f, 0.5f, w, h);
        }

        /// <summary>Builds a valid crop box from a desired center and height (clamped into the frame).</summary>
        public static Box MakeCrop(float centerX, float centerY, float height)
        {
            Box full = FullCrop();
            float h = Math.Max(0.12f, Math.Min(full.H, height));
            float w = CropWidthFor(h);
            return Box.FromCenter(centerX, centerY, w, h).ClampInside(new Box(0f, 0f, 1f, 1f));
        }
    }
}
