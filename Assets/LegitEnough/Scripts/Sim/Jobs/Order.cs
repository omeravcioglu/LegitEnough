using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum OrderStatus
    {
        /// <summary>Accepted; no photo kept yet.</summary>
        AwaitingPhoto,
        /// <summary>Photo exists; document not bound yet.</summary>
        InProduction,
        /// <summary>Bound booklet on the desk.</summary>
        Ready,
        Delivered,
        Cancelled,
        Expired,
    }

    public sealed class Order
    {
        public int Id;
        public int CustomerId;
        public string CountryId;
        public DocType DocType = DocType.Passport;
        public int Price;
        public bool Urgent;
        public GameTime Created;
        public GameTime Deadline;
        public bool WaitsInShop = true;
        public OrderStatus Status = OrderStatus.AwaitingPhoto;
        public DocumentDraft Draft;
        public FinishedDocument Document;
        public int PagesPrinted;
        public int Redos;
        /// <summary>Demo FIX IT over a photo problem: a new photo must be kept before the job can go on.</summary>
        public bool NeedsRetake;
        public QualityReport Quality;
        public OutcomeKind Outcome = OutcomeKind.Pending;
        public int Paid;
        public GameTime? DeliveredAt;
        /// <summary>Day the consequence message lands (0 = not scheduled).</summary>
        public int OutcomeDay;

        public bool IsOpen => Status == OrderStatus.AwaitingPhoto || Status == OrderStatus.InProduction || Status == OrderStatus.Ready;
        public bool IsLate(GameTime now) => IsOpen && now > Deadline;
    }

    public sealed class OrderBook
    {
        readonly List<Order> _orders = new List<Order>();
        int _nextId = 1;

        public IReadOnlyList<Order> All => _orders;

        public Order Create(JobRequest req, int price, GameTime now)
        {
            var o = new Order
            {
                Id = _nextId++,
                CustomerId = req.CustomerId,
                CountryId = req.CountryId,
                DocType = req.DocType,
                Price = price,
                Urgent = req.Urgent,
                Created = now,
                Deadline = req.Deadline,
                WaitsInShop = req.WaitsInShop,
            };
            o.Draft = new DocumentDraft { OrderId = o.Id };
            _orders.Add(o);
            return o;
        }

        public Order Get(int id) => _orders.Find(o => o.Id == id);

        public IEnumerable<Order> Open()
        {
            foreach (var o in _orders) if (o.IsOpen) yield return o;
        }

        public Order OpenFor(int customerId) => _orders.Find(o => o.IsOpen && o.CustomerId == customerId);
    }

    /// <summary>Kept photographs, by id.</summary>
    public sealed class PhotoLibrary
    {
        readonly Dictionary<int, PhotoCapture> _photos = new Dictionary<int, PhotoCapture>();
        int _nextId = 1;

        public int NextId() => _nextId++;

        public void Keep(PhotoCapture photo)
        {
            photo.Kept = true;
            _photos[photo.Id] = photo;
        }

        public PhotoCapture Get(int? id) => id.HasValue && _photos.TryGetValue(id.Value, out var p) ? p : null;

        public IEnumerable<PhotoCapture> All => _photos.Values;

        public List<PhotoCapture> ForCustomer(int customerId)
        {
            var list = new List<PhotoCapture>();
            foreach (var p in _photos.Values) if (p.CustomerId == customerId) list.Add(p);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }
    }
}
