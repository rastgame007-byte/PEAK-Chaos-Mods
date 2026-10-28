// Текстуры новых уровней (1.24): апартаменты, чердак, склад, школа, вентиляция + обновлённые гараж и поля.
using System;
using UnityEngine;

namespace PeakBR
{
    internal static class Tex2
    {
        static float P(float u, float v, float f, float o = 0f) { return Mathf.PerlinNoise(u * f + o, v * f + o * 0.7f); }
        static float Hash(int x, int y) { return (((x * 73856093) ^ (y * 19349663)) & 255) / 255f; }
        static Color Mul(Color c, float s) { return new Color(c.r * s, c.g * s, c.b * s, 1f); }
        static Color Rgb(float r, float g, float b) { return new Color(r, g, b, 1f); }
        static float Edge(float fu, float fv) { return Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)); }

        public static Func<float, float, Color> Wall(int k)
        {
            switch (k)
            {
                case 22: return (u, v) => // Level 15: белые лабораторные панели, синяя полоса, жёлто-чёрная защитная разметка, тёмные экраны, подтёки крови
                {
                    float fu = Mathf.Repeat(u * 4f, 1f); float seam = Mathf.Min(fu, 1f - fu); float n = 0.93f + 0.07f * P(u, v, 30f) + 0.04f * Hash((int)(u * 512f), (int)(v * 256f));
                    Color c = Rgb(0.83f, 0.86f, 0.88f); if (seam < 0.012f) c = Rgb(0.45f, 0.48f, 0.5f);
                    if (v > 0.5f && v < 0.56f) c = Rgb(0.18f, 0.4f, 0.62f);
                    if (v < 0.1f) { c = ((int)((u * 40f + v * 10f)) & 1) == 0 ? Rgb(0.9f, 0.75f, 0.1f) : Rgb(0.1f, 0.1f, 0.1f); if (v > 0.085f) c = Rgb(0.3f, 0.3f, 0.32f); }
                    int pi = (int)(u * 4f); if (Hash(pi, 11) > 0.55f && fu > 0.3f && fu < 0.7f && v > 0.62f && v < 0.82f) { bool fr = fu < 0.32f || fu > 0.68f || v < 0.64f || v > 0.8f; c = fr ? Rgb(0.2f, 0.22f, 0.25f) : Color.Lerp(Rgb(0.02f, 0.06f, 0.07f), Rgb(0.1f, 0.5f, 0.45f), P(u * 9f, v * 9f, 5f, pi) > 0.6f ? 0.8f : 0.1f); }
                    float bl = P(u * 2f, v * 0.7f, 8f, 4f); if (bl > 0.7f && v > 0.1f) c = Color.Lerp(c, Rgb(0.32f, 0.04f, 0.04f), Mathf.Clamp01((bl - 0.7f) * 5f) * (0.4f + 0.6f * (1f - v)));
                    return Mul(c, n * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(v * 3f)));
                };
                case 21: return (u, v) => // Level 94: пастельный «картонный» городок в стиле stop-motion — домики, окна с занавесками, зерно «глины»
                {
                    int hi = u < 0.5f ? 0 : 1; float fu = Mathf.Repeat(u * 2f, 1f);
                    Color[] pal = { Rgb(0.6f, 0.82f, 0.7f), Rgb(0.96f, 0.76f, 0.62f) }; Color c = pal[hi];
                    float grain = 0.92f + 0.1f * Hash((int)(u * 512f), (int)(v * 256f)) + 0.05f * P(u, v, 40f); float fing = P(u * 3f, v * 3f, 7f, hi * 5f); if (fing > 0.62f) grain *= 0.95f;
                    // карнизы и плинтус
                    if (v > 0.9f) c = hi == 0 ? Rgb(0.75f, 0.35f, 0.33f) : Rgb(0.45f, 0.5f, 0.7f); else if (v < 0.07f) c = Rgb(0.82f, 0.8f, 0.72f); else if (v > 0.87f && v <= 0.9f) c = Rgb(0.97f, 0.95f, 0.88f);
                    // окно
                    if (v > 0.4f && v < 0.78f && fu > 0.22f && fu < 0.78f)
                    {
                        bool frame = v < 0.43f || v > 0.75f || fu < 0.25f || fu > 0.75f || Mathf.Abs(fu - 0.5f) < 0.012f || Mathf.Abs(v - 0.59f) < 0.012f;
                        c = frame ? Rgb(0.97f, 0.96f, 0.9f) : Color.Lerp(Rgb(0.42f, 0.62f, 0.78f), Rgb(0.95f, 0.88f, 0.6f), Mathf.Clamp01((v - 0.43f) * 2.5f) * (hi == 1 ? 1f : 0.35f));
                        if (!frame && (fu < 0.34f || fu > 0.66f) && v > 0.55f) c = Rgb(0.9f, 0.45f, 0.5f);   // занавески
                    }
                    else if (v < 0.5f && v >= 0.07f && hi == 0 && fu > 0.35f && fu < 0.65f) { c = Rgb(0.55f, 0.32f, 0.22f); if (Mathf.Abs(fu - 0.5f) < 0.01f) c = Mul(c, 0.7f); if (Mathf.Abs(v - 0.3f) < 0.02f && fu > 0.53f) c = Rgb(0.9f, 0.8f, 0.3f); }
                    return Mul(c, grain * Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(v * 3f)));
                };
                case 16: return (u, v) => // апартаменты: зелёные обои с ромбами, тёмная деревянная панель, дверные коробки
                {
                    if (v < 0.28f) { float g = 0.75f + 0.25f * P(u * 3f, v * 50f, 9f); Color w = Rgb(0.2f * g, 0.13f * g, 0.08f * g); if (Mathf.Abs(v - 0.28f) < 0.014f) w = Rgb(0.34f, 0.24f, 0.14f); float pf = Mathf.Repeat(u * 4f, 1f); if (Edge(pf, Mathf.Repeat(v * 7f, 1f)) < 0.04f) w = Mul(w, 0.6f); return w; }
                    float pu = Mathf.Repeat(u * 8f, 1f) - 0.5f, pv = Mathf.Repeat(v * 4.5f, 1f) - 0.5f; float d = Mathf.Abs(pu) * 1.5f + Mathf.Abs(pv); bool dia = (d < 0.24f && d > 0.19f) || d < 0.04f;
                    float s = 0.88f + 0.12f * P(u * 4f, v * 30f, 6f); Color c = Rgb(0.34f * s, 0.46f * s, 0.27f * s); if (dia) c = Rgb(0.5f * s, 0.6f * s, 0.34f * s);
                    float stain = P(u * 2f, v, 5f, 3f); if (stain > 0.64f) c = Mul(c, Mathf.Lerp(1f, 0.65f, Mathf.Clamp01((stain - 0.64f) * 5f) * Mathf.Clamp01((v - 0.4f) * 2f)));
                    float dr = Mathf.Repeat(u * 2f, 1f); if (dr > 0.38f && dr < 0.62f && v < 0.82f) c = Rgb(0.28f * s, 0.18f * s, 0.1f * s); if (dr > 0.36f && dr < 0.64f && v < 0.84f && (dr < 0.38f || dr > 0.62f || v > 0.82f)) c = Rgb(0.4f, 0.32f, 0.2f);
                    return Mul(c, Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(v * 3f)));
                };
                case 17: return (u, v) => // чердак: вертикальные доски, сучки, потёки, паутина в верхних углах
                {
                    float bu = Mathf.Repeat(u * 12f, 1f); int bi = (int)(u * 12f); float tone = 0.7f + 0.3f * Hash(bi, 3); float grain = 0.82f + 0.18f * P(bu * 2f + bi, v * 3f, 12f, bi);
                    float s = tone * grain; if (bu < 0.04f || bu > 0.97f) s *= 0.45f; float knot = Mathf.Max(0f, 1f - Vector2.Distance(new Vector2(bu, Mathf.Repeat(v * 2f + Hash(bi, 9), 1f)), new Vector2(0.5f, 0.5f)) * 7f); s *= 1f - knot * 0.5f * (Hash(bi, 11) > 0.6f ? 1f : 0f);
                    Color c = Rgb(0.4f * s, 0.29f * s, 0.18f * s); float wet = P(u * 3f, v, 6f, 4f); if (wet > 0.64f && v < 0.6f) c = Mul(c, 0.62f); if (v > 0.9f) c = Mul(c, Mathf.Lerp(1f, 0.55f, (v - 0.9f) * 10f)); return Mul(c, Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(v * 4f)));
                };
                case 18: return (u, v) => // склад: синие стойки стеллажей, оранжевые балки, картонные коробки
                {
                    float fu = Mathf.Repeat(u * 4f, 1f); float post = (fu < 0.05f || fu > 0.95f) ? 1f : 0f; float beam = Mathf.Abs(Mathf.Repeat(v * 5f, 1f) - 0.08f) < 0.05f ? 1f : 0f;
                    if (post > 0f) return Mul(Rgb(0.12f, 0.25f, 0.55f), 0.75f + 0.25f * P(u * 10f, v * 4f, 8f));
                    if (beam > 0f) return Mul(Rgb(0.85f, 0.4f, 0.1f), 0.75f + 0.25f * P(u * 4f, v * 20f, 8f));
                    int bx = (int)(Mathf.Repeat(u * 4f, 1f) * 3f + (int)(u * 4f) * 3), by = (int)(v * 5f); float rv = Mathf.Repeat(v * 5f, 1f); float bw = Mathf.Repeat(u * 12f + Hash(by, 5) * 3f, 1f);
                    if (rv > 0.15f && rv < 0.9f && Hash(bx, by) > 0.28f) { float sh = 0.78f + 0.22f * Hash(bx, by + 40); Color c = Rgb(0.62f * sh, 0.46f * sh, 0.28f * sh); if (bw < 0.05f || Mathf.Abs(rv - 0.52f) < 0.025f) c = Mul(c, 0.6f); if (Mathf.Abs(bw - 0.5f) < 0.05f && rv > 0.5f) c = Rgb(0.82f * sh, 0.78f * sh, 0.65f * sh); return Mul(c, 0.85f + 0.15f * P(u, v, 20f)); }
                    return Mul(Rgb(0.12f, 0.12f, 0.13f), 0.8f + 0.4f * P(u, v, 10f));
                };
                case 19: return (u, v) => // школа: мятная краска, ряды синих шкафчиков с вентиляцией и замками
                {
                    if (v < 0.58f)
                    {
                        float fu = Mathf.Repeat(u * 12f, 1f); float fv = Mathf.Repeat(v / 0.58f * 2f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), fv < 0.5f ? 1f : 1f - fv);
                        Color c = Rgb(0.2f, 0.34f, 0.5f) * (0.82f + 0.18f * P(u * 12f, v * 8f, 5f, (int)(u * 12f))); c.a = 1f; if (e < 0.04f || Mathf.Abs(fv - 0.5f) < 0.02f) c = Mul(c, 0.5f);
                        float vent = Mathf.Repeat(fv * 2f, 1f); if (fu > 0.3f && fu < 0.7f && vent > 0.72f && vent < 0.95f && Mathf.Repeat(fv * 2f * 7f, 1f) < 0.5f) c = Mul(c, 0.25f); if (Mathf.Abs(fu - 0.78f) < 0.04f && Mathf.Abs(Mathf.Repeat(fv, 1f) - 0.35f) < 0.05f) c = Rgb(0.7f, 0.7f, 0.72f);
                        return Mul(c, Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(v * 5f)));
                    }
                    float g = 0.9f + 0.1f * P(u, v, 25f); Color w = v < 0.62f ? Rgb(0.2f, 0.25f, 0.28f) : Rgb(0.74f * g, 0.82f * g, 0.74f * g);
                    float st = P(u * 2f, v, 5f, 6f); if (st > 0.66f) w = Mul(w, 0.78f); if (v > 0.95f) w = Mul(w, 0.8f);
                    if (v > 0.7f && v < 0.9f) { float pu = Mathf.Repeat(u * 2f, 1f); if (pu > 0.15f && pu < 0.85f) { w = Mul(Rgb(0.18f, 0.28f, 0.2f), 0.9f); if (pu < 0.17f || pu > 0.83f || v < 0.72f || v > 0.88f) w = Rgb(0.45f, 0.35f, 0.2f); if (Hash((int)(u * 60f), (int)(v * 30f)) > 0.985f) w = Rgb(0.8f, 0.8f, 0.8f); } }
                    return w;
                };
                case 20: return (u, v) => // вентиляция: оцинкованные листы, фальцы, заклёпки, ржавые потёки
                {
                    float pu = Mathf.Repeat(u * 4f, 1f), pv = Mathf.Repeat(v * 2f, 1f); float e = Edge(pu, pv); float s = e < 0.025f ? 0.45f : (e < 0.06f ? 0.75f : 0.88f) + 0.1f * P(u * 6f, v * 3f, 8f);
                    Color c = Rgb(0.55f * s, 0.6f * s, 0.63f * s); float rivet = Vector2.Distance(new Vector2(Mathf.Repeat(u * 28f, 1f), Mathf.Repeat(v * 14f, 1f)), new Vector2(0.5f, 0.5f)); if (e > 0.03f && e < 0.12f && rivet < 0.16f) c = Mul(c, 1.25f);
                    float streak = P(u * 22f, v * 0.6f, 4f, 3f); if (streak > 0.6f) c = Color.Lerp(c, Rgb(0.4f, 0.24f, 0.12f), Mathf.Clamp01((streak - 0.6f) * 3f) * 0.8f); float dirt = P(u * 2f, v * 2f, 9f, 14f); if (dirt > 0.62f) c = Mul(c, 0.7f);
                    return Mul(c, Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(v * 4f)));
                };
            }
            return null;
        }

        public static Func<float, float, Color> Floor(int k)
        {
            switch (k)
            {
                case 22: return (u, v) => // Level 15: светло-серая эпоксидная плитка со стыками, жёлтой линией и пятнами
                {
                    float fu = Mathf.Repeat(u * 4f, 1f), fv = Mathf.Repeat(v * 4f, 1f); float e = Edge(fu, fv); float n = 0.88f + 0.08f * P(u, v, 14f) + 0.05f * Hash((int)(u * 256f), (int)(v * 256f));
                    Color c = Rgb(0.62f, 0.65f, 0.67f); if (e < 0.02f) c = Rgb(0.3f, 0.32f, 0.33f); if (Mathf.Abs(fu - 0.5f) < 0.025f) c = Rgb(0.8f, 0.65f, 0.12f);
                    float st = P(u, v, 5f, 9f); if (st > 0.72f) c = Color.Lerp(c, Rgb(0.28f, 0.05f, 0.04f), Mathf.Clamp01((st - 0.72f) * 4f)); return Mul(c, n);
                };
                case 21: return (u, v) => // Level 94: холмистая трава с мягкими полосами скоса, пятнами клевера и «глиняным» зерном
                {
                    float n = P(u, v, 6f) * 0.5f + P(u, v, 22f, 3f) * 0.3f + Hash((int)(u * 512f), (int)(v * 512f)) * 0.2f; float stripe = 0.5f + 0.5f * Mathf.Sin(u * 6.2832f * 8f);
                    Color c = Color.Lerp(Rgb(0.28f, 0.55f, 0.2f), Rgb(0.42f, 0.7f, 0.28f), n * 0.8f + stripe * 0.2f);
                    if (P(u, v, 9f, 7f) > 0.7f) c = Color.Lerp(c, Rgb(0.2f, 0.45f, 0.18f), 0.5f); if (Hash((int)(u * 256f), (int)(v * 256f)) > 0.992f) c = Rgb(0.98f, 0.9f, 0.4f);
                    return Mul(c, 0.9f + 0.1f * n);
                };
                case 16: return (u, v) => // коричневый ковёр с геометрическим узором
                {
                    float pu = Mathf.Repeat(u * 6f, 1f) - 0.5f, pv = Mathf.Repeat(v * 6f, 1f) - 0.5f; float d = Mathf.Abs(pu) + Mathf.Abs(pv); float fiber = 0.85f + 0.15f * Hash((int)(u * 512f), (int)(v * 512f)) ; float s = fiber * (0.9f + 0.1f * P(u, v, 30f));
                    Color c = Rgb(0.22f * s, 0.14f * s, 0.09f * s); if (d < 0.42f && d > 0.34f) c = Rgb(0.32f * s, 0.2f * s, 0.11f * s); if (d < 0.14f) c = Rgb(0.3f * s, 0.2f * s, 0.12f * s); if (Mathf.Abs(pu) < 0.03f || Mathf.Abs(pv) < 0.03f) c = Mul(c, 0.8f);
                    if (P(u, v, 4f, 9f) > 0.7f) c = Mul(c, 0.7f); return c;
                };
                case 17: return (u, v) => // старый дощатый пол: щели, сучки, пыль
                {
                    float row = Mathf.Repeat(v * 12f, 1f); int ri = (int)(v * 12f); float off = Hash(ri, 4) * 0.8f; float bl = Mathf.Repeat(u * 3f + off, 1f); float s = 0.65f + 0.35f * Hash(ri, (int)(u * 3f + off) + 20); s *= 0.8f + 0.2f * P(u * 2f, v * 40f, 14f, ri);
                    if (row < 0.05f || row > 0.97f || bl < 0.012f) s *= 0.28f; Color c = Rgb(0.34f * s, 0.24f * s, 0.15f * s); if (P(u, v, 5f, 3f) > 0.68f) c = Mul(c, 0.7f); return c;
                };
                case 18: return (u, v) => // бетон склада: жёлтая разметка проходов, масляные пятна
                {
                    float n = 0.42f + 0.25f * P(u, v, 4f) + 0.1f * P(u, v, 60f); Color c = Rgb(0.5f * n, 0.5f * n, 0.5f * n); float oil = P(u, v, 3f, 11f); if (oil > 0.7f) c = Mul(c, Mathf.Lerp(1f, 0.4f, Mathf.Clamp01((oil - 0.7f) * 5f)));
                    if (Mathf.Abs(Mathf.Repeat(u * 2f, 1f) - 0.5f) < 0.014f) c = Rgb(0.82f * n * 1.4f, 0.68f * n * 1.4f, 0.1f); if (Mathf.Abs(Mathf.Repeat(v * 2f, 1f) - 0.02f) < 0.005f) c = Mul(c, 0.65f); return c;
                };
                case 19: return (u, v) => // школьный линолеум: крапчатая плитка шашечкой
                {
                    int cx = (int)(u * 8f), cy = (int)(v * 8f); bool a = ((cx + cy) & 1) == 0; float sp = Hash((int)(u * 256f), (int)(v * 256f)) > 0.92f ? 0.7f : 1f; Color c = a ? Rgb(0.62f, 0.64f, 0.52f) : Rgb(0.42f, 0.5f, 0.4f);
                    float e = Edge(Mathf.Repeat(u * 8f, 1f), Mathf.Repeat(v * 8f, 1f)); if (e < 0.015f) c = Mul(c, 0.55f); float scuff = P(u * 2f, v * 14f, 6f, 8f); if (scuff > 0.66f) c = Mul(c, 0.82f); return Mul(c, sp * (0.82f + 0.18f * P(u, v, 12f)));
                };
                case 20: return (u, v) => // рифлёный металл
                {
                    float a = Mathf.Repeat((u + v) * 12f, 1f), b = Mathf.Repeat((u - v) * 12f, 1f); float rib = Mathf.Min(Mathf.Abs(a - 0.5f), Mathf.Abs(b - 0.5f)) < 0.1f ? 1.18f : 0.82f; float n = 0.32f + 0.12f * P(u, v, 8f);
                    Color c = Rgb(n * 0.95f * rib, n * rib, n * 1.05f * rib); if (P(u, v, 4f, 3f) > 0.7f) c = Color.Lerp(c, Rgb(0.28f, 0.14f, 0.07f), 0.5f); return c;
                };
            }
            return null;
        }

        public static Func<float, float, Color> Ceil(int k)
        {
            switch (k)
            {
                case 11: return (u, v) => { float e = Edge(Mathf.Repeat(u * 2f, 1f), Mathf.Repeat(v * 2f, 1f)); float n = (e < 0.025f ? 0.55f : 0.85f) * (0.9f + 0.1f * Hash((int)(u * 128f), (int)(v * 128f))); if (P(u, v, 4f, 3f) > 0.7f) n *= 0.75f; return Rgb(0.75f * n, 0.78f * n, 0.6f * n); };
                case 12: return (u, v) => { float row = Mathf.Repeat(u * 6f, 1f); float s = row < 0.06f ? 0.25f : 0.5f + 0.3f * P(u * 2f, v * 14f, 8f); float beam = Mathf.Abs(Mathf.Repeat(v * 2f, 1f) - 0.5f) < 0.06f ? 0.6f : 1f; return Rgb(0.34f * s * beam, 0.24f * s * beam, 0.15f * s * beam); };
                case 13: return (u, v) => { float rib = 0.7f + 0.3f * Mathf.Sin(u * Mathf.PI * 2f * 24f); float n = 0.3f + 0.15f * P(u, v, 5f); if (P(u, v, 3f, 6f) > 0.68f) n *= 0.6f; return Rgb(n * rib, n * rib, n * 1.05f * rib); };
                case 14: return (u, v) => { float e = Edge(Mathf.Repeat(u * 4f, 1f), Mathf.Repeat(v * 4f, 1f)); float dots = Hash((int)(u * 192f), (int)(v * 192f)) > 0.8f ? 0.8f : 1f; float s = (e < 0.02f ? 0.55f : 0.92f) * dots; if (P(u, v, 5f, 8f) > 0.72f) s *= 0.78f; return Rgb(0.88f * s, 0.9f * s, 0.84f * s); };
                case 15: return (u, v) => { float e = Edge(Mathf.Repeat(u * 4f, 1f), Mathf.Repeat(v * 2f, 1f)); float s = (e < 0.03f ? 0.45f : 0.8f) + 0.08f * P(u, v, 10f); return Rgb(0.4f * s, 0.44f * s, 0.47f * s); };
            }
            return null;
        }
    }
}
