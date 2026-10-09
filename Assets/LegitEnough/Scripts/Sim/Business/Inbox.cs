using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum MessageKind { Outcome, Referral, System, Story }

    public sealed class Message
    {
        public int Id;
        public GameTime ArrivesAt;
        public string From = "";
        public int? CustomerId;
        public int? OrderId;
        public string Subject = "";
        public string Body = "";
        public MessageKind Kind;
        public bool Read;
    }

    /// <summary>The computer's MESSAGES app. Messages can be scheduled days ahead (delayed consequences).</summary>
    public sealed class Inbox
    {
        readonly List<Message> _delivered = new List<Message>();
        readonly List<Message> _scheduled = new List<Message>();
        int _nextId = 1;

        /// <summary>Newest first.</summary>
        public IReadOnlyList<Message> Messages => _delivered;

        public int Unread
        {
            get
            {
                int n = 0;
                foreach (var m in _delivered) if (!m.Read) n++;
                return n;
            }
        }

        public Message Schedule(Message m)
        {
            m.Id = _nextId++;
            _scheduled.Add(m);
            return m;
        }

        /// <summary>Moves every message whose time has come into the inbox; returns the new ones.</summary>
        public List<Message> DeliverDue(GameTime now)
        {
            var arrived = new List<Message>();
            for (int i = _scheduled.Count - 1; i >= 0; i--)
            {
                var m = _scheduled[i];
                if (m.ArrivesAt <= now)
                {
                    _scheduled.RemoveAt(i);
                    arrived.Add(m);
                }
            }
            arrived.Sort((a, b) => a.ArrivesAt.CompareTo(b.ArrivesAt));
            foreach (var m in arrived) _delivered.Insert(0, m);
            return arrived;
        }

        public Message Get(int id) => _delivered.Find(m => m.Id == id);

        public void MarkRead(int id)
        {
            var m = Get(id);
            if (m != null) m.Read = true;
        }

        public int PendingCount => _scheduled.Count;
    }
}
