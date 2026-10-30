// Уровень 37 (Бассейны): весь уровень затоплен по колено.
// Вода (1.29.1) — настоящая волнистая поверхность: сетка 0.5 м, которая колышется у игрока, плюс круги, которые расходятся от шагов
// (физически — высота вершин, а не картинка). Три слоя: прозрачная толща с бирюзовым оттенком, зеркальный слой (блики ламп, отражения,
// нормал-карта движется навстречу) и бегущие блики-каустика на самом дне. Плеск, брызги и круги от шагов — своих и чужих (в т.ч. невидимых).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static class Water
    {
        public const float Y = 0.38f;                                  // уровень воды над полом, м
        const int CC = 4, SUB = 8;                                     // клеток в куске воды, ячеек сетки на клетку (шаг 0.5 м)
        sealed class Chunk { public Mesh mesh; public Vector3[] v, n; public float[] h; public float x0, z0, size; public bool active; }
        static readonly List<Chunk> chunks = new List<Chunk>();
        static Material body, spec, cau; static Texture2D texBody, texNrm, texCau; static Vector3 last, lastP; static bool haveLast; static float acc, wake, ringT, dropT;
        static readonly Dictionary<int, Vector3> entLast = new Dictionary<int, Vector3>(); static readonly Dictionary<int, float> entAcc = new Dictionary<int, float>();
        const int MaxR = 32; static readonly float[] rx = new float[MaxR], rz = new float[MaxR], rt = new float[MaxR], ra = new float[MaxR]; static int rn;

        // ---------- текстуры ----------
        static void MakeTextures()
        {
            if (texBody != null) return; const int S = 256;
            texBody = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            texCau = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            texNrm = new Texture2D(S, S, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var h = new float[S * S]; float tp = 6.2832f;
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                float a = Mathf.Sin(tp * (3 * u + 2 * v)) + Mathf.Sin(tp * (-2 * u + 4 * v) + 1.3f) + Mathf.Sin(tp * (5 * u + v) + 2.1f) + Mathf.Sin(tp * (u - 5 * v) + 0.7f) + 0.6f * Mathf.Sin(tp * (7 * u + 6 * v) + 4f) + 0.5f * Mathf.Sin(tp * (-6 * u + 7 * v) + 5.2f);
                h[y * S + x] = a * 0.2f;
            }
            var px = new Color32[S * S]; var cp = new Color32[S * S]; var np = new Color32[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float n = h[y * S + x];
                float line = Mathf.Exp(-n * n * 26f), soft = Mathf.Exp(-n * n * 5f);                           // тонкие яркие линии каустики и мягкое сияние
                float r = 0.20f + soft * 0.06f, g = 0.62f + soft * 0.08f, b = 0.70f + soft * 0.06f;           // бирюзовая толща
                px[y * S + x] = new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), 255);
                float c = Mathf.Clamp01(line * 1.1f + soft * 0.16f); cp[y * S + x] = new Color32((byte)(c * 0.75f * 255), (byte)(c * 255), (byte)(c * 255), 255);
                float dx = h[y * S + (x + 1) % S] - h[y * S + (x - 1 + S) % S], dy = h[((y + 1) % S) * S + x] - h[((y - 1 + S) % S) * S + x];
                Vector3 nv = new Vector3(-dx * 6f, -dy * 6f, 1f).normalized;
                np[y * S + x] = new Color32((byte)((nv.x * 0.5f + 0.5f) * 255), (byte)((nv.y * 0.5f + 0.5f) * 255), (byte)((nv.z * 0.5f + 0.5f) * 255), 255);
            }
            texBody.SetPixels32(px); texBody.Apply(true); texCau.SetPixels32(cp); texCau.Apply(true); texNrm.SetPixels32(np); texNrm.Apply(true);
        }

        static void Transparent(Material m, bool additive, int queue)
        {
            if (m == null || !m.HasProperty("_Surface")) return;
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", additive ? 2f : 0f); m.SetFloat("_SrcBlend", additive ? 1f : 5f); m.SetFloat("_DstBlend", additive ? 1f : 10f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = queue; m.SetOverrideTag("RenderType", "Transparent"); m.SetShaderPassEnabled("ShadowCaster", false);
        }
        static void Bump(Material m, float scale)
        {
            if (m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", texNrm); m.EnableKeyword("_NORMALMAP"); if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", scale); }
        }
        static void Scale(Material m, float s) { m.mainTextureScale = new Vector2(s, s); if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", new Vector2(s, s)); }
        static void Offset(Material m, Vector2 o) { m.mainTextureOffset = o; if (m.HasProperty("_BaseMap")) m.SetTextureOffset("_BaseMap", o); }

        static void MakeMaterials()
        {
            if (body != null) return; MakeTextures();
            try
            {
                // 1) толща воды: бирюзовый оттенок, пол просвечивает
                body = BrGfx.Surf(texBody, new Color(1f, 1f, 1f, 0.42f), 0.6f, 0f, new Color(0.01f, 0.05f, 0.055f)); Bump(body, 0.35f); Scale(body, 1f); Transparent(body, false, 3000);
                // 2) зеркальный слой: почти чёрный, добавляется поверх — остаются только блики ламп и отражения
                spec = BrGfx.Surf(null, new Color(0.012f, 0.018f, 0.022f, 1f), 0.96f, 0f); Bump(spec, 1.0f); Scale(spec, 1.9f); Transparent(spec, true, 3001);
                // 3) каустика на дне: светлые линии, освещаются лампами
                cau = BrGfx.Surf(texCau, new Color(0.55f, 0.85f, 0.9f, 1f), 0.05f, 0f); Transparent(cau, true, 2999);
            }
            catch (Exception e) { Plugin.Warn("вода: материал", e); }
        }

        // ---------- построение ----------
        public static void Build(Built b, Transform T)
        {
            chunks.Clear(); MakeMaterials(); if (body == null) return;
            float C = b.C, step = C / SUB; int V = CC * SUB + 1; var groups = new Dictionary<long, List<Vector2Int>>();
            foreach (var wc in b.cells) { long key = ((long)(wc.x / CC) << 20) | (uint)(wc.y / CC); List<Vector2Int> l; if (!groups.TryGetValue(key, out l)) { l = new List<Vector2Int>(); groups[key] = l; } l.Add(wc); }
            var flat = new MeshB(); int gi = 0;
            foreach (var kv in groups)
            {
                int gx = (int)(kv.Key >> 20), gy = (int)(kv.Key & 0xFFFFF); float x0 = gx * CC * C, z0 = gy * CC * C;
                var v = new Vector3[V * V]; var n = new Vector3[V * V]; var uv = new Vector2[V * V]; var tg = new Vector4[V * V]; var tris = new List<int>();
                for (int j = 0; j < V; j++) for (int i = 0; i < V; i++) { int k = j * V + i; float x = x0 + i * step, z = z0 + j * step; v[k] = new Vector3(x, Y, z); n[k] = Vector3.up; uv[k] = new Vector2(x / 4f, z / 4f); tg[k] = new Vector4(1f, 0f, 0f, -1f); }
                foreach (var wc in kv.Value)
                {
                    int lx = wc.x - gx * CC, ly = wc.y - gy * CC;
                    for (int sj = 0; sj < SUB; sj++) for (int si = 0; si < SUB; si++)
                    { int a = (ly * SUB + sj) * V + lx * SUB + si, bb = a + 1, c = a + V, d = a + V + 1; tris.Add(a); tris.Add(c); tris.Add(bb); tris.Add(bb); tris.Add(c); tris.Add(d); }
                    flat.FloorQuad(wc.x * C, wc.y * C, (wc.x + 1) * C, (wc.y + 1) * C, 0.014f, true);
                }
                var mesh = new Mesh { name = "BR_water_" + gi }; mesh.MarkDynamic(); mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTangents(tg); mesh.SetUVs(0, uv); mesh.SetTriangles(tris, 0);
                float size = CC * C; mesh.bounds = new Bounds(new Vector3(x0 + size * 0.5f, Y, z0 + size * 0.5f), new Vector3(size, 0.4f, size));
                var ch = new Chunk { mesh = mesh, v = v, n = n, h = new float[V * V], x0 = x0, z0 = z0, size = size }; chunks.Add(ch);
                for (int layer = 0; layer < 2; layer++)
                {
                    var go = new GameObject("BR_water_" + gi + (layer == 0 ? "a" : "s")); go.transform.SetParent(T, false); go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = layer == 0 ? body : spec; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                    var bd = mesh.bounds; bd.center += b.baseP; b.crs.Add(mr); b.cbs.Add(bd); b.chunkCount++;
                }
                gi++;
            }
            b.meshCount++;
            // дно: каустика
            if (cau != null)
            {
                int i = 0; foreach (var kv in flat.Split(16f)) { var go = new GameObject("BR_caustic_" + (i++)); go.transform.SetParent(T, false); var m = kv.Value.ToMesh("BR_caustic"); go.AddComponent<MeshFilter>().sharedMesh = m; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = cau; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; var bd = m.bounds; bd.center += b.baseP; b.crs.Add(mr); b.cbs.Add(bd); b.chunkCount++; }
            }
        }

        // ---------- круги ----------
        static void Ripple(Vector3 local, float amp)
        {
            int i = rn < MaxR ? rn++ : 0; if (rn >= MaxR) { int old = 0; for (int k = 1; k < MaxR; k++) if (rt[k] < rt[old]) old = k; i = old; }
            rx[i] = local.x; rz[i] = local.z; rt[i] = Time.time; ra[i] = amp;
        }

        static float Swell(float x, float z, float t)
        {
            return 0.007f * Mathf.Sin(0.9f * x + 0.6f * z + t * 1.1f) + 0.005f * Mathf.Sin(-0.5f * x + 1.3f * z + t * 1.5f + 1.7f) + 0.003f * Mathf.Sin(2.1f * x - 1.7f * z + t * 2.3f);
        }

        static void Deform(Vector3 pl, float t, float rad)
        {
            int V = CC * SUB + 1; float step = (chunks.Count > 0 ? chunks[0].size : 16f) / (V - 1); float r2 = rad * rad, fade0 = rad - 7f;
            for (int q = rn - 1; q >= 0; q--) if (t - rt[q] > 3.6f) { rn--; rx[q] = rx[rn]; rz[q] = rz[rn]; rt[q] = rt[rn]; ra[q] = ra[rn]; }
            foreach (var ch in chunks)
            {
                float cdx = Mathf.Max(0f, Mathf.Max(ch.x0 - pl.x, pl.x - (ch.x0 + ch.size))), cdz = Mathf.Max(0f, Mathf.Max(ch.z0 - pl.z, pl.z - (ch.z0 + ch.size)));
                if (cdx * cdx + cdz * cdz > r2) { if (ch.active) { ch.active = false; for (int k = 0; k < ch.v.Length; k++) { ch.v[k].y = Y; ch.n[k] = Vector3.up; } ch.mesh.SetVertices(ch.v); ch.mesh.SetNormals(ch.n); } continue; }
                ch.active = true; var v = ch.v; var h = ch.h;
                for (int j = 0; j < V; j++) for (int i = 0; i < V; i++)
                {
                    int k = j * V + i; float x = ch.x0 + i * step, z = ch.z0 + j * step; float dx = x - pl.x, dz = z - pl.z, dd = dx * dx + dz * dz; float w = 1f;
                    if (dd > fade0 * fade0) { if (dd > r2) { h[k] = 0f; continue; } float f = (Mathf.Sqrt(dd) - fade0) / 7f; w = 1f - f * f * (3f - 2f * f); }
                    float hh = Swell(x, z, t);
                    for (int q = 0; q < rn; q++)
                    {
                        float age = t - rt[q], R = age * 1.05f; float ex = x - rx[q], ez = z - rz[q]; float d2 = ex * ex + ez * ez; float hi = R + 1.4f; if (d2 > hi * hi) continue; float lo = R - 1.4f; if (lo > 0f && d2 < lo * lo) continue;
                        float d = Mathf.Sqrt(d2), s = d - R; float life = 1f - age / 3.6f; hh += ra[q] * life * life * Mathf.Exp(-s * s / 0.5f) * Mathf.Cos(6.2832f * s / 1.0f) / (1f + d * 0.7f);
                    }
                    h[k] = hh * w;
                }
                for (int j = 0; j < V; j++) for (int i = 0; i < V; i++)
                {
                    int k = j * V + i; int il = i > 0 ? k - 1 : k, ir = i < V - 1 ? k + 1 : k, jd = j > 0 ? k - V : k, ju = j < V - 1 ? k + V : k;
                    float gx = (h[ir] - h[il]) / (step * (ir - il)), gz = (h[ju] - h[jd]) / (step * ((ju - jd) / V));
                    v[k].y = Y + h[k]; ch.n[k] = new Vector3(-gx, 1f, -gz).normalized;
                }
                ch.mesh.SetVertices(v); ch.mesh.SetNormals(ch.n);
            }
        }

        // ---------- покадровая логика ----------
        public static void Tick(Built b, Vector3 pc, float dt)
        {
            if (body == null) return; float t = Time.time;
            try
            {   // слои движутся в разные стороны — получается живая рябь; каустика «дышит»
                Offset(body, new Vector2(t * 0.012f + Mathf.Sin(t * 0.23f) * 0.03f, t * 0.008f + Mathf.Cos(t * 0.19f) * 0.03f));
                Offset(spec, new Vector2(-t * 0.017f + Mathf.Sin(t * 0.31f) * 0.04f, t * 0.011f + Mathf.Cos(t * 0.27f) * 0.04f));
                if (cau != null) Offset(cau, new Vector2(t * 0.02f + Mathf.Sin(t * 0.9f) * 0.012f, -t * 0.014f + Mathf.Cos(t * 0.7f) * 0.012f));
            }
            catch { }
            if (Mathf.Abs(pc.y - (b.baseP.y + 1f)) > 2.5f) { haveLast = false; return; }
            Vector3 pl = pc - b.baseP; Vector3 p = new Vector3(pc.x, 0f, pc.z); if (!haveLast) { last = p; haveLast = true; }
            float d0 = (p - last).magnitude; last = p; if (d0 > 3f) { acc = 0f; wake = 0f; return; }
            if (d0 > 0.004f) { acc += d0; wake += d0; } float spd = d0 / Mathf.Max(0.001f, dt);
            Splash.Tick(dt); if (spd > 0.6f) { Vector3 mv = (p - lastP); lastP = p; Vector3 dirm = mv.sqrMagnitude > 1e-8f ? mv.normalized : Vector3.forward; Splash.Wake(new Vector3(pc.x, b.baseP.y + Y, pc.z), dirm, spd); } else lastP = p;
            if (wake > 0.45f) { wake = 0f; Ripple(new Vector3(pl.x, 0f, pl.z), 0.012f); }                                              // след за ногами
            if (acc > 1.7f - Mathf.Clamp01(spd / 8f) * 0.5f)
            {
                acc = 0f; Vector3 w = new Vector3(pc.x, b.baseP.y + Y + 0.01f, pc.z); float k = Mathf.Clamp01(spd / 6f); Ripple(new Vector3(pl.x, 0f, pl.z), 0.03f + k * 0.03f);
                try { Snd2.At("splash", new Vector3(w.x, w.y, w.z), 0.2f + k * 0.25f); Splash.Step(new Vector3(w.x, b.baseP.y + Y, w.z), k); } catch { }
            }
            if (t > dropT && chunks.Count > 0) { dropT = t + UnityEngine.Random.Range(0.5f, 1.4f); float a = UnityEngine.Random.value * 6.28f, r = 2f + UnityEngine.Random.value * 6f; Ripple(new Vector3(pl.x + Mathf.Cos(a) * r, 0f, pl.z + Mathf.Sin(a) * r), 0.01f); }   // капли с потолка
            if (t >= ringT)
            {   // круги и плеск от монстров поблизости (в том числе невидимых за углом)
                ringT = t + 0.1f; int made = 0;
                for (int i = 0; i < Ents.All.Count && made < 2; i++)
                {
                    var e = Ents.All[i]; if (e == null || e.root == null) continue; Vector3 ep = e.pos; if ((ep - pc).sqrMagnitude > 24f * 24f) continue;
                    Vector3 lp; if (!entLast.TryGetValue(e.id, out lp)) { entLast[e.id] = ep; entAcc[e.id] = 0f; continue; }
                    float md = Util.Flat(ep - lp).magnitude; entLast[e.id] = ep; if (md > 3f) continue; float ac = entAcc[e.id] + md; if (ac < 1.3f) { entAcc[e.id] = ac; continue; }
                    entAcc[e.id] = 0f; made++; Vector3 el = ep - b.baseP; Ripple(new Vector3(el.x, 0f, el.z), 0.035f);
                    try { if ((ep - pc).sqrMagnitude < 16f * 16f) Snd2.At("splash", new Vector3(ep.x, b.baseP.y + Y, ep.z), 0.28f); } catch { }
                }
            }
            if (Play.Qual < 3) { try { Deform(pl, t, Play.Qual >= 2 ? 13f : 18f); } catch (Exception ex) { Plugin.Warn("вода: волны", ex); chunks.Clear(); } }
        }

        public static void Reset()
        {
            Splash.Clear(); haveLast = false; acc = 0f; wake = 0f; rn = 0; entLast.Clear(); entAcc.Clear(); foreach (var ch in chunks) if (ch.mesh != null) UnityEngine.Object.Destroy(ch.mesh); chunks.Clear(); body = null; spec = null; cau = null;
            if (texBody != null) UnityEngine.Object.Destroy(texBody); if (texNrm != null) UnityEngine.Object.Destroy(texNrm); if (texCau != null) UnityEngine.Object.Destroy(texCau); texBody = null; texNrm = null; texCau = null;
        }
    }
}
