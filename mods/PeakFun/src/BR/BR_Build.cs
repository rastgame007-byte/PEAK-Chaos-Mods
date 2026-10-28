// Построение геометрии уровня: стены, полы, потолки, лампы, декор, двери выхода.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal sealed class Hazard { public Vector3 pos; public float r; public int kind; /* 0 ток, 1 тёмная вода */ public float next; }

    internal sealed class Built
    {
        public GameObject root; public Vector3 baseP; public Grid g; public LevelDef L; public int n; public float H, C = 4f; public int seed;
        public Rig rig = new Rig(); public List<Renderer> flick = new List<Renderer>(); public List<Vector3> exitPos = new List<Vector3>(); public List<Vector3> exitDir = new List<Vector3>(); public List<Hazard> hazards = new List<Hazard>();
        public List<Vector2Int> cells = new List<Vector2Int>(); public Vector3 spawn; public int meshCount, colCount; public List<Fx> fx = new List<Fx>();
        public List<Renderer> crs = new List<Renderer>(); public List<Bounds> cbs = new List<Bounds>(); public int chunkCount;
        /// <summary>Отключает куски уровня дальше R метров (их всё равно скрывает туман) — основа оптимизации.</summary>
        public void Cull(Vector3 pc, float R)
        {
            float r2 = R * R; for (int i = 0; i < crs.Count; i++) { var r = crs[i]; if (r == null) continue; bool on = cbs[i].SqrDistance(pc) < r2; if (r.enabled != on) r.enabled = on; }
        }
        public Vector3 CellC(int x, int y) { return baseP + new Vector3((x + 0.5f) * C, 0f, (y + 0.5f) * C); }
        public bool CellOf(Vector3 w, out int x, out int y) { x = Mathf.FloorToInt((w.x - baseP.x) / C); y = Mathf.FloorToInt((w.z - baseP.z) / C); return x >= 0 && y >= 0 && x < n && y < n; }
        public bool Inside(Vector3 w) { var l = w - baseP; return l.x > -4f && l.z > -4f && l.x < n * C + 4f && l.z < n * C + 4f && l.y > -8f && l.y < H + 10f; }
        public void Destroy() { if (root != null) UnityEngine.Object.Destroy(root); root = null; }
    }

    internal static partial class LevelBuilder
    {
        static int layerC = -1;
        public static int Layer()
        {
            if (layerC >= 0) return layerC;
            try
            {
                foreach (var nm in new[] { "Terrain", "Map", "Ground" }) { int l = LayerMask.NameToLayer(nm); if (l >= 0) return layerC = l; }
                int m = Util.TerrainMask(); for (int i = 1; i < 32; i++) if ((m & (1 << i)) != 0) return layerC = i;
            }
            catch { }
            return layerC = 0;
        }

        static readonly Color[] ZoneTint = { new Color(1f, 1f, 1f), new Color(0.9f, 1f, 0.86f), new Color(0.86f, 0.76f, 0.7f) };

        sealed class Ctx
        {
            public Built b; public Transform T; public int layer; public System.Random rnd; public Dictionary<string, MeshB> mesh = new Dictionary<string, MeshB>(); public Dictionary<string, Func<Material>> mat = new Dictionary<string, Func<Material>>();
            public MeshB M(string key, Func<Material> mf) { MeshB m; if (!mesh.TryGetValue(key, out m)) { m = new MeshB(); mesh[key] = m; mat[key] = mf; } return m; }
            public BoxCollider Col(string name, Vector3 c, Vector3 size) { var g = new GameObject(name); g.transform.SetParent(T, false); g.transform.localPosition = c; g.layer = layer; var bc = g.AddComponent<BoxCollider>(); bc.size = size; b.colCount++; return bc; }
            public float R() { return (float)rnd.NextDouble(); }
        }

        static Color Dim(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }
        static int Zone(Built b, int x, int y)
        {
            float p = Mathf.PerlinNoise(Mathf.Clamp(x, 0, b.n) * 0.09f + (b.seed % 97) * 0.37f, Mathf.Clamp(y, 0, b.n) * 0.09f + 3.3f);
            return p < 0.34f ? 1 : (p > 0.68f ? 2 : 0);
        }

        public static Built Build(Grid g, LevelDef L, Vector3 baseP, int seed, bool flashlight)
        {
            var b = new Built { g = g, L = L, n = g.n, H = L.H, baseP = baseP, seed = seed };
            int n = g.n; float C = b.C, H = b.H, BT = L.wallT; int layer = Layer();
            b.root = new GameObject("BR_level_player"); b.root.transform.position = baseP; var T = b.root.transform;
            var x = new Ctx { b = b, T = T, layer = layer, rnd = new System.Random(seed ^ 0x5bd1) }; var rnd = x.rnd;
            Func<int, int, bool> Rch = (cx, cy) => cx >= 0 && cy >= 0 && cx < n && cy < n && g.dist[cx, cy] >= 0;
            var walls = new MeshB[3]; var floors = new MeshB[3]; var ceils = new MeshB[3]; for (int i = 0; i < 3; i++) { walls[i] = new MeshB(); floors[i] = new MeshB(); ceils[i] = new MeshB(); }
            var skirt = x.M("skirt", () => BrGfx.Surf(null, Dim(Color.Lerp(L.tint, new Color(0.15f, 0.12f, 0.06f), 0.7f), 0.55f), 0.2f)); var pil = new MeshB(); var outl = x.M("outl", () => BrGfx.Surf(null, new Color(0.88f, 0.84f, 0.7f), 0.3f));
            var vents = x.M("vents", () => BrGfx.Surf(null, new Color(0.24f, 0.22f, 0.19f), 0.3f, 0.4f)); var frameB = x.M("frame", () => BrGfx.Surf(null, new Color(0.55f, 0.53f, 0.45f), 0.3f, 0.3f));
            var lampB = new MeshB(); var deadB = x.M("dead", () => BrGfx.Surf(null, new Color(0.25f, 0.25f, 0.24f), 0.2f)); var poleB = x.M("pole", () => BrGfx.Surf(null, new Color(0.14f, 0.15f, 0.16f), 0.3f, 0.6f)); var fungB = x.M("fung", () => BrGfx.Surf(null, new Color(0.2f, 0.7f, 0.6f), 0.5f, 0f, new Color(0.15f, 0.9f, 0.7f)));
            const float tile = 4f; bool pipesLvl = L.style == Style.Pipes;
            // ---- стены сплошными отрезками ----
            for (int xi = 0; xi <= n; xi++)
            {
                int y = 0;
                while (y < n)
                {
                    if (!g.wv[xi, y] || !(Rch(xi - 1, y) || Rch(xi, y))) { y++; continue; }
                    int z = Zone(b, xi, y), y0 = y; while (y < n && g.wv[xi, y] && (Rch(xi - 1, y) || Rch(xi, y)) && Zone(b, xi, y) == z) y++;
                    Vector3 c = new Vector3(xi * C, H * 0.5f, (y0 + y) * 0.5f * C), sz = new Vector3(BT, H, (y - y0) * C + BT);
                    walls[z].WallBox(c, sz, tile, H); skirt.WallBox(new Vector3(c.x, 0.11f, c.z), new Vector3(sz.x + 0.07f, 0.22f, sz.z + 0.02f), 4f, 0.22f); x.Col("BR_wall_player", c, sz);
                    for (int k = y0; k < y; k++)
                    {
                        float sg = rnd.NextDouble() < 0.5 ? -1f : 1f; float zc = (k + 0.5f) * C;
                        if (rnd.NextDouble() < 0.12) outl.Box(new Vector3(xi * C + sg * (BT * 0.5f + 0.02f), 0.32f, zc + (x.R() - 0.5f) * 2.6f), new Vector3(0.04f, 0.13f, 0.09f));
                        if (rnd.NextDouble() < 0.06) vents.Box(new Vector3(xi * C + sg * (BT * 0.5f + 0.015f), H - 0.75f, zc + (x.R() - 0.5f) * 2.2f), new Vector3(0.03f, 0.4f, 0.8f));
                    }
                }
            }
            for (int y = 0; y <= n; y++)
            {
                int xi = 0;
                while (xi < n)
                {
                    if (!g.wh[xi, y] || !(Rch(xi, y - 1) || Rch(xi, y))) { xi++; continue; }
                    int z = Zone(b, xi, y), x0 = xi; while (xi < n && g.wh[xi, y] && (Rch(xi, y - 1) || Rch(xi, y)) && Zone(b, xi, y) == z) xi++;
                    Vector3 c = new Vector3((x0 + xi) * 0.5f * C, H * 0.5f, y * C), sz = new Vector3((xi - x0) * C + BT, H, BT);
                    walls[z].WallBox(c, sz, tile, H); skirt.WallBox(new Vector3(c.x, 0.11f, c.z), new Vector3(sz.x + 0.02f, 0.22f, sz.z + 0.07f), 4f, 0.22f); x.Col("BR_wall_player", c, sz);
                    for (int k = x0; k < xi; k++)
                    {
                        float sg = rnd.NextDouble() < 0.5 ? -1f : 1f; float xc = (k + 0.5f) * C;
                        if (rnd.NextDouble() < 0.12) outl.Box(new Vector3(xc + (x.R() - 0.5f) * 2.6f, 0.32f, y * C + sg * (BT * 0.5f + 0.02f)), new Vector3(0.09f, 0.13f, 0.04f));
                        if (rnd.NextDouble() < 0.06) vents.Box(new Vector3(xc + (x.R() - 0.5f) * 2.2f, H - 0.75f, y * C + sg * (BT * 0.5f + 0.015f)), new Vector3(0.8f, 0.4f, 0.03f));
                    }
                }
            }
            // ---- пол, потолок, колонны, лампы, декор ----
            var lampMat = BrGfx.Surf(null, Dim(L.lamp, 0.95f), 0.2f, 0f, new Color(L.lamp.r * 2.6f, L.lamp.g * 2.45f, L.lamp.b * 2.2f));
            bool domeOn = L.dome && Dome.Supported();
            var haloB = new MeshB(); var flickGO = new List<Vector3>(); var flickIdx = new List<int>(); var lampList = b.rig.LampPos; var lampRend = b.rig.LampRend;
            for (int cx = 0; cx < n; cx++) for (int cy = 0; cy < n; cy++)
            {
                if (!Rch(cx, cy)) continue; b.cells.Add(new Vector2Int(cx, cy));
                int z = Zone(b, cx, cy); float x0 = cx * C, z0 = cy * C;
                floors[z].FloorQuad(x0, z0, x0 + C, z0 + C, 0f, true); if (!domeOn) ceils[z].FloorQuad(x0, z0, x0 + C, z0 + C, H, false);
                Vector3 cc = new Vector3(x0 + C * 0.5f, 0f, z0 + C * 0.5f); bool open = g.Pass(cx, cy, 0) && g.Pass(cx, cy, 1) && g.Pass(cx, cy, 2) && g.Pass(cx, cy, 3);
                bool nearSpawn = Mathf.Abs(cx - g.sx) <= 1 && Mathf.Abs(cy - g.sy) <= 1;
                // колонны — со смещением от центра клетки, чтобы сущности не проходили сквозь
                bool pillarLvl = L.id == "0" || L.id == "1" || L.id == "37" || L.id == "6" || L.id == "fun";
                if (open && pillarLvl && !nearSpawn && (L.id == "1" ? (cx % 2 == 1 && cy % 2 == 1) : (((cx * 7 + cy * 13) % 3 == 0 || L.id == "0") && rnd.NextDouble() < 0.4)))
                {
                    Vector3 pc = cc + new Vector3(1.0f, 0f, 1.0f) + Vector3.up * H * 0.5f; float ps = L.id == "1" ? 1.1f : 0.9f; pil.WallBox(pc, new Vector3(ps, H, ps), tile, H); x.Col("BR_pillar_player", pc, new Vector3(ps, H, ps));
                }
                if (L.sky || domeOn)
                {
                    if ((cx + cy) % 2 == 0 && rnd.NextDouble() < L.lampKeep)
                    {
                        Vector3 lcl = new Vector3(x0 + C * 0.5f + 1.45f * ((cx % 2 == 0) ? 1f : -1f), 0f, z0 + C * 0.5f + 1.45f * ((cy % 2 == 0) ? 1f : -1f));
                        float ph = L.poles ? Mathf.Min(H - 0.7f, 5.2f) : Mathf.Min(H - 0.5f, 2.3f);
                        if (L.poles) { poleB.Box(lcl + Vector3.up * ph * 0.5f, new Vector3(0.14f, ph, 0.14f)); lampB.Box(lcl + Vector3.up * (ph + 0.04f), new Vector3(0.7f, 0.1f, 0.35f)); x.Col("BR_pole_player", lcl + Vector3.up * ph * 0.5f, new Vector3(0.3f, ph, 0.3f)); }
                        else if (L.id == "8") fungB.Box(lcl + Vector3.up * 0.3f, new Vector3(0.5f + x.R() * 0.4f, 0.6f, 0.5f + x.R() * 0.4f));
                        lampList.Add(baseP + lcl + Vector3.up * (ph - 0.1f)); lampRend.Add(null);
                    }
                }
                else if ((cx + cy) % 2 == 0)
                {
                    Vector3 lc = new Vector3(x0 + C * 0.5f, H - 0.03f, z0 + C * 0.5f); frameB.Box(lc + Vector3.up * 0.012f, new Vector3(2.0f, 0.05f, 0.88f));
                    bool lit = rnd.NextDouble() < L.lampKeep;
                    if (!lit) { deadB.Box(lc - Vector3.up * 0.012f, new Vector3(1.8f, 0.05f, 0.7f)); }
                    else
                    {
                        bool fl = rnd.NextDouble() < L.flicker; lampList.Add(baseP + lc - Vector3.up * 0.2f); lampRend.Add(null);
                        if (fl) { flickGO.Add(lc - Vector3.up * 0.012f); flickIdx.Add(lampList.Count - 1); } else { lampB.Box(lc - Vector3.up * 0.012f, new Vector3(1.8f, 0.05f, 0.7f)); if (Halos) { float hy = H - 0.055f, hr = 2.1f; haloB.Quad(new Vector3(lc.x - hr, hy, lc.z - hr), new Vector3(lc.x - hr, hy, lc.z + hr), new Vector3(lc.x + hr, hy, lc.z + hr), new Vector3(lc.x + hr, hy, lc.z - hr), Vector3.down, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0)); } }
                    }
                }
                try { Decor(x, cx, cy, cc, open, nearSpawn); } catch (Exception dex) { if (!decorWarned) { decorWarned = true; Plugin.Warn("декор " + L.id, dex); } }
            }
            // стены по периметру — рельефные полосы и скрытые «глухие» стороны не нужны; коллайдеры пола/потолка
            float W = n * C; x.Col("BR_floorcol_player", new Vector3(W * 0.5f, -0.5f, W * 0.5f), new Vector3(W + 6f, 1f, W + 6f)); x.Col("BR_ceilcol_player", new Vector3(W * 0.5f, H + (domeOn ? 12f : 0f) + 0.5f, W * 0.5f), new Vector3(W + 6f, 1f, W + 6f));
            // ---- материалы ----
            var tw = Tex.Wall(L.wallTex); var tf = Tex.Floor(L.floorTex); var tcx = Tex.Ceil(L.ceilTex); float fs = L.id == "37" || L.id == "!" ? 0.45f : (L.id == "3" ? 0.3f : (L.flood ? 0.35f : (L.id == "9" || L.id == "11" ? 0.25f : (L.id == "188" ? 0.4f : 0.05f))));
            for (int z = 0; z < 3; z++)
            {
                Color zt = new Color(L.tint.r * ZoneTint[z].r, L.tint.g * ZoneTint[z].g, L.tint.b * ZoneTint[z].b, 1f);
                Mesh(b, T, "BR_walls" + z, walls[z], BrGfx.Surf(tw, zt, 0.08f)); Mesh(b, T, "BR_floor" + z, floors[z], BrGfx.Surf(tf, Dim(zt, 0.95f), fs)); Mesh(b, T, "BR_ceil" + z, ceils[z], BrGfx.Surf(tcx, zt, 0.1f));
            }
            if (domeOn) Dome.Build(b, T);
            if (L.id == "37") Water.Build(b, T);
            Mesh(b, T, "BR_pillars", pil, BrGfx.Surf(tw, Dim(L.tint, 0.95f), 0.08f));
            try { MeshB dm; if (x.mesh.TryGetValue("decal", out dm)) Plugin.Log("Уровень " + L.id + ": картинок на стенах " + dm.Count / 4); } catch { }
            foreach (var kv in x.mesh) Mesh(b, T, "BR_" + kv.Key, kv.Value, x.mat[kv.Key](), !SmallProp(kv.Key));
            if (haloB.Count > 0) { try { var hm = Vfx.Mat(Vfx.Blob(), new Color(L.lamp.r, L.lamp.g, L.lamp.b, 0.17f)); var hg = Mesh(b, T, "BR_halos", haloB, hm, false, false); if (hg != null) { var hr2 = hg.GetComponent<MeshRenderer>(); hr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; hr2.receiveShadows = false; } } catch { } }
            var lb = Mesh(b, T, "BR_lamps", lampB, lampMat, false, false); if (lb != null) { var r = lb.GetComponent<MeshRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            for (int fi = 0; fi < flickGO.Count; fi++)
            {
                var l = GameObject.CreatePrimitive(PrimitiveType.Cube); l.name = "BR_lamp_player"; UnityEngine.Object.Destroy(l.GetComponent<Collider>()); l.transform.SetParent(T, false);
                l.transform.localPosition = flickGO[fi]; l.transform.localScale = new Vector3(1.8f, 0.05f, 0.7f); var mr = l.GetComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; mr.sharedMaterial = lampMat;
                b.flick.Add(mr); lampRend[flickIdx[fi]] = mr;
            }
            // ---- двери выхода ----
            foreach (var ex in g.exits) { Vector3 dir; b.exitPos.Add(baseP + Door(b, T, ex.x, ex.y, out dir)); b.exitDir.Add(dir); }
            b.spawn = b.CellC(g.sx, g.sy);
            b.rig.Init(T, L.lamp, L.lampInt, flashlight);
            // страховка: декор-коллайдеры не должны перекрывать точку появления и двери выхода
            ClearColliders(T, b.spawn, 3.6f); foreach (var ep in b.exitPos) ClearColliders(T, ep, 2.8f);
            try { VerifyPassages(b, T); } catch (Exception ve) { Plugin.Warn("проверка проходов", ve); }
            return b;
        }

        static void Prop(Ctx x, string key, Color col, Vector3 center, Vector3 size, bool col2)
        {
            x.M(key, () => BrGfx.Surf(null, col, 0.25f)).Box(center, size); if (col2) x.Col("BR_prop_player", center, size);
        }
        static void Car(Ctx x, Vector3 lc, bool rotZ, Color col)
        {
            var body = x.M("car" + ColKey(col), () => BrGfx.Surf(null, col, 0.55f, 0.4f)); var glass = x.M("carg", () => BrGfx.Surf(null, new Color(0.04f, 0.05f, 0.07f), 0.9f)); var wheel = x.M("carw", () => BrGfx.Surf(null, new Color(0.02f, 0.02f, 0.02f), 0.1f));
            Vector3 L = rotZ ? new Vector3(1.8f, 0.8f, 4.2f) : new Vector3(4.2f, 0.8f, 1.8f); Vector3 Cb = rotZ ? new Vector3(1.6f, 0.6f, 2.2f) : new Vector3(2.2f, 0.6f, 1.6f);
            body.Box(lc + Vector3.up * 0.65f, L); glass.Box(lc + Vector3.up * 1.2f, Cb); body.Box(lc + Vector3.up * 1.52f, new Vector3(Cb.x * 0.98f, 0.06f, Cb.z * 0.9f));
            for (int i = 0; i < 4; i++) { float a = (i % 2 == 0 ? 1f : -1f), b = (i < 2 ? 1f : -1f); Vector3 o = rotZ ? new Vector3(a * 0.9f, 0.3f, b * 1.4f) : new Vector3(b * 1.4f, 0.3f, a * 0.9f); wheel.Box(lc + o, rotZ ? new Vector3(0.22f, 0.6f, 0.6f) : new Vector3(0.6f, 0.6f, 0.22f)); }
            x.Col("BR_car_player", lc + Vector3.up * 0.8f, new Vector3(L.x, 1.6f, L.z));
        }
        static string ColKey(Color c) { return ((int)(c.r * 20)).ToString() + ((int)(c.g * 20)) + ((int)(c.b * 20)); }
        static void Tree(Ctx x, Vector3 lc, float r)
        {
            var trunk = x.M("trunk", () => BrGfx.Surf(null, new Color(0.15f, 0.1f, 0.06f), 0.1f)); var leaf = x.M("leaf", () => BrGfx.Surf(null, new Color(0.05f, 0.14f, 0.07f), 0.1f));
            float h = 3.0f + x.R() * 1.0f; trunk.Box(lc + Vector3.up * h * 0.5f, new Vector3(r * 0.8f, h, r * 0.8f)); x.Col("BR_tree_player", lc + Vector3.up * h * 0.5f, new Vector3(r * 0.8f, h, r * 0.8f));
            leaf.Box(lc + Vector3.up * (h + 0.4f), new Vector3(2.2f, 1.6f, 2.2f)); leaf.Box(lc + Vector3.up * (h + 1.4f), new Vector3(1.4f, 1.2f, 1.4f));
        }

        /// <summary>Проверка в самой игре: в каждом проходе между клетками не должно быть твёрдых предметов. Мешающие коллайдеры декора удаляются, стена на открытом проходе — пишется в лог.</summary>
        static void VerifyPassages(Built b, Transform T)
        {
            Physics.SyncTransforms(); var g = b.g; float C = b.C; var buf = new Collider[12]; int edges = 0, removed = 0, wallsBad = 0;
            foreach (var cell in b.cells)
                for (int d = 0; d < 4; d += 2)   // вправо (0) и вверх (2); обратные проходы те же
                {
                    int dd = d == 0 ? 0 : 2; if (!g.Pass(cell.x, cell.y, dd)) continue; edges++;
                    Vector3 mid = b.baseP + new Vector3((cell.x + 0.5f + (dd == 0 ? 0.5f : 0f)) * C, 1.0f, (cell.y + 0.5f + (dd == 2 ? 0.5f : 0f)) * C);
                    Vector3 half = dd == 0 ? new Vector3(0.35f, 0.9f, 0.5f) : new Vector3(0.5f, 0.9f, 0.35f);
                    int k = Physics.OverlapBoxNonAlloc(mid, half, buf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < k; i++)
                    {
                        var c = buf[i]; if (c == null || !c.transform.IsChildOf(T)) continue; string nm = c.name;
                        if (nm.Contains("floorcol") || nm.Contains("ceilcol")) continue;
                        if (nm.Contains("wall")) { wallsBad++; continue; }
                        UnityEngine.Object.Destroy(c.gameObject); removed++;
                    }
                }
            Plugin.Log("Проходы уровня " + b.L.id + ": проверено " + edges + ", убрано мешающих предметов " + removed + ", стен на открытом проходе " + wallsBad);
        }

        static void ClearColliders(Transform T, Vector3 w, float r)
        {
            foreach (var c in T.GetComponentsInChildren<BoxCollider>())
            {
                string nm = c.name; if (nm.Contains("wall") || nm.Contains("floorcol") || nm.Contains("ceilcol")) continue;
                Vector3 d = c.transform.position - w; d.y = 0f; if (d.magnitude < r) UnityEngine.Object.Destroy(c.gameObject);
            }
        }
        const float ChunkSize = 16f;
        static GameObject Mesh(Built b, Transform par, string name, MeshB mb, Material m, bool shadows = true, bool receive = true)
        {
            if (mb.Count == 0) return null; b.meshCount++; GameObject first = null;
            var sc = shadows && BrGfx.LitOk ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            Action<string, MeshB> one = (nm, part) =>
            {
                var go = new GameObject(nm); go.transform.SetParent(par, false); var mesh = part.ToMesh(nm); go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = sc; mr.receiveShadows = receive && BrGfx.LitOk; if (m != null) mr.sharedMaterial = m;
                var bb = mesh.bounds; bb.center += b.baseP; b.crs.Add(mr); b.cbs.Add(bb); b.chunkCount++; if (first == null) first = go;
            };
            if (mb.Count < 3000) { one(name, mb); return first; }
            int i = 0; foreach (var kv in mb.Split(ChunkSize)) one(name + "_" + (i++), kv.Value);
            return first;
        }
        static readonly string[] SmallKeys = { "pp", "brk", "rub", "valve", "dr_", "grate", "cv_", "tf_", "tray", "wire", "cab", "haz_", "pl_", "decal", "ot_", "ex_", "fa_", "vt_", "cam_", "tank", "gauge", "rlamp", "sc_", "hay", "dmp", "cone", "hyd", "mb_", "can", "bar", "buoy", "log", "dk_" };
        static bool SmallProp(string key) { foreach (var k in SmallKeys) if (key.StartsWith(k)) return true; return false; }

        static GameObject Prim(PrimitiveType t, Transform parent, Vector3 lp, Vector3 ls, Color col, string name, Color? emis = null)
        {
            var g = GameObject.CreatePrimitive(t); g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = ls;
            var mr = g.GetComponent<MeshRenderer>(); var m = BrGfx.Surf(null, col, 0.2f, 0f, emis); if (m != null) mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var cl = g.GetComponent<Collider>(); if (cl != null) UnityEngine.Object.DestroyImmediate(cl); return g;
        }

        static Vector3 Door(Built b, Transform T, int ex, int ey, out Vector3 dir)
        {
            float C = b.C, BT = b.L.wallT, H = b.H; Vector3 ec = new Vector3((ex + 0.5f) * C, 0f, (ey + 0.5f) * C); dir = b.g.WallDir(ex, ey); var col = b.L.exitCol;
            float dh = Mathf.Min(2.6f, H - 0.55f);
            Vector3 dp = ec + dir * (C * 0.5f - BT * 0.5f - 0.1f); Quaternion dr = dir == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(-dir);
            Vector3 fwd = dr * Vector3.forward;
            var frame = Prim(PrimitiveType.Cube, T, dp + Vector3.up * (dh * 0.5f + 0.1f), new Vector3(1.8f, dh + 0.2f, 0.1f), Dim(col, 0.2f), "BR_exit_player"); frame.transform.localRotation = dr;
            var doorMat = BrGfx.Surf(null, col, 0.2f, 0f, new Color(col.r * 1.4f, col.g * 2.2f, col.b * 1.6f));
            var door = Prim(PrimitiveType.Cube, T, dp + Vector3.up * (dh * 0.5f) - fwd * 0.04f, new Vector3(1.5f, dh, 0.1f), Color.white, "BR_exit_player"); door.transform.localRotation = dr; door.GetComponent<MeshRenderer>().sharedMaterial = doorMat;
            var sign = Prim(PrimitiveType.Cube, T, dp + Vector3.up * (dh + 0.3f) - fwd * 0.04f, new Vector3(1.3f, 0.22f, 0.1f), Color.white, "BR_exit_player"); sign.transform.localRotation = dr; sign.GetComponent<MeshRenderer>().sharedMaterial = doorMat;
            Prim(PrimitiveType.Sphere, T, dp + Vector3.up * 1.25f - fwd * 0.14f + dr * Vector3.right * 0.5f, new Vector3(0.12f, 0.12f, 0.12f), new Color(0.9f, 0.9f, 0.9f), "BR_exit_player");
            var dl = new GameObject("BR_exitlight_player"); dl.transform.SetParent(T, false); dl.transform.localPosition = dp + Vector3.up * 1.6f + dir * -0.5f; var dlight = dl.AddComponent<Light>(); dlight.type = LightType.Point; dlight.color = col; dlight.range = 16f; dlight.intensity = 3.2f; dlight.shadows = LightShadows.None;
            return dp;
        }

        // ================= декор по уровням =================
        static bool decorWarned; public static bool Halos = true;
        static bool decor3Warned;
        static void Decor(Ctx x, int cx, int cy, Vector3 cc, bool open, bool nearSpawn)
        {
            var b = x.b; var L = b.L; float C = b.C, H = b.H; var rnd = x.rnd; Vector3 lc = cc - b.baseP; // локальный центр клетки
            Decor2(x, cx, cy, cc, open, nearSpawn);
            try { DecorWall(x, cx, cy, cc, nearSpawn); } catch (Exception dw) { if (!decor3Warned) { decor3Warned = true; Plugin.Warn("декор-стены " + L.id, dw); } }
            try { Decor3(x, cx, cy, cc, open, nearSpawn); } catch (Exception d3) { if (!decor3Warned) { decor3Warned = true; Plugin.Warn("декор3 " + L.id, d3); } }
            if (L.id == "!") { try { RunObstacles(x, cx, cy, lc, nearSpawn); } catch (Exception ro) { if (!decor3Warned) { decor3Warned = true; Plugin.Warn("препятствия", ro); } } }
            switch (L.id)
            {
                case "1":
                {
                    var shelf = x.M("shelf", () => BrGfx.Surf(null, new Color(0.22f, 0.3f, 0.5f), 0.3f, 0.5f)); var cargo = x.M("cargo", () => BrGfx.Surf(null, new Color(0.55f, 0.42f, 0.27f), 0.1f));
                    if (nearSpawn) break;
                    for (int d = 0; d < 4; d++)
                    {
                        if (b.g.Pass(cx, cy, d) || rnd.NextDouble() > 0.4) continue; Vector3 nrm = d == 0 ? Vector3.left : d == 1 ? Vector3.right : d == 2 ? Vector3.back : Vector3.forward; bool alongZ = d < 2;
                        Vector3 pc = lc + new Vector3(Grid.DX[d], 0, Grid.DY[d]) * (C * 0.5f - 0.7f); Vector3 sz = alongZ ? new Vector3(0.8f, H - 0.8f, 3.2f) : new Vector3(3.2f, H - 0.8f, 0.8f);
                        float hs = sz.y; for (int lv = 0; lv < 4; lv++) shelf.Box(pc + Vector3.up * (0.15f + lv * (hs - 0.2f) / 3f), alongZ ? new Vector3(0.8f, 0.07f, 3.2f) : new Vector3(3.2f, 0.07f, 0.8f));
                        for (int s = -1; s <= 1; s += 2) for (int t = -1; t <= 1; t += 2) { Vector3 o = alongZ ? new Vector3(s * 0.38f, 0, t * 1.55f) : new Vector3(t * 1.55f, 0, s * 0.38f); shelf.Box(pc + o + Vector3.up * hs * 0.5f, new Vector3(0.07f, hs, 0.07f)); }
                        for (int lv = 0; lv < 3; lv++) for (int k = 0; k < 3; k++) if (rnd.NextDouble() < 0.7) { float off = (k - 1) * 1.0f + (x.R() - 0.5f) * 0.2f; float bh = 0.35f + x.R() * 0.35f; Vector3 bp = pc + (alongZ ? new Vector3(0, 0, off) : new Vector3(off, 0, 0)) + Vector3.up * (0.2f + lv * (hs - 0.2f) / 3f + bh * 0.5f + 0.04f); cargo.Box(bp, alongZ ? new Vector3(0.6f, bh, 0.8f) : new Vector3(0.8f, bh, 0.6f)); }
                        x.Col("BR_shelf_player", pc + Vector3.up * hs * 0.5f, sz);
                    }
                    break;
                }
                case "2": case "3": break;   // новые детальные модели — в Decor3
                case "4":
                {
                    var desk = x.M("desk", () => BrGfx.Surf(null, new Color(0.45f, 0.36f, 0.26f), 0.25f)); var scr = x.M("scr", () => BrGfx.Surf(null, new Color(0.05f, 0.06f, 0.07f), 0.7f)); var part = x.M("part", () => BrGfx.Surf(null, new Color(0.4f, 0.44f, 0.5f), 0.1f));
                    if (open && !nearSpawn && rnd.NextDouble() < 0.28) { Vector3 p = lc + new Vector3(x.R() > 0.5f ? 1.0f : -1.0f, 0, x.R() > 0.5f ? 1.0f : -1.0f); bool rot = rnd.Next(2) == 0; Vector3 sz = rot ? new Vector3(0.8f, 0.75f, 1.6f) : new Vector3(1.6f, 0.75f, 0.8f); desk.Box(p + Vector3.up * 0.375f, sz); scr.Box(p + Vector3.up * 0.98f, rot ? new Vector3(0.05f, 0.4f, 0.5f) : new Vector3(0.5f, 0.4f, 0.05f)); x.Col("BR_desk_player", p + Vector3.up * 0.375f, sz); }
                    else if (open && !nearSpawn && rnd.NextDouble() < 0.12) { Vector3 p = lc + new Vector3(0, 0, 1.2f); part.Box(p + Vector3.up * 0.7f, new Vector3(2.4f, 1.4f, 0.08f)); x.Col("BR_part_player", p + Vector3.up * 0.7f, new Vector3(2.4f, 1.4f, 0.08f)); }
                    break;
                }
                case "5":
                {
                    var door = x.M("hdoor", () => BrGfx.Surf(null, new Color(0.22f, 0.12f, 0.07f), 0.35f)); var plate = x.M("hplate", () => BrGfx.Surf(null, new Color(0.8f, 0.62f, 0.2f), 0.7f, 0.9f)); var paint = x.M("paint", () => BrGfx.Surf(null, new Color(0.25f, 0.2f, 0.12f), 0.3f));
                    for (int d = 0; d < 4; d++)
                    {
                        if (b.g.Pass(cx, cy, d) || nearSpawn) continue; double q = rnd.NextDouble(); if (q > 0.34) continue; Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); Vector3 p = lc + dd * (C * 0.5f - 0.35f * 0.5f - 0.04f); bool alongZ = d < 2;
                        if (q < 0.2) { door.Box(p + Vector3.up * 1.1f, alongZ ? new Vector3(0.06f, 2.2f, 1.1f) : new Vector3(1.1f, 2.2f, 0.06f)); plate.Box(p - dd * 0.04f + Vector3.up * 1.5f, alongZ ? new Vector3(0.04f, 0.1f, 0.2f) : new Vector3(0.2f, 0.1f, 0.04f)); }
                        else paint.Box(p + Vector3.up * 1.7f, alongZ ? new Vector3(0.05f, 0.8f, 1.1f) : new Vector3(1.1f, 0.8f, 0.05f));
                    }
                    break;
                }
                case "fun":
                {
                    var cloth = x.M("cloth", () => BrGfx.Surf(null, new Color(0.95f, 0.5f, 0.7f), 0.2f)); var cake = x.M("cake", () => BrGfx.Surf(null, new Color(1f, 0.95f, 0.8f), 0.3f)); var strm = x.M("strm" + (cx % 3), () => BrGfx.Surf(null, cx % 3 == 0 ? new Color(0.95f, 0.3f, 0.5f) : cx % 3 == 1 ? new Color(0.3f, 0.8f, 0.95f) : new Color(0.98f, 0.85f, 0.3f), 0.3f));
                    if (open && !nearSpawn && rnd.NextDouble() < 0.12) { Vector3 p = lc + new Vector3(0.9f, 0, 0.9f); cloth.Box(p + Vector3.up * 0.4f, new Vector3(1.8f, 0.8f, 1.0f)); cake.Box(p + Vector3.up * 0.95f, new Vector3(0.4f, 0.3f, 0.4f)); x.Col("BR_table_player", p + Vector3.up * 0.4f, new Vector3(1.8f, 0.8f, 1.0f)); }
                    if ((cx + cy) % 3 == 0) strm.Box(lc + new Vector3(0, H - 0.25f - x.R() * 0.2f, 0), new Vector3(C, 0.01f, 0.18f));
                    if (rnd.NextDouble() < 0.06) { var bl = Prim(PrimitiveType.Sphere, x.T, lc + new Vector3((x.R() - 0.5f) * 3f, H - 0.9f - x.R() * 0.8f, (x.R() - 0.5f) * 3f), new Vector3(0.5f, 0.6f, 0.5f), Color.HSVToRGB(x.R(), 0.7f, 0.95f), "BR_balloon_player"); }
                    break;
                }
                case "37":
                {   // тёмная вода — опасные пятна на поверхности (круглые лужи с неровным краем)
                    var dark = x.M("dwater", () => BrGfx.Surf(null, new Color(0.02f, 0.07f, 0.06f), 0.97f));
                    float p2 = Mathf.PerlinNoise(cx * 0.2f + 40f + (b.seed % 17), cy * 0.2f + 9f);
                    if (nearSpawn) break;
                    if (p2 > 0.72f && b.g.dist[cx, cy] > 6) { dark.Puddle(lc + Vector3.up * (Water.Y + 0.03f), 1.9f, 1.9f, x.R() * 180f, cx * 977 + cy * 31 + b.seed); b.hazards.Add(new Hazard { pos = cc + Vector3.up * 0.3f, r = 1.9f, kind = 1 }); }
                    break;
                }
                case "!":
                {
                    var door = x.M("rdoor", () => BrGfx.Surf(null, new Color(0.5f, 0.55f, 0.52f), 0.3f)); var blood = x.M("blood", () => BrGfx.Surf(null, new Color(0.25f, 0.02f, 0.02f), 0.8f)); var gur = x.M("gurney", () => BrGfx.Surf(null, new Color(0.7f, 0.72f, 0.72f), 0.5f, 0.5f));
                    for (int d = 0; d < 4; d++) { if (b.g.Pass(cx, cy, d) || (cx == b.g.sx && cy == b.g.sy)) continue; if (rnd.NextDouble() > 0.2) continue; Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); Vector3 p = lc + dd * (C * 0.5f - 0.2f); door.Box(p + Vector3.up * 1.05f, d < 2 ? new Vector3(0.06f, 2.1f, 1.2f) : new Vector3(1.2f, 2.1f, 0.06f)); }
                    if (rnd.NextDouble() < 0.12) blood.Box(lc + new Vector3((x.R() - 0.5f) * 2f, 0.012f, (x.R() - 0.5f) * 2f), new Vector3(0.8f + x.R(), 0.012f, 0.6f + x.R()));
                    if (!nearSpawn && rnd.NextDouble() < 0.07) { bool hx = b.g.Pass(cx, cy, 0); Vector3 p = lc + (hx ? new Vector3(0, 0, 1.4f) : new Vector3(1.4f, 0, 0)); Vector3 sz = hx ? new Vector3(1.9f, 0.9f, 0.7f) : new Vector3(0.7f, 0.9f, 1.9f); gur.Box(p + Vector3.up * 0.45f, sz); x.Col("BR_gurney_player", p + Vector3.up * 0.45f, sz); }
                    break;
                }
                case "7":
                {
                    var wat = x.M("sea", () => BrGfx.Surf(null, new Color(0.07f, 0.15f, 0.17f), 0.95f, 0f, new Color(0.008f, 0.03f, 0.035f)));
                    wat.Box(lc + Vector3.up * 0.2f, new Vector3(C, 0.04f, C)); if (nearSpawn) break;
                    if (open && rnd.NextDouble() < 0.12) { Prop(x, "crate7", new Color(0.35f, 0.26f, 0.15f), lc + new Vector3(x.R() > 0.5f ? 1.0f : -1.0f, 0.45f, x.R() > 0.5f ? 1.0f : -1.0f), new Vector3(1.0f, 0.9f, 1.0f), true); }
                    else if (rnd.NextDouble() < 0.08) { Prop(x, "barrel7", new Color(0.5f, 0.12f, 0.08f), lc + new Vector3((x.R() - 0.5f) * 2f, 0.5f, (x.R() - 0.5f) * 2f), new Vector3(0.6f, 1.0f, 0.6f), true); }
                    if (rnd.NextDouble() < 0.12) Prop(x, "pipe7", new Color(0.3f, 0.27f, 0.22f), lc + new Vector3(0, H - 0.4f, (x.R() - 0.5f) * 2f), new Vector3(C, 0.18f, 0.18f), false);
                    break;
                }
                case "8":
                {
                    if (nearSpawn) break; var rock = x.M("rock8", () => BrGfx.Surf(null, new Color(0.22f, 0.2f, 0.18f), 0.2f));
                    if (rnd.NextDouble() < 0.28) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 3f, 0, (x.R() - 0.5f) * 3f); float w = 0.5f + x.R() * 0.4f; rock.Box(p + Vector3.up * (H - 0.35f), new Vector3(w, 0.7f, w)); rock.Box(p + Vector3.up * (H - 0.85f), new Vector3(w * 0.6f, 0.5f, w * 0.6f)); rock.Box(p + Vector3.up * (H - 1.2f), new Vector3(w * 0.28f, 0.4f, w * 0.28f)); }
                    if (open && rnd.NextDouble() < 0.18) { Vector3 p = lc + new Vector3(x.R() > 0.5f ? 1.1f : -1.1f, 0, x.R() > 0.5f ? 1.1f : -1.1f); rock.Box(p + Vector3.up * 0.55f, new Vector3(1.1f, 1.1f, 0.9f)); rock.Box(p + Vector3.up * 1.2f, new Vector3(0.6f, 0.6f, 0.5f)); x.Col("BR_rock_player", p + Vector3.up * 0.55f, new Vector3(1.1f, 1.1f, 0.9f)); }
                    else if (rnd.NextDouble() < 0.14) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 3f, 0, (x.R() - 0.5f) * 3f); rock.Box(p + Vector3.up * 0.3f, new Vector3(0.45f, 0.6f, 0.45f)); rock.Box(p + Vector3.up * 0.7f, new Vector3(0.22f, 0.4f, 0.22f)); }
                    break;
                }
                case "9":
                {
                    if (nearSpawn) break; double q = rnd.NextDouble();
                    if (open && q < 0.1) { bool rot = rnd.Next(2) == 0; Color[] cols = { new Color(0.4f, 0.1f, 0.1f), new Color(0.15f, 0.2f, 0.32f), new Color(0.35f, 0.35f, 0.3f) }; Car(x, lc + new Vector3(rot ? 0f : (x.R() - 0.5f), 0, rot ? (x.R() - 0.5f) : 0f) , rot, cols[rnd.Next(3)]); }
                    else if (q < 0.2) Tree(x, lc + new Vector3(x.R() > 0.5f ? 1.2f : -1.2f, 0, x.R() > 0.5f ? -1.2f : 1.2f), 0.5f);
                    else if (q < 0.26) Prop(x, "mail", new Color(0.12f, 0.2f, 0.35f), lc + new Vector3(1.6f, 0.5f, -1.6f), new Vector3(0.3f, 1.0f, 0.3f), true);
                    break;
                }
                case "15":
                {
                    try { Lab(x, cx, cy, lc, open, nearSpawn); } catch (Exception le) { if (!decor3Warned) { decor3Warned = true; Plugin.Warn("Lab", le); } }
                    break;
                }
                case "94":
                {
                    try { Motion(x, cx, cy, lc, open, nearSpawn); } catch (Exception me) { if (!decor3Warned) { decor3Warned = true; Plugin.Warn("Motion", me); } }
                    break;
                }
                case "10":
                {
                    try { Wheat(x, cx, cy, lc); } catch (Exception we) { if (!decor3Warned) { decor3Warned = true; Plugin.Warn("пшеница", we); } }
                    if (nearSpawn) break; double q = rnd.NextDouble();
                    if (open && q < 0.07) { Vector3 p = lc + new Vector3(0.9f, 0, -0.9f); Prop(x, "hay", new Color(0.62f, 0.5f, 0.2f), p + Vector3.up * 0.55f, new Vector3(1.5f, 1.1f, 1.2f), true); }
                    else if (q < 0.12) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 2.4f, 0, (x.R() - 0.5f) * 2.4f); Prop(x, "pole10", new Color(0.3f, 0.22f, 0.12f), p + Vector3.up * 1.1f, new Vector3(0.1f, 2.2f, 0.1f), true); Prop(x, "pole10", new Color(0.3f, 0.22f, 0.12f), p + Vector3.up * 1.7f, new Vector3(1.4f, 0.09f, 0.09f), false); Prop(x, "hat10", new Color(0.55f, 0.45f, 0.22f), p + Vector3.up * 2.28f, new Vector3(0.38f, 0.36f, 0.38f), false); Prop(x, "shirt10", new Color(0.4f, 0.2f, 0.18f), p + Vector3.up * 1.4f, new Vector3(0.5f, 0.7f, 0.2f), false); }
                    break;
                }
                case "11":
                {
                    if (nearSpawn) break; double q = rnd.NextDouble();
                    if (open && q < 0.12) { bool rot = rnd.Next(2) == 0; Color[] cols = { new Color(0.55f, 0.45f, 0.1f), new Color(0.1f, 0.25f, 0.15f), new Color(0.3f, 0.3f, 0.34f), new Color(0.45f, 0.08f, 0.08f) }; Car(x, lc + new Vector3(rot ? 0f : 1.0f, 0, rot ? 1.0f : 0f), rot, cols[rnd.Next(4)]); }
                    else if (q < 0.2) Prop(x, "dump", new Color(0.1f, 0.3f, 0.18f), lc + new Vector3(x.R() > 0.5f ? 1.3f : -1.3f, 0.6f, x.R() > 0.5f ? 1.3f : -1.3f), new Vector3(1.8f, 1.2f, 1.0f), true);
                    else if (q < 0.27) Tree(x, lc + new Vector3(1.3f, 0, 1.3f), 0.4f);
                    else if (q < 0.33) Prop(x, "bench", new Color(0.3f, 0.2f, 0.1f), lc + new Vector3(0, 0.25f, 1.6f), new Vector3(1.5f, 0.5f, 0.45f), true);
                    break;
                }
                case "188":
                {
                    if (nearSpawn) break; double q = rnd.NextDouble();
                    if (open && q < 0.1) { Vector3 p = lc + new Vector3(x.R() > 0.5f ? 1.2f : -1.2f, 0, x.R() > 0.5f ? 1.2f : -1.2f); Prop(x, "pot", new Color(0.85f, 0.85f, 0.82f), p + Vector3.up * 0.35f, new Vector3(0.6f, 0.7f, 0.6f), true); Prop(x, "plant", new Color(0.12f, 0.35f, 0.14f), p + Vector3.up * 1.0f, new Vector3(0.7f, 0.7f, 0.7f), false); }
                    else if (open && q < 0.16) Prop(x, "bench188", new Color(0.75f, 0.72f, 0.65f), lc + new Vector3(0, 0.25f, 1.5f), new Vector3(1.6f, 0.5f, 0.5f), true);
                    break;
                }
                case "6":
                {
                    var junk = x.M("junk", () => BrGfx.Surf(null, new Color(0.12f, 0.12f, 0.14f), 0.3f));
                    if (open && !nearSpawn && rnd.NextDouble() < 0.07) { Vector3 p = lc + new Vector3(1.1f, 0, -1.1f); junk.Box(p + Vector3.up * 0.6f, new Vector3(1.0f, 1.2f, 0.8f)); x.Col("BR_junk_player", p + Vector3.up * 0.6f, new Vector3(1.0f, 1.2f, 0.8f)); }
                    break;
                }
            }
        }
    }
}
