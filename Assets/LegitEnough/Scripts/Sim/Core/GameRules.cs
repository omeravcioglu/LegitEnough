namespace LegitEnough.Sim
{
    public enum Edition { Full, Demo }

    /// <summary>
    /// Which game this session is: the full campaign or the short guided demo. The full game
    /// is the default everywhere; every demo-only behaviour checks <see cref="IsDemo"/>.
    /// </summary>
    public sealed class GameRules
    {
        public Edition Edition = Edition.Full;
        public bool IsDemo => Edition == Edition.Demo;
        /// <summary>The demo ends after this day's report.</summary>
        public int LastDay = 2;
        /// <summary>Guided Day 1 in the demo (restricted replies, a printer jam on purpose).</summary>
        public bool Tutorial = true;
        /// <summary>Highest equipment tier for sale. Tier 3 is full-game only in the demo.</summary>
        public int MaxEquipmentTier = 3;

        /// <summary>The countries the demo can play. The other five stay visible but locked.</summary>
        public static readonly string[] DemoCountries =
        {
            Countries.France, Countries.Turkey, Countries.Germany, Countries.Italy, Countries.UnitedStates,
        };

        public static GameRules Full() => new GameRules();
        public static GameRules Demo() => new GameRules { Edition = Edition.Demo, MaxEquipmentTier = 2 };
    }
}
