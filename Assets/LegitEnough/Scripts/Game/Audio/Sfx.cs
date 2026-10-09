using System;
using System.Collections.Generic;
using UnityEngine;

namespace LegitEnough.Game
{
    public enum SfxId
    {
        Click,
        DoorBell,
        DoorOpen,
        Step,
        Shutter,
        Paper,
        Stamp,
        Cash,
        Key,
        Notify,
        Blip,
        Error,
        PrinterDone,
        Jam,
        Pop,
    }

    /// <summary>
    /// Procedurally synthesized placeholder sounds (design doc §53). Swap any clip for a real
    /// recording later by changing <see cref="SfxPlayer.Build"/>; callers only use <see cref="Play"/>.
    /// </summary>
    public static class Sfx
    {
        internal static SfxPlayer Player;

        public static void Play(SfxId id, float volume = 1f, float pitch = 1f)
        {
            if (Player != null) Player.PlayOneShot(id, volume, pitch);
        }

        /// <summary>A short burst of "voice" blips for a line of dialogue.</summary>
        public static void Voice(string text, float voicePitch)
        {
            if (Player == null || string.IsNullOrEmpty(text)) return;
            int blips = Mathf.Clamp(text.Length / 7, 2, 9);
            Player.QueueVoice(blips, voicePitch);
        }

        public static void SetPrinterRunning(bool running)
        {
            if (Player != null) Player.SetLoop(SfxPlayer.PrinterLoop, running, 0.35f);
        }
    }

    public sealed class SfxPlayer : MonoBehaviour
    {
        public const int PrinterLoop = 2;
        const int Rate = 44100;

        readonly Dictionary<SfxId, AudioClip> _clips = new Dictionary<SfxId, AudioClip>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        readonly AudioSource[] _loops = new AudioSource[3];
        readonly System.Random _rand = new System.Random(7);
        int _next;
        int _voiceLeft;
        float _voicePitch = 1f;
        float _voiceTimer;

        void Awake()
        {
            Build();
            for (int i = 0; i < 10; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _pool.Add(s);
            }
            _loops[0] = MakeLoop(Clip("rain", Rain(4f)), 0.22f);
            _loops[1] = MakeLoop(Clip("hum", Hum(2f)), 0.05f);
            _loops[2] = MakeLoop(Clip("printer", PrinterNoise(1f)), 0f);
            _loops[0].Play();
            _loops[1].Play();
            Sfx.Player = this;
        }

        void OnDestroy()
        {
            if (Sfx.Player == this) Sfx.Player = null;
        }

        AudioSource MakeLoop(AudioClip clip, float volume)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = volume;
            s.playOnAwake = false;
            return s;
        }

        public void SetLoop(int index, bool on, float volume)
        {
            var s = _loops[index];
            if (on)
            {
                s.volume = volume;
                if (!s.isPlaying) s.Play();
            }
            else if (s.isPlaying)
            {
                s.Stop();
            }
        }

        public void PlayOneShot(SfxId id, float volume, float pitch)
        {
            if (!_clips.TryGetValue(id, out var clip)) return;
            var s = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            s.pitch = pitch;
            s.volume = volume;
            s.clip = clip;
            s.Play();
        }

        public void QueueVoice(int blips, float pitch)
        {
            _voiceLeft = blips;
            _voicePitch = pitch;
            _voiceTimer = 0f;
        }

        void Update()
        {
            if (_voiceLeft <= 0) return;
            _voiceTimer -= Time.unscaledDeltaTime;
            if (_voiceTimer > 0f) return;
            _voiceLeft--;
            _voiceTimer = 0.07f + (float)_rand.NextDouble() * 0.05f;
            PlayOneShot(SfxId.Blip, 0.25f, _voicePitch * (0.85f + (float)_rand.NextDouble() * 0.35f));
        }

        // ------------------------------------------------------------------ synthesis

