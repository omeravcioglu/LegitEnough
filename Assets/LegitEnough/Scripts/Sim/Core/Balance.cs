namespace LegitEnough.Sim
{
    /// <summary>
    /// Every tuning number in one place. Change these freely while balancing; nothing else
    /// in the simulation hard-codes economy, timing or scoring values.
    /// </summary>
    public static class Balance
    {
        // ---------- Time ----------
        /// <summary>9 business hours at 0.75 game-min/sec ≈ 12 real minutes per day.</summary>
        public const float GameMinutesPerRealSecond = 0.75f;
        public const int MorningMinute = 8 * 60 + 30;
        public const int OpenMinute = 9 * 60;
        public const int CloseMinute = 18 * 60;
        public const int LastArrivalMinute = 17 * 60 + 15;

        // ---------- Money ----------
        public const int StartingCash = 200;
        public const int DailyRent = 110;
        public const int DailyUtilities = 25;
        public const int BlankBookletCost = 30;
        public const int PrintCost = 8;
        public const int DayOffCost = 0;
        public const int PrinterRepairCost = 150;
        public const int HandbookUpdateCost = 80;
        /// <summary>A broken printer takes this many times longer per page.</summary>
        public const float BrokenPrinterSlowdown = 3f;

        // ---------- Work time costs (game minutes) ----------
        public const float BindMinutes = 6f;
        public const float ConversationMinutes = 4f;

        // ---------- Movement (real seconds between spots) ----------
        public const float WalkSecondsShort = 2.2f;
        public const float WalkSecondsLong = 3.6f;

        // ---------- Patience ----------
        /// <summary>Real seconds of tolerance at Patience=0 and Patience=1.</summary>
        public const float PatienceSecondsMin = 150f;
        public const float PatienceSecondsMax = 520f;
        public const float WaitingPatienceDrainPerSecond = 1f;
        public const float PhotoPatienceDrainPerSecond = 1.2f;
        public const float CommandPatienceCost = 7f;
        public const float DragPatienceCost = 2.5f;
        /// <summary>Calling a customer back for another photo costs this share of their patience.</summary>
        public const float RetakePatienceCost = 0.1f;
        /// <summary>Below this much patience they refuse another photo.</summary>
        public const float RetakeRefuseBelow = 0.25f;

        // ---------- Negotiation ----------
        public const float AskSmallMultiplier = 1.33f;
        public const float AskBigMultiplier = 1.66f;
        public const float UrgentMultiplier = 3.0f;

        // ---------- Photo ----------
        /// <summary>Frame height in head-heights at zoom 1 (head covers 25% of the frame).</summary>
        public const float FrameHeadHeightsAtZoom1 = 4f;
        /// <summary>Sensor aspect (width / height). The viewfinder is portrait 3:4.</summary>
        public const float SensorAspect = 0.75f;
        /// <summary>Passport photo aspect (width / height), 35 x 45.</summary>
        public const float PhotoAspect = 35f / 45f;
        public const float BlinkClosedThreshold = 0.75f;
        public const float BlinkHalfThreshold = 0.35f;

        // ---------- Quality → outcome ----------
        public const float OutcomeMidpoint = 62f;
        public const float OutcomeSpread = 7f;
        public const float UnknownOutcomeChance = 0.08f;

        // ---------- Reputation / heat ----------
        public const int ReputationMax = 1000;
        public const float HeatDailyDecay = 4f;
        public const float HeatQuietDayBonusDecay = 6f;
        public const float HeatPerJobBase = 1f;
        public const float HeatPerJobRiskScale = 6f;
        public const float HeatOnFailure = 10f;
    }
}
