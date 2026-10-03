using System;
using System.IO;
using UnityEngine;

namespace Ricochet.EditorTools
{
    /// <summary>
    /// Generates every game sound procedurally and writes 16-bit mono WAV files.
    /// All audio in the project is therefore original work (no licences needed).
    /// Each sound is a short recipe of oscillators, noise, pitch sweeps and envelopes.
    /// </summary>
    public static class SfxSynth
    {
        const int Rate = 22050;
        static System.Random s_Rng;

        static float Noise() => (float)(s_Rng.NextDouble() * 2.0 - 1.0);
        static float Sine(double phase) => (float)Math.Sin(phase * Math.PI * 2.0);
        static float Saw(double phase) => (float)(2.0 * (phase - Math.Floor(phase + 0.5)));
        static float Square(double phase) => phase - Math.Floor(phase) < 0.5 ? 1f : -1f;
        static float Tri(double phase) => 1f - 4f * Mathf.Abs((float)(phase - Math.Floor(phase + 0.5)));

        /// <summary>Attack-decay envelope: quick fade in, exponential fade out.</summary>
        static float Env(float t, float length, float attack = 0.005f, float curve = 3f)
        {
            if (t < attack) return t / attack;
            float x = Mathf.Clamp01((t - attack) / Mathf.Max(0.0001f, length - attack));
            return Mathf.Pow(1f - x, curve);
        }

        delegate float Voice(float t, float length);

        static float[] Render(float length, Voice voice)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = voice(i / (float)Rate, length);
            return data;
        }

