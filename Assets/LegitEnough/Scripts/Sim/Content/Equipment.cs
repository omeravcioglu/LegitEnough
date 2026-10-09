using System.Collections.Generic;

namespace LegitEnough.Sim
{
    public enum EquipmentSlot { Camera, Printer, Computer, PhotoStation }

    /// <summary>One purchasable piece of equipment (design doc §42). Stats unused by a slot stay at defaults.</summary>
    public sealed class EquipmentDef
    {
        public string Id = "";
        public EquipmentSlot Slot;
        public int Tier;
        public string Name = "";
        public int Price;
        public string Blurb = "";

        // Camera
        public float ShutterLag;
        public float MaxZoom = 2f;
        public int SensorHeightPx = 1600;
        public float Noise;

        // Printer
        /// <summary>Real seconds to print one data page.</summary>
        public float PrintSeconds = 14f;
        public float JamChance;

        // Computer
        /// <summary>Editor can import names from the client sheet.</summary>
        public bool ImportsNames;
        /// <summary>Editor converts typed dates into the template's format.</summary>
        public bool AutoFormatsDates;

        // Photo station
        /// <summary>Real seconds to swap the backdrop roll.</summary>
        public float BackdropSwapSeconds = 1.6f;
        public float LightingBonus;
    }

    public static class EquipmentCatalog
    {
        public static readonly List<EquipmentDef> All = new List<EquipmentDef>
        {
            new EquipmentDef
            {
                Id = "cam1", Slot = EquipmentSlot.Camera, Tier = 1, Name = "Snapmatic 2.1MP", Price = 0,
                Blurb = "It takes pictures. Mostly when you press the button.",
                ShutterLag = 0.12f, MaxZoom = 2f, SensorHeightPx = 1600, Noise = 0.35f,
            },
            new EquipmentDef
            {
                Id = "cam2", Slot = EquipmentSlot.Camera, Tier = 2, Name = "Kestrel D40", Price = 1200,
                Blurb = "Faster shutter, longer zoom, cleaner image.",
                ShutterLag = 0.05f, MaxZoom = 2.5f, SensorHeightPx = 2400, Noise = 0.15f,
            },
            new EquipmentDef
            {
                Id = "cam3", Slot = EquipmentSlot.Camera, Tier = 3, Name = "Halden Pro S", Price = 4200,
                Blurb = "Studio grade. Zero lag. Every pore.",
                ShutterLag = 0f, MaxZoom = 3f, SensorHeightPx = 3600, Noise = 0f,
            },
            new EquipmentDef
            {
                Id = "prn1", Slot = EquipmentSlot.Printer, Tier = 1, Name = "Old Inkjet", Price = 0,
                Blurb = "Slow. Loud. Occasionally eats paper.", PrintSeconds = 16f, JamChance = 0.12f,
            },
            new EquipmentDef
            {
                Id = "prn2", Slot = EquipmentSlot.Printer, Tier = 2, Name = "Office LaserJet", Price = 900,
                Blurb = "Twice the speed, rarely jams.", PrintSeconds = 8f, JamChance = 0.03f,
            },
            new EquipmentDef
            {
                Id = "prn3", Slot = EquipmentSlot.Printer, Tier = 3, Name = "Pressline 900", Price = 3400,
                Blurb = "Professional output in seconds.", PrintSeconds = 3.5f, JamChance = 0f,
            },
            new EquipmentDef
            {
                Id = "pc1", Slot = EquipmentSlot.Computer, Tier = 1, Name = "Beige Tower 98", Price = 0,
                Blurb = "Runs Official-ish 98. Barely.",
            },
            new EquipmentDef
            {
                Id = "pc2", Slot = EquipmentSlot.Computer, Tier = 2, Name = "Modern Desktop", Price = 1500,
                Blurb = "Imports names straight from the client sheet.", ImportsNames = true,
            },
            new EquipmentDef
            {
                Id = "pc3", Slot = EquipmentSlot.Computer, Tier = 3, Name = "Workstation XL", Price = 4600,
                Blurb = "Imports names and reformats dates for each template.", ImportsNames = true, AutoFormatsDates = true,
            },
            new EquipmentDef
            {
                Id = "stn1", Slot = EquipmentSlot.PhotoStation, Tier = 1, Name = "Bedsheet Backdrops", Price = 0,
                Blurb = "Four rolls of something. Wrinkly.", BackdropSwapSeconds = 1.6f, LightingBonus = -3f,
            },
            new EquipmentDef
            {
                Id = "stn2", Slot = EquipmentSlot.PhotoStation, Tier = 2, Name = "Adjustable Backdrop Stand", Price = 700,
                Blurb = "Smooth paper, quick swaps.", BackdropSwapSeconds = 0.8f, LightingBonus = 0f,
            },
            new EquipmentDef
            {
                Id = "stn3", Slot = EquipmentSlot.PhotoStation, Tier = 3, Name = "Studio Kit", Price = 2800,
                Blurb = "Softboxes and seamless paper. Photos look expensive.", BackdropSwapSeconds = 0.3f, LightingBonus = 4f,
            },
        };

        public static EquipmentDef Get(string id) => All.Find(e => e.Id == id);

        public static EquipmentDef Starter(EquipmentSlot slot) => All.Find(e => e.Slot == slot && e.Tier == 1);
    }
}
