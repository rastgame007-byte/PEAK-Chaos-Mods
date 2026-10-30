// Органические модели монстров (1.24): вместо шаров и кубиков — профильные трубки (мышцы, рёбра), скульптурные головы,
// комковатые «живые» формы, крылья-полигоны и светящаяся улыбка-полумесяц. Иерархия суставов (e.j[]) та же — анимация прежняя.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static class OrgMesh
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>Комковатая сфера: диаметр 1, неровности без шва по долготе.</summary>
        public static Mesh Blob(int variant, float amp = 0.07f)
        {
            string key = "blob" + variant + "_" + amp; Mesh m; if (cache.TryGetValue(key, out m) && m != null) return m;
            const int LAT = 14, LON = 22; var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>(); float sd = variant * 3.7f;
            for (int i = 0; i <= LAT; i++) for (int j = 0; j <= LON; j++)
                {
                    float ph = i / (float)LAT * Mathf.PI, la = j / (float)LON * Mathf.PI * 2f; Vector3 d = new Vector3(Mathf.Sin(ph) * Mathf.Sin(la), Mathf.Cos(ph), Mathf.Sin(ph) * Mathf.Cos(la));
                    float nz = Mathf.PerlinNoise(d.x * 1.8f + sd + 5f, d.y * 1.8f + d.z * 1.3f + sd) - 0.5f; float k = 0.5f * (1f + amp * 2f * nz); vs.Add(d * k); uv.Add(new Vector2(j / (float)LON, i / (float)LAT));
                }
            for (int i = 0; i < LAT; i++) for (int j = 0; j < LON; j++) { int a = i * (LON + 1) + j, b = a + LON + 1; tr.Add(a); tr.Add(a + 1); tr.Add(b); tr.Add(a + 1); tr.Add(b + 1); tr.Add(b); }
            m = new Mesh { name = "pf_blob" + variant }; m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.RecalculateNormals();
            var ns = m.normals; for (int i = 0; i <= LAT; i++) { int i0 = i * (LON + 1), i1 = i0 + LON; Vector3 av = (ns[i0] + ns[i1]).normalized; ns[i0] = av; ns[i1] = av; } m.normals = ns; m.RecalculateBounds(); cache[key] = m; return m;
        }

        /// <summary>Эллиптическое крыло (двустороннее): +X — наружу, с лёгким загибом к кончику.</summary>
        public static Mesh Wing(float span, float chord, float sweep, string tag)
        {
            string key = "wing" + tag; Mesh m; if (cache.TryGetValue(key, out m) && m != null) return m;
            const int N = 22; var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>(); var ns = new List<Vector3>();
            vs.Add(Vector3.zero); uv.Add(new Vector2(0f, 0.5f));
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N * Mathf.PI * 2f; float ex = 0.5f + 0.5f * Mathf.Cos(t), ey = Mathf.Sin(t); float x = ex * span, z = ey * chord * 0.5f * (0.55f + 0.45f * Mathf.Sin(ex * Mathf.PI * 0.9f + 0.3f)) - sweep * ex * ex * span;
                vs.Add(new Vector3(x, Mathf.Sin(ex * Mathf.PI) * 0.01f, z)); uv.Add(new Vector2(ex, 0.5f + 0.5f * ey));
            }
            for (int i = 1; i <= N; i++) { tr.Add(0); tr.Add(i); tr.Add(i + 1); }
            int cnt = vs.Count; for (int i = 0; i < cnt; i++) { vs.Add(vs[i]); uv.Add(uv[i]); } int n0 = cnt; for (int i = 1; i <= N; i++) { tr.Add(n0); tr.Add(n0 + i + 1); tr.Add(n0 + i); }
            for (int i = 0; i < cnt; i++) ns.Add(Vector3.up); for (int i = 0; i < cnt; i++) ns.Add(Vector3.down);
            m = new Mesh { name = "pf_wing" + tag }; m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.SetNormals(ns); m.RecalculateBounds(); cache[key] = m; return m;
        }

        /// <summary>Улыбка-полумесяц шириной w: фронтальная лента (нормаль +Z), верхняя и нижняя дуга загнуты вверх.</summary>
        public static Mesh Grin(float w, float thick, float curl)
        {
            string key = "grin" + w + "_" + thick + "_" + curl; Mesh m; if (cache.TryGetValue(key, out m) && m != null) return m;
            const int N = 28; var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>(); var ns = new List<Vector3>();
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N, x = Mathf.Lerp(-w, w, u), q = (x / w); float yc = curl * q * q; float th = thick * Mathf.Pow(Mathf.Max(0f, 1f - q * q), 0.6f);
                vs.Add(new Vector3(x, yc + th * 0.5f, 0f)); vs.Add(new Vector3(x, yc - th * 0.5f, 0f)); uv.Add(new Vector2(u, 1f)); uv.Add(new Vector2(u, 0f)); ns.Add(Vector3.forward); ns.Add(Vector3.forward);
            }
            for (int i = 0; i < N; i++) { int a = i * 2; tr.Add(a); tr.Add(a + 2); tr.Add(a + 1); tr.Add(a + 1); tr.Add(a + 2); tr.Add(a + 3); }
            int cnt = vs.Count; for (int i = 0; i < cnt; i++) { vs.Add(vs[i]); uv.Add(uv[i]); ns.Add(Vector3.back); } for (int i = 0; i < N; i++) { int a = cnt + i * 2; tr.Add(a); tr.Add(a + 1); tr.Add(a + 2); tr.Add(a + 1); tr.Add(a + 3); tr.Add(a + 2); }
            m = new Mesh { name = "pf_grin" }; m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.SetNormals(ns); m.RecalculateBounds(); cache[key] = m; return m;
        }

        public static float GrinThick(float q, float thick) { return thick * Mathf.Pow(Mathf.Max(0f, 1f - q * q), 0.6f); }
    }

    internal static partial class Ents
    {
        // ---------- помощники ----------
        static Transform Limb(Transform par, Vector3 a, Vector3 b, float r, string prof, Material m, float wx = 1f)
        {
            var go = new GameObject("BR_part"); go.transform.SetParent(par, false); Vector3 d = a - b; float len = d.magnitude; if (len < 0.001f) return go.transform;
            go.transform.localPosition = (a + b) * 0.5f; go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d / len); go.transform.localScale = new Vector3(r * 2f * wx, len * 0.5f, r * 2f);
            go.AddComponent<MeshFilter>().sharedMesh = BrBody.Tube(prof, -1); var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return go.transform;
        }
        static Transform Blob(Transform par, Vector3 lp, Vector3 size, Material m, int variant = 0, Vector3 euler = default(Vector3))
        {
            var go = new GameObject("BR_part"); go.transform.SetParent(par, false); go.transform.localPosition = lp; go.transform.localScale = size; go.transform.localEulerAngles = euler;
            go.AddComponent<MeshFilter>().sharedMesh = OrgMesh.Blob(variant); var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return go.transform;
        }
        static Transform MeshAt(Transform par, Mesh mesh, Vector3 lp, Vector3 scale, Material m, Vector3 euler = default(Vector3))
        {
            var go = new GameObject("BR_part"); go.transform.SetParent(par, false); go.transform.localPosition = lp; go.transform.localScale = scale; go.transform.localEulerAngles = euler;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return go.transform;
        }
        static Material TwoSided(Material m) { try { if (m != null && m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); } catch { } return m; }

        /// <summary>Скульптурная голова (как у Вора Кожи) в заданном масштабе: верхняя часть на голове, нижняя челюсть — на суставе jaw.</summary>
        static void SculptHead(Transform head, Transform jawJoint, Vector3 scale, Material skin, Material toothM, Material eyeM, Material dark, bool teeth)
        {
            Mesh hu, hl; BrBody.Head(out hu, out hl); jawJoint.localPosition = Vector3.Scale(BrBody.JawJoint, scale); MeshAt(head, hu, Vector3.zero, scale, skin); MeshAt(jawJoint, hl, Vector3.zero, scale, skin);
            if (eyeM != null) foreach (float sx in new[] { -1f, 1f }) Blob(head, new Vector3(sx * 0.03f * scale.x, 0.144f * scale.y, 0.086f * scale.z), new Vector3(0.016f * scale.x, 0.016f * scale.y, 0.016f * scale.z), eyeM, 1);
            if (!teeth) return; var rn = new System.Random(77);
            for (int i = 0; i < 15; i++) { float xx = (i - 7) * 0.0068f; Part(jawJoint, PrimitiveType.Cube, new Vector3(xx * scale.x, 0.008f * scale.y, (0.093f - xx * xx * 3.5f) * scale.z), new Vector3(0.0045f * scale.x, (0.016f + (float)rn.NextDouble() * 0.014f) * scale.y, 0.0045f * scale.z), toothM); }
            for (int i = 0; i < 17; i++) { float xx = (i - 8) * 0.0063f; Part(head, PrimitiveType.Cube, new Vector3(xx * scale.x, 0.054f * scale.y, (0.103f - xx * xx * 3.5f) * scale.z), new Vector3(0.0045f * scale.x, (0.014f + (float)rn.NextDouble() * 0.012f) * scale.y, 0.0045f * scale.z), toothM); }
        }

        // ================= Улыбка =================
        static void BuildSmilerV2(Ent e)
        {
            var T = e.root.transform; var blk = BrGfx.Surf(null, new Color(0.008f, 0.008f, 0.01f), 0.55f); var gl = Glow(new Color(1f, 0.97f, 0.82f), 3.0f); var gum = BrGfx.Surf(null, new Color(0.01f, 0.01f, 0.012f), 0.2f); TwoSided(gl); TwoSided(gum);
            var body = Jt(T, "body", new Vector3(0, 1.15f, 0)); e.j[0] = body;
            MeshAt(body, BrBody.Tube("uchest", -1), new Vector3(0, -0.12f, 0), new Vector3(0.86f, 0.78f, 0.52f), blk);             // плащ
            for (int i = 0; i < 11; i++)
            {   // рваный подол
                float a = i / 11f * Mathf.PI * 2f, rr = 0.34f; Vector3 p = new Vector3(Mathf.Sin(a) * rr, -0.82f, Mathf.Cos(a) * rr * 0.62f); float ln = 0.28f + 0.2f * Mathf.Abs(Mathf.Sin(i * 2.3f));
                Limb(body, p + Vector3.up * 0.1f, p + new Vector3(Mathf.Sin(a) * 0.05f, -ln, Mathf.Cos(a) * 0.03f), 0.05f, "strand", blk, 1.5f);
            }
            Blob(body, new Vector3(0, 0.74f, 0.02f), new Vector3(0.5f, 0.56f, 0.44f), blk, 2);                                       // голова
            foreach (float sx in new[] { -1f, 1f })
            {   // глаза — вытянутые светящиеся щели
                var eye = Blob(body, new Vector3(sx * 0.125f, 0.82f, 0.19f), new Vector3(0.13f, 0.085f, 0.04f), gl, 3, new Vector3(0, 0, sx * -14f));
                Blob(body, new Vector3(sx * 0.125f, 0.82f, 0.215f), new Vector3(0.035f, 0.05f, 0.012f), gum, 4);
            }
            // улыбка: светящийся полумесяц с тёмными зубами-промежутками
            MeshAt(body, OrgMesh.Grin(0.2f, 0.115f, 0.075f), new Vector3(0, 0.62f, 0.213f), Vector3.one, gl);
            for (int i = -9; i <= 9; i++)
            {
                float q = i / 10f, x = q * 0.2f, th = OrgMesh.GrinThick(q, 0.115f); if (th < 0.02f) continue;
                Part(body, PrimitiveType.Cube, new Vector3(x, 0.62f + 0.075f * q * q, 0.222f), new Vector3(0.0045f, th * 0.9f, 0.004f), gum);
            }
            // 6 длинных рук-щупалец: сначала в сторону, затем свисают; на концах — тонкие пальцы
            for (int i = 0; i < 6; i++)
            {
                float s = i < 3 ? -1f : 1f; float k = i % 3; var l = Jt(body, "leg" + i, new Vector3(s * 0.3f, 0.22f - k * 0.2f, (k - 1f) * 0.12f)); e.j[1 + i] = l;
                Vector3 e1 = new Vector3(s * 0.5f, -0.02f - k * 0.05f, 0f), e2 = e1 + new Vector3(s * 0.2f, -0.55f - k * 0.1f, 0.04f * (k - 1f)); Limb(l, Vector3.zero, e1, 0.034f, "uarm", blk); Limb(l, e1, e2, 0.026f, "farm", blk);
                for (int f = 0; f < 4; f++) Limb(l, e2, e2 + new Vector3(s * (0.02f + f * 0.012f), -0.22f - f * 0.03f, (f - 1.5f) * 0.03f), 0.007f, "finger", blk);
            }
        }

        // ================= Собака Подвала =================
        static void BuildHoundV2(Ent e)
        {
            var T = e.root.transform; var skin = BrGfx.Surf(BrMon.SkinTex(), new Color(0.95f, 0.88f, 0.84f), 0.4f); var tooth = BrGfx.Surf(null, new Color(0.9f, 0.88f, 0.75f), 0.5f); var pale = Glow(new Color(0.95f, 0.97f, 1f), 2.4f); var dark = BrGfx.Surf(null, new Color(0.03f, 0.02f, 0.02f), 0.2f);
            var hips = Jt(T, "hips", new Vector3(0, 0.62f, 0)); e.j[0] = hips;
            MeshAt(hips, BrBody.Tube("uchest", 1), new Vector3(0, 0.07f, 0.28f), new Vector3(0.42f, 0.5f, 0.46f), skin, new Vector3(90, 0, 0));    // грудная клетка с рёбрами
            MeshAt(hips, BrBody.Tube("waist", 1), new Vector3(0, 0.03f, -0.3f), new Vector3(0.36f, 0.34f, 0.4f), skin, new Vector3(90, 0, 0));      // подвздошье
            Blob(hips, new Vector3(0, 0.02f, -0.62f), new Vector3(0.32f, 0.3f, 0.3f), skin, 5);
            for (int i = 0; i < 10; i++) Blob(hips, new Vector3(0, 0.27f - 0.015f * Mathf.Abs(i - 5), -0.55f + i * 0.12f), new Vector3(0.05f, 0.06f, 0.07f), skin, 6 + i);   // позвонки
            var neck = Jt(hips, "neck", new Vector3(0, 0.15f, 0.5f)); e.j[1] = neck; Limb(neck, new Vector3(0, 0.0f, -0.12f), new Vector3(0, 0.1f, 0.32f), 0.085f, "neck", skin, 1f);
            var head = Jt(neck, "head", new Vector3(0, -0.04f, 0.3f)); e.j[2] = head; var jaw = Jt(head, "jaw", Vector3.zero); e.j[3] = jaw;
            SculptHead(head, jaw, new Vector3(2.2f, 2.3f, 3.0f), skin, tooth, pale, dark, true);
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? 0.5f : -0.45f; var up = Jt(hips, "leg" + i, new Vector3(sx * 0.19f, 0.0f, sz)); e.j[4 + i * 2] = up;
                Limb(up, Vector3.zero, new Vector3(0, -0.34f, -0.04f), 0.065f, "thigh", skin); Blob(up, Vector3.zero, new Vector3(0.15f, 0.17f, 0.17f), skin, 20 + i);
                var lo = Jt(up, "lo", new Vector3(0, -0.34f, -0.04f)); e.j[5 + i * 2] = lo; Limb(lo, Vector3.zero, new Vector3(0, -0.27f, 0.03f), 0.04f, "shin", skin); Blob(lo, Vector3.zero, new Vector3(0.09f, 0.1f, 0.1f), skin, 30 + i);
                Blob(lo, new Vector3(0, -0.28f, 0.07f), new Vector3(0.09f, 0.05f, 0.2f), skin, 40 + i);
                for (int c = 0; c < 4; c++) Limb(lo, new Vector3((c - 1.5f) * 0.02f, -0.28f, 0.14f), new Vector3((c - 1.5f) * 0.026f, -0.295f, 0.27f), 0.008f, "finger", tooth);
            }
        }

        // ================= Гость вечеринки =================
        static void BuildPartyV2(Ent e)
        {
            var T = e.root.transform; Color sk = Color.Lerp(new Color(0.78f, 0.64f, 0.3f), new Color(0.86f, 0.74f, 0.4f), (float)rnd.NextDouble()); var skin = BrGfx.Surf(BrMon.SkinTex(), sk, 0.35f); var red = BrGfx.Surf(null, new Color(0.45f, 0.03f, 0.03f), 0.5f); var dark = BrGfx.Surf(null, new Color(0.02f, 0.01f, 0.01f), 0.2f);
            var tooth = BrGfx.Surf(null, new Color(0.9f, 0.88f, 0.7f), 0.4f); var balloon = BrGfx.Surf(null, new Color(0.9f, 0.05f, 0.05f), 0.9f, 0f, new Color(0.25f, 0f, 0f)); var hatM = BrGfx.Surf(null, Color.HSVToRGB((float)rnd.NextDouble(), 0.8f, 0.95f), 0.3f);
            var cloth = BrGfx.Surf(null, Color.HSVToRGB((float)rnd.NextDouble(), 0.45f, 0.6f), 0.15f); var eyeM = Glow(new Color(1f, 0.9f, 0.7f), 1.4f);
            float hs = 0.88f + (float)rnd.NextDouble() * 0.14f; T.localScale = Vector3.one * hs;
            var hips = Jt(T, "hips", new Vector3(0, 0.95f, 0)); e.j[0] = hips; MeshAt(hips, BrBody.Tube("waist", 1), Vector3.zero, new Vector3(0.3f, 0.1f, 0.2f), cloth);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f; var l = Jt(hips, "l" + i, new Vector3(s * 0.1f, 0, 0)); e.j[1 + i] = l;
                Limb(l, Vector3.zero, new Vector3(0, -0.47f, 0.01f), 0.075f, "thigh", cloth); Limb(l, new Vector3(0, -0.47f, 0.01f), new Vector3(0, -0.9f, 0f), 0.055f, "shin", skin); Part(l, PrimitiveType.Cube, new Vector3(0, -0.93f, 0.06f), new Vector3(0.1f, 0.05f, 0.24f), dark);
            }
            var chest = Jt(hips, "chest", new Vector3(0, 0.05f, 0)); e.j[3] = chest; MeshAt(chest, BrBody.Tube("uchest", 1), new Vector3(0, 0.4f, 0), new Vector3(0.38f, 0.42f, 0.24f), cloth); Part(chest, PrimitiveType.Cube, new Vector3(0, 0.42f, 0.115f), new Vector3(0.2f, 0.34f, 0.01f), red);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f; var a = Jt(chest, "a" + i, new Vector3(s * 0.22f, 0.7f, 0)); e.j[4 + i] = a; Limb(a, Vector3.zero, new Vector3(0, -0.42f, 0.02f), 0.05f, "uarm", cloth); Limb(a, new Vector3(0, -0.42f, 0.02f), new Vector3(0, -0.84f, 0.06f), 0.04f, "farm", skin);
                Blob(a, new Vector3(0, -0.88f, 0.06f), new Vector3(0.085f, 0.13f, 0.05f), skin, 50 + i); for (int f = 0; f < 4; f++) Limb(a, new Vector3((f - 1.5f) * 0.017f, -0.92f, 0.06f), new Vector3((f - 1.5f) * 0.02f, -1.05f - (f == 1 || f == 2 ? 0.02f : 0f), 0.07f), 0.007f, "finger", skin);
            }
            MeshAt(chest, BrBody.Tube("neck", 1), new Vector3(0, 0.82f, 0), new Vector3(0.1f, 0.08f, 0.1f), skin);
            var head = Jt(chest, "head", new Vector3(0, 0.88f, 0)); e.j[6] = head; var jaw = Jt(head, "jaw", Vector3.zero);
            SculptHead(head, jaw, new Vector3(1.9f, 1.95f, 1.9f), skin, tooth, eyeM, dark, true);
            MeshAt(head, BrBody.Tube("neck", -1), new Vector3(0, 0.37f, -0.02f), new Vector3(0.26f, 0.2f, 0.26f), hatM); Blob(head, new Vector3(0, 0.58f, -0.02f), new Vector3(0.07f, 0.07f, 0.07f), hatM, 9);
            var bal = Jt(e.j[5], "balloon", new Vector3(0, -0.85f, 0)); e.j[7] = bal; Part(bal, PrimitiveType.Cube, new Vector3(0, 0.75f, 0), new Vector3(0.008f, 1.5f, 0.008f), dark); Blob(bal, new Vector3(0, 1.62f, 0), new Vector3(0.38f, 0.46f, 0.38f), balloon, 11, Vector3.zero);
        }

        // ================= Мотылёк-Смерть =================
        static Texture2D mothTex;
        static Texture2D MothTex()
        {
            if (mothTex == null) mothTex = BrGfx.MakeTex(128, 128, false, (u, v) =>
            {
                float n = 0.55f + 0.45f * Mathf.PerlinNoise(u * 14f, v * 14f); float band = Mathf.Abs(Mathf.Sin(u * 9f + Mathf.PerlinNoise(u * 3f, v * 3f) * 3f)) * 0.3f;
                Color c = Color.Lerp(new Color(0.42f, 0.34f, 0.24f), new Color(0.82f, 0.74f, 0.55f), Mathf.Clamp01(u * 1.1f + band - 0.1f)); float ed = Mathf.Abs(v - 0.5f) * 2f; c = Color.Lerp(c, new Color(0.18f, 0.12f, 0.08f), Mathf.Clamp01((ed - 0.75f) * 3f + (u - 0.85f) * 2f));
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.68f, 0.5f)); if (d < 0.13f) c = Color.Lerp(new Color(0.08f, 0.05f, 0.04f), new Color(0.95f, 0.85f, 0.4f), Mathf.Clamp01((0.13f - d) * 14f * (d < 0.075f ? 0f : 1f)) + (d < 0.05f ? 0f : 0f)); if (d < 0.05f) c = new Color(0.04f, 0.03f, 0.03f);
                c.r *= n; c.g *= n; c.b *= n; c.a = 1f; return c;
            });
            return mothTex;
        }
        static void BuildMothV2(Ent e)
        {
            var T = e.root.transform; var wing = TwoSided(BrGfx.Surf(MothTex(), Color.white, 0.2f)); var body = BrGfx.Surf(null, new Color(0.34f, 0.28f, 0.22f), 0.2f); var eye = Glow(new Color(1f, 0.2f, 0.1f), 2.2f);
            var b = Jt(T, "body", new Vector3(0, 1.8f, 0)); e.j[0] = b;
            MeshAt(b, BrBody.Tube("uchest", 1), new Vector3(0, 0, -0.02f), new Vector3(0.15f, 0.18f, 0.15f), body, new Vector3(90, 0, 0)); MeshAt(b, BrBody.Tube("waist", 1), new Vector3(0, 0, -0.3f), new Vector3(0.11f, 0.17f, 0.11f), body, new Vector3(90, 0, 0));
            Blob(b, new Vector3(0, 0.02f, 0.22f), new Vector3(0.13f, 0.12f, 0.12f), body, 3);
            foreach (float sx in new[] { -1f, 1f })
            {
                Blob(b, new Vector3(sx * 0.045f, 0.045f, 0.275f), new Vector3(0.05f, 0.05f, 0.035f), eye, 4); Limb(b, new Vector3(sx * 0.03f, 0.07f, 0.27f), new Vector3(sx * 0.14f, 0.2f, 0.38f), 0.008f, "finger", body); Limb(b, new Vector3(sx * 0.14f, 0.2f, 0.38f), new Vector3(sx * 0.2f, 0.14f, 0.45f), 0.006f, "finger", body);
            }
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f; var w = Jt(b, "w" + i, new Vector3(s * 0.06f, 0.06f, 0.02f)); e.j[1 + i] = w;
                MeshAt(w, OrgMesh.Wing(0.62f, 0.5f, 0.2f, "f"), Vector3.zero, new Vector3(s, 1f, 1f), wing, new Vector3(0, 0, 0)); MeshAt(w, OrgMesh.Wing(0.42f, 0.38f, 0.1f, "h"), new Vector3(0, -0.005f, -0.16f), new Vector3(s, 1f, 1f), wing, new Vector3(0, s * -14f, 0));
            }
        }
    }
}
