// Level 15 «Футуристические залы» (1.31): планировка «хребет и крылья», лабораторное оборудование, брошенные тела в халатах, мёртвые Псы.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal sealed partial class Grid
    {
        /// <summary>Широкий центральный зал-«хребет» (2 клетки), от него вверх и вниз отходят крылья-коридоры с лабораториями 3×3 по бокам;
        /// концы крыльев замыкаются кольцевыми проходами у краёв карты — получаются и тупики, и обходы.</summary>
        public void GenLab(System.Random rnd)
        {
            int mid = n / 2; var used = new bool[n, n];
            for (int x = 0; x < n; x++) { used[x, mid] = true; used[x, mid - 1] = true; } for (int x = 0; x < n - 1; x++) { Link(x, mid - 1, 0); Link(x, mid, 0); }
            for (int x = 0; x < n; x++) Link(x, mid - 1, 2);
            var cols = new List<int>(); for (int wx = 4; wx < n - 4; wx += 7) cols.Add(wx);
            foreach (int wx in cols)
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    if (rnd.NextDouble() > 0.92) continue;
                    int start = dir > 0 ? mid + 1 : mid - 2, end = dir > 0 ? n - 3 : 2; if ((end - start) * dir < 3) continue;
                    // коридор крыла
                    Link(wx, dir > 0 ? mid : mid - 1, dir > 0 ? 2 : 3);
                    for (int y = start; y != end; y += dir) Link(wx, y, dir > 0 ? 2 : 3); for (int y = Mathf.Min(start, end); y <= Mathf.Max(start, end); y++) used[wx, y] = true;
                    // лаборатории по бокам, каждые 4 клетки
                    for (int ry = dir > 0 ? start + 1 : end + 1; ry + 2 <= (dir > 0 ? end - 1 : start - 1); ry += 4)
                        for (int side = -1; side <= 1; side += 2)
                        {
                            int rx = side < 0 ? wx - 3 : wx + 1; if (rx < 0 || rx + 3 > n) continue;
                            if (rnd.NextDouble() > 0.88) { MarkSolid(rx, ry, 3, 3); continue; }
                            LinkRect(rx, ry, 3, 3); MarkRoom(rx, ry, 3, 3); for (int ux = rx; ux < rx + 3; ux++) for (int uy = ry; uy < ry + 3; uy++) used[ux, uy] = true;
                            Link(side < 0 ? wx - 1 : wx + 1, ry + 1, side < 0 ? 0 : 1);
                            if (rnd.NextDouble() < 0.25) Link(side < 0 ? wx - 1 : wx + 1, ry + (rnd.Next(2) == 0 ? 0 : 2), side < 0 ? 0 : 1);
                        }
                }
            // кольцевые проходы у краёв: соединяют концы соседних крыльев
            foreach (int ry in new[] { 2, n - 3 })
                for (int i = 0; i + 1 < cols.Count; i++)
                    if (rnd.NextDouble() < 0.7) for (int x = cols[i]; x < cols[i + 1]; x++) { Link(x, ry, 0); used[x, ry] = true; used[x + 1, ry] = true; }
            // всё, что не вошло в структуру, — глухая толща (иначе Connect() прорубит там лабиринт)
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) if (!used[x, y]) MarkSolid(x, y, 1, 1);
            sx = n / 2; sy = mid;
        }
    }

    internal static partial class LevelBuilder
    {
        static void Corpse(Ctx x, Vector3 p, bool hound)
        {
            float yaw = x.R() * 360f; Quaternion q = Quaternion.Euler(0f, yaw, 0f); var blood = Mk(x, "lb_bl", 0.22f, 0.02f, 0.02f, 0.85f);
            blood.BoxY(p + Vector3.up * 0.008f, new Vector3(0.9f + x.R() * 0.8f, 0.012f, 0.8f + x.R() * 0.6f), yaw + 20f);
            if (hound)
            {
                var fur = Mk(x, "lb_hd", 0.13f, 0.1f, 0.09f, 0.1f);
                fur.BoxY(p + Vector3.up * 0.2f, new Vector3(1.0f, 0.34f, 0.42f), yaw); fur.BoxY(p + q * new Vector3(0.62f, 0.15f, 0.08f) , new Vector3(0.4f, 0.24f, 0.26f), yaw + 15f);
                for (int i = 0; i < 4; i++) { Vector3 o = new Vector3(i < 2 ? 0.35f : -0.35f, 0.08f, (i % 2 == 0 ? 1f : -1f) * 0.3f); fur.BoxY(p + q * o, new Vector3(0.45f, 0.08f, 0.09f), yaw + (x.R() - 0.5f) * 60f); }
                return;
            }
            var coat = Mk(x, "lb_co", 0.88f, 0.9f, 0.9f, 0.2f); var pants = Mk(x, "lb_pa", 0.15f, 0.17f, 0.25f, 0.2f); var skin = Mk(x, "lb_sk", 0.62f, 0.5f, 0.42f, 0.3f);
            coat.BoxY(p + Vector3.up * 0.13f, new Vector3(0.5f, 0.22f, 0.36f), yaw); skin.BoxY(p + q * new Vector3(0.38f, 0.11f, 0f), new Vector3(0.2f, 0.19f, 0.19f), yaw);
            pants.BoxY(p + q * new Vector3(-0.5f, 0.08f, 0.1f), new Vector3(0.55f, 0.15f, 0.15f), yaw + 8f); pants.BoxY(p + q * new Vector3(-0.5f, 0.08f, -0.1f), new Vector3(0.55f, 0.15f, 0.15f), yaw - 14f);
            coat.BoxY(p + q * new Vector3(0.1f, 0.08f, 0.32f), new Vector3(0.5f, 0.12f, 0.12f), yaw + 25f); coat.BoxY(p + q * new Vector3(0.15f, 0.08f, -0.32f), new Vector3(0.45f, 0.12f, 0.12f), yaw - 30f);
        }

        static void Lab(Ctx x, int cx, int cy, Vector3 lc, bool open, bool nearSpawn)
        {
            var b = x.b; var g = b.g; float C = b.C, H = b.H, T = b.L.wallT; if (nearSpawn) return;
            bool room = g.Kind(cx, cy) == 1; var rack = Mk(x, "lb_rk", 0.12f, 0.13f, 0.15f, 0.5f, 0.6f); var white = Mk(x, "lb_wh", 0.82f, 0.85f, 0.87f, 0.4f, 0.2f);
            var led = Gl(x, "lb_led", 0.2f, 0.9f, 0.5f, 2.2f); var scr = Gl(x, "lb_scr", 0.3f, 0.8f, 1f, 1.6f); var liq = Gl(x, "lb_liq", 0.3f, 1f, 0.5f, 1.2f); var glass = Mk(x, "lb_gl", 0.55f, 0.65f, 0.7f, 0.9f, 0.1f);
            var warn = Gl(x, "lb_warn", 1f, 0.2f, 0.12f, 2.4f);
            // кабельный лоток под потолком над коридором
            bool ew = g.Pass(cx, cy, 0) || g.Pass(cx, cy, 1), ns = g.Pass(cx, cy, 2) || g.Pass(cx, cy, 3);
            if (!room && ew != ns && x.rnd.NextDouble() < 0.7) rack.Box(lc + Vector3.up * (H - 0.28f), ew ? new Vector3(C, 0.06f, 0.5f) : new Vector3(0.5f, 0.06f, C));
            for (int d = 0; d < 4; d++)
            {
                if (g.Pass(cx, cy, d)) continue; Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); Vector3 rt = new Vector3(-dd.z, 0, dd.x); bool aZ = d < 2; float r = x.R();
                Vector3 p = lc + dd * (C * 0.5f - T * 0.5f - 0.42f) + rt * ((x.R() - 0.5f) * 1.6f);
                if (room && r < 0.42f)
                {   // стойка с серверами и светодиодами
                    Vector3 sz = aZ ? new Vector3(0.7f, 2.1f, 0.9f) : new Vector3(0.9f, 2.1f, 0.7f); rack.Box(p + Vector3.up * 1.05f, sz); x.Col("BR_rack_player", p + Vector3.up * 1.05f, sz);
                    for (int k = 0; k < 6; k++) led.Box(p - dd * 0.36f + Vector3.up * (0.4f + k * 0.28f) + rt * 0.15f, aZ ? new Vector3(0.02f, 0.03f, 0.35f) : new Vector3(0.35f, 0.03f, 0.02f));
                }
                else if (room && r < 0.62f)
                {   // пульт с экраном
                    Vector3 sz = aZ ? new Vector3(0.7f, 0.95f, 1.5f) : new Vector3(1.5f, 0.95f, 0.7f); white.Box(p + Vector3.up * 0.475f, sz); x.Col("BR_console_player", p + Vector3.up * 0.475f, sz);
                    scr.Box(p - dd * 0.1f + Vector3.up * 1.15f, aZ ? new Vector3(0.05f, 0.45f, 1.1f) : new Vector3(1.1f, 0.45f, 0.05f));
                }
                else if (room && r < 0.74f)
                {   // капсула с зелёной жидкостью
                    Vector3 pc = lc + dd * (C * 0.5f - T * 0.5f - 0.7f); white.Cyl(pc, pc + Vector3.up * 0.15f, 0.62f, 12); liq.Cyl(pc + Vector3.up * 0.15f, pc + Vector3.up * 1.9f, 0.42f, 12, true); glass.Cyl(pc + Vector3.up * 0.15f, pc + Vector3.up * 2.0f, 0.5f, 12, false); white.Cyl(pc + Vector3.up * 2.0f, pc + Vector3.up * 2.15f, 0.62f, 12);
                    x.Col("BR_tank_player", pc + Vector3.up * 1.1f, new Vector3(1.2f, 2.2f, 1.2f));
                }
                else if (!room && r < 0.1)
                {   // аварийная лампа на стене
                    warn.Box(lc + dd * (C * 0.5f - T * 0.5f - 0.04f) + Vector3.up * (H - 0.8f), aZ ? new Vector3(0.05f, 0.14f, 0.3f) : new Vector3(0.3f, 0.14f, 0.05f));
                }
            }
            double q = x.rnd.NextDouble(); Vector3 cp = lc + new Vector3((x.R() - 0.5f) * 2.2f, 0, (x.R() - 0.5f) * 2.2f);
            if (q < 0.045) Corpse(x, cp, false); else if (q < 0.06) Corpse(x, cp, true);
            if (x.rnd.NextDouble() < 0.03) Fx1(x, 1, lc + new Vector3((x.R() - 0.5f) * 2f, H - 0.4f, (x.R() - 0.5f) * 2f));
        }
    }
}
