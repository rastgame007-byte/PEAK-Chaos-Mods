// Монстр Backrooms: процедурный скелет с плавной анимацией (стойка → сгорбленный бег → на четвереньках), поведение «подглядывает → крадётся → охотится»
// и страшная анимация поимки. Чем дольше игрок в лабиринте, тем больше ярость: рост, длина рук и шеи, наклон, скорость.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
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
        static Texture2D SkinTex()
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
            var g = new GameObject("PF_br_player_mon"); g.transform.SetParent(joint, false); g.AddComponent<MeshFilter>().sharedMesh = BrBody.Tube(prof, dir < 0 ? -1 : 1);
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; rends.Add(r); return new Sg { vis = g.transform, dir = dir, rad = rad, wx = wx };
        }
        GameObject Prim(PrimitiveType pt, Transform par, Vector3 lp, Vector3 ls, Material m, Vector3 euler = default(Vector3))
        {
            var g = GameObject.CreatePrimitive(pt); g.name = "PF_br_player_mon"; var c = g.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c);
            g.transform.SetParent(par, false); g.transform.localPosition = lp; g.transform.localScale = ls; g.transform.localEulerAngles = euler; var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m; rends.Add(r); return g;
        }
        GameObject MeshPart(Mesh mesh, Transform par, Vector3 lp, Material m)
        {
            var g = new GameObject("PF_br_player_mon"); g.transform.SetParent(par, false); g.transform.localPosition = lp; g.AddComponent<MeshFilter>().sharedMesh = mesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; rends.Add(r); return g;
        }
        Transform[] hair = new Transform[16];

        public BrMon(Vector3 pos)
        {
            root = new GameObject("PF_br_player_monster"); root.transform.position = pos; var R = root.transform; rt = R;
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
                hj.localRotation = Quaternion.Euler(8f, ang, 0f); hair[i] = hj; float hl2 = 0.3f + (float)rn.NextDouble() * 0.35f; var hg = new GameObject("PF_br_player_mon"); hg.transform.SetParent(hj, false); hg.transform.localPosition = new Vector3(0, -hl2 * 0.5f, 0);
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
            host = new GameObject("PF_br_player_audio"); UnityEngine.Object.DontDestroyOnLoad(host); s2d = host.AddComponent<AudioSource>(); s2d.spatialBlend = 0f; s2d.playOnAwake = false;
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
    public partial class PeakFunPlugin
    {
        BrMon mon; int ms; const int MsNone = 0, MsPeek = 1, MsStalk = 2, MsFlee = 3, MsHunt = 4, MsCatch = 5;
        float msT, brSeenT, brMonNext, brAngerOverride = -1f, brProx, brGlitch, mPathAt, mMeas, mHuntShout; int brSight; bool brSeenOverride, brCatching;
        readonly List<Vector2Int> mPath = new List<Vector2Int>(); int[,] mFleeMap; float mFleeAt; Vector3 mLastPos;
        GameObject brEnt { get { return mon != null ? mon.root : null; } }
        static readonly string[] MsNames = { "нет", "подглядывает", "крадётся", "убегает", "охотится", "поймал" };

        float BrAnger(float inT)
        {
            if (brAngerOverride >= 0f) return brAngerOverride;
            return Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(inT / Mathf.Max(30f, brAngerSecs)), 0.9f) * 0.85f + brSight * 0.03f);
        }

        void RemoveMon()
        {
            if (mon != null && mon.root != null) UnityEngine.Object.Destroy(mon.root); mon = null; ms = MsNone; mPath.Clear(); brSeenT = 0f; brProx = 0f;
        }

        void BrAudioStop() { BrSnd.Stop(); }

        void BrTickFx(float dt)
        {
            brGlitch = Mathf.MoveTowards(brGlitch, 0f, dt * 1.4f);
            Shade.Boost = brInside ? Mathf.Max(brProx * 0.7f, brGlitch) : 0f;
            if (brInside && mon != null && !brCatching) BrSnd.Heart(brProx * 0.85f, 0.9f + brProx * 0.9f); else if (!brCatching) { if (wtMon != null) BrSnd.Heart(wtHeartVol, wtHeartPitch); else BrSnd.Heart(0f, 1f); }
        }

        bool BrSeen(Camera cam, Vector3 head)
        {
            if (cam == null) return false; Vector3 to = head - cam.transform.position; float d = to.magnitude; if (d > 70f || d < 0.1f) return false;
            if (Vector3.Angle(cam.transform.forward, to) > 36f) return false;
            return !Physics.Linecast(cam.transform.position, head, 1 << BrLayer(), QueryTriggerInteraction.Ignore);
        }

        List<Vector2Int> BrPathCells(Vector2Int from, Vector2Int to)
        {
            var res = new List<Vector2Int>(); if (from == to) return res;
            var prev = new Vector2Int[brN, brN]; var vis = new bool[brN, brN]; var q = new Queue<Vector2Int>(); q.Enqueue(from); vis[from.x, from.y] = true; int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            while (q.Count > 0) { var c = q.Dequeue(); if (c == to) break; for (int k = 0; k < 4; k++) if (BrPass(c.x, c.y, k)) { int nx = c.x + dx[k], ny = c.y + dy[k]; if (!vis[nx, ny]) { vis[nx, ny] = true; prev[nx, ny] = c; q.Enqueue(new Vector2Int(nx, ny)); } } }
            if (!vis[to.x, to.y]) return res; var cc = to; while (cc != from) { res.Add(cc); cc = prev[cc.x, cc.y]; } res.Reverse(); return res;
        }

        Vector2Int BrCell(Vector3 world) { var l = world - brBase; return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(l.x / BrC), 0, brN - 1), Mathf.Clamp(Mathf.FloorToInt(l.z / BrC), 0, brN - 1)); }
        Vector3 BrCenter(Vector2Int c) { return brBase + new Vector3((c.x + 0.5f) * BrC, 0f, (c.y + 0.5f) * BrC); }

        /// <summary>Идёт по клеткам к цели. Возвращает фактическую скорость.</summary>
        float MonMove(Vector3 target, float speed, float dt)
        {
            Vector3 ep = mon.rt.position;
            if (Time.time > mPathAt) { mPathAt = Time.time + 0.35f; mPath.Clear(); mPath.AddRange(BrPathCells(BrCell(ep), BrCell(target))); }
            Vector3 goal = target; Vector3 flatT = target - ep; flatT.y = 0f;
            if (mPath.Count > 0 && flatT.magnitude > BrC * 1.1f)
            {
                goal = BrCenter(mPath[0]); Vector3 dd = goal - ep; dd.y = 0f;
                if (dd.magnitude < 0.45f) { mPath.RemoveAt(0); if (mPath.Count > 0) goal = BrCenter(mPath[0]); }
            }
            Vector3 dir = goal - ep; dir.y = 0f; if (dir.magnitude < 0.05f) return 0f;
            Vector3 np = ep + dir.normalized * speed * dt; np.y = brBase.y; mon.rt.position = np;
            mon.rt.rotation = Quaternion.Slerp(mon.rt.rotation, Quaternion.LookRotation(dir.normalized), 1f - Mathf.Exp(-dt * 9f)); return speed;
        }

        void MonFace(Vector3 pc, float rate, float dt)
        {
            Vector3 d = pc - mon.rt.position; d.y = 0f; if (d.sqrMagnitude < 0.01f) return; mon.rt.rotation = Quaternion.Slerp(mon.rt.rotation, Quaternion.LookRotation(d.normalized), 1f - Mathf.Exp(-dt * rate));
        }

        void BrVanish(float a, bool fx)
        {
            if (mon != null && fx) { BrSnd.Sting(mon.rt.position + Vector3.up * 1.5f, 0.45f); brLightMul = 0.15f; }
            RemoveMon(); brMonNext = Time.time + Mathf.Lerp(38f, 12f, a) * UnityEngine.Random.Range(0.8f, 1.4f);
        }

        void BrNotice(float a)
        {
            brSight++; brSeenT = 0f; msT = 0f; mHuntShout = 0f;
            if (a < 0.3f) { BrVanish(a, true); return; }
            if (a < 0.58f) { ms = MsFlee; mFleeAt = 0f; BrSnd.Sting(mon.rt.position, 0.6f); return; }
            ms = MsHunt; BrSnd.Scream(mon.rt.position + Vector3.up * 1.5f, 1f); brGlitch = 1f; brLightMul = 0.1f; CamShake.Boost = Mathf.Max(CamShake.Boost, 0.9f);
        }

        void SpawnMon(Vector3 pc, float a)
        {
            var cam = Camera.main; if (cam == null) return; float wd = Mathf.Lerp(30f, 13f, a); Vector3 best = Vector3.zero; bool found = false; int mask = 1 << BrLayer();
            for (int i = 0; i < 90 && !found; i++)
            {
                var cell = new Vector2Int(UnityEngine.Random.Range(0, brN), UnityEngine.Random.Range(0, brN)); Vector3 cp = BrCenter(cell); Vector3 flat = cp - pc; flat.y = 0f; float d = flat.magnitude;
                if (d < wd * 0.6f || d > wd * 1.5f) continue;
                Vector3 target = cp + Vector3.up * 1.6f; float ang = Vector3.Angle(cam.transform.forward, target - cam.transform.position);
                if (a < 0.6f && ang < 50f) continue;
                bool los = !Physics.Linecast(cam.transform.position, target, mask, QueryTriggerInteraction.Ignore);
                if (!los && i < 60) continue; best = cp; found = true;
            }
            if (!found) { brMonNext = Time.time + 5f; return; }
            best.y = brBase.y; mon = new BrMon(best); mon.root.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(pc - best, Vector3.up).normalized);
            if (mon.breath != null) { mon.breath.clip = BrSnd.BreathClip; mon.breath.volume = 0.5f; mon.breath.Play(); }
            ms = a >= 0.5f ? MsStalk : MsPeek; msT = 0f; brSeenT = 0f; mLastPos = best; mMeas = 0f; mPathAt = 0f; BrSnd.Sting(best + Vector3.up * 2f, 0.35f); brLightMul = 0.3f;
            BrDiag("монстр появился: ярость " + Mathf.RoundToInt(a * 100f) + "%, состояние «" + MsNames[ms] + "», расстояние " + Vector3.Distance(pc, best).ToString("0") + " м");
        }

        void TickBrMonster(Component me, Vector3 pc, float inT)
        {
            float dt = Time.deltaTime;
            if (!brEntity) { if (mon != null) RemoveMon(); return; }
            float a = BrAnger(inT);
            if (mon == null) { brProx = Mathf.MoveTowards(brProx, 0f, dt); if (Time.time >= brMonNext) SpawnMon(pc, a); return; }
            if (ms == MsCatch) return;
            var cam = Camera.main; Vector3 headP = mon.HeadPos(); Vector3 rp = mon.rt.position; Vector3 toP = pc - rp; float dy = Mathf.Abs(toP.y); toP.y = 0f; float dist = toP.magnitude;
            bool seen = brSeenOverride || BrSeen(cam, headP); msT += dt;
            brProx = Mathf.MoveTowards(brProx, Mathf.Clamp01(1f - dist / 28f), dt * 1.5f);
            float crawl = 0f, hunchT = 0f, shout = 0f; float need = Mathf.Lerp(0.7f, 0.3f, a);
            if (dist < 24f && UnityEngine.Random.value < dt * (0.15f + brProx * 1.6f)) brLightMul = UnityEngine.Random.Range(0.05f, 0.5f);
            switch (ms)
            {
                case MsPeek:
                    if (seen) brSeenT += dt; else brSeenT = Mathf.Max(0f, brSeenT - dt * 0.5f);
                    if (brSeenT > need) { BrNotice(a); break; }
                    if (msT > Mathf.Lerp(16f, 7f, a) && !seen) { if (a >= 0.22f) { ms = MsStalk; msT = 0f; } else { BrVanish(a, true); } break; }
                    MonFace(pc, 2.5f, dt); hunchT = Mathf.Lerp(0f, 22f, Mathf.SmoothStep(0.3f, 0.7f, a));
                    break;
                case MsStalk:
                    {
                        float sp = 0f; float stopD = Mathf.Lerp(10f, 5.5f, a);
                        if (seen) { brSeenT += dt; if (brSeenT > need) { BrNotice(a); break; } MonFace(pc, 4f, dt); }
                        else
                        {
                            brSeenT = Mathf.Max(0f, brSeenT - dt);
                            if (dist > stopD) sp = MonMove(pc, Mathf.Lerp(1.8f, 5f, a), dt); else { MonFace(pc, 3f, dt); if (a >= 0.5f && msT > 4f) { ms = MsHunt; msT = 0f; BrSnd.Scream(mon.rt.position + Vector3.up * 1.5f, 1f); brGlitch = 1f; } }
                        }
                        if (msT > 45f && dist > 20f) { BrVanish(a, false); break; }
                        hunchT = Mathf.Lerp(8f, 38f, a); mMeas = sp; break;
                    }
                case MsFlee:
                    {
                        if (Time.time > mFleeAt) { mFleeAt = Time.time + 0.5f; mFleeMap = BrBfs(BrCell(pc).x, BrCell(pc).y); }
                        var ec = BrCell(rp); Vector2Int bc = ec; int bd = mFleeMap != null ? mFleeMap[ec.x, ec.y] : 0; int[] dx = { 1, -1, 0, 0 }, dyy = { 0, 0, 1, -1 };
                        if (mFleeMap != null) for (int k = 0; k < 4; k++) if (BrPass(ec.x, ec.y, k)) { var n = new Vector2Int(ec.x + dx[k], ec.y + dyy[k]); if (mFleeMap[n.x, n.y] > bd) { bd = mFleeMap[n.x, n.y]; bc = n; } }
                        float sp = 0f; if (bc != ec) { Vector3 g = BrCenter(bc); Vector3 dir = g - rp; dir.y = 0f; float step = 6.5f * dt; mon.rt.position = new Vector3(rp.x, brBase.y, rp.z) + dir.normalized * Mathf.Min(step, dir.magnitude); mon.rt.rotation = Quaternion.Slerp(mon.rt.rotation, Quaternion.LookRotation(dir.normalized), 1f - Mathf.Exp(-dt * 9f)); sp = 6.5f; }
                        if (msT > 5f || (!seen && dist > 26f && msT > 1.5f) || (bc == ec && msT > 0.6f)) { BrVanish(a, true); break; }
                        hunchT = 20f; mMeas = sp; break;
                    }
                case MsHunt:
                    {
                        float sp = MonMove(pc, Mathf.Lerp(4.4f, 8.6f, a), dt); mMeas = sp; crawl = a >= 0.78f ? 1f : (a >= 0.55f ? 0.35f : 0f); hunchT = Mathf.Lerp(25f, 50f, a); shout = Mathf.Clamp01(1f - msT * 0.5f);
                        if (dist < 1.7f && dy < 2.6f) { StartCoroutine(CoBrCatch(me)); break; }
                        if (msT > 34f && dist > 22f && a < 0.92f) BrVanish(a, false);
                        break;
                    }
            }
            if (mon == null) return;
            // фактическая скорость для анимации
            float meas = (mon.rt.position - mLastPos).magnitude / Mathf.Max(dt, 0.0001f); mLastPos = mon.rt.position; mMeas = Mathf.Lerp(mMeas, meas, 1f - Mathf.Exp(-dt * 10f));
            Vector3 lookP = pc + Vector3.up * 0.3f;
            mon.Pose(dt, mMeas, a, crawl, hunchT, lookP, true, shout);
            // звук: дыхание и шаги
            if (mon.breath != null) mon.breath.volume = Mathf.Lerp(0.15f, 0.9f, Mathf.Clamp01(a * 0.6f + brProx * 0.6f));
            float sg = Mathf.Sign(Mathf.Sin(mon.phase)); if (sg != mon.lastStepSign && mMeas > 0.8f) { mon.lastStepSign = sg; BrSnd.Step(mon.rt.position, Mathf.Lerp(0.35f, 1f, a)); } else if (mMeas <= 0.8f) mon.lastStepSign = sg;
            if (ms == MsHunt) CamShake.Boost = Mathf.Max(CamShake.Boost, Mathf.InverseLerp(14f, 2f, dist) * 0.45f);
        }

        // ============================== поимка ==============================
        bool brCatchCam; Vector3 brCamP; Quaternion brCamQ; float brTapeEnd, brCamLightK = 1f;
        static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>
        /// Поимка — не «лицо в кадр», а сцена найденной плёнки: монстр бросается и сбивает тебя (камера падает на пол и валится набок), нависает над объективом,
        /// медленно склоняет искажённое лицо и раскрывает пасть, гаснет свет; затем хватает и тащит камеру по коридору, уходя спиной и вывернув голову назад к объективу;
        /// останавливается в темноте, глаза гаснут — запись обрывается.
        /// </summary>
        IEnumerator CoBrCatch(Component me)
        {
            if (brCatching || !brInside || mon == null) yield break;
            var cam0 = Camera.main; if (cam0 == null) { RemoveMon(); yield break; }
            brCatching = true; brLock = true; ms = MsCatch; mon.catching = false; mon.root.SetActive(true); if (mon.breath != null) mon.breath.Stop(); BrSnd.Heart(0f, 1f);
            BrDiag("игрока поймал монстр: ярость " + Mathf.RoundToInt(BrAnger(Time.time - brEnterT) * 100f) + "%");
            Vector3 P0 = cam0.transform.position; Quaternion Q0 = cam0.transform.rotation; Vector3 pc = CenterOf(me); int mask = 1 << BrLayer();
            Vector3 toMon = mon.rt.position - pc; toMon.y = 0f; if (toMon.sqrMagnitude < 0.01f) { toMon = cam0.transform.forward; toMon.y = 0f; } toMon.Normalize(); Vector3 fallDir = -toMon;
            float floorY = brBase.y; RaycastHit fh; if (Physics.Raycast(P0, Vector3.down, out fh, 4f, mask, QueryTriggerInteraction.Ignore)) floorY = fh.point.y;
            float fallDist = 1.2f; RaycastHit wh; if (Physics.Raycast(P0, fallDir, out wh, 1.7f, mask, QueryTriggerInteraction.Ignore)) fallDist = Mathf.Max(0.15f, wh.distance - 0.45f);
            Vector3 camFloor = new Vector3(P0.x, floorY + 0.26f, P0.z) + fallDir * fallDist; Vector3 monStart = mon.rt.position; Vector3 M1 = new Vector3(camFloor.x, floorY, camFloor.z) + toMon * 1.2f;
            float rs = UnityEngine.Random.value < 0.5f ? -1f : 1f; Vector3 mp = monStart; Vector3 dragDir = Vector3.zero; bool dragInit = false; float dragSpeed = 0f, t = 0f, stepSign = 1f; bool rasped = false; Vector3 camPos = P0; Quaternion camRot = Q0;
            Sfx2.Boom(); CamShake.Boost = Mathf.Max(CamShake.Boost, 1.4f); brGlitch = 1f; brCatchCam = true; brCamP = P0; brCamQ = Q0; float lx = 0f;
            while (t < 8.3f && brInside && mon != null)
            {
                float dt = Time.deltaTime; t += dt; Vector3 headP = mon.HeadPos(); float sway = Mathf.Sin(t * 1.3f) * 0.012f;
                if (t < 0.7f)
                {   // --- бросок и удар ---
                    float lk = Ease(t / 0.4f); mp = Vector3.Lerp(monStart, M1, lk); mon.Pose(dt, 7.5f * (1f - lk), 1f, 0.6f * (1f - lk), 35f, camPos, true, 0.4f);
                    float f = Mathf.Clamp01((t - 0.16f) / 0.55f); float hz = 1f - Mathf.Pow(1f - f, 2f), vz = f * f;
                    camPos = new Vector3(Mathf.Lerp(P0.x, camFloor.x, hz), Mathf.Lerp(P0.y, camFloor.y, vz), Mathf.Lerp(P0.z, camFloor.z, hz));
                    if (t > 0.71f) camPos.y += Mathf.Abs(Mathf.Sin((t - 0.71f) * 14f)) * 0.06f * Mathf.Exp(-(t - 0.71f) * 6f);
                    Quaternion look = Quaternion.LookRotation((headP - camPos).normalized, Vector3.up); camRot = Quaternion.Slerp(Q0, look, Ease(t / 0.35f)) * Quaternion.Euler(0f, 0f, rs * 38f * Ease((t - 0.1f) / 0.55f));
                    brLightMul = UnityEngine.Random.value < 0.4f ? 0.1f : 0.9f; CamShake.Boost = Mathf.Max(CamShake.Boost, 1.2f * (1f - t));
                    if (t > 0.2f && lx < 1f) { lx = 1f; Sfx2.Boom(); brFlash = 0.5f; }
                }
                else if (t < 3.4f)
                {   // --- нависает ---
                    float k = (t - 0.7f) / 2.7f; float close = Ease((t - 1.6f) / 1.7f);
                    mp = M1 - toMon * (0.55f * close); mon.reach = Ease((t - 0.9f) / 1f); mon.jawForce = Mathf.Lerp(0.04f, 0.85f, Ease((t - 1.4f) / 1.9f)); mon.rollForce = rs * Mathf.Lerp(18f, 34f, k) + 6f * Mathf.Sin(t * 2.1f);
                    mon.Pose(dt, 0f, 1f, 0f, Mathf.Lerp(30f, 55f, close), camPos, true, 0f);
                    camPos = camFloor + new Vector3(0f, sway, 0f); Quaternion look = Quaternion.LookRotation((headP - camPos).normalized, Vector3.up);
                    camRot = look * Quaternion.Euler(-6f, 0f, rs * Mathf.Lerp(38f, 22f, k) + 2.5f * Mathf.Sin(t * 1.7f));
                    brLightMul = t < 2.0f ? (UnityEngine.Random.value < 0.5f ? 0.08f : 0.7f) : Mathf.Lerp(0.2f, 0f, Ease((t - 2f) / 0.4f)); brCamLightK = Mathf.Lerp(1f, 0.55f, k);
                    brGlitch = Mathf.Max(brGlitch, 0.25f + 0.5f * close); BrSnd.Heart(0.9f, 1.25f + 0.3f * close);
                    if (!rasped && t > 3.0f) { rasped = true; BrSnd.Rasp(); brGlitch = 1f; CamShake.Boost = Mathf.Max(CamShake.Boost, 1.1f); }
                }
                else
                {   // --- тащит камеру, уходит спиной, голова вывернута назад ---
                    float tt = t - 3.4f;
                    if (!dragInit)
                    {
                        dragInit = true; float bestScore = -1f; Vector3[] axes = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
                        foreach (var ax in axes)
                        {
                            RaycastHit a1, b1; float ahead = 14f, behind = 14f; Vector3 o = new Vector3(mp.x, floorY + 1f, mp.z);
                            if (Physics.Raycast(o, ax, out a1, 14f, mask, QueryTriggerInteraction.Ignore)) ahead = a1.distance; if (Physics.Raycast(o, -ax, out b1, 14f, mask, QueryTriggerInteraction.Ignore)) behind = b1.distance;
                            float sc = Mathf.Min(ahead, behind * 2.2f) - (behind < 1.4f ? 6f : 0f); if (sc > bestScore) { bestScore = sc; dragDir = ax; }
                        }
                        mon.reach = 0f; mon.headRate = 95f; mon.headTwist = 150f; mon.jawForce = 0.55f; mon.rollForce = rs * 14f;
                    }
                    dragSpeed = tt < 3.0f ? Mathf.Lerp(0f, 1.45f, Ease(tt / 1.2f)) : Mathf.Lerp(1.45f, 0f, Ease((tt - 3.0f) / 0.5f));
                    RaycastHit ah; if (Physics.Raycast(new Vector3(mp.x, floorY + 1f, mp.z), dragDir, out ah, 0.8f, mask, QueryTriggerInteraction.Ignore)) dragSpeed = 0f;
                    mp += dragDir * dragSpeed * dt;
                    mon.Pose(dt, dragSpeed, 1f, 0f, 22f, Vector3.zero, false, 0f);
                    float behindD = 1.2f; RaycastHit bh; if (Physics.Raycast(new Vector3(mp.x, floorY + 0.4f, mp.z), -dragDir, out bh, 1.3f, mask, QueryTriggerInteraction.Ignore)) behindD = Mathf.Max(0.35f, bh.distance - 0.25f);
                    Vector3 targetCam = new Vector3(mp.x, floorY + 0.3f, mp.z) - dragDir * behindD + new Vector3(0f, Mathf.Abs(Mathf.Sin(mon.phase)) * 0.035f * (dragSpeed > 0.1f ? 1f : 0f), 0f);
                    camPos = Vector3.Lerp(camPos, targetCam, 1f - Mathf.Exp(-dt * (tt < 0.8f ? 4f : 12f)));
                    Quaternion look = Quaternion.LookRotation((headP - camPos).normalized, Vector3.up); camRot = Quaternion.Slerp(camRot, look * Quaternion.Euler(-4f, 0f, rs * Mathf.Lerp(22f, 8f, Ease(tt / 2f)) + 1.5f * Mathf.Sin(t * 1.9f)), 1f - Mathf.Exp(-dt * 6f));
                    brLightMul = 0f; brCamLightK = 0.5f; brGlitch = Mathf.Max(brGlitch, 0.3f + 0.04f * tt); BrSnd.Heart(0.8f, 1.1f);
                    float sg = Mathf.Sign(Mathf.Sin(mon.phase)); if (sg != stepSign) { stepSign = sg; if (dragSpeed > 0.3f) BrSnd.Step(mp, 0.55f); }
                    if (tt > 3.1f) { mon.eyeOff = Ease((tt - 3.5f) / 0.3f); brTapeEnd = Mathf.Clamp01((tt - 3.0f) / 0.6f); }
                    if (tt > 4.2f) brFade = Mathf.Clamp01((tt - 4.2f) / 0.5f);
                    if (tt > 4.7f) break;
                }
                mon.rt.position = new Vector3(mp.x, floorY, mp.z);
                if (t < 3.4f) { Vector3 fd = camPos - mp; fd.y = 0f; if (fd.sqrMagnitude > 0.001f) mon.rt.rotation = Quaternion.Slerp(mon.rt.rotation, Quaternion.LookRotation(fd.normalized), 1f - Mathf.Exp(-dt * 10f)); }
                else if (dragDir != Vector3.zero) mon.rt.rotation = Quaternion.Slerp(mon.rt.rotation, Quaternion.LookRotation(dragDir), 1f - Mathf.Exp(-dt * 4f));
                brCamP = camPos; brCamQ = camRot;
                yield return null;
            }
            BrSnd.StopScreams(); BrSnd.Heart(0f, 1f); brFade = 1f; brCatchCam = false; RemoveMon(); brCatching = false; brLock = false; brTapeEnd = 0f; brCamLightK = 1f; brLightMul = 1f;
            yield return StartCoroutine(CoBrExit("Запись оборвалась… ты очнулся наверху"));
        }
    }
}
