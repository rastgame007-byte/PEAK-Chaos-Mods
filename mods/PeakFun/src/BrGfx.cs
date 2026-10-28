// Графика Backrooms/чемодана: построитель мешей, материалы (освещённые URP Lit → запасные без света), скруглённые параллелепипеды, процедурные текстуры.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    internal sealed class MeshB
    {
        public List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<int> t = new List<int>();
        public int Count { get { return v.Count; } }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); n.Add(nrm); n.Add(nrm); n.Add(nrm); n.Add(nrm); uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), nrm) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }
        /// <summary>Стена/колонна: 4 боковые грани, UV в мировых метрах (обои непрерывны по всему уровню).</summary>
        public void WallBox(Vector3 c, Vector3 s, float tileW, float tileH)
        {
            float x0 = c.x - s.x * 0.5f, x1 = c.x + s.x * 0.5f, y0 = c.y - s.y * 0.5f, y1 = c.y + s.y * 0.5f, z0 = c.z - s.z * 0.5f, z1 = c.z + s.z * 0.5f;
            Func<float, float, Vector2> U = (a, y) => new Vector2(a / tileW, y / tileH);
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right, U(z0, y0), U(z0, y1), U(z1, y1), U(z1, y0));
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.left, U(z0, y0), U(z0, y1), U(z1, y1), U(z1, y0));
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.forward, U(x0, y0), U(x0, y1), U(x1, y1), U(x1, y0));
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), Vector3.back, U(x0, y0), U(x0, y1), U(x1, y1), U(x1, y0));
        }
        /// <summary>Простой бокс из 6 граней, UV 0..1 на грань.</summary>
        public void Box(Vector3 c, Vector3 s)
        {
            float x0 = c.x - s.x * 0.5f, x1 = c.x + s.x * 0.5f, y0 = c.y - s.y * 0.5f, y1 = c.y + s.y * 0.5f, z0 = c.z - s.z * 0.5f, z1 = c.z + s.z * 0.5f;
            Vector2 a = new Vector2(0, 0), b = new Vector2(0, 1), cc = new Vector2(1, 1), d = new Vector2(1, 0);
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right, a, b, cc, d);
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.left, a, b, cc, d);
            Quad(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.up, a, b, cc, d);
            Quad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), Vector3.down, a, b, cc, d);
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.forward, a, b, cc, d);
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), Vector3.back, a, b, cc, d);
        }
        public void FloorQuad(float x0, float z0, float x1, float z1, float y, bool up)
        {
            Vector3 nrm = up ? Vector3.up : Vector3.down;
            Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), nrm, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        }
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
        }
    }

    internal static class BrGfx
    {
        // ======================= материалы =======================
        static Shader op, lit; static bool looked, litLooked; public static string OpName = "не искали", LitName = "не искали"; public static bool LitOk;
        static readonly string[] CandsOp = { "Universal Render Pipeline/Unlit", "Unlit/Texture", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Lit", "Standard" };
        static readonly string[] CandsLit = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Standard" };

        public static string Survey()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var n in new[] { "Sprites/Default", "Universal Render Pipeline/Unlit", "Unlit/Texture", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Lit", "Standard", "Hidden/Internal-Colored" })
            { var sh = Shader.Find(n); sb.Append(n).Append(sh != null ? (sh.isSupported ? "=да " : "=не поддерж. ") : "=нет "); }
            return sb.ToString();
        }

        static void FindLit()
        {
            if (litLooked) return; litLooked = true;
            foreach (var n in CandsLit) { var s = Shader.Find(n); if (s != null && s.isSupported) { lit = s; LitName = n; LitOk = true; break; } }
            if (lit == null) LitName = "нет (запасной режим без света)";
        }

        public static Material Opaque(Texture tex, Color c)
        {
            if (!looked)
            {
                looked = true;
                foreach (var n in CandsOp) { var s = Shader.Find(n); if (s != null && s.isSupported) { op = s; OpName = n; break; } }
                if (op == null) OpName = "нет (запасной Sprites/Default — стены могут мерцать)";
            }
            if (op == null) return Vfx.Mat(tex, c);
            var m = new Material(op); m.color = c; if (tex != null) m.mainTexture = tex; return m;
        }

        /// <summary>Освещаемый материал (URP Lit); если шейдера нет — непрозрачный без света. emis — самосвечение (лампы, глаза).</summary>
        public static Material Surf(Texture tex, Color c, float smooth = 0.15f, float metal = 0f, Color? emis = null)
        {
            FindLit();
            if (lit == null) { var o = Opaque(tex, emis.HasValue ? Color.Lerp(c, emis.Value, 0.85f) : c); return o; }
            var m = new Material(lit); m.color = c; if (tex != null) m.mainTexture = tex;
            try
            {
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth); if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
                if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
                if (emis.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emis.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            }
            catch { }
            return m;
        }

        public static Texture2D MakeTex(int w, int h, bool repeat, Func<float, float, Color> f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true); t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; t.filterMode = FilterMode.Trilinear; t.anisoLevel = 6;
            var px = new Color[w * h]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            t.SetPixels(px); t.Apply(true, false); return t;
        }

        // ======================= скруглённый бокс =======================
        static readonly Dictionary<string, Mesh> rbCache = new Dictionary<string, Mesh>();
        /// <summary>Скруглённый параллелепипед. cx×cy — раскладка атласа граней (+x,-x,+y,-y,+z,-z), 1×1 — одна текстура на грань.</summary>
        public static Mesh RoundBox(Vector3 size, float r, int cx, int cy)
        {
            string key = size.ToString("0.000") + r.ToString("0.000") + cx + cy; Mesh cm; if (rbCache.TryGetValue(key, out cm) && cm != null) return cm;
            Vector3 h = size * 0.5f; r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
            Func<float, float[]> cuts = hh => new[] { -hh, -hh + 0.3f * r, -hh + 0.7f * r, -hh + r, hh - r, hh - 0.7f * r, hh - 0.3f * r, hh };
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var uvs = new List<Vector2>(); var tr = new List<int>(); float ins = 0.008f;
            for (int f = 0; f < 6; f++)
            {
                int a = f / 2; float s = (f % 2 == 0) ? 1f : -1f; int ua = a == 0 ? 2 : 0, va = a == 1 ? 2 : 1; float[] cu = cuts(h[ua]), cv = cuts(h[va]); int b0 = vs.Count;
                for (int i = 0; i < 8; i++) for (int j = 0; j < 8; j++)
                {
                    Vector3 p = Vector3.zero; p[a] = s * h[a]; p[ua] = cu[i]; p[va] = cv[j];
                    Vector3 inner = new Vector3(Mathf.Clamp(p.x, -(h.x - r), h.x - r), Mathf.Clamp(p.y, -(h.y - r), h.y - r), Mathf.Clamp(p.z, -(h.z - r), h.z - r));
                    Vector3 nr = (p - inner).normalized; vs.Add(inner + nr * r); ns.Add(nr);
                    float fu = (cu[i] + h[ua]) / (2f * h[ua]), fv = (cv[j] + h[va]) / (2f * h[va]);
                    if (cx == 1 && cy == 1) uvs.Add(new Vector2(fu, fv));
                    else { int cxi = f % cx, cyi = f / cx; uvs.Add(new Vector2((cxi + ins + fu * (1f - 2f * ins)) / cx, (cyi + ins + fv * (1f - 2f * ins)) / cy)); }
                }
                Vector3 N = Vector3.zero; N[a] = s;
                Func<int, int, int> id = (i, j) => b0 + i * 8 + j;
                bool flip = Vector3.Dot(Vector3.Cross(vs[id(4, 3)] - vs[id(3, 3)], vs[id(4, 4)] - vs[id(3, 3)]), N) < 0f;
                for (int i = 0; i < 7; i++) for (int j = 0; j < 7; j++)
                {
                    int q00 = id(i, j), q10 = id(i + 1, j), q11 = id(i + 1, j + 1), q01 = id(i, j + 1);
                    if (!flip) { tr.Add(q00); tr.Add(q10); tr.Add(q11); tr.Add(q00); tr.Add(q11); tr.Add(q01); } else { tr.Add(q00); tr.Add(q11); tr.Add(q10); tr.Add(q00); tr.Add(q01); tr.Add(q11); }
                }
            }
            var m = new Mesh { name = "pf_roundbox" }; m.SetVertices(vs); m.SetNormals(ns); m.SetUVs(0, uvs); m.SetTriangles(tr, 0); m.RecalculateBounds(); rbCache[key] = m; return m;
        }

        public static GameObject RBox(Transform par, string name, Vector3 lp, Vector3 size, float r, Material mat, Vector3 euler = default(Vector3), int cx = 1, int cy = 1)
        {
            var g = new GameObject(name); g.transform.SetParent(par, false); g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(euler);
            g.AddComponent<MeshFilter>().sharedMesh = RoundBox(size, r, cx, cy); var mr = g.AddComponent<MeshRenderer>(); if (mat != null) mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; return g;
        }

        // ======================= чемодан (как в PEAK: тёмно-красная кожа, жёлтые ремни с заклёпками, серые уголки, золотые замки, красная ручка) =======================
        static Texture2D caseAtlas, strapTex;
        static readonly Vector2[] faceDim = { new Vector2(0.8f, 0.56f), new Vector2(0.8f, 0.56f), new Vector2(1.3f, 0.8f), new Vector2(1.3f, 0.8f), new Vector2(1.3f, 0.56f), new Vector2(1.3f, 0.56f) };

        public static Texture2D CaseAtlas()
        {
            if (caseAtlas != null) return caseAtlas;
            int cw = 256, ch = 128, W = cw * 3, H = ch * 2; var t = new Texture2D(W, H, TextureFormat.RGBA32, true); t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Trilinear; t.anisoLevel = 6; var px = new Color[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int face = (y / ch) * 3 + (x / cw); float u = (x % cw + 0.5f) / cw, v = (y % ch + 0.5f) / ch; px[y * W + x] = CaseTexel(face, u, v);
            }
            t.SetPixels(px); t.Apply(true, false); caseAtlas = t; return t;
        }

        static Color CaseTexel(int face, float u, float v)
        {
            Vector2 d = faceDim[face]; float um = u * d.x, vm = v * d.y;
            float g1 = Mathf.PerlinNoise(um * 70f + face * 5f, vm * 70f), g2 = Mathf.PerlinNoise(um * 220f, vm * 220f + 4f);
            float grain = 0.82f + g1 * 0.22f + g2 * 0.12f;
            Color col = new Color(0.56f, 0.2f, 0.1f) * grain; col.a = 1f;
            bool side = face == 0 || face == 1 || face >= 4;
            if (side)
            {
                float dv = Mathf.Abs(vm - d.y * 0.5f);
                if (dv < 0.006f) col *= 0.28f; else if (dv < 0.014f) col *= vm > d.y * 0.5f ? 1.25f : 0.78f;
                if (vm < 0.05f) col *= Mathf.Lerp(0.7f, 1f, vm / 0.05f);
            }
            float e = Mathf.Min(Mathf.Min(um, d.x - um), Mathf.Min(vm, d.y - vm)); if (e < 0.035f) col *= Mathf.Lerp(0.72f, 1f, e / 0.035f);
            // царапины и потёртости
            float sc = Mathf.PerlinNoise(um * 9f + 7f, vm * 2.5f); if (sc > 0.7f && Mathf.PerlinNoise(um * 120f, vm * 16f) > 0.58f) col = Color.Lerp(col, new Color(0.62f, 0.4f, 0.32f), 0.55f);
            if (Mathf.PerlinNoise(um * 3f + 2f, vm * 3f + 9f) > 0.76f) col *= 0.78f;
            // наклейки на лицевых гранях
            if (face >= 4)
            {
                if (um > 0.18f && um < 0.30f && vm > 0.09f && vm < 0.21f) col = Color.Lerp(col, new Color(0.9f, 0.82f, 0.25f), 0.85f);
                if (um > 0.95f && um < 1.07f && vm > 0.28f && vm < 0.4f) col = Color.Lerp(col, new Color(0.2f, 0.45f, 0.75f), 0.85f);
                if (Vector2.Distance(new Vector2(um, vm), new Vector2(0.62f, 0.14f)) < 0.05f) col = Color.Lerp(col, new Color(0.85f, 0.9f, 0.9f), 0.8f);
            }
            col.a = 1f; return col;
        }

        public static Texture2D StrapTex()
        {
            if (strapTex != null) return strapTex;
            strapTex = MakeTex(32, 128, false, (u, v) =>
            {
                float g = 0.88f + Mathf.PerlinNoise(u * 18f, v * 40f) * 0.2f; Color c = new Color(0.86f, 0.58f, 0.24f) * g; c.a = 1f;
                if (u < 0.1f || u > 0.9f) c *= 0.82f; if (Mathf.Abs(u - 0.16f) < 0.012f || Mathf.Abs(u - 0.84f) < 0.012f) c *= 0.55f;
                for (int k = 0; k < 8; k++) { float cv = (k + 0.5f) / 8f; float dd = Vector2.Distance(new Vector2(u * 0.11f, v * 0.9f), new Vector2(0.055f, cv * 0.9f)); if (dd < 0.011f) c = Color.Lerp(new Color(0.95f, 0.8f, 0.45f), new Color(0.5f, 0.32f, 0.12f), dd / 0.011f); }
                return c;
            });
            return strapTex;
        }
    }
}
