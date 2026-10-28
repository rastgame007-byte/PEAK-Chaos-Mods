// Процедурные текстуры стен, полов и потолков для всех уровней.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static class Tex
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        public static void Clear() { foreach (var t in cache.Values) if (t != null) UnityEngine.Object.Destroy(t); cache.Clear(); BrGfx.ClearNormals(); Decals.Clear(); }

        static float P(float u, float v, float f, float o = 0f) { return Mathf.PerlinNoise(u * f + o, v * f + o * 0.7f); }
        static float Hash(int x, int y) { return (((x * 73856093) ^ (y * 19349663)) & 255) / 255f; }
        static Color Mul(Color c, float s) { return new Color(c.r * s, c.g * s, c.b * s, 1f); }
        static Color Rgb(float r, float g, float b) { return new Color(r, g, b, 1f); }

        public static Texture2D Wall(int k) { return Get("w" + k, 1024, 512, Wf(k), 1.5f); }
        public static Texture2D Floor(int k) { return Get("f" + k, 512, 512, Ff(k), 1.1f); }
        public static Texture2D Ceil(int k) { return Get("c" + k, 512, 512, Cf(k), 0.8f); }
        static Texture2D Get(string key, int w, int h, Func<float, float, Color> f, float ns = 0f)
        {
            Texture2D t; if (cache.TryGetValue(key, out t) && t != null) return t;
            char kind = key[0]; var f0 = f; f = (u, v) => Detail(kind, f0(u, v), u, v);
            t = BrGfx.MakeTex(w, h, true, f, ns); cache[key] = t; return t;
        }


        // ---------- общий проход «детализации» поверх всех текстур (1.28): бесшовный шум, подтёки, потёртости, зерно ----------
        static float H3(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 1274126177; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
        /// <summary>Бесшовный value-noise: решётка fx×fy замыкается по краям, поэтому текстура остаётся тайлящейся.</summary>
        static float VN(float u, float v, int fx, int fy, int seed)
        {
            float x = u * fx, y = v * fy; int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float tx = x - x0, ty = y - y0; tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
            int xa = ((x0 % fx) + fx) % fx, xb = (xa + 1) % fx, ya = ((y0 % fy) + fy) % fy, yb = (ya + 1) % fy;
            float a = Mathf.Lerp(H3(xa, ya, seed), H3(xb, ya, seed), tx), b = Mathf.Lerp(H3(xa, yb, seed), H3(xb, yb, seed), tx); return Mathf.Lerp(a, b, ty);
        }
        static Color Detail(char kind, Color c, float u, float v)
        {
            float k = 0.955f + 0.09f * VN(u, v, 256, kind == 'w' ? 128 : 256, 1) + (VN(u, v, 5, 3, 4) - 0.5f) * 0.1f;           // мелкое зерно + большие пятна тона
            if (kind == 'w')
            {
                float st = VN(u, v, 72, 3, 2); k *= 1f - 0.2f * Mathf.SmoothStep(0.55f, 0.9f, st) * v * v;                          // потёки от потолка
                if (v < 0.16f) k *= 1f - 0.3f * (1f - v / 0.16f) * VN(u, v, 40, 8, 3);                                              // грязь и потёртости у пола
                if (v > 0.9f) k *= Mathf.Lerp(1f, 0.82f, (v - 0.9f) * 10f);                                                         // тень в углу потолка
                if (VN(u, v, 220, 7, 5) > 0.93f) k *= 0.88f;                                                                       // царапины
                float damp = VN(u, v, 3, 2, 6); if (damp > 0.7f && v < 0.5f) k *= 1f - 0.22f * Mathf.SmoothStep(0.7f, 0.9f, damp);   // сырость
            }
            else if (kind == 'f')
            {
                k *= 0.94f + 0.12f * VN(u, v, 8, 8, 7); float sc = VN(u, v, 64, 64, 8); if (sc > 0.8f) k *= 0.9f;                      // потёртости
                float stn = VN(u, v, 3, 3, 9); if (stn > 0.7f) k *= 1f - 0.24f * Mathf.SmoothStep(0.7f, 0.88f, stn);                   // тёмные пятна
            }
            else { float stn = VN(u, v, 3, 3, 10); if (stn > 0.66f) k *= 1f - 0.2f * Mathf.SmoothStep(0.66f, 0.85f, stn); }          // потолок: потёки и пыль
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
        }

        // u: 0..1 = 4 м по горизонтали; v: 0..1 = высота стены
        static Func<float, float, Color> Wf(int k)
        {
            var x2 = Tex2.Wall(k); if (x2 != null) return x2;
            switch (k)
            {
                case 0: return (u, v) =>
                {
                    float uu = u * 4f; float fu = Mathf.Repeat(uu * 4f, 1f); float stripe = fu < 0.42f ? 1f : (fu < 0.5f ? 0.88f : 0.95f);
                    float dxm = Mathf.Abs(Mathf.Repeat(uu * 2f, 1f) - 0.5f), dym = Mathf.Abs(Mathf.Repeat(v * 3f, 1f) - 0.5f); float damask = (dxm * 1.6f + dym) < 0.22f && fu > 0.5f ? 0.92f : 1f;
                    float stain = P(u, v, 6f, 3.1f); float st = (v > 0.5f && stain > 0.62f) ? Mathf.Lerp(1f, 0.7f, Mathf.Clamp01((stain - 0.62f) * 6f) * Mathf.Clamp01((v - 0.5f) * 3f)) : 1f;
                    float dirt = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(v * 4.5f)) * Mathf.Lerp(1f, 0.8f, Mathf.Clamp01((v - 0.9f) * 9f)); float fib = 0.96f + 0.06f * P(u * 50f, v * 25f, 6f);
                    float s = stripe * damask * st * dirt * fib; return Rgb(0.84f * s, 0.76f * s, 0.38f * s);
                };
                case 1: return (u, v) => // бетон + жёлтая полоса и стыки панелей
                {
                    float n = 0.5f + 0.35f * P(u, v, 5f) + 0.15f * P(u, v, 40f); float seam = (Mathf.Repeat(u * 2f, 1f) < 0.01f || Mathf.Abs(v - 0.5f) < 0.006f) ? 0.7f : 1f;
                    float s = n * seam * Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(v * 5f)); Color c = Rgb(0.62f * s, 0.63f * s, 0.62f * s);
                    if (v > 0.04f && v < 0.1f) c = Rgb(0.8f * n, 0.65f * n, 0.1f); float stain = P(u, v * 0.4f, 4f, 7f); if (stain > 0.66f) c = Mul(c, 0.78f); return c;
                };
                case 2: return (u, v) => // зелёный металл, заклёпки, ржавчина
                {
                    float pu = Mathf.Repeat(u * 4f, 1f), pv = Mathf.Repeat(v * 2f, 1f); float edge = Mathf.Min(Mathf.Min(pu, 1f - pu), Mathf.Min(pv, 1f - pv)); float s = edge < 0.03f ? 0.55f : 0.9f;
                    float rv = Mathf.Abs(Mathf.Repeat(pu * 10f, 1f) - 0.5f) + Mathf.Abs(Mathf.Repeat(pv * 6f, 1f) - 0.5f); if (edge > 0.04f && edge < 0.1f && rv < 0.5f && Hash((int)(u * 200), (int)(v * 100)) > 0.9f) s *= 1.3f;
                    float rust = P(u * 3f, v, 8f, 1.7f); Color c = Rgb(0.28f * s, 0.4f * s, 0.34f * s); if (rust > 0.6f) c = Color.Lerp(c, Rgb(0.45f, 0.24f, 0.1f), Mathf.Clamp01((rust - 0.6f) * 3f)); return c;
                };
                case 3: return (u, v) => // индустриальная: сине-серые панели + жёлто-чёрная полоса внизу
                {
                    float pu = Mathf.Repeat(u * 2f, 1f); float s = (pu < 0.012f || Mathf.Repeat(v * 2f, 1f) < 0.012f) ? 0.5f : 0.85f + 0.1f * P(u, v, 14f);
                    Color c = Rgb(0.3f * s, 0.36f * s, 0.44f * s); if (v < 0.16f) { bool y = Mathf.Repeat((u * 4f + v) * 14f, 1f) < 0.5f; c = y ? Rgb(0.85f, 0.7f, 0.1f) : Rgb(0.08f, 0.08f, 0.08f); c = Mul(c, 0.7f + 0.3f * P(u, v, 20f)); } return c;
                };
                case 4: return (u, v) => // офис: светлая краска, серая панель до пояса
                {
                    float g = 0.9f + 0.1f * P(u, v, 30f); Color c = v < 0.34f ? Rgb(0.45f * g, 0.5f * g, 0.52f * g) : Rgb(0.86f * g, 0.83f * g, 0.74f * g); if (Mathf.Abs(v - 0.34f) < 0.012f) c = Rgb(0.3f, 0.3f, 0.3f);
                    float st = P(u, v, 5f, 2f); if (st > 0.68f && v > 0.5f) c = Mul(c, 0.8f); return Mul(c, Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(v * 5f)));
                };
                case 5: return (u, v) => // отель: бордовые обои с золотым узором, деревянная панель
                {
                    if (v < 0.3f) { float g = 0.7f + 0.3f * P(u * 2f, v * 40f, 8f); Color w = Rgb(0.28f * g, 0.14f * g, 0.08f * g); if (Mathf.Abs(v - 0.3f) < 0.012f || Mathf.Abs(v - 0.15f) < 0.006f) w = Rgb(0.7f, 0.55f, 0.2f); return w; }
                    float pu = Mathf.Repeat(u * 8f, 1f) - 0.5f, pv = Mathf.Repeat(v * 5f, 1f) - 0.5f; float d = Mathf.Abs(pu) * 1.4f + Mathf.Abs(pv); bool gold = d < 0.26f && d > 0.2f || d < 0.05f;
                    float s = 0.85f + 0.15f * P(u, v, 25f); Color c = Rgb(0.42f * s, 0.07f * s, 0.09f * s); if (gold) c = Rgb(0.75f * s, 0.58f * s, 0.2f * s); return Mul(c, Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(v * 3f)));
                };
                case 6: return (u, v) => // вечеринка: яркие полосы + конфетти
                {
                    int band = (int)(u * 24f) % 4; Color[] pal = { Rgb(0.95f, 0.4f, 0.6f), Rgb(0.98f, 0.85f, 0.3f), Rgb(0.4f, 0.8f, 0.9f), Rgb(0.7f, 0.5f, 0.9f) }; Color c = Mul(pal[band], 0.9f);
                    int cx = (int)(u * 128f), cy = (int)(v * 64f); if (Hash(cx, cy) > 0.93f) c = pal[(int)(Hash(cy, cx) * 3.99f)]; return Mul(c, Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(v * 4f)));
                };
                case 7: return (u, v) => // бассейн: белая плитка с затиркой
                {
                    float fu = Mathf.Repeat(u * 16f, 1f), fv = Mathf.Repeat(v * 8f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); float s = e < 0.04f ? 0.65f : 0.95f + 0.05f * Hash((int)(u * 16f), (int)(v * 8f));
                    Color c = Rgb(0.92f * s, 0.93f * s, 0.88f * s); if (v < 0.25f) c = Color.Lerp(c, Rgb(0.45f, 0.78f, 0.78f), 0.45f); return c;
                };
                case 8: return (u, v) => // больница: зелёная плитка, красная полоса, грязь
                {
                    float fu = Mathf.Repeat(u * 24f, 1f), fv = Mathf.Repeat(v * 10f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); float s = e < 0.05f ? 0.55f : 0.9f;
                    Color c = v < 0.55f ? Rgb(0.55f * s, 0.68f * s, 0.6f * s) : Rgb(0.8f * s, 0.8f * s, 0.74f * s); if (Mathf.Abs(v - 0.55f) < 0.04f) c = Rgb(0.65f, 0.05f, 0.05f);
                    float d = P(u * 3f, v, 6f, 4f); if (d > 0.6f) c = Color.Lerp(c, Rgb(0.35f, 0.04f, 0.04f), Mathf.Clamp01((d - 0.6f) * 2.2f)); return Mul(c, 0.75f);
                };
                case 10: return (u, v) => // затопленный отсек: ржавые клёпаные панели, потёки, ватерлиния
                {
                    float pu = Mathf.Repeat(u * 3f, 1f), pv = Mathf.Repeat(v * 2f, 1f); float edge = Mathf.Min(Mathf.Min(pu, 1f - pu), Mathf.Min(pv, 1f - pv)); float s = edge < 0.03f ? 0.4f : 0.7f + 0.15f * P(u, v, 12f);
                    Color c = Rgb(0.26f * s, 0.32f * s, 0.34f * s); float rust = P(u * 2f, v * 3f, 7f, 5f); if (rust > 0.55f) c = Color.Lerp(c, Rgb(0.5f, 0.26f, 0.1f), Mathf.Clamp01((rust - 0.55f) * 3f));
                    float streak = P(u * 18f, v * 0.7f, 5f, 2f); if (streak > 0.62f && v > 0.2f) c = Mul(c, 0.7f);
                    if (v < 0.2f) c = Color.Lerp(c, Rgb(0.08f, 0.14f, 0.12f), Mathf.Clamp01((0.2f - v) * 7f)); return Mul(c, Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(v * 3f)));
                };
                case 11: return (u, v) => // пещера: слоистая порода, трещины, влажные потёки
                {
                    float layer = P(u * 2f, v * 9f, 4f, 3f); float n = 0.3f + 0.22f * layer + 0.12f * P(u, v, 30f); float crack = Mathf.Abs(P(u * 5f, v * 3f, 9f, 8f) - 0.5f); if (crack < 0.02f) n *= 0.4f;
                    Color c = Rgb(n * 0.9f, n * 0.82f, n * 0.74f); float wet = P(u * 3f, v, 6f, 12f); if (wet > 0.6f && v < 0.6f) c = Mul(c, 0.65f); if (P(u, v, 14f, 20f) > 0.84f) c = Color.Lerp(c, Rgb(0.1f, 0.5f, 0.4f), 0.35f); return c;
                };
                case 12: return (u, v) => // пригород: сайдинг, окна с тёплым светом, двери
                {
                    float board = Mathf.Repeat(v * 22f, 1f); float s = board < 0.08f ? 0.65f : 0.88f + 0.08f * P(u * 4f, v * 30f, 6f);
                    Color c = Rgb(0.46f * s, 0.5f * s, 0.52f * s); int hx = (int)(u * 4f); float fu = Mathf.Repeat(u * 4f, 1f);
                    if (v > 0.38f && v < 0.72f && fu > 0.2f && fu < 0.8f) { bool lit = Hash(hx, 7) > 0.65f; bool fr = v < 0.4f || v > 0.7f || fu < 0.23f || fu > 0.77f || Mathf.Abs(fu - 0.5f) < 0.015f || Mathf.Abs(v - 0.55f) < 0.012f; c = fr ? Rgb(0.8f, 0.8f, 0.78f) : (lit ? Rgb(0.95f, 0.72f, 0.3f) : Rgb(0.03f, 0.04f, 0.06f)); }
                    else if (v < 0.34f && fu > 0.35f && fu < 0.65f && Hash(hx, 3) > 0.5f) c = Rgb(0.2f, 0.12f, 0.08f);
                    return Mul(c, Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(v * 4f)));
                };
                case 13: return (u, v) => // пшеница: стебли и колосья
                {
                    float st = Mathf.Repeat(u * 64f + P(u * 3f, v * 2f, 6f) * 0.6f, 1f); float stalk = Mathf.Abs(st - 0.5f) < 0.16f ? 1f : 0.55f; float ear = v > 0.62f ? 1f : 0f; float k = 0.75f + 0.25f * P(u * 40f, v * 12f, 5f);
                    Color c = Rgb(0.75f, 0.6f, 0.2f); c = Color.Lerp(Rgb(0.4f, 0.38f, 0.14f), c, Mathf.Clamp01(v * 1.6f)); if (ear > 0f && Mathf.Repeat(v * 40f, 1f) < 0.55f) c = Rgb(0.85f, 0.7f, 0.28f); return Mul(c, stalk * k * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(v * 3f)));
                };
                case 14: return (u, v) => // небоскрёб: сетка окон, часть горит
                {
                    float fu = Mathf.Repeat(u * 12f, 1f), fv = Mathf.Repeat(v * 14f, 1f); int ix = (int)(u * 12f), iy = (int)(v * 14f); Color c = Rgb(0.2f, 0.21f, 0.25f) * (0.85f + 0.15f * P(u, v, 6f)); c.a = 1f;
                    if (fu > 0.15f && fu < 0.85f && fv > 0.2f && fv < 0.8f) { float h = Hash(ix + 5, iy * 3); c = h > 0.78f ? Rgb(0.95f, 0.85f, 0.55f) : (h > 0.6f ? Rgb(0.4f, 0.55f, 0.75f) : Rgb(0.05f, 0.07f, 0.1f)); }
                    if (v < 0.07) c = Rgb(0.12f, 0.12f, 0.14f); return c;
                };
                case 15: return (u, v) => // белый отель: оштукатуренная стена и ряды одинаковых окон
                {
                    float fu = Mathf.Repeat(u * 6f, 1f), fv = Mathf.Repeat(v * 2f, 1f); float g = 0.9f + 0.1f * P(u, v, 25f); Color c = Rgb(0.9f * g, 0.9f * g, 0.88f * g);
                    if (fu > 0.2f && fu < 0.8f && fv > 0.15f && fv < 0.85f) { bool fr = fu < 0.24f || fu > 0.76f || fv < 0.19f || fv > 0.81f || Mathf.Abs(fu - 0.5f) < 0.012f; c = fr ? Rgb(0.7f, 0.7f, 0.68f) : Color.Lerp(Rgb(0.08f, 0.1f, 0.14f), Rgb(0.2f, 0.28f, 0.4f), fv); c.a = 1f; }
                    return Mul(c, Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(v * 3f)));
                };
                default: return (u, v) => // тёмный бетон с влагой
                {
                    float n = 0.25f + 0.18f * P(u, v, 5f) + 0.1f * P(u, v, 45f); float wet = P(u * 2f, v * 0.5f, 6f, 11f); if (wet > 0.6f && v < 0.5f) n *= 0.6f; return Rgb(n * 0.85f, n * 0.88f, n);
                };
            }
        }

        static Func<float, float, Color> Ff(int k)
        {
            var x2 = Tex2.Floor(k); if (x2 != null) return x2;
            switch (k)
            {
                case 0: return (u, v) => { int ix = (int)(u * 256f), iy = (int)(v * 256f); float nz = Hash(ix, iy) - 0.5f; float wob = 0.93f + 0.07f * Mathf.Sin(u * 400f) * Mathf.Sin(v * 400f); float stain = P(u, v, 4f, 9f) > 0.72f ? 0.8f : 1f; float s = (0.8f + nz * 0.18f) * wob * stain; return Rgb(0.6f * s, 0.52f * s, 0.27f * s); };
                case 1: return (u, v) => { float n = 0.5f + 0.3f * P(u, v, 4f) + 0.1f * P(u, v, 50f); Color c = Rgb(0.5f * n, 0.5f * n, 0.5f * n); if (P(u, v, 3f, 5f) > 0.74f) c = Mul(c, 0.55f); if (Mathf.Abs(Mathf.Repeat(u * 2f, 1f) - 0.5f) < 0.006f) c = Mul(c, 0.7f); return c; };
                case 2: return (u, v) => { float fu = Mathf.Repeat(u * 16f, 1f) - 0.5f, fv = Mathf.Repeat(v * 16f, 1f) - 0.5f; float hole = fu * fu + fv * fv < 0.1f ? 0.25f : 1f; float s = (0.35f + 0.1f * P(u, v, 10f)) * hole; return Rgb(s * 0.9f, s, s * 0.95f); };
                case 3: return (u, v) => { float n = 0.35f + 0.1f * P(u, v, 6f); Color c = Rgb(n * 0.8f, n * 0.9f, n * 1.1f); if (Mathf.Abs(u - 0.5f) < 0.012f) c = Rgb(0.8f, 0.65f, 0.1f); if (P(u, v, 5f, 3f) > 0.78f) c = Mul(c, 0.6f); return c; };
                case 4: return (u, v) => { float fu = Mathf.Repeat(u * 4f, 1f), fv = Mathf.Repeat(v * 4f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); float s = (e < 0.012f ? 0.6f : 0.9f) * (0.85f + 0.15f * Hash((int)(u * 128f), (int)(v * 128f))); return Rgb(0.33f * s, 0.36f * s, 0.4f * s); };
                case 5: return (u, v) => { int ix = (int)(u * 256f), iy = (int)(v * 256f); float nz = Hash(ix, iy) - 0.5f; float pu = Mathf.Repeat(u * 4f, 1f) - 0.5f, pv = Mathf.Repeat(v * 4f, 1f) - 0.5f; float d = Mathf.Abs(pu) + Mathf.Abs(pv); bool gold = (d < 0.34f && d > 0.28f) || d < 0.06f || Mathf.Min(Mathf.Abs(pu), Mathf.Abs(pv)) > 0.46f; float s = 0.8f + nz * 0.15f; Color c = Rgb(0.36f * s, 0.05f * s, 0.07f * s); if (gold) c = Rgb(0.7f * s, 0.52f * s, 0.18f * s); return c; };
                case 6: return (u, v) => { int cx = (int)(u * 8f), cy = (int)(v * 8f); bool a = ((cx + cy) & 1) == 0; Color c = a ? Rgb(0.95f, 0.45f, 0.65f) : Rgb(0.98f, 0.9f, 0.5f); float s = 0.8f + 0.2f * P(u, v, 20f); if (Hash((int)(u * 128f), (int)(v * 128f)) > 0.985f) c = Rgb(0.4f, 0.8f, 0.95f); return Mul(c, s); };
                case 7: return (u, v) => { float fu = Mathf.Repeat(u * 8f, 1f), fv = Mathf.Repeat(v * 8f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); float s = e < 0.035f ? 0.6f : 0.93f + 0.07f * Hash((int)(u * 8f), (int)(v * 8f)); return Rgb(0.9f * s, 0.88f * s, 0.8f * s); };
                case 8: return (u, v) => { float fu = Mathf.Repeat(u * 8f, 1f), fv = Mathf.Repeat(v * 8f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); float s = (e < 0.03f ? 0.6f : 0.9f) * (0.8f + 0.2f * P(u, v, 8f)); Color c = Rgb(0.5f * s, 0.6f * s, 0.55f * s); if (P(u, v, 6f, 8f) > 0.7f) c = Color.Lerp(c, Rgb(0.3f, 0.03f, 0.03f), 0.7f); return c; };
                case 10: return (u, v) => { float n = 0.3f + 0.12f * P(u, v, 6f); float fu = Mathf.Repeat(u * 6f, 1f), fv = Mathf.Repeat(v * 6f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); if (e < 0.04f) n *= 0.5f; Color c = Rgb(n * 0.7f, n * 0.85f, n * 0.9f); if (P(u, v, 4f, 9f) > 0.7f) c = Color.Lerp(c, Rgb(0.3f, 0.17f, 0.08f), 0.5f); return c; };
                case 11: return (u, v) => { float n = 0.25f + 0.15f * P(u, v, 8f) + 0.1f * P(u, v, 50f); Color c = Rgb(n * 0.95f, n * 0.85f, n * 0.75f); if (P(u, v, 5f, 3f) > 0.72f) c = Mul(c, 0.55f); if (Hash((int)(u * 128f), (int)(v * 128f)) > 0.985f) c = Rgb(0.5f, 0.48f, 0.44f); return c; };
                case 12: return (u, v) => { float n = 0.18f + 0.08f * P(u, v, 20f) + 0.06f * Hash((int)(u * 256f), (int)(v * 256f)); Color c = Rgb(n, n, n * 1.05f); float vv = Mathf.Repeat(v * 4f, 1f); if (Mathf.Abs(u - 0.5f) < 0.012f && vv < 0.55f) c = Rgb(0.7f, 0.7f, 0.6f); if (u < 0.12f || u > 0.88f) c = Rgb(0.33f, 0.33f, 0.33f) * (0.9f + 0.1f * P(u, v, 12f)); c.a = 1f; if (P(u, v, 3f, 4f) > 0.75f) c = Mul(c, 0.6f); return c; };
                case 13: return (u, v) => { float n = 0.2f + 0.12f * P(u, v, 7f) + 0.06f * Hash((int)(u * 256f), (int)(v * 256f)); Color c = Rgb(n * 1.1f, n * 0.85f, n * 0.5f); if (P(u, v, 12f, 2f) > 0.6f) c = Color.Lerp(c, Rgb(0.45f, 0.35f, 0.12f), 0.5f); return c; };
                case 14: return (u, v) => { float n = 0.2f + 0.1f * P(u, v, 12f) + 0.05f * Hash((int)(u * 256f), (int)(v * 256f)); Color c = Rgb(n, n, n * 1.05f); float fu = Mathf.Repeat(u * 2f, 1f); if (fu < 0.08f || fu > 0.92f) c = Rgb(0.4f, 0.4f, 0.42f) * (0.9f + 0.1f * Hash((int)(u * 64f), (int)(v * 64f))); c.a = 1f; if (Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f) < 0.03f && fu > 0.3f && fu < 0.7f) c = Rgb(0.75f, 0.75f, 0.65f); return c; };
                case 15: return (u, v) => { int cx = (int)(u * 8f), cy = (int)(v * 8f); bool a = ((cx + cy) & 1) == 0; float s = 0.85f + 0.1f * P(u, v, 14f); float fu = Mathf.Repeat(u * 8f, 1f), fv = Mathf.Repeat(v * 8f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); if (e < 0.02f) s *= 0.6f; return a ? Rgb(0.9f * s, 0.9f * s, 0.88f * s) : Rgb(0.55f * s, 0.55f * s, 0.55f * s); };
                default: return (u, v) => { float n = 0.2f + 0.12f * P(u, v, 5f) + 0.08f * P(u, v, 60f); if (P(u, v, 3f, 2f) > 0.62f) n *= 0.55f; return Rgb(n * 0.9f, n, n * 1.05f); };
            }
        }

        static Func<float, float, Color> Cf(int k)
        {
            var x2 = Tex2.Ceil(k); if (x2 != null) return x2;
            switch (k)
            {
                case 0: return (u, v) => { float fu = Mathf.Repeat(u * 2f, 1f), fv = Mathf.Repeat(v * 2f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); float s = e < 0.025f ? 0.42f : 0.9f; s += (Hash((int)(u * 256f), (int)(v * 256f)) - 0.5f) * 0.07f; if (e >= 0.025f && P(u, v, 7f) > 0.8f) s *= 0.86f; return Rgb(0.88f * s, 0.86f * s, 0.72f * s); };
                case 1: return (u, v) => { float n = 0.45f + 0.25f * P(u, v, 6f) + 0.1f * P(u, v, 40f); if (Mathf.Abs(Mathf.Repeat(u * 2f, 1f) - 0.5f) < 0.008f) n *= 0.7f; return Rgb(n, n, n * 1.02f); };
                case 2: return (u, v) => { float n = 0.18f + 0.1f * P(u, v, 8f); float st = Mathf.Repeat(v * 6f, 1f); if (st < 0.06f) n = 0.45f; return Rgb(n, n * 1.05f, n); };
                case 3: return (u, v) => { float fu = Mathf.Repeat(u * 2f, 1f), fv = Mathf.Repeat(v * 2f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); float s = e < 0.04f ? 0.35f : 0.75f + 0.1f * P(u, v, 10f); return Rgb(0.45f * s, 0.34f * s, 0.25f * s); };
                case 5: return (u, v) => { float n = 0.2f + 0.1f * P(u, v, 8f); float fu = Mathf.Repeat(u * 3f, 1f); if (fu < 0.08f || (fu > 0.5f && fu < 0.56f)) n = 0.38f; Color c = Rgb(n * 0.8f, n * 0.85f, n * 0.85f); if (P(u, v, 5f, 2f) > 0.66f) c = Color.Lerp(c, Rgb(0.4f, 0.22f, 0.1f), 0.5f); return c; };
                case 6: return (u, v) => { float n = 0.14f + 0.12f * P(u, v, 6f) + 0.08f * P(u, v, 30f); return Rgb(n * 0.9f, n * 0.82f, n * 0.75f); };
                case 7: return (u, v) => { float n = 0.02f + 0.02f * P(u, v, 4f); Color c = Rgb(n, n * 1.1f, n * 1.5f); if (Hash((int)(u * 200f), (int)(v * 200f)) > 0.994f) c = Rgb(0.6f, 0.6f, 0.7f); return c; };
                case 8: return (u, v) => { float n = 0.36f + 0.2f * P(u, v, 3f) + 0.1f * P(u, v, 12f); return Rgb(n, n, n * 0.95f); };
                case 9: return (u, v) => { float n = 0.04f + 0.06f * P(u, v, 3f) + 0.04f * v; return Rgb(n * 1.3f, n * 0.9f, n * 1.5f); };
                case 10: return (u, v) => { float fu = Mathf.Repeat(u * 2f, 1f), fv = Mathf.Repeat(v * 2f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); float s = e < 0.02f ? 0.75f : 0.97f; return Rgb(0.93f * s, 0.93f * s, 0.9f * s); };
                default: return (u, v) => { float fu = Mathf.Repeat(u * 4f, 1f), fv = Mathf.Repeat(v * 4f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); float s = e < 0.03f ? 0.7f : 0.96f; return Rgb(0.9f * s, 0.93f * s, 0.9f * s); };
            }
        }
    }
}
