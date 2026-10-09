using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>
    /// One sitting at the photo station: the subject, the tripod, the shutter. The cheap
    /// camera has shutter lag, so the frame is grabbed a moment after the click — which is
    /// exactly when people blink.
    /// </summary>
    public sealed class PhotoSession
    {
        readonly Func<int> _nextPhotoId;
        readonly Func<GameTime> _now;
        readonly List<PhotoCapture> _shots = new List<PhotoCapture>();
        float _pendingShot = -1f;

        public readonly PhotoSubject Subject;
        public readonly CameraRig Rig;
        public readonly EquipmentDef Camera;
        public readonly EquipmentDef Station;
        public readonly int OrderId;

        /// <summary>Set when a shot finishes; the review screen takes it.</summary>
        public PhotoCapture JustCaptured { get; private set; }
        public int ShotsFired { get; private set; }
        public IReadOnlyList<PhotoCapture> Shots => _shots;
        public bool ShotPending => _pendingShot >= 0f;

        public PhotoSession(PhotoSubject subject, CameraRig rig, EquipmentDef camera, EquipmentDef station, int orderId,
            Func<int> nextPhotoId, Func<GameTime> now)
        {
            Subject = subject;
            Rig = rig;
            Camera = camera;
            Station = station;
            OrderId = orderId;
            _nextPhotoId = nextPhotoId;
            _now = now;
            rig.ClampTo(camera);
        }

        public void Tick(float dt)
        {
            Subject.Tick(dt);
            Rig.Tick(dt);
            if (_pendingShot >= 0f)
            {
                _pendingShot -= dt;
                if (_pendingShot < 0f)
                {
                    JustCaptured = Capture();
                    _shots.Add(JustCaptured);
                }
            }
        }

        /// <summary>Returns false if the camera is busy.</summary>
        public bool PressShutter()
        {
            if (ShotPending) return false;
            ShotsFired++;
            _pendingShot = Math.Max(0f, Camera.ShutterLag);
            if (_pendingShot == 0f) _pendingShot = 0.0001f;
            return true;
        }

        public PhotoCapture TakeJustCaptured()
        {
            var c = JustCaptured;
            JustCaptured = null;
            return c;
        }

        public void ZoomBy(float delta)
        {
            Rig.Zoom = (float)Math.Round((Rig.Zoom + delta) * 4f) / 4f;
            Rig.ClampTo(Camera);
        }

        public void PanBy(float dx, float dy)
        {
            Rig.PanX += dx;
            Rig.PanY += dy;
            Rig.ClampTo(Camera);
        }

        public void PullBackdrop(Backdrop b) => Rig.PullBackdrop(b, Station.BackdropSwapSeconds);

        PhotoCapture Capture()
        {
            var s = Subject;
            return new PhotoCapture
            {
                Id = _nextPhotoId(),
                CustomerId = s.Customer.Id,
                TakenAt = _now(),
                Look = s.Customer.Look,
                Pose = s.Pose,
                GazeX = s.GazeX,
                GazeY = s.GazeY,
                Expression = s.Expression,
                Lid = s.Lid,
                Blurred = s.InMotion,
                Glasses = new List<GlassesStyle>(s.Glasses),
                HatOn = s.HatOn,
                SeatOffset = s.SeatOffset,
                Slouch = s.Slouch,
                Backdrop = Rig.Backdrop,
                BackdropMoving = Rig.SwappingBackdrop,
                Zoom = Rig.Zoom,
                PanX = Rig.PanX,
                PanY = Rig.PanY,
                SensorHeightPx = Camera.SensorHeightPx,
                Noise = Camera.Noise,
                LightingBonus = Station.LightingBonus,
            };
        }
    }
}
