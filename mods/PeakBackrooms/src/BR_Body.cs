// Процедурные органические меши для монстра Backrooms: трубки с профилем (мышцы, рёбра, позвоночник, пальцы) и скульптурная голова
// (глазницы, скулы, брови, нос, широкая линия рта, подбородок) с отдельной нижней челюстью.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static class BrBody
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        static float Prof(string n, float u)
        {
            switch (n)
            {
                case "thigh": return Mathf.Lerp(1.0f, 0.6f, u) * (1f + 0.12f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.2f)));
                case "shin": return (u < 0.08f ? 0.78f : 0.7f) + 0.2f * Mathf.Exp(-Mathf.Pow((u - 0.28f) / 0.2f, 2f)) - 0.28f * u;
                case "uarm": return Mathf.Lerp(1.0f, 0.78f, u) * (1f + 0.08f * Mathf.Sin(Mathf.PI * u));
                case "farm": return Mathf.Lerp(0.88f, 0.58f, u) + 0.14f * Mathf.Exp(-Mathf.Pow((u - 0.22f) / 0.16f, 2f));
                case "neck": return Mathf.Lerp(1.2f, 0.85f, u) * (1f + 0.07f * Mathf.Sin(u * 17f));
                case "waist": return 0.9f - 0.16f * Mathf.Sin(Mathf.PI * u) + 0.04f * u;
                case "lchest": return 0.92f + 0.1f * Mathf.Sin(Mathf.PI * u * 0.9f);
                case "uchest": return 1.0f + 0.12f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.1f)) + 0.05f * u;
                case "finger": return Mathf.Lerp(1.0f, 0.42f, u * u);
                case "strand": return Mathf.Lerp(1.0f, 0.3f, u);
                default: return 1f;
            }
        }

        public static Mesh Tube(string name, int dir)
        {
            string key = name + dir; Mesh cm; if (cache.TryGetValue(key, out cm) && cm != null) return cm;
            const int RINGS = 22, SIDES = 16; bool torso = name == "waist" || name == "lchest" || name == "uchest"; float ribs = (name == "lchest" || name == "uchest") ? 0.06f : 0f, ridge = torso ? 0.07f : 0f; float noise = name == "strand" ? 0f : 0.045f; int seed = name.GetHashCode() & 255;
            var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var tr = new List<int>();
            for (int r = 0; r <= RINGS; r++)
            {
                float u = r / (float)RINGS, y = dir < 0 ? 1f - 2f * u : -1f + 2f * u;
                for (int s = 0; s <= SIDES; s++)
                {
                    float th = s / (float)SIDES * Mathf.PI * 2f; float k = Prof(name, u);
                    float front = 0.4f + 0.6f * Mathf.Max(0f, Mathf.Cos(th)); k *= 1f + ribs * Mathf.Sin(u * Mathf.PI * 7f) * front;
                    float back = Mathf.Exp(-Mathf.Pow(Mathf.DeltaAngle(th * Mathf.Rad2Deg, 180f) * Mathf.Deg2Rad / 0.2f, 2f)); k += ridge * back * (0.5f + 0.5f * Mathf.Cos(u * Mathf.PI * 2f * 4f));
                    k *= 1f + noise * (Mathf.PerlinNoise(th * 1.1f + seed, u * 5f + seed * 0.3f) - 0.5f) * 2f;
                    float rad = 0.5f * k; vs.Add(new Vector3(Mathf.Sin(th) * rad, y, Mathf.Cos(th) * rad)); uvs.Add(new Vector2(s / (float)SIDES, u));
                }
            }
            for (int r = 0; r < RINGS; r++) for (int s = 0; s < SIDES; s++)
                {
                    int a = r * (SIDES + 1) + s, b = a + SIDES + 1, c = b + 1, d = a + 1;
                    Vector3 cen = (vs[a] + vs[b] + vs[c] + vs[d]) * 0.25f; Vector3 want = new Vector3(cen.x, 0f, cen.z);
                    Tri(vs, tr, a, b, c, want); Tri(vs, tr, a, c, d, want);
                }
            int topC = vs.Count; vs.Add(new Vector3(0f, 1f, 0f)); uvs.Add(new Vector2(0.5f, 1f)); int botC = vs.Count; vs.Add(new Vector3(0f, -1f, 0f)); uvs.Add(new Vector2(0.5f, 0f));
            int rt = dir < 0 ? 0 : RINGS, rb = dir < 0 ? RINGS : 0;
            for (int s = 0; s < SIDES; s++) { Tri(vs, tr, topC, rt * (SIDES + 1) + s, rt * (SIDES + 1) + s + 1, Vector3.up); Tri(vs, tr, botC, rb * (SIDES + 1) + s, rb * (SIDES + 1) + s + 1, Vector3.down); }
            var m = new Mesh { name = "pf_tube_" + key }; m.SetVertices(vs); m.SetUVs(0, uvs); m.SetTriangles(tr, 0); m.RecalculateNormals();
            var ns = m.normals; for (int r = 0; r <= RINGS; r++) { int i0 = r * (SIDES + 1), i1 = i0 + SIDES; Vector3 av = (ns[i0] + ns[i1]).normalized; ns[i0] = av; ns[i1] = av; } m.normals = ns;
            m.RecalculateBounds(); cache[key] = m; return m;
        }

        static void Tri(List<Vector3> vs, List<int> tr, int a, int b, int c, Vector3 want)
        {
            Vector3 n = Vector3.Cross(vs[b] - vs[a], vs[c] - vs[a]);
            if (Vector3.Dot(n, want) >= 0f) { tr.Add(a); tr.Add(b); tr.Add(c); } else { tr.Add(a); tr.Add(c); tr.Add(b); }
        }
        // Unity (левая система): (a,b,c) лицевой, если Cross(b-a,c-a) смотрит наружу

        // ======================= голова =======================
        static float G(Vector3 d, Vector3 c, float w) { float a = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, c.normalized), -1f, 1f)); return Mathf.Exp(-(a * a) / (w * w)); }

        public static readonly Vector3 HeadCenter = new Vector3(0f, 0.125f, 0.0f);
        public static readonly Vector3 JawJoint = new Vector3(0f, 0.062f, -0.012f);

        static Vector3 Sculpt(Vector3 d)
        {
            float k = 1f;
            foreach (float sx in new[] { -1f, 1f })
            {
                k -= 0.34f * G(d, new Vector3(sx * 0.36f, 0.13f, 0.92f), 0.27f);          // глазницы
                k += 0.08f * G(d, new Vector3(sx * 0.3f, 0.42f, 0.86f), 0.22f);           // брови
                k -= 0.17f * G(d, new Vector3(sx * 0.52f, -0.2f, 0.82f), 0.34f);          // впалые щёки
                k -= 0.12f * G(d, new Vector3(sx * 0.92f, 0.22f, 0.3f), 0.42f);           // виски
                k -= 0.06f * G(d, new Vector3(sx * 0.08f, -0.17f, 0.98f), 0.07f);         // ноздри
            }
            k += 0.13f * G(d, new Vector3(0f, -0.03f, 1f), 0.12f);                        // нос
            k += 0.1f * G(d, new Vector3(0f, -0.86f, 0.5f), 0.3f);                         // подбородок
            if (d.z > 0.15f)
            {
                float ym = -0.4f + 0.55f * d.x * d.x; float gr = Mathf.Exp(-Mathf.Pow((d.y - ym) / 0.05f, 2f)) * Mathf.Clamp01((0.72f - Mathf.Abs(d.x)) / 0.2f);
                k -= 0.14f * gr;                                                          // огромная линия рта
            }
            k += 0.03f * Mathf.Clamp01(-d.z);
            float yy = d.y * 0.145f * k * (d.y < 0f ? 1.25f : 1f);
            return HeadCenter + new Vector3(d.x * 0.086f * k, yy, d.z * 0.108f * k);
        }

        public static void Head(out Mesh upper, out Mesh lower)
        {
            const int LAT = 44, LON = 64; var vs = new List<Vector3>(); var uvs = new List<Vector2>();
            for (int i = 0; i <= LAT; i++) for (int j = 0; j <= LON; j++)
                {
                    float ph = i / (float)LAT * Mathf.PI, la = j / (float)LON * Mathf.PI * 2f; Vector3 d = new Vector3(Mathf.Sin(ph) * Mathf.Sin(la), Mathf.Cos(ph), Mathf.Sin(ph) * Mathf.Cos(la)); vs.Add(Sculpt(d)); uvs.Add(new Vector2(j / (float)LON, 1f - i / (float)LAT));
                }
            var tu = new List<int>(); var tl = new List<int>();
            for (int i = 0; i < LAT; i++) for (int j = 0; j < LON; j++)
                {
                    int a = i * (LON + 1) + j, b = a + LON + 1, c = b + 1, d = a + 1;
                    foreach (var t in new[] { new[] { a, b, c }, new[] { a, c, d } })
                    {
                        Vector3 cen = (vs[t[0]] + vs[t[1]] + vs[t[2]]) / 3f; var tri = cen.y < JawJoint.y - 0.004f ? tl : tu; Vector3 n = Vector3.Cross(vs[t[1]] - vs[t[0]], vs[t[2]] - vs[t[0]]); Vector3 want = cen - HeadCenter;
                        if (Vector3.Dot(n, want) >= 0f) { tri.Add(t[0]); tri.Add(t[1]); tri.Add(t[2]); } else { tri.Add(t[0]); tri.Add(t[2]); tri.Add(t[1]); }
                    }
                }
            upper = new Mesh { name = "pf_head_up" }; upper.SetVertices(vs); upper.SetUVs(0, uvs); upper.SetTriangles(tu, 0); upper.RecalculateNormals(); upper.RecalculateBounds();
            var vj = new List<Vector3>(vs.Count); foreach (var v in vs) vj.Add(v - JawJoint);
            lower = new Mesh { name = "pf_head_low" }; lower.SetVertices(vj); lower.SetUVs(0, uvs); lower.SetTriangles(tl, 0); lower.RecalculateNormals(); lower.RecalculateBounds();
        }
    }
}
