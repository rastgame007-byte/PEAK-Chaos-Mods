// Графика PeakJJK: материалы (URP Lit/Unlit/спрайты), сборщик мешей из примитивов и «модель из частей» для процедурных проклятий и аур.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakJJK
{
    /// <summary>Сборка меша: эллипсоиды, боксы, трубы/конусы, торы, диски и полумесяцы. Нормали посчитаны, UV простые.</summary>
    internal sealed class MB
    {
        public List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<int> t = new List<int>(); public List<Color> c = new List<Color>();
        public int Count { get { return v.Count; } }
        int Add(Vector3 p, Vector3 nr, Vector2 u, Color col) { v.Add(p); n.Add(nr); uv.Add(u); c.Add(col); return v.Count - 1; }

        public void Tri(int a, int b, int d) { t.Add(a); t.Add(b); t.Add(d); }
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 nr, Color col)
        {
            int i = Add(a, nr, new Vector2(0, 0), col); Add(b, nr, new Vector2(0, 1), col); Add(cc, nr, new Vector2(1, 1), col); Add(d, nr, new Vector2(1, 0), col);
            if (Vector3.Dot(Vector3.Cross(b - a, cc - a), nr) >= 0f) { Tri(i, i + 1, i + 2); Tri(i, i + 2, i + 3); } else { Tri(i, i + 2, i + 1); Tri(i, i + 3, i + 2); }
        }
        public void QuadBoth(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 nr, Color col) { Quad(a, b, cc, d, nr, col); Quad(a, b, cc, d, -nr, col); }

        public void Ell(Vector3 center, Vector3 radii, Quaternion rot, Color col, int lat = 9, int lon = 14)
        {
            int b0 = v.Count;
            for (int i = 0; i <= lat; i++)
            {
                float a = Mathf.PI * i / lat;
                for (int j = 0; j <= lon; j++)
                {
                    float bb = 2f * Mathf.PI * j / lon; var nn = new Vector3(Mathf.Sin(a) * Mathf.Cos(bb), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(bb));
                    var p = new Vector3(nn.x * radii.x, nn.y * radii.y, nn.z * radii.z); var nr = new Vector3(nn.x / Mathf.Max(0.001f, radii.x), nn.y / Mathf.Max(0.001f, radii.y), nn.z / Mathf.Max(0.001f, radii.z)).normalized;
                    Add(center + rot * p, rot * nr, new Vector2(j / (float)lon, i / (float)lat), col);
                }
            }
            for (int i = 0; i < lat; i++) for (int j = 0; j < lon; j++) { int a = b0 + i * (lon + 1) + j, b = a + lon + 1; Tri(a, a + 1, b); Tri(a + 1, b + 1, b); }
        }
        public void Ell(Vector3 center, Vector3 radii, Color col) { Ell(center, radii, Quaternion.identity, col); }
        public void Ball(Vector3 center, float r, Color col, int lat = 8, int lon = 12) { Ell(center, new Vector3(r, r, r), Quaternion.identity, col, lat, lon); }

        public void Box(Vector3 center, Vector3 size, Quaternion rot, Color col)
        {
            Vector3 h = size * 0.5f; Vector3[] ax = { rot * Vector3.right, rot * Vector3.up, rot * Vector3.forward };
            for (int f = 0; f < 6; f++)
            {
                int a = f / 2; float s = f % 2 == 0 ? 1f : -1f; int u = (a + 1) % 3, w = (a + 2) % 3; Vector3 nr = ax[a] * s, cc = center + nr * h[a];
                Vector3 du = ax[u] * h[u], dw = ax[w] * h[w];
                Quad(cc - du - dw, cc - du + dw, cc + du + dw, cc + du - dw, nr, col);
            }
        }
        public void Box(Vector3 center, Vector3 size, Color col) { Box(center, size, Quaternion.identity, col); }

        /// <summary>Усечённый конус/цилиндр от a к b с крышками (r0 у a, r1 у b).</summary>
        public void Tube(Vector3 a, Vector3 b, float r0, float r1, Color col, int seg = 8, bool caps = true)
        {
            Vector3 ax = b - a; float len = ax.magnitude; if (len < 0.0001f) return; ax /= len; Vector3 u = Vector3.Cross(ax, Mathf.Abs(ax.y) > 0.9f ? Vector3.right : Vector3.up).normalized, w = Vector3.Cross(ax, u);
            int b0 = v.Count; float slope = (r0 - r1) / len;
            for (int i = 0; i <= seg; i++)
            {
                float an = 2f * Mathf.PI * i / seg; Vector3 d = u * Mathf.Cos(an) + w * Mathf.Sin(an); Vector3 nr = (d + ax * slope).normalized;
                Add(a + d * r0, nr, new Vector2(i / (float)seg, 0), col); Add(b + d * r1, nr, new Vector2(i / (float)seg, 1), col);
            }
            for (int i = 0; i < seg; i++) { int p = b0 + i * 2; Tri(p, p + 2, p + 1); Tri(p + 1, p + 2, p + 3); }
            if (caps)
            {
                if (r0 > 0.001f) Disc(a, -ax, r0, col, seg); if (r1 > 0.001f) Disc(b, ax, r1, col, seg);
            }
        }
        public void Disc(Vector3 center, Vector3 normal, float r, Color col, int seg = 16)
        {
            normal.Normalize(); Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.up).normalized, w = Vector3.Cross(normal, u);
            int c0 = Add(center, normal, new Vector2(0.5f, 0.5f), col);
            for (int i = 0; i <= seg; i++) { float an = 2f * Mathf.PI * i / seg; Add(center + (u * Mathf.Cos(an) + w * Mathf.Sin(an)) * r, normal, new Vector2(0.5f + Mathf.Cos(an) * 0.5f, 0.5f + Mathf.Sin(an) * 0.5f), col); }
            for (int i = 0; i < seg; i++) { int a = c0 + 1 + i; if (Vector3.Dot(Vector3.Cross(v[a] - v[c0], v[a + 1] - v[c0]), normal) >= 0f) Tri(c0, a, a + 1); else Tri(c0, a + 1, a); }
        }
        /// <summary>Тор (кольцо) вокруг оси normal: большой радиус R, малый r.</summary>
        public void Torus(Vector3 center, Vector3 normal, float R, float r, Color col, int seg = 28, int sides = 8)
        {
            normal.Normalize(); Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.up).normalized, w = Vector3.Cross(normal, u); int b0 = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg; Vector3 d = u * Mathf.Cos(a) + w * Mathf.Sin(a), cc = center + d * R;
                for (int j = 0; j <= sides; j++) { float bb = 2f * Mathf.PI * j / sides; Vector3 nr = d * Mathf.Cos(bb) + normal * Mathf.Sin(bb); Add(cc + nr * r, nr, new Vector2(i / (float)seg, j / (float)sides), col); }
            }
            for (int i = 0; i < seg; i++) for (int j = 0; j < sides; j++) { int a = b0 + i * (sides + 1) + j, b = a + sides + 1; Tri(a, b, a + 1); Tri(a + 1, b, b + 1); }
        }
        /// <summary>Плоский полумесяц (волна рассечения): лежит в плоскости (fwd, up→side), дуга arc градусов.</summary>
        public void Crescent(Vector3 center, Vector3 fwd, Vector3 up, float outer, float inner, float arcDeg, float thick, Color col, int seg = 14)
        {
            fwd.Normalize(); Vector3 side = Vector3.Cross(up, fwd).normalized; Vector3 nr = Vector3.Cross(fwd, side).normalized; if (nr.sqrMagnitude < 0.01f) nr = up;
            float half = arcDeg * 0.5f * Mathf.Deg2Rad; Vector3 prevO = Vector3.zero, prevI = Vector3.zero;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)seg); Vector3 d = fwd * Mathf.Cos(a) + side * Mathf.Sin(a);
                float tp = 1f - Mathf.Abs(2f * i / seg - 1f); float ri = Mathf.Lerp(outer, inner, 0f); Vector3 po = center + d * outer, pi = center + d * Mathf.Lerp(outer, inner, Mathf.Clamp01(0.55f + 0.45f * (1f - tp)));
                if (i > 0) { QuadBoth(prevO, po, pi, prevI, nr, col); }
                prevO = po; prevI = pi;
            }
        }

        public void Append(MB o, Vector3 pos, Quaternion rot, float scale = 1f)
        {
            int b0 = v.Count; for (int i = 0; i < o.v.Count; i++) { v.Add(pos + rot * (o.v[i] * scale)); n.Add(rot * o.n[i]); uv.Add(o.uv[i]); c.Add(o.c[i]); }
            for (int i = 0; i < o.t.Count; i++) t.Add(b0 + o.t[i]);
        }
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
        }
    }

    internal static class Gfx
    {
        static Shader lit, unlit, spr, add; static bool looked;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        public static string Info = "";

        static void Look()
        {
            if (looked) return; looked = true;
            foreach (var n in new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Standard" }) { var s = Shader.Find(n); if (s != null && s.isSupported) { lit = s; break; } }
            foreach (var n in new[] { "Universal Render Pipeline/Unlit", "Unlit/Texture", "Unlit/Color" }) { var s = Shader.Find(n); if (s != null && s.isSupported) { unlit = s; break; } }
            foreach (var n in new[] { "Sprites/Default", "Universal Render Pipeline/Particles/Unlit", "Hidden/Internal-Colored" }) { var s = Shader.Find(n); if (s != null) { spr = s; break; } }
            // настоящее аддитивное смешивание: только URP Particles/Unlit (у него _BaseColor, как у Material.color); иначе — Sprites/Default с альфой
            { var s = Shader.Find("Universal Render Pipeline/Particles/Unlit"); if (s != null && s.isSupported) add = s; }
            Info = "lit=" + (lit != null ? lit.name : "нет") + ", unlit=" + (unlit != null ? unlit.name : "нет") + ", sprite=" + (spr != null ? spr.name : "нет") + ", additive=" + (add != null ? add.name : "нет (альфа)");
        }

        public static bool HasLit { get { Look(); return lit != null; } }

        /// <summary>Освещаемый материал (кэш по параметрам). Без URP Lit — непрозрачный Unlit.</summary>
        public static Material Lit(Color c, float smooth = 0.3f, float metal = 0f, Color? emis = null)
        {
            Look(); string key = "L" + c.ToString("F2") + smooth.ToString("F1") + metal.ToString("F1") + (emis.HasValue ? emis.Value.ToString("F2") : "-"); Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            if (lit == null) m = Glow(emis.HasValue ? Color.Lerp(c, emis.Value, 0.8f) : c);
            else
            {
                m = new Material(lit); m.color = c;
                try
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth); if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth); if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
                    if (emis.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emis.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
                }
                catch { }
            }
            cache[key] = m; return m;
        }

        /// <summary>Яркий непрозрачный материал без освещения (глаза, ядра техник).</summary>
        public static Material Glow(Color c)
        {
            Look(); string key = "G" + c.ToString("F2"); Material m; if (cache.TryGetValue(key, out m) && m != null) return m;
            var s = unlit ?? spr; if (s == null) return null;
            m = new Material(s); m.color = c; try { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); } catch { }
            cache[key] = m; return m;
        }

        /// <summary>Полупрозрачный материал (ауры, купола, свечение). Sprites/Default умеет вершинный цвет и альфу. additive — аддитивный, если доступен.</summary>
        public static Material Fade(Texture tex, Color c, bool additive = false)
        {
            Look(); bool useAdd = additive && add != null && (c.r + c.g + c.b) > 0.35f; var s = useAdd ? add : (spr ?? unlit); if (s == null) return null;
            var m = new Material(s); m.name = "jjk_own";
            if (useAdd)
            {
                try
                {
                    m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 1f); m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.SetColor("_BaseColor", c);
                }
                catch { }
            }
            m.color = c; if (tex != null) m.mainTexture = tex; try { m.renderQueue = 3000; } catch { } return m;
        }

        /// <summary>Непрозрачный двусторонний материал с записью глубины — для куполов доменов (URP Unlit).</summary>
        public static Material Dome(Texture tex, Color c)
        {
            Look(); var s = unlit; Material m;
            if (s == null || !(m = new Material(s)).HasProperty("_Cull")) return Fade(tex, c);
            m.color = c; if (tex != null) m.mainTexture = tex;
            try { m.SetInt("_Cull", 0); m.SetInt("_ZWrite", 1); m.SetFloat("_Surface", 0f); m.renderQueue = 2000; } catch { }
            return m;
        }

        // ---------- текстуры ----------
        public static Texture2D Tex(int w, int h, Func<float, float, Color> f, bool repeat = false)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true); t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            var px = new Color[w * h]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            t.SetPixels(px); t.Apply(true, false); return t;
        }
        static float Sm(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3f - 2f * x); }
        static Texture2D blob, ring, streak, spark;
        public static Texture2D Blob() { if (blob == null) blob = Tex(64, 64, (u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f; return new Color(1, 1, 1, Mathf.Pow(1f - Sm(0f, 1f, d), 1.6f)); }); return blob; }
        public static Texture2D RingTex() { if (ring == null) ring = Tex(128, 128, (u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f; float a = Sm(0.72f, 0.88f, d) * (1f - Sm(0.9f, 1f, d)); return new Color(1, 1, 1, a); }); return ring; }
        public static Texture2D Streak() { if (streak == null) streak = Tex(64, 16, (u, v) => { float a = (1f - Mathf.Abs(v - .5f) * 2f); return new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(a), 2f) * (1f - Mathf.Pow(Mathf.Abs(u - .5f) * 2f, 2f))); }); return streak; }
        public static Texture2D Spark() { if (spark == null) spark = Tex(32, 32, (u, v) => { float dx = Mathf.Abs(u - .5f) * 2f, dy = Mathf.Abs(v - .5f) * 2f; float a = Mathf.Pow(1f - Mathf.Clamp01(Mathf.Min(dx * 5f, dy * 5f) * 0.5f + Mathf.Max(dx, dy) * 0.5f), 1.8f); return new Color(1, 1, 1, a); }); return spark; }

        // ---------- MeshRenderer-хелпер ----------
        public static GameObject Obj(string name, Transform parent, MB mb, Material m, bool shadows = false)
        {
            var go = new GameObject(name); if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name); var mr = go.AddComponent<MeshRenderer>(); if (m != null) mr.sharedMaterial = m;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; return go;
        }
    }

    /// <summary>Модель из именованных частей: каждую часть можно двигать (крылья, руки, голова). Материал на каждую (часть, материал) — свой меш.</summary>
    internal sealed class ModelB
    {
        public GameObject root; public Dictionary<string, Transform> parts = new Dictionary<string, Transform>();
        sealed class Slot { public string part; public Func<Material> mat; public MB mb = new MB(); public bool shadow; }
        readonly List<Slot> slots = new List<Slot>(); readonly Dictionary<string, Slot> byKey = new Dictionary<string, Slot>();
        public ModelB(string name) { root = new GameObject(name); parts["root"] = root.transform; }
        public Transform P(string name, string parent = "root", Vector3? pos = null)
        {
            Transform t; if (parts.TryGetValue(name, out t)) return t;
            var go = new GameObject(name); go.transform.SetParent(parts[parent], false); go.transform.localPosition = pos ?? Vector3.zero; parts[name] = go.transform; return go.transform;
        }
        public MB M(string part, string key, Func<Material> mat, bool shadow = false)
        {
            string k = part + "|" + key; Slot s; if (byKey.TryGetValue(k, out s)) return s.mb;
            s = new Slot { part = part, mat = mat, shadow = shadow }; slots.Add(s); byKey[k] = s; return s.mb;
        }
        public MB Lit(string part, Color c, float smooth = 0.3f, float metal = 0f, Color? emis = null) { return M(part, "L" + c.ToString("F2") + smooth + metal + (emis.HasValue ? emis.Value.ToString("F2") : ""), () => Gfx.Lit(c, smooth, metal, emis)); }
        public MB Glow(string part, Color c) { return M(part, "G" + c.ToString("F2"), () => Gfx.Glow(c)); }
        public GameObject Finish()
        {
            foreach (var s in slots) if (s.mb.Count > 0) Gfx.Obj(s.part + "_m", parts[s.part], s.mb, s.mat(), s.shadow);
            return root;
        }
        public Transform T(string part) { Transform t; return parts.TryGetValue(part, out t) ? t : root.transform; }
    }
}
