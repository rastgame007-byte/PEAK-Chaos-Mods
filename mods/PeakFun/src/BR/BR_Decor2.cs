// Декор новых уровней и обновлённых планировок (1.24): мебель комнат, машины и разметка гаража, стеллажи склада,
// чердачный хлам, школьные классы, офисные «загончики», фасады улиц.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static partial class LevelBuilder
    {
        static MeshB Mk(Ctx x, string key, float r, float g, float b, float sm = 0.3f, float me = 0f) { return x.M(key, () => BrGfx.Surf(null, new Color(r, g, b), sm, me)); }
        static MeshB Gl(Ctx x, string key, float r, float g, float b, float k) { return x.M(key, () => BrGfx.Surf(null, new Color(r, g, b), 0.4f, 0f, new Color(r * k, g * k, b * k))); }

        static void Rack(Ctx x, Vector3 lc, int d, float hs, double density)
        {
            var b = x.b; float C = b.C, T = b.L.wallT; var rnd = x.rnd; bool alongZ = d < 2;
            var steel = Mk(x, "rack_s", 0.12f, 0.25f, 0.55f, 0.3f, 0.5f); var beam = Mk(x, "rack_b", 0.85f, 0.4f, 0.1f, 0.3f, 0.3f);
            var cb1 = Mk(x, "cb1", 0.58f, 0.43f, 0.26f, 0.1f); var cb2 = Mk(x, "cb2", 0.7f, 0.6f, 0.4f, 0.1f); var wrap = Mk(x, "wrap", 0.75f, 0.78f, 0.8f, 0.7f); var pal = Mk(x, "pallet", 0.4f, 0.28f, 0.16f, 0.1f);
            Vector3 pc = lc + new Vector3(Grid.DX[d], 0, Grid.DY[d]) * (C * 0.5f - T * 0.5f - 0.55f); float L = C - 0.2f; Vector3 sz = alongZ ? new Vector3(1.0f, hs, L) : new Vector3(L, hs, 1.0f);
            int levels = Mathf.Max(3, Mathf.RoundToInt(hs / 1.5f)); float step = (hs - 0.2f) / levels;
            for (int lv = 0; lv <= levels; lv++) beam.Box(pc + Vector3.up * (0.2f + lv * step), alongZ ? new Vector3(0.1f, 0.12f, L) : new Vector3(L, 0.12f, 0.1f));
            for (int sgn = -1; sgn <= 1; sgn += 2) for (int t = 0; t < 3; t++) { float o = (t - 1) * (L * 0.5f - 0.05f); Vector3 off = alongZ ? new Vector3(sgn * 0.48f, 0, o) : new Vector3(o, 0, sgn * 0.48f); steel.Box(pc + off + Vector3.up * hs * 0.5f, new Vector3(0.09f, hs, 0.09f)); }
            for (int lv = 0; lv < levels; lv++) for (int k = 0; k < 3; k++)
                {
                    if (rnd.NextDouble() > density) continue; float off = (k - 1) * (L / 3f); float bh = 0.55f + x.R() * 0.5f; float y = 0.26f + lv * step + bh * 0.5f; Vector3 bp = pc + (alongZ ? new Vector3(0, 0, off) : new Vector3(off, 0, 0)) + Vector3.up * y;
                    Vector3 bs = alongZ ? new Vector3(0.82f, bh, L / 3f - 0.12f) : new Vector3(L / 3f - 0.12f, bh, 0.82f); (rnd.NextDouble() < 0.5 ? cb1 : cb2).Box(bp, bs); if (rnd.NextDouble() < 0.25) wrap.Box(bp + Vector3.up * 0.02f, bs + new Vector3(0.03f, 0.0f, 0.03f));
                }
            x.Col("BR_rack_player", pc + Vector3.up * hs * 0.5f, sz);
        }

        static void Decor2(Ctx x, int cx, int cy, Vector3 cc, bool open, bool nearSpawn)
        {
            var b = x.b; var L = b.L; var g = b.g; float C = b.C, H = b.H; var rnd = x.rnd; Vector3 lc = cc - b.baseP; int kind = g.Kind(cx, cy); float T = L.wallT;
            switch (L.id)
            {
                case "1":
                {   // гараж: разметка, брошенные машины, трубы
                    var line = Mk(x, "pline", 0.82f, 0.8f, 0.55f, 0.1f); var oil = Mk(x, "poil", 0.03f, 0.03f, 0.035f, 0.9f); var pipe = Mk(x, "gpipe", 0.5f, 0.52f, 0.5f, 0.4f, 0.6f);
                    if (open && rnd.NextDouble() < 0.7) { line.Box(lc + new Vector3(-1.95f, 0.012f, -0.9f), new Vector3(0.1f, 0.012f, 2.2f)); if (rnd.NextDouble() < 0.5) line.Box(lc + new Vector3(0.0f, 0.012f, -1.95f), new Vector3(3.9f, 0.012f, 0.1f)); }
                    if (rnd.NextDouble() < 0.07) oil.Box(lc + new Vector3((x.R() - 0.5f) * 2f, 0.008f, (x.R() - 0.5f) * 2f), new Vector3(0.8f + x.R() * 1.2f, 0.008f, 0.8f + x.R()));
                    if (open && !nearSpawn && rnd.NextDouble() < 0.15) { Color[] cols = { new Color(0.4f, 0.1f, 0.1f), new Color(0.15f, 0.2f, 0.32f), new Color(0.35f, 0.35f, 0.3f), new Color(0.1f, 0.1f, 0.1f) }; Car(x, lc + new Vector3(0f, 0f, -1.35f), false, cols[rnd.Next(4)]); }
                    if (rnd.NextDouble() < 0.3) pipe.Box(lc + new Vector3(0, H - 0.3f, 1.4f), new Vector3(C, 0.16f, 0.16f));
                    break;
                }
                case "4":
                {   // офис: загончики-кубиклы с мониторами
                    if (!(open && !nearSpawn && (cx + cy) % 2 == 0 && rnd.NextDouble() < 0.22)) break;
                    var part = Mk(x, "pod_p", 0.38f, 0.42f, 0.5f, 0.1f); var desk = Mk(x, "pod_d", 0.5f, 0.4f, 0.28f, 0.3f); var scr = Gl(x, "pod_s", 0.12f, 0.2f, 0.18f, 0.5f); var chair = Mk(x, "pod_c", 0.08f, 0.08f, 0.1f, 0.2f);
                    Vector3 p = lc + new Vector3(1.05f, 0f, 1.05f); part.Box(p + new Vector3(0, 0.7f, -0.95f), new Vector3(2.0f, 1.4f, 0.07f)); part.Box(p + new Vector3(-0.95f, 0.7f, 0), new Vector3(0.07f, 1.4f, 2.0f)); part.Box(p + new Vector3(0.95f, 0.7f, 0), new Vector3(0.07f, 1.4f, 2.0f));
                    desk.Box(p + new Vector3(0.1f, 0.74f, -0.6f), new Vector3(1.6f, 0.05f, 0.7f)); scr.Box(p + new Vector3(0.1f, 1.0f, -0.7f), new Vector3(0.5f, 0.38f, 0.04f)); chair.Box(p + new Vector3(0.1f, 0.45f, 0.05f), new Vector3(0.5f, 0.07f, 0.5f)); chair.Box(p + new Vector3(0.1f, 0.75f, 0.28f), new Vector3(0.5f, 0.55f, 0.06f));
                    x.Col("BR_pod_player", p + Vector3.up * 0.7f, new Vector3(2.0f, 1.4f, 2.0f)); break;
                }
                case "5":
                {   // отель: кровать и тумбы в номерах, бра в коридорах
                    var wood = Mk(x, "hbed_w", 0.2f, 0.1f, 0.06f, 0.4f); var lin = Mk(x, "hbed_l", 0.75f, 0.72f, 0.65f, 0.1f); var sc = Gl(x, "hsconce", 1f, 0.8f, 0.5f, 1.6f);
                    if (kind == 1 && !nearSpawn && rnd.NextDouble() < 0.45) { Vector3 p = lc + new Vector3(0.8f, 0f, 0.8f); wood.Box(p + Vector3.up * 0.3f, new Vector3(1.0f, 0.5f, 1.9f)); lin.Box(p + Vector3.up * 0.58f, new Vector3(0.95f, 0.18f, 1.8f)); wood.Box(p + new Vector3(0, 0.9f, 0.95f), new Vector3(1.0f, 0.9f, 0.08f)); x.Col("BR_bed_player", p + Vector3.up * 0.45f, new Vector3(1.0f, 0.9f, 1.9f)); }
                    else if (kind == 0) for (int d = 0; d < 4; d++) if (!g.Pass(cx, cy, d) && rnd.NextDouble() < 0.3) { Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); sc.Box(lc + dd * (C * 0.5f - T * 0.5f - 0.05f) + Vector3.up * 2.0f, d < 2 ? new Vector3(0.1f, 0.2f, 0.22f) : new Vector3(0.22f, 0.2f, 0.1f)); }
                    break;
                }
                case "13":
                {   // апартаменты: двери с номерами в коридоре, мебель в квартирах
                    var door = Mk(x, "adoor", 0.26f, 0.16f, 0.09f, 0.35f); var frame = Mk(x, "aframe", 0.38f, 0.3f, 0.2f, 0.3f); var plate = Mk(x, "aplate", 0.8f, 0.65f, 0.2f, 0.8f, 0.9f); var sofa = Mk(x, "asofa", 0.32f, 0.3f, 0.22f, 0.1f); var table = Mk(x, "atable", 0.35f, 0.25f, 0.16f, 0.3f); var lampG = Gl(x, "alamp", 1f, 0.85f, 0.55f, 1.5f);
                    if (kind == 0) { for (int d = 0; d < 4; d++) { if (g.Pass(cx, cy, d) || nearSpawn || rnd.NextDouble() > 0.55) continue; Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); bool aZ = d < 2; Vector3 p = lc + dd * (C * 0.5f - T * 0.5f - 0.04f) + (aZ ? new Vector3(0, 0, (x.R() - 0.5f) * 1.6f) : new Vector3((x.R() - 0.5f) * 1.6f, 0, 0));
                            frame.Box(p + Vector3.up * 1.1f, aZ ? new Vector3(0.05f, 2.25f, 1.15f) : new Vector3(1.15f, 2.25f, 0.05f)); door.Box(p - dd * 0.02f + Vector3.up * 1.05f, aZ ? new Vector3(0.06f, 2.1f, 0.95f) : new Vector3(0.95f, 2.1f, 0.06f)); plate.Box(p - dd * 0.05f + Vector3.up * 1.55f, aZ ? new Vector3(0.03f, 0.08f, 0.16f) : new Vector3(0.16f, 0.08f, 0.03f)); }
                        if (rnd.NextDouble() < 0.1) lampG.Box(lc + new Vector3(0, H - 0.2f, 0), new Vector3(0.5f, 0.1f, 0.5f)); }
                    else if (!nearSpawn && rnd.NextDouble() < 0.4) { bool sf = rnd.Next(2) == 0; Vector3 p = lc + new Vector3(x.R() > 0.5f ? 1.1f : -1.1f, 0, x.R() > 0.5f ? 1.1f : -1.1f); if (sf) { sofa.Box(p + Vector3.up * 0.25f, new Vector3(1.8f, 0.5f, 0.8f)); sofa.Box(p + new Vector3(0, 0.55f, -0.35f), new Vector3(1.8f, 0.5f, 0.15f)); x.Col("BR_sofa_player", p + Vector3.up * 0.4f, new Vector3(1.8f, 0.8f, 0.8f)); } else { table.Box(p + Vector3.up * 0.4f, new Vector3(1.1f, 0.06f, 0.8f)); table.Box(p + Vector3.up * 0.2f, new Vector3(0.9f, 0.4f, 0.6f)); x.Col("BR_table_player", p + Vector3.up * 0.4f, new Vector3(1.1f, 0.8f, 0.8f)); } }
                    break;
                }
                case "52":
                {   // школа: парты рядами, доска, сломанные стулья
                    var desk = Mk(x, "sdesk", 0.55f, 0.42f, 0.25f, 0.3f); var leg = Mk(x, "sleg", 0.2f, 0.2f, 0.22f, 0.5f, 0.6f); var board = Mk(x, "sboard", 0.08f, 0.2f, 0.14f, 0.3f); var chalk = Mk(x, "schalk", 0.85f, 0.85f, 0.8f, 0.2f);
                    if (kind == 1 && !nearSpawn)
                    {
                        if ((cx + cy) % 2 == 0 && rnd.NextDouble() < 0.8) { Vector3 p = lc + new Vector3(0.9f * (x.R() > 0.5f ? 1 : -1), 0, 0.9f * (x.R() > 0.5f ? 1 : -1)); float ang = rnd.NextDouble() < 0.15 ? 0.4f : 0f; desk.Box(p + Vector3.up * 0.72f, new Vector3(1.0f, 0.05f, 0.55f)); leg.Box(p + new Vector3(-0.45f, 0.36f, 0f), new Vector3(0.05f, 0.72f, 0.5f)); leg.Box(p + new Vector3(0.45f, 0.36f, 0f), new Vector3(0.05f, 0.72f, 0.5f)); leg.Box(p + new Vector3(0f, 0.42f, 0.5f), new Vector3(0.4f, 0.04f, 0.4f)); x.Col("BR_sdesk_player", p + Vector3.up * 0.5f, new Vector3(1.0f, 1.0f, 1.2f)); if (ang > 0) { } }
                        for (int d = 0; d < 4; d++) if (!g.Pass(cx, cy, d) && rnd.NextDouble() < 0.22) { Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); bool aZ = d < 2; Vector3 p = lc + dd * (C * 0.5f - T * 0.5f - 0.04f); board.Box(p + Vector3.up * 1.6f, aZ ? new Vector3(0.04f, 1.1f, 2.6f) : new Vector3(2.6f, 1.1f, 0.04f)); if (rnd.NextDouble() < 0.7) chalk.Box(p - dd * 0.03f + Vector3.up * (1.3f + x.R() * 0.5f) + (aZ ? new Vector3(0, 0, (x.R() - 0.5f) * 1.6f) : new Vector3((x.R() - 0.5f) * 1.6f, 0, 0)), aZ ? new Vector3(0.02f, 0.05f, 0.9f) : new Vector3(0.9f, 0.05f, 0.02f)); }
                    }
                    break;
                }
                case "20":
                {   // склад: стеллажи вдоль стен, паллеты и погрузчики
                    if (nearSpawn) break;
                    for (int d = 0; d < 4; d++) if (!g.Pass(cx, cy, d) && rnd.NextDouble() < 0.7) Rack(x, lc, d, Mathf.Min(H - 0.6f, 5.8f), 0.72);
                    if (open && rnd.NextDouble() < 0.14)
                    {
                        var pal = Mk(x, "pallet", 0.4f, 0.28f, 0.16f, 0.1f); var cb = Mk(x, "cb1", 0.58f, 0.43f, 0.26f, 0.1f); var wrap = Mk(x, "wrap", 0.75f, 0.78f, 0.8f, 0.7f);
                        Vector3 p = lc + new Vector3(x.R() > 0.5f ? 0.9f : -0.9f, 0, x.R() > 0.5f ? 0.9f : -0.9f); pal.Box(p + Vector3.up * 0.08f, new Vector3(1.2f, 0.16f, 1.2f)); int hh = 1 + rnd.Next(3); for (int i = 0; i < hh; i++) cb.Box(p + Vector3.up * (0.4f + i * 0.5f), new Vector3(1.1f, 0.48f, 1.1f)); wrap.Box(p + Vector3.up * (0.16f + hh * 0.25f), new Vector3(1.14f, hh * 0.5f, 1.14f)); x.Col("BR_pallet_player", p + Vector3.up * (0.16f + hh * 0.25f), new Vector3(1.2f, hh * 0.5f + 0.16f, 1.2f));
                    }
                    else if (open && rnd.NextDouble() < 0.03)
                    {
                        var yel = Mk(x, "fork", 0.9f, 0.7f, 0.05f, 0.3f); var blk = Mk(x, "forkb", 0.05f, 0.05f, 0.06f, 0.3f); Vector3 p = lc + new Vector3(0.8f, 0, -0.8f);
                        yel.Box(p + new Vector3(0, 0.6f, 0), new Vector3(1.1f, 0.7f, 1.7f)); blk.Box(p + new Vector3(0, 1.5f, -0.1f), new Vector3(1.0f, 0.04f, 1.2f)); blk.Box(p + new Vector3(-0.5f, 1.05f, -0.1f), new Vector3(0.05f, 0.9f, 0.05f)); blk.Box(p + new Vector3(0.5f, 1.05f, -0.1f), new Vector3(0.05f, 0.9f, 0.05f));
                        blk.Box(p + new Vector3(-0.3f, 0.12f, 1.4f), new Vector3(0.08f, 0.08f, 1.3f)); blk.Box(p + new Vector3(0.3f, 0.12f, 1.4f), new Vector3(0.08f, 0.08f, 1.3f)); blk.Box(p + new Vector3(0, 1.1f, 0.95f), new Vector3(0.7f, 2.0f, 0.06f)); x.Col("BR_fork_player", p + Vector3.up * 0.9f, new Vector3(1.1f, 1.8f, 2.8f));
                    }
                    break;
                }
                case "19":
                {   // чердак: балки, коробки, мебель под простынями, висящая пыль
                    var beam = Mk(x, "abeam", 0.22f, 0.15f, 0.09f, 0.2f); var box = Mk(x, "abox", 0.45f, 0.35f, 0.22f, 0.1f); var sheet = Mk(x, "asheet", 0.62f, 0.62f, 0.58f, 0.05f); var trunk = Mk(x, "atrunk", 0.18f, 0.1f, 0.06f, 0.3f);
                    if (cx % 2 == 0) beam.Box(lc + new Vector3(0, H - 0.14f, 0), new Vector3(C, 0.26f, 0.22f)); else beam.Box(lc + new Vector3(0, H - 0.14f, 0), new Vector3(0.22f, 0.26f, C));
                    if (nearSpawn || rnd.NextDouble() > 0.3) break; Vector3 p = lc + new Vector3(x.R() > 0.5f ? 0.95f : -0.95f, 0, x.R() > 0.5f ? 0.95f : -0.95f); double q = rnd.NextDouble();
                    if (q < 0.4) { int k = 1 + rnd.Next(3); for (int i = 0; i < k; i++) { float s = 0.5f + x.R() * 0.3f; box.Box(p + new Vector3((x.R() - 0.5f) * 0.3f, s * 0.5f + i * 0.5f, (x.R() - 0.5f) * 0.3f), new Vector3(s, s, s)); } x.Col("BR_abox_player", p + Vector3.up * 0.5f, new Vector3(0.9f, 1.0f, 0.9f)); }
                    else if (q < 0.75) { sheet.Box(p + Vector3.up * 0.45f, new Vector3(1.2f, 0.9f, 0.8f)); sheet.Box(p + Vector3.up * 0.95f, new Vector3(0.9f, 0.3f, 0.6f)); x.Col("BR_sheet_player", p + Vector3.up * 0.5f, new Vector3(1.2f, 1.0f, 0.8f)); }
                    else { trunk.Box(p + Vector3.up * 0.3f, new Vector3(1.0f, 0.6f, 0.6f)); x.Col("BR_trunk_player", p + Vector3.up * 0.3f, new Vector3(1.0f, 0.6f, 0.6f)); }
                    break;
                }
                case "96":
                {   // вентиляция: решётки, ряды индикаторов, воздуховоды под потолком
                    var grate = Mk(x, "dgrate", 0.07f, 0.07f, 0.08f, 0.4f, 0.5f); var ind = Gl(x, "dind", 0.2f, 1f, 0.4f, 2.2f); var red = Gl(x, "dred", 1f, 0.15f, 0.1f, 2.2f); var duct = Mk(x, "dduct", 0.4f, 0.43f, 0.46f, 0.4f, 0.6f);
                    for (int d = 0; d < 4; d++)
                    {
                        if (g.Pass(cx, cy, d)) continue; Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); bool aZ = d < 2; Vector3 w = lc + dd * (C * 0.5f - T * 0.5f - 0.03f);
                        if (rnd.NextDouble() < 0.3) grate.Box(w + Vector3.up * (0.5f + x.R() * 0.8f), aZ ? new Vector3(0.04f, 0.5f, 0.9f) : new Vector3(0.9f, 0.5f, 0.04f));
                        if (rnd.NextDouble() < 0.18) (rnd.NextDouble() < 0.2 ? red : ind).Box(w + Vector3.up * 1.5f + (aZ ? new Vector3(0, 0, (x.R() - 0.5f) * 2f) : new Vector3((x.R() - 0.5f) * 2f, 0, 0)), aZ ? new Vector3(0.04f, 0.06f, 0.06f) : new Vector3(0.06f, 0.06f, 0.04f));
                    }
                    if (rnd.NextDouble() < 0.4) duct.Box(lc + new Vector3(0, H - 0.18f, 0), (cx + cy) % 2 == 0 ? new Vector3(C, 0.28f, 0.5f) : new Vector3(0.5f, 0.28f, C));
                    break;
                }
                case "9":
                {   // пригород: крыльцо с лампой у дверей домов
                    if (nearSpawn) break; var door = Mk(x, "pdoor", 0.28f, 0.14f, 0.1f, 0.3f); var lampW = Gl(x, "plamp", 1f, 0.72f, 0.35f, 2.4f); var step = Mk(x, "pstep", 0.4f, 0.4f, 0.4f, 0.1f);
                    for (int d = 0; d < 4; d++) if (!g.Pass(cx, cy, d) && rnd.NextDouble() < 0.16) { Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); bool aZ = d < 2; Vector3 w = lc + dd * (C * 0.5f - T * 0.5f - 0.04f); door.Box(w + Vector3.up * 1.05f, aZ ? new Vector3(0.06f, 2.1f, 1.0f) : new Vector3(1.0f, 2.1f, 0.06f)); lampW.Box(w - dd * 0.12f + Vector3.up * 2.45f, new Vector3(0.18f, 0.22f, 0.18f)); step.Box(w - dd * 0.4f + Vector3.up * 0.1f, aZ ? new Vector3(0.7f, 0.2f, 1.6f) : new Vector3(1.6f, 0.2f, 0.7f)); }
                    break;
                }
                case "11":
                {   // город: неоновые вывески на фасадах
                    if (nearSpawn) break; var n1 = Gl(x, "neon1", 1f, 0.2f, 0.5f, 2.5f); var n2 = Gl(x, "neon2", 0.2f, 0.8f, 1f, 2.5f); var n3 = Gl(x, "neon3", 1f, 0.8f, 0.2f, 2.5f);
                    for (int d = 0; d < 4; d++) if (!g.Pass(cx, cy, d) && rnd.NextDouble() < 0.12) { Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); bool aZ = d < 2; Vector3 w = lc + dd * (C * 0.5f - T * 0.5f - 0.06f) + Vector3.up * (3.0f + x.R() * 2f); var m = rnd.Next(3) == 0 ? n1 : (rnd.Next(2) == 0 ? n2 : n3); m.Box(w, aZ ? new Vector3(0.08f, 0.35f, 1.8f) : new Vector3(1.8f, 0.35f, 0.08f)); }
                    break;
                }
            }
        }
    }
}
