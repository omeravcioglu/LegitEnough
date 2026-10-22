using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LegitEnough.Game
{
    /// <summary>
    /// The McHunter Studio ident at boot: the logo builds itself in over black (the trailer's
    /// timing, <see cref="StudioIdent"/>), fades, then the black lifts off the title underneath.
    /// A click, any key or ESC skips it. It sits above the title, so the harnesses' "Top is
    /// TitleView" holds as soon as it closes; GameRoot never opens it in batch mode.
    /// </summary>
    public sealed class SplashView : Overlay
    {
        const int Scale = 8;
        // The logo fades over the trailer's frames 120-138, then the black lifts off the title.
        const float LogoFadeFrom = 120f / StudioIdent.Fps;
        const float LogoFadeTo = 138f / StudioIdent.Fps;
        const float End = 2.6f;
        // A skip jumps to the last part of the fade instead of cutting.
        const float SkipTo = End - 0.22f;

        readonly StudioIdent _ident = new StudioIdent();
        readonly PixelImage _logo;
        float _t;
        int _frame = -1;
        int _skipFrame = -1;

        public SplashView(GameRoot game) : base(game, "le-splash")
        {
            Root.name = "splash";
            Root.pickingMode = PickingMode.Position;
            int w = DemoArt.StudioW * Scale, h = DemoArt.StudioH * Scale;
            _logo = new PixelImage("le-splash__logo").At(Mathf.Round((GameRoot.StageWidth - w) * 0.5f), Mathf.Round((GameRoot.StageHeight - h) * 0.5f), w, h);
            Root.Add(_logo);
            _logo.Upload(_ident.Frame(0));
            Root.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopPropagation();
                Skip();
            });
        }

        public override bool PausesGame => true;

        public override bool OnEscape()
        {
            Skip();
            return true;
        }

        void Skip()
        {
            // ESC reaches us twice in one frame (GameRoot, then "any key" here): count it once.
            if (_skipFrame == Time.frameCount) return;
            _skipFrame = Time.frameCount;
            if (_t >= SkipTo) Close();
            else _t = SkipTo;
        }

        public override void Tick(float dt)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) Skip();
            if (Game.Top == null || Game.Find<SplashView>() != this) return;

            _t += dt;
            if (_t >= End)
            {
                Close();
                return;
            }

            int frame = Mathf.Min((int)(_t * StudioIdent.Fps), StudioIdent.Frames - 1);
            if (frame != _frame)
            {
                Cues(_frame, frame);
                _frame = frame;
                _logo.Upload(_ident.Frame(frame));
            }
            _logo.style.opacity = 1f - Mathf.Clamp01((_t - LogoFadeFrom) / (LogoFadeTo - LogoFadeFrom));
            Root.style.opacity = 1f - Mathf.Clamp01((_t - LogoFadeTo) / (End - LogoFadeTo));
        }

        /// <summary>The ident's sounds for every cue frame passed since the last tick (a skip plays none).</summary>
        void Cues(int from, int to)
        {
            if (to - from > 12) return;
            for (int f = from + 1; f <= to; f++)
            {
                if (f == StudioIdent.DotFrame) Sfx.Play(SfxId.Pop, 0.7f);
                if (f == StudioIdent.SmallDotFrame) Sfx.Play(SfxId.Pop, 0.5f, 1.3f);
                for (int i = 0; i < StudioIdent.LetterFrames.Length; i++)
                    if (f == StudioIdent.LetterFrames[i]) Sfx.Play(SfxId.Blip, 0.35f, 1f + i * 0.08f);
            }
        }
    }
}
