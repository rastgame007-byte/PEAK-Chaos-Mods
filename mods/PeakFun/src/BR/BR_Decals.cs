// Картинки и мелкие модели на стенах (1.28): плакаты «пропал человек», рамки с картинами, детские рисунки, граффити, планы эвакуации,
// знаки, часы; розетки, выключатели, огнетушители, пожарные кнопки, решётки, камеры наблюдения, плинтусы.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static class Decals
    {
        const int N = 4, CS = 128;
        static Texture2D atlas;
        public static void Clear() { if (atlas != null) UnityEngine.Object.Destroy(atlas); atlas = null; }

        // ---------- мелкие помощники для рисования ----------
        static float H2(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 1274126177; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
        static float Nz(float u, float v, float f, float o = 0f) { return Mathf.PerlinNoise(u * f + o, v * f + o * 0.7f); }
        static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }
        static Color Mul(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }
        static float D(float u, float v, float cx, float cy) { return Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)); }
        static bool Rc(float u, float v, float x0, float y0, float x1, float y1) { return u >= x0 && u <= x1 && v >= y0 && v <= y1; }
        static float Seg(float u, float v, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, t = Mathf.Clamp01(((u - ax) * dx + (v - ay) * dy) / (dx * dx + dy * dy + 1e-6f)); return D(u, v, ax + dx * t, ay + dy * t);
        }
        static bool Frame(float u, float v, float w) { return u < w || u > 1f - w || v < w || v > 1f - w; }
        static Color Wood(float u, float v) { float g = 0.8f + 0.2f * Nz(u * 3f, v * 40f, 6f); return C(0.42f * g, 0.27f * g, 0.13f * g); }

        static Color Img(int i, float u, float v)
        {
            Color c;
            switch (i)
            {
                case 0: // EXIT: зелёный знак, бегущий человечек и дверь
                {
                    c = C(0.04f, 0.46f, 0.2f); if (Frame(u, v, 0.045f)) return C(0.9f, 0.95f, 0.9f); Color w = C(0.95f, 1f, 0.95f);
                    if (D(u, v, 0.34f, 0.68f) < 0.075f || Seg(u, v, 0.34f, 0.6f, 0.42f, 0.42f) < 0.035f || Seg(u, v, 0.42f, 0.42f, 0.32f, 0.26f) < 0.032f || Seg(u, v, 0.42f, 0.42f, 0.5f, 0.28f) < 0.032f || Seg(u, v, 0.37f, 0.56f, 0.24f, 0.5f) < 0.028f || Seg(u, v, 0.37f, 0.56f, 0.5f, 0.62f) < 0.028f) return w;
                    if (Rc(u, v, 0.64f, 0.22f, 0.86f, 0.78f) && !Rc(u, v, 0.68f, 0.26f, 0.82f, 0.74f)) return w; if (Seg(u, v, 0.1f, 0.14f, 0.9f, 0.14f) < 0.02f) return w; return c;
                }
                case 1: // треугольник «внимание»
                {
                    c = C(0.15f, 0.15f, 0.16f); float t = (0.9f - v) / 0.78f * 0.42f; if (v > 0.1f && v < 0.9f && Mathf.Abs(u - 0.5f) < t) { bool inner = v > 0.18f && v < 0.82f && Mathf.Abs(u - 0.5f) < t - 0.07f; if (!inner) return C(0.05f, 0.05f, 0.05f); c = C(0.95f, 0.78f, 0.08f); if (Rc(u, v, 0.465f, 0.4f, 0.535f, 0.66f) || D(u, v, 0.5f, 0.3f) < 0.04f) return C(0.05f, 0.05f, 0.05f); return c; } return c;
                }
                case 2: // «мокрый пол»: жёлтый знак с падающим человечком
                {
                    c = C(0.9f, 0.72f, 0.06f); if (Frame(u, v, 0.05f)) return C(0.08f, 0.08f, 0.08f); Color k = C(0.06f, 0.06f, 0.06f);
                    if (D(u, v, 0.58f, 0.7f) < 0.07f || Seg(u, v, 0.55f, 0.6f, 0.42f, 0.42f) < 0.04f || Seg(u, v, 0.42f, 0.42f, 0.24f, 0.4f) < 0.035f || Seg(u, v, 0.42f, 0.42f, 0.52f, 0.28f) < 0.035f || Seg(u, v, 0.55f, 0.6f, 0.72f, 0.5f) < 0.03f || Seg(u, v, 0.58f, 0.58f, 0.76f, 0.62f) < 0.03f) return k;
                    if (v < 0.2f && Mathf.Abs(v - 0.13f - Mathf.Sin(u * 22f) * 0.03f) < 0.018f) return k; return c;
                }
                case 3: // «пропал человек»
                {
                    c = C(0.86f, 0.82f, 0.7f) * (0.9f + 0.1f * Nz(u, v, 20f)); c.a = 1f; if (v > 0.84f) return C(0.7f, 0.1f, 0.08f); if (v < 0.07f && Mathf.Repeat(u * 8f, 1f) < 0.04f) return Mul(c, 0.5f);
                    if (Rc(u, v, 0.18f, 0.4f, 0.82f, 0.8f)) { float g = 0.35f + 0.2f * Nz(u, v, 9f, 2f); Color p = C(g, g, g * 1.02f); if (D(u, v, 0.5f, 0.64f) < 0.1f || (v < 0.55f && Mathf.Abs(u - 0.5f) < 0.2f - (0.55f - v) * 0.3f && v > 0.4f)) return C(0.08f, 0.08f, 0.08f); return p; }
                    for (int r = 0; r < 4; r++) { float ry = 0.3f - r * 0.065f; if (Mathf.Abs(v - ry) < 0.018f && u > 0.18f && u < 0.82f - r * 0.1f - H2(r, 4, 1) * 0.1f) return C(0.18f, 0.16f, 0.14f); } return c;
                }
                case 4: // пейзаж в раме
                {
                    if (Frame(u, v, 0.075f)) { if (Frame(u, v, 0.02f) || !Frame(u, v, 0.055f)) return Mul(Wood(u, v), 1.25f); return Mul(Wood(u, v), 0.7f); }
                    float sk = Mathf.Clamp01((v - 0.35f) * 1.6f); c = Color.Lerp(C(0.95f, 0.75f, 0.45f), C(0.35f, 0.5f, 0.75f), sk); if (D(u, v, 0.7f, 0.62f) < 0.07f) return C(1f, 0.93f, 0.7f);
                    float h1 = 0.4f + Mathf.Sin(u * 7f) * 0.07f + Mathf.Sin(u * 15f + 1f) * 0.03f; if (v < h1) c = Color.Lerp(C(0.15f, 0.28f, 0.14f), C(0.3f, 0.4f, 0.15f), v * 2f); if (v < 0.22f) c = C(0.2f * (0.8f + 0.4f * Nz(u, v, 30f)), 0.34f, 0.12f); return c * (0.9f + 0.1f * Nz(u, v, 60f));
                }
                case 5: // портрет без лица
                {
                    if (Frame(u, v, 0.075f)) { if (Frame(u, v, 0.02f) || !Frame(u, v, 0.055f)) return Mul(Wood(u, v), 1.2f); return Mul(Wood(u, v), 0.65f); }
                    c = Color.Lerp(C(0.07f, 0.12f, 0.12f), C(0.14f, 0.2f, 0.2f), v); float ex = (u - 0.5f) / 0.15f, ey = (v - 0.6f) / 0.2f; if (ex * ex + ey * ey < 1f) return C(0.78f, 0.7f, 0.6f) * (0.9f + 0.1f * Nz(u, v, 20f)) + new Color(0, 0, 0, 1f);
                    float sx = (u - 0.5f) / 0.32f, sy = (v - 0.12f) / 0.32f; if (sx * sx + sy * sy < 1f || (v < 0.4f && Mathf.Abs(u - 0.5f) < 0.04f + (0.4f - v) * 0.5f)) return C(0.06f, 0.06f, 0.08f); return c;
                }
                case 6: // море в сумерках
                {
                    if (Frame(u, v, 0.075f)) { if (Frame(u, v, 0.02f) || !Frame(u, v, 0.055f)) return Mul(Wood(u, v), 1.2f); return Mul(Wood(u, v), 0.65f); }
                    if (v > 0.45f) { c = Color.Lerp(C(0.9f, 0.45f, 0.25f), C(0.15f, 0.12f, 0.3f), (v - 0.45f) * 1.9f); if (D(u, v, 0.4f, 0.5f) < 0.09f) c = C(1f, 0.8f, 0.5f); return c; }
                    c = Color.Lerp(C(0.05f, 0.08f, 0.16f), C(0.5f, 0.3f, 0.25f), v * 1.6f); float wv = Mathf.Sin(u * 40f + v * 70f) * 0.5f + 0.5f; if (wv > 0.9f) c = Mul(c, 1.5f); if (Rc(u, v, 0.64f, 0.45f, 0.7f, 0.68f)) return C(0.8f, 0.8f, 0.75f); return c;
                }
                case 7: // детский рисунок: дом, солнце, и кто-то высокий
                {
                    c = C(0.93f, 0.9f, 0.8f) * (0.92f + 0.08f * Nz(u, v, 16f)); c.a = 1f; float j = 0.012f * (Nz(u, v, 40f, 5f) - 0.5f); Color crayon = C(0.8f, 0.15f, 0.12f);
                    if (Mathf.Abs(Seg(u, v, 0.1f, 0.38f, 0.1f, 0.12f) + j) < 0.012f || Seg(u, v, 0.1f, 0.12f, 0.36f, 0.12f) + j < 0.012f || Seg(u, v, 0.36f, 0.12f, 0.36f, 0.38f) + j < 0.012f || Seg(u, v, 0.1f, 0.38f, 0.36f, 0.38f) + j < 0.012f || Seg(u, v, 0.08f, 0.38f, 0.23f, 0.54f) + j < 0.012f || Seg(u, v, 0.23f, 0.54f, 0.38f, 0.38f) + j < 0.012f) return crayon;
                    if (Mathf.Abs(D(u, v, 0.8f, 0.82f) - 0.07f) < 0.012f) return C(0.95f, 0.8f, 0.1f); for (int k = 0; k < 8; k++) { float a = k * 0.785f; if (Seg(u, v, 0.8f + Mathf.Cos(a) * 0.09f, 0.82f + Mathf.Sin(a) * 0.09f, 0.8f + Mathf.Cos(a) * 0.14f, 0.82f + Mathf.Sin(a) * 0.14f) < 0.01f) return C(0.95f, 0.8f, 0.1f); }
                    Color kid = C(0.15f, 0.3f, 0.8f); if (Mathf.Abs(D(u, v, 0.5f, 0.34f) - 0.035f) < 0.01f || Seg(u, v, 0.5f, 0.3f, 0.5f, 0.2f) < 0.01f || Seg(u, v, 0.45f, 0.26f, 0.55f, 0.26f) < 0.01f || Seg(u, v, 0.5f, 0.2f, 0.46f, 0.12f) < 0.01f || Seg(u, v, 0.5f, 0.2f, 0.54f, 0.12f) < 0.01f) return kid;
                    Color dark = C(0.05f, 0.05f, 0.05f); if (D(u, v, 0.78f, 0.62f) < 0.04f || Seg(u, v, 0.78f, 0.58f, 0.78f, 0.28f) < 0.012f || Seg(u, v, 0.78f, 0.52f, 0.64f, 0.2f) < 0.01f || Seg(u, v, 0.78f, 0.52f, 0.9f, 0.18f) < 0.01f || Seg(u, v, 0.78f, 0.28f, 0.74f, 0.1f) < 0.01f || Seg(u, v, 0.78f, 0.28f, 0.82f, 0.1f) < 0.01f) return dark;
                    if (v < 0.09f && Mathf.Abs(v - 0.05f - Mathf.Sin(u * 90f) * 0.02f) < 0.012f) return C(0.2f, 0.55f, 0.2f); return c;
                }
                case 8: // граффити «глаз»
                {
                    float g = 0.42f + 0.2f * Nz(u, v, 12f) + 0.1f * Nz(u, v, 60f); c = C(g, g * 0.98f, g * 0.95f); Color red = C(0.78f, 0.06f, 0.05f); float ex = (u - 0.5f) / 0.38f, ey = (v - 0.55f) / 0.2f; float e = ex * ex + ey * ey;
                    if (Mathf.Abs(e - 1f) < 0.16f || D(u, v, 0.5f, 0.55f) < 0.09f) return red; if (u > 0.2f && u < 0.8f && v < 0.5f) { int col = (int)(u * 30f); if (Mathf.Repeat(u * 30f, 1f) < 0.12f && v > 0.18f + H2(col, 3, 9) * 0.3f && v < 0.5f && e < 2f) return red; } return c;
                }
                case 9: // план эвакуации
                {
                    c = C(0.88f, 0.86f, 0.74f) * (0.95f + 0.05f * Nz(u, v, 20f)); c.a = 1f; if (Frame(u, v, 0.03f)) return C(0.2f, 0.4f, 0.25f); Color k = C(0.15f, 0.15f, 0.15f);
                    if ((Mathf.Abs(u - 0.2f) < 0.01f && v > 0.2f && v < 0.8f) || (Mathf.Abs(u - 0.5f) < 0.01f && v > 0.2f && v < 0.55f) || (Mathf.Abs(u - 0.8f) < 0.01f && v > 0.2f && v < 0.8f) || (Mathf.Abs(v - 0.2f) < 0.01f && u > 0.2f && u < 0.8f) || (Mathf.Abs(v - 0.8f) < 0.01f && u > 0.2f && u < 0.8f) || (Mathf.Abs(v - 0.55f) < 0.01f && u > 0.2f && u < 0.5f) || (Mathf.Abs(v - 0.45f) < 0.01f && u > 0.5f && u < 0.8f)) return k;
                    if (D(u, v, 0.33f, 0.35f) < 0.035f) return C(0.85f, 0.1f, 0.1f); if (Mathf.Abs(D(u, v, 0.33f, 0.35f) - 0.06f) < 0.007f) return C(0.85f, 0.1f, 0.1f); if (Seg(u, v, 0.33f, 0.35f, 0.65f, 0.35f) < 0.008f && Mathf.Repeat(u * 24f, 1f) < 0.6f) return C(0.1f, 0.5f, 0.2f); return c;
                }
                case 10: // доска объявлений
                {
                    float g = 0.75f + 0.25f * Nz(u, v, 60f); c = C(0.55f * g, 0.38f * g, 0.22f * g); if (Frame(u, v, 0.04f)) return Mul(Wood(u, v), 0.9f);
                    float[][] notes = { new[] { 0.12f, 0.5f, 0.42f, 0.88f, 0.95f, 0.95f, 0.85f }, new[] { 0.55f, 0.58f, 0.88f, 0.9f, 0.9f, 0.85f, 0.55f }, new[] { 0.14f, 0.12f, 0.46f, 0.42f, 0.7f, 0.9f, 0.95f }, new[] { 0.58f, 0.1f, 0.86f, 0.5f, 0.95f, 0.95f, 0.9f } };
                    for (int n = 0; n < 4; n++) { var nt = notes[n]; if (Rc(u, v, nt[0], nt[1], nt[2], nt[3])) { if (D(u, v, (nt[0] + nt[2]) * 0.5f, nt[3] - 0.03f) < 0.018f) return C(0.85f, 0.1f, 0.1f); for (int r = 0; r < 5; r++) if (Mathf.Abs(v - (nt[3] - 0.09f - r * 0.05f)) < 0.01f && u < nt[2] - 0.04f - H2(n, r, 2) * 0.08f) return C(0.2f, 0.2f, 0.25f); return C(nt[4], nt[5], nt[6]); } }
                    return c;
                }
                case 11: // часы, остановившиеся на 3:33
                {
                    if (Frame(u, v, 0.06f)) return C(0.12f, 0.12f, 0.13f); c = C(0.9f, 0.88f, 0.8f) * (0.93f + 0.07f * Nz(u, v, 30f)); c.a = 1f; float r = D(u, v, 0.5f, 0.5f); if (r > 0.42f) return C(0.12f, 0.12f, 0.13f);
                    for (int k = 0; k < 12; k++) { float a = k * 0.5236f; if (Seg(u, v, 0.5f + Mathf.Sin(a) * 0.36f, 0.5f + Mathf.Cos(a) * 0.36f, 0.5f + Mathf.Sin(a) * 0.4f, 0.5f + Mathf.Cos(a) * 0.4f) < 0.012f) return C(0.1f, 0.1f, 0.1f); }
                    float ah = 1.86f, am = 3.46f; if (Seg(u, v, 0.5f, 0.5f, 0.5f + Mathf.Sin(ah) * 0.2f, 0.5f + Mathf.Cos(ah) * 0.2f) < 0.017f || Seg(u, v, 0.5f, 0.5f, 0.5f + Mathf.Sin(am) * 0.31f, 0.5f + Mathf.Cos(am) * 0.31f) < 0.011f || r < 0.025f) return C(0.08f, 0.08f, 0.08f); return c;
                }
                case 12: // «вход запрещён»
                {
                    c = C(0.92f, 0.92f, 0.9f); if (Frame(u, v, 0.04f)) return C(0.15f, 0.15f, 0.15f); float r = D(u, v, 0.5f, 0.5f); if ((r < 0.4f && r > 0.31f) || Seg(u, v, 0.28f, 0.72f, 0.72f, 0.28f) < 0.035f && r < 0.4f) return C(0.8f, 0.08f, 0.06f); if (r < 0.31f && D(u, v, 0.5f, 0.6f) < 0.05f || Rc(u, v, 0.45f, 0.3f, 0.55f, 0.55f) && r < 0.31f) return C(0.1f, 0.1f, 0.1f); return c;
                }
                case 13: // «высокое напряжение»
                {
                    c = C(0.15f, 0.15f, 0.16f); float t = (0.9f - v) / 0.78f * 0.42f; if (v > 0.1f && v < 0.9f && Mathf.Abs(u - 0.5f) < t) { bool inner = v > 0.18f && v < 0.82f && Mathf.Abs(u - 0.5f) < t - 0.07f; if (!inner) return C(0.05f, 0.05f, 0.05f); if (Seg(u, v, 0.55f, 0.74f, 0.44f, 0.5f) < 0.025f || Seg(u, v, 0.44f, 0.5f, 0.56f, 0.5f) < 0.025f || Seg(u, v, 0.56f, 0.5f, 0.45f, 0.26f) < 0.025f) return C(0.05f, 0.05f, 0.05f); return C(0.95f, 0.78f, 0.08f); } return c;
                }
                case 14: // выцветший смайл
                {
                    c = C(0.62f, 0.6f, 0.55f) * (0.85f + 0.15f * Nz(u, v, 14f, 4f)); c.a = 1f; float r = D(u, v, 0.5f, 0.5f); if (r < 0.4f) { Color y = C(0.78f, 0.7f, 0.25f) * (0.8f + 0.2f * Nz(u, v, 10f, 8f)); y.a = 1f;
                        if (D(u, v, 0.37f, 0.6f) < 0.05f || D(u, v, 0.63f, 0.6f) < 0.05f) return C(0.06f, 0.06f, 0.06f); float sm = D(u, v, 0.5f, 0.52f); if (Mathf.Abs(sm - 0.22f) < 0.022f && v < 0.5f) return C(0.06f, 0.06f, 0.06f); if (Mathf.Abs(sm - 0.22f) < 0.008f && v < 0.5f) return C(0.9f, 0.9f, 0.9f); return y; } return c;
                }
                default: // глаза в темноте
                {
                    c = C(0.03f, 0.03f, 0.035f) * (0.8f + 0.5f * Nz(u, v, 20f)); c.a = 1f; if (Frame(u, v, 0.03f)) return C(0.7f, 0.68f, 0.6f);
                    for (int e = 0; e < 2; e++) { float cx = 0.3f + e * 0.4f, cy = 0.55f; float ex = (u - cx) / 0.17f, ey = (v - cy) / 0.09f; float q = ex * ex + ey * ey; if (q < 1f) { if (D(u, v, cx + 0.015f, cy) < 0.028f) return C(0.02f, 0.02f, 0.02f); float vein = Mathf.Abs(Mathf.Sin(Mathf.Atan2(v - cy, u - cx) * 9f)); return Color.Lerp(C(0.9f, 0.88f, 0.8f), C(0.7f, 0.1f, 0.08f), vein > 0.9f ? 0.7f : 0.05f); } }
                    return c;
                }
            }
        }

        static Color Aged(int i, float u, float v)
        {
            Color c = Img(i, u, v); float g = 0.94f + 0.12f * H2((int)(u * 256f), (int)(v * 256f), i); float dirt = Nz(u, v, 6f, i * 3f); if (dirt > 0.62f) g *= Mathf.Lerp(1f, 0.84f, (dirt - 0.62f) * 4f);
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)); g *= Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(edge * 14f)); if (Mathf.Abs(u - 0.5f) < 0.004f || Mathf.Abs(v - 0.5f) < 0.004f) g *= 0.93f;
            return new Color(Mathf.Clamp01(c.r * g), Mathf.Clamp01(c.g * g), Mathf.Clamp01(c.b * g), 1f);
        }

        public static Texture2D Atlas()
        {
            if (atlas != null) return atlas; int S = N * CS;
            atlas = BrGfx.MakeTex(S, S, false, (u, v) => { int cx = Mathf.Min(N - 1, (int)(u * N)), cy = Mathf.Min(N - 1, (int)(v * N)); return Aged(cy * N + cx, u * N - cx, v * N - cy); }, 0.6f);
            return atlas;
        }

        // кадры: какие картинки висят на каких уровнях, и размер (ширина, высота, высота центра над полом)
        public static readonly float[][] Size = {
            new[]{0.5f,0.5f,1.7f}, new[]{0.4f,0.4f,1.5f}, new[]{0.4f,0.4f,1.2f}, new[]{0.55f,0.72f,1.5f}, new[]{0.9f,0.9f,1.6f}, new[]{0.7f,0.8f,1.6f}, new[]{0.9f,0.75f,1.6f}, new[]{0.45f,0.45f,1.2f},
            new[]{1.0f,1.0f,1.5f}, new[]{0.6f,0.6f,1.5f}, new[]{0.9f,0.6f,1.5f}, new[]{0.45f,0.45f,1.9f}, new[]{0.35f,0.35f,1.5f}, new[]{0.35f,0.35f,1.5f}, new[]{0.7f,0.7f,1.5f}, new[]{0.65f,0.65f,1.5f} };
        public static int[] Pool(string id, out float prob)
        {
            prob = 0.06f;
            switch (id)
            {
                case "0": prob = 0.24f; return new[] { 3, 7, 8, 14, 15, 1, 12, 9, 10, 11, 3, 8 };
                case "1": prob = 0.07f; return new[] { 1, 2, 12, 13, 9, 0, 8 };
                case "2": prob = 0.06f; return new[] { 1, 12, 13, 0, 8 };
                case "3": prob = 0.07f; return new[] { 13, 1, 12, 9, 0 };
                case "4": prob = 0.09f; return new[] { 9, 10, 11, 4, 0, 3, 12 };
                case "5": prob = 0.12f; return new[] { 4, 5, 6, 5, 11, 9, 4 };
                case "fun": prob = 0.1f; return new[] { 14, 7, 10, 15, 3 };
                case "37": prob = 0.06f; return new[] { 2, 12, 1, 0, 9 };
                case "!": prob = 0.08f; return new[] { 15, 8, 14, 3 };
                case "6": prob = 0.06f; return new[] { 15, 8, 3, 14 };
                case "11": prob = 0.07f; return new[] { 3, 8, 12, 9, 10 };
                case "9": prob = 0.05f; return new[] { 3, 7, 10 };
                case "15": prob = 0.05f; return new[] { 14, 3, 15, 8 };
                case "94": prob = 0.04f; return new[] { 3, 7, 10 };
                default: return null;
            }
        }
        public static bool Interior(string id) { switch (id) { case "0": case "1": case "2": case "3": case "4": case "5": case "fun": case "37": case "!": case "6": case "15": return true; default: return false; } }
        public static Color Trim(string id) { switch (id) { case "0": return C(0.3f, 0.27f, 0.12f); case "4": return C(0.32f, 0.32f, 0.3f); case "5": return C(0.18f, 0.11f, 0.07f); case "fun": return C(0.5f, 0.5f, 0.5f); case "37": return C(0.78f, 0.82f, 0.82f); case "6": return C(0.2f, 0.2f, 0.18f); case "15": return C(0.55f, 0.6f, 0.64f); case "!": return C(0.15f, 0.02f, 0.02f); default: return C(0.25f, 0.25f, 0.25f); } }

    }
}
