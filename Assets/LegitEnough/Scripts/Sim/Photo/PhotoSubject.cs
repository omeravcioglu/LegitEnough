using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum PhotoCommand
    {
        LookAtCamera,
        /// <summary>"Stop smiling" / "Relax your face" depending on the current expression.</summary>
        NeutralFace,
        OpenEyes,
        RemoveGlasses,
        RemoveHat,
        HoldStill,
    }

    /// <summary>
    /// A customer sitting in front of the camera (design doc §15-§20). Customers are not
    /// statues: they blink, drift back into habits and only sometimes do what they're told.
    /// </summary>
    public sealed class PhotoSubject
    {
        /// <summary>Real seconds per half-step of head movement (transition frame rate).</summary>
        public const float StepSeconds = 0.075f;

        readonly PoseBehavior _b;
        readonly Rng _rng;
        readonly Queue<string> _lines = new Queue<string>();

        public readonly CustomerProfile Customer;
        public readonly List<GlassesStyle> Glasses;
        public readonly List<GlassesStyle> RemovedGlasses = new List<GlassesStyle>();

        public HeadPose Pose { get; private set; }
        public HeadPose Target { get; private set; }
        public float GazeX { get; private set; }
        public float GazeY { get; private set; }
        public Expression Expression { get; private set; }
        /// <summary>Rendered eyelid closure: 0 = open, 1 = shut.</summary>
        public float Lid { get; private set; }
        public float SeatOffset { get; private set; }
        public float Slouch { get; private set; }
        public bool HatOn { get; private set; }
        public bool HatRemoved { get; private set; }
        public bool Dragging { get; private set; }
        /// <summary>Patience consumed since last collected by the visit.</summary>
        public float PendingPatienceCost { get; private set; }

        float _gazeTargetX, _gazeTargetY;
        float _stepTimer;
        float _sinceMoved = 10f;
        float _blinkIn;
        float _blinkT = -1f;
        float _driftIn;
        float _relapseIn = -1f;
        float _holdStillFor;
        float _eyesOpenFor;
        float _returnIn = -1f;
        HeadPose _returnPose;
        float _delayedLookIn = -1f;
        int _drags;
        int _commandsIssued;

        public PhotoSubject(CustomerProfile customer, Rng rng)
        {
            Customer = customer;
            _b = customer.Behavior;
            _rng = rng;
            Glasses = new List<GlassesStyle>(customer.Look.Glasses);
            HatOn = customer.Look.Hat != HatStyle.None;
            Expression = _b.Resting;

            int yaw = PickWeighted(new[] { -2, -1, 0, 1, 2 }, new[] { 0.12f, 0.25f, 0.26f, 0.25f, 0.12f });
            int pitch = PickWeighted(new[] { -1, 0, 1 }, new[] { 0.35f, 0.4f, 0.25f });
            if (_b.Drift == DriftKind.Sag || _b.Drift == DriftKind.LookDown || _b.Drift == DriftKind.Wobble)
                pitch = rng.Chance(0.5f) ? -2 : -1;
            if (_b.Drift == DriftKind.ChinUp) pitch = 1;
            Pose = Target = new HeadPose(yaw, pitch);

            SeatOffset = rng.Range(-0.35f, 0.35f);
            Slouch = rng.Range(0f, 0.15f);
            if (customer.Has(Trait.Exhausted) || customer.Has(Trait.Drunk)) Slouch += 0.15f;

            _blinkIn = rng.Range(0.4f, _b.BlinkInterval);
            _driftIn = rng.Range(_b.DriftIntervalMin, _b.DriftIntervalMax);
        }

        int PickWeighted(int[] values, float[] weights)
        {
            float total = 0f;
            foreach (var w in weights) total += w;
            float r = _rng.NextFloat() * total;
            for (int i = 0; i < values.Length; i++)
            {
                r -= weights[i];
                if (r < 0f) return values[i];
            }
            return values[values.Length - 1];
        }

        /// <summary>True while the head is moving; a shot taken now comes out blurred.</summary>
        public bool InMotion => Pose != Target || _sinceMoved < 0.09f;

        public bool HasGlasses => Glasses.Count > 0;
        public bool WearingSunglasses => Glasses.Count > 0 && Glasses[Glasses.Count - 1] == GlassesStyle.Sunglasses;

        public bool TryTakeLine(out string line)
        {
            if (_lines.Count > 0)
            {
                line = _lines.Dequeue();
                return true;
            }
            line = null;
            return false;
        }

        public float CollectPatienceCost()
        {
            float c = PendingPatienceCost;
            PendingPatienceCost = 0f;
            return c;
        }

        void Say(string line)
        {
            if (!string.IsNullOrEmpty(line) && _lines.Count < 3) _lines.Enqueue(line);
        }

        // ------------------------------------------------------------------ simulation

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            PendingPatienceCost += dt * Balance.PhotoPatienceDrainPerSecond;
            TickPose(dt);
            TickGaze(dt);
            TickBlink(dt);
            TickHabits(dt);
        }

        void TickPose(float dt)
        {
            if (Pose != Target)
            {
                _stepTimer += dt;
                while (_stepTimer >= StepSeconds && Pose != Target)
                {
                    _stepTimer -= StepSeconds;
                    Pose = Pose.StepToward(Target);
                    _sinceMoved = 0f;
                }
            }
            else
            {
                _stepTimer = 0f;
                _sinceMoved += dt;
            }
        }

        void TickGaze(float dt)
        {
            float k = 1f - (float)Math.Exp(-dt * 14f);
            GazeX += (_gazeTargetX - GazeX) * k;
            GazeY += (_gazeTargetY - GazeY) * k;
        }

        void TickBlink(float dt)
        {
            float closure = 0f;
            if (_blinkT >= 0f)
            {
                _blinkT += dt;
                float t = _blinkT / _b.BlinkDuration;
                if (t >= 1f)
                {
                    _blinkT = -1f;
                    _blinkIn = _b.BlinkInterval * _rng.Range(0.35f, 1.65f);
                }
                else
                {
                    closure = t < 0.4f ? t / 0.4f : t < 0.6f ? 1f : (1f - t) / 0.4f;
                }
            }
            else
            {
                _blinkIn -= dt;
                if (_blinkIn <= 0f) _blinkT = 0f;
            }

            if (_eyesOpenFor > 0f) _eyesOpenFor -= dt;
            float droop = _eyesOpenFor > 0f ? 0f : _b.LidDroop;
            if (Pose.Pitch < 0) droop += 0.07f * -Pose.Pitch;
            if (Expression == Expression.Tired && _eyesOpenFor <= 0f) droop += 0.15f;
            if (Expression == Expression.Smile || Expression == Expression.Grin) droop += 0.08f;
            Lid = Math.Min(1f, Math.Max(droop, closure));
        }

        void TickHabits(float dt)
        {
            if (_relapseIn > 0f)
            {
                _relapseIn -= dt;
                if (_relapseIn <= 0f && Expression == Expression.Neutral && _b.Resting != Expression.Neutral)
                {
                    Expression = _b.Resting;
                    Say(Lines.ExpressionRelapse(Customer, _rng));
                }
            }

            if (_delayedLookIn > 0f)
            {
                _delayedLookIn -= dt;
                if (_delayedLookIn <= 0f) LookAtCameraNow();
            }

            if (_returnIn > 0f)
            {
                _returnIn -= dt;
                if (_returnIn <= 0f && !Dragging)
                {
                    Target = _returnPose;
                    _gazeTargetX = 0f;
                    _gazeTargetY = 0f;
                }
            }

            if (_holdStillFor > 0f)
            {
                _holdStillFor -= dt;
                return;
            }
            if (Dragging || _b.Drift == DriftKind.None) return;

            _driftIn -= dt;
            if (_driftIn > 0f) return;
            _driftIn = _rng.Range(_b.DriftIntervalMin, _b.DriftIntervalMax);
            Drift();
        }

        void Drift()
        {
            switch (_b.Drift)
            {
                case DriftKind.GlanceAtDoor:
                    _returnPose = Target;
                    _gazeTargetX = -1f;
                    _gazeTargetY = 0f;
                    Target = new HeadPose(_rng.Chance(0.5f) ? -2 : -1, Target.Pitch);
                    _returnIn = _rng.Chance(0.55f) ? _rng.Range(0.9f, 2.4f) : -1f;
                    if (_rng.Chance(0.2f)) Say(Lines.GlanceExcuse(Customer, _rng));
                    break;
                case DriftKind.Wobble:
                {
                    bool yawAxis = _rng.Chance(0.5f);
                    int d = _rng.Chance(0.6f) ? -1 : 1;
                    Target = yawAxis ? new HeadPose(Target.Yaw + d, Target.Pitch) : new HeadPose(Target.Yaw, Target.Pitch + d);
                    if (_rng.Chance(0.15f)) Say("*hic*");
                    break;
                }
                case DriftKind.Sag:
                    Target = new HeadPose(Target.Yaw, Target.Pitch - 1);
                    if (Target.Pitch == HeadPose.Min && _rng.Chance(0.3f)) Say("Sorry... long week.");
                    break;
                case DriftKind.Fidget:
                {
                    _returnPose = Target;
                    bool yawAxis = _rng.Chance(0.5f);
                    int d = _rng.Chance(0.5f) ? -1 : 1;
                    Target = yawAxis ? new HeadPose(Target.Yaw + d, Target.Pitch) : new HeadPose(Target.Yaw, Target.Pitch + d);
                    _returnIn = _rng.Chance(0.5f) ? _rng.Range(1f, 2f) : -1f;
                    if (_rng.Chance(0.15f)) Say(Lines.Fidget(Customer, _rng));
                    break;
                }
                case DriftKind.LookDown:
                    Target = new HeadPose(Target.Yaw, Math.Min(Target.Pitch, -1) - (_rng.Chance(0.4f) ? 1 : 0));
                    _gazeTargetY = -0.6f;
                    if (_rng.Chance(0.2f)) Say("Sorry.");
                    break;
                case DriftKind.ChinUp:
                    Target = new HeadPose(Target.Yaw, Target.Pitch + 1);
                    break;
            }
        }

        // ------------------------------------------------------------------ player input

        public void BeginDrag()
        {
            Dragging = true;
            _drags++;
            _returnIn = -1f;
            _delayedLookIn = -1f;
            _gazeTargetX = 0f;
            _gazeTargetY = 0f;
            PendingPatienceCost += Balance.DragPatienceCost;
            if (_drags % 4 == 0 && _rng.Chance(0.6f)) Say(Lines.Manhandled(Customer, _rng));
        }

        /// <summary>Head follows the hand; <paramref name="yaw"/>/<paramref name="pitch"/> are half-steps.</summary>
        public void DragTo(int yaw, int pitch)
        {
            if (!Dragging) return;
            Target = new HeadPose(yaw, pitch);
        }

        public void EndDrag()
        {
            if (!Dragging) return;
            Dragging = false;
            _driftIn = Math.Max(_driftIn, _rng.Range(2.5f, 4.5f));
        }

        /// <summary>Slide on the stool (dragging the shoulders). World units.</summary>
        public void NudgeSeat(float dx)
        {
            SeatOffset = Math.Max(-0.8f, Math.Min(0.8f, SeatOffset + dx));
        }

        public void EndNudge()
        {
            PendingPatienceCost += Balance.DragPatienceCost;
        }

        /// <summary>Spoken instruction. Returns true if obeyed immediately.</summary>
        public bool Command(PhotoCommand cmd)
        {
            _commandsIssued++;
            PendingPatienceCost += Balance.CommandPatienceCost;
            bool obey = _rng.Chance(_b.Compliance);

            switch (cmd)
            {
                case PhotoCommand.LookAtCamera:
                    if (obey)
                    {
                        LookAtCameraNow();
                        Say(Lines.LookAck(Customer, _rng));
                    }
                    else
                    {
                        Say(Lines.LookRefuse(Customer, _rng));
                        _delayedLookIn = _rng.Chance(0.6f) ? _rng.Range(0.9f, 1.8f) : -1f;
                    }
                    return obey;

                case PhotoCommand.NeutralFace:
                    if (Expression == Expression.Neutral)
                    {
                        Say("This IS my face.");
                        return true;
                    }
                    if (!obey)
                    {
                        Say(Lines.FaceRefuse(Customer, _rng));
                        return false;
                    }
                    Say(Lines.FaceAck(Customer, Expression, _rng));
                    Expression = Expression.Neutral;
                    if (_b.Resting != Expression.Neutral)
                        _relapseIn = _b.SmileRelapse ? _rng.Range(_b.RelapseMin, _b.RelapseMax) : _rng.Range(8f, 16f);
                    return true;

                case PhotoCommand.OpenEyes:
                    _eyesOpenFor = _rng.Range(5f, 9f);
                    Say(Customer.Has(Trait.Drunk) ? "Theyrr open." : "I'm awake. I'm awake.");
                    return true;

                case PhotoCommand.RemoveGlasses:
                    if (Glasses.Count == 0) return false;
                    var removed = Glasses[Glasses.Count - 1];
                    Glasses.RemoveAt(Glasses.Count - 1);
                    RemovedGlasses.Add(removed);
                    if (Glasses.Count > 0) Say("...Backup pair.");
                    else if (removed == GlassesStyle.Sunglasses) Say("Fine. It's bright in here.");
                    else Say(Lines.GlassesOff(Customer, _rng));
                    return true;

                case PhotoCommand.RemoveHat:
                    if (!HatOn) return false;
                    HatOn = false;
                    HatRemoved = true;
                    Say(_rng.Chance(0.5f) ? "Hat hair. Great." : "You're the boss.");
                    return true;

                case PhotoCommand.HoldStill:
                    _holdStillFor = _rng.Range(6f, 10f);
                    _returnIn = -1f;
                    Say(_rng.Chance(0.5f) ? "Not moving." : "Like a statue.");
                    return true;
            }
            return false;
        }

        void LookAtCameraNow()
        {
            _returnIn = -1f;
            _delayedLookIn = -1f;
            _gazeTargetX = 0f;
            _gazeTargetY = 0f;
            int yaw = 0;
            if (_b.Overshoots && _rng.Chance(0.5f)) yaw = _rng.Chance(0.5f) ? -1 : 1;
            Target = new HeadPose(yaw, Target.Pitch);
            _driftIn = Math.Max(_driftIn, _rng.Range(2f, 4f));
        }

        /// <summary>Puts everything back on when the session ends.</summary>
        public void Redress()
        {
            for (int i = RemovedGlasses.Count - 1; i >= 0; i--) Glasses.Add(RemovedGlasses[i]);
            RemovedGlasses.Clear();
            if (HatRemoved)
            {
                HatOn = true;
                HatRemoved = false;
            }
        }
    }
}
