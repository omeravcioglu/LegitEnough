using System;
using LegitEnough.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// A customer physically in the shop (design doc §4): walks between spots with depth
    /// scaling, sits, blinks, glances around, moves their mouth when talking. When they're on
    /// the photo stool the figure mirrors the live <see cref="PhotoSubject"/>. Drawn as pixel
    /// art on the room's 4x grid and lit by the room's light.
    /// </summary>
    public sealed class CustomerFigure : VisualElement
    {
        const float BoxW = 760f;
        const float BoxH = 1180f;
        const int Art = ShopRenderer.Scale;

        public readonly Visit Visit;
        public readonly bool IsCompanion;
        /// <summary>The room light (null outside the shop).</summary>
        public ShopRenderer Room;

        RoomLayout.Anchor _anchor;
        bool _seated;
        BodyPose _pose;
        HeadState _head;
        float _phase;
        float _blinkIn = 2f;
        float _blinkT = -1f;
        float _glanceIn = 3f;
        float _glanceT;
        float _talkT;
        float _breathT;
        readonly System.Random _rand;

        readonly PixelImage _image = new PixelImage();
        readonly PixelBuffer _buf = new PixelBuffer((int)(BoxW / Art), (int)(BoxH / Art));
        readonly PixelPainter _painter;
        float _left, _top;
        int _key;

        public CustomerProfile Profile => IsCompanion ? Visit.Companion : Visit.Customer;
        public float Depth => _anchor.Scale;
        public bool Seated => _seated;

        /// <summary>Head center in stage coordinates.</summary>
        public Vector2 HeadStage
        {
            get
            {
                float H = FigurePainter.BaseHeadHeight * _anchor.Scale;
                float hip = _seated ? 1.15f * H : 3.25f * H;
                return new Vector2(_anchor.Feet.x, _anchor.Feet.y - hip - 2.4f * H - 0.7f * H);
            }
        }

        public float HeadSizeStage => FigurePainter.BaseHeadHeight * _anchor.Scale;

        public CustomerFigure(Visit visit, bool companion)
        {
            Visit = visit;
            IsCompanion = companion;
            _rand = new System.Random(visit.Id * 31 + (companion ? 7 : 0));
            _painter = new PixelPainter(_buf);
            AddToClassList("le-figure");
            style.position = Position.Absolute;
            style.width = BoxW;
            style.height = BoxH;
            pickingMode = PickingMode.Position;
            _image.style.position = Position.Absolute;
            _image.style.left = 0;
            _image.style.top = 0;
            _image.style.width = BoxW;
            _image.style.height = BoxH;
            Add(_image);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        public void Sync(float dt)
        {
            var v = Visit;
            var from = RoomLayout.Of(v.From, v.BenchSeat);
            var to = RoomLayout.Of(v.To, v.BenchSeat);
            float t = v.Moving ? Smooth(v.MoveT) : 1f;
            _anchor = v.Moving ? RoomLayout.Anchor.Lerp(from, to, t) : to;
            if (IsCompanion) _anchor = RoomLayout.CompanionOffset(_anchor);

            bool walking = v.Moving;
            _seated = !walking && (v.To == Spot.Bench || v.To == Spot.Stool);
            if (walking) _phase += dt * 9f;
            _breathT += dt;

            float alpha = 1f;
            if (v.Moving && v.To == Spot.Outside) alpha = 1f - t;
            else if (v.Moving && v.From == Spot.Outside) alpha = t;
            else if (!v.Moving && v.To == Spot.Outside) alpha = 0f;
            style.opacity = alpha;
            pickingMode = alpha > 0.5f ? PickingMode.Position : PickingMode.Ignore;

            // Fixed-size box on the room's pixel grid; the feet move inside it.
            _left = Mathf.Round((_anchor.Feet.x - BoxW * 0.5f) / Art) * Art;
            _top = Mathf.Round((_anchor.Feet.y - BoxH) / Art) * Art;
            style.left = _left;
            style.top = _top;

            // Walk cycles in eight frames, like a sprite sheet.
            float frames = 8f;
            float walkFrame = Mathf.Floor(_phase / (Mathf.PI * 2f) * frames) / frames * Mathf.PI * 2f;
            _pose = new BodyPose
            {
                Seated = _seated,
                Walking = walking,
                WalkPhase = walkFrame,
                Holding = !IsCompanion && v.Holding != null,
                HoldingColor = v.Holding != null ? Looks.Of(v.Holding.BookletCover) : Color.clear,
                // Idle breathing: shoulders rise a pixel for a moment every few seconds.
                Breath = !walking && Mathf.Repeat(_breathT * 0.45f + Visit.Id * 0.37f, 1f) < 0.42f ? 1f : 0f,
            };
            UpdateHead(dt);
            Render();
        }

        void UpdateHead(float dt)
        {
            var p = Profile;
            var v = Visit;

            // On the stool during a sitting: mirror the photo subject exactly.
            if (!IsCompanion && v.Subject != null && v.Phase == VisitPhase.PhotoSitting)
            {
                var sub = v.Subject;
                _head = new HeadState
                {
                    Yaw = sub.Pose.YawF,
                    Pitch = sub.Pose.PitchF,
                    GazeX = sub.GazeX,
                    GazeY = sub.GazeY,
                    Lid = sub.Lid,
                    Expression = sub.Expression,
                    Glasses = sub.Glasses,
                    HatOn = sub.HatOn,
                    MouthOpen = TalkMouth(dt),
                };
                return;
            }

            // Blinking.
            float lid = 0f;
            if (_blinkT >= 0f)
            {
                _blinkT += dt;
                float bt = _blinkT / 0.16f;
                if (bt >= 1f)
                {
                    _blinkT = -1f;
                    _blinkIn = 1.5f + (float)_rand.NextDouble() * 4f;
                }
                else lid = bt < 0.5f ? bt * 2f : (1f - bt) * 2f;
            }
            else
            {
                _blinkIn -= dt;
                if (_blinkIn <= 0f) _blinkT = 0f;
            }

            // Idle glances: nervous people check the door, everybody looks around sometimes.
            float yaw = 0f, gaze = 0f;
            _glanceIn -= dt;
            if (_glanceIn <= 0f)
            {
                _glanceT = 0.8f + (float)_rand.NextDouble() * 1.2f;
                _glanceIn = (p.Has(Trait.Nervous) || p.Has(Trait.Suspicious) ? 2.5f : 6f) + (float)_rand.NextDouble() * 4f;
            }
            if (_glanceT > 0f)
            {
                _glanceT -= dt;
                bool towardDoor = p.Has(Trait.Nervous) || p.Has(Trait.Suspicious) || _rand.NextDouble() < 0.02;
                yaw = towardDoor ? -0.5f : 0.5f;
                gaze = towardDoor ? -0.8f : 0.6f;
            }
            if (v.Moving) yaw = 0f;

            var expr = p.Behavior.Resting;
            if (v.PatienceFraction < 0.25f) expr = Expression.Angry;
            if (v.Phase == VisitPhase.Inspecting) expr = Expression.Neutral;

            _head = new HeadState
            {
                Yaw = yaw,
                Pitch = v.Phase == VisitPhase.Inspecting && !IsCompanion ? -0.6f : 0f,
                GazeX = gaze,
                GazeY = v.Phase == VisitPhase.Inspecting && !IsCompanion ? -0.8f : 0f,
                Lid = Mathf.Max(lid, p.Behavior.LidDroop),
                Expression = expr,
                Glasses = p.Look.Glasses,
                HatOn = p.Look.Hat != HatStyle.None,
                MouthOpen = TalkMouth(dt),
            };
        }

        float TalkMouth(float dt)
        {
            var line = Visit.Line;
            bool talking = line != null && line.ByCompanion == IsCompanion && Visit.LineElapsed < line.Duration - 0.3f;
            if (!talking) return 0f;
            _talkT += dt * 14f;
            return 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(_talkT));
        }

        Vector2 FeetArt => new Vector2((_anchor.Feet.x - _left) / Art, (_anchor.Feet.y - _top) / Art);

        void Render()
        {
            float s = _anchor.Scale;
            var feet = FeetArt;
            int glasses = _head.Glasses?.Count ?? 0;
            int key = HashCode.Combine(
                HashCode.Combine(Mathf.RoundToInt(s * 400f), Mathf.RoundToInt(feet.x * 4f), Mathf.RoundToInt(feet.y * 4f), _seated, _pose.Walking, Mathf.RoundToInt(_pose.WalkPhase * 10f), _pose.Holding, _pose.Breath > 0f),
                HashCode.Combine(Mathf.RoundToInt(_head.Yaw * 10f), Mathf.RoundToInt(_head.Pitch * 10f), Mathf.RoundToInt(_head.GazeX * 5f), Mathf.RoundToInt(_head.GazeY * 5f)),
                HashCode.Combine(Mathf.RoundToInt(_head.Lid * 6f), Mathf.RoundToInt(_head.MouthOpen * 3f), _head.Expression, glasses, _head.HatOn),
                Room?.LightVersion ?? -1, Mathf.RoundToInt(_left), Mathf.RoundToInt(_top));
            if (key == _key && _image.HasTexture) return;
            _key = key;

            _buf.Clear();
            if (s > 0f)
            {
                _painter.Target = _buf;
                FigurePainter.DrawFigure(_painter, feet, s, Profile.Look, _pose, _head);
                _buf.Outline(Pal.Outline);
                Room?.Light.Apply(_buf, Mathf.RoundToInt(_left / Art), Mathf.RoundToInt(_top / Art));
            }
            _image.Upload(_buf);
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            float s = _anchor.Scale;
            var feet = new Vector2(_anchor.Feet.x - _left, _anchor.Feet.y - _top);
            return FigurePainter.HitFigure(localPoint, feet, s, _seated);
        }
    }
}
