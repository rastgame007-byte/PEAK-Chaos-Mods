// Монстр Backrooms: процедурный скелет с плавной анимацией (стойка → сгорбленный бег → на четвереньках), поведение «подглядывает → крадётся → охотится»
// и страшная анимация поимки. Чем дольше игрок в лабиринте, тем больше ярость: рост, длина рук и шеи, наклон, скорость.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal sealed class BrMon
    {
        public GameObject root; public Transform rt; public Transform hips, sp1, sp2, chest, neck, head, jaw;
        public Transform[] ua = new Transform[2], fa = new Transform[2], hd = new Transform[2], th = new Transform[2], sn = new Transform[2], ft = new Transform[2];
        readonly Transform[,,] fing = new Transform[2, 4, 2]; Transform[] rags = new Transform[7], shirt = new Transform[6];
        sealed class Sg { public Transform vis; public float dir, rad, wx = 1f; public void Apply(float len, float thick) { vis.localPosition = new Vector3(0, dir * len * 0.5f, 0); vis.localScale = new Vector3(rad * 2f * thick * wx, len * 0.5f, rad * 2f * thick); } }
        Sg[] sgTh = new Sg[2], sgSn = new Sg[2], sgUa = new Sg[2], sgFa = new Sg[2], sgSp = new Sg[3]; Sg sgNeck; Sg[] palm = new Sg[2]; Sg[,,] sgFing = new Sg[2, 4, 2];
        public Material eyeMat; public List<Renderer> rends = new List<Renderer>();
        public float phase, cb, hunch, hipsY = 0.9f, mw, t, headYaw, catchT, lastStepSign = 1f; public bool catching; public Vector3 twitch; float nextTw, jawV;
        public float stretch;      // 0..1 — насколько искажён (для тестов)
        public float reach, headTwist, headRate = -1f, jawForce = -1f, rollForce, eyeOff;   // управление сценой поимки
        public AudioSource breath;

        static float Sm(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3f - 2f * x); }
        static float Rad(float d) { return d * Mathf.Deg2Rad; }

        static Texture2D skinTex, clothTex, lipTex;
        public static Texture2D SkinTex()
        {
            if (skinTex == null) skinTex = BrGfx.MakeTex(256, 256, true, (u, v) =>
            {
                float vein = Mathf.Abs(Mathf.PerlinNoise(u * 11f, v * 11f) - 0.5f); float vein2 = Mathf.Abs(Mathf.PerlinNoise(u * 23f + 5f, v * 17f) - 0.5f);
                float blot = Mathf.PerlinNoise(u * 3f + 4f, v * 3f + 1f); float dirt = Mathf.PerlinNoise(u * 7f + 9f, v * 5f);
                Color c = new Color(0.66f, 0.64f, 0.6f);
                c = Color.Lerp(c, new Color(0.38f, 0.42f, 0.5f), vein < 0.028f ? 0.75f : (vein < 0.05f ? 0.25f : 0f));
                c = Color.Lerp(c, new Color(0.4f, 0.44f, 0.5f), vein2 < 0.018f ? 0.5f : 0f);
                c = Color.Lerp(c, new Color(0.45f, 0.3f, 0.34f), Mathf.Clamp01((blot - 0.58f) * 3f) * 0.7f);
                c = Color.Lerp(c, new Color(0.3f, 0.28f, 0.24f), Mathf.Clamp01((dirt - 0.62f) * 3f) * 0.55f);
                float pores = 0.92f + 0.08f * Mathf.PerlinNoise(u * 140f, v * 140f); c.r *= pores; c.g *= pores; c.b *= pores; c.a = 1f; return c;
            });
            return skinTex;
        }
        static Texture2D ClothTex()
        {
            if (clothTex == null) clothTex = BrGfx.MakeTex(64, 64, true, (u, v) => { float n = 0.6f + 0.6f * Mathf.PerlinNoise(u * 12f, v * 12f); float th = (Mathf.Repeat(u * 24f, 1f) < 0.5f ? 1f : 0.85f); float st = Mathf.PerlinNoise(u * 3f + 8f, v * 3f) > 0.72f ? 0.5f : 1f; return new Color(0.1f * n * th * st, 0.09f * n * th * st, 0.085f * n * th * st, 1f); });
            return clothTex;
        }

        Transform J(Transform par, string name, Vector3 lp) { var g = new GameObject(name); g.transform.SetParent(par, false); g.transform.localPosition = lp; return g.transform; }
        Sg Cap(Transform joint, float dir, float rad, Material m, float wx = 1f, string prof = "thigh")
        {
            var g = new GameObject("BR_mon"); g.transform.SetParent(joint, false); g.AddComponent<MeshFilter>().sharedMesh = BrBody.Tube(prof, dir < 0 ? -1 : 1);
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; rends.Add(r); return new Sg { vis = g.transform, dir = dir, rad = rad, wx = wx };
        }
        GameObject Prim(PrimitiveType pt, Transform par, Vector3 lp, Vector3 ls, Material m, Vector3 euler = default(Vector3))
        {
            var g = GameObject.CreatePrimitive(pt); g.name = "BR_mon"; var c = g.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c);
            g.transform.SetParent(par, false); g.transform.localPosition = lp; g.transform.localScale = ls; g.transform.localEulerAngles = euler; var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m; rends.Add(r); return g;
        }
        GameObject MeshPart(Mesh mesh, Transform par, Vector3 lp, Material m)
        {
            var g = new GameObject("BR_mon"); g.transform.SetParent(par, false); g.transform.localPosition = lp; g.AddComponent<MeshFilter>().sharedMesh = mesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; rends.Add(r); return g;
        }
        Transform[] hair = new Transform[16];

        public BrMon(Vector3 pos)
        {
            root = new GameObject("BR_monster"); root.transform.position = pos; var R = root.transform; rt = R;
            var skin = BrGfx.Surf(SkinTex(), Color.white, 0.38f); var cloth = BrGfx.Surf(ClothTex(), Color.white, 0.08f); var dark = BrGfx.Surf(null, new Color(0.01f, 0.01f, 0.012f), 0.05f);
            var gum = BrGfx.Surf(null, new Color(0.18f, 0.03f, 0.04f), 0.5f); var tooth = BrGfx.Surf(null, new Color(0.88f, 0.84f, 0.68f), 0.5f); var hairM = BrGfx.Surf(null, new Color(0.015f, 0.014f, 0.016f), 0.35f);
            eyeMat = BrGfx.Surf(null, new Color(0.95f, 0.95f, 0.9f), 0.4f, 0f, new Color(2.5f, 2.5f, 2.3f));
            hips = J(R, "hips", new Vector3(0, 0.9f, 0)); Prim(PrimitiveType.Sphere, hips, new Vector3(0, 0.0f, 0), new Vector3(0.3f, 0.2f, 0.19f), skin);
            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? -1f : 1f;
                th[i] = J(hips, "th" + i, new Vector3(sx * 0.1f, 0, 0)); sgTh[i] = Cap(th[i], -1f, 0.07f, skin, 1f, "thigh"); Prim(PrimitiveType.Sphere, th[i], Vector3.zero, new Vector3(0.14f, 0.14f, 0.14f), skin);
                sn[i] = J(th[i], "sn" + i, new Vector3(0, -0.5f, 0)); sgSn[i] = Cap(sn[i], -1f, 0.052f, skin, 1f, "shin"); Prim(PrimitiveType.Sphere, sn[i], Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), skin);
                ft[i] = J(sn[i], "ft" + i, new Vector3(0, -0.5f, 0)); Prim(PrimitiveType.Cube, ft[i], new Vector3(0, -0.03f, 0.075f), new Vector3(0.08f, 0.05f, 0.25f), skin);
                for (int j = 0; j < 4; j++) Prim(PrimitiveType.Sphere, ft[i], new Vector3((j - 1.5f) * 0.02f, -0.045f, 0.2f - Mathf.Abs(j - 1.5f) * 0.012f), new Vector3(0.014f, 0.014f, 0.05f), tooth);
            }
            sp1 = J(hips, "sp1", new Vector3(0, 0.04f, 0)); sgSp[0] = Cap(sp1, 1f, 0.12f, skin, 1.2f, "waist");
            sp2 = J(sp1, "sp2", new Vector3(0, 0.2f, 0)); sgSp[1] = Cap(sp2, 1f, 0.135f, skin, 1.28f, "lchest");
            chest = J(sp2, "chest", new Vector3(0, 0.2f, 0)); sgSp[2] = Cap(chest, 1f, 0.145f, skin, 1.4f, "uchest");
            // ключицы и лохмотья рубашки
            foreach (float sx in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Capsule, chest, new Vector3(sx * 0.1f, 0.185f, 0.075f), new Vector3(0.022f, 0.1f, 0.022f), skin, new Vector3(0f, 0f, -sx * 82f));
                Prim(PrimitiveType.Sphere, chest, new Vector3(sx * 0.19f, 0.17f, 0f), new Vector3(0.1f, 0.1f, 0.1f), skin);
            }
            for (int i = 0; i < 6; i++)
            {
                float ang = (i / 6f) * Mathf.PI * 2f; var sj = J(chest, "sh" + i, new Vector3(Mathf.Sin(ang) * 0.16f, 0.12f, Mathf.Cos(ang) * 0.1f)); Prim(PrimitiveType.Cube, sj.transform, new Vector3(0, -0.16f - (i % 2) * 0.05f, 0), new Vector3(0.07f, 0.32f + (i % 2) * 0.1f, 0.01f), cloth);
                shirt[i] = sj;
            }
            neck = J(chest, "neck", new Vector3(0, 0.2f, 0)); sgNeck = Cap(neck, 1f, 0.04f, skin, 1f, "neck"); head = J(neck, "head", new Vector3(0, 0.1f, 0));
            // ---- голова: скульптура ----
            Mesh hu, hl; BrBody.Head(out hu, out hl);
            MeshPart(hu, head, Vector3.zero, skin);
            jaw = J(head, "jaw", BrBody.JawJoint); MeshPart(hl, jaw, Vector3.zero, skin);
            Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.06f, 0.035f), new Vector3(0.115f, 0.05f, 0.1f), gum);       // пасть внутри
            foreach (float sx in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, head, new Vector3(sx * 0.03f, 0.138f, 0.062f), new Vector3(0.042f, 0.038f, 0.04f), dark);
                Prim(PrimitiveType.Sphere, head, new Vector3(sx * 0.03f, 0.138f, 0.078f), new Vector3(0.011f, 0.011f, 0.011f), eyeMat);
                Prim(PrimitiveType.Cube, head, new Vector3(sx * 0.04f, 0.168f, 0.088f), new Vector3(0.055f, 0.006f, 0.014f), dark, new Vector3(0, 0, -sx * 20f));   // бровь
            }
            var rn = new System.Random(77);
            for (int i = 0; i < 17; i++) { float x = (i - 8) * 0.0063f; Prim(PrimitiveType.Cube, head, new Vector3(x, 0.054f, 0.103f - x * x * 3.5f), new Vector3(0.0045f, 0.014f + (float)rn.NextDouble() * 0.012f, 0.0045f), tooth); }
            for (int i = 0; i < 15; i++) { float x = (i - 7) * 0.0068f; Prim(PrimitiveType.Cube, jaw, new Vector3(x, 0.008f, 0.093f - x * x * 3.5f), new Vector3(0.0045f, 0.016f + (float)rn.NextDouble() * 0.014f, 0.0045f), tooth); }
            for (int i = 0; i < hair.Length; i++)
            {
                float ang = 110f + i / (float)(hair.Length - 1) * 140f; var hj = J(head, "hair" + i, new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * 0.078f, 0.2f - 0.02f * Mathf.Abs(Mathf.Cos(ang * Mathf.Deg2Rad)), Mathf.Cos(ang * Mathf.Deg2Rad) * 0.09f));
                hj.localRotation = Quaternion.Euler(8f, ang, 0f); hair[i] = hj; float hl2 = 0.3f + (float)rn.NextDouble() * 0.35f; var hg = new GameObject("BR_mon"); hg.transform.SetParent(hj, false); hg.transform.localPosition = new Vector3(0, -hl2 * 0.5f, 0);
                hg.AddComponent<MeshFilter>().sharedMesh = BrBody.Tube("strand", -1); var hr = hg.AddComponent<MeshRenderer>(); hr.sharedMaterial = hairM; rends.Add(hr); hg.transform.localScale = new Vector3(0.016f, hl2 * 0.5f, 0.016f); hg.transform.localPosition = new Vector3(0, -hl2 * 0.5f, 0);
            }
            // ---- руки ----
            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? -1f : 1f;
                ua[i] = J(chest, "ua" + i, new Vector3(sx * 0.2f, 0.17f, 0)); sgUa[i] = Cap(ua[i], -1f, 0.046f, skin, 1f, "uarm"); Prim(PrimitiveType.Sphere, ua[i], Vector3.zero, new Vector3(0.095f, 0.095f, 0.095f), skin);
                fa[i] = J(ua[i], "fa" + i, new Vector3(0, -0.33f, 0)); sgFa[i] = Cap(fa[i], -1f, 0.037f, skin, 1f, "farm"); Prim(PrimitiveType.Sphere, fa[i], Vector3.zero, new Vector3(0.075f, 0.075f, 0.075f), skin);
                hd[i] = J(fa[i], "hd" + i, new Vector3(0, -0.33f, 0));
                var pg = Prim(PrimitiveType.Sphere, hd[i], Vector3.zero, new Vector3(0.07f, 0.1f, 0.03f), skin); palm[i] = new Sg { vis = pg.transform, dir = -1f };
                for (int j = 0; j < 4; j++)
                {
                    var f1 = J(hd[i], "f1_" + j, new Vector3((j - 1.5f) * 0.02f, -0.14f, 0)); sgFing[i, j, 0] = Cap(f1, -1f, 0.0085f, skin, 1f, "finger"); fing[i, j, 0] = f1;
                    var f2 = J(f1, "f2_" + j, new Vector3(0, -0.06f, 0)); sgFing[i, j, 1] = Cap(f2, -1f, 0.0072f, skin, 1f, "finger"); fing[i, j, 1] = f2;
                }
            }
            for (int i = 0; i < rags.Length; i++)
            {
                float ang = i / (float)rags.Length * Mathf.PI * 2f; var rj = J(hips, "rag" + i, new Vector3(Mathf.Sin(ang) * 0.15f, 0.02f, Mathf.Cos(ang) * 0.1f)); rags[i] = rj;
                Prim(PrimitiveType.Cube, rj, new Vector3(0, -0.17f - (i % 3) * 0.04f, 0), new Vector3(0.07f, 0.34f + (i % 3) * 0.08f, 0.012f), cloth);
            }
            breath = root.AddComponent<AudioSource>(); breath.spatialBlend = 1f; breath.minDistance = 2f; breath.maxDistance = 38f; breath.rolloffMode = AudioRolloffMode.Linear; breath.loop = true; breath.playOnAwake = false;
            foreach (var r in rends) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            Pose(0.03f, 0f, 0f, 0f, 0f, Vector3.zero, false, 0f);
        }

        public Vector3 HeadPos() { return head.TransformPoint(new Vector3(0, 0.14f, 0.03f)); }

        // 2-звенный IK в сагиттальной плоскости. dx — вперёд, dy — вниз от сустава до цели. sign +1: колено вперёд (ноги), −1: локоть назад (руки).
        static void IK(float L1, float L2, float dx, float dy, float sign, out float a1, out float flex)
        {
            float d = Mathf.Min(Mathf.Sqrt(dx * dx + dy * dy), (L1 + L2) * 0.998f); d = Mathf.Max(d, Mathf.Abs(L1 - L2) + 0.02f);
            float bas = Mathf.Atan2(dx, dy); float ca = Mathf.Clamp((L1 * L1 + d * d - L2 * L2) / (2f * L1 * d), -1f, 1f); float A = Mathf.Acos(ca);
            float ci = Mathf.Clamp((L1 * L1 + L2 * L2 - d * d) / (2f * L1 * L2), -1f, 1f);
            a1 = (bas + sign * A) * Mathf.Rad2Deg; flex = (Mathf.PI - Mathf.Acos(ci)) * Mathf.Rad2Deg;
        }

        void Rot(Transform tr, Vector3 euler, float k) { tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.Euler(euler), k); }

        public void Pose(float dt, float speed, float anger, float crawlT, float hunchT, Vector3 lookAt, bool hasLook, float shout)
        {
            dt = Mathf.Max(dt, 0.0001f); t += dt; float a = catching ? 1f : anger; float st = Sm(0.12f, 0.95f, a); stretch = st; float kk = 1f - Mathf.Exp(-dt * 11f);
            float hs = Mathf.Lerp(1f, 1.4f, st), asx = Mathf.Lerp(1f, 2.1f, st), ns = Mathf.Lerp(1f, 2.8f, st), thin = Mathf.Lerp(1f, 0.62f, st);
            float Lth = 0.5f * hs, Lsn = 0.5f * hs, Lsp = 0.62f * hs, Ln = 0.1f * ns, Lu = 0.33f * asx, Lf = 0.33f * asx, Lh = 0.14f * asx;
            if (catching) { Ln *= 1.5f; }
            cb = Mathf.MoveTowards(cb, catching ? 0f : crawlT, dt * 0.7f); float cs = Sm(0f, 1f, cb);
            hunch = Mathf.MoveTowards(hunch, hunchT, dt * 38f);
            float P = Mathf.Lerp(hunch, 80f, cs), H = Mathf.Lerp(0f, 26f, cs);
            float spd = Mathf.Max(0f, speed); mw = Mathf.MoveTowards(mw, Mathf.Clamp01(spd / 0.9f), dt * 3f);
            float sn_ = Mathf.Clamp01(spd / Mathf.Lerp(5.5f, 9f, cs));
            if (spd > 0.1f) phase += dt * Mathf.PI * 2f * Mathf.Min(3.4f, 0.45f + 0.3f * spd);
            // подёргивания
            if (t > nextTw) { twitch = new Vector3(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(-25f, 25f)) * (0.4f + st); nextTw = t + UnityEngine.Random.Range(1.2f, 4f) / (0.5f + a); }
            twitch = Vector3.Lerp(twitch, Vector3.zero, 1f - Mathf.Exp(-dt * 5f));
            float breathe = Mathf.Sin(t * 1.6f);
            // высота бёдер и ноги (IK)
            float hyStand = (Lth + Lsn) * (0.965f - 0.06f * Sm(0f, 50f, hunch)), hyCrawl = (Lth + Lsn) * 0.56f + 0.06f; float hy = Mathf.Lerp(hyStand, hyCrawl, cs);
            hipsY = Mathf.Lerp(hipsY, hy + 0.012f * Mathf.Sin(phase * 2f) * mw, 1f - Mathf.Exp(-dt * 12f));
            float sh = Lsp * 0.5f * (Mathf.Cos(Rad(H)) + Mathf.Cos(Rad(P))) + 0.04f, shF = Lsp * 0.5f * (Mathf.Sin(Rad(H)) + Mathf.Sin(Rad(P)));   // плечи относительно таза: вверх, вперёд
            var thW = new float[2]; var kn = new float[2]; var uW = new float[2]; var el = new float[2];
            for (int i = 0; i < 2; i++)
            {
                float ph = phase + i * Mathf.PI, pa = ph + Mathf.PI;
                float strideL = Mathf.Lerp(0.14f, 0.4f, sn_) * hs; float lift = Mathf.Lerp(0.05f, 0.2f, sn_) * hs;
                float fdx = Mathf.Lerp(strideL * Mathf.Sin(ph) * mw, 0.14f * hs + 0.3f * Mathf.Sin(ph) * (mw * 0.8f + 0.1f), cs);
                float fdy = hipsY - (lift * Mathf.Max(0f, Mathf.Cos(ph)) * mw);
                float a1, fx; IK(Lth, Lsn, fdx, fdy, 1f, out a1, out fx); thW[i] = a1; kn[i] = fx;
                // руки: стоя — свободный размах, на четвереньках — IK до земли
                float uU = Mathf.Lerp(12f, 34f, sn_) * Mathf.Sin(pa) * mw + 5f + 3f * Mathf.Sin(t * 0.9f + i) + 2f * Mathf.Sin(t * 0.37f + i * 3f) * st; float eU = 14f + 26f * sn_ + 12f * Mathf.Sin(t * 1.3f + i * 2f) * st;
                float uC = uU, eC = eU;
                if (cs > 0.01f)
                {
                    float hx = shF + (Lu + Lf) * 0.2f + 0.34f * Mathf.Sin(pa) * (mw * 0.85f + 0.1f) * asx * 0.6f; float hlift = 0.18f * Mathf.Max(0f, Mathf.Cos(pa)) * mw;
                    float adx = hx - shF, ady = hipsY + sh - hlift - Lh * Mathf.Cos(Rad(78f)); float ua1, ef; IK(Lu, Lf, adx, ady, -1f, out ua1, out ef); uC = ua1; eC = ef;
                }
                uW[i] = Mathf.Lerp(uU, uC, cs); el[i] = Mathf.Lerp(eU, eC, cs);
            }
            if (reach > 0.01f) for (int i = 0; i < 2; i++) { uW[i] = Mathf.Lerp(uW[i], 72f + 10f * Mathf.Sin(t * 0.9f + i * 1.7f), reach); el[i] = Mathf.Lerp(el[i], 16f + 6f * Mathf.Sin(t * 1.3f + i), reach); }
            if (catching)
            {
                for (int i = 0; i < 2; i++) { uW[i] = 78f + 12f * Mathf.Sin(t * 23f + i * 1.9f); el[i] = 28f + 22f * Mathf.Sin(t * 17f + i); }
                P = 14f + 10f * Mathf.Sin(t * 9f);
            }
            // ---- применение ----
            float thick = thin;
            sgNeck.Apply(Ln, thick); sgSp[0].Apply(Lsp / 3f + 0.02f, thick); sgSp[1].Apply(Lsp / 3f + 0.02f, thick); sgSp[2].Apply(Lsp / 3f + 0.02f, thick);
            hips.localPosition = new Vector3(0, hipsY, -Mathf.Lerp(0f, 0.32f, cs));
            sp2.localPosition = new Vector3(0, Lsp / 3f, 0); chest.localPosition = new Vector3(0, Lsp / 3f, 0); neck.localPosition = new Vector3(0, Lsp / 3f, 0); head.localPosition = new Vector3(0, Ln, 0);
            float sway = Mathf.Sin(phase) * mw * (3f + 4f * st);
            Rot(hips, new Vector3(H, 0, sway * 0.5f), kk);
            float spine = P - H;
            Rot(sp1, new Vector3(spine * 0.3f + breathe * 0.8f, Mathf.Sin(phase + 1.2f) * 7f * mw, -sway * 0.6f), kk);
            Rot(sp2, new Vector3(spine * 0.35f + breathe * 0.6f, Mathf.Sin(phase + 1.2f) * 5f * mw, 0f), kk);
            Rot(chest, new Vector3(spine * 0.35f, 0f, 0f), kk);
            // шея и голова: смотрит на цель, подёргивается, иногда «сова»
            float yawT = 0f;
            if (hasLook)
            {
                Vector3 dirL = lookAt - head.position; dirL.y = 0f; Vector3 fw = root.transform.forward; fw.y = 0f;
                if (dirL.sqrMagnitude > 0.01f && fw.sqrMagnitude > 0.01f) yawT = Mathf.Clamp(Vector3.SignedAngle(fw, dirL, Vector3.up), -(100f + 70f * st), 100f + 70f * st);
            }
            headYaw = Mathf.MoveTowardsAngle(headYaw, yawT + headTwist, dt * (headRate > 0f ? headRate : 70f + 180f * st));
            float tilt = (6f + 16f * st) * Mathf.Sin(t * 0.4f) + 20f * st + twitch.z * 0.6f + rollForce;
            float lookPitch = hasLook ? Mathf.Clamp(Mathf.Atan2(head.position.y - lookAt.y, Mathf.Max(0.5f, Vector2.Distance(new Vector2(lookAt.x, lookAt.z), new Vector2(head.position.x, head.position.z)))) * Mathf.Rad2Deg, -25f, 25f) : 0f;
            Rot(neck, new Vector3(-0.55f * P - 8f * cs + twitch.x * 0.4f + (catching ? -12f : 0f), headYaw * 0.45f + twitch.y * 0.4f, tilt * 0.4f), kk);
            Rot(head, new Vector3(-0.42f * P + lookPitch * 0.5f + twitch.x * 0.6f + breathe * 0.6f, headYaw * 0.55f + twitch.y * 0.6f, tilt * 0.6f + (catching ? 25f * Mathf.Sin(t * 31f) : 0f)), kk);
            // челюсть
            float jt = Mathf.Lerp(0.04f, 0.4f, st) + shout * 0.65f + (catching ? 0.35f + 0.3f * Mathf.Abs(Mathf.Sin(t * 26f)) : 0f) + 0.15f * st * Mathf.Max(0f, Mathf.Sin(t * 14f)) * Mathf.Clamp01(Mathf.Abs(twitch.y) / 20f);
            if (jawForce >= 0f) jt = jawForce; jawV = Mathf.Lerp(jawV, Mathf.Clamp01(jt), 1f - Mathf.Exp(-dt * 16f)); jaw.localRotation = Quaternion.Euler(jawV * 52f, 0f, 0f);
            // ноги
            for (int i = 0; i < 2; i++)
            {
                sgTh[i].Apply(Lth, thick); sgSn[i].Apply(Lsn, thick); sn[i].localPosition = new Vector3(0, -Lth, 0); ft[i].localPosition = new Vector3(0, -Lsn, 0);
                float sx = i == 0 ? -1f : 1f;
                Rot(th[i], new Vector3(-thW[i] - H, 0f, sx * (3f + 11f * cs + 3f * st)), kk); Rot(sn[i], new Vector3(kn[i], 0f, 0f), kk); Rot(ft[i], new Vector3(thW[i] - kn[i] - H, 0f, 0f), kk);
                // руки
                sgUa[i].Apply(Lu, thick); sgFa[i].Apply(Lf, thick); fa[i].localPosition = new Vector3(0, -Lu, 0); hd[i].localPosition = new Vector3(0, -Lf, 0);
                ua[i].localPosition = new Vector3(sx * 0.2f * (1f + 0.15f * st), Lsp / 3f * 0.85f, 0);
                float handF = Mathf.Lerp(0f, 78f, cs);
                float spread = Mathf.Lerp(7f + 6f * st, 3f, cs) + (catching ? 28f : 0f) + 2f * Mathf.Sin(t * 0.8f + i);
                Rot(ua[i], new Vector3(-uW[i] - P, 0f, sx * spread), kk); Rot(fa[i], new Vector3(-el[i], 0f, 0f), kk); Rot(hd[i], new Vector3(-handF + uW[i] + el[i] + (catching ? 25f * Mathf.Sin(t * 29f + i) : 0f), 0f, sx * 4f), kk);
                palm[i].vis.localPosition = new Vector3(0, -Lh * 0.36f, 0); palm[i].vis.localScale = new Vector3(0.075f * thick, Lh * 0.72f, 0.026f);
                for (int j = 0; j < 4; j++)
                {
                    float f1L = (j == 1 || j == 2 ? 0.07f : 0.06f) * asx, f2L = f1L * 0.85f; fing[i, j, 0].localPosition = new Vector3((j - 1.5f) * 0.02f, -Lh * 0.9f, 0); fing[i, j, 1].localPosition = new Vector3(0, -f1L, 0);
                    sgFing[i, j, 0].Apply(f1L, 1f); sgFing[i, j, 1].Apply(f2L, 1f);
                    float curl = catching ? 5f + 10f * Mathf.Sin(t * 20f + j) : Mathf.Lerp(40f, 8f, cs) + 22f * Mathf.PerlinNoise(t * 1.4f + j * 3f + i * 7f, 0.5f) + 12f * st * Mathf.Sin(t * 5f + j * 1.3f);
                    Rot(fing[i, j, 0], new Vector3(-curl, 0f, (j - 1.5f) * (catching ? 14f : 4f)), kk); Rot(fing[i, j, 1], new Vector3(-curl * 1.2f, 0f, 0f), kk);
                }
            }
            for (int i = 0; i < rags.Length; i++) rags[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 1.7f + i) * 8f + spd * 4f, 0f, Mathf.Sin(t * 1.3f + i * 2f) * 7f);
            for (int i = 0; i < shirt.Length; i++) shirt[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 1.5f + i * 1.7f) * 6f + spd * 3f - P * 0.2f, i * 60f, Mathf.Sin(t * 1.2f + i) * 5f);
            for (int i = 0; i < hair.Length; i++) { float ang = 110f + i / (float)(hair.Length - 1) * 140f; hair[i].localRotation = Quaternion.Euler(10f + Mathf.Sin(t * 1.3f + i) * 6f + spd * 5f + (catching ? 40f : 0f), ang, Mathf.Sin(t * 1.1f + i * 2.1f) * 7f); }
            if (eyeMat != null) { try { eyeMat.SetColor("_EmissionColor", new Color(2.5f, 2.5f, 2.3f) * (0.7f + 1.8f * a) * (1f - eyeOff)); } catch { } }
        }

        public float MinY()
        {
            float m = 1e9f; foreach (var r in rends) if (r != null) m = Mathf.Min(m, r.bounds.min.y); return m;
        }
    }

    // ============================== звук ==============================
    internal static class BrSnd
    {
        static AudioClip scream, catchC, breath, step, heart, sting; static GameObject host; static AudioSource s2d, heartSrc;
        static AudioClip Make(string name, int rate, float len, Func<float, float, float> f)
        {
            int n = (int)(rate * len); var d = new float[n]; for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)rate, i / (float)n), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, rate, false); c.SetData(d, 0); return c;
        }
        static void Init()
        {
            if (host != null) return;
            host = new GameObject("BR_audio"); UnityEngine.Object.DontDestroyOnLoad(host); s2d = host.AddComponent<AudioSource>(); s2d.spatialBlend = 0f; s2d.playOnAwake = false;
            heartSrc = host.AddComponent<AudioSource>(); heartSrc.spatialBlend = 0f; heartSrc.loop = true; heartSrc.playOnAwake = false;
            var rn = new System.Random(3);
            float ph = 0f, ph2 = 0f;
            scream = Make("br_scream", 22050, 1.4f, (t, k) =>
            {
                float f = 520f + 700f * Mathf.Sin(Mathf.Clamp01(t / 0.6f) * Mathf.PI * 0.5f) - 380f * Mathf.Clamp01((t - 0.6f) / 0.8f) + 40f * Mathf.Sin(t * 41f); ph += f / 22050f; ph2 += f * 1.51f / 22050f;
                float saw = (ph % 1f) * 2f - 1f, saw2 = (ph2 % 1f) * 2f - 1f; float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-Mathf.Max(0f, t - 0.7f) * 2.6f);
                return (float)Math.Tanh((saw * 0.6f + saw2 * 0.4f + ((float)rn.NextDouble() - 0.5f) * 0.7f) * 2.2f) * env * 0.8f;
            });
            float pa = 0f, pb = 0f, pc = 0f;
            catchC = Make("br_catch", 22050, 2.6f, (t, k) =>
            {
                float f = 420f + 520f * Mathf.Sin(t * 5f) * Mathf.Exp(-t * 0.5f) + 380f * Mathf.Clamp01(1f - t / 0.4f); pa += f / 22050f; pb += f * 1.48f / 22050f; pc += (f * 0.5f + 33f * Mathf.Sin(t * 60f)) / 22050f;
                float env = Mathf.Clamp01(t / 0.015f) * (t < 2.0f ? 1f : Mathf.Clamp01((2.6f - t) / 0.6f));
                float boom = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 7f) * 1.4f; float noise = ((float)rn.NextDouble() - 0.5f) * (0.9f + 0.6f * Mathf.Sin(t * 70f));
                float s = ((pa % 1f) * 2f - 1f) * 0.5f + ((pb % 1f) * 2f - 1f) * 0.4f + ((pc % 1f) * 2f - 1f) * 0.4f + noise * 0.9f;
                return (float)Math.Tanh(s * 3.2f + boom) * env;
            });
            float lp = 0f;
            breath = Make("br_breath", 22050, 3f, (t, k) =>
            {
                lp = lp * 0.86f + ((float)rn.NextDouble() - 0.5f) * 0.14f; float cyc = t / 3f; float inh = Mathf.Sin(Mathf.Clamp01(cyc / 0.4f) * Mathf.PI) * (cyc < 0.4f ? 1f : 0f), exh = Mathf.Sin(Mathf.Clamp01((cyc - 0.45f) / 0.5f) * Mathf.PI) * (cyc > 0.45f ? 1f : 0f);
                return lp * (inh * 3.2f + exh * 4.5f) * 0.7f;
            });
            step = Make("br_step", 22050, 0.22f, (t, k) => { float env = Mathf.Exp(-t * 22f); return (Mathf.Sin(2f * Mathf.PI * (75f - 110f * t) * t) * 1.2f + ((float)rn.NextDouble() - 0.5f) * 0.25f * Mathf.Exp(-t * 80f)) * env; });
            heart = Make("br_heart", 22050, 1f, (t, k) => { float a = Mathf.Sin(2f * Mathf.PI * 52f * t) * Mathf.Exp(-t * 16f); float t2 = t - 0.26f; float b = t2 > 0f ? Mathf.Sin(2f * Mathf.PI * 46f * t2) * Mathf.Exp(-t2 * 18f) * 0.8f : 0f; return (a + b) * 0.9f; });
            sting = Make("br_sting", 22050, 0.9f, (t, k) => { float env = Mathf.Exp(-t * 3.5f) * Mathf.Clamp01(t / 0.01f); return (Mathf.Sin(2f * Mathf.PI * 1244f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 1319f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 622f * t) * 0.3f + ((float)rn.NextDouble() - 0.5f) * 0.25f * Mathf.Exp(-t * 30f)) * env; });
        }
        public static AudioClip BreathClip { get { Init(); return breath; } }
        public static void Scream(Vector3 pos, float vol) { Init(); AudioSource.PlayClipAtPoint(scream, pos, vol); }
        public static void Sting(Vector3 pos, float vol) { Init(); AudioSource.PlayClipAtPoint(sting, pos, vol); }
        public static void Step(Vector3 pos, float vol) { Init(); AudioSource.PlayClipAtPoint(step, pos, vol); }
        public static void CatchScream() { Init(); s2d.Stop(); s2d.clip = catchC; s2d.volume = 1f; s2d.Play(); }
        public static void Rasp() { Init(); s2d.Stop(); s2d.clip = scream; s2d.pitch = 0.5f; s2d.volume = 0.6f; s2d.Play(); }
        public static void StopScreams() { if (s2d != null) { s2d.Stop(); s2d.pitch = 1f; } }
        public static void Heart(float vol, float pitch)
        {
            Init(); if (vol < 0.02f) { if (heartSrc.isPlaying) heartSrc.Stop(); return; }
            if (!heartSrc.isPlaying) { heartSrc.clip = heart; heartSrc.Play(); } heartSrc.volume = Mathf.Clamp01(vol); heartSrc.pitch = pitch;
        }
        public static void Stop() { if (heartSrc != null) heartSrc.Stop(); if (s2d != null) s2d.Stop(); }
    }

    // ============================== поведение ==============================
}
