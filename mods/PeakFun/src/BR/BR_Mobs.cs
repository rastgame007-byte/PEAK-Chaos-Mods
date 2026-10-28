// Новые монстры Backrooms (1.21): Бактерия, Комок, Окно, Дуллер, Безликие, Горемыка, Тварь с Уровня 7, Дозорный.
// Часть класса Ents: модели, поведение (на хосте) и анимация (у всех).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static partial class Ents
    {
        public static float LureUntil; public static Vector3 LureP;
        public static void Lure(Vector3 p, float secs) { LureP = p; LureUntil = Time.time + secs; }
        public static float SpeedRef = 6f; public static bool LocalLoud;
        /// <summary>Порог «шумного» движения: подстраивается под максимальную скорость игрока (бег), чтобы не зависеть от точных чисел игры.</summary>
        public static float LoudSpd { get { return Mathf.Clamp(SpeedRef * 0.78f, 3.3f, 5.0f); } }
        public static bool Loud(PL p) { return p != null && !p.crouch && p.spd > LoudSpd; }
        public static bool Awake(Ent e) { return Time.time - LevelT >= e.delay; }
        public static bool HasHearing(LevelDef L) { return L.bacteria > 0 || L.clumps > 0 || L.things > 0 || L.dullers > 0; }

        static bool IsNew(string k) { return k == "bact" || k == "clump" || k == "window" || k == "duller" || k == "face" || k == "wretch" || k == "thing" || k == "watch"; }

        // ================= размещение =================
        static int Cnt(int n, float sc) { return n <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(n * sc)); }

        static bool SpotWall(Built b, List<Vector3> used, float minSep, int minDist, float inset, out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero; yaw = 0f; var g = b.g;
            for (int k = 0; k < 600; k++)
            {
                var c = b.cells[rnd.Next(b.cells.Count)]; if (g.dist[c.x, c.y] < minDist) continue;
                int pass = 0; var walls = new List<int>(); for (int d = 0; d < 4; d++) { if (g.Pass(c.x, c.y, d)) pass++; else walls.Add(d); }
                if (pass < 2 || walls.Count == 0) continue;
                int w = walls[rnd.Next(walls.Count)]; Vector3 dir = new Vector3(Grid.DX[w], 0f, Grid.DY[w]); Vector3 p = b.CellC(c.x, c.y) + dir * (b.C * 0.5f - inset);
                bool ok = true; foreach (var u in used) if (Util.Flat(u - p).magnitude < minSep) { ok = false; break; }
                if (!ok) continue; pos = p; yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg; used.Add(p); return true;
            }
            return false;
        }

        static void SpawnNew(Built b, float sc, Func<int, int, Vector3> pickFar, int far, Func<string, Vector3, int, Ent> add)
        {
            var L = b.L; var used = new List<Vector3>();
            for (int i = 0; i < Cnt(L.bacteria, Mathf.Max(1f, sc * 0.7f)); i++) { var e = add("bact", pickFar(far, 100000), 0); e.delay = L.bactDelay; }
            for (int i = 0; i < Cnt(L.clumps, sc); i++) { var e = add("clump", pickFar(Mathf.Max(8, far / 2), 100000), 0); e.height = L.H - 1.05f; }
            int wc = Cnt(L.windows, sc);
            for (int i = 0; i < wc; i++) { Vector3 p; float yaw; if (!SpotWall(b, used, 7f, 5, 0.175f + 0.02f, out p, out yaw)) break; var e = add("window", p, 0); e.yaw = e.tyaw = yaw; e.fixY = true; }
            int dc = Cnt(L.dullers, sc);
            for (int i = 0; i < dc; i++)
            {
                if (L.dullHunt) { add("duller", pickFar(Mathf.Max(8, far / 2), 100000), 1); continue; }
                Vector3 p; float yaw; if (!SpotWall(b, used, 9f, 6, 0f, out p, out yaw)) break; var e = add("duller", p, 0); e.yaw = e.tyaw = yaw; e.fixY = true;
            }
            int fa = Cnt(L.faces, sc); for (int i = 0; i < fa; i++) add("face", pickFar(5, 100000), 0);
            int kc = Cnt(L.kids, sc); Vector3 k0 = Vector3.zero;
            for (int i = 0; i < kc; i++) { if (i % 3 == 0) k0 = pickFar(Mathf.Max(8, far / 2), 100000); Vector3 p = k0 + new Vector3((rnd.Next(5) - 2) * 0.6f, 0, (rnd.Next(5) - 2) * 0.6f); int cx, cy; if (!b.CellOf(p, out cx, out cy) || b.g.dist[cx, cy] < 0) p = k0; var e = add("face", p, 1); e.pack = i / 3; }
            int wr = Cnt(L.wretches, sc); Vector3 w0 = Vector3.zero;
            for (int i = 0; i < wr; i++) { if (i % 3 == 0) w0 = pickFar(Mathf.Max(8, far / 2), 100000); Vector3 p = w0 + new Vector3((rnd.Next(5) - 2) * 0.6f, 0, (rnd.Next(5) - 2) * 0.6f); int cx, cy; if (!b.CellOf(p, out cx, out cy) || b.g.dist[cx, cy] < 0) p = w0; var e = add("wretch", p, 0); e.pack = i / 3; }
            for (int i = 0; i < Cnt(L.things, Mathf.Max(1f, sc * 0.8f)); i++) add("thing", pickFar(far, 100000), 0);
            for (int i = 0; i < Cnt(L.watchers, sc); i++) add("watch", pickFar(Mathf.Max(8, far / 2), 100000), 0);
        }

        // ================= модели =================
        static Material Mt(Color c, float sm = 0.3f, float me = 0f) { return BrGfx.Surf(null, c, sm, me); }

        static void BuildNew(Ent e)
        {
            switch (e.kind)
            {
                case "bact": BuildBact(e); e.radius = 1.7f; break;
                case "clump": BuildClump(e); e.radius = 1.8f; break;
                case "window": BuildWindow(e); e.radius = 2.6f; break;
                case "duller": BuildDuller(e); e.radius = e.sub == 1 ? 1.6f : 1.9f; break;
                case "face": BuildFace(e); e.radius = e.sub == 1 ? 1.1f : 0f; break;
                case "wretch": BuildWretch(e); e.radius = 1.6f; break;
                case "thing": BuildThing(e); e.radius = 2.3f; break;
                default: BuildWatch(e); e.radius = 2.4f; break;
            }
        }

        static void BuildBact(Ent e)
        {
            var T = e.root.transform; var blk = Mt(new Color(0.025f, 0.025f, 0.03f), 0.35f); var hole = Glow(new Color(0.8f, 0.9f, 1f), 1.2f); var bone = Mt(new Color(0.12f, 0.12f, 0.13f), 0.25f);
            var hips = Jt(T, "hips", new Vector3(0, 1.25f, 0)); e.j[0] = hips; Part(hips, PrimitiveType.Cube, Vector3.zero, new Vector3(0.2f, 0.16f, 0.12f), blk);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f; var th = Jt(hips, "th" + i, new Vector3(s * 0.09f, -0.05f, 0)); e.j[1 + i] = th; Part(th, PrimitiveType.Cube, new Vector3(0, -0.3f, 0), new Vector3(0.05f, 0.62f, 0.05f), blk);
                var kn = Jt(th, "kn" + i, new Vector3(0, -0.62f, 0)); e.j[7 + i] = kn; Part(kn, PrimitiveType.Cube, new Vector3(0, -0.3f, 0), new Vector3(0.04f, 0.62f, 0.04f), blk); Part(kn, PrimitiveType.Cube, new Vector3(0, -0.62f, 0.07f), new Vector3(0.07f, 0.03f, 0.2f), blk);
            }
            var ch = Jt(hips, "chest", new Vector3(0, 0.08f, 0)); e.j[3] = ch; Part(ch, PrimitiveType.Cube, new Vector3(0, 0.42f, 0), new Vector3(0.1f, 0.84f, 0.08f), blk);
            for (int i = 0; i < 5; i++) Part(ch, PrimitiveType.Cube, new Vector3(0, 0.3f + i * 0.1f, 0), new Vector3(0.34f - i * 0.02f, 0.014f, 0.1f), bone);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f; var sh = Jt(ch, "sh" + i, new Vector3(s * 0.2f, 0.82f, 0)); e.j[4 + i] = sh; Part(sh, PrimitiveType.Cube, new Vector3(0, -0.36f, 0), new Vector3(0.04f, 0.72f, 0.04f), blk);
                var el = Jt(sh, "el" + i, new Vector3(0, -0.72f, 0)); e.j[9 + i] = el; Part(el, PrimitiveType.Cube, new Vector3(0, -0.42f, 0), new Vector3(0.035f, 0.84f, 0.035f), blk);
                for (int f = 0; f < 3; f++) Part(el, PrimitiveType.Cube, new Vector3((f - 1) * 0.03f, -0.92f, 0.02f), new Vector3(0.01f, 0.16f, 0.01f), blk);
            }
            var hd = Jt(ch, "head", new Vector3(0, 0.95f, 0)); e.j[6] = hd; Part(hd, PrimitiveType.Sphere, new Vector3(0, 0.05f, 0), new Vector3(0.36f, 0.34f, 0.36f), blk);
            for (int i = 0; i < 7; i++) { float a = i * 0.9f; Part(hd, PrimitiveType.Sphere, new Vector3(Mathf.Sin(a) * 0.12f, 0.05f + Mathf.Cos(a * 1.7f) * 0.09f, 0.12f + Mathf.Cos(a) * 0.04f), new Vector3(0.045f, 0.045f, 0.03f), hole); }
        }

        static void BuildClump(Ent e)
        {
            var T = e.root.transform; var skin = Mt(new Color(0.55f, 0.4f, 0.34f), 0.4f); var dark = Mt(new Color(0.07f, 0.02f, 0.02f), 0.2f); var tooth = Mt(new Color(0.9f, 0.88f, 0.75f), 0.4f); var skin2 = Mt(new Color(0.62f, 0.5f, 0.42f), 0.4f);
            var body = Jt(T, "body", new Vector3(0, 1f, 0)); e.j[0] = body; Part(body, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.95f, 0.85f, 0.95f), skin);
            var mouth = Jt(body, "mouth", new Vector3(0, -0.05f, 0.4f)); e.j[1] = mouth; Part(mouth, PrimitiveType.Sphere, new Vector3(0, 0, 0.04f), new Vector3(0.45f, 0.3f, 0.2f), dark);
            for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI * 2f; Part(mouth, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.17f, Mathf.Sin(a) * 0.11f, 0.13f), new Vector3(0.025f, 0.07f, 0.025f), tooth, new Vector3(0, 0, a * Mathf.Rad2Deg + 90f)); }
            for (int i = 0; i < 16; i++)
            {
                Vector3 d = new Vector3((float)rnd.NextDouble() * 2f - 1f, (float)rnd.NextDouble() * 2f - 1f, (float)rnd.NextDouble() * 2f - 1f); if (d.sqrMagnitude < 0.05f) d = Vector3.right; d.Normalize(); if (d.z > 0.7f) d.z = 0.3f; d.Normalize();
                var l = Jt(body, "lm" + i, d * 0.38f); l.localRotation = Quaternion.LookRotation(d); e.j[2 + i] = l; float len = 0.6f + (float)rnd.NextDouble() * 0.5f;
                Part(l, PrimitiveType.Cube, new Vector3(0, 0, len * 0.5f), new Vector3(0.09f, 0.09f, len), i % 2 == 0 ? skin : skin2); Part(l, PrimitiveType.Sphere, new Vector3(0, 0, len), new Vector3(0.14f, 0.1f, 0.2f), skin2);
            }
            var bait = Jt(body, "bait", new Vector3(0.1f, -0.2f, 0.1f)); e.j[18] = bait; Part(bait, PrimitiveType.Cube, new Vector3(0, -1.1f, 0), new Vector3(0.075f, 2.2f, 0.075f), skin2); Part(bait, PrimitiveType.Sphere, new Vector3(0, -2.25f, 0), new Vector3(0.16f, 0.22f, 0.1f), skin2);
            for (int f = 0; f < 4; f++) Part(bait, PrimitiveType.Cube, new Vector3((f - 1.5f) * 0.04f, -2.4f, 0), new Vector3(0.015f, 0.14f, 0.015f), skin2);
        }

        static void BuildWindow(Ent e)
        {
            var T = e.root.transform; var frame = Mt(new Color(0.78f, 0.78f, 0.74f), 0.3f); var glass = Glow(new Color(0.16f, 0.2f, 0.28f), 0.7f); var blk = Mt(new Color(0.01f, 0.01f, 0.012f), 0.2f); var eye = Glow(new Color(1f, 0.15f, 0.1f), 2f);
            float cy = 1.75f, w = 1.5f, h = 1.7f; float fz = 0.05f;
            Part(T, PrimitiveType.Cube, new Vector3(0, cy + h * 0.5f + 0.06f, fz), new Vector3(w + 0.3f, 0.12f, 0.16f), frame); Part(T, PrimitiveType.Cube, new Vector3(0, cy - h * 0.5f - 0.06f, fz + 0.05f), new Vector3(w + 0.4f, 0.12f, 0.26f), frame);
            Part(T, PrimitiveType.Cube, new Vector3(-w * 0.5f - 0.06f, cy, fz), new Vector3(0.12f, h + 0.24f, 0.16f), frame); Part(T, PrimitiveType.Cube, new Vector3(w * 0.5f + 0.06f, cy, fz), new Vector3(0.12f, h + 0.24f, 0.16f), frame);
            Part(T, PrimitiveType.Cube, new Vector3(0, cy, fz), new Vector3(0.05f, h, 0.1f), frame);
            var gl = Part(T, PrimitiveType.Cube, new Vector3(0, cy, 0f), new Vector3(w, h, 0.04f), glass); e.j[3] = gl;
            var fig = Jt(T, "fig", new Vector3(0, 0, -0.1f)); e.j[0] = fig;
            Part(fig, PrimitiveType.Cube, new Vector3(0, 1.55f, 0), new Vector3(0.5f, 0.8f, 0.06f), blk); Part(fig, PrimitiveType.Sphere, new Vector3(0, 2.12f, 0), new Vector3(0.28f, 0.36f, 0.08f), blk);
            Part(fig, PrimitiveType.Sphere, new Vector3(-0.06f, 2.15f, 0.04f), new Vector3(0.045f, 0.03f, 0.02f), eye); Part(fig, PrimitiveType.Sphere, new Vector3(0.06f, 2.15f, 0.04f), new Vector3(0.045f, 0.03f, 0.02f), eye);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f; var hnd = Jt(fig, "hand" + i, new Vector3(s * 0.3f, 1.85f, 0)); e.j[1 + i] = hnd; Part(hnd, PrimitiveType.Cube, new Vector3(0, 0, 0.5f), new Vector3(0.1f, 0.1f, 1f), blk);
                for (int f = 0; f < 4; f++) Part(hnd, PrimitiveType.Cube, new Vector3((f - 1.5f) * 0.035f, 0, 1.06f), new Vector3(0.012f, 0.012f, 0.14f), blk);
            }
        }

        static void BuildDuller(Ent e)
        {
            var T = e.root.transform; var blk = Mt(new Color(0.015f, 0.015f, 0.02f), 0.55f);
            var hips = Jt(T, "hips", new Vector3(0, 1.1f, 0)); e.j[0] = hips; Part(hips, PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, 0.2f, 0.16f), blk);
            for (int i = 0; i < 2; i++) { float s = i == 0 ? -1f : 1f; var l = Jt(hips, "lg" + i, new Vector3(s * 0.1f, -0.05f, 0)); e.j[1 + i] = l; Part(l, PrimitiveType.Cube, new Vector3(0, -0.5f, 0), new Vector3(0.09f, 1.05f, 0.09f), blk); }
            var ch = Jt(hips, "chest", new Vector3(0, 0.1f, 0)); e.j[3] = ch; Part(ch, PrimitiveType.Cube, new Vector3(0, 0.4f, 0), new Vector3(0.36f, 0.85f, 0.15f), blk);
            var hd = Jt(ch, "head", new Vector3(0, 0.95f, 0)); e.j[6] = hd; Part(hd, PrimitiveType.Sphere, new Vector3(0, 0.05f, 0), new Vector3(0.22f, 0.3f, 0.22f), blk);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f; var sh = Jt(T, "arm" + i, new Vector3(s * 0.26f, 1.65f, 0)); e.j[4 + i] = sh; Part(sh, PrimitiveType.Cube, new Vector3(0, 0, 1.1f), new Vector3(0.07f, 0.07f, 2.2f), blk);
                for (int f = 0; f < 4; f++) Part(sh, PrimitiveType.Cube, new Vector3((f - 1.5f) * 0.035f, 0, 2.35f), new Vector3(0.015f, 0.015f, 0.34f), blk);
            }
            if (e.sub == 0) e.j[0].gameObject.SetActive(false);
        }

        struct HS { public Color skin, top, pants; public float scale, arm, leg, thick; public bool sockets, knife, ribs; }
        static void BuildHuman(Ent e, HS h)
        {
            var T = e.root.transform; T.localScale = Vector3.one * h.scale; var skin = Mt(h.skin, 0.35f); var top = Mt(h.top, 0.2f); var pants = Mt(h.pants, 0.2f); var dark = Mt(new Color(0.01f, 0.01f, 0.01f), 0.2f); float th = h.thick;
            e.by = h.leg; var hips = Jt(T, "hips", new Vector3(0, h.leg, 0)); e.j[0] = hips; Part(hips, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.3f * th, 0.2f, 0.2f * th), pants);
            for (int i = 0; i < 2; i++) { float s = i == 0 ? -1f : 1f; var l = Jt(hips, "l" + i, new Vector3(s * 0.1f * th, 0, 0)); e.j[1 + i] = l; Part(l, PrimitiveType.Cube, new Vector3(0, -h.leg * 0.5f, 0), new Vector3(0.11f * th, h.leg, 0.11f * th), pants); Part(l, PrimitiveType.Cube, new Vector3(0, -h.leg + 0.03f, 0.05f), new Vector3(0.11f * th, 0.06f, 0.24f), dark); }
            var ch = Jt(hips, "chest", new Vector3(0, 0.05f, 0)); e.j[3] = ch; Part(ch, PrimitiveType.Cube, new Vector3(0, 0.38f, 0), new Vector3(0.38f * th, 0.78f, 0.2f * th), top);
            if (h.ribs) for (int i = 0; i < 5; i++) Part(ch, PrimitiveType.Cube, new Vector3(0, 0.22f + i * 0.1f, 0.095f * th), new Vector3(0.3f * th, 0.018f, 0.03f), Mt(new Color(0.75f, 0.66f, 0.6f), 0.3f));
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f; var a = Jt(ch, "a" + i, new Vector3(s * 0.23f * th, 0.7f, 0)); e.j[4 + i] = a; Part(a, PrimitiveType.Cube, new Vector3(0, -h.arm * 0.22f, 0), new Vector3(0.08f * th, h.arm * 0.44f, 0.08f * th), top); Part(a, PrimitiveType.Cube, new Vector3(0, -h.arm * 0.72f, 0), new Vector3(0.06f * th, h.arm * 0.56f, 0.06f * th), skin); Part(a, PrimitiveType.Sphere, new Vector3(0, -h.arm, 0), new Vector3(0.1f, 0.14f, 0.07f), skin);
                if (h.knife && i == 1) Part(a, PrimitiveType.Cube, new Vector3(0, -h.arm, 0.14f), new Vector3(0.02f, 0.03f, 0.3f), Mt(new Color(0.7f, 0.72f, 0.75f), 0.8f, 0.8f));
            }
            var hd = Jt(ch, "head", new Vector3(0, 0.9f, 0)); e.j[6] = hd; Part(hd, PrimitiveType.Sphere, new Vector3(0, 0.05f, 0), new Vector3(0.27f * th, 0.34f, 0.27f * th), skin);
            if (h.sockets) { Part(hd, PrimitiveType.Sphere, new Vector3(-0.07f, 0.1f, 0.1f), new Vector3(0.08f, 0.1f, 0.06f), dark); Part(hd, PrimitiveType.Sphere, new Vector3(0.07f, 0.1f, 0.1f), new Vector3(0.08f, 0.1f, 0.06f), dark); Part(hd, PrimitiveType.Cube, new Vector3(0, -0.07f, 0.12f), new Vector3(0.09f, 0.12f, 0.03f), dark); }
        }
        static readonly Color[] tops = { new Color(0.55f, 0.15f, 0.12f), new Color(0.2f, 0.3f, 0.5f), new Color(0.75f, 0.7f, 0.5f), new Color(0.25f, 0.4f, 0.25f), new Color(0.6f, 0.6f, 0.62f), new Color(0.5f, 0.3f, 0.5f) };
        static void BuildFace(Ent e)
        {
            Color skin = Color.Lerp(new Color(0.85f, 0.7f, 0.6f), new Color(0.65f, 0.5f, 0.4f), (float)rnd.NextDouble()); Color top = tops[rnd.Next(tops.Length)]; Color pants = rnd.Next(2) == 0 ? new Color(0.18f, 0.22f, 0.35f) : new Color(0.3f, 0.26f, 0.2f);
            BuildHuman(e, new HS { skin = skin, top = top, pants = pants, scale = e.sub == 1 ? 0.68f : 1.04f, arm = 0.85f, leg = 0.95f, thick = 1f, knife = e.sub == 1 });
        }
        static void BuildWretch(Ent e)
        {
            Color skin = Color.Lerp(new Color(0.5f, 0.32f, 0.3f), new Color(0.42f, 0.3f, 0.3f), (float)rnd.NextDouble());
            BuildHuman(e, new HS { skin = skin, top = skin * 0.85f, pants = new Color(0.16f, 0.14f, 0.12f), scale = 1.02f, arm = 1.15f, leg = 0.95f, thick = 0.72f, sockets = true, ribs = true });
        }

        static void BuildThing(Ent e)
        {
            var T = e.root.transform; var skin = Mt(new Color(0.035f, 0.06f, 0.08f), 0.8f); var belly = Mt(new Color(0.35f, 0.4f, 0.42f), 0.6f); var eye = Glow(new Color(0.85f, 0.95f, 1f), 1.6f); var tooth = Mt(new Color(0.85f, 0.85f, 0.75f), 0.5f); var dark = Mt(new Color(0.01f, 0.01f, 0.01f), 0.2f);
            var hd = Jt(T, "head", Vector3.zero); e.j[0] = hd; Part(hd, PrimitiveType.Sphere, Vector3.zero, new Vector3(1.5f, 1.1f, 1.8f), skin);
            Part(hd, PrimitiveType.Sphere, new Vector3(-0.55f, 0.2f, 0.65f), new Vector3(0.4f, 0.4f, 0.3f), eye); Part(hd, PrimitiveType.Sphere, new Vector3(0.55f, 0.2f, 0.65f), new Vector3(0.4f, 0.4f, 0.3f), eye);
            Part(hd, PrimitiveType.Sphere, new Vector3(-0.55f, 0.2f, 0.78f), new Vector3(0.14f, 0.14f, 0.1f), dark); Part(hd, PrimitiveType.Sphere, new Vector3(0.55f, 0.2f, 0.78f), new Vector3(0.14f, 0.14f, 0.1f), dark);
            for (int i = 0; i < 5; i++) Part(hd, PrimitiveType.Cube, new Vector3(0, 0.62f + i * 0.02f, -0.4f + i * 0.28f), new Vector3(0.08f, 0.5f - i * 0.05f, 0.18f), skin, new Vector3(-12f, 0, 0));
            var jaw = Jt(hd, "jaw", new Vector3(0, -0.4f, 0.4f)); e.j[1] = jaw; Part(jaw, PrimitiveType.Cube, new Vector3(0, 0, 0.1f), new Vector3(1.0f, 0.12f, 1.0f), belly);
            for (int i = 0; i < 14; i++) { float a = i / 13f * Mathf.PI; float xx = Mathf.Cos(a) * 0.45f, zz = Mathf.Sin(a) * 0.5f; Part(jaw, PrimitiveType.Cube, new Vector3(xx, 0.12f, 0.1f + zz), new Vector3(0.05f, 0.2f, 0.05f), tooth); Part(hd, PrimitiveType.Cube, new Vector3(xx, -0.34f, 0.5f + zz), new Vector3(0.05f, 0.2f, 0.05f), tooth); }
            for (int i = 0; i < 10; i++)
            {
                var sg = Jt(T, "seg" + i, Vector3.zero); e.j[2 + i] = sg; float r = Mathf.Lerp(1.2f, 0.4f, i / 9f); Part(sg, PrimitiveType.Sphere, Vector3.zero, new Vector3(r * 1.5f, r * 1.2f, r * 1.7f), skin);
                Part(sg, PrimitiveType.Cube, new Vector3(0, r * 0.62f, 0), new Vector3(0.07f, r * 0.55f, r * 0.7f), skin, new Vector3(-10f, 0, 0)); if (i % 2 == 0) Part(sg, PrimitiveType.Sphere, new Vector3(0, -r * 0.2f, 0), new Vector3(r * 1.1f, r * 0.7f, r * 1.3f), belly);
            }
        }

        static void BuildWatch(Ent e)
        {
            var T = e.root.transform; var ball = Mt(new Color(0.92f, 0.86f, 0.8f), 0.7f); var iris = Mt(new Color(0.45f, 0.3f, 0.1f), 0.6f); var pup = Glow(new Color(1f, 0.95f, 0.7f), 3.2f); var vein = Mt(new Color(0.55f, 0.08f, 0.08f), 0.3f);
            var leg = Mt(new Color(0.28f, 0.38f, 0.2f), 0.3f); var blood = Mt(new Color(0.3f, 0.04f, 0.04f), 0.3f);
            var body = Jt(T, "body", new Vector3(0, 2.3f, 0)); e.j[0] = body; Part(body, PrimitiveType.Sphere, Vector3.zero, new Vector3(1.7f, 1.7f, 1.7f), ball);
            Part(body, PrimitiveType.Sphere, new Vector3(0, 0, 0.66f), new Vector3(1.0f, 1.0f, 0.3f), iris); Part(body, PrimitiveType.Sphere, new Vector3(0, 0, 0.8f), new Vector3(0.42f, 0.42f, 0.12f), pup);
            for (int i = 0; i < 9; i++) { float a = i * 0.7f; Part(body, PrimitiveType.Cube, new Vector3(Mathf.Sin(a) * 0.55f, Mathf.Cos(a) * 0.55f, 0.4f), new Vector3(0.015f, 0.35f, 0.015f), vein, new Vector3(0, 0, -a * Mathf.Rad2Deg)); }
            Part(body, PrimitiveType.Sphere, new Vector3(0, -0.45f, -0.3f), new Vector3(1.4f, 0.8f, 1.1f), blood);
            var lg = new GameObject("beam"); lg.transform.SetParent(body, false); lg.transform.localPosition = new Vector3(0, 0, 0.85f); var li = lg.AddComponent<Light>(); li.type = LightType.Spot; li.spotAngle = 42f; li.range = 26f; li.intensity = 4.5f; li.color = new Color(1f, 0.93f, 0.7f); li.shadows = LightShadows.None; e.lt = li;
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? 1f : -1f; var up = Jt(body, "lg" + i, new Vector3(sx * 0.45f, -0.6f, sz * 0.4f)); e.j[1 + i] = up; Part(up, PrimitiveType.Cube, new Vector3(sx * 0.2f, -0.6f, 0), new Vector3(0.11f, 1.3f, 0.11f), leg, new Vector3(0, 0, sx * 12f));
                var lo = Jt(up, "lo", new Vector3(sx * 0.4f, -1.2f, 0)); e.j[5 + i] = lo; Part(lo, PrimitiveType.Cube, new Vector3(0, -0.4f, 0), new Vector3(0.08f, 0.9f, 0.08f), blood); Part(lo, PrimitiveType.Cube, new Vector3(0, -0.85f, 0), new Vector3(0.28f, 0.07f, 0.28f), leg);
                for (int f = 0; f < 4; f++) Part(lo, PrimitiveType.Cube, new Vector3((f - 1.5f) * 0.07f, -0.9f, 0.15f), new Vector3(0.025f, 0.03f, 0.2f), leg);
            }
        }

        // ================= навигация-помощники =================
        static void WanderAny(Ent e, float dt, float spd)
        {
            e.spd = spd; e.stateT += dt; float wd = Util.Flat(e.wander - e.pos).magnitude;
            if (!e.hasWander || wd < 1.3f || e.stateT > 15f) { var c = B.cells[rnd.Next(B.cells.Count)]; e.wander = B.CellC(c.x, c.y); e.hasWander = true; e.stateT = 0f; }
            Move(e, e.wander, spd, dt);
        }
        static void WanderNear(Ent e, float dt, float spd, float radius)
        {
            e.spd = spd; e.stateT += dt; float wd = Util.Flat(e.wander - e.pos).magnitude;
            if (!e.hasWander || wd < 1.0f || e.stateT > 12f)
            {
                e.hasWander = true; e.stateT = 0f; e.wander = e.home;
                for (int k = 0; k < 14; k++) { var c = B.cells[rnd.Next(B.cells.Count)]; Vector3 p = B.CellC(c.x, c.y); if (Util.Flat(p - e.home).magnitude < radius * B.C) { e.wander = p; break; } }
            }
            Move(e, e.wander, spd, dt);
        }
        static PL Ear(List<PL> pls, Vector3 from, float range)
        {
            PL best = null; float bd = range; foreach (var p in pls) { if (!Loud(p)) continue; float d = Util.Flat(p.pos - from).magnitude; if (d < bd) { bd = d; best = p; } }
            return best;
        }

        // ================= симуляция (хост) =================
        static bool SimNew(Ent e, float dt, PL near, float nd, bool los, bool grace, List<PL> pls)
        {
            switch (e.kind)
            {
                case "bact": SimBact(e, dt, near, nd, los, grace, pls); return true;
                case "clump": SimClump(e, dt, near, nd, los, grace, pls); return true;
                case "window": SimWindow(e, dt, near, nd, los, grace); return true;
                case "duller": SimDuller(e, dt, near, nd, los, grace, pls); return true;
                case "face": SimFace(e, dt, near, nd, los, grace, pls); return true;
                case "wretch": SimWretch(e, dt, near, nd, los, grace, pls); return true;
                case "thing": SimThing(e, dt, near, nd, los, grace, pls); return true;
                case "watch": SimWatch(e, dt, near, nd, los, grace); return true;
            }
            return false;
        }

        static void SimBact(Ent e, float dt, PL near, float nd, bool los, bool grace, List<PL> pls)
        {
            if (!Awake(e)) { e.spd = 0f; return; }
            if (e.t3 == 0f) { e.t3 = 1f; if (e.delay > 0f) Relocate(e, pls, 28f); }
            PL ear = Ear(pls, e.pos, 34f);
            switch (e.st)
            {
                case 0:
                    WanderAny(e, dt, 2.1f);
                    if (!grace)
                    {
                        if (los && nd < 30f && near != null) { e.st = 2; e.stateT = 0f; e.lost = 0f; BrSnd.Scream(e.pos, 0.9f); }
                        else if (ear != null) { e.st = 1; e.aux = ear.pos; e.stateT = 0f; Snd2.At("hiss", e.pos, 0.5f); }
                    }
                    break;
                case 1:
                    e.spd = 3.5f * DiffV; e.stateT += dt; Move(e, e.aux, e.spd, dt); if (ear != null) e.aux = ear.pos;
                    if (los && nd < 30f && near != null && !grace) { e.st = 2; e.stateT = 0f; e.lost = 0f; BrSnd.Scream(e.pos, 0.9f); }
                    else if (Util.Flat(e.aux - e.pos).magnitude < 2f || e.stateT > 12f) { e.st = 0; e.hasWander = false; e.stateT = 0f; }
                    break;
                default:
                    if (near == null) { e.st = 0; break; }
                    e.stateT += dt; e.spd = Mathf.Min((4.7f + e.anger * 0.7f) * DiffV, 5.9f); Move(e, near.pos, e.spd, dt);
                    if (!los && ear == null) e.lost += dt; else e.lost = 0f;
                    if (e.lost > 8f || e.stateT > 60f) { e.st = 0; e.hasWander = false; e.lost = 0f; e.stateT = 0f; }
                    break;
            }
        }

        static void SimClump(Ent e, float dt, PL near, float nd, bool los, bool grace, List<PL> pls)
        {
            float H = B.L.H; PL ear = Ear(pls, e.pos, 18f);
            switch (e.st)
            {
                case 0:
                    e.spd = 0f; e.height = Mathf.Lerp(e.height, H - 1.05f, dt * 2f);
                    if (!grace && near != null)
                    {
                        if (nd < 3.6f && los) { e.st = 2; e.stateT = 0f; Snd2.At("hiss", e.pos, 0.9f); }
                        else if (ear != null) { e.st = 1; e.aux = ear.pos; e.stateT = 0f; }
                    }
                    break;
                case 1:
                    e.spd = 2.8f * DiffV; e.stateT += dt; e.height = Mathf.Lerp(e.height, 0.8f, dt * 3f); Move(e, e.aux, e.spd, dt); if (ear != null) e.aux = ear.pos;
                    if (near != null && nd < 7f && los) { e.st = 2; e.stateT = 0f; Snd2.At("hiss", e.pos, 0.8f); }
                    else if (Util.Flat(e.aux - e.pos).magnitude < 1.5f || e.stateT > 9f) { e.st = 3; e.stateT = 0f; }
                    break;
                case 2:
                    if (near == null) { e.st = 3; break; }
                    e.stateT += dt; e.spd = Mathf.Min(6.0f * DiffV, 6.2f); e.height = Mathf.Lerp(e.height, 0.75f, dt * 5f); Move(e, near.pos, e.spd, dt);
                    if (e.stateT > 6.5f || (!los && e.stateT > 2f)) { e.st = 3; e.stateT = 0f; }
                    break;
                default:
                    e.spd = 3.2f; e.height = Mathf.Lerp(e.height, H - 1.05f, dt * 0.8f); Move(e, e.home, e.spd, dt);
                    if (Util.Flat(e.home - e.pos).magnitude < 0.7f) { e.st = 0; e.spd = 0f; }
                    break;
            }
        }

        static void SimWindow(Ent e, float dt, PL near, float nd, bool los, bool grace)
        {
            e.spd = 0f; if (near == null || grace) { e.st = 0; return; }
            Vector3 fwd = Quaternion.Euler(0f, e.yaw, 0f) * Vector3.forward; Vector3 to = Util.Flat(near.pos - e.pos); float wd = to.magnitude; bool front = wd > 0.01f && Vector3.Dot(fwd, to / wd) > 0.1f;
            if (e.st == 2) { e.t2 -= dt; if (e.t2 <= 0f) e.st = (front && wd < 10f) ? 1 : 0; return; }
            if (front && los && wd < 10f) { e.st = 1; if (wd < 2.4f) { e.st = 2; e.t2 = 1.3f; Snd2.At("hiss", e.pos, 0.9f); } } else e.st = 0;
        }

        static void SimDuller(Ent e, float dt, PL near, float nd, bool los, bool grace, List<PL> pls)
        {
            if (e.sub == 1) { SimDullHunt(e, dt, near, nd, los, grace, pls); return; }
            e.spd = 0f; e.stateT += dt; Vector3 fwd = Quaternion.Euler(0f, e.yaw, 0f) * Vector3.forward; PL tgt = null; float td = 1e9f;
            foreach (var p in pls) { if (p.crouch || p.spd < 1.6f) continue; Vector3 to = Util.Flat(p.pos - e.pos); float d = to.magnitude; if (d > 0.01f && Vector3.Dot(fwd, to / d) < -0.1f) continue; if (d < td) { td = d; tgt = p; } }
            switch (e.st)
            {
                case 0:
                    if (!grace && tgt != null && td < 4.3f) { e.st = 1; e.stateT = 0f; e.t2 = 0f; Snd2.At("hiss", e.pos + Vector3.up, 0.4f); }
                    break;
                case 1:
                    if (tgt != null && td < 1.9f && !grace) { e.st = 2; e.stateT = 0f; Snd2.At("hiss", e.pos + Vector3.up, 0.9f); }
                    else if (tgt == null || td > 5.2f) { e.t2 += dt; if (e.t2 > 3f) { e.st = 0; e.t2 = 0f; } }
                    else e.t2 = 0f;
                    break;
                default:
                    if (e.stateT > 1.1f) { e.st = 0; e.t2 = 0f; }
                    break;
            }
        }
        static void SimDullHunt(Ent e, float dt, PL near, float nd, bool los, bool grace, List<PL> pls)
        {
            switch (e.st)
            {
                case 0:
                    e.spd = 0f; if (near != null && !grace && nd < 26f && los) { e.st = 1; e.stateT = 0f; e.t2 = 0f; e.lost = 0f; }
                    break;
                case 1:
                    if (near == null) { e.st = 0; break; }
                    if (Seen(e) && nd < 40f)
                    {
                        e.t2 += dt; e.spd = 1.1f; Face(e, near.pos, dt); Move(e, e.home, e.spd, dt, true);
                        if (e.t2 > 2.5f) { e.st = 2; e.stateT = 0f; e.t2 = 0f; Relocate(e, pls, 22f); e.home = e.pos; }
                    }
                    else { e.t2 = Mathf.Max(0f, e.t2 - dt * 0.5f); e.spd = Mathf.Min(5.3f * DiffV, 5.9f); Move(e, near.pos, e.spd, dt); }
                    if (!los) e.lost += dt; else e.lost = 0f; if (e.lost > 9f) { e.st = 0; e.spd = 0f; }
                    break;
                default:
                    e.spd = 0f; e.stateT += dt; if (e.stateT > 8f) e.st = 0;
                    break;
            }
        }

        static void SimFace(Ent e, float dt, PL near, float nd, bool los, bool grace, List<PL> pls)
        {
            if (e.sub == 0)
            {
                if (e.st == 0)
                {
                    WanderAny(e, dt, 1.3f);
                    if (near != null && nd < 8f && los && rnd.NextDouble() < dt * 0.35) { e.st = 1; e.stateT = 0f; }
                }
                else
                {
                    e.spd = 0f; e.stateT += dt; if (near != null) Face(e, near.pos, dt);
                    if (near == null || e.stateT > 5f + (e.id % 3) || nd > 14f) { e.st = 0; e.stateT = 0f; e.hasWander = false; }
                }
                return;
            }
            switch (e.st)
            {
                case 0:
                    e.spd = 0f; if (near != null && !grace && ((nd < 13f && los && !near.crouch) || nd < 4f))
                    {
                        foreach (var o in All) if (o.kind == "face" && o.sub == 1 && o.st != 1 && (o.pos - e.pos).magnitude < 14f) { o.st = 1; o.calmT = 0f; o.lost = 0f; }
                        Snd2.At("hiss", e.pos, 0.6f);
                    }
                    break;
                case 1:
                    if (near == null) { e.st = 2; break; }
                    if (grace) { e.spd = 0f; Face(e, near.pos, dt); break; }
                    e.spd = Mathf.Min(4.7f * DiffV, 5.4f); Move(e, near.pos, e.spd, dt);
                    if (near.crouch && nd > 3f) e.calmT += dt; else e.calmT = 0f; if (!los) e.lost += dt; else e.lost = 0f;
                    if (e.calmT > 3f || e.lost > 7f) e.st = 2;
                    break;
                default:
                    e.spd = 2.2f; Move(e, e.home, e.spd, dt); if (Util.Flat(e.home - e.pos).magnitude < 0.5f) { e.st = 0; e.spd = 0f; }
                    if (near != null && !grace && nd < 6f && los && !near.crouch) e.st = 1;
                    break;
            }
        }

        static void SimWretch(Ent e, float dt, PL near, float nd, bool los, bool grace, List<PL> pls)
        {
            switch (e.st)
            {
                case 0:
                    WanderNear(e, dt, 1.2f, 4f); if (rnd.NextDouble() < dt * 0.12) Snd2.At("hiss", e.pos, 0.3f);
                    if (near != null && !grace && ((nd < 15f && los) || nd < 6f))
                    {
                        foreach (var o in All) if (o.kind == "wretch" && o.st == 0 && (o.pos - e.pos).magnitude < 22f) { o.st = 1; o.stateT = 0f; o.lost = 0f; }
                        e.st = 1; e.stateT = 0f; e.lost = 0f; Snd2.At("growl", e.pos, 0.9f);
                    }
                    break;
                case 1:
                    if (near == null) { e.st = 2; break; }
                    e.stateT += dt; e.spd = Mathf.Min((4.6f + e.anger * 0.8f) * DiffV, 5.8f); Move(e, near.pos, e.spd, dt);
                    if (!los) e.lost += dt; else e.lost = 0f; float hd = Util.Flat(e.home - e.pos).magnitude;
                    if (e.lost > 8f || e.stateT > 40f || hd > 55f) e.st = 2;
                    break;
                default:
                    e.spd = 3f; Move(e, e.home, e.spd, dt); if (Util.Flat(e.home - e.pos).magnitude < 1f) { e.st = 0; e.spd = 0f; e.hasWander = false; }
                    break;
            }
        }

        static void SimThing(Ent e, float dt, PL near, float nd, bool los, bool grace, List<PL> pls)
        {
            PL ear = Ear(pls, e.pos, 26f);
            if (e.st == 0)
            {
                WanderAny(e, dt, 1.8f);
                if (!grace && near != null && (nd < 7f || ear != null || (los && nd < 12f && !near.crouch))) { e.st = 1; e.stateT = 0f; e.lost = 0f; Snd2.At("growl", e.pos, 1f); }
            }
            else
            {
                if (near == null) { e.st = 0; return; }
                e.stateT += dt; e.spd = Mathf.Min((5.0f + e.anger * 0.5f) * DiffV, 5.9f); Move(e, near.pos, e.spd, dt);
                if (!los && ear == null) e.lost += dt; else e.lost = 0f;
                if (e.lost > 8f || e.stateT > 50f || (near.crouch && nd > 6f && e.lost > 3f)) { e.st = 0; e.hasWander = false; e.stateT = 0f; }
            }
        }

        static void SimWatch(Ent e, float dt, PL near, float nd, bool los, bool grace)
        {
            float sweep = Mathf.Sin(Time.time * 0.7f + e.id) * 38f;
            switch (e.st)
            {
                case 0:
                    WanderAny(e, dt, 1.7f);
                    if (near != null && !grace && los && nd < 34f)
                    {
                        Vector3 to = Util.Flat(near.pos - e.pos); float ang = Vector3.Angle(Quaternion.Euler(0f, e.yaw + sweep, 0f) * Vector3.forward, to);
                        if (ang < 50f || nd < 8f) { e.st = 1; e.growlT = 1.3f; e.stateT = 0f; Snd2.At("hiss", e.pos, 1f); Snd2.At("growl", e.pos, 0.8f); }
                    }
                    break;
                case 1:
                    e.spd = 0f; e.growlT -= dt; if (near != null) Face(e, near.pos, dt); if (e.growlT <= 0f) { e.st = 2; e.lost = 0f; e.stateT = 0f; }
                    break;
                default:
                    if (near == null) { e.st = 0; break; }
                    e.stateT += dt; e.spd = Mathf.Min(5.4f * DiffV, 6.1f); Move(e, near.pos, e.spd, dt);
                    if (!los) e.lost += dt; else e.lost = 0f; if (e.lost > 6f || e.stateT > 45f) { e.st = 0; e.hasWander = false; e.stateT = 0f; }
                    break;
            }
        }

        // ================= правила поимки =================
        static bool CanCatchNew(Ent e)
        {
            switch (e.kind)
            {
                case "bact": return Awake(e) && e.st == 2;
                case "clump": return e.st == 1 || e.st == 2;
                case "window": return e.st == 2;
                case "duller": return e.sub == 1 ? e.st == 1 : e.st == 2;
                case "face": return e.sub == 1 && e.st == 1;
                case "wretch": return e.st == 1;
                case "thing": return e.st == 1;
                case "watch": return e.st == 2;
            }
            return true;
        }
        static void AfterCatchNew(Ent e, List<PL> pls)
        {
            e.catchCd = Time.time + 10f; e.spd = 0f; e.stateT = 0f;
            switch (e.kind)
            {
                case "window": e.st = 0; break;
                case "duller": e.st = 0; if (e.sub == 1) Relocate(e, pls, 22f); break;
                case "clump": e.st = 3; break;
                case "face": case "wretch": e.st = 0; e.pos = e.home; e.tpos = e.home; break;
                default: e.st = 0; Relocate(e, pls, 22f); break;
            }
        }
        static bool Threatening(Ent e)
        {
            switch (e.kind)
            {
                case "bact": return Awake(e) && e.st > 0;
                case "clump": return e.st > 0;
                case "window": return e.st > 0;
                case "duller": return e.st > 0;
                case "face": return e.sub == 1 && e.st == 1;
                case "wretch": return e.st == 1;
                case "thing": return e.st == 1;
                case "watch": return e.st > 0;
            }
            return false;
        }
        static string KindNameNew(string k)
        {
            switch (k)
            {
                case "bact": return "Бактерия"; case "clump": return "Комок"; case "window": return "Окно"; case "duller": return "Дуллер"; case "face": return "Безликий"; case "wretch": return "Горемыка"; case "thing": return "Тварь с Уровня 7"; case "watch": return "Дозорный";
            }
            return null;
        }
        public static string KindName(string k) { return KindNameNew(k); }

        // ================= анимация =================
        static void AnimNew(Ent e, float dt, Vector3 cp, float tm, float dist)
        {
            switch (e.kind)
            {
                case "bact": AnimBact(e, dt, tm); break;
                case "clump": AnimClump(e, dt, tm, dist); break;
                case "window": AnimWindow(e, dt, tm, dist); break;
                case "duller": AnimDuller(e, dt, tm); break;
                case "face": AnimHuman(e, dt, tm, e.sub == 1 ? 11f : 6f, e.sub == 1 ? 55f : 32f, e.st == 1 && e.sub == 1 ? 16f : 3f, e.sub == 1 ? 6f : 14f); break;
                case "wretch": AnimHuman(e, dt, tm, e.st == 1 ? 12f : 4f, e.st == 1 ? 60f : 22f, e.st == 1 ? 38f : 24f, 40f); break;
                case "thing": AnimThing(e, dt, tm, dist); break;
                case "watch": AnimWatch(e, dt, tm, dist); break;
            }
        }

        static void AnimBact(Ent e, float dt, float tm)
        {
            if (e.j[0] == null) return; float sp = e.spd; float run = Mathf.Clamp01(sp / 5f); bool mv = sp > 0.2f; bool ch = e.st == 2; e.ph += dt * (mv ? 2.2f + sp * 1.7f : 0f); float ph = e.ph;
            e.j[0].localPosition = new Vector3(0, 1.25f + Mathf.Abs(Mathf.Sin(ph)) * 0.05f * (0.4f + run), 0);
            e.j[3].localRotation = Quaternion.Euler((ch ? 22f : 6f) + Mathf.Sin(ph * 2f) * 3f, Mathf.Sin(ph) * 7f, 0);
            float jit = Mathf.PerlinNoise(tm * 6f, e.id) - 0.5f; e.j[6].localRotation = Quaternion.Euler(jit * (e.st == 0 ? 24f : 12f) + (ch ? 8f : 0f), (Mathf.PerlinNoise(tm * 2f, e.id + 5f) - 0.5f) * (e.st == 1 ? 100f : 55f), jit * 14f);
            for (int i = 0; i < 2; i++)
            {
                float off = i == 0 ? 0f : Mathf.PI; float s = Mathf.Sin(ph + off); e.j[1 + i].localRotation = Quaternion.Euler(s * 38f * run, 0, 0); e.j[7 + i].localRotation = Quaternion.Euler(Mathf.Max(0f, -Mathf.Cos(ph + off)) * 55f * run, 0, 0);
                e.j[4 + i].localRotation = Quaternion.Euler(-s * 30f * run + (ch ? 35f : 0f), 0, (i == 0 ? 1f : -1f) * (6f + Mathf.Sin(tm * 1.7f + i) * 3f)); e.j[9 + i].localRotation = Quaternion.Euler(-20f - run * 25f, 0, 0);
            }
        }

        static void AnimClump(Ent e, float dt, float tm, float dist)
        {
            if (e.j[0] == null) return; bool act = e.st == 1 || e.st == 2; float sp = e.spd; float k = act ? 1f : 0.25f;
            e.j[0].localPosition = new Vector3(0, e.height + Mathf.Abs(Mathf.Sin(tm * (act ? 10f : 1.4f))) * (act ? 0.12f : 0.02f), 0); float pulse = 1f + Mathf.Sin(tm * 8f) * 0.04f; e.j[0].localScale = new Vector3(pulse, 1f / pulse, pulse);
            if (act) e.j[0].Rotate(Vector3.right, sp * 70f * dt, Space.Self);
            else e.j[0].localRotation = Quaternion.Slerp(e.j[0].localRotation, Quaternion.identity, dt * 3f);
            for (int i = 0; i < 16; i++) { var l = e.j[2 + i]; if (l == null) continue; float a = Mathf.Sin(tm * (act ? 9f : 2f) + i * 1.7f) * 22f * k; l.localRotation = Quaternion.LookRotation(l.localPosition.normalized) * Quaternion.Euler(a, a * 0.6f, 0); }
            if (e.j[1] != null) e.j[1].localScale = new Vector3(1f, 1f + (act ? Mathf.Abs(Mathf.Sin(tm * 12f)) * 0.5f : 0f), 1f);
            if (e.j[18] != null) { float len = act ? 0.35f : 1f; var sc = e.j[18].localScale; sc.y = Mathf.Lerp(sc.y, len, dt * 6f); e.j[18].localScale = sc; e.j[18].localRotation = Quaternion.Euler(Mathf.Sin(tm * 0.9f) * 7f, 0, Mathf.Sin(tm * 0.7f + 1f) * 9f); }
        }

        static void AnimWindow(Ent e, float dt, float tm, float dist)
        {
            if (e.j[0] == null) return; bool grab = e.st == 2; bool knock = e.st == 1 || (e.st == 0 && dist < 18f);
            float rate = e.st == 1 ? 0.9f : 0.33f; float cyc = Mathf.Repeat(tm * rate, 1f); float tap = knock && cyc < 0.3f ? Mathf.Max(0f, Mathf.Sin(cyc / 0.3f * Mathf.PI * 6f)) : 0f;
            e.j[0].localPosition = new Vector3(Mathf.Sin(tm * 0.7f) * 0.03f, 0f, -0.1f + (grab ? 0.14f : 0f));
            for (int i = 0; i < 2; i++)
            {
                var h = e.j[1 + i]; float len = grab ? 2.3f : 0.1f + (i == 1 ? tap * 0.04f : 0f); var sc = h.localScale; sc.z = Mathf.Lerp(sc.z, len, dt * (grab ? 14f : 8f)); h.localScale = sc;
                h.localRotation = Quaternion.Euler(grab ? 0f : (i == 1 ? -8f * tap : 0f), grab ? (i == 0 ? 8f : -8f) : 0f, 0f);
            }
            if (e.j[3] != null && e.j[3].gameObject.activeSelf == grab) e.j[3].gameObject.SetActive(!grab);
            if (dist < 15f && knock && cyc < 0.04f && Time.time > e.seenLocal) { e.seenLocal = Time.time + (e.st == 1 ? 1.2f : 3.2f); Snd2.At("knock", e.root.transform.position + Vector3.up * 1.7f, Mathf.Clamp01(1f - dist / 15f) * 0.9f); }
        }

        static void AnimDuller(Ent e, float dt, float tm)
        {
            if (e.sub == 0)
            {
                float target = e.st == 0 ? 0f : (e.st == 1 ? 0.7f : 1f); e.t3 = Mathf.MoveTowards(e.t3, target, dt * (e.st == 2 ? 6f : 1.6f)); bool show = e.t3 > 0.03f;
                for (int i = 0; i < 2; i++)
                {
                    var a = e.j[4 + i]; if (a == null) continue; if (a.gameObject.activeSelf != show) a.gameObject.SetActive(show); if (!show) continue;
                    float sw = Mathf.Sin(tm * 1.3f + i * 2.1f) * (e.st == 1 ? 38f : (e.st == 2 ? 6f : 12f)); float close = e.st == 2 ? (i == 0 ? 10f : -10f) : 0f;
                    a.localRotation = Quaternion.Euler(8f + Mathf.Sin(tm * 0.9f + i) * 6f, sw + close, 0); a.localScale = new Vector3(1f, 1f, Mathf.Max(0.02f, e.t3));
                }
                return;
            }
            float sp = e.spd; bool mv = sp > 0.2f; float run = Mathf.Clamp01(sp / 5f); e.ph += dt * (mv ? 3f + sp * 1.6f : 0f); float ph = e.ph;
            e.j[0].localPosition = new Vector3(0, 1.1f + Mathf.Abs(Mathf.Sin(ph)) * 0.05f * run, 0); e.j[3].localRotation = Quaternion.Euler(8f + run * 14f, Mathf.Sin(ph) * 8f, 0); e.j[6].localRotation = Quaternion.Euler(e.st == 1 && sp < 2f ? -18f : 8f, Mathf.Sin(tm * 2f) * 8f, 0);
            for (int i = 0; i < 2; i++) { float s = Mathf.Sin(ph + (i == 0 ? 0f : Mathf.PI)); e.j[1 + i].localRotation = Quaternion.Euler(s * 36f * run, 0, 0); e.j[4 + i].localRotation = Quaternion.Euler(80f - s * 28f * run, 0, 0); e.j[4 + i].localScale = new Vector3(1f, 1f, 0.7f); }
        }

        static void AnimHuman(Ent e, float dt, float tm, float rate, float swing, float hunch, float headJit)
        {
            if (e.j[0] == null) return; float sp = e.spd; bool mv = sp > 0.15f; float k = Mathf.Clamp01(sp / 3f); e.ph += dt * (mv ? rate * (0.8f + sp * 0.35f) : rate * 0.12f); float ph = e.ph;
            e.j[0].localPosition = new Vector3(0, e.by + Mathf.Abs(Mathf.Sin(ph)) * 0.045f * k, 0);
            bool wr = e.kind == "wretch"; bool chase = e.st == 1;
            e.j[3].localRotation = Quaternion.Euler(hunch + Mathf.Sin(ph * 2f) * 2f * k + (wr && !mv ? Mathf.Sin(tm * 1.1f + e.id) * 3f : 0f), Mathf.Sin(ph) * 5f * k, 0);
            float jit = Mathf.PerlinNoise(tm * (wr ? 7f : 1.2f), e.id * 3.1f) - 0.5f;
            e.j[6].localRotation = Quaternion.Euler(jit * headJit * 0.6f, (Mathf.PerlinNoise(tm * 1.5f, e.id + 9f) - 0.5f) * headJit * (chase ? 0.4f : 1.4f), jit * headJit * (wr ? 0.8f : 0.2f));
            for (int i = 0; i < 2; i++)
            {
                float s = Mathf.Sin(ph + (i == 0 ? 0f : Mathf.PI)); e.j[1 + i].localRotation = Quaternion.Euler(s * swing * k, 0, 0);
                float armX = -s * swing * 0.8f * k; if (wr && chase) armX = 55f + s * 20f; else if (wr) armX += 6f + Mathf.Sin(tm * 2f + i * 3f) * 4f;
                e.j[4 + i].localRotation = Quaternion.Euler(armX, 0, (i == 0 ? 1f : -1f) * (wr ? 10f : 3f));
            }
        }

        static void TrailPush(Ent e)
        {
            if (e.trail != null && (e.trail[0] - e.pos).sqrMagnitude > 36f) e.trail = null;
            if (e.trail == null) { e.trail = new List<Vector3>(); for (int i = 0; i < 4; i++) e.trail.Add(e.pos); }
            if ((e.trail[0] - e.pos).sqrMagnitude > 0.16f) e.trail.Insert(0, e.pos); else e.trail[0] = e.pos;
            if (e.trail.Count > 80) e.trail.RemoveAt(e.trail.Count - 1);
        }
        static Vector3 TrailAt(List<Vector3> tr, float dist)
        {
            float acc = 0f; for (int i = 0; i + 1 < tr.Count; i++) { float seg = (tr[i] - tr[i + 1]).magnitude; if (seg < 0.0001f) continue; if (acc + seg >= dist) return Vector3.Lerp(tr[i], tr[i + 1], (dist - acc) / seg); acc += seg; }
            return tr[tr.Count - 1];
        }
        static void AnimThing(Ent e, float dt, float tm, float dist)
        {
            if (e.j[0] == null) return; TrailPush(e); float by = B.baseP.y; bool hunt = e.st == 1; float rise = hunt ? 0.55f : 0.0f; e.t3 = Mathf.Lerp(e.t3, rise, dt * 2f);
            e.j[0].position = new Vector3(e.pos.x, by + 0.35f + e.t3 + Mathf.Sin(tm * 1.8f) * 0.08f, e.pos.z); e.j[0].rotation = Quaternion.Euler(hunt ? 8f : 0f, e.yaw, 0);
            e.j[1].localRotation = Quaternion.Euler(hunt ? 28f + Mathf.Sin(tm * 6f) * 6f : 6f, 0, 0);
            for (int i = 0; i < 10; i++)
            {
                var sg = e.j[2 + i]; if (sg == null) continue; Vector3 p = TrailAt(e.trail, 1.5f + i * 1.25f); float wave = Mathf.Sin(tm * 1.6f - i * 0.8f); float r = Mathf.Lerp(1.2f, 0.4f, i / 9f);
                float y = by + 0.1f + (wave * 0.5f + 0.45f) * (hunt ? 0.9f : 0.55f) * (1f - i * 0.04f) - 0.35f; sg.position = new Vector3(p.x, y, p.z);
                Vector3 nx = TrailAt(e.trail, 1.5f + i * 1.25f - 0.8f); Vector3 d = nx - p; d.y = 0f; if (d.sqrMagnitude > 0.001f) sg.rotation = Quaternion.LookRotation(d);
            }
        }

        static void AnimWatch(Ent e, float dt, float tm, float dist)
        {
            if (e.j[0] == null) return; float sp = e.spd; bool mv = sp > 0.2f; float run = Mathf.Clamp01(sp / 5.5f); e.ph += dt * (mv ? 2.5f + sp * 1.5f : 0f); float ph = e.ph; bool pose = e.st == 1; bool ch = e.st == 2;
            float h = pose ? 1.75f : (ch ? 2.0f : 2.3f); e.t3 = Mathf.Lerp(e.t3 == 0f ? 2.3f : e.t3, h, dt * 5f);
            e.j[0].localPosition = new Vector3(0, e.t3 + Mathf.Abs(Mathf.Sin(ph)) * 0.06f * (0.4f + run), 0);
            float sweep = e.st == 0 ? Mathf.Sin(Time.time * 0.7f + e.id) * 38f : 0f; e.j[0].localRotation = Quaternion.Euler(pose ? 22f : (ch ? 10f : 0f), sweep, 0);
            for (int i = 0; i < 4; i++)
            {
                float off = (i == 0 || i == 3) ? 0f : Mathf.PI; float s = Mathf.Sin(ph + off); float sx = i % 2 == 0 ? -1f : 1f;
                e.j[1 + i].localRotation = Quaternion.Euler(s * 26f * run + (pose ? 12f : 0f), 0, sx * (pose ? 30f : 10f)); e.j[5 + i].localRotation = Quaternion.Euler(Mathf.Max(0f, -Mathf.Cos(ph + off)) * 40f * run, 0, 0);
            }
            if (e.lt != null) { e.lt.enabled = dist < 70f; e.lt.intensity = (pose ? 6f : 4.5f) * (0.9f + 0.1f * Mathf.Sin(tm * 17f)); }
            if (mv && dist < 36f && Time.time > e.seenLocal) { e.seenLocal = Time.time + 0.55f / Mathf.Clamp(sp / 3f, 0.6f, 2f); Snd2.At("step", e.root.transform.position, Mathf.Clamp01(1f - dist / 36f) * 0.95f); }
        }
    }
}
