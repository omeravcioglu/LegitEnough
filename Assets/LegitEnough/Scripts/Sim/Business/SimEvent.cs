using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum SimEventType
    {
        DayStarted,
        ShopOpened,
        ClosingTime,
        DayEnded,
        VisitArrived,
        VisitLeft,
        DoorOpened,
        Speech,
        FolderPlaced,
        OrderCreated,
        OrderCancelled,
        PhotoKept,
        PrintQueued,
        PagePrinted,
        PrinterJammed,
        DocumentBound,
        DocumentShredded,
        DocumentHandedOver,
        CashPlaced,
        CashCollected,
        MoneySpent,
        MessageArrived,
        Purchase,
        ReputationChanged,
        Toast,
    }

    /// <summary>A notification for the presentation layer (sounds, popups, animations).</summary>
    public readonly struct SimEvent
    {
        public readonly SimEventType Type;
        public readonly int VisitId;
        public readonly int OrderId;
        public readonly int Amount;
        public readonly string Text;

        public SimEvent(SimEventType type, int visitId = 0, int orderId = 0, int amount = 0, string text = "")
        {
            Type = type;
            VisitId = visitId;
            OrderId = orderId;
            Amount = amount;
            Text = text ?? "";
        }

        public override string ToString() => $"{Type} v{VisitId} o{OrderId} {Amount} {Text}";
    }

    /// <summary>End-of-day summary (design doc §44).</summary>
    public sealed class DayReport
    {
        public int Day;
        public int Revenue;
        public int Expenses;
        public int Profit => Revenue - Expenses;
        public int CashEnd;
        public int Customers;
        public int JobsAccepted;
        public int JobsCompleted;
        public int OrdersOpen;
        public int MessagesReceived;
        public float ReputationStart;
        public float ReputationEnd;
        public float HeatStart;
        public float HeatEnd;
        public readonly List<KeyValuePair<string, int>> Lines = new List<KeyValuePair<string, int>>();
    }
}
