using System;
using System.Collections.Generic;

namespace LegitEnough.Sim
{
    /// <summary>The desk printer. Pages print one at a time in real time and land in the tray.</summary>
    public sealed class Printer
    {
        readonly List<PrintedPage> _queue = new List<PrintedPage>();
        float _jamAt = 2f;

        public EquipmentDef Model;
        /// <summary>1 = healthy; a broken printer runs slower until repaired.</summary>
        public float Slowdown = 1f;
        /// <summary>False during a power cut.</summary>
        public bool Powered = true;
        public PrintedPage Printing { get; private set; }
        /// <summary>0..1 progress of the page currently printing.</summary>
        public float Progress { get; private set; }
        public bool Jammed { get; private set; }
        public readonly List<PrintedPage> Tray = new List<PrintedPage>();

        public Printer(EquipmentDef model)
        {
            Model = model;
        }

        public int Queued => _queue.Count;
        public bool Busy => Printing != null || _queue.Count > 0;

        public void Enqueue(PrintedPage page) => _queue.Add(page);

        /// <summary>Returns a page that just finished printing this tick, if any.</summary>
        public PrintedPage Tick(float dt, Rng rng)
        {
            if (Jammed || !Powered || dt <= 0f) return null;
            if (Printing == null)
            {
                if (_queue.Count == 0) return null;
                Printing = _queue[0];
                _queue.RemoveAt(0);
                Progress = 0f;
                _jamAt = rng.Chance(Model.JamChance) ? rng.Range(0.25f, 0.8f) : 2f;
            }

            Progress += dt / (Model.PrintSeconds * Math.Max(1f, Slowdown));
            if (Progress >= _jamAt)
            {
                Progress = _jamAt;
                _jamAt = 2f;
                Jammed = true;
                return null;
            }
            if (Progress < 1f) return null;

            var done = Printing;
            Tray.Add(done);
            Printing = null;
            Progress = 0f;
            return done;
        }

        /// <summary>Yank the paper out and slap the side panel. Half the time the page survives cleanly.</summary>
        public void ClearJam(Rng rng)
        {
            if (!Jammed) return;
            Jammed = false;
            if (Printing != null && rng.Chance(0.5f)) Printing.Smudged = true;
        }

        public PrintedPage TakeFromTray(int pageId)
        {
            int i = Tray.FindIndex(p => p.Id == pageId);
            if (i < 0) return null;
            var p = Tray[i];
            Tray.RemoveAt(i);
            return p;
        }
    }
}
