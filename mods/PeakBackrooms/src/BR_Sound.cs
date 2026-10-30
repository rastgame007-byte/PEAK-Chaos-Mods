// Синтезированные звуки уровней и монстров (без файлов).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static class Snd2
    {
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(); static System.Random rn = new System.Random(11);
        static float Nz() { return (float)rn.NextDouble() * 2f - 1f; }
        static AudioClip Make(string name, int rate, float len, Func<float, float, float> f, bool loop = false)
        {
            AudioClip c; if (clips.TryGetValue(name, out c) && c != null) return c;
            int n = (int)(rate * len); var d = new float[n]; for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)rate, i / (float)n), -1f, 1f);
            c = AudioClip.Create(name, n, 1, rate, false); c.SetData(d, 0); clips[name] = c; return c;
        }
        public static void Clear() { foreach (var c in clips.Values) if (c != null) UnityEngine.Object.Destroy(c); clips.Clear(); }

        public static AudioClip Get(string k)
        {
            float a = 0f, b = 0f, c2 = 0f, lp = 0f, lp2 = 0f;
            switch (k)
            {
                case "hum":    // гул ламп 100 Гц + шум, зацикленный (целое число периодов)
                    return Make("hum", 22050, 2f, (t, q) => (Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.2f + Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.1f + Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.15f) * 0.45f);
                case "buzz":
                    return Make("buzz", 22050, 2f, (t, q) => (Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 120f * t)) * 0.18f + Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.1f) * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 4f * t)) * 0.5f);
                case "pipe":
                    return Make("pipe", 22050, 4f, (t, q) => { lp = lp * 0.97f + Nz() * 0.03f; return (lp * 5f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.5f * t)) + Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.25f) * 0.6f; });
                case "water":
                    return Make("water", 22050, 4f, (t, q) => { lp = lp * 0.9f + Nz() * 0.1f; float dr = t % 1.3f < 0.05f ? Mathf.Sin(2f * Mathf.PI * (900f + 400f * (t % 1.3f)) * t) * (1f - (t % 1.3f) / 0.05f) * 0.25f : 0f; return lp * 0.5f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.25f * t)) + dr; });
                case "party":
                    return Make("party", 22050, 6f, (t, q) =>
                    {
                        float[] mel = { 523f, 659f, 784f, 659f, 587f, 698f, 880f, 698f }; int i = (int)(t * 2f) % 8; float lt = (t * 2f) % 1f; float f = mel[i] * (0.5f + 0.003f * Mathf.Sin(t * 3f));
                        float env = Mathf.Exp(-lt * 3f); float s = Mathf.Sin(2f * Mathf.PI * f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.1f; lp = lp * 0.5f + s * 0.5f; return lp * env * 0.7f + Nz() * 0.015f;
                    });
                case "siren":
                    return Make("siren", 22050, 3f, (t, q) => { float f = 700f + 350f * Mathf.Sin(2f * Mathf.PI * t / 1.5f * 1f); a += f / 22050f; return (float)Math.Tanh(Mathf.Sin(2f * Mathf.PI * a) * 2f) * 0.4f; });
                case "dark":
                    return Make("dark", 22050, 4f, (t, q) => { lp = lp * 0.985f + Nz() * 0.015f; return lp * 7f * 0.5f + Mathf.Sin(2f * Mathf.PI * 38f * t) * 0.18f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.25f * t)); });
                case "sea":   // низкий гул воды, скрип металла
                    return Make("sea", 22050, 4f, (t, q) => { lp = lp * 0.96f + Nz() * 0.04f; float creak = Mathf.Sin(2f * Mathf.PI * (180f + 40f * Mathf.Sin(2f * Mathf.PI * 0.25f * t)) * t) * 0.06f * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 0.5f * t)); return lp * 4.5f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.25f * t)) + Mathf.Sin(2f * Mathf.PI * 40f * t) * 0.2f + creak; });
                case "cave":  // сквозняк и капли
                    return Make("cave", 22050, 4f, (t, q) => { lp = lp * 0.985f + Nz() * 0.015f; float tt = t % 1.7f; float drip = tt < 0.08f ? Mathf.Sin(2f * Mathf.PI * (1400f - 800f * tt) * t) * (1f - tt / 0.08f) * 0.2f : 0f; return lp * 4f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.25f * t)) + Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.1f + drip; });
                case "wind":  // ночной ветер
                    return Make("wind", 22050, 4f, (t, q) => { lp = lp * 0.93f + Nz() * 0.07f; lp2 = lp2 * 0.99f + Nz() * 0.01f; return (lp * 0.9f + lp2 * 5f) * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 0.5f * t + 1f)) * 0.55f + Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.06f; });
                case "rain":
                    return Make("rain", 22050, 3f, (t, q) => { lp = lp * 0.45f + Nz() * 0.55f; lp2 = lp2 * 0.995f + Nz() * 0.005f; return lp * 0.32f * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.33f * t)) + lp2 * 2f; });
                case "city":
                    return Make("city", 22050, 4f, (t, q) => { lp = lp * 0.99f + Nz() * 0.01f; float horn = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.04f * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 0.25f * t - 1.2f)); return lp * 4f + Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.14f * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t)) + horn; });
                case "knock":
                    return Make("knock", 22050, 0.9f, (t, q) => { float v = 0f; for (int i = 0; i < 3; i++) { float tt = t - i * 0.26f; if (tt > 0f) v += Mathf.Sin(2f * Mathf.PI * (130f - 40f * tt) * tt) * Mathf.Exp(-tt * 28f) * 1.0f + Nz() * 0.15f * Mathf.Exp(-tt * 90f); } return v; });
                case "growl":
                    return Make("growl", 22050, 1.3f, (t, q) => { float f = 70f + 25f * Mathf.Sin(t * 22f) + 20f * Mathf.Sin(t * 7f); a += f / 22050f; float saw = (a % 1f) * 2f - 1f; lp = lp * 0.7f + Nz() * 0.3f; float env = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((1.3f - t) / 0.4f); return (float)Math.Tanh((saw * 0.8f + lp * 0.6f) * 2.2f) * env * 0.8f; });
                case "howl":
                    return Make("howl", 22050, 3.4f, (t, q) => { float f = 300f + 520f * Mathf.Sin(Mathf.Clamp01(t / 1.2f) * Mathf.PI * 0.5f) - 280f * Mathf.Clamp01((t - 1.5f) / 1.8f) + 28f * Mathf.Sin(t * 33f); a += f / 22050f; b += f * 1.5f / 22050f; float env = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((3.4f - t) / 0.9f); return (float)Math.Tanh(((a % 1f) * 2f - 1f) * 0.6f + ((b % 1f) * 2f - 1f) * 0.35f + Nz() * 0.25f) * env * 0.85f; });
                case "hiss":
                    return Make("hiss", 22050, 0.8f, (t, q) => { lp = lp * 0.3f + Nz() * 0.7f; float env = Mathf.Sin(Mathf.Clamp01(t / 0.8f) * Mathf.PI); return lp * env * 0.5f; });
                case "boom":
                    return Make("boom", 22050, 1.2f, (t, q) => (Mathf.Sin(2f * Mathf.PI * (60f - 35f * t) * t) * Mathf.Exp(-t * 4f) * 1.2f + Nz() * 0.4f * Mathf.Exp(-t * 18f)));
                case "ding":
                    return Make("ding", 22050, 1.6f, (t, q) => (Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.15f) * Mathf.Exp(-t * 2.6f));
                case "zap":
                    return Make("zap", 22050, 0.5f, (t, q) => { float env = Mathf.Exp(-t * 7f); return (Mathf.Sign(Mathf.Sin(2f * Mathf.PI * (200f + 800f * Nz() * 0.1f) * t)) * 0.4f + Nz() * 0.6f) * env; });
                case "click":
                    return Make("click", 22050, 0.12f, (t, q) => { float env = Mathf.Exp(-t * 90f); return (Nz() * 0.5f + Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.5f) * env + (t > 0.045f ? Mathf.Exp(-(t - 0.045f) * 110f) * Nz() * 0.4f : 0f); });
                case "splash":
                    return Make("splash", 22050, 0.7f, (t, q) => { lp = lp * 0.75f + Nz() * 0.25f; return lp * Mathf.Exp(-t * 5f) * 1.2f; });
                case "flap":
                    return Make("flap", 22050, 0.25f, (t, q) => { lp = lp * 0.8f + Nz() * 0.2f; return lp * Mathf.Sin(t / 0.25f * Mathf.PI) * 0.6f * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 40f)); });
                case "drip":
                    return Make("drip", 22050, 0.35f, (t, q) => Mathf.Sin(2f * Mathf.PI * (1900f - 2600f * t) * t) * Mathf.Exp(-t * 17f) * 0.45f + Nz() * 0.02f * Mathf.Exp(-t * 40f));
                case "dread":   // нагнетающий гул: две близкие низкие частоты дают медленные «биения», плюс тихий шорох
                    return Make("dread", 22050, 8f, (t, q) => { lp = lp * 0.995f + Nz() * 0.005f; float sw = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.125f * t); return (Mathf.Sin(2f * Mathf.PI * 41f * t) * 0.34f + Mathf.Sin(2f * Mathf.PI * 43.25f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.07f * sw + Mathf.Sin(2f * Mathf.PI * 61.5f * t) * 0.09f + lp * 2.2f) * 0.8f; });
                case "whisper": // шёпот без слов: «слоги» из полосового шума
                    return Make("whisper", 22050, 2.6f, (t, q) => { int syl = (int)(t * 5.5f); float lt = (t * 5.5f) % 1f; float amp = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(syl * 12.9898f)); float env = Mathf.Sin(Mathf.Clamp01(lt / 0.8f) * Mathf.PI) * Mathf.Clamp01(Mathf.Min(t, 2.6f - t) * 4f); if (Mathf.Sin(syl * 78.233f) > 0.75f) env *= 0.15f; float kk = 0.25f + 0.2f * Mathf.Abs(Mathf.Sin(syl * 5.1f)); lp = lp * (1f - kk) + Nz() * kk; lp2 = lp2 * 0.97f + lp * 0.03f; return (lp - lp2) * env * amp * 1.8f; });
                case "creak":   // стон дерева/металла
                    return Make("creak", 22050, 2.2f, (t, q) => { float f = 95f - 32f * q + 6f * Mathf.Sin(t * 31f); a += f / 22050f; float env = Mathf.Sin(q * Mathf.PI); lp = lp * 0.9f + Nz() * 0.1f; return ((a % 1f) * 2f - 1f) * 0.22f * env * (0.6f + 0.4f * Mathf.Sin(t * 47f)) + lp * 0.1f * env; });
                case "step":
                    return Make("step", 22050, 0.22f, (t, q) => { float env = Mathf.Exp(-t * 22f); return (Mathf.Sin(2f * Mathf.PI * (75f - 110f * t) * t) * 1.2f + Nz() * 0.25f * Mathf.Exp(-t * 80f)) * env; });
            }
            return null;
        }

        public static void At(string k, Vector3 pos, float vol) { var c = Get(k); if (c != null) AudioSource.PlayClipAtPoint(c, pos, vol); }
        static GameObject host; static AudioSource s2d;
        public static void Flat(string k, float vol) { var c = Get(k); if (c == null) return; if (host == null) { host = new GameObject("BR_audio"); UnityEngine.Object.DontDestroyOnLoad(host); s2d = host.AddComponent<AudioSource>(); s2d.spatialBlend = 0f; } s2d.PlayOneShot(c, vol); }

        /// <summary>Зацикленная атмосфера уровня.</summary>
        public static AudioSource Ambient(string kind, float vol)
        {
            var c = Get(kind); if (c == null) return null; var g = new GameObject("BR_ambient_player"); var s = g.AddComponent<AudioSource>(); s.clip = c; s.loop = true; s.volume = vol; s.spatialBlend = 0f; s.Play(); return s;
        }
    }
}