        /// <summary>Oscillator with a pitch that sweeps exponentially from f0 to f1.</summary>
        static float[] Sweep(float length, float f0, float f1, Func<double, float> wave, float curve = 3f, float noise = 0f, float volume = 0.8f)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float k = t / length;
                float f = f0 * Mathf.Pow(f1 / f0, k);
                phase += f / Rate;
                float s = wave(phase) * (1f - noise) + Noise() * noise;
                data[i] = s * Env(t, length, 0.004f, curve) * volume;
            }
            return data;
        }

        static float[] Mix(params float[][] parts)
        {
            int n = 0;
            foreach (var p in parts) n = Mathf.Max(n, p.Length);
            var o = new float[n];
            foreach (var p in parts)
                for (int i = 0; i < p.Length; i++) o[i] += p[i];
            return o;
        }

        static float[] Delay(float[] src, float seconds)
        {
            int off = Mathf.RoundToInt(seconds * Rate);
            var o = new float[src.Length + off];
            Array.Copy(src, 0, o, off, src.Length);
            return o;
        }

        static float[] LowPass(float[] d, float amount)
        {
            float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                y += (d[i] - y) * amount;
                d[i] = y;
            }
            return d;
        }

        static float[] Normalize(float[] d, float peak = 0.9f)
        {
            float max = 0.0001f;
            foreach (var s in d) max = Mathf.Max(max, Mathf.Abs(s));
            float g = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
            return d;
        }

        static float[] Notes(float[] freqs, float noteLength, Func<double, float> wave, float volume = 0.5f)
        {
            var parts = new float[freqs.Length][];
            for (int i = 0; i < freqs.Length; i++)
                parts[i] = Delay(Sweep(noteLength * 1.6f, freqs[i], freqs[i] * 1.001f, wave, 2.5f, 0f, volume), noteLength * i);
            return Mix(parts);
        }

        // ---------------- Recipes ----------------

        public static void GenerateAll(string folder)
        {
            s_Rng = new System.Random(1234);
            Directory.CreateDirectory(folder);

            Write(folder, "SFX_PlayerShoot", Normalize(Mix(
                Sweep(0.16f, 1800f, 260f, Square, 2.5f, 0.05f, 0.5f),
                Sweep(0.12f, 2600f, 600f, Sine, 3f, 0f, 0.4f)), 0.8f));

            Write(folder, "SFX_PlayerDeath", Normalize(LowPass(Mix(
                Sweep(1.4f, 420f, 38f, Saw, 1.2f, 0.25f, 0.7f),
                Sweep(1.0f, 210f, 30f, Square, 1.5f, 0f, 0.3f)), 0.25f)));

            Write(folder, "SFX_EnemySpawn", Normalize(LowPass(Mix(
                Render(0.8f, (t, l) => Noise() * Env(t, l, 0.15f, 1.5f) * 0.8f),
                Sweep(0.8f, 55f, 140f, Saw, 1.2f, 0f, 0.6f),
                Render(0.8f, (t, l) => Sine(t * 90f) * Sine(t * 7f) * Env(t, l, 0.2f, 1.2f) * 0.5f)), 0.12f)));

            Write(folder, "SFX_SpitterShoot", Normalize(Mix(
                LowPass(Render(0.3f, (t, l) => Noise() * Env(t, l, 0.01f, 2f)), 0.3f),
                Sweep(0.3f, 380f, 110f, (p) => Sine(p) * (0.6f + 0.4f * Sine(p * 0.13)), 2f, 0f, 0.7f))));

            Write(folder, "SFX_WalkerAttackHit", Normalize(Mix(
                Sweep(0.22f, 140f, 45f, Sine, 2f, 0f, 1f),
                Render(0.08f, (t, l) => Noise() * Env(t, l, 0.001f, 2f) * 0.7f))));

            Write(folder, "SFX_LaserBounce", Normalize(Mix(
                Sweep(0.18f, 2300f, 2100f, Sine, 4f, 0f, 0.5f),
                Sweep(0.14f, 3450f, 3300f, Sine, 5f, 0f, 0.3f)), 0.7f));

            Write(folder, "SFX_EnemyHit", Normalize(Mix(
                Sweep(0.08f, 650f, 250f, Square, 3f, 0.3f, 0.6f),
                Render(0.04f, (t, l) => Noise() * Env(t, l, 0.001f, 2f) * 0.5f)), 0.75f));

            Write(folder, "SFX_EnemyDeath", Normalize(LowPass(Mix(
                Render(0.7f, (t, l) =>
                {
                    float f = Mathf.Lerp(150f, 70f, t / l) * (1f + 0.05f * Sine(t * 8f));
                    return Saw(t * f) * Env(t, l, 0.05f, 1.5f) * 0.7f;
                }),
                Render(0.7f, (t, l) => Noise() * Env(t, l, 0.02f, 2f) * 0.3f)), 0.2f)));

            Write(folder, "SFX_PlayerHurt", Normalize(LowPass(Mix(
                Sweep(0.22f, 240f, 140f, Square, 2f, 0.15f, 0.7f),
                Render(0.1f, (t, l) => Noise() * Env(t, l, 0.001f, 2f) * 0.4f)), 0.35f), 0.85f));

            Write(folder, "SFX_CardPickup", Normalize(Notes(new[] { 523f, 659f, 784f, 1046f }, 0.07f, Tri), 0.8f));

            Write(folder, "SFX_MirrorPlace", Normalize(Render(0.5f, (t, l) =>
                (Sine(t * 880f) + Sine(t * 1320f) * 0.6f + Sine(t * 1760f) * 0.3f) * (0.6f + 0.4f * Sine(t * 18f)) * Env(t, l, 0.01f, 2f)), 0.6f));

            Write(folder, "SFX_Overheat", Normalize(Mix(
                LowPass(Render(0.7f, (t, l) => Noise() * Env(t, l, 0.01f, 1.2f)), 0.6f),
                Sweep(0.3f, 300f, 200f, Square, 2f, 0f, 0.3f)), 0.7f));

            Write(folder, "SFX_CountdownBeep", Normalize(Sweep(0.15f, 880f, 880f, Sine, 2f, 0f, 0.8f), 0.7f));
            Write(folder, "SFX_CountdownGo", Normalize(Mix(
                Sweep(0.45f, 1320f, 1320f, Sine, 2f, 0f, 0.6f),
                Sweep(0.45f, 660f, 660f, Square, 2f, 0f, 0.2f)), 0.8f));

            Write(folder, "SFX_RoundWin", Normalize(Notes(new[] { 523f, 659f, 784f, 1046f, 1318f }, 0.12f, Square, 0.3f), 0.8f));

            Write(folder, "SFX_UIClick", Normalize(Sweep(0.035f, 2200f, 1500f, Sine, 4f, 0.1f, 0.8f), 0.5f));

            Write(folder, "MUS_Loop", Normalize(Music(), 0.8f));
        }

        /// <summary>16 s seamless synth loop at 120 BPM: bass line + arpeggio + soft kick.</summary>
        static float[] Music()
        {
            const float bpm = 120f;
            const float beat = 60f / bpm;
            const int bars = 8;
            float length = bars * 4 * beat;
            int n = Mathf.RoundToInt(length * Rate);
            var data = new float[n];

            float[] roots = { 55f, 55f, 65.41f, 49f, 55f, 55f, 73.42f, 61.74f }; // A A C G A A D B
            float[] arp = { 1f, 1.5f, 2f, 3f, 2f, 1.5f, 2f, 2.5f };

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int bar = Mathf.FloorToInt(t / (4 * beat)) % bars;
                float root = roots[bar];

                // Bass: eighth notes
                float eighth = beat * 0.5f;
                float te = t % eighth;
                float bass = Saw(t * root) * Mathf.Exp(-te * 6f) * 0.35f;

                // Arpeggio: sixteenth notes
                float sixteenth = beat * 0.25f;
                int step = Mathf.FloorToInt(t / sixteenth) % arp.Length;
                float ts = t % sixteenth;
                float arpVoice = Tri(t * root * 4f * arp[step]) * Mathf.Exp(-ts * 14f) * 0.18f;

                // Kick on every beat
                float tb = t % beat;
                float kick = Sine(tb * (50f + 120f * Mathf.Exp(-tb * 30f))) * Mathf.Exp(-tb * 12f) * 0.5f;

                // Hi-hat on off-beats
                float hat = (tb > beat * 0.5f && tb < beat * 0.5f + 0.03f) ? Noise() * 0.06f : 0f;

                data[i] = bass + arpVoice + kick + hat;
            }
            return LowPass(data, 0.5f);
        }

        // ---------------- WAV writer ----------------

        static void Write(string folder, string name, float[] samples)
        {
            string path = Path.Combine(folder, name + ".wav");
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);       // PCM
                w.Write((short)1);       // mono
                w.Write(Rate);
                w.Write(Rate * 2);       // byte rate
                w.Write((short)2);       // block align
                w.Write((short)16);      // bits
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                foreach (var s in samples)
                    w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
            }
        }
    }
}
