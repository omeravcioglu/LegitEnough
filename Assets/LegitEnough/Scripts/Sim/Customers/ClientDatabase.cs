using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum ClientStatus { Unknown, Success, Failed, Missing, Returning }

    /// <summary>What the shop computer remembers about a client (design doc §40).</summary>
    public sealed class ClientRecord
    {
        public int CustomerId;
        public int FirstVisitDay;
        public int LastVisitDay;
        public int Visits;
        public int Jobs;
        public int TotalPaid;
        public ClientStatus Status = ClientStatus.Unknown;
        public readonly List<string> Log = new List<string>();
    }

    /// <summary>All customers the world has generated, plus the shop's records about them.</summary>
    public sealed class ClientDatabase
    {
        readonly ulong _worldSeed;
        readonly Dictionary<int, CustomerProfile> _profiles = new Dictionary<int, CustomerProfile>();
        readonly Dictionary<int, ClientRecord> _records = new Dictionary<int, ClientRecord>();
        int _nextGeneratedId = 1;

        public ClientDatabase(ulong worldSeed)
        {
            _worldSeed = worldSeed;
        }

        public IEnumerable<ClientRecord> Records => _records.Values;
        public int Count => _records.Count;

        /// <summary>Allocates a brand-new generated customer.</summary>
        public CustomerProfile CreateNew()
        {
            int id = _nextGeneratedId++;
            var p = CustomerGenerator.Generate(id, _worldSeed);
            _profiles[id] = p;
            return p;
        }

        /// <summary>Registers a handcrafted customer (story characters use ids >= 10000).</summary>
        public void Register(CustomerProfile profile)
        {
            _profiles[profile.Id] = profile;
        }

        public CustomerProfile Get(int id)
        {
            if (_profiles.TryGetValue(id, out var p)) return p;
            p = CustomerGenerator.Generate(id, _worldSeed);
            _profiles[id] = p;
            return p;
        }

        public bool TryGetRecord(int id, out ClientRecord record) => _records.TryGetValue(id, out record);

        public ClientRecord RecordFor(int id, int today)
        {
            if (!_records.TryGetValue(id, out var r))
            {
                r = new ClientRecord { CustomerId = id, FirstVisitDay = today, LastVisitDay = today };
                _records[id] = r;
            }
            return r;
        }

        public void NoteVisit(int id, int today)
        {
            var r = RecordFor(id, today);
            if (r.Visits > 0 && r.LastVisitDay != today) r.Status = r.Status == ClientStatus.Unknown ? ClientStatus.Returning : r.Status;
            r.Visits++;
            r.LastVisitDay = today;
        }
    }
}
