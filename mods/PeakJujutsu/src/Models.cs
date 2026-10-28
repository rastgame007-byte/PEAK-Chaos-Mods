// Процедурные модели проклятий, боссов и призванных существ. Всё собирается из примитивов (MB) с именованными частями для анимации.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakJJK
{
    internal static class Models
    {
        static Color C(float r, float g, float b, float a = 1f) { return new Color(r, g, b, a); }
        static readonly Color Flesh = C(0.09f, 0.06f, 0.11f), FleshHi = C(0.16f, 0.1f, 0.18f), Bone = C(0.86f, 0.84f, 0.76f), Eye = C(1f, 0.9f, 0.6f), Red = C(1f, 0.15f, 0.12f);
        static Quaternion Q(float x, float y, float z) { return Quaternion.Euler(x, y, z); }

        // ---------- Проклятие 4 ранга: «Глаз» ----------
        public static ModelB Wisp()
        {
            var m = new ModelB("Wisp"); m.P("body"); var b = m.Lit("body", Flesh, 0.55f); var hi = m.Lit("body", FleshHi, 0.6f);
            b.Ell(Vector3.zero, new Vector3(0.5f, 0.42f, 0.5f), Flesh); hi.Ell(new Vector3(0, 0.18f, -0.1f), new Vector3(0.36f, 0.22f, 0.34f), FleshHi);
            var eye = m.Glow("body", Eye); eye.Ell(new Vector3(0, 0.04f, 0.4f), new Vector3(0.24f, 0.24f, 0.12f), Quaternion.identity, Eye); m.Glow("body", Red).Ell(new Vector3(0, 0.04f, 0.5f), new Vector3(0.09f, 0.14f, 0.05f), Quaternion.identity, Red);
            var bone = m.Lit("body", Bone, 0.3f); for (int i = 0; i < 6; i++) { float a = -0.6f + i * 0.24f; bone.Tube(new Vector3(Mathf.Sin(a) * 0.3f, -0.2f, 0.38f), new Vector3(Mathf.Sin(a) * 0.3f, -0.32f, 0.4f), 0.03f, 0.005f, Bone, 5, false); }
            for (int i = 0; i < 6; i++)
            {
                string pn = "tent" + i; m.P(pn, "body", Vector3.zero); float a = i / 6f * Mathf.PI * 2f; Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var t = m.Lit(pn, Flesh, 0.5f);
                t.Tube(d * 0.25f - Vector3.up * 0.1f, d * 0.5f - Vector3.up * 0.6f, 0.09f, 0.02f, Flesh, 6, false); t.Tube(d * 0.5f - Vector3.up * 0.6f, d * 0.4f - Vector3.up * 0.95f, 0.02f, 0.006f, Flesh, 5, false);
            }
            return m;
        }

        // ---------- Проклятие 3 ранга: «Пёс» ----------
        public static ModelB Hound(Color fur, Color mark, Color glow)
        {
            var m = new ModelB("Hound"); m.P("body"); var f = m.Lit("body", fur, 0.5f); f.Ell(new Vector3(0, 0.55f, 0), new Vector3(0.34f, 0.32f, 0.7f), fur); f.Ell(new Vector3(0, 0.62f, 0.5f), new Vector3(0.36f, 0.34f, 0.4f), fur);
            f.Tube(new Vector3(0, 0.55f, -0.55f), new Vector3(0, 0.75f, -1.15f), 0.07f, 0.01f, fur, 6, false);
            var sp = m.Lit("body", mark, 0.3f); for (int i = 0; i < 5; i++) { float z = -0.35f + i * 0.22f; sp.Tube(new Vector3(0, 0.84f - Mathf.Abs(i - 2) * 0.02f, z), new Vector3(0, 1.05f - Mathf.Abs(i - 2) * 0.04f, z - 0.1f), 0.05f, 0.005f, mark, 5, false); }
            m.P("head", "body", new Vector3(0, 0.78f, 0.82f)); var hd = m.Lit("head", fur, 0.5f); hd.Ell(Vector3.zero, new Vector3(0.28f, 0.26f, 0.3f), fur);
            var mask = m.Lit("head", Bone, 0.25f); mask.Ell(new Vector3(0, -0.03f, 0.26f), new Vector3(0.2f, 0.17f, 0.28f), Bone); mask.Tube(new Vector3(-0.14f, 0.2f, 0f), new Vector3(-0.2f, 0.5f, -0.1f), 0.05f, 0.008f, Bone, 5, false); mask.Tube(new Vector3(0.14f, 0.2f, 0f), new Vector3(0.2f, 0.5f, -0.1f), 0.05f, 0.008f, Bone, 5, false);
            var g = m.Glow("head", glow); g.Ell(new Vector3(-0.13f, 0.08f, 0.2f), new Vector3(0.06f, 0.04f, 0.04f), glow); g.Ell(new Vector3(0.13f, 0.08f, 0.2f), new Vector3(0.06f, 0.04f, 0.04f), glow);
            m.P("jaw", "head", new Vector3(0, -0.12f, 0.1f)); var jw = m.Lit("jaw", Bone, 0.25f); jw.Ell(new Vector3(0, 0, 0.2f), new Vector3(0.14f, 0.05f, 0.22f), Bone); for (int i = -2; i <= 2; i++) jw.Tube(new Vector3(i * 0.04f, 0.02f, 0.3f), new Vector3(i * 0.04f, 0.08f, 0.31f), 0.012f, 0.002f, Bone, 4, false);
            string[] ln = { "legFL", "legFR", "legBL", "legBR" }; for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 0.24f, z = i < 2 ? 0.55f : -0.4f; m.P(ln[i], "body", new Vector3(x, 0.55f, z)); var l = m.Lit(ln[i], fur, 0.5f);
                l.Tube(Vector3.zero, new Vector3(0, -0.32f, 0.08f), 0.1f, 0.06f, fur, 6, false); l.Tube(new Vector3(0, -0.32f, 0.08f), new Vector3(0, -0.55f, -0.02f), 0.06f, 0.04f, fur, 6, false); l.Ell(new Vector3(0, -0.56f, 0.04f), new Vector3(0.06f, 0.03f, 0.1f), fur);
            }
            return m;
        }

        // ---------- Проклятие 2 ранга: «Мухоголовое» ----------
        public static ModelB Flyhead()
        {
            var m = new ModelB("Flyhead"); m.P("body"); var f = m.Lit("body", C(0.07f, 0.06f, 0.07f), 0.6f); f.Ell(new Vector3(0, 0, 0), new Vector3(0.28f, 0.5f, 0.24f), C(0.07f, 0.06f, 0.07f)); f.Ell(new Vector3(0, -0.5f, 0.05f), new Vector3(0.22f, 0.3f, 0.2f), C(0.1f, 0.08f, 0.09f));
            for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2f; f.Tube(new Vector3(Mathf.Cos(a) * 0.12f, -0.65f, Mathf.Sin(a) * 0.12f), new Vector3(Mathf.Cos(a) * 0.3f, -1.4f, Mathf.Sin(a) * 0.3f), 0.04f, 0.008f, C(0.1f, 0.08f, 0.09f), 5, false); }
            m.P("head", "body", new Vector3(0, 0.62f, 0.05f)); var h = m.Lit("head", C(0.12f, 0.1f, 0.1f), 0.5f); h.Ell(Vector3.zero, new Vector3(0.3f, 0.3f, 0.3f), C(0.12f, 0.1f, 0.1f));
            var eye = m.Glow("head", C(0.9f, 0.25f, 0.1f)); eye.Ell(new Vector3(-0.2f, 0.06f, 0.2f), new Vector3(0.16f, 0.2f, 0.14f), C(0.9f, 0.25f, 0.1f)); eye.Ell(new Vector3(0.2f, 0.06f, 0.2f), new Vector3(0.16f, 0.2f, 0.14f), C(0.9f, 0.25f, 0.1f));
            var eh = m.Glow("head", C(1f, 0.6f, 0.3f)); for (int i = 0; i < 6; i++) { float a = i * 1.05f; eh.Ball(new Vector3(-0.2f + Mathf.Cos(a) * 0.08f, 0.06f + Mathf.Sin(a) * 0.1f, 0.3f), 0.03f, C(1f, 0.7f, 0.4f), 4, 6); eh.Ball(new Vector3(0.2f + Mathf.Cos(a) * 0.08f, 0.06f + Mathf.Sin(a) * 0.1f, 0.3f), 0.03f, C(1f, 0.7f, 0.4f), 4, 6); }
            var pr = m.Lit("head", C(0.15f, 0.12f, 0.12f), 0.4f); pr.Tube(new Vector3(0, -0.12f, 0.25f), new Vector3(0, -0.4f, 0.45f), 0.05f, 0.01f, C(0.15f, 0.12f, 0.12f), 6, false);
            foreach (var sd in new[] { -1, 1 })
                for (int w = 0; w < 2; w++)
                {
                    string pn = (sd < 0 ? "wingL" : "wingR") + w; m.P(pn, "body", new Vector3(sd * 0.15f, 0.25f - w * 0.2f, -0.08f)); var mb = m.M(pn, "wing", () => Gfx.Fade(null, C(0.75f, 0.85f, 0.9f, 0.4f)));
                    Vector3 dir = new Vector3(sd, 0.3f - w * 0.25f, -0.4f).normalized; mb.QuadBoth(Vector3.zero, dir * 1.2f + Vector3.up * 0.25f, dir * 1.5f - Vector3.up * 0.1f, dir * 0.4f - Vector3.up * 0.3f, Vector3.up, C(0.8f, 0.9f, 1f, 1f));
                }
            return m;
        }

        // ---------- Проклятие 1 ранга: «Громила» ----------
        public static ModelB Brute()
        {
            var m = new ModelB("Brute"); m.P("body"); Color sk = C(0.2f, 0.17f, 0.2f), dk = C(0.1f, 0.08f, 0.1f); var f = m.Lit("body", sk, 0.45f); var d = m.Lit("body", dk, 0.5f);
            f.Ell(new Vector3(0, 1.5f, 0), new Vector3(0.95f, 0.95f, 0.7f), sk); d.Ell(new Vector3(0, 0.9f, 0), new Vector3(0.6f, 0.45f, 0.5f), dk);
            var lava = m.Glow("body", C(1f, 0.45f, 0.1f)); for (int i = 0; i < 5; i++) lava.Box(new Vector3(-0.5f + i * 0.25f, 1.55f + Mathf.Sin(i * 2f) * 0.2f, 0.62f), new Vector3(0.05f, 0.45f, 0.04f), Q(0, 0, (i - 2) * 12f), C(1f, 0.45f, 0.1f));
            var teeth = m.Lit("body", Bone, 0.3f); teeth.Ell(new Vector3(0, 1.2f, 0.58f), new Vector3(0.38f, 0.18f, 0.14f), C(0.3f, 0.02f, 0.05f)); for (int i = -3; i <= 3; i++) { teeth.Tube(new Vector3(i * 0.1f, 1.3f, 0.66f), new Vector3(i * 0.1f, 1.18f, 0.68f), 0.025f, 0.003f, Bone, 4, false); teeth.Tube(new Vector3(i * 0.1f, 1.08f, 0.66f), new Vector3(i * 0.1f, 1.2f, 0.68f), 0.025f, 0.003f, Bone, 4, false); }
            m.P("head", "body", new Vector3(0, 2.35f, 0.05f)); var h = m.Lit("head", sk, 0.45f); h.Ell(Vector3.zero, new Vector3(0.38f, 0.34f, 0.36f), sk); var hn = m.Lit("head", Bone, 0.3f); hn.Tube(new Vector3(0, 0.25f, 0.1f), new Vector3(0, 0.75f, 0.2f), 0.09f, 0.01f, Bone, 6, true);
            var g = m.Glow("head", C(1f, 0.2f, 0.1f)); g.Ell(new Vector3(0, 0.02f, 0.33f), new Vector3(0.2f, 0.08f, 0.06f), C(1f, 0.2f, 0.1f));
            foreach (var sd in new[] { -1, 1 })
            {
                string an = sd < 0 ? "armL" : "armR"; m.P(an, "body", new Vector3(sd * 1.0f, 2.0f, 0)); var a = m.Lit(an, sk, 0.45f);
                a.Tube(Vector3.zero, new Vector3(sd * 0.25f, -0.9f, 0.2f), 0.3f, 0.26f, sk, 8, false); a.Tube(new Vector3(sd * 0.25f, -0.9f, 0.2f), new Vector3(sd * 0.15f, -1.7f, 0.5f), 0.26f, 0.2f, sk, 8, false); a.Ell(new Vector3(sd * 0.15f, -1.85f, 0.55f), new Vector3(0.3f, 0.3f, 0.3f), dk);
                string ln = sd < 0 ? "legL" : "legR"; m.P(ln, "body", new Vector3(sd * 0.4f, 0.8f, 0)); var l = m.Lit(ln, dk, 0.5f); l.Tube(Vector3.zero, new Vector3(0, -0.8f, 0.05f), 0.28f, 0.2f, dk, 8, false); l.Ell(new Vector3(0, -0.85f, 0.15f), new Vector3(0.24f, 0.1f, 0.34f), dk);
            }
            return m;
        }

        // ---------- универсальный гуманоид ----------
        public struct HumOpts { public Color skin, cloth, cloth2, hair; public float scale; public bool coat; public int hairStyle; public Color eye; public bool blindfold; public int extraArms; public float legLen; }
        /// <summary>Части: pelvis, torso, head, armL/armR (+ foreL/foreR), legL/legR. Рост ≈ 1.8*scale. hairStyle: 0 торчащие шипы, 1 длинные, 2 острый ёжик, 3 нет.</summary>
        public static ModelB Humanoid(string name, HumOpts o)
        {
            var m = new ModelB(name); float s = o.scale <= 0 ? 1f : o.scale; float ll = o.legLen <= 0 ? 0.9f : o.legLen; m.P("pelvis", "root", new Vector3(0, ll * s, 0));
            var sk = m.Lit("pelvis", o.skin, 0.35f); var cl = m.Lit("pelvis", o.cloth, 0.4f); var cl2 = m.Lit("pelvis", o.cloth2, 0.4f);
            cl.Ell(Vector3.zero, new Vector3(0.24f, 0.17f, 0.17f) * s, o.cloth);
            m.P("torso", "pelvis", new Vector3(0, 0.12f * s, 0)); var tc = m.Lit("torso", o.cloth, 0.4f); tc.Ell(new Vector3(0, 0.28f * s, 0), new Vector3(0.27f, 0.34f, 0.18f) * s, o.cloth); tc.Ell(new Vector3(0, 0.5f * s, 0), new Vector3(0.31f, 0.2f, 0.19f) * s, o.cloth);
            if (o.coat) { tc.Ell(new Vector3(0, 0.1f * s, 0), new Vector3(0.3f, 0.3f, 0.21f) * s, o.cloth); m.Lit("torso", o.cloth2, 0.35f).Box(new Vector3(0, 0.32f * s, 0.17f * s), new Vector3(0.08f, 0.55f, 0.02f) * s, o.cloth2); }
            var tk = m.Lit("torso", o.skin, 0.35f); tk.Ell(new Vector3(0, 0.66f * s, 0.02f * s), new Vector3(0.1f, 0.1f, 0.1f) * s, o.skin);
            m.P("head", "torso", new Vector3(0, 0.84f * s, 0)); var hd = m.Lit("head", o.skin, 0.35f); hd.Ell(Vector3.zero, new Vector3(0.17f, 0.21f, 0.18f) * s, o.skin);
            var eyes = m.Glow("head", o.eye); if (!o.blindfold) { eyes.Ell(new Vector3(-0.07f * s, 0.03f * s, 0.15f * s), new Vector3(0.04f, 0.022f, 0.02f) * s, o.eye); eyes.Ell(new Vector3(0.07f * s, 0.03f * s, 0.15f * s), new Vector3(0.04f, 0.022f, 0.02f) * s, o.eye); }
            else { m.Lit("head", C(0.03f, 0.03f, 0.04f), 0.3f).Box(new Vector3(0, 0.03f * s, 0.01f * s), new Vector3(0.36f, 0.11f, 0.37f) * s, C(0.03f, 0.03f, 0.04f)); }
            var hr = m.Lit("head", o.hair, 0.5f);
            if (o.hairStyle == 0) { hr.Ell(new Vector3(0, 0.08f * s, -0.03f * s), new Vector3(0.19f, 0.18f, 0.2f) * s, o.hair); for (int i = 0; i < 11; i++) { float a = i / 11f * Mathf.PI * 2f; Vector3 d = new Vector3(Mathf.Cos(a) * 0.7f, 0.55f + (i % 3) * 0.15f, Mathf.Sin(a) * 0.7f).normalized; hr.Tube(new Vector3(0, 0.12f * s, 0) + d * 0.12f * s, new Vector3(0, 0.12f * s, 0) + d * 0.42f * s, 0.045f * s, 0.004f, o.hair, 5, false); } }
            else if (o.hairStyle == 1) { hr.Ell(new Vector3(0, 0.06f * s, -0.04f * s), new Vector3(0.2f, 0.2f, 0.22f) * s, o.hair); hr.Tube(new Vector3(0, 0.1f * s, -0.15f * s), new Vector3(0, -0.5f * s, -0.28f * s), 0.12f * s, 0.03f * s, o.hair, 8, false); hr.Tube(new Vector3(-0.15f * s, 0.05f * s, 0), new Vector3(-0.2f * s, -0.3f * s, -0.05f * s), 0.05f * s, 0.01f, o.hair, 6, false); hr.Tube(new Vector3(0.15f * s, 0.05f * s, 0), new Vector3(0.2f * s, -0.3f * s, -0.05f * s), 0.05f * s, 0.01f, o.hair, 6, false); }
            else if (o.hairStyle == 2) { hr.Ell(new Vector3(0, 0.1f * s, -0.02f * s), new Vector3(0.18f, 0.14f, 0.19f) * s, o.hair); for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2f; Vector3 d = new Vector3(Mathf.Cos(a) * 0.5f, 0.9f, Mathf.Sin(a) * 0.5f).normalized; hr.Tube(new Vector3(0, 0.14f * s, 0) + d * 0.08f * s, new Vector3(0, 0.14f * s, 0) + d * 0.26f * s, 0.03f * s, 0.003f, o.hair, 4, false); } }
            int arms = 2 + Mathf.Max(0, o.extraArms);
            for (int ai = 0; ai < arms; ai++)
            {
                int sd = ai % 2 == 0 ? -1 : 1; int tier = ai / 2; string an = ai < 2 ? (sd < 0 ? "armL" : "armR") : "arm" + ai; string fn = ai < 2 ? (sd < 0 ? "foreL" : "foreR") : "fore" + ai; float y = (0.56f - tier * 0.15f) * s;
                m.P(an, "torso", new Vector3(sd * 0.34f * s, y, 0)); m.P(fn, an, new Vector3(sd * 0.1f * s, -0.32f * s, 0.02f * s));
                var us = m.Lit(an, ai < 2 && o.coat ? o.cloth : o.skin, 0.4f); us.Tube(Vector3.zero, new Vector3(sd * 0.1f, -0.32f, 0.02f) * s, 0.075f * s, 0.062f * s, o.coat ? o.cloth : o.skin, 7, false); us.Ball(Vector3.zero, 0.08f * s, o.coat ? o.cloth : o.skin, 6, 8);
                var fs = m.Lit(fn, o.skin, 0.35f); fs.Tube(Vector3.zero, new Vector3(sd * 0.04f, -0.3f, 0.06f) * s, 0.06f * s, 0.045f * s, o.skin, 7, false); fs.Ell(new Vector3(sd * 0.045f, -0.34f, 0.07f) * s, new Vector3(0.05f, 0.07f, 0.04f) * s, o.skin);
            }
            foreach (var sd in new[] { -1, 1 })
            {
                string ln = sd < 0 ? "legL" : "legR"; m.P(ln, "pelvis", new Vector3(sd * 0.11f * s, -0.05f * s, 0)); var l = m.Lit(ln, o.cloth2, 0.4f); float th = ll * 0.5f * s;
                l.Tube(Vector3.zero, new Vector3(0, -th, 0.02f * s), 0.1f * s, 0.075f * s, o.cloth2, 7, false); l.Tube(new Vector3(0, -th, 0.02f * s), new Vector3(0, -ll * s * 0.92f, 0), 0.075f * s, 0.055f * s, o.cloth2, 7, false); l.Ell(new Vector3(0, -ll * s * 0.95f, 0.07f * s), new Vector3(0.07f, 0.05f, 0.13f) * s, C(0.05f, 0.05f, 0.06f));
            }
            return m;
        }

        // ---------- боссы (все координаты умножаются на масштаб скелета s: рост ≈ 2,07·s м) ----------
        public static ModelB Jogo()
        {
            float s = 1.75f; Color lv = C(1f, 0.45f, 0.08f);
            var m = Humanoid("Jogo", new HumOpts { skin = C(0.52f, 0.54f, 0.52f), cloth = C(0.42f, 0.44f, 0.42f), cloth2 = C(0.34f, 0.35f, 0.34f), hair = C(0.2f, 0.2f, 0.2f), scale = s, hairStyle = 3, eye = C(1f, 0.55f, 0.1f) });
            var rock = m.Lit("head", C(0.27f, 0.27f, 0.27f), 0.3f); var lava = m.Glow("head", lv);
            rock.Tube(new Vector3(0, 0.14f, 0) * s, new Vector3(0, 0.5f, 0) * s, 0.26f * s, 0.15f * s, C(0.27f, 0.27f, 0.27f), 10, false); lava.Disc(new Vector3(0, 0.495f, 0) * s, Vector3.up, 0.12f * s, lv, 10);
            lava.Ell(new Vector3(0, 0.1f, 0.16f) * s, new Vector3(0.07f, 0.04f, 0.025f) * s, lv); lava.Ell(new Vector3(-0.1f, -0.045f, 0.14f) * s, new Vector3(0.035f, 0.022f, 0.016f) * s, lv); lava.Ell(new Vector3(0.1f, -0.045f, 0.14f) * s, new Vector3(0.035f, 0.022f, 0.016f) * s, lv);
            var cr = m.Glow("torso", lv); for (int i = 0; i < 7; i++) cr.Box(new Vector3(-0.2f + i * 0.067f, 0.3f + Mathf.Sin(i * 1.7f) * 0.1f, 0.19f) * s, new Vector3(0.014f, 0.13f, 0.01f) * s, Q(0, 0, Mathf.Sin(i * 2.3f) * 30f), lv);
            return m;
        }
        public static ModelB Hanami()
        {
            float s = 1.95f; Color bark = C(0.28f, 0.2f, 0.14f), skin = C(0.6f, 0.68f, 0.7f), leaf = C(0.2f, 0.5f, 0.2f), pink = C(0.95f, 0.45f, 0.6f);
            var m = Humanoid("Hanami", new HumOpts { skin = skin, cloth = C(0.35f, 0.26f, 0.18f), cloth2 = bark, hair = leaf, scale = s, hairStyle = 3, eye = C(0.9f, 1f, 0.4f), extraArms = 2 });
            var bk = m.Lit("head", bark, 0.3f); var lf = m.Lit("head", leaf, 0.4f); var fl = m.Lit("head", pink, 0.4f);
            for (int i = 0; i < 7; i++) { float a = i / 7f * Mathf.PI * 2f; Vector3 d = new Vector3(Mathf.Cos(a) * 0.6f, 0.8f, Mathf.Sin(a) * 0.6f).normalized; Vector3 e = (d * 0.34f + Vector3.up * 0.12f) * s; bk.Tube(d * 0.14f * s, e, 0.035f * s, 0.008f * s, bark, 5, false); lf.Ell(e, new Vector3(0.08f, 0.025f, 0.06f) * s, Quaternion.LookRotation(d), leaf); }
            for (int i = 0; i < 5; i++) { float a = i / 5f * Mathf.PI * 2f; fl.Ell(new Vector3(Mathf.Cos(a) * 0.06f, 0.24f, Mathf.Sin(a) * 0.06f) * s, new Vector3(0.06f, 0.02f, 0.035f) * s, Q(0, -a * Mathf.Rad2Deg, 20), pink); }
            var rt = m.Lit("pelvis", bark, 0.3f); for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2f; rt.Tube(new Vector3(0, -0.05f, 0) * s, new Vector3(Mathf.Cos(a) * 0.55f, -0.9f, Mathf.Sin(a) * 0.55f) * s, 0.1f * s, 0.015f * s, bark, 6, false); }
            m.Lit("torso", bark, 0.3f).Ell(new Vector3(0, 0.3f, 0.15f) * s, new Vector3(0.11f, 0.2f, 0.06f) * s, bark);
            return m;
        }
        public static ModelB Mahito()
        {
            float s = 1.1f; Color dk = C(0.08f, 0.08f, 0.11f), bl = C(0.72f, 0.76f, 0.85f);
            var m = Humanoid("Mahito", new HumOpts { skin = C(0.62f, 0.66f, 0.76f), cloth = C(0.35f, 0.4f, 0.55f), cloth2 = C(0.25f, 0.3f, 0.42f), hair = C(0.35f, 0.4f, 0.6f), scale = s, hairStyle = 1, eye = C(0.9f, 0.5f, 0.8f) });
            var st = m.Lit("torso", dk, 0.3f); for (int i = 0; i < 6; i++) st.Box(new Vector3(-0.18f + i * 0.07f, 0.4f, 0.19f) * s, new Vector3(0.008f, 0.17f, 0.008f) * s, Q(0, 0, 10f), dk); st.Box(new Vector3(0, 0.55f, 0.2f) * s, new Vector3(0.5f, 0.01f, 0.008f) * s, dk);
            var sm = m.Lit("head", dk, 0.3f); sm.Box(new Vector3(0, -0.085f, 0.158f) * s, new Vector3(0.14f, 0.01f, 0.01f) * s, dk); sm.Box(new Vector3(0, 0.1f, 0.15f) * s, new Vector3(0.2f, 0.008f, 0.008f) * s, dk);
            var bd = m.Lit("foreR", bl, 0.4f); bd.Tube(new Vector3(0.04f, -0.32f, 0.04f) * s, new Vector3(0.04f, -0.32f, 0.04f) * s + Vector3.forward * 1.1f, 0.06f * s, 0.004f, bl, 5, false);
            return m;
        }
        public static ModelB Sukuna()
        {
            float s = 1.25f; Color tt = C(0.05f, 0.04f, 0.05f), rd = C(1f, 0.15f, 0.1f);
            var m = Humanoid("Sukuna", new HumOpts { skin = C(0.85f, 0.72f, 0.65f), cloth = C(0.9f, 0.88f, 0.84f), cloth2 = C(0.15f, 0.14f, 0.16f), hair = C(1f, 0.55f, 0.65f), scale = s, hairStyle = 2, eye = rd, extraArms = 2, coat = true });
            var tk = m.Lit("head", tt, 0.3f); tk.Box(new Vector3(0, -0.03f, 0.176f) * s, new Vector3(0.26f, 0.008f, 0.008f) * s, tt); tk.Box(new Vector3(0, 0.12f, 0.17f) * s, new Vector3(0.012f, 0.09f, 0.008f) * s, tt); tk.Box(new Vector3(0, -0.1f, 0.168f) * s, new Vector3(0.012f, 0.06f, 0.008f) * s, tt);
            var e2 = m.Glow("head", rd); e2.Ell(new Vector3(-0.1f, -0.045f, 0.14f) * s, new Vector3(0.035f, 0.016f, 0.015f) * s, rd); e2.Ell(new Vector3(0.1f, -0.045f, 0.14f) * s, new Vector3(0.035f, 0.016f, 0.015f) * s, rd);
            return m;
        }
        public static ModelB Gojo()
        {
            return Humanoid("Gojo", new HumOpts { skin = C(0.95f, 0.85f, 0.8f), cloth = C(0.06f, 0.07f, 0.12f), cloth2 = C(0.06f, 0.07f, 0.12f), hair = C(0.95f, 0.97f, 1f), scale = 1.25f, hairStyle = 0, eye = C(0.5f, 0.85f, 1f), blindfold = true, coat = true });
        }

        // ---------- призванные ----------
        public static ModelB Nue()
        {
            var m = new ModelB("Nue"); m.P("body"); Color f = C(0.12f, 0.12f, 0.2f), a = C(0.9f, 0.92f, 1f);
            var b = m.Lit("body", f, 0.5f); b.Ell(Vector3.zero, new Vector3(0.3f, 0.28f, 0.7f), f); b.Ell(new Vector3(0, 0.12f, 0.75f), new Vector3(0.24f, 0.24f, 0.26f), f); b.Tube(new Vector3(0, -0.05f, -0.6f), new Vector3(0, -0.1f, -1.4f), 0.12f, 0.02f, f, 6, false);
            var bk = m.Lit("body", C(1f, 0.85f, 0.25f), 0.3f); bk.Tube(new Vector3(0, 0.1f, 0.95f), new Vector3(0, 0.0f, 1.2f), 0.08f, 0.005f, C(1f, 0.85f, 0.25f), 6, false);
            m.Glow("body", C(0.8f, 0.9f, 1f)).Ell(new Vector3(-0.1f, 0.18f, 0.93f), new Vector3(0.05f, 0.05f, 0.03f), C(0.8f, 0.9f, 1f)); m.Glow("body", C(0.8f, 0.9f, 1f)).Ell(new Vector3(0.1f, 0.18f, 0.93f), new Vector3(0.05f, 0.05f, 0.03f), C(0.8f, 0.9f, 1f));
            foreach (var sd in new[] { -1, 1 })
            {
                string wn = sd < 0 ? "wingL" : "wingR"; m.P(wn, "body", new Vector3(sd * 0.25f, 0.1f, 0.1f)); var w = m.Lit(wn, f, 0.4f); var wl = m.Glow(wn, C(0.6f, 0.8f, 1f));
                for (int i = 0; i < 6; i++) { float t = i / 5f; Vector3 end = new Vector3(sd * (1.2f + t * 1.7f), 0.15f - t * 0.3f, -0.3f - t * 0.9f); w.Tube(new Vector3(0, 0, 0.2f - t * 0.4f), end, 0.1f - t * 0.04f, 0.015f, f, 5, false); wl.Tube(end * 0.85f, end, 0.015f, 0.004f, C(0.6f, 0.8f, 1f), 4, false); }
            }
            return m;
        }
        public static ModelB Mahoraga()
        {
            float s = 2.4f; Color sk = C(0.82f, 0.82f, 0.85f), dk = C(0.55f, 0.56f, 0.62f), gd = C(1f, 0.85f, 0.4f);
            var m = Humanoid("Mahoraga", new HumOpts { skin = sk, cloth = C(0.7f, 0.7f, 0.76f), cloth2 = dk, hair = sk, scale = s, hairStyle = 3, eye = C(0.4f, 0.9f, 1f), extraArms = 2, legLen = 0.9f });
            var hd = m.Lit("head", dk, 0.3f); hd.Ell(new Vector3(0, 0.0f, 0.05f) * s, new Vector3(0.19f, 0.22f, 0.15f) * s, dk); hd.Tube(new Vector3(-0.1f, 0.15f, 0) * s, new Vector3(-0.2f, 0.4f, -0.06f) * s, 0.04f * s, 0.006f * s, sk, 5, false); hd.Tube(new Vector3(0.1f, 0.15f, 0) * s, new Vector3(0.2f, 0.4f, -0.06f) * s, 0.04f * s, 0.006f * s, sk, 5, false);
            m.P("wheel", "head", new Vector3(0, 0.42f, -0.02f) * s); var w = m.Glow("wheel", gd); w.Torus(Vector3.zero, Vector3.up, 0.3f * s, 0.018f * s, gd, 24, 6); for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2f; w.Tube(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.3f * s, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.42f * s, 0.018f * s, 0.008f * s, gd, 4, false); }
            var sw = m.Lit("foreR", C(0.75f, 0.78f, 0.9f), 0.7f, 0.8f); sw.Tube(new Vector3(0.04f, -0.32f, 0.05f) * s, new Vector3(0.04f, -0.32f, 0.05f) * s + Vector3.forward * 2.2f, 0.07f, 0.012f, C(0.75f, 0.78f, 0.9f), 6, true);
            return m;
        }
        public static ModelB Clone()
        {
            return Humanoid("Clone", new HumOpts { skin = C(0.88f, 0.7f, 0.62f), cloth = C(0.1f, 0.1f, 0.18f), cloth2 = C(0.12f, 0.12f, 0.2f), hair = C(0.95f, 0.5f, 0.55f), scale = 0.9f, hairStyle = 2, eye = C(1f, 0.6f, 0.3f), coat = true });
        }
    }
}
