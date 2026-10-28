// Сущности Backrooms: Вор Кожи, Смайлер, Гончая, Гость вечеринки, Мотылёк-Смерть. Симуляция — на хосте, у остальных интерполяция по снимкам.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace PeakBR
{
    internal sealed class PL { public Component c; public int actor; public Vector3 pos; public bool crouch; public float spd; }

    internal sealed class Ent
    {
        public int id; public string kind; public GameObject root; public Vector3 pos, home, tpos; public float yaw, tyaw, spd, anger, radius = 1.6f; public int st;
        public float stateT, catchCd, peekT, stareT, giveUp, growlT, calmT, lost, stepT, ph, flapT; public Vector3 wander; public bool hasWander; public BrMon mon; public float height;
        public Transform[] j = new Transform[24]; public Vector3 prev; public int pack; public float seenLocal;
        public float t2, t3, delay, by; public Vector3 aux; public int sub; public bool fixY; public Light lt; public List<Vector3> trail;
    }

    internal static partial class Ents
    {
        public static readonly List<Ent> All = new List<Ent>(); public static Built B; public static float DiffV = 1f, LevelT; public static float Grace;
        static readonly Dictionary<int, float> seenAt = new Dictionary<int, float>(); static readonly Dictionary<int, int[,]> flows = new Dictionary<int, int[,]>(); static System.Random rnd = new System.Random(1);

        // ================= создание =================
        public static void Clear() { foreach (var e in All) if (e.root != null) UnityEngine.Object.Destroy(e.root); All.Clear(); seenAt.Clear(); flows.Clear(); B = null; }

        public static void Spawn(Built b, int seed, float sizeMul)
        {
            Clear(); B = b; var L = b.L; rnd = new System.Random(seed ^ 0x3ac9); LevelT = Time.time; int id = 0; var g = b.g;
            int maxd = 0; foreach (var c in b.cells) maxd = Mathf.Max(maxd, g.dist[c.x, c.y]);
            Func<int, int, Vector3> pickFar = (lo, hi) =>
            {
                for (int k = 0; k < 400; k++) { var c = b.cells[rnd.Next(b.cells.Count)]; int d = g.dist[c.x, c.y]; if (d >= lo && d <= hi) return b.CellC(c.x, c.y); }
                var f = b.cells[b.cells.Count - 1]; return b.CellC(f.x, f.y);
            };
            // «Беги!»: монстры ТОЛЬКО позади игрока (в хвосте коридора, откуда он стартовал)
            Func<int, int, Vector3> pickBehind = (lo, hi) =>
            {
                if (g.behind == null) return pickFar(lo, hi); var lst = new List<Vector2Int>(); int bd = -1; Vector2Int bc = new Vector2Int(g.sx, g.sy);
                foreach (var c in b.cells) { if (!g.behind[c.x, c.y]) continue; int d = g.dist[c.x, c.y]; if (d >= lo && d <= hi) lst.Add(c); if (d > bd) { bd = d; bc = c; } }
                var pk = lst.Count > 0 ? lst[rnd.Next(lst.Count)] : bc; return b.CellC(pk.x, pk.y);
            };
            int far = Mathf.Max(10, Mathf.RoundToInt(maxd * 0.28f));
            float sc = Mathf.Clamp(sizeMul, 0.7f, 2f); bool chase = L.chaseStart;
            Func<string, Vector3, int, Ent> add2 = (kind, p, sb) => { var e = Make(kind, p, id++, sb); All.Add(e); return e; };
            Action<string, Vector3> add = (kind, p) => { add2(kind, p, 0); };
            for (int i = 0; i < Mathf.Max(L.stealers > 0 ? 1 : 0, Mathf.RoundToInt(L.stealers * sc)); i++) add("stealer", pickFar(far, 100000));
            for (int i = 0; i < Mathf.Max(L.smilers > 0 ? 1 : 0, Mathf.RoundToInt(L.smilers * (chase ? 1f : sc))); i++) add("smiler", chase ? pickBehind(5, 8) : pickFar(far, 100000));
            int hc = Mathf.RoundToInt(L.hounds * sc); Vector3 hc0 = Vector3.zero;
            for (int i = 0; i < hc; i++) { if (i % 3 == 0) hc0 = pickFar(far, 100000); add("hound", hc0 + new Vector3((rnd.Next(5) - 2) * 0.5f, 0, (rnd.Next(5) - 2) * 0.5f)); All[All.Count - 1].pack = i / 3; }
            int pc = chase ? L.party : Mathf.RoundToInt(L.party * sc); Vector3 pc0 = Vector3.zero;
            for (int i = 0; i < pc; i++)
            {
                if (chase) { add("party", pickBehind(Mathf.Min(14, 6 + i), 14)); continue; }
                if (i % 5 == 0) pc0 = pickFar(Mathf.Max(8, far / 2), 100000); Vector3 p = pc0 + new Vector3((rnd.Next(9) - 4) * 0.7f, 0, (rnd.Next(9) - 4) * 0.7f);
                int cx, cy; if (!b.CellOf(p, out cx, out cy) || g.dist[cx, cy] < 0) p = pc0; add("party", p);
            }
            for (int i = 0; i < Mathf.RoundToInt(L.moths * sc); i++) add("moth", pickFar(6, 100000));
            SpawnNew(b, sc, pickFar, far, add2);
            foreach (var e in All) { e.home = e.pos; e.tpos = e.pos; if (!e.fixY) e.yaw = e.tyaw = (float)rnd.NextDouble() * 360f; if (chase && e.kind != "moth") { e.st = e.kind == "party" ? 1 : 0; } e.peekT = U(12f, 24f); }
            Grace = Time.time + (chase ? 4f : 0f);
        }
        static float U(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        static Ent Make(string kind, Vector3 p, int id, int sub = 0)
        {
            var e = new Ent { id = id, kind = kind, pos = p, sub = sub }; e.root = new GameObject("BR_ent_" + kind + "_player"); e.root.transform.position = p;
            switch (kind)
            {
                case "stealer": e.mon = new BrMon(p); e.mon.root.transform.SetParent(e.root.transform, false); e.mon.root.transform.localPosition = Vector3.zero; e.radius = 1.7f; break;
                case "smiler": BuildSmilerV2(e); e.radius = 1.6f; break;
                case "hound": BuildHoundV2(e); e.radius = 1.5f; break;
                case "party": BuildPartyV2(e); e.radius = 1.8f; break;
                case "moth": BuildMothV2(e); e.radius = 0f; break;
                default: BuildNew(e); break;
            }
            return e;
        }

        // ---------- модели ----------
        static Transform Part(Transform par, PrimitiveType t, Vector3 lp, Vector3 ls, Material m, Vector3 euler = default(Vector3))
        {
            var g = GameObject.CreatePrimitive(t); g.name = "BR_part"; if (t == PrimitiveType.Sphere) { var mf0 = g.GetComponent<MeshFilter>(); if (mf0 != null) mf0.sharedMesh = OrgMesh.Blob(((int)(lp.x * 37f + lp.y * 91f + lp.z * 53f)) & 7, 0.05f); } var c = g.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c);
            g.transform.SetParent(par, false); g.transform.localPosition = lp; g.transform.localScale = ls; g.transform.localEulerAngles = euler; var r = g.GetComponent<MeshRenderer>(); if (m != null) r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; return g.transform;
        }
        static Transform Jt(Transform par, string n, Vector3 lp) { var g = new GameObject(n); g.transform.SetParent(par, false); g.transform.localPosition = lp; return g.transform; }
        static Material Glow(Color c, float k) { return BrGfx.Surf(null, c, 0.4f, 0f, new Color(c.r * k, c.g * k, c.b * k)); }

        static void BuildSmiler(Ent e)
        {
            var T = e.root.transform; var blk = BrGfx.Surf(null, new Color(0.01f, 0.01f, 0.012f), 0.6f); var gl = Glow(new Color(1f, 0.98f, 0.85f), 2.8f);
            var body = Jt(T, "body", new Vector3(0, 1.15f, 0)); e.j[0] = body; Part(body, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.75f, 1.0f, 0.55f), blk);
            Part(body, PrimitiveType.Sphere, new Vector3(0, 0.55f, 0.02f), new Vector3(0.62f, 0.62f, 0.55f), blk);
            Part(body, PrimitiveType.Sphere, new Vector3(-0.15f, 0.62f, 0.25f), new Vector3(0.1f, 0.13f, 0.05f), gl); Part(body, PrimitiveType.Sphere, new Vector3(0.15f, 0.62f, 0.25f), new Vector3(0.1f, 0.13f, 0.05f), gl);
            for (int i = 0; i < 13; i++) { float u = (i / 12f) * 2f - 1f; Part(body, PrimitiveType.Sphere, new Vector3(u * 0.22f, 0.45f - 0.1f * (1f - u * u), 0.26f), new Vector3(0.035f, 0.045f, 0.03f), gl); }
            for (int i = 0; i < 6; i++) { float s = i < 3 ? -1f : 1f; float k = i % 3; var l = Jt(body, "leg" + i, new Vector3(s * 0.3f, -0.2f - k * 0.1f, (k - 1f) * 0.15f)); e.j[1 + i] = l; Part(l, PrimitiveType.Cube, new Vector3(s * 0.4f, -0.2f, 0), new Vector3(0.8f, 0.025f, 0.025f), blk, new Vector3(0, 0, -s * 35f)); }
        }

        static void BuildHound(Ent e)
        {
            var T = e.root.transform; var skin = BrGfx.Surf(BrMon.SkinTex(), new Color(0.95f, 0.85f, 0.8f), 0.4f); var tooth = BrGfx.Surf(null, new Color(0.9f, 0.88f, 0.75f), 0.5f); var pale = Glow(new Color(0.95f, 0.97f, 1f), 2.2f); var dark = BrGfx.Surf(null, new Color(0.03f, 0.02f, 0.02f), 0.2f);
            var hips = Jt(T, "hips", new Vector3(0, 0.62f, 0)); e.j[0] = hips; Part(hips, PrimitiveType.Sphere, new Vector3(0, 0.02f, -0.3f), new Vector3(0.45f, 0.42f, 0.55f), skin); Part(hips, PrimitiveType.Sphere, new Vector3(0, 0.06f, 0.25f), new Vector3(0.48f, 0.46f, 0.6f), skin);
            Part(hips, PrimitiveType.Cube, new Vector3(0, 0.1f, -0.05f), new Vector3(0.38f, 0.34f, 0.5f), skin);
            var neck = Jt(hips, "neck", new Vector3(0, 0.15f, 0.5f)); e.j[1] = neck; Part(neck, PrimitiveType.Cube, new Vector3(0, 0.05f, 0.1f), new Vector3(0.22f, 0.22f, 0.4f), skin, new Vector3(-20, 0, 0));
            var head = Jt(neck, "head", new Vector3(0, 0.1f, 0.3f)); e.j[2] = head; Part(head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.34f, 0.32f, 0.36f), skin); Part(head, PrimitiveType.Cube, new Vector3(0, -0.06f, 0.28f), new Vector3(0.2f, 0.16f, 0.34f), skin);
            var jaw = Jt(head, "jaw", new Vector3(0, -0.12f, 0.12f)); e.j[3] = jaw; Part(jaw, PrimitiveType.Cube, new Vector3(0, 0, 0.16f), new Vector3(0.18f, 0.06f, 0.32f), dark);
            for (int i = 0; i < 8; i++) { float s = i % 2 == 0 ? -1f : 1f; float z = 0.1f + (i / 2) * 0.07f; Part(head, PrimitiveType.Cube, new Vector3(s * 0.085f, -0.15f, 0.2f + (i / 2) * 0.07f - 0.02f), new Vector3(0.012f, 0.05f, 0.012f), tooth); Part(jaw, PrimitiveType.Cube, new Vector3(s * 0.08f, 0.04f, z), new Vector3(0.012f, 0.045f, 0.012f), tooth); }
            Part(head, PrimitiveType.Sphere, new Vector3(-0.11f, 0.06f, 0.15f), new Vector3(0.07f, 0.07f, 0.04f), pale); Part(head, PrimitiveType.Sphere, new Vector3(0.11f, 0.06f, 0.15f), new Vector3(0.07f, 0.07f, 0.04f), pale);
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? 0.5f : -0.45f; var up = Jt(hips, "leg" + i, new Vector3(sx * 0.2f, 0.0f, sz)); e.j[4 + i * 2] = up; Part(up, PrimitiveType.Cube, new Vector3(0, -0.2f, 0), new Vector3(0.1f, 0.4f, 0.1f), skin);
                var lo = Jt(up, "lo", new Vector3(0, -0.4f, 0)); e.j[5 + i * 2] = lo; Part(lo, PrimitiveType.Cube, new Vector3(0, -0.15f, 0), new Vector3(0.07f, 0.3f, 0.07f), skin); Part(lo, PrimitiveType.Cube, new Vector3(0, -0.3f, 0.05f), new Vector3(0.1f, 0.04f, 0.18f), skin);
            }
        }

        static void BuildParty(Ent e)
        {
            var T = e.root.transform; Color sk = Color.Lerp(new Color(0.72f, 0.58f, 0.2f), new Color(0.8f, 0.68f, 0.3f), (float)rnd.NextDouble()); var skin = BrGfx.Surf(null, sk, 0.35f); var red = BrGfx.Surf(null, new Color(0.4f, 0.03f, 0.03f), 0.5f); var dark = BrGfx.Surf(null, new Color(0.02f, 0.01f, 0.01f), 0.2f);
            var tooth = BrGfx.Surf(null, new Color(0.9f, 0.88f, 0.7f), 0.4f); var balloon = BrGfx.Surf(null, new Color(0.9f, 0.05f, 0.05f), 0.9f, 0f, new Color(0.25f, 0f, 0f)); var hatM = BrGfx.Surf(null, Color.HSVToRGB((float)rnd.NextDouble(), 0.8f, 0.95f), 0.3f);
            float hs = 1.05f + (float)rnd.NextDouble() * 0.2f; T.localScale = Vector3.one * hs;
            var hips = Jt(T, "hips", new Vector3(0, 0.95f, 0)); e.j[0] = hips; Part(hips, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.3f, 0.2f, 0.2f), skin);
            for (int i = 0; i < 2; i++) { float s = i == 0 ? -1f : 1f; var l = Jt(hips, "l" + i, new Vector3(s * 0.1f, 0, 0)); e.j[1 + i] = l; Part(l, PrimitiveType.Cube, new Vector3(0, -0.45f, 0), new Vector3(0.1f, 0.95f, 0.1f), skin); Part(l, PrimitiveType.Cube, new Vector3(0, -0.93f, 0.05f), new Vector3(0.1f, 0.05f, 0.22f), dark); }
            var chest = Jt(hips, "chest", new Vector3(0, 0.05f, 0)); e.j[3] = chest; Part(chest, PrimitiveType.Cube, new Vector3(0, 0.38f, 0), new Vector3(0.36f, 0.78f, 0.2f), skin); Part(chest, PrimitiveType.Cube, new Vector3(0.05f, 0.35f, 0.105f), new Vector3(0.2f, 0.3f, 0.01f), red);
            for (int i = 0; i < 2; i++) { float s = i == 0 ? -1f : 1f; var a = Jt(chest, "a" + i, new Vector3(s * 0.22f, 0.7f, 0)); e.j[4 + i] = a; Part(a, PrimitiveType.Cube, new Vector3(0, -0.4f, 0), new Vector3(0.07f, 0.85f, 0.07f), skin); Part(a, PrimitiveType.Sphere, new Vector3(0, -0.85f, 0), new Vector3(0.12f, 0.16f, 0.07f), skin); }
            var head = Jt(chest, "head", new Vector3(0, 0.9f, 0)); e.j[6] = head; Part(head, PrimitiveType.Sphere, new Vector3(0, 0.05f, 0), new Vector3(0.26f, 0.34f, 0.26f), skin);
            Part(head, PrimitiveType.Sphere, new Vector3(-0.07f, 0.1f, 0.1f), new Vector3(0.07f, 0.08f, 0.05f), dark); Part(head, PrimitiveType.Sphere, new Vector3(0.07f, 0.1f, 0.1f), new Vector3(0.07f, 0.08f, 0.05f), dark);
            Part(head, PrimitiveType.Cube, new Vector3(0, -0.06f, 0.125f), new Vector3(0.17f, 0.07f, 0.03f), dark); for (int i = 0; i < 7; i++) Part(head, PrimitiveType.Cube, new Vector3((i - 3) * 0.022f, -0.04f, 0.14f), new Vector3(0.012f, 0.03f, 0.01f), tooth);
            Part(head, PrimitiveType.Cylinder, new Vector3(0, 0.28f, 0), new Vector3(0.14f, 0.1f, 0.14f), hatM, new Vector3(0, 0, 0)); Part(head, PrimitiveType.Sphere, new Vector3(0, 0.4f, 0), new Vector3(0.05f, 0.05f, 0.05f), hatM);
            var bal = Jt(e.j[5], "balloon", new Vector3(0, -0.85f, 0)); e.j[7] = bal; Part(bal, PrimitiveType.Cube, new Vector3(0, 0.75f, 0), new Vector3(0.008f, 1.5f, 0.008f), dark); Part(bal, PrimitiveType.Sphere, new Vector3(0, 1.62f, 0), new Vector3(0.36f, 0.44f, 0.36f), balloon);
        }

        static void BuildMoth(Ent e)
        {
            var T = e.root.transform; var wing = BrGfx.Surf(null, new Color(0.85f, 0.8f, 0.65f), 0.3f); var body = BrGfx.Surf(null, new Color(0.3f, 0.25f, 0.2f), 0.3f); var eye = Glow(new Color(1f, 0.2f, 0.1f), 2f);
            var b = Jt(T, "body", new Vector3(0, 1.8f, 0)); e.j[0] = b; Part(b, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.14f, 0.14f, 0.34f), body); Part(b, PrimitiveType.Sphere, new Vector3(0, 0.02f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), body);
            Part(b, PrimitiveType.Sphere, new Vector3(-0.04f, 0.04f, 0.25f), new Vector3(0.04f, 0.04f, 0.03f), eye); Part(b, PrimitiveType.Sphere, new Vector3(0.04f, 0.04f, 0.25f), new Vector3(0.04f, 0.04f, 0.03f), eye);
            for (int i = 0; i < 2; i++) { float s = i == 0 ? -1f : 1f; var w = Jt(b, "w" + i, new Vector3(s * 0.05f, 0.05f, 0.02f)); e.j[1 + i] = w; Part(w, PrimitiveType.Cube, new Vector3(s * 0.28f, 0, -0.05f), new Vector3(0.56f, 0.012f, 0.4f), wing); }
        }

        // ================= навигация =================
        static int[,] Flow(Built b, int tx, int ty)
        {
            int key = tx * 4096 + ty; int[,] d; if (flows.TryGetValue(key, out d)) return d; if (flows.Count > 30) flows.Clear(); d = b.g.Bfs(tx, ty); flows[key] = d; return d;
        }

        static void Move(Ent e, Vector3 goal, float speed, float dt, bool faceGoal = false)
        {
            var b = B; int cx, cy, tx, ty; Vector3 aim = goal;
            if (!b.CellOf(e.pos, out cx, out cy)) { e.pos = b.CellC(Mathf.Clamp(cx, 0, b.n - 1), Mathf.Clamp(cy, 0, b.n - 1)); return; }
            if (b.CellOf(goal, out tx, out ty) && (tx != cx || ty != cy) && b.g.dist[tx, ty] >= 0)
            {
                var d = Flow(b, tx, ty); int best = d[cx, cy], bk = -1;
                for (int k = 0; k < 4; k++) if (b.g.Pass(cx, cy, k)) { int nx = cx + Grid.DX[k], ny = cy + Grid.DY[k]; if (d[nx, ny] >= 0 && d[nx, ny] < best) { best = d[nx, ny]; bk = k; } }
                if (bk >= 0) aim = b.CellC(cx + Grid.DX[bk], cy + Grid.DY[bk]); else if (d[cx, cy] < 0) return;
            }
            Vector3 dv = aim - e.pos; dv.y = 0f; float dist = dv.magnitude;
            if (dist > 0.02f) { float step = Mathf.Min(speed * dt, dist); e.pos += dv / dist * step; float ty2 = Mathf.Atan2(dv.x, dv.z) * Mathf.Rad2Deg; if (!faceGoal) e.yaw = Mathf.MoveTowardsAngle(e.yaw, ty2, 540f * dt); }
        }
        static void Face(Ent e, Vector3 p, float dt) { Vector3 d = p - e.pos; d.y = 0f; if (d.sqrMagnitude > 0.01f) e.yaw = Mathf.MoveTowardsAngle(e.yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f * dt); }

        static void Relocate(Ent e, List<PL> pls, float minD)
        {
            var b = B; Vector3 best = e.pos; float bd = -1f;
            for (int k = 0; k < 24; k++)
            {
                var c = b.cells[rnd.Next(b.cells.Count)]; Vector3 p = b.CellC(c.x, c.y); float nd = 1e9f; foreach (var pl in pls) nd = Mathf.Min(nd, Util.Flat(p - pl.pos).magnitude);
                if (nd >= minD) { best = p; bd = nd; break; } if (nd > bd) { bd = nd; best = p; }
            }
            e.pos = best; e.tpos = best; e.hasWander = false;
        }

        static bool Seen(Ent e) { float t; return seenAt.TryGetValue(e.id, out t) && Time.time - t < 0.7f; }
        public static void MarkSeen(int id) { seenAt[id] = Time.time; }

        // ================= симуляция (хост) =================
        public static void HostTick(float dt, List<PL> pls)
        {
            if (B == null || pls.Count == 0) return; float now = Time.time; var L = B.L; float anger = Mathf.Clamp01(L.anger0 + (now - LevelT) / 320f) ;
            foreach (var e in All)
            {
                PL near = null; float nd = 1e9f; foreach (var p in pls) { float d = Util.Flat(p.pos - e.pos).magnitude; if (d < nd) { nd = d; near = p; } }
                e.anger = anger; Vector3 eye = e.pos + Vector3.up * 1.5f; bool los = near != null && nd < 40f && Util.Los(eye, near.pos);
                bool grace = now < Grace;
                if (now < LureUntil && e.st == 0 && (e.kind == "bact" || e.kind == "thing" || e.kind == "wretch" || e.kind == "hound" || e.kind == "watch" || (e.kind == "face" && e.sub == 0)) && Util.Flat(LureP - e.pos).magnitude < 70f && (e.kind != "bact" || Awake(e))) { e.wander = LureP; e.hasWander = true; e.stateT = 0f; }
                switch (e.kind)
                {
                    case "stealer": SimStealer(e, dt, near, nd, los, grace, pls); break;
                    case "smiler": SimSmiler(e, dt, near, nd, grace); break;
                    case "hound": SimHound(e, dt, near, nd, los, grace); break;
                    case "party": SimParty(e, dt, near, nd, los, grace, pls); break;
                    default: if (!SimNew(e, dt, near, nd, los, grace, pls)) SimMoth(e, dt, near, nd, los); break;
                }
            }
        }

        static void SimStealer(Ent e, float dt, PL near, float nd, bool los, bool grace, List<PL> pls)
        {
            float a = e.anger; e.spd = 0f;
            switch (e.st)
            {
                case 0:
                    e.peekT -= dt; if (near != null && nd < 40f) Face(e, near.pos, dt);
                    if (los && nd < 24f && Seen(e)) e.stareT += dt; else e.stareT = Mathf.Max(0f, e.stareT - dt * 0.5f);
                    if (e.stareT > 1.6f) { Relocate(e, pls, 14f); e.stareT = 0f; e.peekT = U(10f, 20f) * (1.3f - a); Snd2.At("hiss", e.pos, 0.5f); }
                    if (e.peekT <= 0f && !grace) { e.st = 1; e.stateT = 0f; }
                    break;
                case 1:
                    if (near == null) break; e.stateT += dt; bool seen = Seen(e) && nd < 22f;
                    if (!seen) { e.spd = (1.8f + a * 1.4f) * DiffV; Move(e, near.pos, e.spd, dt); } else Face(e, near.pos, dt);
                    if ((nd < 11f && los) || e.stateT > 70f * (1.3f - a)) { e.st = 2; e.stateT = 0f; e.giveUp = 0f; BrSnd.Scream(e.pos, 1f); }
                    break;
                default:
                    if (near == null) break; e.stateT += dt; e.spd = (4.3f + a * 1.5f) * DiffV; Move(e, near.pos, e.spd, dt);
                    if (!los && nd > 22f) e.giveUp += dt; else e.giveUp = 0f; if (e.giveUp > 10f || e.stateT > 45f) { e.st = 0; Relocate(e, pls, 16f); e.peekT = U(14f, 26f) * (1.3f - a); e.spd = 0f; }
                    break;
            }
        }

        static void SimSmiler(Ent e, float dt, PL near, float nd, bool grace)
        {
            e.spd = 0f; if (near == null) return; Face(e, near.pos, dt); if (grace || nd > 90f) return;
            if (Seen(e)) { e.st = 0; return; }
            e.st = 1; e.spd = (4.6f + e.anger * 0.8f) * DiffV * (B.L.chaseStart ? 0.8f : 1f); Move(e, near.pos, e.spd, dt, true);
        }

        static void SimHound(Ent e, float dt, PL near, float nd, bool los, bool grace)
        {
            switch (e.st)
            {
                case 0:
                    e.spd = 2.1f; float wd = Util.Flat(e.wander - e.pos).magnitude; e.stateT += dt;
                    if (!e.hasWander || wd < 1.2f || e.stateT > 14f) { var c = B.cells[rnd.Next(B.cells.Count)]; e.wander = B.CellC(c.x, c.y); e.hasWander = true; e.stateT = 0f; }
                    Move(e, e.wander, e.spd, dt);
                    if (near != null && !grace && ((nd < 11f) || (nd < 26f && los))) { Alert(e, near); }
                    break;
                case 1:
                    e.spd = 0f; e.growlT -= dt; if (near != null) Face(e, near.pos, dt); if (e.growlT <= 0f) { e.st = 2; e.lost = 0f; }
                    break;
                default:
                    if (near == null) { e.st = 0; break; }
                    e.spd = (5.0f + e.anger * 0.7f + (e.pack % 2) * 0.12f) * DiffV; e.spd = Mathf.Min(e.spd, 5.9f); Move(e, near.pos, e.spd, dt);
                    if (!los) e.lost += dt; else e.lost = 0f; if (e.lost > 7f) { e.st = 0; e.hasWander = false; e.stateT = 0f; }
                    break;
            }
        }
        static void Alert(Ent e, PL near)
        {
            e.st = 1; e.growlT = 1.1f; Snd2.At("growl", e.pos, 1f);
            foreach (var o in All) if (o != e && o.kind == "hound" && o.st == 0 && (o.pos - e.pos).magnitude < 32f) { o.st = 1; o.growlT = 0.9f + (float)rnd.NextDouble() * 0.8f; Snd2.At("growl", o.pos, 0.8f); }
        }

        static void SimParty(Ent e, float dt, PL near, float nd, bool los, bool grace, List<PL> pls)
        {
            switch (e.st)
            {
                case 0:
                    e.spd = 0f; if (near != null && !grace && ((nd < 10f && los && !near.crouch) || nd < 2.8f)) { foreach (var o in All) if (o.kind == "party" && o.st != 1 && (o.pos - e.pos).magnitude < 28f) { o.st = 1; o.calmT = 0f; o.lost = 0f; } Snd2.At("hiss", e.pos, 0.7f); }
                    break;
                case 1:
                    if (near == null) { e.st = 2; break; }
                    if (grace) { e.spd = 0f; Face(e, near.pos, dt); break; }
                    e.spd = 4.8f * DiffV * (B.L.chaseStart ? 0.78f : 1f); Move(e, near.pos, e.spd, dt);
                    if (B.L.chaseStart) break;
                    if (near.crouch && nd > 3f) e.calmT += dt; else e.calmT = 0f; if (!los) e.lost += dt; else e.lost = 0f;
                    if (e.calmT > 3f || e.lost > 8f) e.st = 2;
                    break;
                default:
                    e.spd = 2.2f; Move(e, e.home, e.spd, dt); if (Util.Flat(e.home - e.pos).magnitude < 0.5f) { e.st = 0; e.spd = 0f; }
                    if (near != null && !grace && nd < 7f && los && !near.crouch) e.st = 1;
                    break;
            }
        }

        static void SimMoth(Ent e, float dt, PL near, float nd, bool los)
        {
            e.stateT += dt;
            if (e.st == 0)
            {
                e.spd = 1.5f; if (!e.hasWander || Util.Flat(e.wander - e.pos).magnitude < 1f || e.stateT > 9f) { int cx, cy; if (B.CellOf(e.pos, out cx, out cy)) { var d = rnd.Next(4); int nx = Mathf.Clamp(cx + Grid.DX[d], 0, B.n - 1), ny = Mathf.Clamp(cy + Grid.DY[d], 0, B.n - 1); if (!B.g.Pass(cx, cy, d)) { nx = cx; ny = cy; } e.wander = B.CellC(nx, ny) + new Vector3(U(-1.5f, 1.5f), 0, U(-1.5f, 1.5f)); e.hasWander = true; e.stateT = 0f; } }
                Move(e, e.wander, e.spd, dt, true); e.height = Mathf.Lerp(e.height, 2.2f + Mathf.Sin(Time.time * 2f + e.id) * 0.4f, dt);
                if (near != null && nd < 14f && los) { e.st = 1; e.stateT = 0f; }
            }
            else if (e.st == 1)
            {
                if (near == null) { e.st = 0; return; } e.spd = 3.4f; Move(e, near.pos, e.spd, dt, true); e.height = Mathf.Lerp(e.height, 1.6f, dt * 2f);
                if (nd < 1.1f || e.stateT > 20f) { e.st = 2; e.stateT = 0f; }
            }
            else
            {
                e.spd = 3f; var c = B.cells[rnd.Next(B.cells.Count)]; if (!e.hasWander || e.stateT < 0.05f) { e.wander = B.CellC(c.x, c.y); e.hasWander = true; }
                Move(e, e.wander, e.spd, dt, true); e.height = Mathf.Lerp(e.height, 2.6f, dt); if (e.stateT > 4f) { e.st = 0; e.hasWander = false; e.stateT = 0f; }
            }
        }

        /// <summary>Кто из сущностей поймал игрока (радиус поимки). Вызывать на хосте.</summary>
        public static Ent CaughtBy(PL p, float now)
        {
            foreach (var e in All)
            {
                if (e.kind == "moth" || e.catchCd > now) continue; if (IsNew(e.kind) && !CanCatchNew(e)) continue;
                if (e.kind == "party" && e.st != 1) continue; if (e.kind == "hound" && e.st != 2) continue; if (e.kind == "stealer" && e.st == 0) continue;
                if (Mathf.Abs(p.pos.y - (B.baseP.y + 1f)) > 3.5f) continue;
                if (Util.Flat(p.pos - e.pos).magnitude < e.radius) return e;
            }
            return null;
        }
        public static void AfterCatch(Ent e, List<PL> pls)
        {
            if (IsNew(e.kind)) { AfterCatchNew(e, pls); return; }
            e.catchCd = Time.time + 10f; e.st = 0; e.stateT = 0f; e.spd = 0f; e.peekT = U(14f, 26f);
            if (e.kind == "party") { e.pos = e.home; e.tpos = e.home; } else if (e.kind != "moth") Relocate(e, pls, 22f);
        }
        public static void CalmAll() { foreach (var e in All) { if (e.kind == "moth") continue; e.st = 0; e.spd = 0f; e.hasWander = false; } }
        public static void Provoke(Vector3 target)
        {
            foreach (var e in All) { if (e.kind == "bact") { e.delay = 0f; e.st = 2; e.stateT = 0f; e.lost = 0f; } else if (e.kind == "thing" || e.kind == "wretch") { e.st = 1; e.stateT = 0f; e.lost = 0f; } else if (e.kind == "watch") { e.st = 1; e.growlT = 1f; } else if (e.kind == "hound") { e.st = 1; e.growlT = U(0.3f, 1.2f); } else if (e.kind == "stealer" && e.st < 2) { e.st = 2; e.stateT = 0f; e.giveUp = 0f; } }
        }

        // ================= снимки по сети =================
        static string F(float v) { return v.ToString("0.#", CultureInfo.InvariantCulture); }
        public static string Snapshot(Vector3 refPos)
        {
            var sb = new StringBuilder(); var bp = B.baseP; bool first = true;
            foreach (var e in All)
            {
                if (!first) sb.Append(';'); first = false;
                sb.Append(e.id).Append(',').Append(Mathf.RoundToInt((e.pos.x - bp.x) * 10f)).Append(',').Append(Mathf.RoundToInt((e.pos.z - bp.z) * 10f)).Append(',').Append(Mathf.RoundToInt(Mathf.Repeat(e.yaw, 360f))).Append(',').Append(F(e.spd)).Append(',').Append(e.st).Append(',').Append(Mathf.RoundToInt(e.anger * 100f)).Append(',').Append(Mathf.RoundToInt(e.height * 10f));
            }
            return sb.ToString();
        }
        public static void Apply(string s)
        {
            if (B == null || string.IsNullOrEmpty(s)) return;
            try
            {
                foreach (var part in s.Split(';'))
                {
                    var f = part.Split(','); if (f.Length < 8) continue; int id = int.Parse(f[0]); if (id < 0 || id >= All.Count) continue; var e = All[id];
                    e.tpos = new Vector3(B.baseP.x + int.Parse(f[1]) * 0.1f, e.pos.y, B.baseP.z + int.Parse(f[2]) * 0.1f); e.tyaw = float.Parse(f[3], CultureInfo.InvariantCulture); float sp = float.Parse(f[4], CultureInfo.InvariantCulture);
                    int st = int.Parse(f[5]); if (e.st != st) { OnState(e, st); e.st = st; } e.spd = sp; e.anger = int.Parse(f[6]) * 0.01f; e.height = int.Parse(f[7]) * 0.1f;
                    if ((e.tpos - e.pos).sqrMagnitude > 64f) e.pos = e.tpos;
                }
            }
            catch (Exception ex) { Plugin.Warn("снимок", ex); }
        }
        // звуки смены состояния для клиентов (на хосте звучит при Alert/Scream)
        static void OnState(Ent e, int st)
        {
            if (Plugin.Inst == null || Play.IsHost) return;
            if (e.kind == "stealer" && st == 2) BrSnd.Scream(e.pos, 1f); else if (e.kind == "hound" && st == 1) Snd2.At("growl", e.pos, 1f); else if (e.kind == "party" && st == 1 && e.st == 0) Snd2.At("hiss", e.pos, 0.7f); else if (e.kind == "bact" && st == 2) BrSnd.Scream(e.pos, 0.8f); else if ((e.kind == "thing" || e.kind == "wretch") && st == 1) Snd2.At("growl", e.pos, 1f); else if ((e.kind == "watch" && st == 1) || (e.kind == "clump" && st == 2) || (e.kind == "face" && st == 1 && e.sub == 1)) Snd2.At("hiss", e.pos, 0.8f);
        }

        // ================= визуальная часть (у всех) =================
        public static void Visual(float dt, bool host)
        {
            if (B == null) return; var cam = Camera.main; Vector3 cp = cam != null ? cam.transform.position : B.spawn;
            foreach (var e in All)
            {
                if (e.root == null) continue;
                if (!host) { Vector3 pv = e.pos; e.pos = Vector3.Lerp(e.pos, e.tpos, 1f - Mathf.Exp(-dt * 11f)); e.yaw = Mathf.LerpAngle(e.yaw, e.tyaw, 1f - Mathf.Exp(-dt * 12f)); }
                float dist = (e.pos - cp).magnitude; bool vis = dist < 95f; if (e.root.activeSelf != vis) e.root.SetActive(vis); if (!vis) continue;
                float y = B.baseP.y; e.root.transform.position = new Vector3(e.pos.x, y, e.pos.z); e.root.transform.rotation = Quaternion.Euler(0, e.yaw, 0);
                float tm = Time.time + e.id * 1.37f; float sp = e.spd;
                switch (e.kind)
                {
                    case "stealer": AnimStealer(e, dt, cp, dist); break;
                    case "smiler": AnimSmiler(e, dt, tm, dist); break;
                    case "hound": AnimHound(e, dt, tm, dist); break;
                    case "party": AnimParty(e, dt, tm, dist); break;
                    case "moth": AnimMoth(e, dt, tm, dist); break;
                    default: AnimNew(e, dt, cp, tm, dist); break;
                }
                // шаги
                if (sp > 0.5f && e.kind != "moth" && e.kind != "watch" && dist < 28f) { e.stepT -= dt * Mathf.Clamp(sp / 2f, 0.6f, 3f); if (e.stepT <= 0f) { e.stepT = e.kind == "hound" ? 0.22f : 0.5f; Snd2.At("step", e.root.transform.position, Mathf.Clamp01(1f - dist / 28f) * (e.kind == "smiler" ? 0.2f : 0.55f)); } }
            }
        }

        static void AnimStealer(Ent e, float dt, Vector3 cp, float dist)
        {
            float a = e.anger; bool hunt = e.st == 2; float crawl = hunt ? Mathf.Clamp01((a - 0.5f) / 0.45f) : 0f; float hunch = hunt ? 36f : (e.st == 1 ? 12f : 0f);
            e.mon.Pose(dt, e.spd, Mathf.Clamp01(a + (hunt ? 0.2f : 0f)), crawl, hunch, cp + Vector3.up * 0.2f, dist < 22f, 0f);
            if (e.mon.breath != null) { if (dist < 20f && !e.mon.breath.isPlaying) { e.mon.breath.clip = BrSnd.BreathClip; e.mon.breath.Play(); } e.mon.breath.volume = Mathf.Clamp01(1f - dist / 20f) * 0.6f; }
        }
        static void AnimSmiler(Ent e, float dt, float tm, float dist)
        {
            var b = e.j[0]; if (b == null) return; bool mv = e.spd > 0.2f; float jit = mv ? 1f : 0.25f;
            b.localPosition = new Vector3(Mathf.Sin(tm * 7f) * 0.02f * jit, 1.15f + Mathf.Sin(tm * 2.5f) * 0.06f, 0); b.localRotation = Quaternion.Euler(Mathf.Sin(tm * 5f) * 3f * jit, 0, Mathf.Sin(tm * 3f) * 4f);
            for (int i = 0; i < 6; i++) { var l = e.j[1 + i]; if (l == null) continue; l.localRotation = Quaternion.Euler(0, Mathf.Sin(tm * (mv ? 14f : 2f) + i * 1.3f) * (mv ? 30f : 6f), 0); }
            if (mv && dist < 18f && Time.time > e.seenLocal) { e.seenLocal = Time.time + UnityEngine.Random.Range(2.5f, 5f); Snd2.At("hiss", e.root.transform.position, 0.45f); }
        }
        static void AnimHound(Ent e, float dt, float tm, float dist)
        {
            if (e.j[0] == null) return; float sp = e.spd; float run = Mathf.Clamp01(sp / 5f); float gait = sp < 0.2f ? 0f : 1f; e.ph += dt * (3f + sp * 2.2f) * gait; float ph = e.ph;
            e.j[0].localPosition = new Vector3(0, 0.62f + Mathf.Abs(Mathf.Sin(ph)) * 0.04f * run, 0); e.j[0].localRotation = Quaternion.Euler(Mathf.Sin(ph * 2f) * 3f * run, 0, 0);
            float jawOpen = e.st == 1 ? 35f + Mathf.Sin(tm * 25f) * 10f : (e.st == 2 ? 28f : 4f); e.j[3].localRotation = Quaternion.Euler(Mathf.Lerp(0f, jawOpen, 1f), 0, 0);
            e.j[2].localRotation = Quaternion.Euler(e.st == 1 ? 12f : 0f, Mathf.Sin(tm * 1.1f) * (e.st == 0 ? 20f : 0f) + (e.st == 1 ? Mathf.Sin(tm * 30f) * 4f : 0f), 0);
            for (int i = 0; i < 4; i++) { float off = (i == 0 || i == 3) ? 0f : Mathf.PI; float s = Mathf.Sin(ph + off); e.j[4 + i * 2].localRotation = Quaternion.Euler(s * 48f * run + (e.st == 1 ? -8f : 0f), 0, 0); e.j[5 + i * 2].localRotation = Quaternion.Euler(Mathf.Max(0f, -Mathf.Cos(ph + off)) * 60f * run, 0, 0); }
        }
        static void AnimParty(Ent e, float dt, float tm, float dist)
        {
            if (e.j[0] == null) return; bool chase = e.st == 1 || (e.st == 2 && e.spd > 0.5f); float sp = e.spd; e.ph += dt * (chase ? 9f * Mathf.Clamp(sp / 4.8f, 0.5f, 1.2f) : 3.2f); float ph = e.ph;
            if (chase)
            {
                e.j[0].localPosition = new Vector3(0, 0.95f + Mathf.Abs(Mathf.Sin(ph)) * 0.06f, 0); e.j[0].localRotation = Quaternion.Euler(14f, 0, Mathf.Sin(ph) * 3f);
                e.j[1].localRotation = Quaternion.Euler(Mathf.Sin(ph) * 55f, 0, 0); e.j[2].localRotation = Quaternion.Euler(-Mathf.Sin(ph) * 55f, 0, 0);
                e.j[4].localRotation = Quaternion.Euler(-Mathf.Sin(ph) * 50f, 0, 12f); e.j[5].localRotation = Quaternion.Euler(Mathf.Sin(ph) * 20f - 70f, 0, -12f); e.j[6].localRotation = Quaternion.Euler(-12f, Mathf.Sin(ph * 0.5f) * 15f, 0);
            }
            else
            {
                float sw = Mathf.Sin(ph); e.j[0].localPosition = new Vector3(Mathf.Sin(ph * 0.5f) * 0.05f, 0.95f + Mathf.Abs(Mathf.Sin(ph)) * 0.05f, 0); e.j[0].localRotation = Quaternion.Euler(0, 0, sw * 4f);
                e.j[1].localRotation = Quaternion.Euler(0, 0, sw * 3f); e.j[2].localRotation = Quaternion.Euler(0, 0, -sw * 3f);
                e.j[4].localRotation = Quaternion.Euler(0, 0, 14f + Mathf.Sin(ph * 1.3f) * 14f); e.j[5].localRotation = Quaternion.Euler(Mathf.Sin(ph) * 8f - 150f, 0, -10f); e.j[6].localRotation = Quaternion.Euler(0, Mathf.Sin(ph * 0.5f) * 25f, Mathf.Sin(ph) * 6f);
            }
            e.j[7].localRotation = Quaternion.Euler(Mathf.Sin(ph * 0.7f) * 4f, 0, Mathf.Sin(ph * 0.9f) * 6f);
        }
        static void AnimMoth(Ent e, float dt, float tm, float dist)
        {
            if (e.j[0] == null) return; float fl = Mathf.Sin(tm * (e.st == 1 ? 38f : 24f)) * 55f; e.j[1].localRotation = Quaternion.Euler(0, 0, fl); e.j[2].localRotation = Quaternion.Euler(0, 0, -fl);
            e.j[0].localPosition = new Vector3(0, Mathf.Max(0.5f, e.height) + Mathf.Sin(tm * 3f) * 0.08f, 0); e.j[0].localRotation = Quaternion.Euler(e.st == 1 ? 12f : 0f, 0, 0);
            if (dist < 14f && Time.time > e.seenLocal) { e.seenLocal = Time.time + UnityEngine.Random.Range(0.6f, 1.2f); Snd2.At("flap", e.root.transform.position + Vector3.up * 2f, Mathf.Clamp01(1f - dist / 14f) * 0.35f); }
        }

        // ================= прочее =================
        public static float LampDisturb(Vector3 p)
        {
            float best = 0f; foreach (var e in All) { if (e.kind == "moth" || e.kind == "party" || e.kind == "face" || e.kind == "window" || e.kind == "clump" || e.root == null) continue; float d = (p - e.root.transform.position).magnitude; if (d < 13f) best = Mathf.Max(best, Mathf.Lerp(0.12f, 0.65f, 1f - d / 13f) * (0.45f + 0.55f * e.anger)); }
            return best;
        }
        public static float NearestThreat(Vector3 p)
        {
            float best = 1e9f; foreach (var e in All) { if (e.kind == "moth" || e.root == null) continue; if (e.kind == "party" && e.st == 0) continue; if (e.kind == "hound" && e.st == 0) continue; if (IsNew(e.kind) && !Threatening(e)) continue; float d = Util.Flat(e.pos - p).magnitude; if (d < best) best = d; }
            return best;
        }
        public static bool AnyChasing() { foreach (var e in All) if ((e.kind == "stealer" && e.st == 2) || (e.kind == "hound" && e.st == 2) || (e.kind == "party" && e.st == 1) || (e.kind == "smiler" && e.spd > 0.3f) || (IsNew(e.kind) && Threatening(e) && e.spd > 0.5f)) return true; return false; }

        /// <summary>Каждый клиент: какие сущности он видит (для Смайлера и пр.). Возвращает список id.</summary>
        public static List<int> LocalSees()
        {
            var res = new List<int>(); var cam = Camera.main; if (cam == null || B == null) return res; Vector3 cp = cam.transform.position, cf = cam.transform.forward;
            foreach (var e in All)
            {
                if (e.kind == "moth") continue; Vector3 p = e.pos + Vector3.up * 1.3f; Vector3 d = p - cp; float dist = d.magnitude; if (dist > 55f) continue;
                if (Vector3.Dot(cf, d / Mathf.Max(0.01f, dist)) < 0.42f) continue; if (!Util.Los(cp, p)) continue; res.Add(e.id);
            }
            return res;
        }
    }
}
