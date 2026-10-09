namespace LegitEnough.Sim
{
    public enum Bargain { Accepted, Countered, WalkedOut, Declined }

    public readonly struct NegotiationReply
    {
        public readonly Bargain Result;
        public readonly int Amount;

        public NegotiationReply(Bargain result, int amount)
        {
            Result = result;
            Amount = amount;
        }
    }

    /// <summary>
    /// One haggle over a job (design doc §29). The customer has a hidden reservation price;
    /// personality decides how they react to being pushed past it.
    /// </summary>
    public sealed class Negotiation
    {
        readonly CustomerProfile _customer;
        readonly int _reservation;

        public readonly int Offer;
        public int Rounds { get; private set; }
        /// <summary>Set once the customer has named their "final offer".</summary>
        public int? FinalOffer { get; private set; }
        public bool IsOver { get; private set; }
        public int AgreedPrice { get; private set; }

        public Negotiation(CustomerProfile customer, int offer, int reservation)
        {
            _customer = customer;
            Offer = offer;
            _reservation = reservation;
        }

        /// <summary>The amount currently on the table.</summary>
        public int Current => FinalOffer ?? Offer;
        public int SmallAsk => Pricing.RoundTo((int)(Offer * Balance.AskSmallMultiplier), 25);
        public int BigAsk => Pricing.RoundTo((int)(Offer * Balance.AskBigMultiplier), 25);

        public NegotiationReply Accept()
        {
            AgreedPrice = Current;
            IsOver = true;
            return new NegotiationReply(Bargain.Accepted, AgreedPrice);
        }

        public NegotiationReply Decline()
        {
            IsOver = true;
            return new NegotiationReply(Bargain.Declined, 0);
        }

        public NegotiationReply Ask(int amount, Rng rng)
        {
            Rounds++;
            var s = _customer.Stats;
            if (amount <= _reservation)
                return Close(amount);

            if (FinalOffer.HasValue)
            {
                // Pushing past "final offer".
                if (s.Desperation > 0.75f && rng.Chance(0.5f)) return Close(FinalOffer.Value);
                IsOver = true;
                return new NegotiationReply(Bargain.WalkedOut, 0);
            }

            float tolerance = 1.25f + 0.3f * s.Desperation;
            if (amount <= _reservation * tolerance || s.Desperation > 0.65f || rng.Chance(0.2f + 0.4f * s.Patience))
            {
                FinalOffer = _reservation;
                return new NegotiationReply(Bargain.Countered, _reservation);
            }

            IsOver = true;
            return new NegotiationReply(Bargain.WalkedOut, 0);
        }

        NegotiationReply Close(int amount)
        {
            AgreedPrice = amount;
            IsOver = true;
            return new NegotiationReply(Bargain.Accepted, amount);
        }
    }
}
