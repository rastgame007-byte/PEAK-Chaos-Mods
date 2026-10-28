// Расширение домена: кинематографичное вступление (замедление, зум, чёрные полосы, титр), раскрытие купола,
// вращающееся небо в два слоя, процедурные текстуры (считаются в фоне), своя «начинка» у каждого домена.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace PeakJJK
{
    internal static class Domains
    {
        const float Cast = 1.25f, OpenT = 1.1f, WinEnd = Cast + OpenT + 1.15f;
        static readonly string[] Titles = { "ХРАМ ЗЛОБЫ", "БЕЗГРАНИЧНАЯ ПУСТОТА", "САД ТЕНЕЙ: ХИМЕРА" };
        static readonly string[] Subs = { "Рёмен Сукуна", "Сатору Годзё", "Мегуми Фусигуро" };

        sealed class Dom
        {
            public GameObject root, dome, dome2, floor, props, shrine; public Light light; public Material dm, fm;
            public float t0, ut0, dur, radius, gy; public int style; public Color col; public Vector3 c, shrineBase;
            public float a1, a2, a3, a4, a5, nextFlash; public bool opened, closing, landed; public Transform[] tend; public float[] tph, tH;
        }
        static readonly List<Dom> active = new List<Dom>(); static Mesh sphere, disc; static bool fogSaved, fogWas;
        public static bool Any { get { return active.Count > 0; } }
        public static bool PlayerInside(Vector3 p) { foreach (var d in active) if ((p - d.root.transform.position).sqrMagnitude < d.radius * d.radius) return true; return false; }

        // ---------------- шум (с горизонтальной зацикленностью — без шва на куполе) ----------------
        static float Hh(int x, int y, int seed) { unchecked { int h = x * 374761393 + y * 668265263 + seed * 144665; h = (h ^ (h >> 13)) * 1274126177; h ^= h >> 16; return (h & 0xFFFFFF) / 16777216f; } }
        static float TN(float u, float v, int fx, int fy, int seed)
        {
            float x = u * fx, y = v * fy; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float xf = x - xi, yf = y - yi; xf = xf * xf * (3 - 2 * xf); yf = yf * yf * (3 - 2 * yf);
            int x0 = ((xi % fx) + fx) % fx, x1 = (((xi + 1) % fx) + fx) % fx;
            return Mathf.Lerp(Mathf.Lerp(Hh(x0, yi, seed), Hh(x1, yi, seed), xf), Mathf.Lerp(Hh(x0, yi + 1, seed), Hh(x1, yi + 1, seed), xf), yf);
        }
        static float Fbm(float u, float v, int fx, int fy, int oct, int seed) { float a = 0.5f, sum = 0f, norm = 0f; for (int i = 0; i < oct; i++) { sum += a * TN(u, v, fx << i, fy << i, seed + i * 7); norm += a; a *= 0.5f; } return sum / norm; }
        static float Stars(float u, float v, int cx, int cy, int seed, float density, float size)
        {
            float x = u * cx, y = v * cy; int xi = Mathf.Min(cx - 1, Mathf.FloorToInt(x)), yi = Mathf.FloorToInt(y); if (Hh(xi, yi, seed) > density) return 0f;
            float dx = (x - xi) - (0.2f + 0.6f * Hh(xi, yi, seed + 11)), dy = (y - yi) - (0.2f + 0.6f * Hh(xi, yi, seed + 23)); float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / size);
            return d * d * (0.4f + 0.6f * Hh(xi, yi, seed + 5));
        }
        static float Sm(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3f - 2f * x); }
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float Back(float x) { x = Mathf.Clamp01(x) - 1f; return 1f + 2.2f * x * x * x + 1.2f * x * x; }

        // ---------------- текстуры: считаем в фоновом потоке, в Unity создаём в основном ----------------
        sealed class Recipe { public int w, h; public bool repeat; public Func<float, float, Color> f; }
        static readonly Dictionary<string, Recipe> recipes = new Dictionary<string, Recipe>(); static readonly Dictionary<string, Texture2D> texs = new Dictionary<string, Texture2D>(); static readonly Dictionary<string, Color32[]> ready = new Dictionary<string, Color32[]>(); static readonly object lk = new object(); static bool warmStarted;
        static Color32[] Compute(Recipe r) { var px = new Color32[r.w * r.h]; for (int y = 0; y < r.h; y++) for (int x = 0; x < r.w; x++) px[y * r.w + x] = r.f((x + 0.5f) / r.w, (y + 0.5f) / r.h); return px; }
        static Texture2D Make(Recipe r, Color32[] px) { var t = new Texture2D(r.w, r.h, TextureFormat.RGBA32, true); t.wrapMode = r.repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; t.anisoLevel = 2; t.SetPixels32(px); t.Apply(true, false); return t; }
        static Texture2D Tx(string key)
        {
            Texture2D t; if (texs.TryGetValue(key, out t) && t != null) return t; Recipe r = Rec(key); Color32[] px = null;
            lock (lk) { if (ready.TryGetValue(key, out px)) ready.Remove(key); }
            if (px == null) px = Compute(r); t = Make(r, px); texs[key] = t; return t;
        }
        static void PumpReady() { string k = null; Color32[] px = null; lock (lk) { foreach (var kv in ready) { k = kv.Key; px = kv.Value; break; } if (k != null) ready.Remove(k); } if (k != null && !texs.ContainsKey(k)) texs[k] = Make(Rec(k), px); }
        static readonly string[] AllKeys = { "d0", "l0", "f0", "d1", "l1", "f1", "d2", "l2", "f2", "vig" };
        static void StartWarm()
        {
            if (warmStarted) return; warmStarted = true; foreach (var k in AllKeys) Rec(k);
            ThreadPool.QueueUserWorkItem(_ => { foreach (var k in AllKeys) { try { Recipe r; lock (lk) { r = recipes[k]; } var px = Compute(r); lock (lk) { ready[k] = px; } } catch { } } });
        }
        static Recipe Rec(string key)
        {
            lock (lk) { Recipe r; if (recipes.TryGetValue(key, out r)) return r; r = Build(key); recipes[key] = r; return r; }
        }
        static Recipe Build(string key)
        {
            switch (key)
            {
                case "d0": return new Recipe { w = 1024, h = 512, repeat = true, f = Sky0 };
                case "d1": return new Recipe { w = 1024, h = 512, repeat = true, f = Sky1 };
                case "d2": return new Recipe { w = 1024, h = 512, repeat = true, f = Sky2 };
                case "l0": return new Recipe { w = 512, h = 256, repeat = true, f = Layer0 };
                case "l1": return new Recipe { w = 512, h = 256, repeat = true, f = Layer1 };
                case "l2": return new Recipe { w = 512, h = 256, repeat = true, f = Layer2 };
                case "f0": return new Recipe { w = 512, h = 512, repeat = false, f = Floor0 };
                case "f1": return new Recipe { w = 512, h = 512, repeat = false, f = Floor1 };
                case "f2": return new Recipe { w = 512, h = 512, repeat = false, f = Floor2 };
                default: return new Recipe { w = 128, h = 128, repeat = false, f = (u, v) => { float d = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f)) * 1.4142f; return new Color(0, 0, 0, Sm(0.42f, 1.02f, d)); } };
            }
        }

        // небо: v=0.5 — горизонт, v→1 зенит
        static Color Sky0(float u, float v)
        {
            float el = Mathf.Clamp01((v - 0.5f) * 2f); float c1 = Fbm(u, v, 6, 3, 5, 1), c2 = Fbm(u + 0.13f, v, 12, 6, 4, 9); float cloud = Mathf.Clamp01(c1 * 1.7f - 0.25f);
            Color c = Color.Lerp(new Color(0.03f, 0f, 0.02f), new Color(0.8f, 0.07f, 0.08f), Mathf.Clamp01(cloud * 1.35f) * (0.4f + 0.6f * (1f - el * 0.5f)));
            c += new Color(1f, 0.28f, 0.08f) * (Mathf.Pow(1f - el, 4f) * (0.35f + 0.65f * c2) * 0.95f);
            float du = Mathf.Abs(u - 0.25f); du = Mathf.Min(du, 1f - du) * 2.2f; float md = Mathf.Sqrt(du * du + (v - 0.74f) * (v - 0.74f));
            if (md < 0.085f) { float cr = Fbm(u, v, 40, 20, 3, 31); c = Color.Lerp(new Color(1f, 0.85f, 0.75f), new Color(0.85f, 0.25f, 0.2f), Sm(0.5f, 0.8f, cr) * 0.7f + Sm(0.05f, 0.085f, md) * 0.5f); }
            c += new Color(1f, 0.3f, 0.2f) * (Mathf.Exp(-md * 9f) * 1.0f);
            float ln = Mathf.Abs(Mathf.Sin((u * 15f + v * 5f + c2 * 1.6f) * Mathf.PI)); if (ln > 0.9935f && el > 0.04f && Fbm(u, v, 4, 2, 2, 60) > 0.56f) c = Color.Lerp(c, new Color(1f, 0.93f, 0.88f), (ln - 0.9935f) / 0.0065f);
            if (v < 0.5f) c *= 0.35f; c.a = 1f; return c;
        }
        static Color Sky1(float u, float v)
        {
            float band = v - 0.62f - Mathf.Sin(u * Mathf.PI * 4f) * 0.09f - Mathf.Sin(u * Mathf.PI * 2f + 1f) * 0.05f; float g = Mathf.Exp(-band * band * 38f);
            float n1 = Fbm(u, v, 6, 3, 5, 3), n2 = Fbm(u + 0.4f, v, 14, 7, 4, 17);
            Color c = new Color(0.003f, 0.004f, 0.02f); c += Color.Lerp(new Color(0.35f, 0.12f, 0.7f), new Color(0.1f, 0.55f, 0.95f), n2) * (g * Mathf.Clamp01(n1 * 1.6f - 0.1f) * 1.1f);
            c += new Color(1f, 0.9f, 0.85f) * (g * g * g * 0.35f * n2);
            float s = Stars(u, v, 360, 180, 1, 0.22f, 0.28f) * 1.3f + Stars(u, v, 140, 70, 2, 0.13f, 0.3f) * 2f + Stars(u, v, 60, 30, 3, 0.05f, 0.3f) * 3.5f;
            float tint = Hh(Mathf.FloorToInt(u * 48f), Mathf.FloorToInt(v * 24f), 4); c += Color.Lerp(new Color(0.7f, 0.8f, 1f), new Color(1f, 0.8f, 0.9f), tint) * s;
            c.a = 1f; return c;
        }
        static Color Sky2(float u, float v)
        {
            float n = Fbm(u, v, 5, 4, 5, 5), w = Fbm(u * 2f, v * 0.6f, 24, 4, 4, 13); float el = Mathf.Clamp01((v - 0.5f) * 2f);
            Color c = Color.Lerp(new Color(0f, 0f, 0.005f), new Color(0.02f, 0.05f, 0.16f), Mathf.Pow(n, 1.8f) * (0.4f + 0.6f * (1f - el)));
            c += new Color(0.05f, 0.18f, 0.4f) * (Mathf.Pow(Mathf.Clamp01(w * 1.3f - 0.25f), 3f) * (0.3f + 0.7f * (1f - el)));
            float hz = Mathf.Pow(1f - el, 6f); c += new Color(0.06f, 0.14f, 0.35f) * hz;
            float s = Stars(u, v, 220, 110, 6, 0.12f, 0.3f) * 1.4f; c += new Color(0.4f, 0.7f, 1f) * s;
            float sh = Mathf.Pow(Mathf.Clamp01(Fbm(u * 3f, v, 30, 3, 3, 55) * 1.8f - 0.75f), 2f); c += new Color(0.08f, 0.25f, 0.6f) * (sh * el * 1.2f);
            if (v < 0.5f) c *= Mathf.Lerp(0.15f, 1f, Sm(0.38f, 0.5f, v)); c.a = 1f; return c;
        }
        // слои-«дымка» (рисуются поверх купола с альфой, вращаются в противоположную сторону)
        static Color Layer0(float u, float v)
        {
            float n = Fbm(u, v * 1.6f, 8, 5, 5, 21); float st = Fbm(u * 3f, v, 24, 4, 4, 5); float a = Mathf.Clamp01(n * 1.9f - 0.8f) * Mathf.Clamp01(st * 1.6f) * 0.65f;
            return new Color(1f, Mathf.Lerp(0.25f, 0.5f, n), 0.12f, a * Mathf.Clamp01((v - 0.44f) * 3f));
        }
        static Color Layer1(float u, float v)
        {
            float n = Fbm(u, v, 7, 4, 5, 33); float ang = Mathf.Sin(u * Mathf.PI * 2f * 3f + v * 6f) * 0.5f + 0.5f; float a = Mathf.Clamp01(n * 1.8f - 0.7f) * (0.35f + 0.65f * ang) * 0.5f;
            return new Color(Mathf.Lerp(0.4f, 0.3f, n), Mathf.Lerp(0.4f, 0.9f, n), 1f, a * Mathf.Clamp01((v - 0.35f) * 3f));
        }
        static Color Layer2(float u, float v)
        {
            float n = Fbm(u * 2f, v, 12, 4, 5, 41); float a = Mathf.Clamp01(n * 1.7f - 0.55f) * 0.5f; return new Color(0.2f, 0.45f, 1f, a * Mathf.Clamp01((v - 0.42f) * 3.5f));
        }
        // полы (диск: центр — (0.5,0.5))
        static Color Floor0(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f; float n = Fbm(u, v, 6, 6, 5, 8), n2 = Fbm(u, v, 14, 14, 3, 19);
            Color c = Color.Lerp(new Color(0.11f, 0.004f, 0.012f), new Color(0.5f, 0.03f, 0.04f), n * n * 1.4f);
            float rip = Mathf.Pow(Mathf.Abs(Mathf.Sin(r * 70f + n * 9f)), 14f); c += new Color(0.9f, 0.12f, 0.1f) * (rip * 0.35f * (0.4f + n2));
            float gl = Mathf.Pow(Mathf.Clamp01(n2 * 1.6f - 0.7f), 2f); c += new Color(1f, 0.35f, 0.2f) * (gl * 0.35f);
            c = Color.Lerp(c, new Color(0.5f, 0.1f, 0.06f), Sm(0.82f, 1f, r)); c.a = 1f; return c;
        }
        static Color Floor1(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f; float ang = Mathf.Atan2(dy, dx);
            Color c = new Color(0.004f, 0.006f, 0.03f) + new Color(0.03f, 0.05f, 0.18f) * Fbm(u, v, 5, 5, 4, 12);
            float ring = Mathf.Pow(Mathf.Abs(Mathf.Sin(r * Mathf.PI * 12f)), 40f), spoke = Mathf.Pow(Mathf.Abs(Mathf.Sin(ang * 12f)), 60f) * Mathf.Clamp01(r * 1.4f);
            c += new Color(0.2f, 0.55f, 1f) * ((ring + spoke * 0.8f) * 0.55f); c += Color.white * (Stars(u, v, 200, 200, 14, 0.4f, 0.5f) * 1.6f);
            c = Color.Lerp(c, new Color(0.06f, 0.08f, 0.25f), Sm(0.8f, 1f, r)); c.a = 1f; return c;
        }
        static Color Floor2(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f; Color c = new Color(0f, 0f, 0.004f);
            for (int i = 0; i < 5; i++) { float cx = Hh(i, 1, 70) , cy = Hh(i, 2, 70); float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)); c += new Color(0.1f, 0.3f, 0.75f) * (Mathf.Pow(Mathf.Abs(Mathf.Sin(d * 90f)), 18f) * Mathf.Exp(-d * 5f) * 0.55f); }
            c += new Color(0.01f, 0.03f, 0.1f) * Fbm(u, v, 8, 8, 4, 15); c = Color.Lerp(c, new Color(0.03f, 0.07f, 0.2f), Sm(0.8f, 1f, r)); c.a = 1f; return c;
        }

        // ---------------- меши ----------------
        static Mesh Sphere()
        {
            if (sphere != null) return sphere; int lat = 32, lon = 64; var vs = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
            for (int i = 0; i <= lat; i++) for (int j = 0; j <= lon; j++) { float a = Mathf.PI * i / lat, b = 2f * Mathf.PI * j / lon; vs.Add(new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b))); uv.Add(new Vector2(j / (float)lon, 1f - i / (float)lat)); }
            for (int i = 0; i < lat; i++) for (int j = 0; j < lon; j++) { int a = i * (lon + 1) + j, b = a + lon + 1; ts.Add(a); ts.Add(a + 1); ts.Add(b); ts.Add(a + 1); ts.Add(b + 1); ts.Add(b); }
            sphere = new Mesh { name = "jjk_domesph" }; sphere.SetVertices(vs); sphere.SetUVs(0, uv); sphere.SetTriangles(ts, 0); sphere.RecalculateBounds(); return sphere;
        }
        static Mesh Disc()
        {
            if (disc != null) return disc; int seg = 64; var vs = new List<Vector3> { Vector3.zero }; var uv = new List<Vector2> { new Vector2(.5f, .5f) }; var ts = new List<int>();
            for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2f / seg; vs.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))); uv.Add(new Vector2(Mathf.Cos(a) * .5f + .5f, Mathf.Sin(a) * .5f + .5f)); }
            for (int i = 1; i <= seg; i++) { ts.Add(0); ts.Add(i + 1); ts.Add(i); }
            disc = new Mesh { name = "jjk_domedisc" }; disc.SetVertices(vs); disc.SetUVs(0, uv); disc.SetTriangles(ts, 0); disc.RecalculateBounds(); return disc;
        }
        static GameObject Hide(GameObject g) { g.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(g); return g; }
        static GameObject ribsPf, skullsPf; static Mesh tendBody, tendTip;
        static GameObject Ribs()
        {
            if (ribsPf != null) return ribsPf; var m = new ModelB("Ribs"); m.P("r"); Color bc = new Color(0.86f, 0.8f, 0.68f); var bone = m.Lit("r", bc, 0.3f); int N = 16;
            Func<float, Vector3> pt = (t) => new Vector3(0f, 0.92f * Mathf.Sin(t * Mathf.PI * 0.5f), 0f) + Vector3.right * (0.97f * (1f - Mathf.Pow(t, 1.7f)) + 0.04f);
            for (int i = 0; i < N; i++)
            {
                var q = Quaternion.Euler(0, i * 360f / N, 0); Vector3 prev = q * pt(0f);
                for (int s = 1; s <= 14; s++) { float t = s / 14f; Vector3 p = q * pt(t); bone.Tube(prev, p, 0.016f * (1f - (s - 1) / 14f * 0.75f), 0.016f * (1f - s / 14f * 0.75f), bc, 6, false); prev = p; }
            }
            foreach (float t in new[] { 0.28f, 0.52f, 0.76f }) { Vector3 p = pt(t); bone.Torus(Vector3.up * p.y, Vector3.up, p.x, 0.0065f, bc, 48, 5); }
            ribsPf = Hide(m.Finish()); return ribsPf;
        }
        static GameObject Skulls()
        {
            if (skullsPf != null) return skullsPf; var m = new ModelB("Skulls"); m.P("s"); Color bc = new Color(0.82f, 0.77f, 0.66f); var bone = m.Lit("s", bc, 0.3f); var dark = m.Lit("s", new Color(0.02f, 0.01f, 0.01f), 0.1f); var rnd = new System.Random(7); const float R = 36f;
            for (int i = 0; i < 56; i++)
            {
                double ang = rnd.NextDouble() * Math.PI * 2; float r = (float)Math.Sqrt(rnd.NextDouble()) * 0.88f * R; if (r < 4.5f) r += 5f; Vector3 p = new Vector3((float)Math.Cos(ang) * r, 0.06f, (float)Math.Sin(ang) * r); float sz = 1.4f + (float)rnd.NextDouble() * 1.1f;
                var q = Quaternion.Euler((float)rnd.NextDouble() * 50f - 25f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 40f - 20f);
                bone.Ell(p + Vector3.up * 0.2f * sz, new Vector3(0.22f, 0.19f, 0.24f) * sz, q, bc, 8, 11); bone.Ell(p + q * new Vector3(0f, 0.0f, 0.16f) * sz, new Vector3(0.12f, 0.07f, 0.14f) * sz, q, bc, 6, 8);
                dark.Ell(p + q * new Vector3(-0.09f, 0.25f, 0.17f) * sz, new Vector3(0.06f, 0.065f, 0.04f) * sz, q, Color.black, 6, 8); dark.Ell(p + q * new Vector3(0.09f, 0.25f, 0.17f) * sz, new Vector3(0.06f, 0.065f, 0.04f) * sz, q, Color.black, 6, 8);
            }
            for (int i = 0; i < 46; i++)
            {
                double ang = rnd.NextDouble() * Math.PI * 2; float r = (float)Math.Sqrt(rnd.NextDouble()) * 0.9f * R; Vector3 p = new Vector3((float)Math.Cos(ang) * r, 0.15f, (float)Math.Sin(ang) * r); double a2 = rnd.NextDouble() * Math.PI * 2; float len = 1.6f + (float)rnd.NextDouble() * 2.4f;
                Vector3 d = new Vector3((float)Math.Cos(a2), (float)rnd.NextDouble() * 0.25f, (float)Math.Sin(a2)); bone.Tube(p, p + d * len, 0.09f, 0.09f, bc, 6, false); bone.Ell(p, new Vector3(0.18f, 0.16f, 0.18f), Quaternion.identity, bc, 5, 7); bone.Ell(p + d * len, new Vector3(0.18f, 0.16f, 0.18f), Quaternion.identity, bc, 5, 7);
            }
            skullsPf = Hide(m.Finish()); return skullsPf;
        }
        static Vector3 TendPt(float t) { return new Vector3(Mathf.Sin(t * 2.4f) * 0.13f * t, t, Mathf.Sin(t * 1.5f + 0.7f) * 0.06f * t); }
        static void TendMeshes()
        {
            if (tendBody != null) return; var mb = new MB(); Vector3 prev = TendPt(0f); Color k = new Color(0.02f, 0.02f, 0.05f);
            for (int s = 1; s <= 12; s++) { float t = s / 12f; Vector3 p = TendPt(t); mb.Tube(prev, p, 0.075f * Mathf.Pow(1f - (s - 1) / 12f, 0.8f) + 0.004f, 0.075f * Mathf.Pow(1f - s / 12f, 0.8f) + 0.004f, k, 7, false); prev = p; }
            tendBody = mb.ToMesh("jjk_tend"); var tb = new MB(); tb.Ell(TendPt(1f), new Vector3(0.03f, 0.05f, 0.03f), new Color(0.4f, 0.7f, 1f)); tendTip = tb.ToMesh("jjk_tendtip");
        }

        static GameObject Shrine(float scale)
        {
            var m = new ModelB("Shrine"); m.P("s"); var dk = m.Lit("s", new Color(0.05f, 0.04f, 0.05f), 0.4f); var rd = m.Lit("s", new Color(0.55f, 0.05f, 0.05f), 0.4f); var bn = m.Lit("s", new Color(0.88f, 0.84f, 0.75f), 0.3f); var gl = m.Glow("s", new Color(1f, 0.25f, 0.15f));
            dk.Box(new Vector3(0, 0.3f, 0), new Vector3(9f, 0.6f, 6f), new Color(0.05f, 0.04f, 0.05f)); dk.Box(new Vector3(0, 0.8f, 0), new Vector3(7.5f, 0.4f, 5f), new Color(0.08f, 0.06f, 0.07f));
            foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) { dk.Tube(new Vector3(sx * 2.8f, 1f, sz * 1.8f), new Vector3(sx * 2.8f, 6.2f, sz * 1.8f), 0.38f, 0.33f, new Color(0.07f, 0.05f, 0.06f), 8, true); gl.Torus(new Vector3(sx * 2.8f, 5.6f, sz * 1.8f), Vector3.up, 0.45f, 0.04f, new Color(1f, 0.25f, 0.15f), 14, 5); }
            rd.Box(new Vector3(0, 6.5f, 0), new Vector3(8.8f, 0.5f, 6.4f), new Color(0.5f, 0.04f, 0.05f)); dk.Box(new Vector3(0, 7.1f, 0), new Vector3(7.4f, 0.5f, 5.4f), new Color(0.06f, 0.04f, 0.05f)); rd.Box(new Vector3(0, 7.7f, 0), new Vector3(5.6f, 0.5f, 4.2f), new Color(0.5f, 0.04f, 0.05f)); dk.Box(new Vector3(0, 8.4f, 0), new Vector3(3.6f, 0.9f, 3f), new Color(0.06f, 0.04f, 0.05f));
            foreach (int sx in new[] { -1, 1 }) { dk.Tube(new Vector3(sx * 4.6f, 6.6f, 3.4f), new Vector3(sx * 5.8f, 8.2f, 3.4f), 0.2f, 0.03f, new Color(0.07f, 0.05f, 0.06f), 6, false); dk.Tube(new Vector3(sx * 3.4f, 7.8f, 2.4f), new Vector3(sx * 4.2f, 9.2f, 2.4f), 0.16f, 0.03f, new Color(0.07f, 0.05f, 0.06f), 6, false); }
            for (int i = -2; i <= 2; i++) { bn.Ell(new Vector3(i * 1.4f, 5.6f, 3.35f), new Vector3(0.32f, 0.38f, 0.28f), bn.Count < 0 ? Color.white : new Color(0.88f, 0.84f, 0.75f)); gl.Ell(new Vector3(i * 1.4f - 0.1f, 5.65f, 3.6f), new Vector3(0.07f, 0.09f, 0.05f), new Color(1f, 0.25f, 0.15f)); gl.Ell(new Vector3(i * 1.4f + 0.1f, 5.65f, 3.6f), new Vector3(0.07f, 0.09f, 0.05f), new Color(1f, 0.25f, 0.15f)); bn.Tube(new Vector3(i * 1.4f - 0.2f, 5.9f, 3.3f), new Vector3(i * 1.4f - 0.65f, 6.4f, 3.2f), 0.06f, 0.01f, new Color(0.88f, 0.84f, 0.75f), 5, false); bn.Tube(new Vector3(i * 1.4f + 0.2f, 5.9f, 3.3f), new Vector3(i * 1.4f + 0.65f, 6.4f, 3.2f), 0.06f, 0.01f, new Color(0.88f, 0.84f, 0.75f), 5, false); }
            var go = m.Finish(); go.transform.localScale = Vector3.one * scale; return go;
        }


        // ---------------- открытие ----------------
        public static void Open(Vector3 center, float radius, int style, float dur, Color col)
        {
            StartWarm(); style = Mathf.Clamp(style, 0, 2);
            var d = new Dom { t0 = Time.time, ut0 = Time.unscaledTime, dur = dur, radius = radius, style = style, col = col, c = center }; bool f; d.gy = World.Ground(center, out f); if (!f) d.gy = center.y - 1f;
            d.root = new GameObject("JJK_Domain"); d.root.transform.position = center; string sk = style.ToString();
            d.dm = Gfx.Dome(Tx("d" + sk), Color.white); d.dome = new GameObject("dome"); d.dome.transform.SetParent(d.root.transform, false); d.dome.AddComponent<MeshFilter>().sharedMesh = Sphere(); var mr = d.dome.AddComponent<MeshRenderer>(); mr.sharedMaterial = d.dm; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            d.dome2 = new GameObject("dome2"); d.dome2.transform.SetParent(d.root.transform, false); d.dome2.AddComponent<MeshFilter>().sharedMesh = Sphere(); var mr2 = d.dome2.AddComponent<MeshRenderer>(); mr2.sharedMaterial = Gfx.Fade(Tx("l" + sk), Color.white, true); mr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr2.receiveShadows = false;
            d.fm = Gfx.Dome(Tx("f" + sk), Color.white); d.floor = new GameObject("floor"); d.floor.transform.SetParent(d.root.transform, false); d.floor.transform.position = new Vector3(center.x, d.gy + 0.06f, center.z); d.floor.AddComponent<MeshFilter>().sharedMesh = Disc(); var fr = d.floor.AddComponent<MeshRenderer>(); fr.sharedMaterial = d.fm; fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fr.receiveShadows = false;
            d.props = new GameObject("props"); d.props.transform.SetParent(d.root.transform, false); d.props.transform.position = new Vector3(center.x, d.gy, center.z);
            var lg = new GameObject("light"); lg.transform.SetParent(d.root.transform, false); lg.transform.localPosition = Vector3.up * 6f; d.light = lg.AddComponent<Light>(); d.light.type = LightType.Point; d.light.color = style == 1 ? new Color(0.7f, 0.8f, 1f) : col; d.light.range = radius * 1.5f; d.light.intensity = 0f; d.light.shadows = LightShadows.None;
            if (style == 0)
            {
                var rb = UnityEngine.Object.Instantiate(Ribs()); rb.transform.SetParent(d.props.transform, false); rb.transform.localScale = Vector3.one * radius; rb.SetActive(true);
                var sc = UnityEngine.Object.Instantiate(Skulls()); sc.transform.SetParent(d.props.transform, false); sc.transform.localScale = Vector3.one * (radius / 36f); sc.SetActive(true);
                d.shrine = Shrine(radius / 20f); d.shrine.transform.SetParent(d.root.transform, true); d.shrineBase = new Vector3(center.x, d.gy + 0.06f, center.z) + Pl.Fwd * radius * 0.3f; d.shrine.transform.rotation = Quaternion.LookRotation(-Pl.Fwd); d.shrine.SetActive(false);
            }
            else if (style == 2)
            {
                TendMeshes(); int n = 24; d.tend = new Transform[n]; d.tph = new float[n]; d.tH = new float[n]; var bm = Gfx.Lit(new Color(0.02f, 0.02f, 0.05f), 0.5f); var tm = Gfx.Glow(new Color(0.4f, 0.7f, 1f));
                for (int i = 0; i < n; i++)
                {
                    var g = new GameObject("tendril"); g.transform.SetParent(d.props.transform, false); float a = UnityEngine.Random.value * Mathf.PI * 2f, r = Mathf.Sqrt(UnityEngine.Random.Range(0.03f, 0.78f)) * radius; g.transform.localPosition = new Vector3(Mathf.Cos(a) * r, -0.2f, Mathf.Sin(a) * r);
                    g.AddComponent<MeshFilter>().sharedMesh = tendBody; var m1 = g.AddComponent<MeshRenderer>(); if (bm != null) m1.sharedMaterial = bm; m1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var tp = new GameObject("tip"); tp.transform.SetParent(g.transform, false); tp.AddComponent<MeshFilter>().sharedMesh = tendTip; var m2 = tp.AddComponent<MeshRenderer>(); if (tm != null) m2.sharedMaterial = tm; m2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    d.tend[i] = g.transform; d.tph[i] = UnityEngine.Random.value * 6.28f; d.tH[i] = UnityEngine.Random.Range(4f, 10f) * Mathf.Clamp(radius / 26f, 0.7f, 1.4f); g.transform.localScale = Vector3.zero;
                }
            }
            d.dome.SetActive(false); d.dome2.SetActive(false); d.floor.SetActive(false); d.props.SetActive(false);
            active.Add(d); if (!fogSaved) { fogSaved = true; fogWas = RenderSettings.fog; }
            Sfx.Charge(center, 0.9f); Fx.Slow(0.28f, Cast); Fx.Orb(center + Vector3.up * 1.1f, 1.5f, new Color(col.r, col.g, col.b, 0.5f), Cast, true); Fx.Ring(new Vector3(center.x, d.gy + 0.1f, center.z), Vector3.up, col, 7f, 0.9f, 0.25f);
        }

        static int Acc(ref float a, float rate, float dt) { a += rate * dt; int n = (int)a; a -= n; return Mathf.Min(n, 10); }
        static Vector3 Dir(bool upper) { var v = UnityEngine.Random.onUnitSphere; if (upper) v.y = Mathf.Abs(v.y) * 0.9f + 0.05f; return v.normalized; }
        static Vector3 InDome(Dom d, float r0, float r1, bool upper) { return d.c + Dir(upper) * UnityEngine.Random.Range(r0, r1) * d.radius; }
        static Vector3 OnFloor(Dom d, float r0, float r1) { float a = UnityEngine.Random.value * Mathf.PI * 2f, r = Mathf.Sqrt(UnityEngine.Random.Range(r0 * r0, r1 * r1)) * d.radius; return new Vector3(d.c.x + Mathf.Cos(a) * r, d.gy + 0.12f, d.c.z + Mathf.Sin(a) * r); }

        static void Streak(Vector3 a, Vector3 b, Color c, float w, float dur)
        {
            var go = new GameObject("JJK_Streak"); var lr = go.AddComponent<LineRenderer>(); lr.material = Gfx.Fade(null, Color.white, true); lr.useWorldSpace = true; lr.positionCount = 2; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Vector3 n = (b - a); float len = n.magnitude; n /= Mathf.Max(0.01f, len); float tail = Mathf.Min(len * 0.4f, 12f);
            Fx.Add(go, dur, (k, g) => { float hd = len * (1f - Mathf.Pow(1f - k, 2f)); lr.SetPosition(1, a + n * hd); lr.SetPosition(0, a + n * Mathf.Max(0f, hd - tail)); float al = Mathf.Sin(k * Mathf.PI); lr.startColor = new Color(c.r, c.g, c.b, 0f); lr.endColor = new Color(c.r, c.g, c.b, al * c.a); lr.startWidth = 0.01f; lr.endWidth = w * (0.6f + 0.4f * al); });
        }

        public static void Tick()
        {
            StartWarm(); PumpReady();
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var d = active[i]; if (d.root == null) { active.RemoveAt(i); continue; }
                try { Step(d); } catch (Exception e) { Plugin.Log("домен", e); }
                if (Time.time - d.t0 >= d.dur) { Finish(d); active.RemoveAt(i); }
            }
            if (active.Count > 0) { if (RenderSettings.fog) RenderSettings.fog = false; } else if (fogSaved) { RenderSettings.fog = fogWas; fogSaved = false; }
        }

        static void Finish(Dom d)
        {
            Vector3 c = d.c; Fx.Burst(c + Vector3.up * 2f, 70, d.radius * 0.7f, 0.5f, d.col, 1.3f); Fx.Burst(c + Vector3.up * 2f, 30, d.radius * 0.5f, 0.35f, Color.white, 1.0f); Fx.Flash(d.style == 1 ? new Color(0.85f, 0.9f, 1f) : Color.white, 0.5f, 3f); Fx.Shake(0.35f, 0.5f);
            Fx.Kill(d.root); if (d.dm != null) UnityEngine.Object.Destroy(d.dm); if (d.fm != null) UnityEngine.Object.Destroy(d.fm);
        }

        static void Step(Dom d)
        {
            float dt = Time.deltaTime, age = Time.time - d.t0, u = Time.unscaledTime - d.ut0; Vector3 c = d.c;
            if (u < Cast)
            {
                float t = u / Cast; Fx.FovTarget = -20f * Smooth(t); int n = Mathf.CeilToInt(90f * Time.unscaledDeltaTime);
                for (int i = 0; i < n; i++) { Vector3 p = c + Vector3.up * 1.1f + UnityEngine.Random.onUnitSphere * Mathf.Lerp(7f, 2.5f, t) * UnityEngine.Random.Range(0.6f, 1.2f); Fx.Spark(p, (c + Vector3.up * 1.1f - p) * 2.2f, UnityEngine.Random.Range(0.12f, 0.3f), UnityEngine.Random.value < 0.3f ? Color.white : d.col, 0.45f); }
                if (t > 0.55f && d.a5 < 1f) { d.a5 = 1f; Fx.Ring(new Vector3(c.x, d.gy + 0.1f, c.z), Vector3.up, d.col, 5f, 0.7f, 0.3f); Fx.Shake(0.12f, 0.7f); }
                return;
            }
            if (!d.opened)
            {
                d.opened = true; d.dome.SetActive(true); d.dome2.SetActive(true); d.floor.SetActive(true); d.props.SetActive(true); if (d.shrine != null) d.shrine.SetActive(true);
                Vector3 g0 = new Vector3(c.x, d.gy + 0.15f, c.z); Fx.Flash(d.style == 1 ? new Color(0.9f, 0.95f, 1f) : Color.Lerp(d.col, Color.white, 0.5f), 0.95f, 1.3f); Fx.Shake(0.8f, 1.4f); Fx.FovTarget = 18f; Sfx.Domain(1f); Sfx.Boom(c, 1f);
                Fx.Ring(g0, Vector3.up, Color.white, d.radius * 1.05f, 0.9f, 1.2f); Fx.Ring(g0, Vector3.up, d.col, d.radius * 1.0f, 1.3f, 0.9f); Fx.Ring(g0 + Vector3.up * 3f, Vector3.up, d.col, d.radius * 0.8f, 1.7f, 0.6f);
                for (int i = 0; i < 70; i++) { float a = i / 70f * Mathf.PI * 2f; Vector3 p = g0 + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * d.radius * 0.1f; Fx.Puff(p, new Vector3(Mathf.Cos(a), 0.12f, Mathf.Sin(a)) * d.radius * 0.85f, 5f, new Color(d.col.r * 0.4f, d.col.g * 0.4f, d.col.b * 0.4f, 0.45f), 1.4f, true); }
                Fx.Light(c + Vector3.up * 4f, d.col, 9f, d.radius * 2f, 1.2f);
            }
            float ou = u - Cast, openK = Mathf.Clamp01(ou / OpenT), k = openK < 1f ? Back(openK) : 1f; float closeK = Mathf.Clamp01((d.dur - age) / 0.9f);
            if (closeK < 1f && !d.closing)
            {
                d.closing = true; Fx.Flash(d.col, 0.4f, 3f); Fx.Shake(0.45f, 0.8f); Sfx.Boom(c, 0.8f); Fx.Ring(new Vector3(c.x, d.gy + 0.15f, c.z), Vector3.up, d.col, d.radius * 1.1f, 0.8f, 0.8f);
                for (int i = 0; i < 90; i++) { Vector3 p = c + Dir(true) * d.radius * 0.97f; Fx.Spark(p, -(p - c).normalized * UnityEngine.Random.Range(4f, 16f), UnityEngine.Random.Range(0.2f, 0.5f), UnityEngine.Random.value < 0.4f ? Color.white : d.col, 1f); }
            }
            k *= Smooth(closeK); float R = Mathf.Max(0.05f, d.radius * k);
            d.dome.transform.localScale = Vector3.one * R; d.dome2.transform.localScale = Vector3.one * R * 0.985f; d.floor.transform.localScale = new Vector3(R, 1f, R); d.props.transform.localScale = Vector3.one * Mathf.Max(0.001f, k);
            float spin = d.style == 0 ? 2.5f : d.style == 1 ? 1.2f : 1.8f; d.dome.transform.Rotate(0f, spin * dt, 0f, Space.World); d.dome2.transform.Rotate(0f, -spin * 3.2f * dt, 0f, Space.World); d.floor.transform.Rotate(0f, (d.style == 2 ? -4f : 2f) * dt, 0f, Space.World);
            float baseI = d.style == 0 ? 7f : d.style == 1 ? 3.5f : 2.2f; d.light.intensity = baseI * Mathf.Clamp01(k) * (0.82f + 0.18f * Mathf.Sin(Time.time * 7f) + 0.08f * Mathf.Sin(Time.time * 17f)); d.light.range = d.radius * 1.5f;
            if (closeK < 1f) return;
            if (d.style == 0) StepShrine(d, dt, ou); else if (d.style == 1) StepVoid(d, dt, ou); else StepShadow(d, dt, ou);
        }

        static void StepShrine(Dom d, float dt, float ou)
        {
            Vector3 c = d.c; var warm = new Color(1f, 0.45f, 0.15f, 0.9f);
            float tr = Mathf.Clamp01((ou - 0.2f) / 1.5f); float off = Mathf.Lerp(-18f, 0f, 1f - Mathf.Pow(1f - tr, 3f)) * (d.radius / 20f);
            d.shrine.transform.position = d.shrineBase + Vector3.up * off;
            if (tr < 1f) { int n = Acc(ref d.a4, 40f, dt); for (int i = 0; i < n; i++) { Vector3 p = d.shrineBase + new Vector3(UnityEngine.Random.Range(-1f, 1f) * 7f, 0.5f, UnityEngine.Random.Range(-1f, 1f) * 5f) * (d.radius / 20f); Fx.Puff(p, new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(1f, 4f), UnityEngine.Random.Range(-2f, 2f)), 3.5f, new Color(0.25f, 0.05f, 0.06f, 0.5f), 1.6f, true); } if (UnityEngine.Random.value < 0.3f) Fx.Shake(0.12f, 0.15f); }
            else if (!d.landed) { d.landed = true; Fx.Shake(0.55f, 0.7f); Sfx.Thud(d.shrineBase, 1f); Fx.Burst(d.shrineBase + Vector3.up * 0.5f, 60, 16f, 0.7f, new Color(0.3f, 0.05f, 0.06f, 0.6f), 1.8f, false, 0.6f); Fx.Ring(d.shrineBase + Vector3.up * 0.2f, Vector3.up, new Color(1f, 0.2f, 0.15f), d.radius * 0.5f, 0.8f, 0.6f); }
            int ns = Acc(ref d.a1, 11f, dt); for (int i = 0; i < ns; i++) { Vector3 p0 = InDome(d, 0.12f, 0.92f, true), dir = Dir(false); Fx.Bolt(p0, p0 + dir * UnityEngine.Random.Range(8f, 20f), new Color(1f, 0.93f, 0.88f, 1f), UnityEngine.Random.Range(0.08f, 0.2f), 0.22f, 0.12f, 4); }
            int ne = Acc(ref d.a2, 24f, dt); for (int i = 0; i < ne; i++) Fx.Puff(OnFloor(d, 0f, 0.92f), new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(2f, 6f), UnityEngine.Random.Range(-1f, 1f)), UnityEngine.Random.Range(0.2f, 0.45f), warm, 3.4f, false);
            int nm = Acc(ref d.a3, 4f, dt); for (int i = 0; i < nm; i++) Fx.Puff(OnFloor(d, 0f, 0.9f), new Vector3(UnityEngine.Random.Range(-1f, 1f), 0.3f, UnityEngine.Random.Range(-1f, 1f)), UnityEngine.Random.Range(8f, 13f), new Color(0.4f, 0.02f, 0.04f, 0.26f), 4.5f, true);
        }

        static void StepVoid(Dom d, float dt, float ou)
        {
            Vector3 c = d.c; var pal = new[] { new Color(1f, 1f, 1f, 0.9f), new Color(0.6f, 0.75f, 1f, 0.9f), new Color(0.8f, 0.6f, 1f, 0.9f), new Color(0.6f, 1f, 1f, 0.9f) };
            int n = Acc(ref d.a1, 60f, dt); for (int i = 0; i < n; i++) Fx.Puff(InDome(d, 0.08f, 0.96f, false), UnityEngine.Random.insideUnitSphere * 0.5f, UnityEngine.Random.Range(0.1f, 0.38f), pal[UnityEngine.Random.Range(0, 4)], 4f, false);
            int ns = Acc(ref d.a2, 8f, dt); for (int i = 0; i < ns; i++) { Vector3 a = InDome(d, 0.8f, 0.92f, false), b = InDome(d, 0.8f, 0.92f, false); Color col = UnityEngine.Random.value < 0.5f ? new Color(0.8f, 0.9f, 1f, 1f) : (UnityEngine.Random.value < 0.5f ? new Color(0.65f, 0.5f, 1f, 1f) : new Color(0.4f, 0.95f, 1f, 1f)); Streak(a, b, col, UnityEngine.Random.Range(0.12f, 0.35f), UnityEngine.Random.Range(0.55f, 1.0f)); }
            if (Acc(ref d.a3, 1.1f, dt) > 0) { var g = new Vector3(c.x, d.gy + 0.2f, c.z); Fx.Ring(g, Vector3.up, new Color(0.7f, 0.85f, 1f, 0.6f), d.radius * 0.9f, 1.6f, 0.5f); }
            if (Time.time >= d.nextFlash) { d.nextFlash = Time.time + UnityEngine.Random.Range(1.5f, 2.6f); Fx.Flash(new Color(0.85f, 0.92f, 1f), 0.22f, 5f); Fx.Shake(0.09f, 0.25f); Sfx.Zap(null, 0.35f); Fx.FovTarget = 6f; Fx.Light(c + Vector3.up * 3f, new Color(0.7f, 0.85f, 1f), 7f, d.radius * 1.6f, 0.4f); }
        }

        static void StepShadow(Dom d, float dt, float ou)
        {
            Vector3 c = d.c; float t = Time.time;
            for (int i = 0; i < d.tend.Length; i++)
            {
                var tr = d.tend[i]; if (tr == null) continue; float gr = Smooth((ou - 0.25f - d.tph[i] * 0.06f) / 0.9f); float h = d.tH[i] * gr * (0.92f + 0.08f * Mathf.Sin(t * 1.3f + d.tph[i]));
                tr.localScale = Vector3.one * Mathf.Max(0.001f, h); tr.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.1f + d.tph[i]) * 6f, d.tph[i] * 57f, Mathf.Sin(t * 0.8f + d.tph[i] * 2f) * 6f);
            }
            int nr = Acc(ref d.a1, 3.2f, dt); for (int i = 0; i < nr; i++) Fx.Ring(OnFloor(d, 0f, 0.85f), Vector3.up, new Color(0.3f, 0.55f, 1f, 0.7f), UnityEngine.Random.Range(3f, 7f), 1.8f, 0.22f);
            int ne = Acc(ref d.a2, 2.8f, dt); for (int i = 0; i < ne; i++) { Vector3 p = c + Dir(true) * d.radius * UnityEngine.Random.Range(0.55f, 0.9f); p.y = Mathf.Max(p.y, d.gy + 2f); Vector3 side = Vector3.Cross(Vector3.up, (p - c)).normalized * 0.45f; Color ec = new Color(0.45f, 0.75f, 1f, 1f); Fx.Puff(p + side, Vector3.zero, 0.8f, ec, 1.9f, false); Fx.Puff(p - side, Vector3.zero, 0.8f, ec, 1.9f, false); }
            int nf = Acc(ref d.a3, 14f, dt); for (int i = 0; i < nf; i++) Fx.Puff(OnFloor(d, 0f, 0.9f) + Vector3.up * UnityEngine.Random.Range(0f, 4f), new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(0.4f, 1.5f), UnityEngine.Random.Range(-0.6f, 0.6f)), UnityEngine.Random.Range(0.15f, 0.3f), new Color(0.5f, 0.8f, 1f, 0.9f), 3.5f, false);
            int nm = Acc(ref d.a4, 3f, dt); for (int i = 0; i < nm; i++) Fx.Puff(OnFloor(d, 0f, 0.9f), new Vector3(UnityEngine.Random.Range(-1f, 1f), 0.3f, UnityEngine.Random.Range(-1f, 1f)), UnityEngine.Random.Range(7f, 12f), new Color(0.02f, 0.05f, 0.15f, 0.35f), 4.5f, true);
        }

        // ---------------- экранная часть: полосы, виньетка, титр ----------------
        public static void DrawOverlay()
        {
            if (active.Count == 0 || Event.current.type != EventType.Repaint) return;
            Dom d = null; foreach (var x in active) if (Time.unscaledTime - x.ut0 < WinEnd) d = x; if (d == null) return;
            float u = Time.unscaledTime - d.ut0, W = Screen.width, H = Screen.height; float bars = Smooth(u / 0.35f) * (1f - Smooth((u - (WinEnd - 0.5f)) / 0.5f)); if (bars <= 0.001f) return;
            var old = GUI.color; Texture2D vig = Tx("vig"); Color tint = d.style == 1 ? new Color(0f, 0f, 0.05f) : Color.Lerp(Color.black, d.col, 0.25f);
            GUI.color = new Color(tint.r, tint.g, tint.b, 0.85f * bars); GUI.DrawTexture(new Rect(0, 0, W, H), vig);
            GUI.color = new Color(0, 0, 0, 1f); float bh = H * 0.105f * bars; GUI.DrawTexture(new Rect(0, 0, W, bh), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0, H - bh, W, bh), Texture2D.whiteTexture);
            GUI.color = new Color(d.col.r, d.col.g, d.col.b, 0.9f * bars); GUI.DrawTexture(new Rect(0, bh - 2f, W, 2f), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0, H - bh, W, 2f), Texture2D.whiteTexture); GUI.color = old;
            string title = Titles[d.style], sub = Subs[d.style], cap = Spaced("РАСШИРЕНИЕ ДОМЕНА");
            float aIn = Smooth((u - 0.3f) / 0.4f), aOut = 1f - Smooth((u - (Cast + OpenT + 0.55f)) / 0.55f); float al = aIn * aOut; if (al <= 0.01f) return;
            float pop = 1f + 0.35f * (1f - Smooth((u - 0.3f) / 0.55f)); int fs = (int)Mathf.Min(H * 0.095f, W * 0.8f / Mathf.Max(8, title.Length) / 0.62f); fs = (int)(fs * pop);
            var rc = new Rect(0, H * 0.43f - fs * 0.6f, W, fs * 1.3f); Color glow = new Color(d.col.r, d.col.g, d.col.b, 0.55f * al);
            Shadowed(new Rect(0, rc.y - H * 0.065f, W, H * 0.05f), cap, (int)(H * 0.027f), new Color(1f, 1f, 1f, 0.9f * al), glow);
            Shadowed(rc, title, fs, new Color(1f, 1f, 1f, al), glow);
            Shadowed(new Rect(0, rc.yMax + H * 0.004f, W, H * 0.05f), sub, (int)(H * 0.032f), new Color(d.col.r * 0.4f + 0.6f, d.col.g * 0.4f + 0.6f, d.col.b * 0.4f + 0.6f, 0.95f * al), glow);
        }
        static string Spaced(string s) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < s.Length; i++) { sb.Append(s[i]); if (i < s.Length - 1) sb.Append(' '); } return sb.ToString(); }
        static void Shadowed(Rect r, string s, int size, Color col, Color glow)
        {
            float o = Mathf.Max(2f, size * 0.045f); var blk = new Color(0f, 0f, 0f, col.a * 0.9f);
            foreach (var v in new[] { new Vector2(o * 1.6f, o * 1.6f), new Vector2(-o * 1.6f, o * 1.6f), new Vector2(o * 1.6f, -o * 1.6f), new Vector2(-o * 1.6f, -o * 1.6f) }) Ui.Text(new Rect(r.x + v.x, r.y + v.y, r.width, r.height), s, size, glow, TextAnchor.MiddleCenter, true);
            foreach (var v in new[] { new Vector2(o, 0), new Vector2(-o, 0), new Vector2(0, o), new Vector2(0, -o) }) Ui.Text(new Rect(r.x + v.x, r.y + v.y, r.width, r.height), s, size, blk, TextAnchor.MiddleCenter, true);
            Ui.Text(r, s, size, col, TextAnchor.MiddleCenter, true);
        }

        public static void Warm() { foreach (var k in AllKeys) Tx(k); Sphere(); Disc(); Ribs(); Skulls(); TendMeshes(); }
        public static void Clear() { foreach (var d in active) if (d.root != null) { UnityEngine.Object.Destroy(d.root); if (d.dm != null) UnityEngine.Object.Destroy(d.dm); if (d.fm != null) UnityEngine.Object.Destroy(d.fm); } active.Clear(); Fx.ResetTime(); if (fogSaved) { RenderSettings.fog = fogWas; fogSaved = false; } }
    }
}
