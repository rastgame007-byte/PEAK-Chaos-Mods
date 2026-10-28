// Детализированные модели (1.26): настоящие трубы с фланцами/кронштейнами/вентилями, рёбра воздуховода,
// трансформаторы и кабельные лотки, сталактиты и кристаллы, бассейны, чучела, мусорные баки; лужи-зеркала.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static partial class LevelBuilder
    {
        static readonly float[][] PipeCols = {
            new[]{0.46f,0.2f,0.1f,0.35f,0.55f}, new[]{0.36f,0.39f,0.4f,0.5f,0.85f}, new[]{0.12f,0.3f,0.22f,0.4f,0.5f}, new[]{0.68f,0.54f,0.14f,0.45f,0.4f}, new[]{0.14f,0.24f,0.44f,0.45f,0.6f} };
        static MeshB PipeM(Ctx x, int i) { var c = PipeCols[((i % 5) + 5) % 5]; return Mk(x, "pp" + (((i % 5) + 5) % 5), c[0], c[1], c[2], c[3], c[4]); }
        static void Fx1(Ctx x, int kind, Vector3 lp) { x.b.fx.Add(new Fx { kind = kind, pos = lp }); }

        // труба вдоль оси с фланцами на концах и кронштейнами к потолку
        static void PipeRun(Ctx x, MeshB pm, MeshB brk, Vector3 a, Vector3 b, float r, float H, bool bracket)
        {
            pm.Cyl(a, b, r, 8, false); Vector3 d = (b - a).normalized; float fl = 0.09f;
            pm.Cyl(a + d * 0.05f, a + d * (0.05f + fl), r * 1.38f, 8); Vector3 mid = (a + b) * 0.5f; if (r > 0.15f) pm.Cyl(mid - d * 0.18f, mid + d * 0.18f, r * 1.14f, 8, false);   // фланец и муфта
            if (bracket) for (int s = -1; s <= 1; s += 2) { Vector3 p = mid + d * (s * 1.1f); brk.Box(new Vector3(p.x, (H + p.y) * 0.5f, p.z), new Vector3(0.07f, H - p.y, 0.07f)); brk.Box(p + Vector3.up * (-r - 0.015f), new Vector3(Mathf.Abs(d.x) > 0.5f ? 0.1f : r * 2.3f, 0.03f, Mathf.Abs(d.z) > 0.5f ? 0.1f : r * 2.3f)); }
        }

        static void Puddles(Ctx x, int cx, int cy, Vector3 lc, bool open, bool nearSpawn)
        {
            var b = x.b; string id = b.L.id; double p;
            switch (id) { case "1": p = .13; break; case "2": p = .15; break; case "3": p = .08; break; case "8": p = .15; break; case "37": p = .0; break; case "7": p = .2; break; case "9": p = .11; break; case "11": p = .12; break; case "10": p = .07; break; case "20": p = .06; break; case "0": p = .03; break; default: p = 0; break; }
            if (p <= 0 || nearSpawn || x.rnd.NextDouble() > p) return;
            var pud = x.M("pud", () => BrGfx.Surf(null, new Color(0.02f, 0.025f, 0.03f), 0.99f, 0.85f));
            float rx = 0.45f + x.R() * 1.0f, rz = 0.45f + x.R() * 1.0f; Vector3 c = lc + new Vector3((x.R() - 0.5f) * 1.6f, 0.015f, (x.R() - 0.5f) * 1.6f);
            pud.Puddle(c, rx, rz, x.R() * 180f, cx * 977 + cy * 31 + b.seed);
            Fx1(x, 5, c); if (x.R() < 0.5f) Fx1(x, 2, c);
        }

        static void Decor3(Ctx x, int cx, int cy, Vector3 cc, bool open, bool nearSpawn)
        {
            var b = x.b; var L = b.L; var g = b.g; float C = b.C, H = b.H, T = L.wallT; var rnd = x.rnd; Vector3 lc = cc - b.baseP; int kind = g.Kind(cx, cy);
            bool ew = g.Pass(cx, cy, 0) || g.Pass(cx, cy, 1), ns = g.Pass(cx, cy, 2) || g.Pass(cx, cy, 3); float half = C * 0.5f, iw = (C - T) * 0.5f;
            Puddles(x, cx, cy, lc, open, nearSpawn);
            switch (L.id)
            {
                case "2":
                {   // тоннели: три трубы под потолком (разной толщины и цвета), настенные трубы, вентили, стояки, насосные залы
                    var brk = Mk(x, "brk", 0.14f, 0.14f, 0.15f, 0.3f, 0.5f); var rubber = Mk(x, "rub", 0.05f, 0.05f, 0.05f, 0.2f); var red = Mk(x, "valve_r", 0.65f, 0.08f, 0.06f, 0.4f, 0.4f);
                    int hv = ((cx / 3) * 7 + (cy / 3) * 3) & 15; float[] rr = { 0.2f, 0.12f, 0.08f }; float[] oz = { -0.6f, 0.0f, 0.62f };
                    if (kind == 0)
                    {
                        if (ew) for (int k = 0; k < 3; k++) { float y = H - 0.36f - k * 0.2f; PipeRun(x, PipeM(x, hv + k * 2), brk, lc + new Vector3(-half, y, oz[k] * (iw / 1.3f)), lc + new Vector3(half, y, oz[k] * (iw / 1.3f)), rr[k], H, k == 0); }
                        if (ns) for (int k = 0; k < 2; k++) { float y = H - 0.9f - k * 0.2f; float xo = (k == 0 ? -0.55f : 0.5f) * (iw / 1.3f); PipeRun(x, PipeM(x, hv + 1 + k * 3), brk, lc + new Vector3(xo, y, -half), lc + new Vector3(xo, y, half), k == 0 ? 0.16f : 0.1f, H, k == 0); }
                        // настенные трубы
                        if (ew && !ns) for (int s = -1; s <= 1; s += 2) { float y = 0.55f + ((cx / 2 + (s > 0 ? 1 : 0)) % 2) * 0.6f; float zz = s * (iw - 0.12f); var pm = PipeM(x, hv + s + 2); PipeRun(x, pm, brk, lc + new Vector3(-half, y, zz), lc + new Vector3(half, y, zz), 0.09f, H, false); for (int q = -1; q <= 1; q += 2) brk.Box(lc + new Vector3(q * 1.1f, y, zz + s * 0.09f), new Vector3(0.06f, 0.2f, 0.05f));
                            if (rnd.NextDouble() < 0.2) { Vector3 vp = lc + new Vector3((x.R() - 0.5f) * 2.4f, y + 0.2f, zz); red.Cyl(vp - Vector3.up * 0.2f, vp, 0.025f, 6, false); red.Wheel(vp, Vector3.up, 0.13f, 0.014f); } }
                        if (ns && !ew) for (int s = -1; s <= 1; s += 2) { float y = 0.6f + ((cy / 2 + (s > 0 ? 1 : 0)) % 2) * 0.55f; float xx = s * (iw - 0.12f); PipeRun(x, PipeM(x, hv + s + 3), brk, lc + new Vector3(xx, y, -half), lc + new Vector3(xx, y, half), 0.09f, H, false);
                            if (rnd.NextDouble() < 0.2) { Vector3 vp = lc + new Vector3(xx, y + 0.2f, (x.R() - 0.5f) * 2.4f); red.Cyl(vp - Vector3.up * 0.2f, vp, 0.025f, 6, false); red.Wheel(vp, Vector3.up, 0.13f, 0.014f); } }
                        if (ew && ns) for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2) if ((sx + sz + cx + cy) % 2 == 0) { var pm = PipeM(x, hv + 4); Vector3 p = lc + new Vector3(sx * (iw - 0.2f), 0f, sz * (iw - 0.2f)); PipeRun(x, pm, brk, p, p + Vector3.up * H, 0.13f, H, false); pm.Cyl(p + Vector3.up * 1.2f, p + Vector3.up * 1.3f, 0.18f, 10); }
                        if (rnd.NextDouble() < 0.05) { Vector3 sp = lc + new Vector3((x.R() - 0.5f) * 2f, H - 0.75f, (x.R() - 0.5f) * 2f); Fx1(x, 1, sp); rubber.Box(sp + Vector3.up * 0.1f, new Vector3(0.22f, 0.1f, 0.22f)); }
                        if (rnd.NextDouble() < 0.07) Fx1(x, 2, lc + new Vector3((x.R() - 0.5f) * 2f, 0.02f, (x.R() - 0.5f) * 2f));
                        // решётка в полу
                        if (ew != ns && ((cx + cy) & 1) == 0) { var gr = Mk(x, "grate", 0.07f, 0.07f, 0.075f, 0.4f, 0.5f); bool a = ew; gr.Box(lc + Vector3.up * 0.01f, a ? new Vector3(C * 0.9f, 0.02f, 0.8f) : new Vector3(0.8f, 0.02f, C * 0.9f)); var bars = Mk(x, "grate_b", 0.3f, 0.31f, 0.3f, 0.4f, 0.6f); for (int i = -4; i <= 4; i++) bars.Box(lc + (a ? new Vector3(i * 0.4f, 0.025f, 0f) : new Vector3(0f, 0.025f, i * 0.4f)), a ? new Vector3(0.04f, 0.02f, 0.8f) : new Vector3(0.8f, 0.02f, 0.04f)); }
                    }
                    else
                    {   // насосный зал
                        bool centre = g.Kind(cx - 1, cy) == 1 && g.Kind(cx + 1, cy) == 1 && g.Kind(cx, cy - 1) == 1 && g.Kind(cx, cy + 1) == 1 && !nearSpawn; var steel = Mk(x, "tank", 0.3f, 0.34f, 0.33f, 0.45f, 0.7f); var band = Mk(x, "tank_b", 0.08f, 0.09f, 0.09f, 0.4f, 0.6f);
                        if (centre)
                        {
                            steel.Cyl(lc + Vector3.up * 0.15f, lc + Vector3.up * (H - 0.5f), 1.15f, 20); for (int i = 1; i <= 4; i++) band.Cyl(lc + Vector3.up * (0.15f + i * (H - 0.65f) / 5f), lc + Vector3.up * (0.15f + i * (H - 0.65f) / 5f + 0.08f), 1.19f, 20);
                            band.Cyl(lc, lc + Vector3.up * 0.18f, 1.3f, 20); PipeM(x, 1).Cyl(lc + new Vector3(0, H - 0.5f, 0), lc + new Vector3(0, H, 0), 0.25f, 12);
                            for (int a = 0; a < 3; a++) { float an = a * 2.09f + 0.5f; Vector3 dir = new Vector3(Mathf.Cos(an), 0, Mathf.Sin(an)); var pm = PipeM(x, a + 2); pm.Cyl(lc + dir * 1.1f + Vector3.up * 0.7f, lc + dir * 1.9f + Vector3.up * 0.7f, 0.1f, 8); red.Wheel(lc + dir * 1.55f + Vector3.up * 0.95f, Vector3.up, 0.12f, 0.013f); pm.Cyl(lc + dir * 1.55f + Vector3.up * 0.7f, lc + dir * 1.55f + Vector3.up * 0.95f, 0.025f, 6, false); }
                            var gl = Gl(x, "gauge", 0.9f, 0.8f, 0.4f, 0.8f); gl.Box(lc + new Vector3(1.17f, 1.5f, 0), new Vector3(0.05f, 0.3f, 0.3f)); x.Col("BR_tank_player", lc + Vector3.up * H * 0.5f, new Vector3(2.4f, H, 2.4f)); Fx1(x, 1, lc + new Vector3(1.0f, H - 0.7f, 0.5f));
                        }
                        else if (!nearSpawn)
                        {
                            if (rnd.NextDouble() < 0.5) { Vector3 p = lc + new Vector3(((cx + cy) % 2 == 0 ? 1f : -1f) * (half - 0.7f), 0, ((cx * 3 + cy) % 2 == 0 ? 1f : -1f) * (half - 0.7f)); var drum = PipeM(x, cx + cy + 3); for (int i = 0; i < 2; i++) { Vector3 q = p + new Vector3(i * 0.62f, 0, (i & 1) * 0.1f); drum.Cyl(q, q + Vector3.up * 0.9f, 0.28f, 12); band.Cyl(q + Vector3.up * 0.3f, q + Vector3.up * 0.34f, 0.29f, 12); band.Cyl(q + Vector3.up * 0.6f, q + Vector3.up * 0.64f, 0.29f, 12); } x.Col("BR_drum_player", p + new Vector3(0.3f, 0.45f, 0.05f), new Vector3(1.2f, 0.9f, 0.6f)); }
                            else if (rnd.NextDouble() < 0.5) { Vector3 p = lc + new Vector3(x.R() * 1.6f - 0.8f, 0, x.R() * 1.6f - 0.8f); band.Box(p + Vector3.up * 0.12f, new Vector3(1.5f, 0.24f, 0.8f)); steel.Cyl(p + new Vector3(-0.5f, 0.55f, 0), p + new Vector3(0.45f, 0.55f, 0), 0.32f, 14); PipeM(x, 4).Cyl(p + new Vector3(0.45f, 0.55f, 0), p + new Vector3(0.7f, 0.55f, 0), 0.18f, 10); PipeM(x, 1).Cyl(p + new Vector3(0.7f, 0.55f, 0), p + new Vector3(0.7f, H - 0.35f, 0), 0.1f, 8); x.Col("BR_pump_player", p + Vector3.up * 0.55f, new Vector3(1.5f, 1.1f, 0.8f)); }
                        }
                        if (rnd.NextDouble() < 0.12) Fx1(x, 2, lc + new Vector3(x.R() * 2f - 1f, 0.02f, x.R() * 2f - 1f));
                    }
                    break;
                }
                case "96":
                {   // воздуховод: рёбра жёсткости, светящиеся решётки, кабели, вентиляторы в тупиках
                    var rib = Mk(x, "dr_rib", 0.3f, 0.32f, 0.33f, 0.5f, 0.8f); var rivet = Mk(x, "dr_rv", 0.5f, 0.52f, 0.52f, 0.6f, 0.9f); var slat = Mk(x, "dr_sl", 0.05f, 0.05f, 0.055f, 0.4f, 0.5f); var grl = Gl(x, "dr_gl", 0.25f, 0.55f, 0.6f, 1.1f); var cab = Mk(x, "dr_cab", 0.05f, 0.05f, 0.06f, 0.5f);
                    int passes = 0; for (int d = 0; d < 4; d++) if (g.Pass(cx, cy, d)) passes++;
                    if (passes == 0) break;
                    if (ew && !ns) { for (int i = 0; i < 4; i++) { float xo = -1.5f + i; for (int s = -1; s <= 1; s += 2) rib.Box(lc + new Vector3(xo, H * 0.5f, s * (iw - 0.04f)), new Vector3(0.1f, H, 0.08f)); rib.Box(lc + new Vector3(xo, H - 0.04f, 0), new Vector3(0.1f, 0.08f, iw * 2f)); rib.Box(lc + new Vector3(xo, 0.04f, 0), new Vector3(0.1f, 0.08f, iw * 2f)); rivet.Box(lc + new Vector3(xo, H - 0.1f, iw - 0.12f), new Vector3(0.12f, 0.04f, 0.04f)); }
                        if (rnd.NextDouble() < 0.4) { int s = rnd.NextDouble() < 0.5 ? -1 : 1; Vector3 p = lc + new Vector3((x.R() - 0.5f) * 1.2f, 1.3f, s * (iw - 0.02f)); grl.Box(p, new Vector3(1.0f, 0.62f, 0.03f)); for (int k = -2; k <= 2; k++) slat.Box(p + new Vector3(0, k * 0.11f, -s * 0.02f), new Vector3(1.02f, 0.035f, 0.04f)); }
                        cab.Cyl(lc + new Vector3(-half, H - 0.2f, -iw + 0.35f), lc + new Vector3(half, H - 0.2f, -iw + 0.35f), 0.04f, 6, false); cab.Cyl(lc + new Vector3(-half, H - 0.26f, -iw + 0.42f), lc + new Vector3(half, H - 0.26f, -iw + 0.42f), 0.03f, 6, false); }
                    else if (ns && !ew) { for (int i = 0; i < 4; i++) { float zo = -1.5f + i; for (int s = -1; s <= 1; s += 2) rib.Box(lc + new Vector3(s * (iw - 0.04f), H * 0.5f, zo), new Vector3(0.08f, H, 0.1f)); rib.Box(lc + new Vector3(0, H - 0.04f, zo), new Vector3(iw * 2f, 0.08f, 0.1f)); rib.Box(lc + new Vector3(0, 0.04f, zo), new Vector3(iw * 2f, 0.08f, 0.1f)); }
                        if (rnd.NextDouble() < 0.4) { int s = rnd.NextDouble() < 0.5 ? -1 : 1; Vector3 p = lc + new Vector3(s * (iw - 0.02f), 1.3f, (x.R() - 0.5f) * 1.2f); grl.Box(p, new Vector3(0.03f, 0.62f, 1.0f)); for (int k = -2; k <= 2; k++) slat.Box(p + new Vector3(-s * 0.02f, k * 0.11f, 0), new Vector3(0.04f, 0.035f, 1.02f)); } }
                    if (passes == 1)
                    {   // тупик: вентилятор в стене
                        int d = 0; for (int k = 0; k < 4; k++) if (g.Pass(cx, cy, k)) d = k; int wd = d ^ 1; Vector3 dir = new Vector3(Grid.DX[wd], 0, Grid.DY[wd]); Vector3 fp = lc + dir * (iw - 0.05f) + Vector3.up * (H * 0.5f); Vector3 ax = dir;
                        rib.Cyl(fp - ax * 0.05f, fp, 0.95f, 20, false); slat.Cyl(fp - ax * 0.02f, fp, 0.9f, 20); Vector3 u = Vector3.Cross(ax, Vector3.up).normalized;
                        for (int k = 0; k < 6; k++) { float an = k * 60f; Vector3 bd = Quaternion.AngleAxis(an, ax) * Vector3.up; rib.Cyl(fp - ax * 0.08f, fp - ax * 0.08f + bd * 0.8f, 0.07f, 4, false); }
                        rib.Cyl(fp - ax * 0.14f, fp - ax * 0.04f, 0.14f, 10); Fx1(x, 4, fp);
                    }
                    if (rnd.NextDouble() < 0.06) Fx1(x, 1, lc + new Vector3(0, H - 0.4f, 0));
                    break;
                }
                case "3":
                {   // электрощитовая: трансформаторы, шкафы, лотки с кабелями, висящие кабели, красные лампы
                    var tr = Mk(x, "tf_b", 0.22f, 0.3f, 0.26f, 0.4f, 0.5f); var cer = Mk(x, "tf_c", 0.8f, 0.78f, 0.7f, 0.7f); var fin = Mk(x, "tf_f", 0.18f, 0.24f, 0.21f, 0.4f, 0.5f); var yel = Mk(x, "haz_y", 0.8f, 0.65f, 0.05f, 0.3f); var blk = Mk(x, "haz_k", 0.05f, 0.05f, 0.05f, 0.3f);
                    var cab = Mk(x, "cab", 0.25f, 0.28f, 0.3f, 0.4f, 0.6f); var lamp = Gl(x, "rlamp", 1f, 0.1f, 0.05f, 2.2f); var tray = Mk(x, "tray", 0.35f, 0.36f, 0.35f, 0.5f, 0.8f); var wire = Mk(x, "wire", 0.04f, 0.04f, 0.045f, 0.5f);
                    if (open && !nearSpawn && rnd.NextDouble() < 0.18)
                    {
                        Vector3 p = lc + new Vector3((x.R() - 0.5f) * 1.2f, 0, (x.R() - 0.5f) * 1.2f); float yaw = (rnd.Next(4)) * 90f;
                        tr.BoxY(p + Vector3.up * 0.9f, new Vector3(1.5f, 1.5f, 1.0f), yaw); tr.BoxY(p + Vector3.up * 1.7f, new Vector3(1.6f, 0.1f, 1.1f), yaw); var q = Quaternion.Euler(0, yaw, 0);
                        for (int i = -1; i <= 1; i++) { Vector3 bp = p + q * new Vector3(i * 0.5f, 1.75f, 0); cer.Cyl(bp, bp + Vector3.up * 0.45f, 0.06f, 8); for (int k = 0; k < 4; k++) cer.Cyl(bp + Vector3.up * (0.08f + k * 0.1f), bp + Vector3.up * (0.12f + k * 0.1f), 0.11f, 8); cab.Cyl(bp + Vector3.up * 0.45f, bp + Vector3.up * (H - 0.3f), 0.025f, 5, false); }
                        for (int s = -1; s <= 1; s += 2) for (int i = 0; i < 6; i++) fin.BoxY(p + q * new Vector3(s * 0.78f, 0.9f, -0.5f + i * 0.2f), new Vector3(0.12f, 1.3f, 0.07f), yaw);
                        for (int i = 0; i < 6; i++) (i % 2 == 0 ? yel : blk).BoxY(p + q * new Vector3(-0.62f + i * 0.25f, 0.1f, -0.51f), new Vector3(0.25f, 0.2f, 0.03f), yaw);
                        lamp.BoxY(p + q * new Vector3(0, 1.45f, -0.51f), new Vector3(0.12f, 0.12f, 0.04f), yaw); x.Col("BR_trf_player", p + Vector3.up * 0.9f, new Vector3(1.7f, 1.8f, 1.2f)); Fx1(x, 3, p + Vector3.up * 1.9f);
                    }
                    else if (kind == 0 && (ew != ns))
                    {   // лоток с кабелями вдоль потолка
                        bool a = ew; Vector3 ta = a ? new Vector3(C, 0.06f, 0.45f) : new Vector3(0.45f, 0.06f, C); tray.Box(lc + Vector3.up * (H - 0.4f), ta);
                        for (int s = -1; s <= 1; s += 2) tray.Box(lc + Vector3.up * (H - 0.35f) + (a ? new Vector3(0, 0, s * 0.22f) : new Vector3(s * 0.22f, 0, 0)), a ? new Vector3(C, 0.1f, 0.03f) : new Vector3(0.03f, 0.1f, C));
                        for (int k = 0; k < 3; k++) { float o = (k - 1) * 0.12f; Vector3 p0 = lc + new Vector3(a ? -half : o, H - 0.34f, a ? o : -half), p1 = lc + new Vector3(a ? half : o, H - 0.34f, a ? o : half); wire.Cyl(p0, p1, 0.035f, 6, false); }
                        for (int i = -1; i <= 1; i += 2) { Vector3 p = lc + (a ? new Vector3(i * 1.2f, 0, 0) : new Vector3(0, 0, i * 1.2f)); tray.Box(new Vector3(p.x, H - 0.2f, p.z), new Vector3(0.05f, 0.4f, 0.05f)); }
                        if (rnd.NextDouble() < 0.4) { int s = rnd.NextDouble() < 0.5 ? -1 : 1; Vector3 p = lc + (a ? new Vector3((x.R() - 0.5f) * 2f, 0, s * (iw - 0.3f)) : new Vector3(s * (iw - 0.3f), 0, (x.R() - 0.5f) * 2f)); cab.Box(p + Vector3.up * 1.0f, a ? new Vector3(0.8f, 2.0f, 0.5f) : new Vector3(0.5f, 2.0f, 0.8f)); lamp.Box(p + new Vector3(0, 2.05f, 0), new Vector3(0.1f, 0.08f, 0.1f)); x.Col("BR_cab_player", p + Vector3.up, a ? new Vector3(0.8f, 2f, 0.5f) : new Vector3(0.5f, 2f, 0.8f)); }
                    }
                    if (rnd.NextDouble() < 0.14)
                    {   // свисающий провисший кабель
                        Vector3 p = lc + new Vector3((x.R() - 0.5f) * 2.4f, 0, (x.R() - 0.5f) * 2.4f); Vector3 prev = p + Vector3.up * H; for (int i = 1; i <= 6; i++) { float t = i / 6f; Vector3 q = p + new Vector3(Mathf.Sin(t * 3f + cx) * 0.15f, H - t * (H - 0.45f), 0); wire.Cyl(prev, q, 0.03f, 5, false); prev = q; }
                        Fx1(x, 3, prev);
                    }
                    break;
                }
                case "8":
                {   // пещера: сталактиты/сталагмиты, кристаллы, валуны
                    var rock = Mk(x, "cv_r", 0.3f, 0.28f, 0.26f, 0.25f); var wet = Mk(x, "cv_w", 0.22f, 0.22f, 0.24f, 0.7f); var cry = Gl(x, "cv_c", 0.2f, 0.55f, 0.95f, 1.3f); var cry2 = Gl(x, "cv_c2", 0.6f, 0.25f, 0.9f, 1.1f);
                    int nst = 1 + rnd.Next(3); for (int i = 0; i < nst; i++) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * C * 0.85f, 0, (x.R() - 0.5f) * C * 0.85f); float len = 0.5f + x.R() * Mathf.Max(0.4f, H - 2.2f), r = 0.1f + x.R() * 0.22f; (i % 2 == 0 ? rock : wet).Cone(p + Vector3.up * H, p + Vector3.up * (H - len), r, 7); if (i == 0 && rnd.NextDouble() < 0.35) Fx1(x, 2, p + new Vector3(0, 0.02f, 0)); }
                    if (rnd.NextDouble() < 0.5) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * C * 0.85f, 0, (x.R() - 0.5f) * C * 0.85f); float len = 0.4f + x.R() * 0.9f, r = 0.1f + x.R() * 0.2f; rock.Cone(p, p + Vector3.up * len, r, 7); }
                    if (!nearSpawn && rnd.NextDouble() < 0.09) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 2.4f, 0, (x.R() - 0.5f) * 2.4f); var cm = rnd.NextDouble() < 0.7 ? cry : cry2; for (int i = 0; i < 5; i++) { Vector3 tilt = new Vector3((x.R() - 0.5f) * 0.9f, 1f, (x.R() - 0.5f) * 0.9f).normalized; Vector3 bp = p + new Vector3((x.R() - 0.5f) * 0.4f, 0, (x.R() - 0.5f) * 0.4f); cm.Cone(bp, bp + tilt * (0.35f + x.R() * 0.7f), 0.07f + x.R() * 0.07f, 6); } }
                    if (!nearSpawn && rnd.NextDouble() < 0.3) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 2.8f, 0, (x.R() - 0.5f) * 2.8f); float s = 0.5f + x.R() * 1.1f; rock.Blob(p + Vector3.up * s * 0.3f, new Vector3(s * 1.2f, s * 0.8f, s), cx * 131 + cy * 17); if (s > 0.8f) x.Col("BR_boulder_player", p + Vector3.up * s * 0.35f, new Vector3(s, s * 0.7f, s)); }
                    break;
                }
                case "37":
                {   // бассейны (затоплены по колено): поручни-лестницы у стен, дорожки с поплавками, надувные «макаронины», капли
                    var chrome = Mk(x, "pl_c", 0.75f, 0.77f, 0.8f, 0.9f, 1f); var rope = Mk(x, "pl_r", 0.78f, 0.78f, 0.8f, 0.5f); var bR = Mk(x, "pl_b1", 0.85f, 0.1f, 0.08f, 0.5f); var bW = Mk(x, "pl_b2", 0.93f, 0.93f, 0.9f, 0.5f); var noodle = Mk(x, "pl_n", 0.95f, 0.5f, 0.1f, 0.5f);
                    float wy = Water.Y + 0.035f;
                    if (!nearSpawn) for (int d = 0; d < 4; d++)
                    {
                        if (g.Pass(cx, cy, d) || rnd.NextDouble() > 0.07) continue; Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); Vector3 pp = new Vector3(dd.z, 0, dd.x);
                        Vector3 b0 = lc + dd * (half - T * 0.5f - 0.18f) + pp * -0.3f, b1 = lc + dd * (half - T * 0.5f - 0.18f) + pp * 0.3f;
                        chrome.Cyl(b0, b0 + Vector3.up * 1.15f, 0.022f, 6, false); chrome.Cyl(b1, b1 + Vector3.up * 1.15f, 0.022f, 6, false);
                        foreach (float yy in new[] { 0.55f, 0.85f, 1.15f }) chrome.Cyl(b0 + Vector3.up * yy, b1 + Vector3.up * yy, 0.018f, 6, false);
                    }
                    if (open && !nearSpawn && rnd.NextDouble() < 0.2)
                    {   // разделительная дорожка с поплавками
                        bool alongX = rnd.Next(2) == 0; Vector3 ax = alongX ? Vector3.right : Vector3.forward; float off = (x.R() - 0.5f) * 1.2f; Vector3 pp = alongX ? Vector3.forward : Vector3.right;
                        Vector3 a = lc + pp * off - ax * half + Vector3.up * wy, b2 = lc + pp * off + ax * half + Vector3.up * wy; rope.Cyl(a, b2, 0.01f, 5, false);
                        for (int k = 0; k < 10; k++) { Vector3 bp = Vector3.Lerp(a, b2, (k + 0.5f) / 10f); (k % 2 == 0 ? bR : bW).Box(bp, alongX ? new Vector3(0.28f, 0.09f, 0.09f) : new Vector3(0.09f, 0.09f, 0.28f)); }
                    }
                    else if (open && !nearSpawn && rnd.NextDouble() < 0.09)
                    {   // плавающая «макаронина»
                        Vector3 p = lc + new Vector3((x.R() - 0.5f) * 2f, wy, (x.R() - 0.5f) * 2f); Vector3 ax = Quaternion.Euler(0, x.R() * 180f, 0) * Vector3.right; noodle.Cyl(p - ax * 0.8f, p + ax * 0.8f, 0.045f, 6, true);
                    }
                    if (rnd.NextDouble() < 0.25) Fx1(x, 2, lc + new Vector3((x.R() - 0.5f) * 2.5f, Water.Y + 0.02f, (x.R() - 0.5f) * 2.5f));
                    break;
                }
                case "6":
                {   // заброшенный зал: столы, стулья, шкафы
                    if (!(open && !nearSpawn && rnd.NextDouble() < 0.13)) break;
                    var wood = Mk(x, "dk_w", 0.18f, 0.12f, 0.08f, 0.3f); var lgt = Mk(x, "dk_l", 0.28f, 0.2f, 0.12f, 0.3f); Vector3 p = lc + new Vector3((x.R() - 0.5f) * 1.6f, 0, (x.R() - 0.5f) * 1.6f); float yaw = x.R() * 180f;
                    if (rnd.NextDouble() < 0.5) { lgt.BoxY(p + Vector3.up * 0.75f, new Vector3(1.4f, 0.06f, 0.8f), yaw); var q = Quaternion.Euler(0, yaw, 0); for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2) wood.Cyl(p + q * new Vector3(sx * 0.62f, 0, sz * 0.32f), p + q * new Vector3(sx * 0.62f, 0.75f, sz * 0.32f), 0.035f, 5); x.Col("BR_desk_player", p + Vector3.up * 0.4f, new Vector3(1.4f, 0.8f, 0.8f)); for (int c2 = 0; c2 < 2; c2++) { Vector3 cp = p + q * new Vector3(-0.3f + c2 * 0.7f, 0, 0.75f); lgt.BoxY(cp + Vector3.up * 0.45f, new Vector3(0.42f, 0.04f, 0.42f), yaw + c2 * 20f); for (int k = 0; k < 4; k++) wood.Cyl(cp + new Vector3(((k & 1) - 0.5f) * 0.36f, 0, ((k >> 1) - 0.5f) * 0.36f), cp + new Vector3(((k & 1) - 0.5f) * 0.36f, 0.45f, ((k >> 1) - 0.5f) * 0.36f), 0.02f, 4); wood.BoxY(cp + q * new Vector3(0, 0.7f, 0.2f), new Vector3(0.42f, 0.4f, 0.03f), yaw + c2 * 20f); } }
                    else { wood.BoxY(p + Vector3.up * 1.0f, new Vector3(1.0f, 2.0f, 0.5f), yaw); lgt.BoxY(p + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 1.0f, -0.26f), new Vector3(0.04f, 1.9f, 0.02f), yaw); x.Col("BR_wardrobe_player", p + Vector3.up, new Vector3(1.0f, 2.0f, 0.5f)); }
                    break;
                }
                case "7":
                {   // затопленный город-порт: бочки, буйки, коряги
                    if (nearSpawn) break; var barrel = Mk(x, "bar", 0.55f, 0.1f, 0.08f, 0.4f, 0.3f); var buoy = Mk(x, "buoy", 0.9f, 0.4f, 0.05f, 0.4f); var log = Mk(x, "log", 0.25f, 0.17f, 0.1f, 0.2f);
                    if (rnd.NextDouble() < 0.08) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 2.5f, 0.12f, (x.R() - 0.5f) * 2.5f); Vector3 ax = new Vector3(x.R() - 0.5f, 0.1f, x.R() - 0.5f).normalized; barrel.Cyl(p - ax * 0.45f, p + ax * 0.45f, 0.28f, 12); }
                    if (rnd.NextDouble() < 0.06) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 2.5f, 0.2f, (x.R() - 0.5f) * 2.5f); buoy.Blob(p, new Vector3(0.6f, 0.6f, 0.6f), cx + cy * 7, 0.05f); }
                    if (rnd.NextDouble() < 0.07) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 2.5f, 0.1f, (x.R() - 0.5f) * 2.5f); Vector3 ax = new Vector3(x.R() - 0.5f, 0.05f, x.R() - 0.5f).normalized; log.Cyl(p - ax * 1.3f, p + ax * 1.3f, 0.12f, 7); }
                    break;
                }
                case "10":
                {   // поле: чучела и стога
                    if (!(open && !nearSpawn)) break; var wood = Mk(x, "sc_w", 0.25f, 0.16f, 0.09f, 0.2f); var sack = Mk(x, "sc_s", 0.55f, 0.45f, 0.28f, 0.1f); var coat = Mk(x, "sc_c", 0.15f, 0.12f, 0.1f, 0.1f); var hay = Mk(x, "hay", 0.72f, 0.6f, 0.25f, 0.1f);
                    if (rnd.NextDouble() < 0.025) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 2f, 0, (x.R() - 0.5f) * 2f); wood.Cyl(p, p + Vector3.up * 2.1f, 0.05f, 6); wood.Cyl(p + new Vector3(-0.8f, 1.65f, 0), p + new Vector3(0.8f, 1.65f, 0), 0.04f, 6); sack.Blob(p + Vector3.up * 1.95f, new Vector3(0.34f, 0.4f, 0.34f), cx * 7 + cy); coat.BoxY(p + Vector3.up * 1.35f, new Vector3(0.45f, 0.6f, 0.22f), x.R() * 20f); coat.Cyl(p + new Vector3(-0.8f, 1.65f, 0), p + new Vector3(-0.8f, 1.2f, 0), 0.02f, 4, false); x.Col("BR_scare_player", p + Vector3.up, new Vector3(0.3f, 2f, 0.3f)); }
                    else if (rnd.NextDouble() < 0.04) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 2f, 0.5f, (x.R() - 0.5f) * 2f); Vector3 ax = Quaternion.Euler(0, x.R() * 180f, 0) * Vector3.right; hay.Cyl(p - ax * 0.55f, p + ax * 0.55f, 0.5f, 14); x.Col("BR_hay_player", p, new Vector3(1.1f, 1.0f, 1.0f)); }
                    break;
                }
                case "11":
                {   // город: мусорные баки, конусы, гидранты
                    if (!open || nearSpawn) break; var dump = Mk(x, "dmp", 0.12f, 0.28f, 0.16f, 0.3f, 0.2f); var lid = Mk(x, "dmp_l", 0.06f, 0.08f, 0.07f, 0.3f); var cone = Mk(x, "cone", 0.95f, 0.38f, 0.05f, 0.4f); var hyd = Mk(x, "hyd", 0.7f, 0.08f, 0.06f, 0.4f, 0.3f);
                    if (rnd.NextDouble() < 0.06) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 2f, 0, (x.R() - 0.5f) * 2f); float yw = (rnd.Next(2)) * 90f; dump.BoxY(p + Vector3.up * 0.55f, new Vector3(1.8f, 1.1f, 0.9f), yw); lid.BoxY(p + Vector3.up * 1.13f, new Vector3(1.85f, 0.06f, 0.95f), yw); x.Col("BR_dumpster_player", p + Vector3.up * 0.55f, yw > 45 ? new Vector3(0.9f, 1.1f, 1.8f) : new Vector3(1.8f, 1.1f, 0.9f)); }
                    if (rnd.NextDouble() < 0.07) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 3f, 0, (x.R() - 0.5f) * 3f); cone.Cone(p, p + Vector3.up * 0.7f, 0.16f, 10); lid.Box(p + Vector3.up * 0.01f, new Vector3(0.4f, 0.02f, 0.4f)); }
                    if (rnd.NextDouble() < 0.035) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 3f, 0, (x.R() - 0.5f) * 3f); hyd.Cyl(p, p + Vector3.up * 0.7f, 0.1f, 8); hyd.Cyl(p + new Vector3(-0.14f, 0.5f, 0), p + new Vector3(0.14f, 0.5f, 0), 0.05f, 6); hyd.Cyl(p + Vector3.up * 0.68f, p + Vector3.up * 0.78f, 0.075f, 8); }
                    break;
                }
                case "9":
                {   // пригород: почтовые ящики и мусорные баки
                    if (!open || nearSpawn) break; var post = Mk(x, "mb_p", 0.2f, 0.14f, 0.1f, 0.2f); var box = Mk(x, "mb_b", 0.15f, 0.2f, 0.35f, 0.4f, 0.2f); var can = Mk(x, "can", 0.2f, 0.22f, 0.22f, 0.5f, 0.5f);
                    if (rnd.NextDouble() < 0.05) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 3f, 0, (x.R() - 0.5f) * 3f); post.Cyl(p, p + Vector3.up * 1.1f, 0.04f, 6); box.BoxY(p + Vector3.up * 1.2f, new Vector3(0.22f, 0.2f, 0.45f), x.R() * 90f); }
                    if (rnd.NextDouble() < 0.05) { Vector3 p = lc + new Vector3((x.R() - 0.5f) * 3f, 0, (x.R() - 0.5f) * 3f); can.Cyl(p, p + Vector3.up * 0.85f, 0.26f, 12); can.Cyl(p + Vector3.up * 0.85f, p + Vector3.up * 0.9f, 0.29f, 12); }
                    break;
                }
            }
        }
    }
}
