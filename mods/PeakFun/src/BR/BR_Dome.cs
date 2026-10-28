// Небо-купол вместо потолка (1.32): процедурная текстура неба (звёзды, облака, луна/солнце), купол следует за камерой как скайбокс.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static class Dome
    {
        static GameObject cur; static bool hooked; static Shader shader; static bool looked;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        static Shader Sh()
        {
            if (looked) return shader; looked = true;
            foreach (var n in new[] { "Sprites/Default", "Universal Render Pipeline/Unlit" }) { var s = Shader.Find(n); if (s != null) { shader = s; break; } }
            return shader;
        }
        /// <summary>Купол можно строить только если материал без записи глубины (иначе он закрыл бы весь уровень).</summary>
        public static bool Supported() { try { var s = Sh(); if (s == null) return false; if (s.name == "Sprites/Default") return true; var m = new Material(s); bool ok = m.HasProperty("_ZWrite"); UnityEngine.Object.Destroy(m); return ok; } catch { return false; } }

        static float Hash(int x, int y) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16; return (h & 0xFFFFFF) / 16777215f; } }
        static float Sm(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3f - 2f * x); }
        static float H3(int x, int y, int z, int sd) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + z * 1442695041 + sd * 1013904223); h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16; return (h & 0xFFFF) / 65535f; } }
        static float N3(float x, float y, float z, int sd)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y), zi = Mathf.FloorToInt(z); float fx = x - xi, fy = y - yi, fz = z - zi; fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
            float a = Mathf.Lerp(H3(xi, yi, zi, sd), H3(xi + 1, yi, zi, sd), fx), b = Mathf.Lerp(H3(xi, yi + 1, zi, sd), H3(xi + 1, yi + 1, zi, sd), fx);
            float c = Mathf.Lerp(H3(xi, yi, zi + 1, sd), H3(xi + 1, yi, zi + 1, sd), fx), d = Mathf.Lerp(H3(xi, yi + 1, zi + 1, sd), H3(xi + 1, yi + 1, zi + 1, sd), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
        }
        /// <summary>Шум на сфере (шва нет, без растяжения): точка единичной сферы × масштаб.</summary>
        static float Fbm(Vector3 p, int sd)
        {
            float a = 0.5f, f = 1f, s = 0f, tot = 0f;
            for (int o = 0; o < 4; o++) { s += a * N3(p.x * f + 7.3f, p.y * f + 1.7f, p.z * f + 3.1f, sd + o); tot += a; a *= 0.5f; f *= 2.05f; }
            return s / tot;
        }

        const int TW = 2048, TH = 512; const float E0 = -60f, E1 = 90f;
        static Texture2D MakeTex(LevelDef L)
        {
            string id = L.id; Texture2D t; if (cache.TryGetValue(id, out t) && t != null) return t;
            Color hor = L.fog, top, glow = Color.black; float stars = 0f, cloudCov = 0.5f, cloudDark = 0f, cloudAlpha = 0.6f; Color cloudCol = Color.white; Vector2 sun = new Vector2(-1f, 0f); float sunR = 0f; Color sunCol = Color.white; float seed = 11f + (id.GetHashCode() & 255);
            switch (id)
            {
                case "9": top = new Color(0.006f, 0.012f, 0.035f); stars = 0.0045f; cloudCov = 0.55f; cloudAlpha = 0.28f; cloudCol = new Color(0.05f, 0.06f, 0.09f); sun = new Vector2(0.64f, 38f); sunR = 3.2f; sunCol = new Color(0.82f, 0.88f, 1f); break;
                case "11": top = new Color(0.012f, 0.014f, 0.04f); stars = 0.0022f; glow = new Color(0.95f, 0.5f, 0.2f); cloudCov = 0.5f; cloudAlpha = 0.35f; cloudCol = new Color(0.18f, 0.11f, 0.1f); sun = new Vector2(0.22f, 52f); sunR = 2.4f; sunCol = new Color(0.9f, 0.92f, 1f); break;
                case "10": top = new Color(0.42f, 0.38f, 0.28f); glow = new Color(1f, 0.7f, 0.28f); cloudCov = 0.38f; cloudAlpha = 0.9f; cloudCol = new Color(0.55f, 0.5f, 0.4f); cloudDark = 0.55f; sun = new Vector2(0.27f, 7f); sunR = 4.5f; sunCol = new Color(1f, 0.85f, 0.5f); break;
                case "94": top = new Color(0.22f, 0.5f, 0.92f); cloudCov = 0.52f; cloudAlpha = 0.95f; cloudCol = new Color(1f, 1f, 1f); cloudDark = 0.12f; sun = new Vector2(0.4f, 48f); sunR = 3.4f; sunCol = new Color(1f, 0.98f, 0.88f); break;
                default: top = new Color(0.0f, 0.016f, 0.022f); stars = 0.0008f; glow = new Color(0.1f, 0.45f, 0.5f); cloudCov = 0.5f; cloudAlpha = 0.4f; cloudCol = new Color(0.02f, 0.09f, 0.1f); break;
            }
            t = Vfx.Tex(TW, TH, (u, v) =>
            {
                float e = Mathf.Lerp(E0, E1, v), th = u * Mathf.PI * 2f; Color c;
                float up = Sm(0f, 62f, e); c = Color.Lerp(hor, top, Mathf.Pow(up, 0.75f));
                if (e < 0f) c = Color.Lerp(hor, hor * 0.85f, Mathf.Clamp01(-e / 30f));
                if (glow.maxColorComponent > 0f) c = Color.Lerp(c, glow, 0.5f * (1f - Sm(0f, 24f, Mathf.Abs(e))) * (0.55f + 0.45f * Mathf.Sin(th * 3f + seed)));
                if (e > -2f)
                {
                    if (sunR > 0f)
                    {   // солнце / луна с ореолом
                        float du = Mathf.Abs(u - sun.x); du = Mathf.Min(du, 1f - du); float dx = du * 360f * Mathf.Cos(Mathf.Clamp(sun.y, -80f, 80f) * Mathf.Deg2Rad), dy = e - sun.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                        c += sunCol * 0.38f * Mathf.Exp(-d / (sunR * 3.4f)) * (stars > 0f ? 0.5f : 1f); if (d < sunR) c = Color.Lerp(c, sunCol, 1f - Sm(sunR * 0.8f, sunR, d));
                    }
                    if (stars > 0f && e > 3f)
                    {
                        int sx = (int)(u * TW), sy = (int)(v * TH); float h = Hash(sx / 2, sy / 2); float dens = stars * Sm(3f, 30f, e);
                        if (h > 1f - dens) { float br = 0.35f + 0.65f * Hash(sx, sy + 77); c += new Color(0.9f, 0.95f, 1f) * br; }
                    }
                    float ce = Mathf.Cos(e * Mathf.Deg2Rad); float n = Fbm(new Vector3(Mathf.Cos(th) * ce, Mathf.Sin(e * Mathf.Deg2Rad), Mathf.Sin(th) * ce) * 2.4f, (int)seed);
                    float dens2 = Sm(cloudCov - 0.1f, cloudCov + 0.18f, n) * Sm(-1f, 14f, e) * (1f - 0.35f * Sm(70f, 90f, e));
                    if (dens2 > 0f)
                    {
                        Color cc = Color.Lerp(cloudCol, cloudCol * (1f - cloudDark), Sm(0.55f, 0.9f, n)); if (glow.maxColorComponent > 0f && id == "10") cc = Color.Lerp(cc, glow, 0.35f * (1f - Sm(0f, 40f, e)));
                        c = Color.Lerp(c, cc, dens2 * cloudAlpha);
                    }
                }
                c.a = 1f; return c;
            });
            t.wrapMode = TextureWrapMode.Repeat; t.filterMode = FilterMode.Bilinear; cache[id] = t; return t;
        }

        static Mesh MakeMesh()
        {
            int rings = 24, seg = 64; float R = 14f; var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float f = (float)r / rings, e = Mathf.Lerp(E0, E1, f) * Mathf.Deg2Rad;
                for (int s = 0; s <= seg; s++) { float a = (float)s / seg * Mathf.PI * 2f; vs.Add(new Vector3(Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Sin(a) * Mathf.Cos(e)) * R); uv.Add(new Vector2((float)s / seg, f)); }
            }
            for (int r = 0; r < rings; r++) for (int s = 0; s < seg; s++)
            {
                int i0 = r * (seg + 1) + s, i1 = i0 + 1, i2 = i0 + seg + 1, i3 = i2 + 1;
                tr.Add(i0); tr.Add(i2); tr.Add(i1); tr.Add(i1); tr.Add(i2); tr.Add(i3); tr.Add(i0); tr.Add(i1); tr.Add(i2); tr.Add(i1); tr.Add(i3); tr.Add(i2);   // обе стороны
            }
            var m = new Mesh { name = "BR_dome" }; m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.bounds = new Bounds(Vector3.zero, Vector3.one * 100f); return m;
        }

        public static void Build(Built b, Transform T)
        {
            try
            {
                var go = new GameObject("BR_dome"); go.transform.SetParent(T, false); go.AddComponent<MeshFilter>().sharedMesh = MakeMesh();
                var mr = go.AddComponent<MeshRenderer>(); var m = new Material(Sh()); m.mainTexture = MakeTex(b.L); m.color = Color.white;
                try { m.SetInt("_ZWrite", 0); m.SetInt("_Cull", 0); } catch { }
                m.renderQueue = 1000; mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                cur = go; Follow(); Hook();
            }
            catch (Exception e) { Plugin.Warn("Dome", e); }
        }
        public static void Follow() { try { if (cur == null) return; var cam = Camera.main; if (cam == null) return; cur.transform.position = cam.transform.position; } catch { } }
        static void Hook()
        {
            if (hooked) return; hooked = true;
            try { Camera.onPreCull += c => { if (cur != null && c == Camera.main) cur.transform.position = c.transform.position; }; } catch { }
            try { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += (ctx, c) => { if (cur != null && c == Camera.main) cur.transform.position = c.transform.position; }; } catch { }
        }
    }
}
