using LegitEnough.Sim;
using UnityEngine;

namespace LegitEnough.Game
{
    /// <summary>The background music cues (Tools/Music/compose.py writes them).</summary>
    public enum MusicCue
    {
        None,
        /// <summary>Splash and title: the rainy-night loop.</summary>
        Title,
        /// <summary>The shop, morning to report: the laid-back day loop.</summary>
        Shop,
        /// <summary>A rush job on the desk, or someone still waiting after closing time.</summary>
        Busy,
        /// <summary>The demo-end screen: plays once and ends.</summary>
        End,
    }

    /// <summary>
    /// Plays the background music. Picks a cue from the game state every frame and crossfades to
    /// it on two AudioSources. Shop and Busy are the same performance (same length, same tempo,
    /// same bass and comping), so switching between them starts the new one at the old one's
    /// position: the band digs in rather than changing songs. Ducks while a customer talks and
    /// dips under the pause and settings cards. Follows the MUSIC setting; the master volume
    /// (AudioListener.volume) applies on top as it does to every sound.
    /// Batch runs (smoke test, store shots, trailer capture) load and play nothing.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }

        const string Folder = "LegitEnough/Music/";
        static readonly string[] Files = { null, "title_night", "shop_day", "shop_busy", "demo_end" };
        /// <summary>Per-cue trim (the files are mastered within 1.5 LU of each other).</summary>
        static readonly float[] CueGain = { 0f, 1f, 1f, 1f, 1f };

        const float CrossfadeSeconds = 1.5f;
        const float FirstFadeSeconds = 3f;
        const float TalkDuckDb = -4f;
        const float MenuDip = 0.5f;
        /// <summary>A new cue has to be wanted this long before the music follows (no flicker on screen changes).</summary>
        const float SwitchHold = 0.2f;
        /// <summary>After the MUSIC setting changes, the menu dip lifts for a moment so the player hears the real level.</summary>
        const float PreviewSeconds = 2f;

        readonly AudioSource[] _src = new AudioSource[2];
        readonly MusicCue[] _srcCue = new MusicCue[2];
        readonly float[] _weight = new float[2];
        readonly AudioClip[] _clips = new AudioClip[Files.Length];
        readonly bool[] _missing = new bool[Files.Length];
        int _active = -1;
        MusicCue _target = MusicCue.None;
        MusicCue _wanted = MusicCue.None;
        float _wantedFor;
        float _fadeIn = FirstFadeSeconds;
        float _duck = 1f;
        float _dip = 1f;
        float _preview;
        bool _enabled;

        /// <summary>The cue the game state asks for right now (also tracked in batch runs, where nothing plays).</summary>
        public MusicCue Cue => _target;

