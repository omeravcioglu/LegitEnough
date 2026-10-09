using LegitEnough.Sim;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// A full-stage layer on top of the shop: computer, handbook, camera, papers, reports.
    /// Overlays stack; ESC closes the top one.
    /// </summary>
    public abstract class Overlay
    {
        protected readonly GameRoot Game;
        public readonly VisualElement Root;

        protected GameSession S => Game.Session;

        protected Overlay(GameRoot game, string classes)
        {
            Game = game;
            Root = Ui.El("le-overlay " + classes);
            Root.At(0, 0, GameRoot.StageWidth, GameRoot.StageHeight);
        }

        /// <summary>True for menus and transition cards: the shop clock stops.</summary>
        public virtual bool PausesGame => false;

        public virtual void Tick(float dt) { }

        /// <summary>Return true if ESC was consumed.</summary>
        public virtual bool OnEscape()
        {
            Close();
            return true;
        }

        public void Close() => Game.CloseOverlay(this);

        public virtual void OnOpened() { }

        public virtual void OnClosed() { }
    }
}
