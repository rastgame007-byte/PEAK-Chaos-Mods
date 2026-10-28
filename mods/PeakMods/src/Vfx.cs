// Общие визуальные заготовки 1.9: материалы, процедурные текстуры, кольца-ударные волны, кратеры, пыль, звуки.
// Ничего внешнего: всё рисуется кодом в рантайме.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakMods
{
    internal static class Vfx
    {
        // ---------- материалы ----------
        static Shader shader; static bool shaderLooked;
        public static Shader Sh()
        {
            if (shaderLooked) return shader;
            shaderLooked = true;
            foreach (var n in new[] { "Sprites/Default", "Universal Render Pipeline/Unlit", "Unlit/Texture", "Unlit/Color", "Hidden/Internal-Colored" })
            { shader = Shader.Find(n); if (shader != null) break; }
            return shader;
        }

        public static Material Mat(Texture tex, Color c)
        {
            var s = Sh(); if (s == null) return null;
            var m = new Material(s); m.color = c;
            if (tex != null) m.mainTexture = tex;
            return m;
        }

        // ---------- процедурные текстуры ----------
        public static Texture2D Tex(int w, int h, Func<float, float, Color> f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            t.SetPixels(px); t.Apply(false, false);
            return t;
        }

        static float Smooth(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3f - 2f * x); }

        static Texture2D blob;
        public static Texture2D Blob()
        {
            if (blob == null) blob = Tex(32, 32, (u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f; return new Color(1, 1, 1, 1f - Smooth(0.55f, 1f, d)); });
            return blob;
        }

        static Texture2D[] splats;
        public static Texture2D Splat(int i)
        {
            if (splats == null)
            {
                splats = new Texture2D[4];
                for (int k = 0; k < 4; k++)
                {
                    var rnd = new System.Random(100 + k * 17);
                    var cs = new List<Vector3>();
                    cs.Add(new Vector3(0.5f, 0.5f, 0.17f + (float)rnd.NextDouble() * 0.06f));
                    int n = 12 + rnd.Next(8);
                    for (int j = 0; j < n; j++)
                    {
                        float a = (float)(rnd.NextDouble() * Math.PI * 2), d = 0.14f + (float)rnd.NextDouble() * 0.3f, r = 0.012f + (float)rnd.NextDouble() * 0.05f * (1f - d);
                        cs.Add(new Vector3(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, r));
                        if (rnd.NextDouble() < 0.5)      // «хвост» от капли к центру
                            for (int s = 1; s <= 3; s++) { float dd = d * (1f - s * 0.18f); cs.Add(new Vector3(0.5f + Mathf.Cos(a) * dd, 0.5f + Mathf.Sin(a) * dd, r * (0.8f - s * 0.18f) + 0.006f)); }
                    }
                    splats[k] = Tex(64, 64, (u, v) =>
                    {
                        float best = 0f;
                        foreach (var c in cs) { float d = Vector2.Distance(new Vector2(u, v), new Vector2(c.x, c.y)); best = Mathf.Max(best, 1f - Smooth(c.z * 0.75f, c.z, d)); }
                        return new Color(1, 1, 1, best);
                    });
                }
            }
            return splats[((i % 4) + 4) % 4];
        }

        static Texture2D crack;
        public static Texture2D Crack()
        {
            if (crack != null) return crack;
            var rnd = new System.Random(7);
            var lines = new List<Vector4>();
            for (int i = 0; i < 12; i++)
            {
                float a = (float)(rnd.NextDouble() * Math.PI * 2), l = 0.28f + (float)rnd.NextDouble() * 0.2f;
                lines.Add(new Vector4(0.5f + Mathf.Cos(a) * 0.05f, 0.5f + Mathf.Sin(a) * 0.05f, 0.5f + Mathf.Cos(a) * l, 0.5f + Mathf.Sin(a) * l));
                float a2 = a + ((float)rnd.NextDouble() - .5f) * 0.9f, m = l * 0.6f;
                lines.Add(new Vector4(0.5f + Mathf.Cos(a) * m, 0.5f + Mathf.Sin(a) * m, 0.5f + Mathf.Cos(a2) * (l + 0.06f), 0.5f + Mathf.Sin(a2) * (l + 0.06f)));
            }
            crack = Tex(128, 128, (u, v) =>
            {
                var p = new Vector2(u, v); float best = 0f;
                float dc = Vector2.Distance(p, new Vector2(.5f, .5f));
                best = Mathf.Max(best, (1f - Smooth(0.06f, 0.1f, dc)) * 0.9f);
                foreach (var l in lines)
                {
                    Vector2 a = new Vector2(l.x, l.y), b = new Vector2(l.z, l.w), ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    float d = Vector2.Distance(p, a + ab * t);
                    best = Mathf.Max(best, 1f - Smooth(0.004f, 0.012f + 0.01f * (1f - t), d));
                }
                return new Color(1, 1, 1, best);
            });
            return crack;
        }

        public static bool InPoly(Vector2[] poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (((poly[i].y > y) != (poly[j].y > y)) && (x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) inside = !inside;
            return inside;
        }

        static Texture2D eagle;
        /// <summary>Золотой орёл на груди Патриота.</summary>
        public static Texture2D Eagle()
        {
            if (eagle != null) return eagle;
            var wingL = new[]
            {
                new Vector2(.46f,.60f), new Vector2(.34f,.78f), new Vector2(.18f,.88f), new Vector2(.04f,.86f), new Vector2(.12f,.77f), new Vector2(.01f,.72f), new Vector2(.12f,.66f),
                new Vector2(.03f,.58f), new Vector2(.16f,.55f), new Vector2(.08f,.46f), new Vector2(.22f,.47f), new Vector2(.18f,.38f), new Vector2(.32f,.42f), new Vector2(.46f,.34f)
            };
            var wingR = new Vector2[wingL.Length];
            for (int i = 0; i < wingL.Length; i++) wingR[i] = new Vector2(1f - wingL[i].x, wingL[i].y);
            var tail = new[] { new Vector2(.43f, .30f), new Vector2(.5f, .06f), new Vector2(.57f, .30f) };
            eagle = Tex(128, 128, (u, v) =>
            {
                bool body = InPoly(wingL, u, v) || InPoly(wingR, u, v) || InPoly(tail, u, v)
                    || (Mathf.Pow((u - .5f) / .075f, 2) + Mathf.Pow((v - .47f) / .26f, 2) < 1f)
                    || (Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .73f)) < .065f);
                if (!body) return new Color(0, 0, 0, 0);
                float sh = 0.8f + 0.25f * v;
                return new Color(1f * sh, 0.78f * sh, 0.12f * sh, 1f);
            });
            return eagle;
        }

        static Texture2D bolt;
        public static Texture2D Bolt()
        {
            if (bolt != null) return bolt;
            var poly = new[] { new Vector2(.60f, .95f), new Vector2(.22f, .50f), new Vector2(.46f, .50f), new Vector2(.34f, .05f), new Vector2(.78f, .56f), new Vector2(.52f, .56f) };
            bolt = Tex(64, 64, (u, v) => InPoly(poly, u, v) ? new Color(1f, 0.9f, 0.1f, 1f) : new Color(0, 0, 0, 0));
            return bolt;
        }

        static Texture2D flag;
        /// <summary>Плащ-флаг: синее поле со звёздами сверху, красно-белые полосы ниже.</summary>
        public static Texture2D Flag()
        {
            if (flag != null) return flag;
            flag = Tex(64, 128, (u, v) =>
            {
                // v=1 — верх плаща (у шеи)
                float top = 0.62f;
                if (v > top)
                {
                    float fx = u * 6f, fy = (v - top) / (1f - top) * 5f;
                    float cx = Mathf.Floor(fx) + 0.5f, cy = Mathf.Floor(fy) + 0.5f;
                    if (((int)Mathf.Floor(fx) + (int)Mathf.Floor(fy)) % 2 == 0 && Vector2.Distance(new Vector2(fx, fy), new Vector2(cx, cy)) < 0.2f) return new Color(0.96f, 0.96f, 1f, 1f);
                    return new Color(0.07f, 0.12f, 0.45f, 1f);
                }
                bool red = ((int)Mathf.Floor(v / top * 7f)) % 2 == 0;
                return red ? new Color(0.72f, 0.07f, 0.1f, 1f) : new Color(0.96f, 0.96f, 0.96f, 1f);
            });
            return flag;
        }

        // ---------- процедурные меши ----------
        static Mesh sphereMesh;
        /// <summary>Эллипсоид с вершинными цветами (псевдо-освещение). Радиус 0.5.</summary>
        public static Mesh Sphere()
        {
            if (sphereMesh != null) return sphereMesh;
            int lat = 12, lon = 18; var vs = new List<Vector3>(); var cs = new List<Color>(); var tr = new List<int>(); var uvs = new List<Vector2>();
            Vector3 light = new Vector3(0.35f, 0.55f, 0.75f).normalized;
            for (int i = 0; i <= lat; i++)
            {
                float a = Mathf.PI * i / lat;
                for (int j = 0; j <= lon; j++)
                {
                    float b = 2f * Mathf.PI * j / lon;
                    var n = new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b));
                    vs.Add(n * 0.5f); uvs.Add(new Vector2(j / (float)lon, i / (float)lat));
                    float l = 0.62f + 0.38f * Mathf.Clamp01(Vector3.Dot(n, light) * 0.5f + 0.5f);
                    cs.Add(new Color(l, l, l, 1f));
                }
            }
            for (int i = 0; i < lat; i++) for (int j = 0; j < lon; j++)
            {
                int a = i * (lon + 1) + j, b = a + lon + 1;
                tr.Add(a); tr.Add(b); tr.Add(a + 1); tr.Add(a + 1); tr.Add(b); tr.Add(b + 1);
            }
            sphereMesh = new Mesh { name = "pf_sphere" };
            sphereMesh.SetVertices(vs); sphereMesh.SetUVs(0, uvs); sphereMesh.SetColors(cs); sphereMesh.SetTriangles(tr, 0); sphereMesh.RecalculateBounds();
            return sphereMesh;
        }

        static Mesh quadMesh;
        /// <summary>Квадрат в плоскости XY, нормаль -Z, 1×1, центр в нуле, UV 0..1.</summary>
        public static Mesh Quad()
        {
            if (quadMesh != null) return quadMesh;
            quadMesh = new Mesh { name = "pf_quad" };
            quadMesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
            quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quadMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };   // двусторонний
            quadMesh.RecalculateBounds();
            return quadMesh;
        }

        // ---------- короткие эффекты ----------
        sealed class Item { public GameObject go; public float t0, dur; public Action<float, GameObject> upd; }
        static readonly List<Item> items = new List<Item>();

        public static void Add(GameObject go, float dur, Action<float, GameObject> upd)
        {
            items.Add(new Item { go = go, t0 = Time.time, dur = dur, upd = upd });
        }

        public static void Tick()
        {

            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                if (it.go == null) { items.RemoveAt(i); continue; }
                float k = (Time.time - it.t0) / it.dur;
                if (k >= 1f) { UnityEngine.Object.Destroy(it.go); items.RemoveAt(i); continue; }
                try { it.upd(k, it.go); } catch { }
            }
        }

        public static void Clear() { foreach (var it in items) if (it.go != null) UnityEngine.Object.Destroy(it.go); items.Clear(); }

        /// <summary>Расширяющееся кольцо (ударная волна). normal — ось кольца.</summary>
        public static void Ring(Vector3 pos, Vector3 normal, Color col, float maxR, float dur)
        {
            var s = Sh(); if (s == null) return;
            var go = new GameObject("PF_Ring"); go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude < 0.01f ? Vector3.up : normal.normalized);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = Mat(null, Color.white); lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 48; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI * 2f / 48f; lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f)); }
            Add(go, dur, (k, g) =>
            {
                float r = maxR * (1f - Mathf.Pow(1f - k, 3f));
                g.transform.localScale = Vector3.one * Mathf.Max(0.01f, r);
                var c = col; c.a = (1f - k) * col.a; lr.startColor = c; lr.endColor = c;
                lr.startWidth = lr.endWidth = Mathf.Lerp(0.45f, 0.04f, k) * Mathf.Clamp(maxR / 8f, 0.5f, 2f);
            });
        }

        // ---------- искры ----------
        static ParticleSystem sparks;
        public static void Sparks(Vector3 pos, Vector3 dir, int count, Color? tint = null)
        {
            var s = Sh(); if (s == null) return;
            if (sparks == null)
            {
                var go = new GameObject("PF_Sparks"); UnityEngine.Object.DontDestroyOnLoad(go);
                sparks = go.AddComponent<ParticleSystem>();
                var main = sparks.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 500; main.gravityModifier = 1.6f;
                var em = sparks.emission; em.enabled = false; var sh = sparks.shape; sh.enabled = false;
                var sz = sparks.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1f, 1, 0.1f));
                var col = sparks.colorOverLifetime; col.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(new Color(1f, 1f, 0.9f), 0), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 0.5f), new GradientColorKey(new Color(1f, 0.25f, 0.1f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0, 1) });
                col.color = g;
                var r = go.GetComponent<ParticleSystemRenderer>(); r.material = Mat(Blob(), Color.white); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 2.2f;
                sparks.Play();
            }
            dir = dir.sqrMagnitude < 0.01f ? Vector3.up : dir.normalized;
            for (int i = 0; i < count; i++)
            {
                var ep = new ParticleSystem.EmitParams();
                ep.position = pos + UnityEngine.Random.insideUnitSphere * 0.1f;
                ep.velocity = (dir * UnityEngine.Random.Range(4f, 14f) + UnityEngine.Random.insideUnitSphere * 7f);
                ep.startSize = UnityEngine.Random.Range(0.06f, 0.16f); ep.startLifetime = UnityEngine.Random.Range(0.25f, 0.6f);
                ep.startColor = tint ?? new Color(1f, 0.9f, 0.6f, 1f);
                sparks.Emit(ep, 1);
            }
        }

        // ---------- текстура костюма: тонкие швы, блик, лёгкая ткань ----------
        static Texture2D suitTex;
        public static Texture2D SuitTex()
        {
            if (suitTex != null) return suitTex;
            suitTex = Tex(64, 64, (u, v) =>
            {
                float weave = 0.93f + 0.07f * Mathf.Sin(u * 160f) * Mathf.Sin(v * 160f);
                float seam = Mathf.Abs(u - 0.5f) < 0.012f ? 0.72f : 1f;
                float sheen = 0.88f + 0.22f * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u - 0.28f) * 3f), 2f);
                float k = weave * seam * sheen; return new Color(k, k, k, 1f);
            });
            suitTex.wrapMode = TextureWrapMode.Repeat;
            return suitTex;
        }

        static Texture2D swoosh;
        /// <summary>Эмблема Неуязвимого: жёлтый «разрез» по диагонали на круге.</summary>
        public static Texture2D Swoosh()
        {
            if (swoosh != null) return swoosh;
            swoosh = Tex(64, 64, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f));
                if (d > 0.46f) return new Color(0, 0, 0, 0);
                float diag = Mathf.Abs((u - 0.5f) + (v - 0.5f) * 0.9f);
                if (diag < 0.1f && d < 0.4f) return new Color(0.08f, 0.1f, 0.35f, 1f);
                return new Color(1f, 0.85f, 0.1f, 1f);
            });
            return swoosh;
        }

        static Texture2D star;
        public static Texture2D Star()
        {
            if (star != null) return star;
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++) { float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f; float r = i % 2 == 0 ? 0.46f : 0.19f; pts[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r); }
            star = Tex(48, 48, (u, v) => InPoly(pts, u, v) ? new Color(1f, 0.95f, 0.9f, 1f) : new Color(0, 0, 0, 0));
            return star;
        }

        // ---------- пыль ----------
        static ParticleSystem dust;
        public static void Dust(Vector3 pos, float radius, int count = 26)
        {
            var s = Sh(); if (s == null) return;
            if (dust == null)
            {
                var go = new GameObject("PF_Dust"); UnityEngine.Object.DontDestroyOnLoad(go);
                dust = go.AddComponent<ParticleSystem>();
                var main = dust.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 600; main.gravityModifier = -0.02f;
                var em = dust.emission; em.enabled = false; var sh = dust.shape; sh.enabled = false;
                var sz = dust.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.4f, 1, 1.6f));
                var col = dust.colorOverLifetime; col.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.55f, 0), new GradientAlphaKey(0.35f, 0.4f), new GradientAlphaKey(0, 1) });
                col.color = g;
                var r = go.GetComponent<ParticleSystemRenderer>(); r.material = Mat(Blob(), Color.white); r.sortingFudge = 2f;
                dust.Play();
            }
            for (int i = 0; i < count; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                var ep = new ParticleSystem.EmitParams();
                ep.position = pos + new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a)) * radius * 0.25f;
                ep.velocity = new Vector3(Mathf.Cos(a), 0.12f + UnityEngine.Random.value * 0.25f, Mathf.Sin(a)) * radius * (0.7f + UnityEngine.Random.value * 0.9f);
                ep.startSize = 0.5f + UnityEngine.Random.value * 0.9f; ep.startLifetime = 1.0f + UnityEngine.Random.value * 0.8f;
                float gcol = 0.55f + UnityEngine.Random.value * 0.15f; ep.startColor = new Color(gcol, gcol * 0.93f, gcol * 0.82f, 1f);
                dust.Emit(ep, 1);
            }
        }
    }
}