        void Awake()
        {
            Instance = this;
            _enabled = !Application.isBatchMode;
            if (!_enabled) return;
            for (int i = 0; i < _src.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.volume = 0f;
                s.spatialBlend = 0f;
                s.priority = 0; // never the voice that gets stolen
                s.bypassReverbZones = true;
                s.ignoreListenerPause = true;
                _src[i] = s;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>The MUSIC setting changed: let the player hear it at full level for a moment.</summary>
        public static void OnVolumeChanged()
        {
            if (Instance != null) Instance._preview = PreviewSeconds;
        }

        // ------------------------------------------------------------------ state -> cue

        static MusicCue Choose(GameRoot game)
        {
            if (game == null || game.Session == null) return MusicCue.None;
            if (game.Find<DemoEndView>() != null) return MusicCue.End;
            if (game.Find<SplashView>() != null || game.Find<TitleView>() != null) return MusicCue.Title;
            return IsBusy(game.Session) ? MusicCue.Busy : MusicCue.Shop;
        }

        /// <summary>
        /// A rush job is on the books (Day 2's "I fly tonight", or any urgent order in the full
        /// game), or the shop is past closing with a customer still inside.
        /// </summary>
        static bool IsBusy(GameSession s)
        {
            if (s.Phase == DayPhase.Closed) return false;
            foreach (var o in s.Orders.Open())
                if (o.Urgent) return true;
            if (s.Phase == DayPhase.AfterHours)
            {
                foreach (var v in s.Visits)
                    if (v.Phase != VisitPhase.Gone && !v.Leaving) return true;
            }
            return false;
        }

        static bool SomeoneTalking(GameSession s)
        {
            if (s == null) return false;
            foreach (var v in s.Visits)
                if (v.Phase != VisitPhase.Gone && v.Line != null) return true;
            return false;
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            var game = GameRoot.Instance;
            var want = Choose(game);
            if (!_enabled)
            {
                _target = want;
                return;
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            if (want != _wanted)
            {
                _wanted = want;
                _wantedFor = 0f;
            }
            else
            {
                _wantedFor += dt;
            }
            if (_wanted != _target && (_wantedFor >= SwitchHold || _target == MusicCue.None) && CanSwitch(_wanted)) Switch(_wanted);

            // Crossfade weights.
            for (int i = 0; i < _src.Length; i++)
            {
                var s = _src[i];
                if (s.clip == null) continue;
                bool on = i == _active;
                float rate = dt / (on ? _fadeIn : CrossfadeSeconds);
                _weight[i] = Mathf.MoveTowards(_weight[i], on ? 1f : 0f, rate);
                if (!on && _weight[i] <= 0f)
                {
                    s.Stop();
                    s.clip = null; // release the stream
                    _srcCue[i] = MusicCue.None;
                }
            }

            // Duck under speech, dip under the pause and settings cards.
            bool menu = game != null && (game.Find<PauseView>() != null || game.Find<SettingsView>() != null);
            if (_preview > 0f)
            {
                _preview -= dt;
                menu = false;
            }
            float duckTo = SomeoneTalking(game != null ? game.Session : null) ? Mathf.Pow(10f, TalkDuckDb / 20f) : 1f;
            _duck = Mathf.MoveTowards(_duck, duckTo, dt / (duckTo < _duck ? 0.25f : 0.9f));
            _dip = Mathf.MoveTowards(_dip, menu ? MenuDip : 1f, dt / 0.4f);

            // The pips are closer to even steps for the ear than a straight line: 60% is about -7 dB.
            float level = Mathf.Pow(GameSettings.MusicVolume / 10f, 1.5f);
            for (int i = 0; i < _src.Length; i++)
            {
                var s = _src[i];
                if (s.clip == null) continue;
                float w = _weight[i];
                // Equal-power crossfade.
                float shape = Mathf.Sin(w * Mathf.PI * 0.5f);
                s.volume = level * CueGain[(int)_srcCue[i]] * shape * _duck * _dip;
            }
        }

        /// <summary>
        /// A new cue needs a free source. If the other one is still fading out (two quick screen
        /// changes in a row), wait the moment it takes instead of cutting it off mid-note.
        /// </summary>
        bool CanSwitch(MusicCue want)
        {
            if (want == MusicCue.None) return true;
            for (int i = 0; i < _src.Length; i++)
                if (_srcCue[i] == want && _src[i].clip != null && _src[i].isPlaying) return true;
            for (int i = 0; i < _src.Length; i++)
                if (i != _active && (_src[i].clip == null || _weight[i] <= 0.001f)) return true;
            return false;
        }

        void Switch(MusicCue want)
        {
            var from = _target;
            _target = want;
            _fadeIn = from == MusicCue.None ? FirstFadeSeconds : CrossfadeSeconds;
            if (want == MusicCue.None)
            {
                _active = -1;
                return;
            }
            // Coming back to a cue that is still fading out: just turn it around.
            for (int i = 0; i < _src.Length; i++)
            {
                if (_srcCue[i] == want && _src[i].clip != null && _src[i].isPlaying)
                {
                    _active = i;
                    return;
                }
            }
            var clip = Clip(want);
            if (clip == null)
            {
                _active = -1;
                return;
            }
            int next = -1;
            for (int i = 0; i < _src.Length; i++)
                if (i != _active && (next < 0 || _weight[i] < _weight[next])) next = i;
            int prev = _active;
            var s = _src[next];
            s.Stop();
            s.clip = clip;
            s.loop = want != MusicCue.End;
            _srcCue[next] = want;
            _weight[next] = 0f;
            s.volume = 0f;
            s.Play();
            // Shop <-> Busy: same performance, same length; pick up where the other one is.
            bool paired = (want == MusicCue.Shop || want == MusicCue.Busy) && prev >= 0 &&
                          (_srcCue[prev] == MusicCue.Shop || _srcCue[prev] == MusicCue.Busy) &&
                          _src[prev].clip != null && _src[prev].isPlaying && _src[prev].clip.samples == clip.samples;
            if (paired)
            {
                int pos = _src[prev].timeSamples;
                if (pos > 0 && pos < clip.samples) s.timeSamples = pos;
            }
            _active = next;
        }

        AudioClip Clip(MusicCue cue)
        {
            int i = (int)cue;
            if (_clips[i] != null || _missing[i]) return _clips[i];
            _clips[i] = Resources.Load<AudioClip>(Folder + Files[i]);
            if (_clips[i] == null)
            {
                _missing[i] = true;
                Debug.LogWarning("LegitEnough: music " + Folder + Files[i] + " not found in Resources.");
            }
            return _clips[i];
        }
    }
}
