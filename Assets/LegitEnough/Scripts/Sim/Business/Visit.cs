using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>Physical places a customer can be in the shop (design doc §4).</summary>
    public enum Spot { Outside, Door, Counter, Stool, Bench }

    public enum VisitPhase
    {
        /// <summary>Walking somewhere; see <see cref="Visit.Next"/> for what happens on arrival.</summary>
        Walking,
        /// <summary>On the bench, waiting for the counter to free up.</summary>
        Queueing,
        /// <summary>Saying lines; the flow continues when they finish.</summary>
        Talking,
        /// <summary>Waiting for the player to pick a reply.</summary>
        Choosing,
        /// <summary>Order accepted, standing at the counter until the photo starts.</summary>
        AwaitingPhoto,
        /// <summary>Sitting on the photo stool.</summary>
        PhotoSitting,
        /// <summary>On the bench while the player works.</summary>
        Waiting,
        /// <summary>At the counter, expecting their document.</summary>
        AwaitingHandover,
        /// <summary>Turning the booklet over in their hands.</summary>
        Inspecting,
        Gone,
    }

    public sealed class Speech
    {
        public string Text = "";
        /// <summary>Spoken by the companion rather than the customer.</summary>
        public bool ByCompanion;
        public float Duration;
    }

    public sealed class Choice
    {
        public string Id = "";
        public string Text = "";
        public bool Enabled = true;

        public Choice(string id, string text, bool enabled = true)
        {
            Id = id;
            Text = text;
            Enabled = enabled;
        }
    }

    /// <summary>
    /// One customer's trip into the shop: where they are, what they're saying, what they
    /// want, and how much patience they have left.
    /// </summary>
    public sealed class Visit
    {
        public int Id;
        public CustomerProfile Customer;
        public CustomerProfile Companion;
        public ArrivalKind Kind;
        public StoryBeat Beat;
        public VisitPhase Phase = VisitPhase.Walking;

        // ---- movement
        public Spot From = Spot.Door;
        public Spot To = Spot.Door;
        public int BenchSeat = -1;
        public float MoveT = 1f;
        public float MoveDuration = 1f;
        /// <summary>What to do on reaching <see cref="To"/>.</summary>
        public Action Next;

        public bool Moving => MoveT < 1f;
        public Spot At => Moving ? From : To;

        // ---- conversation
        readonly Queue<Speech> _queue = new Queue<Speech>();
        public Speech Line { get; private set; }
        public float LineElapsed;
        public Action AfterLines;
        public readonly List<Choice> Choices = new List<Choice>();

        // ---- business
        public JobRequest Request;
        public Negotiation Negotiation;
        public Order Order;
        public FinishedDocument Holding;
        public FinishedDocument PendingHandover;
        public float InspectTimer;
        public bool Greeted;
        /// <summary>How many problems they spotted during inspection.</summary>
        public int Noticed;
        /// <summary>The person on the photo stool; created when they sit down.</summary>
        public PhotoSubject Subject;

        // ---- patience
        public float Patience;
        public float PatienceMax;
        public int ComplaintLevel;
        public float IdleTimer;

        /// <summary>Set once they've decided to go; nothing else happens to them after that.</summary>
        public bool Leaving;

        public float PatienceFraction => PatienceMax <= 0f ? 1f : Math.Max(0f, Patience / PatienceMax);
        public bool IsTalking => Line != null || _queue.Count > 0;
        public bool AtCounterSide => (To == Spot.Counter && !Moving) || (Moving && To == Spot.Counter);

        public void Say(string text, bool companion = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            float dur = Math.Max(1.7f, Math.Min(5.2f, 1.1f + text.Length * 0.05f));
            _queue.Enqueue(new Speech { Text = text, ByCompanion = companion, Duration = dur });
        }

        /// <summary>Advances the current line; returns true when all lines just finished.</summary>
        public bool TickLines(float dt)
        {
            if (Line == null)
            {
                if (_queue.Count == 0) return false;
                Line = _queue.Dequeue();
                LineElapsed = 0f;
                return false;
            }
            LineElapsed += dt;
            if (LineElapsed < Line.Duration) return false;
            Line = null;
            if (_queue.Count > 0)
            {
                Line = _queue.Dequeue();
                LineElapsed = 0f;
                return false;
            }
            return true;
        }

        /// <summary>Player clicked the bubble.</summary>
        public void SkipLine()
        {
            if (Line != null) LineElapsed = Math.Max(LineElapsed, Line.Duration);
        }

        public void ClearLines()
        {
            _queue.Clear();
            Line = null;
        }
    }

    /// <summary>Cash a customer left on the counter. The player clicks it to put it away.</summary>
    public sealed class CashPile
    {
        public int Id;
        public int Amount;
        public int OrderId;
        public string From = "";
    }
}