        void Build()
        {
            _clips[SfxId.Click] = Clip("click", Mix(Noise(0.012f, 0.6f, 0.002f), Tone(1800f, 0.02f, 0.004f, 0.3f)));
            _clips[SfxId.DoorBell] = Clip("bell", Mix(Bell(1318.5f, 1.4f, 0.5f), Delay(Bell(1046.5f, 1.6f, 0.5f), 0.16f)));
            _clips[SfxId.DoorOpen] = Clip("door", Mix(LowNoise(0.35f, 0.06f, 0.08f), Tone(85f, 0.3f, 0.12f, 0.4f)));
            _clips[SfxId.Step] = Clip("step", LowNoise(0.08f, 0.15f, 0.02f));
            _clips[SfxId.Shutter] = Clip("shutter", Mix(Noise(0.018f, 0.9f, 0.004f), Delay(Noise(0.03f, 0.7f, 0.01f), 0.07f), Delay(Tone(220f, 0.05f, 0.015f, 0.4f), 0.07f)));
            _clips[SfxId.Paper] = Clip("paper", BandNoise(0.25f, 0.5f, 0.08f));
            _clips[SfxId.Stamp] = Clip("stamp", Mix(LowNoise(0.12f, 0.25f, 0.03f), Delay(Noise(0.03f, 0.5f, 0.01f), 0.05f)));
            _clips[SfxId.Cash] = Clip("cash", Mix(Bell(2093f, 0.6f, 0.35f), Delay(Bell(2637f, 0.7f, 0.35f), 0.08f), Noise(0.08f, 0.4f, 0.03f)));
            _clips[SfxId.Key] = Clip("key", Mix(Noise(0.01f, 0.5f, 0.002f), Tone(900f, 0.015f, 0.003f, 0.2f)));
            _clips[SfxId.Notify] = Clip("notify", Mix(Square(880f, 0.09f, 0.25f), Delay(Square(1320f, 0.12f, 0.25f), 0.1f)));
            _clips[SfxId.Blip] = Clip("blip", Triangle(330f, 0.06f, 0.5f));
            _clips[SfxId.Error] = Clip("error", Square(140f, 0.22f, 0.3f));
            _clips[SfxId.PrinterDone] = Clip("printdone", Mix(BandNoise(0.2f, 0.4f, 0.05f), Delay(Tone(660f, 0.08f, 0.02f, 0.3f), 0.18f)));
            _clips[SfxId.Jam] = Clip("jam", Mix(Square(110f, 0.35f, 0.25f), LowNoise(0.35f, 0.3f, 0.1f)));
            _clips[SfxId.Pop] = Clip("pop", Tone(520f, 0.08f, 0.03f, 0.4f));
        }

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, Mathf.Max(1, data.Length), 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static int N(float seconds) => Mathf.Max(1, (int)(seconds * Rate));

        static float[] Tone(float freq, float dur, float decay, float amp)
        {
            var d = new float[N(dur)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * amp * Mathf.Exp(-t / Mathf.Max(0.001f, decay));
            }
            return d;
        }

        static float[] Square(float freq, float dur, float amp)
        {
            var d = new float[N(dur)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(1f - t / dur) * Mathf.Clamp01(t * 200f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) > 0f ? 1f : -1f) * amp * 0.5f * env;
            }
            return d;
        }

        static float[] Triangle(float freq, float dur, float amp)
        {
            var d = new float[N(dur)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float ph = (t * freq) % 1f;
                float tri = 4f * Mathf.Abs(ph - 0.5f) - 1f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));
                d[i] = tri * amp * env;
            }
            return d;
        }

        static float[] Bell(float freq, float dur, float amp)
        {
            var d = new float[N(dur)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 4.5f);
                d[i] = amp * env * (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f
                    + Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t) * 0.25f * Mathf.Exp(-t * 6f)
                    + Mathf.Sin(2f * Mathf.PI * freq * 5.4f * t) * 0.12f * Mathf.Exp(-t * 9f));
            }
            return d;
        }

        float[] Noise(float dur, float amp, float decay)
        {
            var d = new float[N(dur)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = ((float)_rand.NextDouble() * 2f - 1f) * amp * Mathf.Exp(-t / Mathf.Max(0.0005f, decay));
            }
            return d;
        }

        float[] LowNoise(float dur, float amp, float decay)
        {
            var d = Noise(dur, amp * 3f, decay);
            float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                y += (d[i] - y) * 0.05f;
                d[i] = y;
            }
            return d;
        }

        float[] BandNoise(float dur, float amp, float decay)
        {
            var d = new float[N(dur)];
            float lp = 0f, prev = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float n = (float)_rand.NextDouble() * 2f - 1f;
                lp += (n - lp) * 0.3f;
                float hp = lp - prev;
                prev = lp;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur)) * Mathf.Exp(-t / Mathf.Max(0.01f, decay * 3f));
                d[i] = hp * amp * 2.5f * env;
            }
            return d;
        }

        float[] Rain(float dur)
        {
            var d = new float[N(dur)];
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float n = (float)_rand.NextDouble() * 2f - 1f;
                lp += (n - lp) * 0.12f;
                float drop = _rand.NextDouble() < 0.0008 ? ((float)_rand.NextDouble() * 0.6f) : 0f;
                d[i] = lp * 0.35f + drop;
            }
            Crossfade(d, N(0.2f));
            return d;
        }

        static float[] Hum(float dur)
        {
            var d = new float[N(dur)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.1f;
            }
            return d;
        }

        float[] PrinterNoise(float dur)
        {
            var d = new float[N(dur)];
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float n = (float)_rand.NextDouble() * 2f - 1f;
                lp += (n - lp) * 0.2f;
                float chop = 0.6f + 0.4f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 8f * t));
                d[i] = lp * 0.5f * chop + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.08f;
            }
            Crossfade(d, N(0.05f));
            return d;
        }

        static void Crossfade(float[] d, int n)
        {
            n = Mathf.Min(n, d.Length / 2);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                int j = d.Length - n + i;
                d[i] = d[i] * t + d[j] * (1f - t);
            }
        }

        static float[] Delay(float[] src, float seconds)
        {
            int off = N(seconds);
            var d = new float[src.Length + off];
            Array.Copy(src, 0, d, off, src.Length);
            return d;
        }

        static float[] Mix(params float[][] parts)
        {
            int len = 0;
            foreach (var p in parts) len = Mathf.Max(len, p.Length);
            var d = new float[len];
            foreach (var p in parts)
                for (int i = 0; i < p.Length; i++) d[i] += p[i];
            for (int i = 0; i < len; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
            return d;
        }
    }
}
